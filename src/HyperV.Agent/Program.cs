using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcn.Services;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hyper-V Agent API",
        Version = "v1",
        Description = "REST API exposing Hyper-V host functions (HCS, HCN, WMI, VHD).",
        Contact = new OpenApiContact { Name = "HyperV Agent", Email = "admin@example.com" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    // XML docs
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xml in xmlFiles) c.IncludeXmlComments(xml, includeControllerXmlComments: true);
    c.EnableAnnotations();
});

builder.Services.AddSingleton<HyperV.Core.Hcs.Services.VmService>();
builder.Services.AddSingleton<HyperV.Core.Wmi.Services.VmService>();
builder.Services.AddSingleton<VmCreationService>();
builder.Services.AddSingleton<NetworkService>();
builder.Services.AddSingleton<ReplicationService>();
builder.Services.AddSingleton<HyperV.Core.Hcs.Services.ContainerService>();
builder.Services.AddSingleton<HyperV.Core.Wmi.Services.ContainerService>();
builder.Services.AddSingleton<IStorageService, HyperV.Core.Wmi.Services.StorageService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hyper-V Agent API v1");
    c.RoutePrefix = "swagger";
});

app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Service")
   .WithSummary("Health check")
   .WithDescription("Returns health status of the agent.");

app.MapGet("/", () => Results.Ok("Welcome to Hyper-V Agent API!"));

app.MapControllers();

app.Run("http://127.0.0.1:8743");
