using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Runs one in-flight overview scan per API process, shares it across concurrent
/// callers, caps concurrency, caches results, and enforces an overall budget. A caller's
/// cancellation stops that caller's wait without cancelling the shared scan. Process
/// shutdown may cancel the scan.
/// </summary>
public sealed class HttpEndpointScanCoordinator
{
    private const int MaxConcurrency = 20;

    private readonly HttpEndpointChecker _checker;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly object _gate = new();

    private Task<HttpEndpointScanResult>? _inFlight;
    private HttpEndpointScanResult? _cached;
    private DateTimeOffset _cachedAtUtc;

    public HttpEndpointScanCoordinator(HttpEndpointChecker checker, IHostApplicationLifetime lifetime)
    {
        _checker = checker;
        _lifetime = lifetime;
    }

    /// <summary>How long a completed scan is reused. Defaults to 15 seconds.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Overall scan budget. Unfinished checks map to timeout. Defaults to 12 seconds.</summary>
    public TimeSpan ScanBudget { get; set; } = TimeSpan.FromSeconds(12);

    public async Task<HttpEndpointScanResult> GetScanAsync(
        HttpEndpointConfiguration configuration,
        CancellationToken callerToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Task<HttpEndpointScanResult> scanTask;
        lock (_gate)
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _cachedAtUtc < CacheDuration)
            {
                return _cached;
            }

            if (_inFlight is null)
            {
                var completion = new TaskCompletionSource<HttpEndpointScanResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _inFlight = completion.Task;

                // Start the scan; it clears _inFlight and populates the cache when done.
                _ = RunScanAsync(configuration, completion);
                scanTask = completion.Task;
            }
            else
            {
                scanTask = _inFlight;
            }
        }

        // A caller's cancellation stops only this caller's wait; the shared scan keeps running.
        return await scanTask.WaitAsync(callerToken).ConfigureAwait(false);
    }

    private async Task RunScanAsync(
        HttpEndpointConfiguration configuration,
        TaskCompletionSource<HttpEndpointScanResult> completion)
    {
        try
        {
            using var budgetSource = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            budgetSource.CancelAfter(ScanBudget);
            var token = budgetSource.Token;

            var outcomes = new ConcurrentDictionary<string, EndpointCheckOutcome>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var throttle = new SemaphoreSlim(MaxConcurrency);
                var tasks = configuration.Endpoints
                    .Select(definition => RunOneAsync(definition, throttle, token, outcomes))
                    .ToArray();

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Overall budget elapsed; unfinished checks are mapped below.
            }

            foreach (var definition in configuration.Endpoints)
            {
                outcomes.TryAdd(
                    definition.Id,
                    EndpointCheckOutcome.TimedOut(definition.Id, DateTimeOffset.UtcNow, definition.Timeout));
            }

            var result = new HttpEndpointScanResult(
                outcomes.Values.OrderBy(outcome => outcome.EndpointId, StringComparer.OrdinalIgnoreCase).ToArray());

            lock (_gate)
            {
                _cached = result;
                _cachedAtUtc = DateTimeOffset.UtcNow;
                _inFlight = null;
            }

            completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _inFlight = null;
            }

            completion.TrySetException(exception);
        }
    }

    private async Task RunOneAsync(
        HttpEndpointDefinition definition,
        SemaphoreSlim throttle,
        CancellationToken token,
        ConcurrentDictionary<string, EndpointCheckOutcome> outcomes)
    {
        try
        {
            await throttle.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var outcome = await _checker.CheckAsync(definition, token).ConfigureAwait(false);
                outcomes[definition.Id] = outcome;
            }
            finally
            {
                throttle.Release();
            }
        }
        catch (OperationCanceledException)
        {
            outcomes[definition.Id] = EndpointCheckOutcome.TimedOut(definition.Id, DateTimeOffset.UtcNow, definition.Timeout);
        }
    }
}
