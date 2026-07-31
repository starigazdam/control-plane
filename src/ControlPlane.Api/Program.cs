using ControlPlane.Api.Infrastructure;
using ControlPlane.Api.Persistence;
using ControlPlane.Api.Services;
using ControlPlane.Azure;
using ControlPlane.DevOps;
using ControlPlane.Kafka;
using ControlPlane.ServiceBus;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControlPlanePlugins(
    typeof(AzurePlugin).Assembly,
    typeof(KafkaPlugin).Assembly,
    typeof(DevOpsPlugin).Assembly,
    typeof(ServiceBusPlugin).Assembly);
builder.Services.AddDbContext<ControlPlaneDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ControlPlane")
        ?? "Data Source=control-plane.db";
    options.UseSqlite(connectionString);
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

app.Run();
