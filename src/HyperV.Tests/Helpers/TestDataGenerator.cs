using HyperV.Contracts.Models;
using HyperV.Agent.Controllers;

namespace HyperV.Tests.Helpers;

/// <summary>
/// Klasa do generowania przykładowych danych testowych
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Generuje przykładowe żądanie utworzenia VM
    /// </summary>
    public static CreateVmRequest CreateVmRequest(
        string? id = null,
        string? name = null,
        int? memoryMB = null,
        int? cpuCount = null,
        int? diskSizeGB = null,
        VmCreationMode mode = VmCreationMode.WMI)
    {
        return new CreateVmRequest
        {
            Id = id ?? $"test-vm-{Guid.NewGuid():N}",
            Name = name ?? $"TestVM-{DateTime.Now:yyyyMMdd-HHmmss}",
            MemoryMB = memoryMB ?? 2048,
            CpuCount = cpuCount ?? 2,
            DiskSizeGB = diskSizeGB ?? 20,
            Mode = mode
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie utworzenia kontenera
    /// </summary>
    public static CreateContainerRequest CreateContainerRequest(
        string? id = null,
        string? name = null,
        string? image = null,
        int? memoryMB = null,
        int? cpuCount = null,
        int? storageSizeGB = null,
        ContainerCreationMode mode = ContainerCreationMode.HCS)
    {
        return new CreateContainerRequest
        {
            Id = id ?? $"test-container-{Guid.NewGuid():N}",
            Name = name ?? $"TestContainer-{DateTime.Now:yyyyMMdd-HHmmss}",
            Image = image ?? "mcr.microsoft.com/windows/nanoserver:ltsc2022",
            MemoryMB = memoryMB ?? 1024,
            CpuCount = cpuCount ?? 1,
            StorageSizeGB = storageSizeGB ?? 10,
            Mode = mode,
            Environment = new Dictionary<string, string>
            {
                ["TEST_ENV"] = "true",
                ["CONTAINER_TYPE"] = "test"
            },
            PortMappings = new Dictionary<int, int>(),
            VolumeMounts = new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie utworzenia VHD
    /// </summary>
    public static CreateVhdRequest CreateVhdRequest(
        string? path = null,
        ulong? maxInternalSize = null,
        string? format = null,
        string? type = null)
    {
        return new CreateVhdRequest
        {
            Path = path ?? Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.vhdx"),
            MaxInternalSize = maxInternalSize ?? (10UL * 1024 * 1024 * 1024), // 10GB
            Format = format ?? "VHDX",
            Type = type ?? "Dynamic"
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie zmiany rozmiaru VHD
    /// </summary>
    public static ResizeVhdRequest CreateResizeVhdRequest(
        string? path = null,
        ulong? maxInternalSize = null)
    {
        return new ResizeVhdRequest
        {
            Path = path ?? Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.vhdx"),
            MaxInternalSize = maxInternalSize ?? (20UL * 1024 * 1024 * 1024) // 20GB
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie dodania urządzenia pamięci masowej
    /// </summary>
    public static AddStorageDeviceRequest CreateAddStorageDeviceRequest(
        string? deviceType = null,
        string? path = null,
        bool readOnly = false,
        string? controllerId = null)
    {
        return new AddStorageDeviceRequest
        {
            DeviceType = deviceType ?? "VirtualDisk",
            Path = path ?? Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.vhdx"),
            ReadOnly = readOnly,
            ControllerId = controllerId
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie utworzenia dysku różnicowego
    /// </summary>
    public static DifferencingDiskRequest CreateDifferencingDiskRequest(
        string? childPath = null,
        string? parentPath = null)
    {
        return new DifferencingDiskRequest
        {
            ChildPath = childPath ?? Path.Combine(Path.GetTempPath(), $"child-{Guid.NewGuid():N}.vhdx"),
            ParentPath = parentPath ?? Path.Combine(Path.GetTempPath(), $"parent-{Guid.NewGuid():N}.vhdx")
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie łączenia dysków
    /// </summary>
    public static MergeDiskRequest CreateMergeDiskRequest(
        string? childPath = null,
        string? destinationPath = null,
        uint mergeDepth = 1)
    {
        return new MergeDiskRequest
        {
            ChildPath = childPath ?? Path.Combine(Path.GetTempPath(), $"child-{Guid.NewGuid():N}.vhdx"),
            DestinationPath = destinationPath ?? Path.Combine(Path.GetTempPath(), $"merged-{Guid.NewGuid():N}.vhdx"),
            MergeDepth = mergeDepth
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie konwersji VHD
    /// </summary>
    public static ConvertVhdRequest CreateConvertVhdRequest(
        string? sourcePath = null,
        string? destinationPath = null,
        VirtualDiskFormat targetFormat = VirtualDiskFormat.VHDX)
    {
        return new ConvertVhdRequest
        {
            SourcePath = sourcePath ?? Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}.vhd"),
            DestinationPath = destinationPath ?? Path.Combine(Path.GetTempPath(), $"converted-{Guid.NewGuid():N}.vhdx"),
            TargetFormat = targetFormat
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie utworzenia snapshotu VHD
    /// </summary>
    public static CreateVhdSnapshotRequest CreateVhdSnapshotRequest(
        string? name = null,
        string? description = null)
    {
        return new CreateVhdSnapshotRequest
        {
            Name = name ?? $"TestSnapshot-{DateTime.Now:yyyyMMdd-HHmmss}",
            Description = description ?? "Test snapshot created by automated test"
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie walidacji VHD
    /// </summary>
    public static VhdValidationRequest CreateVhdValidationRequest(string? path = null)
    {
        return new VhdValidationRequest
        {
            Path = path ?? GenerateVhdPath()
        };
    }

    /// <summary>
    /// Generuje przykładowe żądanie konwersji VHD Set
    /// </summary>
    public static ConvertToVhdSetRequest CreateConvertToVhdSetRequest(
        string? sourcePath = null,
        string? destinationPath = null)
    {
        return new ConvertToVhdSetRequest
        {
            SourcePath = sourcePath ?? Path.Combine(Path.GetTempPath(), $"source-{Guid.NewGuid():N}.vhdx"),
            DestinationPath = destinationPath ?? Path.Combine(Path.GetTempPath(), $"converted-{Guid.NewGuid():N}.vhdset")
        };
    }

    /// <summary>
    /// Generuje losowy identyfikator GUID jako string
    /// </summary>
    public static string GenerateId() => Guid.NewGuid().ToString();

    /// <summary>
    /// Generuje losową nazwę VM dla testów
    /// </summary>
    public static string GenerateVmName()
    {
        var guid = Guid.NewGuid().ToString("N");
        return $"TestVM-{DateTime.Now:yyyyMMdd-HHmmss}-{guid.Substring(0,8)}";
    }

    /// <summary>
    /// Generuje losową nazwę kontenera dla testów
    /// </summary>
    public static string GenerateContainerName()
    {
        var guid = Guid.NewGuid().ToString("N");
        return $"TestContainer-{DateTime.Now:yyyyMMdd-HHmmss}-{guid.Substring(0,8)}";
    }

    /// <summary>
    /// Generuje losowy identyfikator kontenera dla testów
    /// </summary>
    public static string GenerateContainerId() => $"container-{Guid.NewGuid():N}";

    /// <summary>
    /// Generuje losową ścieżkę do pliku VHD dla testów
    /// </summary>
    public static string GenerateVhdPath() => Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.vhdx");

    /// <summary>
    /// Generuje losowy identyfikator sieci
    /// </summary>
    public static Guid GenerateNetworkId() => Guid.NewGuid();

    /// <summary>
    /// Generuje losową nazwę sieci dla testów
    /// </summary>
    public static string GenerateNetworkName()
    {
        var guid = Guid.NewGuid().ToString("N");
        return $"TestNetwork-{DateTime.Now:yyyyMMdd-HHmmss}-{guid.Substring(0,8)}";
    }

    /// <summary>
    /// Generuje losową nazwę endpointu dla testów
    /// </summary>
    public static string GenerateEndpointName()
    {
        var guid = Guid.NewGuid().ToString("N");
        return $"TestEndpoint-{DateTime.Now:yyyyMMdd-HHmmss}-{guid.Substring(0,8)}";
    }

    /// <summary>
    /// Generuje losowy identyfikator zadania
    /// </summary>
    public static string GenerateJobId() => $"job-{Guid.NewGuid():N}";
}