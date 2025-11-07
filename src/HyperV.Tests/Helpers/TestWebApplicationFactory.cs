using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Models;
using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using HyperV.Core.Hcn.Services;

namespace HyperV.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory dla testów HyperV.Agent
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<TestStartup>
{
    private IStorageService? _storageService;
    private IImageManagementService? _imageManagementService;
    private IJobService? _jobService;
    private HyperV.Core.Hcs.Services.VmService? _hcsVmService;
    private HyperV.Core.Wmi.Services.VmService? _wmiVmService;
    private VmCreationService? _vmCreationService;
    private ReplicationService? _replicationService;
    private NetworkService? _networkService;
    private HyperV.Core.Hcs.Services.ContainerService? _hcsContainerService;
    private HyperV.Core.Wmi.Services.ContainerService? _wmiContainerService;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Konfiguracja serwisów testowych
            if (_storageService != null)
                services.AddSingleton(_storageService);
            else
                services.AddSingleton<IStorageService, EnhancedStorageService>();

            if (_imageManagementService != null)
                services.AddSingleton(_imageManagementService);
            else
                services.AddSingleton<IImageManagementService, ImageManagementService>();

            if (_jobService != null)
                services.AddSingleton(_jobService);

            if (_hcsVmService != null)
                services.AddSingleton(_hcsVmService);
            else
                services.AddSingleton<HyperV.Core.Hcs.Services.VmService>();

            if (_wmiVmService != null)
                services.AddSingleton(_wmiVmService);
            else
                services.AddSingleton<HyperV.Core.Wmi.Services.VmService>();

            if (_vmCreationService != null)
                services.AddSingleton(_vmCreationService);
            else
                services.AddSingleton<VmCreationService>();

            if (_replicationService != null)
                services.AddSingleton(_replicationService);
            else
                services.AddSingleton<ReplicationService>();

            if (_networkService != null)
                services.AddSingleton(_networkService);
            else
                services.AddSingleton<NetworkService>();

            if (_hcsContainerService != null)
                services.AddSingleton(_hcsContainerService);
            else
                services.AddSingleton<HyperV.Core.Hcs.Services.ContainerService>();

            if (_wmiContainerService != null)
                services.AddSingleton(_wmiContainerService);
            else
                services.AddSingleton<HyperV.Core.Wmi.Services.ContainerService>();
        });

        builder.UseEnvironment("Testing");
        builder.UseUrls("http://127.0.0.1:0"); // Użyj losowego portu dla testów
    }

    public void ConfigureMocks(
        IStorageService storageService,
        IImageManagementService imageManagementService,
        IJobService jobService,
        HyperV.Core.Hcs.Services.VmService hcsVmService,
        HyperV.Core.Wmi.Services.VmService wmiVmService,
        VmCreationService vmCreationService,
        ReplicationService replicationService,
        NetworkService networkService,
        HyperV.Core.Hcs.Services.ContainerService hcsContainerService,
        HyperV.Core.Wmi.Services.ContainerService wmiContainerService)
    {
        _storageService = storageService;
        _imageManagementService = imageManagementService;
        _jobService = jobService;
        _hcsVmService = hcsVmService;
        _wmiVmService = wmiVmService;
        _vmCreationService = vmCreationService;
        _replicationService = replicationService;
        _networkService = networkService;
        _hcsContainerService = hcsContainerService;
        _wmiContainerService = wmiContainerService;
    }
}

/// <summary>
/// Test Startup class dla aplikacji testowej
/// </summary>
public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/api/v1/health", async context =>
            {
                await context.Response.WriteAsync("{\"status\":\"ok\"}");
                context.Response.ContentType = "application/json";
            });
            endpoints.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Welcome to Hyper-V Agent API!");
            });
        });
    }
}