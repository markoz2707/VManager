using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HyperV.LocalShell.Infrastructure;

public class AgentApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentApiClient(string baseUrl = "https://localhost:8743")
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    // VM Operations
    public async Task<VmListResponse?> GetVmsAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/vms");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VmListResponse>(_jsonOptions);
    }

    public async Task<VmInfo?> GetVmInfoAsync(string vmName)
    {
        var response = await _httpClient.GetAsync($"/api/v1/vms/{vmName}/properties");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<VmInfo>(_jsonOptions);
    }

    public async Task<bool> StartVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/start", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> StopVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/stop", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ShutdownVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/shutdown", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PauseVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/pause", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResumeVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/resume", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SaveVmAsync(string vmName)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/save", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<SnapshotInfo>?> GetSnapshotsAsync(string vmName)
    {
        var response = await _httpClient.GetAsync($"/api/v1/vms/{vmName}/snapshots");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<SnapshotInfo>>(_jsonOptions);
    }

    public async Task<bool> CreateSnapshotAsync(string vmName, string name, string? description = null)
    {
        var content = JsonContent.Create(new { name, description });
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/snapshots", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteSnapshotAsync(string vmName, string snapshotId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/vms/{vmName}/snapshots/{snapshotId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RevertSnapshotAsync(string vmName, string snapshotId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/vms/{vmName}/snapshots/{snapshotId}/revert", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<VmUsageSummary?> GetVmMetricsAsync(string vmName)
    {
        var response = await _httpClient.GetAsync($"/api/v1/vms/{vmName}/metrics/usage");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<VmUsageSummary>(_jsonOptions);
    }

    // Network Operations
    public async Task<NetworkListResponse?> GetNetworksAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/networks");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<NetworkListResponse>(_jsonOptions);
    }

    // Storage Operations
    public async Task<List<StorageDevice>?> GetStorageDevicesAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/storage/devices");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<StorageDevice>>(_jsonOptions);
    }

    public async Task<List<StorageLocation>?> GetStorageLocationsAsync(int minGb = 0)
    {
        var response = await _httpClient.GetAsync($"/api/v1/storage/locations?minGb={minGb}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<List<StorageLocation>>(_jsonOptions);
    }

    // Host Operations
    public async Task<HostHardwareInfo?> GetHostHardwareAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/host/hardware");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HostHardwareInfo>(_jsonOptions);
    }

    public async Task<HostSystemInfo?> GetHostSystemAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/host/system");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HostSystemInfo>(_jsonOptions);
    }

    public async Task<HostUsageSummary?> GetHostMetricsAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/host/metrics/usage");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<HostUsageSummary>(_jsonOptions);
    }

    public async Task<ServiceHealth?> GetHealthAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/service/health");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ServiceHealth>(_jsonOptions);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

// DTOs
public class VmListResponse
{
    public List<VmInfo> HcsVms { get; set; } = new();
    public List<VmInfo> WmiVms { get; set; } = new();
}

public class VmInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public bool EnableDynamicMemory { get; set; }
    public long? MinMemoryMB { get; set; }
    public long? MaxMemoryMB { get; set; }
}

public class SnapshotInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreationTime { get; set; }
    public string? Type { get; set; }
}

public class VmUsageSummary
{
    public CpuUsage Cpu { get; set; } = new();
    public MemoryUsage Memory { get; set; } = new();
    public List<DiskUsage> Disks { get; set; } = new();
    public List<NetworkUsage> Networks { get; set; } = new();
}

public class CpuUsage
{
    public double UsagePercent { get; set; }
    public double GuestAverageUsage { get; set; }
}

public class MemoryUsage
{
    public long AssignedMB { get; set; }
    public long DemandMB { get; set; }
    public double UsagePercent { get; set; }
    public string Status { get; set; } = "";
}

public class DiskUsage
{
    public string Name { get; set; } = "";
    public long ReadIops { get; set; }
    public long WriteIops { get; set; }
    public double LatencyMs { get; set; }
    public long ThroughputBytesPerSec { get; set; }
}

public class NetworkUsage
{
    public string AdapterName { get; set; } = "";
    public long BytesReceivedPerSec { get; set; }
    public long BytesSentPerSec { get; set; }
    public long PacketsDropped { get; set; }
}

public class NetworkListResponse
{
    public List<NetworkInfo> HcnNetworks { get; set; } = new();
    public List<NetworkInfo> WmiSwitches { get; set; } = new();
}

public class NetworkInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Subnet { get; set; }
}

public class StorageDevice
{
    public string Id { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string Path { get; set; } = "";
    public bool ReadOnly { get; set; }
}

public class StorageLocation
{
    public string Path { get; set; } = "";
    public long FreeSpaceGB { get; set; }
    public long TotalSpaceGB { get; set; }
}

public class HostHardwareInfo
{
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string BiosVersion { get; set; } = "";
    public string CpuName { get; set; } = "";
    public int CpuCores { get; set; }
    public int CpuLogicalProcessors { get; set; }
    public long TotalMemoryMB { get; set; }
}

public class HostSystemInfo
{
    public string OsName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public DateTime LastBootTime { get; set; }
    public string SystemUuid { get; set; } = "";
}

public class HostUsageSummary
{
    public HostCpuUsage Cpu { get; set; } = new();
    public HostMemoryUsage Memory { get; set; } = new();
    public List<PhysicalDiskUsage> PhysicalDisks { get; set; } = new();
    public List<HostNetworkAdapter> NetworkAdapters { get; set; } = new();
}

public class HostCpuUsage
{
    public double UsagePercent { get; set; }
    public int Cores { get; set; }
    public int LogicalProcessors { get; set; }
}

public class HostMemoryUsage
{
    public long TotalPhysicalMB { get; set; }
    public long AvailableMB { get; set; }
    public long UsedMB { get; set; }
    public double UsagePercent { get; set; }
}

public class PhysicalDiskUsage
{
    public string Name { get; set; } = "";
    public long Iops { get; set; }
    public double ThroughputMBps { get; set; }
    public double LatencyMs { get; set; }
    public long QueueLength { get; set; }
}

public class HostNetworkAdapter
{
    public string Name { get; set; } = "";
    public long SpeedMbps { get; set; }
    public long BytesSentPerSec { get; set; }
    public long BytesReceivedPerSec { get; set; }
}

public class ServiceHealth
{
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Components { get; set; } = new();
}
