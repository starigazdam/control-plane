using Xunit;

namespace ControlPlane.Api.Tests.Support;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessGlobalObserverCollection
{
    public const string Name = "Process-global observers";
}
