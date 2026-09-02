using ControlPlane.Api.Configuration;
using ControlPlane.Api.Contracts;
using ControlPlane.Api.Persistence;
using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ControlPlane.Api.Services;

public sealed class ControlPlaneWorkbenchService
{
    private const string PlaceholderProjectId = "placeholder";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<IStatusProvider> _statusProviders;
    private readonly IReadOnlyDictionary<string, IOperation> _operationsById;
    private readonly ControlPlaneDbContext _dbContext;
    private readonly IOptions<ProjectSettings> _projectSettings;

    public ControlPlaneWorkbenchService(
        IEnumerable<IStatusProvider> statusProviders,
        IEnumerable<IOperation> operations,
        ControlPlaneDbContext dbContext,
        IOptions<ProjectSettings> projectSettings)
    {
        _statusProviders = statusProviders.ToArray();
        _operationsById = operations.ToDictionary(operation => operation.Definition.Id, StringComparer.OrdinalIgnoreCase);
        _dbContext = dbContext;
        _projectSettings = projectSettings;
    }

    public async Task<OverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var project = await EnsurePlaceholderProjectAsync(cancellationToken);
        var statusSnapshots = await CollectSnapshotsAsync(project, cancellationToken);
        var statusLevel = GetWorstLevel(statusSnapshots.Select(snapshot => snapshot.OverallLevel));
        var activeAlerts = statusSnapshots.Sum(snapshot => snapshot.Signals.Count(signal => signal.Level is StatusLevel.Warning or StatusLevel.Critical));

