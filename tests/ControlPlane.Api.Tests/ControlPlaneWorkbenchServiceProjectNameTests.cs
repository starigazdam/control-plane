using ControlPlane.Api.Configuration;
using ControlPlane.Api.Persistence;
using ControlPlane.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ControlPlane.Api.Tests;

public sealed class ControlPlaneWorkbenchServiceProjectNameTests
{
    [Fact]
    public async Task GetOverviewAsync_UsesProjectNameFromConfiguredSettings()
    {
        const string configuredName = "Aurora Platform";

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ControlPlaneDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var projectSettings = Options.Create(new ProjectSettings { Name = configuredName });
        var service = new ControlPlaneWorkbenchService(
            statusProviders: [],
            operations: [],
            dbContext: dbContext,
            projectSettings: projectSettings);

        var overview = await service.GetOverviewAsync(CancellationToken.None);

        var project = Assert.Single(overview.Projects);
        Assert.Equal(configuredName, project.ProjectName);
    }

    [Fact]
    public async Task GetOverviewAsync_FallsBackToDefaultNameWhenNotConfigured()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ControlPlaneDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var projectSettings = Options.Create(new ProjectSettings { Name = string.Empty });
        var service = new ControlPlaneWorkbenchService(
            statusProviders: [],
            operations: [],
            dbContext: dbContext,
            projectSettings: projectSettings);

        var overview = await service.GetOverviewAsync(CancellationToken.None);

        var project = Assert.Single(overview.Projects);
        Assert.Equal(ProjectSettings.DefaultName, project.ProjectName);
    }
}
