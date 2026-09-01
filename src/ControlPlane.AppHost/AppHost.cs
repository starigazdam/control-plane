using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();
var controlPlaneDb = postgres.AddDatabase("controlplane");

var serviceBus = builder
    .AddAzureServiceBus("servicebus")
    .RunAsEmulator();
var controlPlaneQueue = serviceBus.AddServiceBusQueue("controlplane-queue");

var api = builder
    .AddProject<ControlPlane_Api>("api")
    .WithReference(controlPlaneDb)
    .WithReference(serviceBus)
    .WithReference(controlPlaneQueue)
    .WithEnvironment("Database__Provider", "Postgres")
    .WaitFor(controlPlaneDb)
    .WaitFor(serviceBus);

var ui = builder
    .AddViteApp("ui", "../../ui")
    .WithReference(api);

builder
    .AddYarp("web")
    .WithExternalHttpEndpoints()
    .WithConfiguration(config =>
    {
        config.AddRoute("/api/{**catch-all}", api);
    })
    .PublishWithStaticFiles(ui);

builder.Build().Run();
