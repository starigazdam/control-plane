namespace ControlPlane.Azure;

public sealed class AzureSettings
{
    public const string SectionName = "Azure";

    public string TenantId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
