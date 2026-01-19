using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;

namespace HyperV.CentralManagement.Services;

public class AuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(string? username, string action, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Username = username,
            Action = action,
            Details = details,
            TimestampUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
