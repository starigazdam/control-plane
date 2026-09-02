using System.Linq;
using ControlPlane.Api.Configuration;
using ControlPlane.Api.Infrastructure;
using ControlPlane.Api.Persistence;
using ControlPlane.Api.Services;
using ControlPlane.Azure;
using ControlPlane.CopilotAgent;
using ControlPlane.DevOps;
using ControlPlane.Kafka;
using ControlPlane.ServiceBus;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Load .env (committed defaults) and .env.local (local developer overrides,
// gitignored) as data only (setEnvVars: false), then apply values manually
// so environment variables set BEFORE this process started (e.g. Aspire's
// WithEnvironment, the shell, or deployment infrastructure) always win.
// Precedence: external environment > .env.local > .env default.
var externalKeys = new HashSet<string>(
    Environment.GetEnvironmentVariables().Keys.Cast<string>(),
    StringComparer.OrdinalIgnoreCase);

var root = builder.Environment.ContentRootPath;
foreach (var name in new[] { ".env", ".env.local" })
{
    var path = Path.Combine(root, name);
    if (!File.Exists(path))
        path = Path.Combine(root, "..", name);
    if (!File.Exists(path))
        continue;

    foreach (var (key, value) in Env.Load(path, new LoadOptions(setEnvVars: false)))
    {
        if (externalKeys.Contains(key))
            continue;

        Environment.SetEnvironmentVariable(key, value);
    }
}

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<ProjectSettings>(builder.Configuration.GetSection(ProjectSettings.SectionName));

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControlPlanePlugins(
    builder.Configuration,
    typeof(AzurePlugin).Assembly,
    typeof(KafkaPlugin).Assembly,
    typeof(DevOpsPlugin).Assembly,
    typeof(ServiceBusPlugin).Assembly,
    typeof(CopilotAgentPlugin).Assembly);
builder.Services.AddDbContext<ControlPlaneDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ControlPlane")
        ?? builder.Configuration.GetConnectionString("controlplane")
        ?? "Data Source=control-plane.db";

    var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
});
builder.Services.AddScoped<ControlPlaneWorkbenchService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