        return new OverviewResponse(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Projects:
            [
                new OverviewProjectSummary(
                    ProjectId: project.Id,
                    ProjectName: project.Name,
                    StatusLevel: statusLevel,
                    ActiveAlerts: activeAlerts,
                    AvailableOperations: _operationsById.Count)
            ]);
    }

    public async Task<ProjectDetailsResponse?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = await EnsurePlaceholderProjectAsync(cancellationToken);
        if (!string.Equals(projectId, project.Id, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var statusSnapshots = await CollectSnapshotsAsync(project, cancellationToken);
        var statusLevel = GetWorstLevel(statusSnapshots.Select(snapshot => snapshot.OverallLevel));

        return new ProjectDetailsResponse(
            Project: project,
            StatusLevel: statusLevel,
            StatusSnapshots: statusSnapshots,
            Operations: _operationsById.Values.Select(operation => operation.Definition).ToArray());
    }

    public async Task<OperationHistoryEntry> ExecuteAsync(ExecuteOperationRequest request, CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var initiatedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? "local-engineer" : request.RequestedBy.Trim();
        var operation = ResolveOperation(request.OperationId);
        var context = BuildExecutionContext(request, requestedAtUtc, initiatedBy);

        if (operation is null)
        {
            var rejected = CreateRejectedResult(
                message: $"Unknown operation '{request.OperationId}'.",
                requestedAtUtc: requestedAtUtc,
                errorCode: "operation_not_found");

            var missingOperationEntry = new OperationHistoryEntry(
                request.ProjectId,
                request.OperationId,
                initiatedBy,
                requestedAtUtc,
                request.CorrelationId,
                rejected);

            await PersistOperationExecutionAsync(missingOperationEntry, request.Input, cancellationToken);
            return missingOperationEntry;
        }

        var validationErrors = await operation.ValidateAsync(context, cancellationToken);
        if (validationErrors.Count > 0)
        {
            var rejected = CreateRejectedResult(
                message: string.Join("; ", validationErrors),
                requestedAtUtc: requestedAtUtc,
                errorCode: "operation_validation_failed");

            var validationFailedEntry = new OperationHistoryEntry(
                request.ProjectId,
                request.OperationId,
                initiatedBy,
                requestedAtUtc,
                request.CorrelationId,
                rejected);

            await PersistOperationExecutionAsync(validationFailedEntry, request.Input, cancellationToken);
            return validationFailedEntry;
        }

        var result = await operation.ExecuteAsync(context, cancellationToken);
        var entry = new OperationHistoryEntry(
            request.ProjectId,
            request.OperationId,
            initiatedBy,
            requestedAtUtc,
            request.CorrelationId,
            result);

        await PersistOperationExecutionAsync(entry, request.Input, cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<OperationHistoryEntry>> GetExecutionHistoryAsync(int take, CancellationToken cancellationToken)
    {
        var limitedTake = Math.Clamp(take, 1, 200);
        var entries = await _dbContext.OperationExecutions
            .AsNoTracking()
            .OrderByDescending(execution => execution.RequestedAtUtc)
            .Take(limitedTake)
            .ToListAsync(cancellationToken);

        return entries.Select(ToHistoryEntry).ToArray();
    }

    private static OperationExecutionContext BuildExecutionContext(
        ExecuteOperationRequest request,
        DateTimeOffset requestedAtUtc,
        string initiatedBy)
    {
        var input = request.Input ?? new Dictionary<string, string?>();
        return new OperationExecutionContext(
            Request: new OperationRequest(request.ProjectId, request.OperationId, input),
            InitiatedBy: initiatedBy,
            RequestedAtUtc: requestedAtUtc,
            CorrelationId: request.CorrelationId);
    }

    private static OperationExecutionResult CreateRejectedResult(
        string message,
        DateTimeOffset requestedAtUtc,
        string errorCode)
    {
        return new OperationExecutionResult(
            Status: OperationExecutionStatus.Rejected,
            Message: message,
            StartedAtUtc: requestedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            ErrorCode: errorCode,
            Output: new Dictionary<string, string?>());
    }

    private async Task<Project> EnsurePlaceholderProjectAsync(CancellationToken cancellationToken)
    {
        var configuredName = _projectSettings.Value.Name;
        var projectName = string.IsNullOrWhiteSpace(configuredName)
            ? ProjectSettings.DefaultName
            : configuredName.Trim();

        var existing = await _dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == PlaceholderProjectId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Name, projectName, StringComparison.Ordinal))
            {
                existing.Name = projectName;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return ToProject(existing);
        }

        var entity = new ProjectEntity
        {
            Id = PlaceholderProjectId,
            Name = projectName,
            TagsJson = JsonSerializer.Serialize(new[] { "control-plane", "draft" }, SerializerOptions),
            EnvironmentsJson = JsonSerializer.Serialize(new[] { "dev" }, SerializerOptions)
        };

        _dbContext.Projects.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToProject(entity);
    }

    private static Project ToProject(ProjectEntity entity)
    {
        return new Project(
            Id: entity.Id,
            Name: entity.Name,
            Tags: DeserializeArray(entity.TagsJson),
            Environments: DeserializeArray(entity.EnvironmentsJson));
    }

    private static OperationHistoryEntry ToHistoryEntry(OperationExecutionEntity entity)
    {
        var status = Enum.TryParse<OperationExecutionStatus>(entity.Status, ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : OperationExecutionStatus.Failed;

        var result = new OperationExecutionResult(
            Status: status,
            Message: entity.Message,
            StartedAtUtc: entity.StartedAtUtc,
            CompletedAtUtc: entity.CompletedAtUtc,
            ErrorCode: entity.ErrorCode,
            Output: DeserializeMap(entity.OutputJson));

        return new OperationHistoryEntry(
            ProjectId: entity.ProjectId,
            OperationId: entity.OperationId,
            InitiatedBy: entity.InitiatedBy,
            RequestedAtUtc: entity.RequestedAtUtc,
            CorrelationId: entity.CorrelationId,
            Result: result);
    }

    private async Task PersistOperationExecutionAsync(
        OperationHistoryEntry entry,
        IReadOnlyDictionary<string, string?>? input,
        CancellationToken cancellationToken)
    {
        var entity = new OperationExecutionEntity
        {
            ProjectId = entry.ProjectId,
            OperationId = entry.OperationId,
            InitiatedBy = entry.InitiatedBy,
            RequestedAtUtc = entry.RequestedAtUtc,
            CorrelationId = entry.CorrelationId,
            Status = entry.Result.Status.ToString(),
            Message = entry.Result.Message,
            StartedAtUtc = entry.Result.StartedAtUtc,
            CompletedAtUtc = entry.Result.CompletedAtUtc,
            ErrorCode = entry.Result.ErrorCode,
            InputJson = JsonSerializer.Serialize(input ?? new Dictionary<string, string?>(), SerializerOptions),
            OutputJson = JsonSerializer.Serialize(entry.Result.Output, SerializerOptions)
        };

        _dbContext.OperationExecutions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<string> DeserializeArray(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? [];
    }

    private static IReadOnlyDictionary<string, string?> DeserializeMap(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, SerializerOptions)
               ?? new Dictionary<string, string?>();
    }

    private static StatusLevel GetWorstLevel(IEnumerable<StatusLevel> levels)
    {
        var orderedLevels = levels.ToArray();
        if (orderedLevels.Length == 0)
        {
            return StatusLevel.Unknown;
        }

        if (orderedLevels.Any(level => level == StatusLevel.Critical))
        {
            return StatusLevel.Critical;
        }

        if (orderedLevels.Any(level => level == StatusLevel.Warning))
        {
            return StatusLevel.Warning;
        }

        if (orderedLevels.Any(level => level == StatusLevel.Healthy))
        {
            return StatusLevel.Healthy;
        }

        return StatusLevel.Unknown;
    }

    private async Task<IReadOnlyList<StatusSnapshot>> CollectSnapshotsAsync(Project project, CancellationToken cancellationToken)
    {
        if (_statusProviders.Count == 0)
        {
            return [];
        }

        var tasks = _statusProviders.Select(provider => provider.GetStatusAsync(project, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private IOperation? ResolveOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return _operationsById.TryGetValue(operationId.Trim(), out var operation)
            ? operation
            : null;
    }
}
