using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

public class AlertService
{
    private readonly AppDbContext _context;

    public AlertService(AppDbContext context)
    {
        _context = context;
    }

    // Alert Definitions

    public async Task<List<AlertDefinition>> GetDefinitionsAsync()
    {
        return await _context.AlertDefinitions
            .Include(ad => ad.NotificationChannels)
            .ThenInclude(anc => anc.NotificationChannel)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<AlertDefinition?> GetDefinitionAsync(Guid id)
    {
        return await _context.AlertDefinitions
            .Include(ad => ad.NotificationChannels)
            .ThenInclude(anc => anc.NotificationChannel)
            .FirstOrDefaultAsync(ad => ad.Id == id);
    }

    public async Task<AlertDefinition> CreateDefinitionAsync(AlertDefinition definition)
    {
        _context.AlertDefinitions.Add(definition);
        await _context.SaveChangesAsync();
        return definition;
    }

    public async Task<bool> UpdateDefinitionAsync(Guid id, AlertDefinition updated)
    {
        var existing = await _context.AlertDefinitions.FindAsync(id);
        if (existing == null) return false;

        existing.Name = updated.Name;
        existing.Description = updated.Description;
        existing.MetricName = updated.MetricName;
        existing.Condition = updated.Condition;
        existing.ThresholdValue = updated.ThresholdValue;
        existing.Severity = updated.Severity;
        existing.EvaluationPeriods = updated.EvaluationPeriods;
        existing.CooldownSeconds = updated.CooldownSeconds;
        existing.IsEnabled = updated.IsEnabled;
        existing.ClusterId = updated.ClusterId;
        existing.AgentHostId = updated.AgentHostId;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteDefinitionAsync(Guid id)
    {
        var definition = await _context.AlertDefinitions.FindAsync(id);
        if (definition == null) return false;

        _context.AlertDefinitions.Remove(definition);
        await _context.SaveChangesAsync();
        return true;
    }

    // Alert Instances

    public async Task<List<AlertInstance>> GetActiveAlertsAsync()
    {
        return await _context.AlertInstances
            .Include(ai => ai.AlertDefinition)
            .Where(ai => ai.Status == AlertInstanceStatus.Active)
            .OrderByDescending(ai => ai.FiredUtc)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<AlertInstance>> GetAlertHistoryAsync(int limit = 100)
    {
        return await _context.AlertInstances
            .Include(ai => ai.AlertDefinition)
            .OrderByDescending(ai => ai.FiredUtc)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> AcknowledgeAlertAsync(Guid instanceId, string acknowledgedBy)
    {
        var instance = await _context.AlertInstances.FindAsync(instanceId);
        if (instance == null || instance.Status != AlertInstanceStatus.Active) return false;

        instance.Status = AlertInstanceStatus.Acknowledged;
        instance.AcknowledgedBy = acknowledgedBy;
        instance.AcknowledgedUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResolveAlertAsync(Guid instanceId)
    {
        var instance = await _context.AlertInstances.FindAsync(instanceId);
        if (instance == null || instance.Status == AlertInstanceStatus.Resolved) return false;

        instance.Status = AlertInstanceStatus.Resolved;
        instance.ResolvedUtc = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    // Notification Channels

    public async Task<List<NotificationChannel>> GetChannelsAsync()
    {
        return await _context.NotificationChannels.AsNoTracking().ToListAsync();
    }

    public async Task<NotificationChannel> CreateChannelAsync(NotificationChannel channel)
    {
        _context.NotificationChannels.Add(channel);
        await _context.SaveChangesAsync();
        return channel;
    }

    public async Task<bool> UpdateChannelAsync(Guid id, NotificationChannel updated)
    {
        var existing = await _context.NotificationChannels.FindAsync(id);
        if (existing == null) return false;

        existing.Name = updated.Name;
        existing.Type = updated.Type;
        existing.Configuration = updated.Configuration;
        existing.IsEnabled = updated.IsEnabled;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteChannelAsync(Guid id)
    {
        var channel = await _context.NotificationChannels.FindAsync(id);
        if (channel == null) return false;

        _context.NotificationChannels.Remove(channel);
        await _context.SaveChangesAsync();
        return true;
    }
}
