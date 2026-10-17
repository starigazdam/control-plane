namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Fetches <c>GET /capabilities</c> once per plugin synchronously, because the
/// container does not exist yet during registration. A non-2xx response or a body
/// that is not valid capabilities is <see cref="ExternalDiscoveryStatus.InvalidCapabilities"/>;
/// a timeout or transport failure is <see cref="ExternalDiscoveryStatus.Unreachable"/>.
/// </summary>
internal static class ExternalPluginDiscoveryClient
{
    public static ExternalPluginDiscovery Discover(
        HttpClient client,
        ExternalPluginDefinition definition,
        TimeSpan timeout)
    {
        var observedAt = DateTimeOffset.UtcNow;
        using var timeoutSource = new CancellationTokenSource(timeout);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                ExternalEndpointUri.Capabilities(definition.BaseUri));
            using var response = client.Send(
                request,
                HttpCompletionOption.ResponseContentRead,
                timeoutSource.Token);

            var body = response.Content.ReadAsStringAsync(timeoutSource.Token).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                return InvalidCapabilities(definition, observedAt);
            }

            var parsed = ExternalCapabilitiesParser.Parse(body);
            if (!parsed.IsValid)
            {
                return InvalidCapabilities(definition, observedAt);
            }

            return new ExternalPluginDiscovery(
                definition.Name,
                definition.BaseUri,
                ExternalDiscoveryStatus.Succeeded,
                parsed.StatusProviderIds,
                parsed.Operations,
                parsed.Exclusions,
                observedAt);
        }
        catch (OperationCanceledException)
        {
            return Unreachable(definition, observedAt);
        }
        catch (HttpRequestException)
        {
            return Unreachable(definition, observedAt);
        }
        catch (Exception)
        {
            return Unreachable(definition, observedAt);
        }
    }

    private static ExternalPluginDiscovery Unreachable(ExternalPluginDefinition definition, DateTimeOffset observedAt)
        => new(
            definition.Name,
            definition.BaseUri,
            ExternalDiscoveryStatus.Unreachable,
            [],
            [],
            [],
            observedAt);

    private static ExternalPluginDiscovery InvalidCapabilities(ExternalPluginDefinition definition, DateTimeOffset observedAt)
        => new(
            definition.Name,
            definition.BaseUri,
            ExternalDiscoveryStatus.InvalidCapabilities,
            [],
            [],
            [],
            observedAt);
}
