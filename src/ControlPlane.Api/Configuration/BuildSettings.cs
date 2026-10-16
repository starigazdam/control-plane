namespace ControlPlane.Api.Configuration;

public sealed class BuildSettings
{
    public const string SectionName = "Build";

    public const string UnknownCommitSha = "unknown";

    public string? CommitSha { get; set; }

    public string ResolveCommitSha()
    {
        return string.IsNullOrWhiteSpace(CommitSha) ? UnknownCommitSha : CommitSha;
    }
}
