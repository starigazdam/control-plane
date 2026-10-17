using System.Text.Json;
using ControlPlane.Core.Operations;

namespace ControlPlane.ExternalPlugins;

internal sealed record ExternalCapabilityExclusion(string Kind, string Identifier, string Reason);

internal sealed record ExternalOperationDefinition(
    string RemoteId,
    string DisplayName,
    string Description,
    bool RequiresConfirmation,
    IReadOnlyList<OperationParameter> Parameters);

internal sealed record ExternalCapabilitiesParseResult(
    bool IsValid,
    IReadOnlyList<string> StatusProviderIds,
    IReadOnlyList<ExternalOperationDefinition> Operations,
    IReadOnlyList<ExternalCapabilityExclusion> Exclusions);

/// <summary>
/// Parses a <c>GET /capabilities</c> body. A body that is not a JSON object with a
/// <c>statusProviderIds</c> array and an <c>operations</c> array is invalid. A single
/// malformed capability is excluded with a warning instead of failing the whole
/// response.
/// </summary>
internal static class ExternalCapabilitiesParser
{
    private const string StatusProviderKind = "status-provider";
    private const string OperationKind = "operation";

    public static ExternalCapabilitiesParseResult Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Invalid();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Invalid();
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("statusProviderIds", out var statusProviderElement)
                || statusProviderElement.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("operations", out var operationsElement)
                || operationsElement.ValueKind != JsonValueKind.Array)
            {
                return Invalid();
            }

            var exclusions = new List<ExternalCapabilityExclusion>();
            var statusProviderIds = ParseStatusProviderIds(statusProviderElement, exclusions);
            var operations = ParseOperations(operationsElement, exclusions);

            return new ExternalCapabilitiesParseResult(true, statusProviderIds, operations, exclusions);
        }
    }

    internal static bool IsValidRemoteId(string id)
    {
        if (id.Length is < 1 or > 64)
        {
            return false;
        }

        if (!IsAsciiLetterOrDigit(id[0]))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<string> ParseStatusProviderIds(
        JsonElement element,
        List<ExternalCapabilityExclusion> exclusions)
    {
        var candidates = new List<(string Id, string Value)>();
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                exclusions.Add(new ExternalCapabilityExclusion(
                    StatusProviderKind,
                    $"#{index}",
                    "A statusProviderIds element is not a string and was excluded."));
            }
            else
            {
                var id = item.GetString() ?? string.Empty;
                if (!IsValidRemoteId(id))
                {
                    exclusions.Add(new ExternalCapabilityExclusion(
                        StatusProviderKind,
                        id,
                        $"'{id}' is not a valid remote id and was excluded."));
                }
                else if (string.Equals(id, "bridge", StringComparison.OrdinalIgnoreCase))
                {
                    exclusions.Add(new ExternalCapabilityExclusion(
                        StatusProviderKind,
                        id,
                        $"'{id}' is a reserved status provider id and was excluded."));
                }
                else
                {
                    candidates.Add((id, id));
                }
            }

            index++;
        }

        return ExcludeDuplicateGroups(candidates, StatusProviderKind, exclusions);
    }

    private static IReadOnlyList<ExternalOperationDefinition> ParseOperations(
        JsonElement element,
        List<ExternalCapabilityExclusion> exclusions)
    {
        var candidates = new List<(string Id, ExternalOperationDefinition Value)>();
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                exclusions.Add(new ExternalCapabilityExclusion(
                    OperationKind,
                    $"#{index}",
                    "An operations element is not an object and was excluded."));
            }
            else if (!TryParseOperation(item, out var operation, out var reason))
            {
                exclusions.Add(new ExternalCapabilityExclusion(
                    OperationKind,
                    GetIdentifier(item) ?? $"#{index}",
                    reason!));
            }
            else if (!IsValidRemoteId(operation!.RemoteId))
            {
                exclusions.Add(new ExternalCapabilityExclusion(
                    OperationKind,
                    operation.RemoteId,
                    $"'{operation.RemoteId}' is not a valid remote id and was excluded."));
            }
            else
            {
                candidates.Add((operation.RemoteId, operation));
            }

            index++;
        }

        return ExcludeDuplicateGroups(candidates, OperationKind, exclusions);
    }

    private static List<TValue> ExcludeDuplicateGroups<TValue>(
        IReadOnlyList<(string Id, TValue Value)> candidates,
        string kind,
        List<ExternalCapabilityExclusion> exclusions)
    {
        var duplicateIds = candidates
            .GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var valid = new List<TValue>(candidates.Count);
        foreach (var (id, value) in candidates)
        {
            if (duplicateIds.Contains(id))
            {
                exclusions.Add(new ExternalCapabilityExclusion(
                    kind,
                    id,
                    $"'{id}' is listed more than once and was excluded."));
            }
            else
            {
                valid.Add(value);
            }
        }

        return valid;
    }

    private static bool TryParseOperation(
        JsonElement element,
        out ExternalOperationDefinition? operation,
        out string? reason)
    {
        operation = null;

        if (!TryGetRequiredString(element, "id", out var id))
        {
            reason = "An operation entry is missing a string 'id' and was excluded.";
            return false;
        }

        if (!TryGetRequiredString(element, "displayName", out var displayName))
        {
            reason = $"Operation '{id}' is missing a string 'displayName' and was excluded.";
            return false;
        }

        if (!TryGetOptionalString(element, "description", out var description))
        {
            reason = $"Operation '{id}' has a wrongly typed optional 'description' and was excluded.";
            return false;
        }

        if (!TryGetOptionalBool(element, "requiresConfirmation", out var requiresConfirmation))
        {
            reason = $"Operation '{id}' has a wrongly typed optional 'requiresConfirmation' and was excluded.";
            return false;
        }

        if (!TryGetOptionalArray(element, "parameters", out var parametersElement))
        {
            reason = $"Operation '{id}' has a wrongly typed optional 'parameters' and was excluded.";
            return false;
        }

        var parameters = new List<OperationParameter>();
        if (parametersElement is { } parametersArray)
        {
            foreach (var parameterElement in parametersArray.EnumerateArray())
            {
                if (!TryParseParameter(parameterElement, out var parameter, out var parameterReason))
                {
                    reason = $"Operation '{id}' has an invalid parameter: {parameterReason}";
                    return false;
                }

                parameters.Add(parameter!);
            }
        }

        operation = new ExternalOperationDefinition(
            id!,
            displayName!,
            description ?? string.Empty,
            requiresConfirmation,
            parameters);
        reason = null;
        return true;
    }

    private static bool TryParseParameter(
        JsonElement element,
        out OperationParameter? parameter,
        out string? reason)
    {
        parameter = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            reason = "a parameter entry is not an object.";
            return false;
        }

        if (!TryGetRequiredString(element, "name", out var name))
        {
            reason = "a parameter entry is missing a string 'name'.";
            return false;
        }

        if (!TryGetRequiredString(element, "type", out var type))
        {
            reason = $"parameter '{name}' is missing a string 'type'.";
            return false;
        }

        if (!TryGetOptionalString(element, "displayName", out var displayName))
        {
            reason = $"parameter '{name}' has a wrongly typed optional 'displayName'.";
            return false;
        }

        if (!TryGetOptionalString(element, "description", out var description))
        {
            reason = $"parameter '{name}' has a wrongly typed optional 'description'.";
            return false;
        }

        if (!TryGetOptionalBool(element, "isRequired", out var isRequired))
        {
            reason = $"parameter '{name}' has a wrongly typed optional 'isRequired'.";
            return false;
        }

        if (!TryGetOptionalDefaultValue(element, "defaultValue", out var defaultValue))
        {
            reason = $"parameter '{name}' has a 'defaultValue' that is not a string or null.";
            return false;
        }

        parameter = new OperationParameter(
            name!,
            displayName ?? name!,
            description ?? string.Empty,
            isRequired,
            type!,
            defaultValue);
        reason = null;
        return true;
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetOptionalString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryGetOptionalBool(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetOptionalArray(JsonElement element, string propertyName, out JsonElement? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        value = property;
        return true;
    }

    private static bool TryGetOptionalDefaultValue(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static string? GetIdentifier(JsonElement element)
    {
        return element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
    }

    private static bool IsAsciiLetterOrDigit(char character)
        => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static ExternalCapabilitiesParseResult Invalid()
        => new(false, [], [], []);
}
