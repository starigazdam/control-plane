using ControlPlane.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.Api.Infrastructure;

/// <summary>
/// Fails startup when two registered operations, or two registered status
/// providers, expose ids that are equal under ordinal case-insensitive
/// comparison. The check resolves the capabilities from the built container
/// because instance and factory descriptors expose an id only on an instance.
/// </summary>
public static class CapabilityIdValidationExtensions
{
    public static void ValidateUniqueCapabilityIds(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        EnsureUnique(
            serviceProvider.GetServices<IOperation>().Select(operation => operation.Definition.Id),
            "operation");

        EnsureUnique(
            serviceProvider.GetServices<IStatusProvider>().Select(provider => provider.Id),
            "status provider");
    }

    private static void EnsureUnique(IEnumerable<string> ids, string capabilityKind)
    {
        var duplicate = ids
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate {capabilityKind} id '{duplicate.First()}' is registered by more than one plugin. " +
                $"Capability ids must be unique under ordinal case-insensitive comparison.");
        }
    }
}
