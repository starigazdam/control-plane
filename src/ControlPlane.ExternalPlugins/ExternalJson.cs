using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Shared JSON options: the web defaults (camelCase, case-insensitive) plus string
/// enum values, matching the host's contract serialization.
/// </summary>
internal static class ExternalJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
