using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/drs")]
[Authorize]
public class DrsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly MigrationOrchestrator _migrationOrchestrator;
    private readonly AuditLogService _audit;

    public DrsController(AppDbContext db, MigrationOrchestrator migrationOrchestrator, AuditLogService audit)
    {
        _db = db;
        _migrationOrchestrator = migrationOrchestrator;
        _audit = audit;
    }

    [HttpGet("config/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetConfiguration(Guid clusterId)
    {
        var config = await _db.DrsConfigurations.FirstOrDefaultAsync(d => d.ClusterId == clusterId);
        if (config == null) return Ok(new { configured = false, clusterId });
        return Ok(config);
    }

    [HttpPost("config/{clusterId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> CreateOrUpdateConfiguration(Guid clusterId, [FromBody] DrsConfigDto dto)
    {
        var existing = await _db.DrsConfigurations.FirstOrDefaultAsync(d => d.ClusterId == clusterId);

        if (existing != null)
        {
            existing.IsEnabled = dto.IsEnabled;
            existing.AutomationLevel = dto.AutomationLevel;
            existing.CpuImbalanceThreshold = dto.CpuImbalanceThreshold;
            existing.MemoryImbalanceThreshold = dto.MemoryImbalanceThreshold;
            existing.EvaluationIntervalSeconds = dto.EvaluationIntervalSeconds;
            existing.MinBenefitPercent = dto.MinBenefitPercent;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.DrsConfigurations.Add(new DrsConfiguration
            {
                ClusterId = clusterId,
                IsEnabled = dto.IsEnabled,
                AutomationLevel = dto.AutomationLevel,
                CpuImbalanceThreshold = dto.CpuImbalanceThreshold,
                MemoryImbalanceThreshold = dto.MemoryImbalanceThreshold,
                EvaluationIntervalSeconds = dto.EvaluationIntervalSeconds,
                MinBenefitPercent = dto.MinBenefitPercent
            });
        }

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "drs_config_updated", clusterId.ToString());
        return Ok();
    }

    [HttpGet("recommendations/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetRecommendations(Guid clusterId, [FromQuery] DrsRecommendationStatus? status = null)
    {
        var configId = await _db.DrsConfigurations
            .Where(d => d.ClusterId == clusterId)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        if (configId == Guid.Empty) return Ok(Array.Empty<object>());

        var query = _db.DrsRecommendations.Where(r => r.DrsConfigurationId == configId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var recommendations = await query
            .OrderByDescending(r => r.CreatedUtc)
            .Take(50)
            .AsNoTracking()
            .ToListAsync();

        return Ok(recommendations);
    }

    [HttpPost("recommendations/{id:guid}/apply")]
    [RequirePermission("vm", "migrate")]
    public async Task<IActionResult> ApplyRecommendation(Guid id)
    {
        var rec = await _db.DrsRecommendations.FindAsync(id);
        if (rec == null) return NotFound();
        if (rec.Status != DrsRecommendationStatus.Pending)
            return BadRequest(new { error = "Recommendation is not pending." });

        try
        {
            await _migrationOrchestrator.InitiateMigrationAsync(
                rec.VmInventoryId, rec.DestinationAgentId, true, false, User.Identity?.Name);

            rec.Status = DrsRecommendationStatus.Applied;
            rec.AppliedUtc = DateTimeOffset.UtcNow;
            rec.AppliedBy = User.Identity?.Name;
            await _db.SaveChangesAsync();

            await _audit.WriteAsync(User.Identity?.Name, "drs_recommendation_applied", id.ToString());
            return Ok(rec);
        }
        catch (Exception ex)
        {
            rec.Status = DrsRecommendationStatus.Failed;
            await _db.SaveChangesAsync();
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("recommendations/{id:guid}/reject")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> RejectRecommendation(Guid id)
    {
        var rec = await _db.DrsRecommendations.FindAsync(id);
        if (rec == null) return NotFound();
        if (rec.Status != DrsRecommendationStatus.Pending)
            return BadRequest(new { error = "Recommendation is not pending." });

        rec.Status = DrsRecommendationStatus.Rejected;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "drs_recommendation_rejected", id.ToString());
        return Ok();
    }

    #region Affinity Rules

    [HttpGet("rules/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetAffinityRules(Guid clusterId)
    {
        var configId = await _db.DrsConfigurations
            .Where(d => d.ClusterId == clusterId)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        if (configId == Guid.Empty) return Ok(Array.Empty<object>());

        var rules = await _db.DrsAffinityRules
            .Where(r => r.DrsConfigurationId == configId)
            .Include(r => r.VmMembers)
            .AsNoTracking()
            .ToListAsync();

        return Ok(rules.Select(r => new
        {
            r.Id,
            r.Name,
            Type = r.Type.ToString(),
            r.IsEnabled,
            r.IsMandatory,
            VmMembers = r.VmMembers.Select(vm => vm.VmInventoryId),
            r.CreatedUtc
        }));
    }

    [HttpPost("rules/{clusterId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> CreateAffinityRule(Guid clusterId, [FromBody] AffinityRuleDto dto)
    {
        var config = await _db.DrsConfigurations.FirstOrDefaultAsync(d => d.ClusterId == clusterId);
        if (config == null)
            return NotFound(new { error = "DRS not configured for this cluster" });

        var rule = new DrsAffinityRule
        {
            DrsConfigurationId = config.Id,
            Name = dto.Name,
            Type = dto.Type,
            IsEnabled = dto.IsEnabled,
            IsMandatory = dto.IsMandatory,
            VmMembers = dto.VmIds.Select(vmId => new DrsAffinityRuleVm { VmInventoryId = vmId }).ToList()
        };

        _db.DrsAffinityRules.Add(rule);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "drs_rule_created", $"{rule.Name} ({rule.Type})");
        return Created($"/api/drs/rules/{clusterId}/{rule.Id}", new { rule.Id, rule.Name });
    }

    [HttpPut("rules/{clusterId:guid}/{ruleId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> UpdateAffinityRule(Guid clusterId, Guid ruleId, [FromBody] AffinityRuleDto dto)
    {
        var rule = await _db.DrsAffinityRules
            .Include(r => r.VmMembers)
            .FirstOrDefaultAsync(r => r.Id == ruleId);

        if (rule == null) return NotFound();

        rule.Name = dto.Name;
        rule.Type = dto.Type;
        rule.IsEnabled = dto.IsEnabled;
        rule.IsMandatory = dto.IsMandatory;

        // Replace VM members
        _db.DrsAffinityRuleVms.RemoveRange(rule.VmMembers);
        rule.VmMembers = dto.VmIds.Select(vmId => new DrsAffinityRuleVm
        {
            DrsAffinityRuleId = rule.Id,
            VmInventoryId = vmId
        }).ToList();

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "drs_rule_updated", rule.Name);
        return Ok(new { rule.Id, rule.Name });
    }

    [HttpDelete("rules/{clusterId:guid}/{ruleId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> DeleteAffinityRule(Guid clusterId, Guid ruleId)
    {
        var rule = await _db.DrsAffinityRules.FindAsync(ruleId);
        if (rule == null) return NotFound();

        _db.DrsAffinityRules.Remove(rule);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "drs_rule_deleted", rule.Name);
        return Ok(new { message = "Deleted" });
    }

    #endregion

    [HttpGet("balance/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetClusterBalance(Guid clusterId)
    {
        var nodes = await _db.ClusterNodes
            .Where(cn => cn.ClusterId == clusterId)
            .Include(cn => cn.AgentHost)
            .Where(cn => cn.AgentHost != null)
            .ToListAsync();

        var balance = new List<object>();
        foreach (var node in nodes)
        {
            var vmCount = await _db.VmInventory.CountAsync(v => v.AgentHostId == node.AgentHostId);

            // Get latest metrics
            var latestCpu = await _db.MetricDataPoints
                .Where(m => m.AgentHostId == node.AgentHostId && m.MetricName == "host_cpu_usage" && m.VmInventoryId == null)
                .OrderByDescending(m => m.TimestampUtc)
                .FirstOrDefaultAsync();

            var latestMem = await _db.MetricDataPoints
                .Where(m => m.AgentHostId == node.AgentHostId && m.MetricName == "host_memory_usage" && m.VmInventoryId == null)
                .OrderByDescending(m => m.TimestampUtc)
                .FirstOrDefaultAsync();

            balance.Add(new
            {
                agentHostId = node.AgentHostId,
                hostname = node.AgentHost!.Hostname,
                status = node.AgentHost.Status.ToString(),
                vmCount,
                cpuUsagePercent = latestCpu?.Value ?? 0,
                memoryUsagePercent = latestMem?.Value ?? 0
            });
        }

        return Ok(new { clusterId, hosts = balance });
    }
}

public class AffinityRuleDto
{
    public string Name { get; set; } = string.Empty;
    public AffinityRuleType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsMandatory { get; set; }
    public List<Guid> VmIds { get; set; } = new();
}

public class DrsConfigDto
{
    public bool IsEnabled { get; set; } = true;
    public DrsAutomationLevel AutomationLevel { get; set; } = DrsAutomationLevel.Manual;
    public int CpuImbalanceThreshold { get; set; } = 25;
    public int MemoryImbalanceThreshold { get; set; } = 25;
    public int EvaluationIntervalSeconds { get; set; } = 300;
    public int MinBenefitPercent { get; set; } = 10;
}
