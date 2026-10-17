namespace ControlPlane.ExternalPlugins;

/// <summary>
/// Resolves a wire endpoint by appending its path to the configured <c>baseUrl</c>
/// with exactly one <c>/</c> separator, preserving any path in the base URL. The
/// remote id (captured at discovery) and the project id are percent-encoded.
/// </summary>
internal static class ExternalEndpointUri
{
    public static Uri Capabilities(Uri baseUri)
        => Build(baseUri, "capabilities");

    public static Uri Status(Uri baseUri, string remoteId, string projectId)
        => new(
            $"{Base(baseUri)}/status/{Uri.EscapeDataString(remoteId)}?projectId={Uri.EscapeDataString(projectId)}",
            UriKind.Absolute);

    public static Uri Validate(Uri baseUri, string remoteId)
        => Build(baseUri, "operations", Uri.EscapeDataString(remoteId), "validate");

    public static Uri Execute(Uri baseUri, string remoteId)
        => Build(baseUri, "operations", Uri.EscapeDataString(remoteId), "execute");

    private static Uri Build(Uri baseUri, params string[] segments)
        => new($"{Base(baseUri)}/{string.Join('/', segments)}", UriKind.Absolute);

    private static string Base(Uri baseUri) => baseUri.AbsoluteUri.TrimEnd('/');
}
