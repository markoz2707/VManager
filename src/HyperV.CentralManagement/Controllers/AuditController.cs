using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("audit", "read")]
    public async Task<IActionResult> GetAuditLogs()
    {
        var logs = await _db.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .Take(200)
            .AsNoTracking()
            .ToListAsync();

        return Ok(logs);
    }
}
