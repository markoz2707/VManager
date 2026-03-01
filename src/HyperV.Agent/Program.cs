using HyperV.Contracts.Interfaces;
using Prometheus;
using VManager.Provider.HyperV;
using VManager.Provider.KVM;
using HyperV.Contracts.Services;
using HyperV.Core.Hcn.Services;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using Serilog;
using HyperV.Agent.Middleware;
using HyperV.Agent.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

var serverConfig = builder.Configuration.GetSection("Server").Get<ServerConfig>() ?? new ServerConfig();
var corsConfig = builder.Configuration.GetSection("Cors").Get<CorsConfig>() ?? new CorsConfig();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

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

// Determine hypervisor mode early (needed for conditional controller registration)
var hypervisorType = builder.Configuration.GetValue<string>("Hypervisor:Type") ?? "auto";
var isKvm = hypervisorType == "kvm" || (hypervisorType == "auto" && System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux));

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(m =>
        m.FeatureProviders.Add(new HyperV.Agent.Controllers.HypervisorControllerFeatureProvider(isKvm)));
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Configure JWT Authentication
var jwtConfig = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtConfig["Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = "default-secret-key-min-32-chars-required-for-hmacsha256";
}
var secretKey = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidAudience = jwtConfig["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey)
    };
});

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VManager Agent API",
        Version = "v1",
        Description = "REST API for multi-hypervisor VM management (Hyper-V, KVM).",
        Contact = new OpenApiContact { Name = "HyperV Agent", Email = "admin@example.com" },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    // XML docs
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
    foreach (var xml in xmlFiles) c.IncludeXmlComments(xml, includeControllerXmlComments: true);
    c.EnableAnnotations();
});

// Common services (both hypervisors)
builder.Services.AddSingleton<IJobService, HyperV.Agent.Services.InMemoryJobService>();
builder.Services.AddHostedService<HyperV.Agent.Services.PrometheusMetricsCollector>();

// Conditional hypervisor provider and service registration
if (isKvm)
{
    builder.Services.Configure<KvmOptions>(builder.Configuration.GetSection("Hypervisor:KVM"));
    builder.Services.AddKvmProvider();
}
else
{
    // HyperV-specific backing services (required by HyperV providers)
    builder.Services.AddSingleton<HyperV.Core.Hcs.Services.VmService>();
    builder.Services.AddSingleton<HyperV.Core.Wmi.Services.VmService>();
    builder.Services.AddSingleton<VmCreationService>();
    builder.Services.AddSingleton<MetricsService>();
    builder.Services.AddSingleton<ResourcePoolsService>();
    builder.Services.AddSingleton<NetworkService>();
    builder.Services.AddSingleton<WmiNetworkService>();
    builder.Services.AddSingleton<HyperV.Core.Hcs.Services.ContainerService>();
    builder.Services.AddSingleton<HyperV.Core.Wmi.Services.ContainerService>();
    builder.Services.AddSingleton<IStorageService, EnhancedStorageService>();
    builder.Services.AddSingleton<IHostInfoService>(sp => new HyperV.Core.Wmi.Services.HostInfoService(sp.GetRequiredService<ILogger<HyperV.Core.Wmi.Services.HostInfoService>>(), sp.GetRequiredService<IMemoryCache>()));

    // HyperV providers (IVmProvider, IHostProvider, IMetricsProvider, etc.)
    builder.Services.AddHyperVProvider();

    // Services for HyperVFeaturesController (HyperV-only)
    builder.Services.AddSingleton<IReplicationService, ReplicationService>();
    builder.Services.AddSingleton<FibreChannelService>();
    builder.Services.AddSingleton<IImageManagementService, HyperV.Core.Wmi.Services.ImageManagementService>();
    builder.Services.AddSingleton<IStorageQoSService, HyperV.Core.Wmi.Services.StorageQoSService>();
}

// Health checks (conditional)
var hc = builder.Services.AddHealthChecks();
if (!isKvm)
{
    hc.AddCheck<HyperV.Agent.Services.WmiHealthCheck>("wmi", tags: new[] { "hyperv", "wmi" });
    hc.AddCheck<HyperV.Agent.Services.HcsHealthCheck>("hcs", tags: new[] { "hyperv", "hcs" });
}
hc.AddCheck<HyperV.Agent.Services.DiskSpaceHealthCheck>("disk-space", tags: new[] { "system" });

// Register SignalR hub notifier
builder.Services.AddSingleton<IAgentHubNotifier, AgentHubNotifier>();

// Scheduler services
builder.Services.AddSingleton<HyperV.Agent.Services.ScheduleStore>();
builder.Services.AddHostedService<HyperV.Agent.Services.ScheduledTaskService>();

// VM State Monitor (polls for changes, emits granular SignalR events)
builder.Services.AddHostedService<HyperV.Agent.Services.VmStateMonitorService>();

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

// Global exception handler
app.UseMiddleware<GlobalExceptionHandler>();

// Security headers (X-Content-Type-Options, X-Frame-Options, HSTS, CSP, etc.)
app.UseSecurityHeaders();

// Rate limiting (IP-based, configurable via RateLimiting section in appsettings)
app.UseRateLimiting();

// Enable CORS middleware - use default policy for development
app.UseCors();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Configure static files for LocalManagement UI
app.UseStaticFiles();
app.UseDefaultFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hyper-V Agent API v1");
    c.RoutePrefix = "swagger";
});

app.MapHealthChecks("/api/v1/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds + "ms"
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapMetrics(); // Prometheus /metrics endpoint
app.MapControllers();
app.MapHub<AgentHub>("/hubs/agent");

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

