using System.Net.Http.Headers;
using System.Net.Http.Json;
using HyperV.CentralManagement.Models;
using Microsoft.AspNetCore.Components;

namespace HyperV.CentralManagement.Services;

public class CentralApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _nav;
    private readonly AuthSession _session;

    public CentralApiClient(IHttpClientFactory httpClientFactory, NavigationManager nav, AuthSession session)
    {
        _httpClientFactory = httpClientFactory;
        _nav = nav;
        _session = session;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("central");
        client.BaseAddress = new Uri(_nav.BaseUri);
        if (!string.IsNullOrWhiteSpace(_session.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }

        return client;
    }

    public async Task<IReadOnlyList<AgentHost>> GetAgentsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AgentHost>>("api/agents") ?? new List<AgentHost>();
    }

    public async Task<AgentHost?> GetAgentAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<AgentHost>($"api/agents/{id}");
    }

    public async Task<IReadOnlyList<Cluster>> GetClustersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<Cluster>>("api/clusters") ?? new List<Cluster>();
    }

    public async Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AuditLog>>("api/audit") ?? new List<AuditLog>();
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync("api/auth/login", new { username, password });
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return payload?.Token;
    }

    private sealed record LoginResponse(string Token);
}
