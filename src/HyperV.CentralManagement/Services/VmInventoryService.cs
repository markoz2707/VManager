using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

public class VmInventoryService
{
    private readonly AppDbContext _context;
    private readonly AgentApiClient _agentClient;
    private readonly ILogger<VmInventoryService> _logger;

    public VmInventoryService(
        AppDbContext context,
        AgentApiClient agentClient,
        ILogger<VmInventoryService> logger)
    {
        _context = context;
        _agentClient = agentClient;
        _logger = logger;
    }

    /// <summary>
    /// Get all VMs across all agents
    /// </summary>
    public async Task<List<VmInventory>> GetAllVmsAsync()
    {
        return await _context.VmInventory
            .Include(v => v.AgentHost)
            .Include(v => v.Folder)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Get VMs for a specific agent
    /// </summary>
    public async Task<List<VmInventory>> GetVmsByAgentAsync(Guid agentHostId)
    {
        return await _context.VmInventory
            .Include(v => v.Folder)
            .Where(v => v.AgentHostId == agentHostId)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Get a specific VM by ID
    /// </summary>
    public async Task<VmInventory?> GetVmAsync(Guid id)
    {
        return await _context.VmInventory
            .Include(v => v.AgentHost)
            .Include(v => v.Folder)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    /// <summary>
    /// Get VMs by folder
    /// </summary>
    public async Task<List<VmInventory>> GetVmsByFolderAsync(Guid? folderId)
    {
        return await _context.VmInventory
            .Include(v => v.AgentHost)
            .Where(v => v.FolderId == folderId)
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Search VMs by name or tags
    /// </summary>
    public async Task<List<VmInventory>> SearchVmsAsync(string query)
    {
        var lowerQuery = query.ToLower();
        return await _context.VmInventory
            .Include(v => v.AgentHost)
            .Include(v => v.Folder)
            .Where(v => v.Name.ToLower().Contains(lowerQuery) ||
                        (v.Tags != null && v.Tags.ToLower().Contains(lowerQuery)))
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Sync VMs from a specific agent
    /// </summary>
    public async Task<int> SyncVmsFromAgentAsync(AgentHost agent)
    {
        try
        {
            var vmList = await _agentClient.GetVmsAsync(agent.ApiBaseUrl);
            if (vmList == null)
            {
                _logger.LogWarning("Failed to retrieve VMs from agent {AgentName}", agent.Hostname);
                return 0;
            }

            var syncedCount = 0;
            var now = DateTimeOffset.UtcNow;

            // Process HCS VMs
            foreach (var vm in vmList.Hcs)
            {
                await UpsertVmAsync(agent.Id, vm, "HCS", now);
                syncedCount++;
            }

            // Process WMI VMs
            foreach (var vm in vmList.Wmi)
            {
                await UpsertVmAsync(agent.Id, vm, "WMI", now);
                syncedCount++;
            }

            // Remove VMs that are no longer on the agent
            var allVmIds = vmList.Hcs.Select(v => v.Id)
                .Concat(vmList.Wmi.Select(v => v.Id))
                .ToHashSet();

            var staleVms = await _context.VmInventory
                .Where(v => v.AgentHostId == agent.Id && !allVmIds.Contains(v.VmId))
                .ToListAsync();

            if (staleVms.Any())
            {
                _context.VmInventory.RemoveRange(staleVms);
                _logger.LogInformation("Removed {Count} stale VMs from agent {AgentName}",
                    staleVms.Count, agent.Hostname);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Synced {Count} VMs from agent {AgentName}",
                syncedCount, agent.Hostname);

            return syncedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing VMs from agent {AgentName}", agent.Hostname);
            return 0;
        }
    }

    private async Task UpsertVmAsync(Guid agentHostId, VmInfo vm, string environment, DateTimeOffset syncTime)
    {
        var existing = await _context.VmInventory
            .FirstOrDefaultAsync(v => v.AgentHostId == agentHostId && v.VmId == vm.Id);

        if (existing != null)
        {
            existing.Name = vm.Name;
            existing.State = vm.State;
            existing.CpuCount = vm.CpuCount;
            existing.MemoryMB = vm.MemoryMB;
            existing.Environment = environment;
            existing.LastSyncUtc = syncTime;
        }
        else
        {
            _context.VmInventory.Add(new VmInventory
            {
                Id = Guid.NewGuid(),
                AgentHostId = agentHostId,
                VmId = vm.Id,
                Name = vm.Name,
                State = vm.State,
                CpuCount = vm.CpuCount,
                MemoryMB = vm.MemoryMB,
                Environment = environment,
                LastSyncUtc = syncTime
            });
        }
    }

    /// <summary>
    /// Update VM metadata (folder, tags, notes)
    /// </summary>
    public async Task<bool> UpdateVmMetadataAsync(Guid id, Guid? folderId, string? tags, string? notes)
    {
        var vm = await _context.VmInventory.FindAsync(id);
        if (vm == null) return false;

        vm.FolderId = folderId;
        vm.Tags = tags;
        vm.Notes = notes;

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Power operation on a VM
    /// </summary>
    public async Task<bool> PowerOperationAsync(Guid id, string operation)
    {
        var vm = await _context.VmInventory
            .Include(v => v.AgentHost)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vm?.AgentHost == null) return false;

        var success = operation.ToLower() switch
        {
            "start" => await _agentClient.StartVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name),
            "stop" => await _agentClient.StopVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name),
            "shutdown" => await _agentClient.ShutdownVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name),
            "pause" => await _agentClient.PauseVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name),
            "resume" => await _agentClient.ResumeVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name),
            _ => false
        };

        if (success)
        {
            // Trigger a sync to update state
            await SyncVmsFromAgentAsync(vm.AgentHost);
        }

        return success;
    }

