using HyperV.Contracts.Interfaces;
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

builder.Services.AddControllers();
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
builder.Services.AddSingleton<IStorageQoSService, HyperV.Core.Wmi.Services.StorageQoSService>();
builder.Services.AddSingleton<IJobService, HyperV.Agent.Services.InMemoryJobService>();
builder.Services.AddSingleton<IHostInfoService>(sp => new HyperV.Core.Wmi.Services.HostInfoService(sp.GetRequiredService<ILogger<HyperV.Core.Wmi.Services.HostInfoService>>(), sp.GetRequiredService<IMemoryCache>()));

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

// Enable CORS middleware - use default policy for development
app.UseCors();

// Alternative: Use named policy for production
// app.UseCors("ProductionCORS");

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

