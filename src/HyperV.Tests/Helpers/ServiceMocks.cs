using Moq;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using HyperV.Core.Hcn.Services;
using System.Text.Json;

namespace HyperV.Tests.Helpers;

/// <summary>
/// Klasa do konfigurowania mocków serwisów
/// </summary>
public static class ServiceMocks
{
    /// <summary>
    /// Konfiguruje mock dla IStorageService
    /// </summary>
    public static Mock<IStorageService> ConfigureStorageServiceMock()
    {
        var mock = new Mock<IStorageService>();
        
        // Podstawowe operacje VHD
        mock.Setup(x => x.CreateVirtualHardDisk(It.IsAny<CreateVhdRequest>()))
            .Verifiable();
            
        mock.Setup(x => x.AttachVirtualHardDisk(It.IsAny<string>(), It.IsAny<string>()))
            .Verifiable();
            
        mock.Setup(x => x.DetachVirtualHardDisk(It.IsAny<string>(), It.IsAny<string>()))
            .Verifiable();
            
        mock.Setup(x => x.ResizeVirtualHardDisk(It.IsAny<ResizeVhdRequest>()))
            .Verifiable();

        // Asynchroniczne operacje
        mock.Setup(x => x.GetVhdMetadataAsync(It.IsAny<string>()))
            .ReturnsAsync(new VhdMetadata 
            { 
                Path = "test.vhdx", 
                Format = "VHDX", 
                UniqueId = Guid.NewGuid(),
                IsAttached = false 
            });
            
        mock.Setup(x => x.GetVmStorageDevicesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<StorageDeviceResponse>
            {
                new()
                {
                    DeviceId = "device-1",
                    Name = "Test Storage Device",
                    DeviceType = "VirtualDisk",
                    Path = "test.vhdx",
                    ControllerId = "controller-1",
                    ControllerType = "SCSI",
                    IsReadOnly = false,
                    OperationalStatus = "Connected"
                }
            });
            
        mock.Setup(x => x.GetVmStorageControllersAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<StorageControllerResponse>
            {
                new()
                {
                    ControllerId = "controller-1",
                    Name = "SCSI Controller",
                    ControllerType = "SCSI",
                    MaxDevices = 64,
                    AttachedDevices = 1,
                    SupportsHotPlug = true,
                    OperationalStatus = "OK",
                    Protocol = "SCSI",
                    AvailableLocations = new List<int> { 1, 2, 3 }
                }
            });

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla IImageManagementService
    /// </summary>
    public static Mock<IImageManagementService> ConfigureImageManagementServiceMock()
    {
        var mock = new Mock<IImageManagementService>();
        
        mock.Setup(x => x.GetVirtualHardDiskSettingDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirtualHardDiskSettingData
            {
                Path = "test.vhdx",
                MaxInternalSize = 10737418240, // 10GB
                Format = VirtualDiskFormat.VHDX,
                Type = VirtualDiskType.Dynamic
            });
            
        mock.Setup(x => x.GetVirtualHardDiskStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VirtualHardDiskState());

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla IJobService
    /// </summary>
    public static Mock<IJobService> ConfigureJobServiceMock()
    {
        var mock = new Mock<IJobService>();
        
        mock.Setup(x => x.GetStorageJobsAsync())
            .ReturnsAsync(new List<StorageJobResponse>
            {
                new()
                {
                    JobId = "job-1",
                    OperationType = "CreateVHD",
                    State = StorageJobState.Completed,
                    StartTime = DateTime.UtcNow.AddMinutes(-5),
                    EndTime = DateTime.UtcNow,
                    PercentComplete = 100,
                    Description = "Create VHD job"
                }
            });
            
        mock.Setup(x => x.GetStorageJobAsync(It.IsAny<string>()))
            .ReturnsAsync(new StorageJobResponse
            {
                JobId = "job-1",
                OperationType = "CreateVHD",
                State = StorageJobState.Completed,
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow,
                PercentComplete = 100,
                Description = "Create VHD job"
            });
            
        mock.Setup(x => x.GetJobAffectedElementsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<AffectedElementResponse>
            {
                new()
                {
                    ElementId = "element-1",
                    ElementName = "test.vhdx",
                    ElementType = "VirtualHardDisk",
                    Effects = new List<ElementEffect>
                    {
                        new() { Type = ElementEffectType.Other, Description = "Created" }
                    }
                }
            });

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla HCS VmService
    /// </summary>
    public static Mock<HyperV.Core.Hcs.Services.VmService> ConfigureHcsVmServiceMock()
    {
        var mock = new Mock<HyperV.Core.Hcs.Services.VmService>();
        
        mock.Setup(x => x.IsVmPresent(It.IsAny<string>()))
            .Returns(false); // Domyślnie VM nie istnieje
            
        mock.Setup(x => x.ListVms())
            .Returns(JsonSerializer.Serialize(new
            {
                Count = 0,
                VMs = new object[0],
                Backend = "HCS"
            }));
            
        mock.Setup(x => x.GetVmProperties(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Name = "TestVM",
                State = "Running",
                Backend = "HCS"
            }));

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla WMI VmService
    /// </summary>
    public static Mock<HyperV.Core.Wmi.Services.VmService> ConfigureWmiVmServiceMock()
    {
        var mock = new Mock<HyperV.Core.Wmi.Services.VmService>();
        
        mock.Setup(x => x.IsVmPresent(It.IsAny<string>()))
            .Returns(false); // Domyślnie VM nie istnieje
            
        mock.Setup(x => x.ListVms())
            .Returns(JsonSerializer.Serialize(new
            {
                Count = 0,
                VMs = new object[0],
                Backend = "WMI"
            }));
            
        mock.Setup(x => x.GetVmProperties(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Name = "TestVM",
                State = "Running",
                Backend = "WMI"
            }));

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla VmCreationService
    /// </summary>
    public static Mock<VmCreationService> ConfigureVmCreationServiceMock()
    {
        var mock = new Mock<VmCreationService>();
        
        mock.Setup(x => x.CreateHyperVVm(It.IsAny<string>(), It.IsAny<CreateVmRequest>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = "vm-1",
                Name = "TestVM",
                Status = "Created",
                Backend = "WMI"
            }));

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla ReplicationService
    /// </summary>
    public static Mock<ReplicationService> ConfigureReplicationServiceMock()
    {
        var mock = new Mock<ReplicationService>();
        
        mock.Setup(x => x.IsVmPresent(It.IsAny<string>()))
            .Returns(false);

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla NetworkService
    /// </summary>
    public static Mock<NetworkService> ConfigureNetworkServiceMock()
    {
        var mock = new Mock<NetworkService>();
        
        mock.Setup(x => x.CreateNATNetwork(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Guid.NewGuid());
            
        mock.Setup(x => x.QueryNetworkProperties(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = Guid.NewGuid(),
                Name = "TestNetwork",
                Type = "NAT",
                Subnet = "192.168.100.0/24"
            }));
            
        mock.Setup(x => x.CreateEndpoint(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Guid.NewGuid());

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla HCS ContainerService
    /// </summary>
    public static Mock<HyperV.Core.Hcs.Services.ContainerService> ConfigureHcsContainerServiceMock()
    {
        var mock = new Mock<HyperV.Core.Hcs.Services.ContainerService>();
        
        mock.Setup(x => x.IsContainerPresent(It.IsAny<string>()))
            .Returns(false);
            
        mock.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<CreateContainerRequest>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = "container-1",
                Name = "TestContainer",
                Status = "Created",
                Mode = "HCS"
            }));
            
        mock.Setup(x => x.GetContainerProperties(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = "container-1",
                Name = "TestContainer",
                State = "Running",
                Backend = "HCS"
            }));

        return mock;
    }

    /// <summary>
    /// Konfiguruje mock dla WMI ContainerService
    /// </summary>
    public static Mock<HyperV.Core.Wmi.Services.ContainerService> ConfigureWmiContainerServiceMock()
    {
        var mock = new Mock<HyperV.Core.Wmi.Services.ContainerService>();
        
        mock.Setup(x => x.IsContainerPresent(It.IsAny<string>()))
            .Returns(false);
            
        mock.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<CreateContainerRequest>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = "container-1",
                Name = "TestContainer",
                Status = "Created",
                Mode = "WMI"
            }));
            
        mock.Setup(x => x.GetContainerProperties(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new
            {
                Id = "container-1",
                Name = "TestContainer",
                State = "Running",
                Backend = "WMI"
            }));

        return mock;
    }
}