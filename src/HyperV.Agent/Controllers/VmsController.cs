using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;

namespace HyperV.Agent.Controllers;

/// <summary>Operacje na maszynach wirtualnych (VM).</summary>
[ApiController]
[Route("api/v1/vms")]
public class VmsController : ControllerBase
{
    private readonly HyperV.Core.Hcs.Services.VmService _hcsVm;
    private readonly VmCreationService _wmiCreation;
    private readonly HyperV.Core.Wmi.Services.VmService _wmiVm;
    private readonly ReplicationService _repl;
    
    public VmsController(
        HyperV.Core.Hcs.Services.VmService hcsVm, 
        VmCreationService wmiCreation,
        HyperV.Core.Wmi.Services.VmService wmiVm,
        ReplicationService repl) 
    {
        _hcsVm = hcsVm;
        _wmiCreation = wmiCreation;
        _wmiVm = wmiVm;
        _repl = repl;
    }

    /// <summary>Utwórz nową maszynę wirtualną.</summary>
    /// <param name="req">Parametry VM.</param>
    /// <returns>JSON odpowiedzi HCS lub WMI.</returns>
    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create new VM", Description = "Creates a VM using HCS or WMI API based on Mode parameter.")]
    public IActionResult Create([FromBody] CreateVmRequest req)
    {
        try
        {
            // Validate the request
            if (req == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields manually if needed
            if (string.IsNullOrEmpty(req.Id))
            {
                return BadRequest(new { error = "Id field is required" });
            }

            if (string.IsNullOrEmpty(req.Name))
            {
                return BadRequest(new { error = "Name field is required" });
            }

            string resultJson;
            
            // Choose service based on Mode parameter
            switch (req.Mode)
            {
                case VmCreationMode.HCS:
                    // Use HCS service for container-like VMs (not visible in Hyper-V Manager)
                    resultJson = _hcsVm.Create(req.Id, req);
                    break;
                    
                case VmCreationMode.WMI:
                default:
                    // Use WMI service to create proper Hyper-V VMs that appear in Hyper-V Manager
                    resultJson = _wmiCreation.CreateHyperVVm(req.Id, req);
                    break;
            }
            
            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>Sprawdza, czy istnieje maszyna o podanej nazwie (HCS lub WMI).</summary>
    [HttpGet("{name}/present")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Check VM presence", Description = "Uses HCS and WMI to verify if a VM with given name exists.")]
    public IActionResult VmPresent([FromRoute] string name) 
    {
        // Check both HCS and WMI VMs
        var hcsPresent = _hcsVm.IsVmPresent(name);
        var wmiPresent = _wmiVm.IsVmPresent(name);
        return Ok(new { present = hcsPresent || wmiPresent, hcs = hcsPresent, wmi = wmiPresent });
    }

    /// <summary>Uruchamia maszynę wirtualną.</summary>
    [HttpPost("{name}/start")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(Summary = "Start VM", Description = "Starts a VM with the given name.")]
    public IActionResult StartVm([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                try
                {
                    _hcsVm.StartVm(name);
                    return Ok();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = $"Failed to start HCS VM: {ex.Message}", backend = "HCS" });
                }
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                try
                {
                    _wmiVm.StartVm(name);
                    return Ok();
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = $"Failed to start WMI VM: {ex.Message}", backend = "WMI" });
                }
            }
            
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Unexpected error: {ex.Message}" });
        }
    }

    /// <summary>Zatrzymuje maszynę wirtualną.</summary>
    [HttpPost("{name}/stop")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Stop VM", Description = "Stops a VM with the given name.")]
    public IActionResult StopVm([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.StopVm(name);
                return Ok();
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.StopVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Wyłącza maszynę wirtualną (graceful shutdown).</summary>
    [HttpPost("{name}/shutdown")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Shutdown VM", Description = "Gracefully shuts down a VM with the given name.")]
    public IActionResult ShutdownVm([FromRoute] string name)
    {
        try
        {
            // For HCS VMs, use StopVm (which is graceful shutdown in HCS)
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.StopVm(name);
                return Ok();
            }
            
            // Fall back to WMI graceful shutdown (same as stop for WMI)
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.StopVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Terminuje maszynę wirtualną (force stop).</summary>
    [HttpPost("{name}/terminate")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Terminate VM", Description = "Forcefully terminates a VM with the given name.")]
    public IActionResult TerminateVm([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.TerminateVm(name);
                return Ok();
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.TerminateVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Wstrzymuje maszynę wirtualną.</summary>
    [HttpPost("{name}/pause")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Pause VM", Description = "Pauses a VM with the given name.")]
    public IActionResult PauseVm([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.PauseVm(name);
                return Ok();
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.PauseVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Wznawia maszynę wirtualną.</summary>
    [HttpPost("{name}/resume")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Resume VM", Description = "Resumes a paused VM with the given name.")]
    public IActionResult ResumeVm([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.ResumeVm(name);
                return Ok();
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.ResumeVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Zapisuje stan maszyny wirtualnej.</summary>
    [HttpPost("{name}/save")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Save VM", Description = "Saves the state of a VM with the given name.")]
    public IActionResult SaveVm([FromRoute] string name)
    {
        try
        {
            // HCS VMs don't support save state (container-like behavior)
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "Save state not supported for HCS VMs (container-like behavior)" });
            }
            
            // Fall back to WMI for traditional VM save state (use ReplicationService for this)
            if (_repl.IsVmPresent(name))
            {
                _repl.SaveVm(name);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Pobiera właściwości maszyny wirtualnej.</summary>
    [HttpGet("{name}/properties")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Properties", Description = "Gets properties of a VM with the given name.")]
    public IActionResult GetVmProperties([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                var hcsProperties = _hcsVm.GetVmProperties(name);
                return Ok(new { backend = "HCS", properties = hcsProperties });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                var wmiProperties = _wmiVm.GetVmProperties(name);
                return Ok(new { backend = "WMI", properties = wmiProperties });
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
    }

    /// <summary>Modyfikuje konfigurację maszyny wirtualnej.</summary>
    [HttpPost("{name}/modify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Modify VM", Description = "Modifies configuration of a VM with the given name.")]
    public IActionResult ModifyVm([FromRoute] string name, [FromBody] string configuration)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                _hcsVm.ModifyVm(name, configuration);
                return Ok();
            }
            
            // Fall back to WMI (use ReplicationService for this)
            if (_repl.IsVmPresent(name))
            {
                _repl.ModifyVm(name, configuration);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "VM modification not implemented for WMI backend" });
        }
    }

    /// <summary>Listuje wszystkie maszyny wirtualne.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "List VMs", Description = "Lists all VMs from both HCS and WMI backends.")]
    public IActionResult ListVms()
    {
        try
        {
            var hcsVms = _hcsVm.ListVms();
            var wmiVms = _wmiVm.ListVms();
            
            return Ok(new 
            { 
                HCS = JsonSerializer.Deserialize<object>(hcsVms),
                WMI = JsonSerializer.Deserialize<object>(wmiVms)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to list VMs: {ex.Message}" });
        }
    }

    /// <summary>Modyfikuje konfigurację VM (pamięć, CPU, notatki).</summary>
    [HttpPost("{name}/configure")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Configure VM", Description = "Modifies VM configuration (memory, CPU, notes).")]
    public IActionResult ConfigureVm([FromRoute] string name, VmConfigurationRequest request)
    {
        try
        {
            // Try HCS first (limited configuration support)
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "VM configuration modification not supported for HCS VMs", backend = "HCS" });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.ModifyVmConfiguration(name, request.StartupMemoryMB, request.CpuCount, request.Notes, request.EnableDynamicMemory, request.MinimumMemoryMB, request.MaximumMemoryMB, request.TargetMemoryBuffer);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to configure VM: {ex.Message}" });
        }
    }

    /// <summary>Listuje snapshoty maszyny wirtualnej.</summary>
    [HttpGet("{name}/snapshots")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "List VM Snapshots", Description = "Lists all snapshots for a VM.")]
    public IActionResult ListVmSnapshots([FromRoute] string name)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "Snapshots not supported for HCS VMs", backend = "HCS" });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                var snapshots = _wmiVm.ListVmSnapshots(name);
                return Ok(JsonSerializer.Deserialize<object>(snapshots));
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to list VM snapshots: {ex.Message}" });
        }
    }

    /// <summary>Tworzy snapshot maszyny wirtualnej.</summary>
    [HttpPost("{name}/snapshots")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Create VM Snapshot", Description = "Creates a snapshot of a VM.")]
    public IActionResult CreateVmSnapshot([FromRoute] string name, [FromBody] CreateSnapshotRequest request)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "Snapshots not supported for HCS VMs", backend = "HCS" });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                var result = _wmiVm.CreateVmSnapshot(name, request.SnapshotName, request.Notes);
                return Ok(JsonSerializer.Deserialize<object>(result));
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to create VM snapshot: {ex.Message}" });
        }
    }

    /// <summary>Usuwa snapshot maszyny wirtualnej.</summary>
    [HttpDelete("{name}/snapshots/{snapshotId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Delete VM Snapshot", Description = "Deletes a VM snapshot.")]
    public IActionResult DeleteVmSnapshot([FromRoute] string name, [FromRoute] string snapshotId)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "Snapshots not supported for HCS VMs", backend = "HCS" });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.DeleteVmSnapshot(name, snapshotId);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to delete VM snapshot: {ex.Message}" });
        }
    }

    /// <summary>Przywraca VM do snapshotu.</summary>
    [HttpPost("{name}/snapshots/{snapshotId}/revert")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Revert VM to Snapshot", Description = "Reverts a VM to a specific snapshot.")]
    public IActionResult RevertVmToSnapshot([FromRoute] string name, [FromRoute] string snapshotId)
    {
        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                return StatusCode(501, new { error = "Snapshots not supported for HCS VMs", backend = "HCS" });
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.RevertVmToSnapshot(name, snapshotId);
                return Ok();
            }
            
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to revert VM to snapshot: {ex.Message}" });
        }
    }
}

/// <summary>Request model for VM configuration.</summary>
public class VmConfigurationRequest
{
    public int? StartupMemoryMB { get; set; }
    public int? CpuCount { get; set; }
    public string? Notes { get; set; }
    public bool? EnableDynamicMemory { get; set; }
    public int? MinimumMemoryMB { get; set; }
    public int? MaximumMemoryMB { get; set; }
    public int? TargetMemoryBuffer { get; set; }
    public int? VirtualMachineReserve { get; set; }
    public int? VirtualMachineLimit { get; set; }
    public int? RelativeWeight { get; set; }
    public bool? LimitProcessorFeatures { get; set; }
    public int? MaxProcessorsPerNumaNode { get; set; }
    public int? MaxNumaNodesPerSocket { get; set; }
    public int? HwThreadsPerCore { get; set; }
}

/// <summary>Request model for creating snapshots.</summary>
public class CreateSnapshotRequest
{
    public string SnapshotName { get; set; } = "";
    public string? Notes { get; set; }
}
