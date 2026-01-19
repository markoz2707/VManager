using System.Net.Http.Json;

namespace HyperV.CentralManagement.Services;

public class AgentApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AgentHealthResponse?> GetHealthAsync(string apiBaseUrl)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(EnsureTrailingSlash(apiBaseUrl));
        return await client.GetFromJsonAsync<AgentHealthResponse>("api/v1/health");
    }

    public async Task<VmListResponse?> GetVmsAsync(string apiBaseUrl)
    {
        using var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(EnsureTrailingSlash(apiBaseUrl));
        return await client.GetFromJsonAsync<VmListResponse>("api/v1/vms");
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : value + "/";
    }
}

public record AgentHealthResponse(string Status);

public record VmListResponse(List<VmInfo> Hcs, List<VmInfo> Wmi);

public record VmInfo(string Id, string Name, string State);
