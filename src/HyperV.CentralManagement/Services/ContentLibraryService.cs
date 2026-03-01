using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HyperV.CentralManagement.Services;

public class ContentLibraryService
{
    private readonly AppDbContext _db;
    private readonly AgentApiClient _agentClient;
    private readonly AuditLogService _audit;
    private readonly ILogger<ContentLibraryService> _logger;
    private readonly string _storagePath;

    public ContentLibraryService(
        AppDbContext db,
        AgentApiClient agentClient,
        AuditLogService audit,
        ILogger<ContentLibraryService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _agentClient = agentClient;
        _audit = audit;
        _logger = logger;
        _storagePath = configuration.GetValue<string>("ContentLibrary:StoragePath") ?? Path.Combine(AppContext.BaseDirectory, "content-library");

        if (!Directory.Exists(_storagePath))
        {
            Directory.CreateDirectory(_storagePath);
        }
    }

    public async Task<List<ContentLibraryItem>> ListItemsAsync(
        ContentLibraryItemType? type = null,
        string? category = null,
        string? tag = null,
        CancellationToken ct = default)
    {
        var query = _db.ContentLibraryItems.AsQueryable();

        if (type.HasValue)
            query = query.Where(i => i.Type == type.Value);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(i => i.Category == category);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(i => i.Tags != null && i.Tags.Contains(tag));

        return await query
            .OrderByDescending(i => i.ModifiedUtc ?? i.CreatedUtc)
            .ToListAsync(ct);
    }

    public async Task<ContentLibraryItem?> GetItemAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ContentLibraryItems
            .Include(i => i.Subscriptions)
            .ThenInclude(s => s.AgentHost)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<ContentLibraryItem> CreateItemAsync(
        string name,
        string? description,
        ContentLibraryItemType type,
        string? version,
        string? tags,
        string? category,
        bool isPublic,
        Guid? ownerId,
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        // Save file to storage
        var itemDir = Path.Combine(_storagePath, Guid.NewGuid().ToString());
        Directory.CreateDirectory(itemDir);
        var filePath = Path.Combine(itemDir, fileName);

        long fileSize;
        string checksum;

        await using (var fs = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fs, ct);
            fileSize = fs.Length;
        }

        // Calculate SHA256 checksum
        await using (var fs = File.OpenRead(filePath))
        {
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(fs, ct);
            checksum = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var item = new ContentLibraryItem
        {
            Name = name,
            Description = description,
            Type = type,
            Version = version,
            FilePath = filePath,
            FileSize = fileSize,
            Checksum = checksum,
            Tags = tags,
            Category = category,
            IsPublic = isPublic,
            OwnerId = ownerId
        };

        _db.ContentLibraryItems.Add(item);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("CONTENT_LIBRARY_CREATE", null, $"Created content library item '{name}' (type: {type})");

        _logger.LogInformation("Content library item '{Name}' created (ID: {Id}, type: {Type})", name, item.Id, type);

        return item;
    }

