using Microsoft.Extensions.Configuration;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Reads and validates the static external plugin list. A missing or invalid name,
/// a duplicate name, or a missing or invalid <c>baseUrl</c> fails startup with an
/// error that names the setting.
/// </summary>
internal static class ExternalPluginConfiguration
{
    private const string NameKey = "Name";
    private const string BaseUrlKey = "BaseUrl";

    public static IReadOnlyList<ExternalPluginDefinition> Parse(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration
            .GetSection(ExternalPluginsSettings.SectionName)
            .Get<ExternalPluginsSettings>();

        var entries = settings?.Plugins;
        if (entries is null || entries.Count == 0)
        {
            return [];
        }

        var definitions = new List<ExternalPluginDefinition>(entries.Count);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index] ?? new ExternalPluginEntry();
            var nameSetting = $"{ExternalPluginsSettings.SectionName}:Plugins[{index}]:{NameKey}";
            var baseUrlSetting = $"{ExternalPluginsSettings.SectionName}:Plugins[{index}]:{BaseUrlKey}";

            var name = entry.Name;
            if (string.IsNullOrWhiteSpace(name) || !IsValidPluginName(name))
            {
                throw new InvalidOperationException(
                    $"The '{nameSetting}' setting is missing or invalid. " +
                    "A plugin name must be 1-32 lowercase ASCII letters, digits, or hyphens and must start with a letter.");
            }

            if (!seenNames.Add(name))
            {
                throw new InvalidOperationException(
                    $"The '{nameSetting}' setting duplicates the plugin name '{name}'. Plugin names must be unique.");
            }

            var baseUrl = entry.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl)
                || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
                || !IsHttpOrHttps(baseUri)
                || !string.IsNullOrEmpty(baseUri.Query)
                || !string.IsNullOrEmpty(baseUri.Fragment)
                || !string.IsNullOrEmpty(baseUri.UserInfo))
            {
                throw new InvalidOperationException(
                    $"The '{baseUrlSetting}' setting is missing or invalid. " +
                    "A baseUrl must be an absolute http or https URI with no query, fragment, or userinfo.");
            }

            definitions.Add(new ExternalPluginDefinition(name, baseUri));
        }

        return definitions;
    }

    internal static bool IsValidPluginName(string name)
    {
        if (name.Length is < 1 or > 32)
        {
            return false;
        }

        if (name[0] is not (>= 'a' and <= 'z'))
        {
            return false;
        }

        foreach (var character in name)
        {
            var isLowerLetter = character is >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';
            if (!isLowerLetter && !isDigit && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHttpOrHttps(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record ExternalPluginDefinition(string Name, Uri BaseUri);
