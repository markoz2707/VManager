using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using HyperV.Core.Hcn.Services;
using HyperV.Agent.Controllers;
using Moq;

namespace HyperV.Tests.Helpers;

/// <summary>
/// Bazowa klasa testowa z wspólnymi funkcjonalnościami dla testów API
/// </summary>
public abstract class BaseTestClass : IDisposable
{
    protected readonly TestWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    protected readonly Mock<IStorageService> MockStorageService;
    protected readonly Mock<IImageManagementService> MockImageManagementService;
    protected readonly Mock<IJobService> MockJobService;
    protected readonly Mock<HyperV.Core.Hcs.Services.VmService> MockHcsVmService;
    protected readonly Mock<HyperV.Core.Wmi.Services.VmService> MockWmiVmService;
    protected readonly Mock<VmCreationService> MockVmCreationService;
    protected readonly Mock<ReplicationService> MockReplicationService;
    protected readonly Mock<NetworkService> MockNetworkService;
    protected readonly Mock<HyperV.Core.Hcs.Services.ContainerService> MockHcsContainerService;
    protected readonly Mock<HyperV.Core.Wmi.Services.ContainerService> MockWmiContainerService;

    protected BaseTestClass()
    {
        // Inicjalizacja mocków
        MockStorageService = new Mock<IStorageService>();
        MockImageManagementService = new Mock<IImageManagementService>();
        MockJobService = new Mock<IJobService>();
        MockHcsVmService = new Mock<HyperV.Core.Hcs.Services.VmService>();
        MockWmiVmService = new Mock<HyperV.Core.Wmi.Services.VmService>();
        MockVmCreationService = new Mock<VmCreationService>();
        MockReplicationService = new Mock<ReplicationService>();
        MockNetworkService = new Mock<NetworkService>();
        MockHcsContainerService = new Mock<HyperV.Core.Hcs.Services.ContainerService>();
        MockWmiContainerService = new Mock<HyperV.Core.Wmi.Services.ContainerService>();

        // Tworzenie factory testowej aplikacji
        Factory = new TestWebApplicationFactory();
        Factory.ConfigureMocks(
            MockStorageService.Object,
            MockImageManagementService.Object,
            MockJobService.Object,
            MockHcsVmService.Object,
            MockWmiVmService.Object,
            MockVmCreationService.Object,
            MockReplicationService.Object,
            MockNetworkService.Object,
            MockHcsContainerService.Object,
            MockWmiContainerService.Object);

        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Reset all mocks before each test
    /// </summary>
    protected virtual void ResetMocks()
    {
        MockStorageService.Reset();
        MockImageManagementService.Reset();
        MockJobService.Reset();
        MockHcsVmService.Reset();
        MockWmiVmService.Reset();
        MockVmCreationService.Reset();
        MockReplicationService.Reset();
        MockNetworkService.Reset();
        MockHcsContainerService.Reset();
        MockWmiContainerService.Reset();
    }

    public virtual void Dispose()
    {
        Client?.Dispose();
        Factory?.Dispose();
    }
}