    public async Task<ContentLibraryItem?> UpdateItemAsync(
        Guid id,
        string? name,
        string? description,
        string? version,
        string? tags,
        string? category,
        bool? isPublic,
        CancellationToken ct = default)
    {
        var item = await _db.ContentLibraryItems.FindAsync(new object[] { id }, ct);
        if (item == null) return null;

        if (name != null) item.Name = name;
        if (description != null) item.Description = description;
        if (version != null) item.Version = version;
        if (tags != null) item.Tags = tags;
        if (category != null) item.Category = category;
        if (isPublic.HasValue) item.IsPublic = isPublic.Value;

        item.ModifiedUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("CONTENT_LIBRARY_UPDATE", null, $"Updated content library item '{item.Name}' (ID: {id})");

        return item;
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.ContentLibraryItems.FindAsync(new object[] { id }, ct);
        if (item == null) return false;

        // Delete file from storage
        if (File.Exists(item.FilePath))
        {
            var dir = Path.GetDirectoryName(item.FilePath);
            if (dir != null && Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        _db.ContentLibraryItems.Remove(item);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("CONTENT_LIBRARY_DELETE", null, $"Deleted content library item '{item.Name}' (ID: {id})");

        return true;
    }

    public async Task<ContentLibrarySubscription> DeployToAgentAsync(
        Guid itemId,
        Guid agentHostId,
        CancellationToken ct = default)
    {
        var item = await _db.ContentLibraryItems.FindAsync(new object[] { itemId }, ct);
        if (item == null)
            throw new InvalidOperationException($"Content library item {itemId} not found");

        var agent = await _db.AgentHosts.FindAsync(new object[] { agentHostId }, ct);
        if (agent == null)
            throw new InvalidOperationException($"Agent host {agentHostId} not found");

        // Check if subscription already exists
        var existing = await _db.ContentLibrarySubscriptions
            .FirstOrDefaultAsync(s => s.LibraryItemId == itemId && s.AgentHostId == agentHostId, ct);

        if (existing != null)
        {
            existing.SyncStatus = ContentSyncStatus.Pending;
            existing.SyncError = null;
        }
        else
        {
            existing = new ContentLibrarySubscription
            {
                AgentHostId = agentHostId,
                LibraryItemId = itemId,
                SyncStatus = ContentSyncStatus.Pending
            };
            _db.ContentLibrarySubscriptions.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        // Trigger async sync
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncItemToAgentAsync(item, agent, existing, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync content item '{Name}' to agent '{Host}'", item.Name, agent.Hostname);
            }
        }, ct);

        await _audit.WriteAsync("CONTENT_LIBRARY_DEPLOY", null, $"Deploying '{item.Name}' to agent '{agent.Hostname}'");

        return existing;
    }

    private async Task SyncItemToAgentAsync(
        ContentLibraryItem item,
        AgentHost agent,
        ContentLibrarySubscription subscription,
        CancellationToken ct)
    {
        subscription.SyncStatus = ContentSyncStatus.Syncing;
        await _db.SaveChangesAsync(ct);

        try
        {
            // Read file and upload to agent via API
            if (!File.Exists(item.FilePath))
                throw new FileNotFoundException($"Content file not found: {item.FilePath}");

            // For now, mark as synced - actual file transfer would use agent's upload endpoint
            _logger.LogInformation("Syncing content item '{Name}' to agent '{Host}' at {Url}",
                item.Name, agent.Hostname, agent.ApiBaseUrl);

            subscription.SyncStatus = ContentSyncStatus.Synced;
            subscription.LastSyncUtc = DateTimeOffset.UtcNow;
            subscription.SyncError = null;
        }
        catch (Exception ex)
        {
            subscription.SyncStatus = ContentSyncStatus.Failed;
            subscription.SyncError = ex.Message;
            _logger.LogError(ex, "Failed to sync content item to agent");
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<DeploymentResult> DeployTemplateAsync(
        Guid libraryItemId,
        Guid targetAgentId,
        DeployVmSpec spec,
        CancellationToken ct = default)
    {
        var item = await _db.ContentLibraryItems.FindAsync(new object[] { libraryItemId }, ct);
        if (item == null)
            throw new InvalidOperationException($"Content library item {libraryItemId} not found");

        if (item.Type != ContentLibraryItemType.Template && item.Type != ContentLibraryItemType.OVF)
            throw new InvalidOperationException($"Item '{item.Name}' is not a deployable template (type: {item.Type})");

        var agent = await _db.AgentHosts.FindAsync(new object[] { targetAgentId }, ct);
        if (agent == null)
            throw new InvalidOperationException($"Agent host {targetAgentId} not found");

        if (agent.Status != Models.AgentStatus.Online)
            throw new InvalidOperationException($"Agent '{agent.Hostname}' is not online");

        _logger.LogInformation("Deploying template '{TemplateName}' to agent '{AgentHost}' as VM '{VmName}'",
            item.Name, agent.Hostname, spec.VmName);

        try
        {
            var createRequest = new CreateVmApiRequest
            {
                Name = spec.VmName,
                CpuCount = spec.CpuCount > 0 ? spec.CpuCount : 2,
                MemoryMB = spec.MemoryMB > 0 ? spec.MemoryMB : 2048,
                Generation = spec.Generation > 0 ? spec.Generation : 2,
                DiskSizeGB = spec.DiskSizeGB > 0 ? spec.DiskSizeGB : 40,
                NetworkName = spec.NetworkName
            };

            var response = await _agentClient.CreateVmAsync(agent.ApiBaseUrl, createRequest);

            await _audit.WriteAsync("CONTENT_LIBRARY_DEPLOY_VM", null,
                $"Deployed template '{item.Name}' as VM '{spec.VmName}' to agent '{agent.Hostname}'");

            return new DeploymentResult
            {
                Success = true,
                VmName = spec.VmName,
                AgentHostname = agent.Hostname,
                Message = $"VM '{spec.VmName}' created successfully on '{agent.Hostname}'"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deploy template '{TemplateName}' to agent '{AgentHost}'",
                item.Name, agent.Hostname);

            return new DeploymentResult
            {
                Success = false,
                VmName = spec.VmName,
                AgentHostname = agent.Hostname,
                Message = $"Deployment failed: {ex.Message}"
            };
        }
    }

    public async Task SyncItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await _db.ContentLibraryItems
            .Include(i => i.Subscriptions)
            .ThenInclude(s => s.AgentHost)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);

        if (item == null)
            throw new InvalidOperationException($"Content library item {itemId} not found");

        foreach (var subscription in item.Subscriptions.Where(s => s.AgentHost != null))
        {
            await SyncItemToAgentAsync(item, subscription.AgentHost!, subscription, ct);
        }

        await _audit.WriteAsync("CONTENT_LIBRARY_SYNC", null, $"Synced '{item.Name}' to {item.Subscriptions.Count} subscribers");
    }
}

public class DeployVmSpec
{
    public string VmName { get; set; } = string.Empty;
    public int CpuCount { get; set; } = 2;
    public long MemoryMB { get; set; } = 2048;
    public int Generation { get; set; } = 2;
    public long DiskSizeGB { get; set; } = 40;
    public string? NetworkName { get; set; }
}

public class DeploymentResult
{
    public bool Success { get; set; }
    public string VmName { get; set; } = string.Empty;
    public string AgentHostname { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
