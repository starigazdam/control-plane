using ControlPlane.Api.Infrastructure;
using ControlPlane.Core.Concepts;
using ControlPlane.Core.Interfaces;
using ControlPlane.Core.Operations;
using ControlPlane.Core.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Api.Tests;

public sealed class PluginRegistrationContractTests
{
    [Fact]
    public void Duplicate_operation_id_stops_startup_case_insensitively()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IOperation>(new StubOperation("dup"));
        builder.Services.AddSingleton<IOperation>(new StubOperation("DUP"));
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.ValidateUniqueCapabilityIds());
        Assert.Contains("dup", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_status_provider_id_stops_startup_case_insensitively()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IStatusProvider>(new StubStatusProvider("dup"));
        builder.Services.AddSingleton<IStatusProvider>(new StubStatusProvider("Dup"));
        using var app = builder.Build();

        Assert.Throws<InvalidOperationException>(() => app.ValidateUniqueCapabilityIds());
    }

    [Fact]
    public void Unique_ids_pass()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IOperation>(new StubOperation("one"));
        builder.Services.AddSingleton<IOperation>(new StubOperation("two"));
        builder.Services.AddSingleton<IStatusProvider>(new StubStatusProvider("one"));
        using var app = builder.Build();

        app.ValidateUniqueCapabilityIds();
    }

    [Fact]
    public void Descriptors_record_types_instances_and_factories_as_singletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();
        services.AddControlPlanePlugins(configuration, typeof(DescriptorTestPlugin).Assembly);
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<DiscoveredPluginCatalog>();
        Assert.Equal(["Descriptors"], catalog.PluginNames);
        Assert.Equal(2, catalog.StatusProviders.Count);
        Assert.Equal(3, catalog.Operations.Count);

        var first = provider.GetServices<IOperation>().ToDictionary(operation => operation.Definition.Id);
        var second = provider.GetServices<IOperation>().ToDictionary(operation => operation.Definition.Id);
        Assert.NotSame(first["alpha-op"], second["alpha-op"]);
        Assert.Same(first["beta-op"], second["beta-op"]);
        Assert.Same(first["gamma-op"], second["gamma-op"]);
    }
}

internal sealed class DescriptorTestPlugin : IControlPlanePlugin
{
    public DescriptorTestPlugin()
    {
    }

    public string Name => "Descriptors";

    public void Register(IPluginRegistration registration)
    {
        registration.AddStatusProvider<AlphaStatusProvider>();
        registration.AddStatusProvider<AlphaStatusProvider>();
        registration.AddStatusProvider(new BetaStatusProvider());
        registration.AddOperation<AlphaOperation>();
        registration.AddOperation<AlphaOperation>();
        registration.AddOperation(new BetaOperation());
        registration.AddOperation(_ => new GammaOperation());
    }
}

internal class StubOperation : IOperation
{
    public StubOperation(string id)
    {
        Definition = new OperationDefinition(id, id, string.Empty, false, []);
    }

    public OperationDefinition Definition { get; }

    public Task<IReadOnlyList<string>> ValidateAsync(OperationExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<OperationExecutionResult> ExecuteAsync(OperationExecutionContext context, CancellationToken cancellationToken)
        => Task.FromResult(new OperationExecutionResult(
            OperationExecutionStatus.Succeeded,
            "ok",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            new Dictionary<string, string?>()));
}

internal sealed class AlphaOperation : StubOperation
{
    public AlphaOperation()
        : base("alpha-op")
    {
    }
}

internal sealed class BetaOperation : StubOperation
{
    public BetaOperation()
        : base("beta-op")
    {
    }
}

internal sealed class GammaOperation : StubOperation
{
    public GammaOperation()
        : base("gamma-op")
    {
    }
}

internal class StubStatusProvider : IStatusProvider
{
    public StubStatusProvider(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public Task<StatusSnapshot> GetStatusAsync(Project project, CancellationToken cancellationToken)
        => Task.FromResult(new StatusSnapshot(project.Id, Id, StatusLevel.Healthy, DateTimeOffset.UtcNow, []));
}

internal sealed class AlphaStatusProvider : StubStatusProvider
{
    public AlphaStatusProvider()
        : base("alpha")
    {
    }
}

internal sealed class BetaStatusProvider : StubStatusProvider
{
    public BetaStatusProvider()
        : base("beta")
    {
    }
}
