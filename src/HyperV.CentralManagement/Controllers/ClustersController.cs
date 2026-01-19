using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/clusters")]
public class ClustersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public ClustersController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetClusters()
    {
        var clusters = await _db.Clusters
            .Include(c => c.Nodes)
            .ThenInclude(n => n.AgentHost)
            .AsNoTracking()
            .ToListAsync();

        return Ok(clusters);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateCluster([FromBody] ClusterRequest request)
    {
        var cluster = new Cluster
        {
            Name = request.Name,
            Description = request.Description
        };

        _db.Clusters.Add(cluster);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "cluster_created", cluster.Name);

        return Ok(cluster);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCluster(Guid id, [FromBody] ClusterRequest request)
    {
        var cluster = await _db.Clusters.FirstOrDefaultAsync(c => c.Id == id);
        if (cluster == null)
        {
            return NotFound();
        }

        cluster.Name = request.Name;
        cluster.Description = request.Description;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "cluster_updated", cluster.Name);

        return Ok(cluster);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCluster(Guid id)
    {
        var cluster = await _db.Clusters.FirstOrDefaultAsync(c => c.Id == id);
        if (cluster == null)
        {
            return NotFound();
        }

        _db.Clusters.Remove(cluster);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "cluster_deleted", cluster.Name);
        return Ok();
    }

    [HttpPost("{id:guid}/nodes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddNode(Guid id, [FromBody] ClusterNodeRequest request)
    {
        if (!await _db.Clusters.AnyAsync(c => c.Id == id))
        {
            return NotFound();
        }

        if (!await _db.AgentHosts.AnyAsync(a => a.Id == request.AgentHostId))
        {
            return BadRequest(new { error = "Agent host not found." });
        }

        var node = new ClusterNode
        {
            ClusterId = id,
            AgentHostId = request.AgentHostId
        };

        _db.ClusterNodes.Add(node);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "cluster_node_added", request.AgentHostId.ToString());
        return Ok(node);
    }

    [HttpDelete("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveNode(Guid id, Guid nodeId)
    {
        var node = await _db.ClusterNodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.ClusterId == id);
        if (node == null)
        {
            return NotFound();
        }

        _db.ClusterNodes.Remove(node);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "cluster_node_removed", nodeId.ToString());
        return Ok();
    }
}

public record ClusterRequest(string Name, string? Description);
public record ClusterNodeRequest(Guid AgentHostId);
