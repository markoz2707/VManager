using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HyperV.CentralManagement.Services;

public class AgentApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonSerializerOptions _jsonOptions;

    public AgentApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private HttpClient CreateClient(string apiBaseUrl)
    {
        var client = _httpClientFactory.CreateClient("AgentClient");
        client.BaseAddress = new Uri(EnsureTrailingSlash(apiBaseUrl));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    // Health check
    public async Task<AgentHealthResponse?> GetHealthAsync(string apiBaseUrl)
    {
        using var client = CreateClient(apiBaseUrl);
        return await client.GetFromJsonAsync<AgentHealthResponse>("api/v1/health", _jsonOptions);
    }

    // VM List
    public async Task<VmListResponse?> GetVmsAsync(string apiBaseUrl)
    {
        using var client = CreateClient(apiBaseUrl);
        return await client.GetFromJsonAsync<VmListResponse>("api/v1/vms", _jsonOptions);
    }

    // VM Properties
    public async Task<VmPropertiesResponse?> GetVmPropertiesAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        return await client.GetFromJsonAsync<VmPropertiesResponse>($"api/v1/vms/{vmName}/properties", _jsonOptions);
    }

    // VM Power Operations
    public async Task<bool> StartVmAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsync($"api/v1/vms/{vmName}/start", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> StopVmAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsync($"api/v1/vms/{vmName}/stop", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ShutdownVmAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsync($"api/v1/vms/{vmName}/shutdown", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PauseVmAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsync($"api/v1/vms/{vmName}/pause", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResumeVmAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsync($"api/v1/vms/{vmName}/resume", null);
        return response.IsSuccessStatusCode;
    }

    // VM Migration
    public async Task<MigrationResponse?> MigrateVmAsync(string apiBaseUrl, string vmName, MigrationRequest request)
    {
        using var client = CreateClient(apiBaseUrl);
        var response = await client.PostAsJsonAsync($"api/v1/vms/{vmName}/migrate", request, _jsonOptions);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<MigrationResponse>(_jsonOptions);
    }

    // VM Metrics
    public async Task<VmMetricsResponse?> GetVmMetricsAsync(string apiBaseUrl, string vmName)
    {
        using var client = CreateClient(apiBaseUrl);
        return await client.GetFromJsonAsync<VmMetricsResponse>($"api/v1/vms/{vmName}/metrics/usage", _jsonOptions);
    }

    // Host Metrics
    public async Task<HostMetricsResponse?> GetHostMetricsAsync(string apiBaseUrl)
    {
        using var client = CreateClient(apiBaseUrl);
        return await client.GetFromJsonAsync<HostMetricsResponse>("api/v1/host/metrics/usage", _jsonOptions);
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
    }
}

// DTOs
public record AgentHealthResponse(string Status);

public class VmListResponse
{
    [JsonPropertyName("hcsVms")]
    public List<VmInfo> Hcs { get; set; } = new();

    [JsonPropertyName("wmiVms")]
    public List<VmInfo> Wmi { get; set; } = new();
}

public class VmInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string State { get; set; } = "";
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
}

public class VmPropertiesResponse
{
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public bool EnableDynamicMemory { get; set; }
    public long? MinMemoryMB { get; set; }
    public long? MaxMemoryMB { get; set; }
}

public class MigrationRequest
{
    public string DestinationHost { get; set; } = "";
    public bool Live { get; set; } = true;
    public bool IncludeStorage { get; set; } = false;
}

public class MigrationResponse
{
    public bool Success { get; set; }
    public string? JobId { get; set; }
    public string? Message { get; set; }
}

public class VmMetricsResponse
{
    public CpuMetrics Cpu { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
}

public class CpuMetrics
{
    public double UsagePercent { get; set; }
    public double GuestAverageUsage { get; set; }
}

public class MemoryMetrics
{
    public long AssignedMB { get; set; }
    public long DemandMB { get; set; }
    public double UsagePercent { get; set; }
    public string Status { get; set; } = "";
}

public class HostMetricsResponse
{
    public HostCpuMetrics Cpu { get; set; } = new();
    public HostMemoryMetrics Memory { get; set; } = new();
}

public class HostCpuMetrics
{
    public double UsagePercent { get; set; }
    public int Cores { get; set; }
    public int LogicalProcessors { get; set; }
}

public class HostMemoryMetrics
{
    public long TotalPhysicalMB { get; set; }
    public long AvailableMB { get; set; }
    public long UsedMB { get; set; }
    public double UsagePercent { get; set; }
}
