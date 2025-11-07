using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcn.Services;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

var serverConfig = builder.Configuration.GetSection("Server").Get<ServerConfig>() ?? new ServerConfig();
var corsConfig = builder.Configuration.GetSection("Cors").Get<CorsConfig>() ?? new CorsConfig();

// Configure logging based on configuration
var logToFile = builder.Configuration.GetValue<bool>("Logging:LogToFile");
var logFilePath = builder.Configuration.GetValue<string>("Logging:LogFilePath") ?? "logs/hyperv-agent.log";

builder.Logging.ClearProviders();

// Always add console logging unless file-only is specified
var logToConsole = builder.Configuration.GetValue<bool>("Logging:LogToConsole", true);
if (logToConsole)
{
    builder.Logging.AddConsole();
}

if (logToFile)
{
    // Ensure log directory exists
    var logDir = Path.GetDirectoryName(logFilePath);
    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
    {
        Directory.CreateDirectory(logDir);
    }
    
    // For now, we'll use console logging with option to redirect
    // In production, consider using Serilog: builder.Host.UseSerilog()
    Console.WriteLine($"File logging would be configured to: {logFilePath}");
    if (!logToConsole)
    {
        builder.Logging.AddConsole(); // Fallback for now
    }
}

// Configure log levels
var logLevel = builder.Configuration.GetValue<string>("Logging:LogLevel:Default", "Information");
if (Enum.TryParse<LogLevel>(logLevel, true, out var level))
{
    builder.Logging.SetMinimumLevel(level);
}

// Configure CORS for central management applications
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddSingleton<ResourcePoolsService>();
builder.Services.AddSingleton<NetworkService>();
builder.Services.AddSingleton<WmiNetworkService>();
builder.Services.AddSingleton<ReplicationService>();
builder.Services.AddSingleton<FibreChannelService>();
builder.Services.AddSingleton<HyperV.Core.Hcs.Services.ContainerService>();
builder.Services.AddSingleton<HyperV.Core.Wmi.Services.ContainerService>();
builder.Services.AddSingleton<IStorageService, EnhancedStorageService>();
builder.Services.AddSingleton<IImageManagementService, HyperV.Core.Wmi.Services.ImageManagementService>();
builder.Services.AddSingleton<IJobService, HyperV.Agent.Services.InMemoryJobService>();
builder.Services.AddSingleton<IHostInfoService, HyperV.Core.Wmi.Services.HostInfoService>();

builder.WebHost.ConfigureKestrel(options =>
{
    var hostIp = IPAddress.Parse(serverConfig.Host);
    var port = serverConfig.Port;

    if (serverConfig.Protocol.ToLower() == "https")
    {
        if (!string.IsNullOrEmpty(serverConfig.Https.CertificatePath) &&
            !string.IsNullOrEmpty(serverConfig.Https.CertificateKeyPath))
        {
            try
            {
                var cert = X509Certificate2.CreateFromPemFile(
                    serverConfig.Https.CertificatePath,
                    serverConfig.Https.CertificateKeyPath);
                
                options.Listen(hostIp, port, listenOptions =>
                {
                    listenOptions.UseHttps(cert);
                });
            }
            catch (Exception ex)
            {
                // Log error and fallback to development cert
                Console.WriteLine($"Failed to load custom certificate: {ex.Message}. Using development certificate.");
                options.Listen(hostIp, port, listenOptions =>
                {
                    listenOptions.UseHttps();
                });
            }
        }
        else
        {
            // Use development certificate
            options.Listen(hostIp, port, listenOptions =>
            {
                listenOptions.UseHttps();
            });
        }
    }
    else
    {
        options.Listen(hostIp, port);
    }
});

var app = builder.Build();

// Enable CORS middleware - use default policy for development
app.UseCors();

// Alternative: Use named policy for production
// app.UseCors("ProductionCORS");

// Configure static files for LocalManagement UI
app.UseStaticFiles();
app.UseDefaultFiles();

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

app.MapControllers();

// SPA fallback - serve index.html only for non-API routes
app.Map("{*path:nonfile}", async (HttpContext context) =>
{
    // Don't serve SPA for API routes
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Not Found");
        return;
    }
    
    // Serve SPA for other routes
    await context.Response.SendFileAsync("wwwroot/index.html");
}).Add(endpointBuilder =>
{
    endpointBuilder.Metadata.Add(new Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute { IgnoreApi = true });
});

app.Run();

public class ServerConfig
{
    public string Protocol { get; set; } = "http";
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8743;
    public HttpsConfig Https { get; set; } = new();
}

public class HttpsConfig
{
    public string CertificatePath { get; set; } = "";
    public string CertificateKeyPath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
}

public class CorsConfig
{
    public string Hostname { get; set; } = "management.hyperv.local";
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
}

