namespace ControlPlane.ExternalPlugins;

/// <summary>Stable bridge-side execute and validate error codes.</summary>
internal static class ExternalOperationErrorCodes
{
    public const string Prefix = "external_operation_";

    public const string Timeout = "external_operation_timeout";

    public const string TransportFailed = "external_operation_transport_failed";

    public const string UnexpectedStatus = "external_operation_unexpected_status";

    public const string InvalidResponse = "external_operation_invalid_response";
}

/// <summary>Fixed signal ids the bridge status provider reports.</summary>
internal static class ExternalBridgeSignals
{
    public const string Unreachable = "unreachable";

    public const string InvalidCapabilities = "invalid-capabilities";

    public const string StatusCallFailed = "status-call-failed";
}
