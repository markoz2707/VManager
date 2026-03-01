using System.Net.Http.Headers;
using System.Net.Http.Json;
using HyperV.CentralManagement.Controllers;
using HyperV.CentralManagement.Models;

namespace HyperV.CentralManagement.Services;

public class CentralApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthSession _session;

    public CentralApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, AuthSession session)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _session = session;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("central");
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            client.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
        }
        if (!string.IsNullOrWhiteSpace(_session.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);
        }

        return client;
    }

    // === Auth ===

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

    // === Agents ===

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

    public async Task<HttpResponseMessage> RegisterAgentAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/agents", payload);
    }

    public async Task<HttpResponseMessage> CreateRegistrationTokenAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/agents/tokens", payload);
    }

    // === Clusters ===

    public async Task<IReadOnlyList<Cluster>> GetClustersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<Cluster>>("api/clusters") ?? new List<Cluster>();
    }

    public async Task<Cluster?> GetClusterAsync(Guid id)
    {
        using var client = CreateClient();
        var clusters = await client.GetFromJsonAsync<List<Cluster>>("api/clusters") ?? new List<Cluster>();
        return clusters.FirstOrDefault(c => c.Id == id);
    }

    public async Task<HttpResponseMessage> CreateClusterAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/clusters", payload);
    }

    public async Task<HttpResponseMessage> UpdateClusterAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/clusters/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteClusterAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/clusters/{id}");
    }

    public async Task<HttpResponseMessage> AddClusterNodeAsync(Guid clusterId, object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/clusters/{clusterId}/nodes", payload);
    }

    public async Task<HttpResponseMessage> RemoveClusterNodeAsync(Guid clusterId, Guid nodeId)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/clusters/{clusterId}/nodes/{nodeId}");
    }

    // === Users ===

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<UserDto>>("api/users") ?? new List<UserDto>();
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<UserDto>($"api/users/{id}");
    }

    public async Task<HttpResponseMessage> CreateUserAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/users", payload);
    }

    public async Task<HttpResponseMessage> UpdateUserAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/users/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteUserAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/users/{id}");
    }

    public async Task<HttpResponseMessage> AssignRoleAsync(Guid userId, object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/users/{userId}/roles", payload);
    }

    public async Task<HttpResponseMessage> RemoveRoleAssignmentAsync(Guid userId, Guid userRoleId)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/users/{userId}/roles/{userRoleId}");
    }

    // === Roles ===

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<RoleDto>>("api/roles") ?? new List<RoleDto>();
    }

    public async Task<RoleDto?> GetRoleAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<RoleDto>($"api/roles/{id}");
    }

    public async Task<HttpResponseMessage> CreateRoleAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/roles", payload);
    }

    public async Task<HttpResponseMessage> UpdateRoleAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/roles/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteRoleAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/roles/{id}");
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<PermissionDto>>("api/roles/permissions") ?? new List<PermissionDto>();
    }

    // === Alerts ===

    public async Task<IReadOnlyList<AlertDefinition>> GetAlertDefinitionsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AlertDefinition>>("api/alerts/definitions") ?? new List<AlertDefinition>();
    }

    public async Task<AlertDefinition?> GetAlertDefinitionAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<AlertDefinition>($"api/alerts/definitions/{id}");
    }

    public async Task<HttpResponseMessage> CreateAlertDefinitionAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/alerts/definitions", payload);
    }

    public async Task<HttpResponseMessage> UpdateAlertDefinitionAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/alerts/definitions/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteAlertDefinitionAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/alerts/definitions/{id}");
    }

    public async Task<IReadOnlyList<AlertInstance>> GetActiveAlertsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AlertInstance>>("api/alerts/active") ?? new List<AlertInstance>();
    }

    public async Task<IReadOnlyList<AlertInstance>> GetAlertHistoryAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AlertInstance>>("api/alerts/history") ?? new List<AlertInstance>();
    }

    public async Task<HttpResponseMessage> AcknowledgeAlertAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/alerts/{id}/acknowledge", null);
    }

    public async Task<HttpResponseMessage> ResolveAlertAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/alerts/{id}/resolve", null);
    }

    // === Notification Channels ===

    public async Task<IReadOnlyList<NotificationChannel>> GetNotificationChannelsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<NotificationChannel>>("api/notification-channels") ?? new List<NotificationChannel>();
    }

    public async Task<HttpResponseMessage> CreateNotificationChannelAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/notification-channels", payload);
    }

    public async Task<HttpResponseMessage> UpdateNotificationChannelAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/notification-channels/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteNotificationChannelAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/notification-channels/{id}");
    }

    public async Task<HttpResponseMessage> TestNotificationChannelAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/notification-channels/{id}/test", null);
    }

    // === VMs ===

    public async Task<IReadOnlyList<VmInventoryDto>> GetVmsAsync(string? search = null)
    {
        using var client = CreateClient();
        var url = string.IsNullOrWhiteSpace(search) ? "api/v1/vms" : $"api/v1/vms?search={Uri.EscapeDataString(search)}";
        return await client.GetFromJsonAsync<List<VmInventoryDto>>(url) ?? new List<VmInventoryDto>();
    }

    public async Task<IReadOnlyList<VmInventoryDto>> GetVmsByAgentAsync(Guid agentId)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<VmInventoryDto>>($"api/v1/vms/agent/{agentId}") ?? new List<VmInventoryDto>();
    }

    public async Task<VmInventoryDto?> GetVmAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<VmInventoryDto>($"api/v1/vms/{id}");
    }

    public async Task<HttpResponseMessage> PowerVmAsync(Guid vmId, string operation)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/v1/vms/{vmId}/power", new { operation });
    }

    public async Task<HttpResponseMessage> MigrateVmAsync(Guid vmId, object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/v1/vms/{vmId}/migrate", payload);
    }

    public async Task<VmStatistics?> GetVmStatisticsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<VmStatistics>("api/v1/vms/statistics");
    }

    // === VM Folders ===

    public async Task<IReadOnlyList<VmFolderDto>> GetVmFoldersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<VmFolderDto>>("api/v1/vms/folders") ?? new List<VmFolderDto>();
    }

    public async Task<IReadOnlyList<VmFolderDto>> GetVmRootFoldersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<VmFolderDto>>("api/v1/vms/folders/root") ?? new List<VmFolderDto>();
    }

    public async Task<HttpResponseMessage> CreateVmFolderAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/v1/vms/folders", payload);
    }

    public async Task<IReadOnlyList<VmInventoryDto>> GetVmsByFolderAsync(Guid folderId)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<VmInventoryDto>>($"api/v1/vms/folder/{folderId}") ?? new List<VmInventoryDto>();
    }

    public async Task<IReadOnlyList<VmInventoryDto>> GetVmsWithoutFolderAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<VmInventoryDto>>("api/v1/vms/folder/root") ?? new List<VmInventoryDto>();
    }

    // === Datacenters ===

    public async Task<IReadOnlyList<Datacenter>> GetDatacentersAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<Datacenter>>("api/datacenters") ?? new List<Datacenter>();
    }

    public async Task<HttpResponseMessage> CreateDatacenterAsync(object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync("api/datacenters", payload);
    }

    public async Task<HttpResponseMessage> UpdateDatacenterAsync(Guid id, object payload)
    {
        using var client = CreateClient();
        return await client.PutAsJsonAsync($"api/datacenters/{id}", payload);
    }

    public async Task<HttpResponseMessage> DeleteDatacenterAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/datacenters/{id}");
    }

    // === Metrics ===

    public async Task<DashboardData?> GetMetricsDashboardAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<DashboardData>("api/metrics/dashboard");
    }

    // === HA ===

    public async Task<HaConfiguration?> GetHaConfigAsync(Guid clusterId)
    {
        using var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<HaConfiguration>($"api/ha/config/{clusterId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<HttpResponseMessage> SaveHaConfigAsync(Guid clusterId, object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/ha/config/{clusterId}", payload);
    }

    public async Task<IReadOnlyList<HaEvent>> GetHaEventsAsync(Guid clusterId)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<HaEvent>>($"api/ha/events/{clusterId}") ?? new List<HaEvent>();
    }

    // === DRS ===

    public async Task<DrsConfiguration?> GetDrsConfigAsync(Guid clusterId)
    {
        using var client = CreateClient();
        try
        {
            return await client.GetFromJsonAsync<DrsConfiguration>($"api/drs/config/{clusterId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<HttpResponseMessage> SaveDrsConfigAsync(Guid clusterId, object payload)
    {
        using var client = CreateClient();
        return await client.PostAsJsonAsync($"api/drs/config/{clusterId}", payload);
    }

    public async Task<IReadOnlyList<DrsRecommendation>> GetDrsRecommendationsAsync(Guid clusterId)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<DrsRecommendation>>($"api/drs/recommendations/{clusterId}") ?? new List<DrsRecommendation>();
    }

    public async Task<HttpResponseMessage> ApplyDrsRecommendationAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/drs/recommendations/{id}/apply", null);
    }

    public async Task<HttpResponseMessage> RejectDrsRecommendationAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/drs/recommendations/{id}/reject", null);
    }

    // === Content Library ===

    public async Task<IReadOnlyList<ContentLibraryItemDto>> GetContentLibraryAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<ContentLibraryItemDto>>("api/content-library") ?? new List<ContentLibraryItemDto>();
    }

    public async Task<HttpResponseMessage> DeleteContentLibraryItemAsync(Guid id)
    {
        using var client = CreateClient();
        return await client.DeleteAsync($"api/content-library/{id}");
    }

    // === Audit ===

    public async Task<IReadOnlyList<AuditLog>> GetAuditLogsAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<AuditLog>>("api/audit") ?? new List<AuditLog>();
    }

    // === Migrations ===

    public async Task<IReadOnlyList<MigrationTask>> GetMigrationHistoryAsync(Guid vmId)
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<List<MigrationTask>>($"api/v1/vms/{vmId}/migrate/history") ?? new List<MigrationTask>();
    }

    public async Task<HttpResponseMessage> CancelMigrationAsync(Guid taskId)
    {
        using var client = CreateClient();
        return await client.PostAsync($"api/v1/vms/migrate/{taskId}/cancel", null);
    }

    // === DTOs ===
    private sealed record LoginResponse(string Token);
}

/// <summary>
/// DTO matching the MetricsController dashboard endpoint response
/// </summary>
public class DashboardData
{
    public int TotalAgents { get; set; }
    public int OnlineAgents { get; set; }
    public int TotalVms { get; set; }
    public int RunningVms { get; set; }
    public int ActiveAlerts { get; set; }
    public List<DashboardAgentInfo> Agents { get; set; } = new();
}

public class DashboardAgentInfo
{
    public Guid Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string HostType { get; set; } = string.Empty;
    public DateTimeOffset LastSeenUtc { get; set; }
}

public class ContentLibraryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Version { get; set; }
    public long FileSize { get; set; }
    public string? Checksum { get; set; }
    public string? Tags { get; set; }
    public string? Category { get; set; }
    public bool IsPublic { get; set; }
    public Guid? OwnerId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? ModifiedUtc { get; set; }
    public int SubscriptionCount { get; set; }
}