    /// <summary>
    /// Migrate VM to another host
    /// </summary>
    public async Task<MigrationResponse?> MigrateVmAsync(Guid id, string destinationHost, bool live, bool includeStorage)
    {
        var vm = await _context.VmInventory
            .Include(v => v.AgentHost)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vm?.AgentHost == null) return null;

        var request = new MigrationRequest
        {
            DestinationHost = destinationHost,
            Live = live,
            IncludeStorage = includeStorage
        };

        return await _agentClient.MigrateVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name, request);
    }

    // Folder operations

    /// <summary>
    /// Get all folders
    /// </summary>
    public async Task<List<VmFolder>> GetFoldersAsync()
    {
        return await _context.VmFolders
            .Include(f => f.Children)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Get root folders (no parent)
    /// </summary>
    public async Task<List<VmFolder>> GetRootFoldersAsync()
    {
        return await _context.VmFolders
            .Include(f => f.Children)
            .Where(f => f.ParentId == null)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Create a folder
    /// </summary>
    public async Task<VmFolder> CreateFolderAsync(string name, Guid? parentId)
    {
        var folder = new VmFolder
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parentId
        };

        _context.VmFolders.Add(folder);
        await _context.SaveChangesAsync();

        return folder;
    }

    /// <summary>
    /// Rename a folder
    /// </summary>
    public async Task<bool> RenameFolderAsync(Guid id, string newName)
    {
        var folder = await _context.VmFolders.FindAsync(id);
        if (folder == null) return false;

        folder.Name = newName;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Delete a folder
    /// </summary>
    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        var folder = await _context.VmFolders
            .Include(f => f.Children)
            .Include(f => f.Vms)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (folder == null) return false;

        // Move VMs to no folder
        foreach (var vm in folder.Vms)
        {
            vm.FolderId = null;
        }

        // Check for children
        if (folder.Children.Any())
        {
            // Move children to parent
            foreach (var child in folder.Children)
            {
                child.ParentId = folder.ParentId;
            }
        }

        _context.VmFolders.Remove(folder);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Move VM to folder
    /// </summary>
    public async Task<bool> MoveVmToFolderAsync(Guid vmId, Guid? folderId)
    {
        var vm = await _context.VmInventory.FindAsync(vmId);
        if (vm == null) return false;

        vm.FolderId = folderId;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get VM statistics
    /// </summary>
    public async Task<VmStatistics> GetStatisticsAsync()
    {
        var vms = await _context.VmInventory.ToListAsync();

        return new VmStatistics
        {
            TotalVms = vms.Count,
            RunningVms = vms.Count(v => v.State.Equals("Running", StringComparison.OrdinalIgnoreCase)),
            StoppedVms = vms.Count(v => v.State.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                                        v.State.Equals("Stopped", StringComparison.OrdinalIgnoreCase)),
            PausedVms = vms.Count(v => v.State.Equals("Paused", StringComparison.OrdinalIgnoreCase)),
            SavedVms = vms.Count(v => v.State.Equals("Saved", StringComparison.OrdinalIgnoreCase)),
            TotalCpus = vms.Sum(v => v.CpuCount),
            TotalMemoryMB = vms.Sum(v => v.MemoryMB)
        };
    }
}

public class VmStatistics
{
    public int TotalVms { get; set; }
    public int RunningVms { get; set; }
    public int StoppedVms { get; set; }
    public int PausedVms { get; set; }
    public int SavedVms { get; set; }
    public int TotalCpus { get; set; }
    public long TotalMemoryMB { get; set; }
}
