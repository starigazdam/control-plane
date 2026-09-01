namespace ControlPlane.CopilotAgent.Operations;

internal static class OperationHelpers
{
    /// <summary>Maximum input length accepted for prompt/subject parameters.</summary>
    internal const int MaxInputLength = 2000;

    internal static string TruncateForMessage(string value, int maxLength = 80) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
