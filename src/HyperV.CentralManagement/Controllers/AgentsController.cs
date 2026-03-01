using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public AgentsController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _db.AgentHosts.AsNoTracking().ToListAsync();
        return Ok(agents);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetAgent(Guid id)
    {
        var agent = await _db.AgentHosts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        return agent == null ? NotFound() : Ok(agent);
    }

    [HttpPost]
    [RequirePermission("host", "create")]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        var agent = new AgentHost
        {
            Hostname = request.Hostname,
            ApiBaseUrl = request.ApiBaseUrl,
            IpAddress = request.IpAddress,
            HostType = string.IsNullOrWhiteSpace(request.HostType) ? "Hyper-V" : request.HostType,
            Status = AgentStatus.Unknown,
            LastSeenUtc = DateTimeOffset.UtcNow,
            DatacenterId = request.DatacenterId
        };

        _db.AgentHosts.Add(agent);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "agent_created", agent.Hostname);

        return Ok(agent);
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAgent([FromBody] RegisterAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { error = "Registration token is required." });
        }

        var token = await _db.RegistrationTokens.FirstOrDefaultAsync(t => t.Token == request.Token);
        if (token == null || token.ExpiresUtc < DateTimeOffset.UtcNow || token.UsedByAgentId.HasValue)
        {
            return Unauthorized(new { error = "Invalid or expired registration token." });
        }

        var agent = new AgentHost
        {
            Hostname = request.Hostname,
            ApiBaseUrl = request.ApiBaseUrl,
            IpAddress = request.IpAddress,
            HyperVVersion = request.HyperVVersion,
            HypervisorVersion = request.HypervisorVersion,
            OperatingSystem = request.OperatingSystem,
            AgentVersion = request.AgentVersion,
            HostType = string.IsNullOrWhiteSpace(request.HostType) ? "Hyper-V" : request.HostType,
            Tags = request.Tags,
            Status = AgentStatus.Online,
            LastSeenUtc = DateTimeOffset.UtcNow,
            ClusterName = request.ClusterName
        };

        _db.AgentHosts.Add(agent);
        token.UsedByAgentId = agent.Id;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(null, "agent_registered", agent.Hostname);
        return Ok(agent);
    }

    [HttpPut("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id)
    {
        var agent = await _db.AgentHosts.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null)
        {
            return NotFound();
        }

        agent.LastSeenUtc = DateTimeOffset.UtcNow;
        agent.Status = AgentStatus.Online;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(null, "agent_heartbeat", agent.Hostname);

        return Ok();
    }

    [HttpPatch("{id:guid}")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest request)
    {
        var agent = await _db.AgentHosts.FirstOrDefaultAsync(a => a.Id == id);
        if (agent == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Tags))
        {
            agent.Tags = request.Tags;
        }

        if (!string.IsNullOrWhiteSpace(request.ClusterName))
        {
            agent.ClusterName = request.ClusterName;
        }

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "agent_updated", agent.Hostname);
        return Ok(agent);
    }

    [HttpPost("tokens")]
    [RequirePermission("host", "create")]
    public async Task<IActionResult> CreateRegistrationToken([FromBody] CreateTokenRequest request)
    {
        var token = new RegistrationToken
        {
            Token = request.Token ?? Guid.NewGuid().ToString("N"),
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(request.TtlMinutes <= 0 ? 60 : request.TtlMinutes)
        };

        _db.RegistrationTokens.Add(token);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "registration_token_created", token.Token);

        return Ok(token);
    }
}

public record RegisterAgentRequest(
    string Hostname,
    string ApiBaseUrl,
    string Token,
    string? IpAddress,
    string? HyperVVersion,
    string? HypervisorVersion,
    string? OperatingSystem,
    string? AgentVersion,
    string? HostType,
    string? Tags,
    string? ClusterName);

public record CreateAgentRequest(string Hostname, string ApiBaseUrl, string? IpAddress, string? HostType, Guid? DatacenterId);

public record UpdateAgentRequest(string? Tags, string? ClusterName);

public record CreateTokenRequest(string? Token, int TtlMinutes);
