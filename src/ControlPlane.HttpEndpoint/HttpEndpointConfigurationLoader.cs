using System.Text.Json;

namespace ControlPlane.HttpEndpoint;

/// <summary>
/// Loads and validates the configured endpoint file. Fatal problems fail startup with a
/// safe <see cref="HttpEndpointConfigurationException"/>; invalid individual entries are
/// excluded and reported as index-identified warnings.
/// </summary>
public static class HttpEndpointConfigurationLoader
{
    public const int MaxEntries = 20;
    public const int DefaultTimeoutSeconds = 5;
    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 10;
    public const int MaxIdLength = 64;

    private const string SettingName = $"{HttpEndpointSettings.SectionName}__{nameof(HttpEndpointSettings.ConfigPath)}";

    public static HttpEndpointConfiguration Load(HttpEndpointSettings settings, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var configuredPath = settings.ConfigPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return HttpEndpointConfiguration.Disabled;
        }

        var resolvedPath = ResolvePath(configuredPath, contentRootPath);

        string json;
        try
        {
            json = File.ReadAllText(resolvedPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            throw new HttpEndpointConfigurationException(
                $"The configured HTTP endpoint file could not be read. Setting: {SettingName}. Path: {resolvedPath}.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new HttpEndpointConfigurationException(
                $"The configured HTTP endpoint file is not valid JSON. Setting: {SettingName}. Path: {resolvedPath}.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new HttpEndpointConfigurationException(
                    $"The configured HTTP endpoint file must contain a JSON array. Setting: {SettingName}. Path: {resolvedPath}.");
            }

            var elements = document.RootElement.EnumerateArray().ToArray();
            if (elements.Length > MaxEntries)
            {
                throw new HttpEndpointConfigurationException(
                    $"The configured HTTP endpoint file must contain no more than {MaxEntries} entries. Setting: {SettingName}. Path: {resolvedPath}.");
            }

            return Validate(elements, settings.DefaultCron);
        }
    }

    private static HttpEndpointConfiguration Validate(JsonElement[] elements, string defaultCron)
    {
        var warnings = new List<HttpEndpointConfigWarning>();
        var candidates = new List<(int Index, HttpEndpointDefinition Definition)>();
        var hasScheduleWarning = !string.IsNullOrWhiteSpace(defaultCron);

        for (var index = 0; index < elements.Length; index++)
        {
            var element = elements[index];
            if (element.ValueKind != JsonValueKind.Object)
            {
                warnings.Add(new HttpEndpointConfigWarning(index, "invalid_entry"));
                continue;
            }

            if (TryGetProperty(element, "cron", out _))
            {
                hasScheduleWarning = true;
            }

            var (definition, code) = ParseEntry(element);
            if (definition is null)
            {
                warnings.Add(new HttpEndpointConfigWarning(index, code ?? "invalid_entry"));
            }
            else
            {
                candidates.Add((index, definition));
            }
        }

        var duplicateIndexes = candidates
            .GroupBy(candidate => candidate.Definition.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .Select(candidate => candidate.Index)
            .ToHashSet();

        foreach (var duplicateIndex in duplicateIndexes)
        {
            warnings.Add(new HttpEndpointConfigWarning(duplicateIndex, "duplicate_id"));
        }

        var endpoints = candidates
            .Where(candidate => !duplicateIndexes.Contains(candidate.Index))
            .OrderBy(candidate => candidate.Index)
            .Select(candidate => candidate.Definition)
            .ToArray();

        var orderedWarnings = warnings
            .OrderBy(warning => warning.Index)
            .ToArray();

        return new HttpEndpointConfiguration(
            isEnabled: true,
            endpoints: endpoints,
            warnings: orderedWarnings,
            hasScheduleWarning: hasScheduleWarning);
    }

    private static (HttpEndpointDefinition? Definition, string? Code) ParseEntry(JsonElement element)
    {
        if (!TryGetString(element, "id", out var id) || !IsValidId(id))
        {
            return (null, "invalid_id");
        }

        if (!TryGetString(element, "name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return (null, "invalid_name");
        }

        if (!TryGetString(element, "url", out var url) || !IsValidUrl(url))
        {
            return (null, "invalid_url");
        }

        if (!TryGetInt(element, "expectedStatus", out var expectedStatus)
            || expectedStatus < 100
            || expectedStatus > 599)
        {
            return (null, "invalid_status");
        }

        if (!TryGetString(element, "method", out var method) || !IsSupportedMethod(method))
        {
            return (null, "invalid_method");
        }

        var timeoutSeconds = DefaultTimeoutSeconds;
        if (TryGetProperty(element, "timeoutSeconds", out var timeoutElement))
        {
            if (timeoutElement.ValueKind != JsonValueKind.Number
                || !timeoutElement.TryGetInt32(out var parsedTimeout)
                || parsedTimeout < MinTimeoutSeconds
                || parsedTimeout > MaxTimeoutSeconds)
            {
                return (null, "invalid_timeout");
            }

            timeoutSeconds = parsedTimeout;
        }

        return (
            new HttpEndpointDefinition(
                Id: id,
                Name: name.Trim(),
                Url: url,
                ExpectedStatus: expectedStatus,
                Method: method.ToUpperInvariant(),
                Timeout: TimeSpan.FromSeconds(timeoutSeconds)),
            null);
    }

    private static string ResolvePath(string configuredPath, string contentRootPath)
    {
        try
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return Path.GetFullPath(configuredPath);
            }

            return Path.GetFullPath(Path.Combine(contentRootPath ?? string.Empty, configuredPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new HttpEndpointConfigurationException(
                $"The configured HTTP endpoint path is not valid. Setting: {SettingName}. Path: {configuredPath}.");
        }
    }

    private static bool IsValidId(string id)
    {
        if (id.Length is < 1 or > MaxIdLength)
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        return string.IsNullOrEmpty(uri.UserInfo);
    }

    private static bool IsSupportedMethod(string method)
    {
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        if (TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
