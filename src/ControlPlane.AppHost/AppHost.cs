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
    .WaitFor(controlPlaneDb)
    .WaitFor(serviceBus);

builder
    .AddViteApp("ui", "../../ui")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
