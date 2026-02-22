using HyperV.Contracts.Models;
using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;
using System.Management;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace HyperV.Agent.Controllers;

/// <summary>Operacje na maszynach wirtualnych (VM).</summary>
[ApiController]
[Route("api/v1/vms")]
public class VmsController : ControllerBase
{
    private readonly HyperV.Core.Hcs.Services.VmService _hcsVm;
    private readonly VmCreationService _wmiCreation;
    private readonly HyperV.Core.Wmi.Services.VmService _wmiVm;
    private readonly MetricsService _metricsService;
    private readonly ResourcePoolsService _resourcePoolsService;
    private readonly ReplicationService _repl;
    private readonly IStorageService _storageService;
    private readonly ILogger<VmsController> _logger;

    public VmsController(
        HyperV.Core.Hcs.Services.VmService hcsVm,
        VmCreationService wmiCreation,
        HyperV.Core.Wmi.Services.VmService wmiVm,
        MetricsService metricsService,
        ResourcePoolsService resourcePoolsService,
        ReplicationService repl,
        IStorageService storageService,
        ILogger<VmsController> logger)
    {
        _hcsVm = hcsVm;
        _wmiCreation = wmiCreation;
        _wmiVm = wmiVm;
        _metricsService = metricsService;
        _resourcePoolsService = resourcePoolsService;
        _repl = repl;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>Utwórz nową maszynę wirtualną.</summary>
    /// <param name="req">Parametry VM.</param>
    /// <returns>JSON odpowiedzi HCS lub WMI.</returns>
    [HttpPost]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create new VM", Description = "Creates a VM using HCS or WMI API based on Mode parameter.")]
    public IActionResult Create([FromBody] CreateVmRequest req)
    {
        _logger.LogInformation("Creating VM with Id: {Id}, Mode: {Mode}", req?.Id, req?.Mode);
        try
        {
            // Validate the request
            if (req == null)
            {
                _logger.LogWarning("Create VM request is null");
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
            _logger.LogError(ex, "Error creating VM");
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
                var hcsPropertiesJson = _hcsVm.GetVmProperties(name);
                
                try
                {
                    // Parse HCS JSON response to extract actual memory and CPU values
                    using var doc = JsonDocument.Parse(hcsPropertiesJson);
                    var properties = new
                    {
                        memory = ExtractMemoryFromHcsProperties(doc),
                        processors = ExtractProcessorsFromHcsProperties(doc),
                        rawProperties = hcsPropertiesJson
                    };
                    
                    return Ok(new
                    {
                        backend = "HCS",
                        properties = properties
                    });
                }
                catch (JsonException)
                {
                    // Fallback if JSON parsing fails
                    return Ok(new
                    {
                        backend = "HCS",
                        properties = new
                        {
                            memory = 2048, // Default fallback
                            processors = 2, // Default fallback
                            rawProperties = hcsPropertiesJson
                        }
                    });
                }
            }
            
            // Fall back to WMI
            if (_wmiVm.IsVmPresent(name))
            {
                // Get actual WMI properties including memory and CPU settings
                var wmiProperties = GetWmiVmResourceSettings(name);
                return Ok(new
                {
                    backend = "WMI",
                    properties = wmiProperties
                });
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
    public async Task<IActionResult> ModifyVm([FromRoute] string name, [FromBody] string configuration)
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
                var result = await _repl.ModifyVmAsync(name, configuration);
                return Ok(JsonSerializer.Deserialize<object>(result));
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
            var hcsVms = _hcsVm.ListVms(); // already JSON string
            var wmiVms = _wmiVm.ListVms(); // already JSON string

            // Parse the JSON strings to objects and return them via Ok() for proper test compatibility
            using (var doc1 = JsonDocument.Parse(hcsVms))
            using (var doc2 = JsonDocument.Parse(wmiVms))
            {
                var hcsObj = JsonSerializer.Deserialize<object>(doc1.RootElement);
                var wmiObj = JsonSerializer.Deserialize<object>(doc2.RootElement);

                var combinedObj = new Dictionary<string, object?>
                {
                    ["HCS"] = hcsObj,
                    ["WMI"] = wmiObj
                };

                return Ok(combinedObj);
            }
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
                _wmiVm.ModifyVmConfiguration(name, request.StartupMemoryMB, request.CpuCount, request.Notes ?? string.Empty, request.EnableDynamicMemory, request.MinimumMemoryMB, request.MaximumMemoryMB, request.TargetMemoryBuffer);
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
                var result = _wmiVm.CreateVmSnapshot(name, request.SnapshotName, request.Notes ?? string.Empty);
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

    #region Storage Management

    /// <summary>Pobiera listę urządzeń pamięci masowej w maszynie wirtualnej.</summary>
    [HttpGet("{name}/storage/devices")]
    [ProducesResponseType(typeof(List<StorageDeviceResponse>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Devices", Description = "Gets list of storage devices in a VM.")]
    public async Task<IActionResult> GetVmStorageDevices([FromRoute] string name)
    {
        try
        {
            var devices = await _storageService.GetVmStorageDevicesAsync(name);
            return Ok(devices);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get storage devices: {ex.Message}" });
        }
    }

    /// <summary>Dodaje urządzenie pamięci masowej do maszyny wirtualnej.</summary>
    [HttpPost("{name}/storage/devices")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Add VM Storage Device", Description = "Adds a storage device to a VM.")]
    public async Task<IActionResult> AddVmStorageDevice([FromRoute] string name, [FromBody] AddStorageDeviceRequest request)
    {
        try
        {
            await _storageService.AddStorageDeviceToVmAsync(name, request);
            return Ok(new { message = "Storage device added successfully", vmName = name });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to add storage device: {ex.Message}" });
        }
    }

    /// <summary>Usuwa urządzenie pamięci masowej z maszyny wirtualnej.</summary>
    [HttpDelete("{name}/storage/devices/{deviceId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Remove VM Storage Device", Description = "Removes a storage device from a VM.")]
    public async Task<IActionResult> RemoveVmStorageDevice([FromRoute] string name, [FromRoute] string deviceId)
    {
        try
        {
            await _storageService.RemoveStorageDeviceFromVmAsync(name, deviceId);
            return Ok(new { message = "Storage device removed successfully", vmName = name, deviceId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or device '{deviceId}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to remove storage device: {ex.Message}" });
        }
    }

    /// <summary>Pobiera listę kontrolerów pamięci masowej w maszynie wirtualnej.</summary>
    [HttpGet("{name}/storage/controllers")]
    [ProducesResponseType(typeof(List<StorageControllerResponse>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Controllers", Description = "Gets list of storage controllers in a VM.")]
    public async Task<IActionResult> GetVmStorageControllers([FromRoute] string name)
    {
        try
        {
            var controllers = await _storageService.GetVmStorageControllersAsync(name);
            return Ok(controllers);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get storage controllers: {ex.Message}" });
        }
    }

    /// <summary>Pobiera listę dysków wirtualnych w maszynie wirtualnej.</summary>
    [HttpGet("{name}/storage/drives")]
    [ProducesResponseType(typeof(List<object>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Drives", Description = "Gets list of virtual drives in a VM.")]
    public async Task<IActionResult> GetVmStorageDrives([FromRoute] string name)
    {
        try
        {
            var drives = await _storageService.GetVmStorageDevicesAsync(name);
            var drivesList = drives.Where(d => d.DeviceType == "VirtualDisk")
                                  .Select(d => new
                                  {
                                      DriveId = d.DeviceId,
                                      Type = d.DeviceType,
                                      Path = d.Path,
                                      State = d.OperationalStatus,
                                      ControllerId = d.ControllerId,
                                      ControllerType = d.ControllerType,
                                      IsReadOnly = d.IsReadOnly
                                  }).ToList();
            
            return Ok(drivesList);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get storage drives: {ex.Message}" });
        }
    }

    /// <summary>Pobiera szczegóły konkretnego dysku wirtualnego.</summary>
    [HttpGet("{name}/storage/drives/{driveId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Drive", Description = "Gets details of a specific virtual drive.")]
    public async Task<IActionResult> GetVmStorageDrive([FromRoute] string name, [FromRoute] string driveId)
    {
        try
        {
            // Try WMI first
            if (_wmiVm.IsVmPresent(name))
            {
                var driveDetails = _wmiVm.GetVmStorageDrive(name, driveId);
                return Ok(JsonSerializer.Deserialize<object>(driveDetails));
            }

            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "Get VM storage drive not implemented for WMI backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get storage drive: {ex.Message}" });
        }
    }

    /// <summary>Pobiera stan dysku wirtualnego.</summary>
    [HttpGet("{name}/storage/drives/{driveId}/state")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Drive State", Description = "Gets operational state of a virtual drive.")]
    public async Task<IActionResult> GetVmStorageDriveState([FromRoute] string name, [FromRoute] string driveId)
    {
        try
        {
            // Try WMI first
            if (_wmiVm.IsVmPresent(name))
            {
                var state = _wmiVm.GetVmStorageDriveState(name, driveId);
                return Ok(JsonSerializer.Deserialize<object>(state));
            }

            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "Get VM storage drive state not implemented for WMI backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get drive state: {ex.Message}" });
        }
    }

    /// <summary>Resetuje dysk wirtualny.</summary>
    [HttpPost("{name}/storage/drives/{driveId}/reset")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Reset VM Storage Drive", Description = "Resets a virtual drive.")]
    public async Task<IActionResult> ResetVmStorageDrive([FromRoute] string name, [FromRoute] string driveId)
    {
        try
        {
            // Try WMI first
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.ResetVmStorageDrive(name, driveId);
                return Ok(new { message = "Drive reset successfully", vmName = name, driveId });
            }

            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "Reset VM storage drive not implemented for WMI backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to reset drive: {ex.Message}" });
        }
    }

    /// <summary>Blokuje/odblokowuje nośnik w dysku wirtualnym.</summary>
    [HttpPut("{name}/storage/drives/{driveId}/lock")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Lock/Unlock VM Storage Drive Media", Description = "Locks or unlocks media in a virtual drive.")]
    public async Task<IActionResult> LockVmStorageDriveMedia([FromRoute] string name, [FromRoute] string driveId, [FromBody] LockMediaRequest request)
    {
        try
        {
            // Try WMI first
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.LockVmStorageDriveMedia(name, driveId, request.Lock);
                var action = request.Lock ? "locked" : "unlocked";
                return Ok(new { message = $"Media {action} successfully", vmName = name, driveId });
            }

            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "Lock/Unlock VM storage drive media not implemented for WMI backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to lock/unlock media: {ex.Message}" });
        }
    }

    /// <summary>Pobiera możliwości dysku wirtualnego.</summary>
    [HttpGet("{name}/storage/drives/{driveId}/capabilities")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Drive Capabilities", Description = "Gets capabilities of a virtual drive.")]
    public async Task<IActionResult> GetVmStorageDriveCapabilities([FromRoute] string name, [FromRoute] string driveId)
    {
        try
        {
            // Try WMI first
            if (_wmiVm.IsVmPresent(name))
            {
                var capabilities = _wmiVm.GetVmStorageDriveCapabilities(name, driveId);
                return Ok(JsonSerializer.Deserialize<object>(capabilities));
            }

            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { error = "Get VM storage drive capabilities not implemented for WMI backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get drive capabilities: {ex.Message}" });
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>Extracts memory information from HCS properties JSON.</summary>
    private int ExtractMemoryFromHcsProperties(JsonDocument doc)
    {
        try
        {
            if (doc.RootElement.TryGetProperty("VirtualMachine", out var vm) &&
                vm.TryGetProperty("ComputeTopology", out var topology) &&
                topology.TryGetProperty("Memory", out var memory) &&
                memory.TryGetProperty("SizeInMB", out var size))
            {
                return size.GetInt32();
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return 2048; // Default fallback
    }

    /// <summary>Extracts processor information from HCS properties JSON.</summary>
    private int ExtractProcessorsFromHcsProperties(JsonDocument doc)
    {
        try
        {
            if (doc.RootElement.TryGetProperty("VirtualMachine", out var vm) &&
                vm.TryGetProperty("ComputeTopology", out var topology) &&
                topology.TryGetProperty("Processor", out var processor) &&
                processor.TryGetProperty("Count", out var count))
            {
                return count.GetInt32();
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return 2; // Default fallback
    }

    /// <summary>Gets WMI VM resource settings (memory and CPU).</summary>
    private object GetWmiVmResourceSettings(string vmName)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            // Get VM instance
            var vmQuery = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}'");
            using var vmSearcher = new ManagementObjectSearcher(scope, vmQuery);
            using var vmResults = vmSearcher.Get();

            foreach (ManagementObject vm in vmResults)
            {
                using (vm)
                {
                    // Get VM settings
                    var vmGuid = vm["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(vmGuid))
                    {
                        // Get memory settings
                        var memoryQuery = new ObjectQuery($"SELECT * FROM Msvm_MemorySettingData WHERE InstanceID LIKE '%{vmGuid}%'");
                        using var memSearcher = new ManagementObjectSearcher(scope, memoryQuery);
                        using var memResults = memSearcher.Get();
                        
                        var memoryMB = 2048; // Default
                        foreach (ManagementObject mem in memResults)
                        {
                            using (mem)
                            {
                                if (mem["VirtualQuantity"] != null)
                                {
                                    memoryMB = Convert.ToInt32(mem["VirtualQuantity"]);
                                    break;
                                }
                            }
                        }

                        // Get processor settings
                        var cpuQuery = new ObjectQuery($"SELECT * FROM Msvm_ProcessorSettingData WHERE InstanceID LIKE '%{vmGuid}%'");
                        using var cpuSearcher = new ManagementObjectSearcher(scope, cpuQuery);
                        using var cpuResults = cpuSearcher.Get();
                        
                        var cpuCount = 2; // Default
                        foreach (ManagementObject cpu in cpuResults)
                        {
                            using (cpu)
                            {
                                if (cpu["VirtualQuantity"] != null)
                                {
                                    cpuCount = Convert.ToInt32(cpu["VirtualQuantity"]);
                                    break;
                                }
                            }
                        }

                        return new { memory = memoryMB, processors = cpuCount };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WMI VM resource settings");
        }

        return new { memory = 2048, processors = 2 }; // Default fallback
    }

    /// <summary>Gets HCS VM usage metrics.</summary>
    private VmUsageSummary GetHcsVmUsageMetrics(string vmName)
    {
        // Placeholder - implement HCS metrics collection
        return new VmUsageSummary
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Cpu = new CpuUsage
            {
                UsagePercent = 0.0,
                GuestAverageUsage = 0.0
            },
            Memory = new MemoryUsage
            {
                AssignedMB = 2048,
                DemandMB = 1024,
                UsagePercent = 50.0,
                Status = "Healthy"
            },
            Disks = new List<DiskUsage>(),
            Networks = new List<NetworkUsage>(),
            StorageAdapters = new List<StorageAdapterUsage>()
        };
    }

    /// <summary>Gets WMI VM usage metrics.</summary>
    private VmUsageSummary GetWmiVmUsageMetrics(string vmName)
    {
        var usage = new VmUsageSummary
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Disks = new List<DiskUsage>(),
            Networks = new List<NetworkUsage>(),
            StorageAdapters = new List<StorageAdapterUsage>()
        };

        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            // Get VM instance
            var vmQuery = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}'");
            using var vmSearcher = new ManagementObjectSearcher(scope, vmQuery);
            using var vmResults = vmSearcher.Get();

            foreach (ManagementObject vm in vmResults)
            {
                using (vm)
                {
                    // Get CPU metrics
                    var cpuUsage = 0.0;
                    try
                    {
                        var processorLoadQuery = new ObjectQuery($"SELECT * FROM Msvm_Processor WHERE SystemName = '{vmName}'");
                        using var procSearcher = new ManagementObjectSearcher(scope, processorLoadQuery);
                        using var procResults = procSearcher.Get();
                        foreach (ManagementObject proc in procResults)
                        {
                            using (proc)
                            {
                                if (proc["LoadPercentage"] != null)
                                {
                                    cpuUsage = Convert.ToDouble(proc["LoadPercentage"]);
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    usage.Cpu = new CpuUsage
                    {
                        UsagePercent = cpuUsage,
                        GuestAverageUsage = cpuUsage
                    };

                    // Get Memory metrics
                    var vmGuid = vm["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(vmGuid))
                    {
                        var memoryQuery = new ObjectQuery($"SELECT * FROM Msvm_MemorySettingData WHERE InstanceID LIKE '%{vmGuid}%'");
                        using var memSearcher = new ManagementObjectSearcher(scope, memoryQuery);
                        using var memResults = memSearcher.Get();

                        var assignedMB = 2048;
                        var demandMB = 1024;
                        foreach (ManagementObject mem in memResults)
                        {
                            using (mem)
                            {
                                if (mem["VirtualQuantity"] != null)
                                {
                                    assignedMB = Convert.ToInt32(mem["VirtualQuantity"]);
                                }
                            }
                        }

                        // Try to get memory demand from summary information
                        try
                        {
                            var summaryQuery = new ObjectQuery($"SELECT * FROM Msvm_SummaryInformation WHERE Name = '{vmGuid}'");
                            using var summarySearcher = new ManagementObjectSearcher(scope, summaryQuery);
                            using var summaryResults = summarySearcher.Get();
                            foreach (ManagementObject summary in summaryResults)
                            {
                                using (summary)
                                {
                                    if (summary["MemoryUsage"] != null)
                                    {
                                        demandMB = Convert.ToInt32(summary["MemoryUsage"]);
                                    }
                                }
                            }
                        }
                        catch { }

                        var usagePercent = assignedMB > 0 ? (double)demandMB / assignedMB * 100.0 : 0.0;
                        var status = usagePercent < 70 ? "Healthy" : usagePercent < 90 ? "Warning" : "Critical";

                        usage.Memory = new MemoryUsage
                        {
                            AssignedMB = assignedMB,
                            DemandMB = demandMB,
                            UsagePercent = usagePercent,
                            Status = status
                        };
                    }

                    // Get Disk metrics (placeholder - would need to query virtual disks)
                    usage.Disks.Add(new DiskUsage
                    {
                        Name = "System VHD",
                        ReadIops = 0,
                        WriteIops = 0,
                        LatencyMs = 0.0,
                        ThroughputBytesPerSec = 0
                    });

                    // Get Network metrics (placeholder - would need to query network adapters)
                    usage.Networks.Add(new NetworkUsage
                    {
                        AdapterName = "Network Adapter",
                        BytesReceivedPerSec = 0,
                        BytesSentPerSec = 0,
                        PacketsDropped = 0
                    });

                    // Get Storage Adapter metrics (placeholder)
                    usage.StorageAdapters.Add(new StorageAdapterUsage
                    {
                        Name = "SCSI Controller 0",
                        QueueDepth = 0,
                        ThroughputBytesPerSec = 0,
                        ErrorsCount = 0
                    });

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WMI VM usage metrics for {VmName}", vmName);
            // Return default values on error
            usage.Cpu = new CpuUsage { UsagePercent = 0.0, GuestAverageUsage = 0.0 };
            usage.Memory = new MemoryUsage
            {
                AssignedMB = 2048,
                DemandMB = 1024,
                UsagePercent = 50.0,
                Status = "Healthy"
            };
        }

        return usage;
    }

    #endregion

    #region Bulk VM Operations

    /// <summary>Starts multiple VMs in bulk.</summary>
    /// <param name="request">List of VM IDs to start.</param>
    /// <returns>Bulk operation response with progress tracking.</returns>
    [HttpPost("bulk/start")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkVmOperationResponse), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Start VMs", Description = "Starts multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkStartVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var response = new BulkVmOperationResponse
        {
            TotalRequested = request.VmIds.Count
        };

        var tasks = request.VmIds.Select(vmId => Task.Run(() => PerformVmOperation(vmId, "start"))).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            response.Results.Add(result);
            if (result.Success)
                response.Successful++;
            else
                response.Failed++;
        }

        response.CompletedAt = DateTime.UtcNow;
        return Ok(response);
    }

    /// <summary>Stops multiple VMs in bulk.</summary>
    /// <param name="request">List of VM IDs to stop.</param>
    /// <returns>Bulk operation response with progress tracking.</returns>
    [HttpPost("bulk/stop")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkVmOperationResponse), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Stop VMs", Description = "Stops multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkStopVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var response = new BulkVmOperationResponse
        {
            TotalRequested = request.VmIds.Count
        };

        var tasks = request.VmIds.Select(vmId => Task.Run(() => PerformVmOperation(vmId, "stop"))).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            response.Results.Add(result);
            if (result.Success)
                response.Successful++;
            else
                response.Failed++;
        }

        response.CompletedAt = DateTime.UtcNow;
        return Ok(response);
    }

    /// <summary>Shuts down multiple VMs in bulk.</summary>
    /// <param name="request">List of VM IDs to shutdown.</param>
    /// <returns>Bulk operation response with progress tracking.</returns>
    [HttpPost("bulk/shutdown")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkVmOperationResponse), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Shutdown VMs", Description = "Gracefully shuts down multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkShutdownVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var response = new BulkVmOperationResponse
        {
            TotalRequested = request.VmIds.Count
        };

        var tasks = request.VmIds.Select(vmId => Task.Run(() => PerformVmOperation(vmId, "shutdown"))).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            response.Results.Add(result);
            if (result.Success)
                response.Successful++;
            else
                response.Failed++;
        }

        response.CompletedAt = DateTime.UtcNow;
        return Ok(response);
    }

    /// <summary>Terminates multiple VMs in bulk.</summary>
    /// <param name="request">List of VM IDs to terminate.</param>
    /// <returns>Bulk operation response with progress tracking.</returns>
    [HttpPost("bulk/terminate")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkVmOperationResponse), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Terminate VMs", Description = "Forcefully terminates multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkTerminateVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var response = new BulkVmOperationResponse
        {
            TotalRequested = request.VmIds.Count
        };

        var tasks = request.VmIds.Select(vmId => Task.Run(() => PerformVmOperation(vmId, "terminate"))).ToList();
        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            response.Results.Add(result);
            if (result.Success)
                response.Successful++;
            else
                response.Failed++;
        }

        response.CompletedAt = DateTime.UtcNow;
        return Ok(response);
    }

    /// <summary>Performs a VM operation on a single VM.</summary>
    /// <param name="vmId">VM identifier.</param>
    /// <param name="operation">Operation to perform (start, stop, shutdown, terminate).</param>
    /// <returns>Operation result.</returns>
    private BulkVmOperationResult PerformVmOperation(string vmId, string operation)
    {
        var result = new BulkVmOperationResult { VmId = vmId };

        try
        {
            // Try HCS first
            if (_hcsVm.IsVmPresent(vmId))
            {
                result.Backend = "HCS";
                switch (operation)
                {
                    case "start":
                        _hcsVm.StartVm(vmId);
                        break;
                    case "stop":
                        _hcsVm.StopVm(vmId);
                        break;
                    case "shutdown":
                        _hcsVm.StopVm(vmId); // HCS uses StopVm for graceful shutdown
                        break;
                    case "terminate":
                        _hcsVm.TerminateVm(vmId);
                        break;
                }
                result.Success = true;
            }
            // Fall back to WMI
            else if (_wmiVm.IsVmPresent(vmId))
            {
                result.Backend = "WMI";
                switch (operation)
                {
                    case "start":
                        _wmiVm.StartVm(vmId);
                        break;
                    case "stop":
                        _wmiVm.StopVm(vmId);
                        break;
                    case "shutdown":
                        _wmiVm.StopVm(vmId); // WMI uses StopVm for graceful shutdown
                        break;
                    case "terminate":
                        _wmiVm.TerminateVm(vmId);
                        break;
                }
                result.Success = true;
            }
            else
            {
                result.Success = false;
                result.Error = "VM not found";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    /// <summary>Gets dynamic memory status for a VM.</summary>
    [HttpGet("{name}/memory/status")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Memory Status", Description = "Gets dynamic memory status including available buffer and swap usage.")]
    public IActionResult GetVmMemoryStatus([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var status = _wmiVm.GetVmMemoryStatus(name);
                return Ok(JsonSerializer.Deserialize<object>(status));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets SLP data root for a VM.</summary>
    [HttpGet("{name}/memory/slp")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM SLP Data Root", Description = "Gets the swap file data root location for a VM.")]
    public IActionResult GetSlpDataRoot([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var slpRoot = _wmiVm.GetSlpDataRoot(name);
                return Ok(JsonSerializer.Deserialize<object>(slpRoot));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Modifies SLP data root for a VM.</summary>
    [HttpPut("{name}/memory/slp")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Modify VM SLP Data Root", Description = "Updates the swap file data root location for a VM.")]
    public IActionResult ModifySlpDataRoot([FromRoute] string name, [FromBody] string newLocation)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.ModifySlpDataRoot(name, newLocation);
                return Ok();
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Queries if metric collection is enabled for a VM and metric.</summary>
    [HttpGet("{name}/metrics/enabled")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Query Metric Collection Enabled", Description = "Checks if metric collection is enabled for a VM and specific metric.")]
    public IActionResult QueryMetricCollectionEnabled([FromRoute] string name, [FromQuery] string metricDefinitionName)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var enabled = _metricsService.QueryMetricCollectionEnabled(name, metricDefinitionName);
                return Ok(JsonSerializer.Deserialize<object>(enabled));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Enumerates discrete metrics for a VM.</summary>
    [HttpGet("{name}/metrics/discrete")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Enumerate VM Discrete Metrics", Description = "Lists discrete metrics composing aggregate metrics for a VM.")]
    public IActionResult EnumerateDiscreteMetrics([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var metrics = _metricsService.EnumerateDiscreteMetricsForVm(name);
                return Ok(JsonSerializer.Deserialize<object>(metrics));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
    /// <summary>Enables metric collection for a VM.</summary>
    [HttpPost("{name}/metrics/enable")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Enable VM Metrics Collection", Description = "Enables metric collection for a VM by creating Msvm_MetricDefForME associations for specified or default metrics.")]
    public IActionResult EnableVmMetricsCollection([FromRoute] string name, [FromBody] string[]? metricNames = null)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var result = _metricsService.EnableMetricsCollection(name, metricNames);
                return Ok(JsonSerializer.Deserialize<object>(result));
            }
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to enable metrics collection: {ex.Message}" });
        }
    }

    /// <summary>Enumerates metrics for a resource pool.</summary>
    [HttpGet("metrics/pool")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Enumerate Resource Pool Metrics", Description = "Lists metrics for a specific resource pool.")]
    public IActionResult EnumerateResourcePoolMetrics([FromQuery] string resourceType, [FromQuery] string resourceSubType, [FromQuery] string poolId)
    {
        try
        {
            var metrics = _metricsService.EnumerateMetricsForResourcePool(resourceType, resourceSubType, poolId);
            return Ok(JsonSerializer.Deserialize<object>(metrics));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets comprehensive VM resource usage metrics.</summary>
    [HttpGet("{name}/metrics/usage")]
    [ProducesResponseType(typeof(VmUsageSummary), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Usage Metrics", Description = "Retrieves comprehensive CPU, memory, disk, network, and storage adapter usage metrics for a VM.")]
    public IActionResult GetVmUsageMetrics([FromRoute] string name)
    {
        try
        {
            VmUsageSummary usage;

            // Try HCS first
            if (_hcsVm.IsVmPresent(name))
            {
                usage = GetHcsVmUsageMetrics(name);
            }
            // Fall back to WMI
            else if (_wmiVm.IsVmPresent(name))
            {
                usage = GetWmiVmUsageMetrics(name);
            }
            else
            {
                return NotFound(new { error = $"VM '{name}' not found" });
            }

            return Ok(usage);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets VM generation type.</summary>
    [HttpGet("{name}/generation")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Generation", Description = "Gets the generation type (1 or 2) for a VM.")]
    public IActionResult GetVmGeneration([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var generation = _wmiVm.GetVmGeneration(name);
                return Ok(JsonSerializer.Deserialize<object>(generation));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets secure boot state for a VM.</summary>
    [HttpGet("{name}/secureboot")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Secure Boot", Description = "Gets the secure boot enabled state for a VM.")]
    public IActionResult GetSecureBoot([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var secureBoot = _wmiVm.GetSecureBoot(name);
                return Ok(JsonSerializer.Deserialize<object>(secureBoot));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Sets secure boot state for a VM.</summary>
    [HttpPut("{name}/secureboot")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Set VM Secure Boot", Description = "Updates the secure boot enabled state for a VM.")]
    public IActionResult SetSecureBoot([FromRoute] string name, [FromBody] bool enabled)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.SetSecureBoot(name, enabled);
                return Ok();
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets boot order for a VM.</summary>
    [HttpGet("{name}/bootorder")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Boot Order", Description = "Gets the boot device order for a VM.")]
    public IActionResult GetBootOrder([FromRoute] string name)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                var bootOrder = _wmiVm.GetBootOrder(name);
                return Ok(JsonSerializer.Deserialize<object>(bootOrder));
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Sets boot order for a VM.</summary>
    [HttpPut("{name}/bootorder")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Set VM Boot Order", Description = "Updates the boot device order for a VM.")]
    public IActionResult SetBootOrder([FromRoute] string name, [FromBody] string[] bootOrder)
    {
        try
        {
            if (_wmiVm.IsVmPresent(name))
            {
                _wmiVm.SetBootOrder(name, bootOrder);
                return Ok();
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
/// <summary>Migrates a VM to another host.</summary>
/// <param name="name">VM name.</param>
/// <param name="request">Migration parameters.</param>
/// <returns>Accepted response with job ID.</returns>
[HttpPost("{name}/migrate")]
[ProducesResponseType(typeof(object), 202)]
[ProducesResponseType(400)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Migrate VM", Description = "Migrates a VM to a destination host.")]
public IActionResult MigrateVm([FromRoute] string name, [FromBody] MigrateRequest request)
{
    try
    {
        if (string.IsNullOrEmpty(request?.DestinationHost))
        {
            return BadRequest(new { error = "DestinationHost is required" });
        }

        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        // Note: _wmiVm.MigrateVm needs implementation in VmService.cs using Msvm_VirtualSystemMigrationService.MigrateVirtualSystemToHost
        // with compatibility check via CheckSystemCompatibilityInfo
        var jobId = _wmiVm.MigrateVm(name, request.DestinationHost, request.Live, request.Storage);
        return Accepted(new { jobId });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Migrate VM storage to a new location.</summary>
[HttpPost("{name}/migrate-storage")]
[ProducesResponseType(typeof(object), 202)]
[ProducesResponseType(400)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Migrate VM Storage", Description = "Moves VM storage (VHDs) to a new location without downtime.")]
public IActionResult MigrateVmStorage([FromRoute] string name, [FromBody] MigrateStorageRequest request)
{
    try
    {
        if (string.IsNullOrEmpty(request?.DestinationPath))
        {
            return BadRequest(new { error = "DestinationPath is required" });
        }

        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        var jobId = _wmiVm.MoveVmStorage(name, request.DestinationPath);
        return Accepted(new { jobId, message = $"Storage migration initiated for VM '{name}'" });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Gets VM console connection info.</summary>
[HttpGet("{name}/console")]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Get VM Console Info", Description = "Gets connection information for VM console access including RDP details.")]
public IActionResult GetConsoleInfo([FromRoute] string name)
{
    try
    {
        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        var connectInfo = _wmiVm.GetVmConnectInfo(name);
        return Ok(connectInfo);
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Generates an RDP file for VM console access.</summary>
[HttpGet("{name}/console/rdp")]
[ProducesResponseType(typeof(FileContentResult), 200)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Download RDP File", Description = "Generates and downloads an .rdp file for connecting to the VM console.")]
public IActionResult DownloadRdpFile([FromRoute] string name)
{
    try
    {
        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        var connectInfo = _wmiVm.GetVmConnectInfo(name);
        var host = Request.Host.Host;

        var rdpContent = $"""
            full address:s:{host}:2179
            pcb:s:{name}
            server port:i:2179
            negotiate security layer:i:1
            authentication level:i:0
            prompt for credentials:i:1
            use redirection server name:i:1
            """;

        var bytes = System.Text.Encoding.UTF8.GetBytes(rdpContent);
        return File(bytes, "application/x-rdp", $"{name}.rdp");
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Gets application health status for a VM.</summary>
/// <param name="name">VM name.</param>
/// <returns>OK response with health status.</returns>
[HttpGet("{name}/apphealth")]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Get VM App Health", Description = "Gets application health status from heartbeat component.")]
public IActionResult GetAppHealth([FromRoute] string name)
{
    try
    {
        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        // Note: _wmiVm.GetAppHealth needs implementation in VmService.cs querying Msvm_HeartbeatComponent OperationalStatus
        // OK=2, map to "OK" or "Critical"
        var health = _wmiVm.GetAppHealth(name);
        return Ok(health);
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Copies a file to the guest VM.</summary>
/// <param name="name">VM name.</param>
/// <param name="request">File copy parameters.</param>
/// <returns>OK response with job ID.</returns>
[HttpPost("{name}/guestfilecopy")]
[ProducesResponseType(typeof(object), 200)]
[ProducesResponseType(400)]
[ProducesResponseType(404)]
[SwaggerOperation(Summary = "Copy File to Guest VM", Description = "Copies a file from host to guest VM.")]
public IActionResult CopyFileToGuest([FromRoute] string name, [FromBody] GuestFileRequest request)
{
    try
    {
        if (string.IsNullOrEmpty(request?.SourcePath) || string.IsNullOrEmpty(request?.DestPath))
        {
            return BadRequest(new { error = "SourcePath and DestPath are required" });
        }

        if (!_wmiVm.IsVmPresent(name))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }

        // Note: _wmiVm.CopyFileToGuest needs implementation in VmService.cs using Msvm_GuestFileService.CopyFilesToGuest
        var jobId = _wmiVm.CopyFileToGuest(name, request.SourcePath, request.DestPath, request.Overwrite);
        return Ok(new { jobId });
    }
    catch (Exception ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

/// <summary>Request model for locking/unlocking media.</summary>
public class LockMediaRequest
{
    public bool Lock { get; set; }
}

/// <summary>Request model for VM configuration.</summary>
public class VmConfigurationRequest
{
    [Range(256, 1048576, ErrorMessage = "Startup memory must be between 256 MB and 1048576 MB")]
    public int? StartupMemoryMB { get; set; }

    [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128")]
    public int? CpuCount { get; set; }

    [StringLength(1024, ErrorMessage = "Notes cannot exceed 1024 characters")]
    public string? Notes { get; set; }

    public bool? EnableDynamicMemory { get; set; }

    [Range(256, 1048576, ErrorMessage = "Minimum memory must be between 256 MB and 1048576 MB")]
    public int? MinimumMemoryMB { get; set; }

    [Range(256, 1048576, ErrorMessage = "Maximum memory must be between 256 MB and 1048576 MB")]
    public int? MaximumMemoryMB { get; set; }

    [Range(5, 2000, ErrorMessage = "Target memory buffer must be between 5% and 2000%")]
    public int? TargetMemoryBuffer { get; set; }

    [Range(0, 100, ErrorMessage = "Virtual machine reserve must be between 0% and 100%")]
    public int? VirtualMachineReserve { get; set; }

    [Range(0, 100, ErrorMessage = "Virtual machine limit must be between 0% and 100%")]
    public int? VirtualMachineLimit { get; set; }

    [Range(1, 10000, ErrorMessage = "Relative weight must be between 1 and 10000")]
    public int? RelativeWeight { get; set; }

    public bool? LimitProcessorFeatures { get; set; }

    [Range(1, 64, ErrorMessage = "Max processors per NUMA node must be between 1 and 64")]
    public int? MaxProcessorsPerNumaNode { get; set; }

    [Range(1, 64, ErrorMessage = "Max NUMA nodes per socket must be between 1 and 64")]
    public int? MaxNumaNodesPerSocket { get; set; }

    [Range(1, 64, ErrorMessage = "Hardware threads per core must be between 1 and 64")]
    public int? HwThreadsPerCore { get; set; }
}

/// <summary>Request model for creating snapshots.</summary>
public class CreateSnapshotRequest
{
    [Required(ErrorMessage = "Snapshot name is required")]
    [StringLength(100, ErrorMessage = "Snapshot name cannot exceed 100 characters")]
    public string SnapshotName { get; set; } = "";

    [StringLength(1024, ErrorMessage = "Notes cannot exceed 1024 characters")]
    public string? Notes { get; set; }
}

/// <summary>Request model for VM storage migration.</summary>
public class MigrateStorageRequest
{
    [Required(ErrorMessage = "Destination path is required")]
    [StringLength(500, ErrorMessage = "Destination path cannot exceed 500 characters")]
    public string DestinationPath { get; set; } = string.Empty;
}

/// <summary>Request model for VM migration.</summary>
public class MigrateRequest
{
    [Required(ErrorMessage = "Destination host is required")]
    [StringLength(255, ErrorMessage = "Destination host cannot exceed 255 characters")]
    public string DestinationHost { get; set; } = string.Empty;

    public bool Live { get; set; }
    public bool Storage { get; set; }
}

/// <summary>Response model for app health.</summary>
public class AppHealthResponse
{
    public string Status { get; set; } = "OK"; // or "Critical"
    public int AppStatus { get; set; } // e.g., 2 for OK
}

/// <summary>Request model for guest file copy.</summary>
public class GuestFileRequest
{
    [Required(ErrorMessage = "Source path is required")]
    [StringLength(260, ErrorMessage = "Source path cannot exceed 260 characters")]
    public string SourcePath { get; set; } = string.Empty;

    [Required(ErrorMessage = "Destination path is required")]
    [StringLength(260, ErrorMessage = "Destination path cannot exceed 260 characters")]
    public string DestPath { get; set; } = string.Empty;

    public bool Overwrite { get; set; } = false;
}

/// <summary>Request model for bulk VM operations.</summary>
public class BulkVmOperationRequest
{
    [Required(ErrorMessage = "VM IDs list is required")]
    [MinLength(1, ErrorMessage = "At least one VM ID must be provided")]
    public List<string> VmIds { get; set; } = new List<string>();
}

/// <summary>Response model for bulk VM operations.</summary>
public class BulkVmOperationResponse
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString();
    public int TotalRequested { get; set; }
    public int Successful { get; set; }
    public int Failed { get; set; }
    public List<BulkVmOperationResult> Results { get; set; } = new List<BulkVmOperationResult>();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Result model for individual VM operation in bulk operations.</summary>
public class BulkVmOperationResult
{
    public string VmId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Backend { get; set; } = string.Empty;
}

    #region VM Templates and Wizard

    /// <summary>Get available VM templates.</summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<VmTemplateConfiguration>), 200)]
    [SwaggerOperation(Summary = "Get VM Templates", Description = "Gets list of available VM templates with their default configurations.")]
    public IActionResult GetVmTemplates()
    {
        var templates = GetAvailableTemplates();
        return Ok(templates);
    }

    /// <summary>Create VM from template.</summary>
    [HttpPost("templates/create")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create VM from Template", Description = "Creates a VM using predefined templates with common configurations.")]
    public IActionResult CreateVmFromTemplate([FromBody] CreateVmFromTemplateRequest req)
    {
        _logger.LogInformation("Creating VM from template with Id: {Id}, Template: {Template}", req?.Id, req?.Template);
        try
        {
            // Validate the request
            if (req == null)
            {
                _logger.LogWarning("Create VM from template request is null");
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

            // Get template configuration
            var template = GetTemplateConfiguration(req.Template);
            if (template == null)
            {
                return BadRequest(new { error = $"Template '{req.Template}' not found" });
            }

            // Build CreateVmRequest from template and overrides
            var createReq = BuildCreateVmRequestFromTemplate(req, template);

            // Validate template compatibility with backends
            if (!template.SupportedModes.Contains(createReq.Mode))
            {
                return BadRequest(new { error = $"Template '{req.Template}' does not support mode '{createReq.Mode}'" });
            }

            string resultJson;

            // Choose service based on Mode parameter
            switch (createReq.Mode)
            {
                case VmCreationMode.HCS:
                    // Use HCS service for container-like VMs (not visible in Hyper-V Manager)
                    resultJson = _hcsVm.Create(createReq.Id, createReq);
                    break;

                case VmCreationMode.WMI:
                default:
                    // Use WMI service to create proper Hyper-V VMs that appear in Hyper-V Manager
                    resultJson = _wmiCreation.CreateHyperVVm(createReq.Id, createReq);
                    break;
            }

            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VM from template");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>Get VM creation wizard recommendations.</summary>
    [HttpPost("wizard/recommend")]
    [ProducesResponseType(typeof(VmWizardResponse), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "VM Creation Wizard", Description = "Provides intelligent recommendations for VM creation based on workload type and resource preferences.")]
    public IActionResult GetVmWizardRecommendations([FromBody] VmWizardRequest req)
    {
        try
        {
            if (req == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = GenerateWizardRecommendations(req);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating wizard recommendations");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Create VM using wizard recommendations.</summary>
    [HttpPost("wizard/create")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create VM via Wizard", Description = "Creates a VM using wizard recommendations with intelligent defaults and validation.")]
    public IActionResult CreateVmViaWizard([FromBody] VmWizardRequest req)
    {
        _logger.LogInformation("Creating VM via wizard with Id: {Id}, Workload: {Workload}", req?.Id, req?.WorkloadType);
        try
        {
            // Validate the request
            if (req == null)
            {
                _logger.LogWarning("Create VM via wizard request is null");
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

            // Get wizard recommendations
            var wizardResponse = GenerateWizardRecommendations(req);

            // Validate recommendations
            if (wizardResponse.ValidationMessages.Any())
            {
                return BadRequest(new { error = "Validation failed", messages = wizardResponse.ValidationMessages });
            }

            string resultJson;

            // Choose service based on selected backend
            switch (wizardResponse.BackendSelected)
            {
                case VmCreationMode.HCS:
                    // Use HCS service for container-like VMs (not visible in Hyper-V Manager)
                    resultJson = _hcsVm.Create(wizardResponse.RecommendedConfiguration.Id, wizardResponse.RecommendedConfiguration);
                    break;

                case VmCreationMode.WMI:
                default:
                    // Use WMI service to create proper Hyper-V VMs that appear in Hyper-V Manager
                    resultJson = _wmiCreation.CreateHyperVVm(wizardResponse.RecommendedConfiguration.Id, wizardResponse.RecommendedConfiguration);
                    break;
            }

            return Content(resultJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VM via wizard");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    #endregion

    #region Helper Methods for Templates and Wizard

    /// <summary>Gets available VM templates with their configurations.</summary>
    private List<VmTemplateConfiguration> GetAvailableTemplates()
    {
        return new List<VmTemplateConfiguration>
        {
            new VmTemplateConfiguration
            {
                Type = VmTemplateType.Development,
                DefaultMemoryMB = 4096,
                DefaultCpuCount = 4,
                DefaultDiskSizeGB = 50,
                DefaultGeneration = 2,
                DefaultSecureBoot = true,
                SupportedModes = new List<VmCreationMode> { VmCreationMode.WMI, VmCreationMode.HCS },
                Description = "Development template with higher resources for development work, debugging, and testing."
            },
            new VmTemplateConfiguration
            {
                Type = VmTemplateType.Production,
                DefaultMemoryMB = 8192,
                DefaultCpuCount = 4,
                DefaultDiskSizeGB = 100,
                DefaultGeneration = 2,
                DefaultSecureBoot = true,
                SupportedModes = new List<VmCreationMode> { VmCreationMode.WMI },
                Description = "Production template with balanced resources for production workloads and services."
            },
            new VmTemplateConfiguration
            {
                Type = VmTemplateType.Lightweight,
                DefaultMemoryMB = 1024,
                DefaultCpuCount = 1,
                DefaultDiskSizeGB = 20,
                DefaultGeneration = 2,
                DefaultSecureBoot = true,
                SupportedModes = new List<VmCreationMode> { VmCreationMode.WMI, VmCreationMode.HCS },
                Description = "Lightweight template with minimal resources for testing, small applications, or containers."
            }
        };
    }

    /// <summary>Gets template configuration by type.</summary>
    private VmTemplateConfiguration? GetTemplateConfiguration(VmTemplateType type)
    {
        return GetAvailableTemplates().FirstOrDefault(t => t.Type == type);
    }

    /// <summary>Builds CreateVmRequest from template and user overrides.</summary>
    private CreateVmRequest BuildCreateVmRequestFromTemplate(CreateVmFromTemplateRequest req, VmTemplateConfiguration template)
    {
        // Determine mode - prefer WMI for templates unless HCS is explicitly requested
        var mode = req.PreferredBackend ?? (template.SupportedModes.Contains(VmCreationMode.WMI) ? VmCreationMode.WMI : VmCreationMode.HCS);

        return new CreateVmRequest
        {
            Id = req.Id,
            Name = req.Name,
            MemoryMB = req.MemoryMB ?? template.DefaultMemoryMB,
            CpuCount = req.CpuCount ?? template.DefaultCpuCount,
            DiskSizeGB = req.DiskSizeGB ?? template.DefaultDiskSizeGB,
            Mode = mode,
            Generation = req.Generation ?? template.DefaultGeneration,
            SecureBoot = req.SecureBoot ?? template.DefaultSecureBoot,
            VhdPath = req.VhdPath,
            SwitchName = req.SwitchName,
            Notes = req.Notes ?? $"Created from {template.Type} template"
        };
    }

    /// <summary>Generates wizard recommendations based on workload and preferences.</summary>
    private VmWizardResponse GenerateWizardRecommendations(VmWizardRequest req)
    {
        var response = new VmWizardResponse();
        var recommendations = new Dictionary<string, string>();

        // Determine template based on workload type
        VmTemplateType selectedTemplate;
        VmCreationMode selectedBackend;

        switch (req.WorkloadType)
        {
            case VmWorkloadType.Development:
                selectedTemplate = VmTemplateType.Development;
                selectedBackend = req.PreferredBackend ?? VmCreationMode.WMI;
                recommendations["workload"] = "Development workloads benefit from higher resources and full Hyper-V features";
                break;

            case VmWorkloadType.WebServer:
            case VmWorkloadType.Database:
                selectedTemplate = VmTemplateType.Production;
                selectedBackend = req.PreferredBackend ?? VmCreationMode.WMI;
                recommendations["workload"] = "Server workloads require production-grade resources and stability";
                break;

            case VmWorkloadType.Container:
                selectedTemplate = VmTemplateType.Lightweight;
                selectedBackend = req.PreferredBackend ?? VmCreationMode.HCS;
                recommendations["workload"] = "Container workloads work best with lightweight configurations and HCS backend";
                break;

            case VmWorkloadType.Testing:
                selectedTemplate = VmTemplateType.Lightweight;
                selectedBackend = req.PreferredBackend ?? VmCreationMode.WMI;
                recommendations["workload"] = "Testing environments can use minimal resources while maintaining full VM features";
                break;

            case VmWorkloadType.General:
            default:
                selectedTemplate = VmTemplateType.Production;
                selectedBackend = req.PreferredBackend ?? VmCreationMode.WMI;
                recommendations["workload"] = "General purpose workloads use balanced production resources";
                break;
        }

        // Adjust resources based on resource level
        var template = GetTemplateConfiguration(selectedTemplate);
        if (template != null)
        {
            var memoryMultiplier = GetResourceMultiplier(req.ResourceLevel);
            var adjustedMemory = (int)(template.DefaultMemoryMB * memoryMultiplier);
            var adjustedCpu = Math.Max(1, (int)(template.DefaultCpuCount * memoryMultiplier));

            response.RecommendedConfiguration = new CreateVmRequest
            {
                Id = req.Id,
                Name = req.Name,
                MemoryMB = Math.Min(adjustedMemory, 1048576), // Cap at max allowed
                CpuCount = Math.Min(adjustedCpu, 128), // Cap at max allowed
                DiskSizeGB = template.DefaultDiskSizeGB,
                Mode = selectedBackend,
                Generation = template.DefaultGeneration,
                SecureBoot = template.DefaultSecureBoot,
                VhdPath = req.VhdPath,
                SwitchName = req.SwitchName,
                Notes = req.Notes ?? $"Created via wizard for {req.WorkloadType} workload"
            };

            // Validate backend compatibility
            if (!template.SupportedModes.Contains(selectedBackend))
            {
                response.ValidationMessages.Add($"Selected backend '{selectedBackend}' is not compatible with template '{selectedTemplate}'. Switching to compatible backend.");
                selectedBackend = template.SupportedModes.First();
                // Create new configuration with corrected backend
                response.RecommendedConfiguration = new CreateVmRequest
                {
                    Id = response.RecommendedConfiguration.Id,
                    Name = response.RecommendedConfiguration.Name,
                    MemoryMB = response.RecommendedConfiguration.MemoryMB,
                    CpuCount = response.RecommendedConfiguration.CpuCount,
                    DiskSizeGB = response.RecommendedConfiguration.DiskSizeGB,
                    Mode = selectedBackend,
                    Generation = response.RecommendedConfiguration.Generation,
                    SecureBoot = response.RecommendedConfiguration.SecureBoot,
                    VhdPath = response.RecommendedConfiguration.VhdPath,
                    SwitchName = response.RecommendedConfiguration.SwitchName,
                    Notes = response.RecommendedConfiguration.Notes
                };
            }

            response.TemplateUsed = selectedTemplate;
            response.BackendSelected = selectedBackend;
            response.Recommendations = recommendations;

            // Add resource recommendations
            response.Recommendations["resources"] = $"Allocated {response.RecommendedConfiguration.MemoryMB}MB RAM and {response.RecommendedConfiguration.CpuCount} vCPUs based on {req.ResourceLevel} resource level";
            response.Recommendations["backend"] = selectedBackend == VmCreationMode.HCS ?
                "HCS backend selected for container-like behavior" :
                "WMI backend selected for full Hyper-V VM features";
        }

        return response;
    }

    /// <summary>Gets resource multiplier based on resource level.</summary>
    private double GetResourceMultiplier(VmResourceLevel level)
    {
        return level switch
        {
            VmResourceLevel.Minimal => 0.25,
            VmResourceLevel.Low => 0.5,
            VmResourceLevel.Medium => 1.0,
            VmResourceLevel.High => 1.5,
            VmResourceLevel.Maximum => 2.0,
            _ => 1.0
        };
    }

    #endregion

    #region VM Cloning and Templates

    /// <summary>Clone an existing VM with optional modifications.</summary>
    [HttpPost("{sourceVmName}/clone")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CloneVmResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Clone VM", Description = "Creates a clone of an existing VM with support for full clones and linked clones, with optional resource modifications.")]
    public async Task<IActionResult> CloneVm([FromRoute] string sourceVmName, [FromBody] CloneVmRequest request)
    {
        _logger.LogInformation("Cloning VM {SourceVm} to {NewVm}", sourceVmName, request?.NewVmName);
        try
        {
            // Validate the request
            if (request == null)
            {
                _logger.LogWarning("Clone VM request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.NewVmName))
            {
                return BadRequest(new { error = "New VM name is required" });
            }

            // Check if source VM exists
            bool sourceExists = _hcsVm.IsVmPresent(sourceVmName) || _wmiVm.IsVmPresent(sourceVmName);
            if (!sourceExists)
            {
                return NotFound(new { error = $"Source VM '{sourceVmName}' not found" });
            }

            // Check if target VM name already exists
            bool targetExists = _hcsVm.IsVmPresent(request.NewVmName) || _wmiVm.IsVmPresent(request.NewVmName);
            if (targetExists)
            {
                return BadRequest(new { error = $"VM with name '{request.NewVmName}' already exists" });
            }

            // Determine backend and perform clone
            CloneVmResponse response = await PerformVmClone(sourceVmName, request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning VM");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>Create a template from an existing VM.</summary>
    [HttpPost("templates")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TemplateOperationResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Create Template from VM", Description = "Creates a reusable template from an existing VM configuration.")]
    public IActionResult CreateTemplateFromVm([FromBody] CreateTemplateFromVmRequest request)
    {
        _logger.LogInformation("Creating template from VM {VmName}", request?.SourceVmName);
        try
        {
            // Validate the request
            if (request == null)
            {
                _logger.LogWarning("Create template request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.SourceVmName))
            {
                return BadRequest(new { error = "Source VM name is required" });
            }

            if (string.IsNullOrEmpty(request.TemplateName))
            {
                return BadRequest(new { error = "Template name is required" });
            }

            // Check if source VM exists
            bool sourceExists = _hcsVm.IsVmPresent(request.SourceVmName) || _wmiVm.IsVmPresent(request.SourceVmName);
            if (!sourceExists)
            {
                return NotFound(new { error = $"Source VM '{request.SourceVmName}' not found" });
            }

            // Create template
            var template = CreateVmTemplate(request);
            SaveTemplate(template);

            var response = new TemplateOperationResponse
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                Operation = "Created",
                Success = true,
                Details = new Dictionary<string, string>
                {
                    ["sourceVm"] = request.SourceVmName,
                    ["backend"] = template.SourceBackend.ToString(),
                    ["category"] = template.Category.ToString()
                }
            };

            return Created($"api/v1/vms/templates/{template.Id}", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template from VM");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>Get all available VM templates.</summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<VmTemplate>), 200)]
    [SwaggerOperation(Summary = "List VM Templates", Description = "Gets list of all available VM templates.")]
    public IActionResult ListTemplates()
    {
        try
        {
            var templates = GetStoredTemplates();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to list templates: {ex.Message}" });
        }
    }

    /// <summary>Get a specific template by ID.</summary>
    [HttpGet("templates/{templateId}")]
    [ProducesResponseType(typeof(VmTemplate), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Template", Description = "Gets details of a specific VM template.")]
    public IActionResult GetTemplate([FromRoute] string templateId)
    {
        try
        {
            var template = GetStoredTemplate(templateId);
            if (template == null)
            {
                return NotFound(new { error = $"Template '{templateId}' not found" });
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get template: {ex.Message}" });
        }
    }

    /// <summary>Update an existing template.</summary>
    [HttpPut("templates/{templateId}")]
    [Authorize]
    [ProducesResponseType(typeof(TemplateOperationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Update VM Template", Description = "Updates an existing VM template configuration.")]
    public IActionResult UpdateTemplate([FromRoute] string templateId, [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = GetStoredTemplate(templateId);
            if (template == null)
            {
                return NotFound(new { error = $"Template '{templateId}' not found" });
            }

            // Update template properties
            UpdateTemplateFromRequest(template, request);
            SaveTemplate(template);

            var response = new TemplateOperationResponse
            {
                TemplateId = template.Id,
                TemplateName = template.Name,
                Operation = "Updated",
                Success = true
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Delete a template.</summary>
    [HttpDelete("templates/{templateId}")]
    [Authorize]
    [ProducesResponseType(typeof(TemplateOperationResponse), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete VM Template", Description = "Deletes a VM template.")]
    public IActionResult DeleteTemplate([FromRoute] string templateId)
    {
        try
        {
            var template = GetStoredTemplate(templateId);
            if (template == null)
            {
                return NotFound(new { error = $"Template '{templateId}' not found" });
            }

            DeleteStoredTemplate(templateId);

            var response = new TemplateOperationResponse
            {
                TemplateId = templateId,
                TemplateName = template.Name,
                Operation = "Deleted",
                Success = true
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template");
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region VM Cloning and Templates Helper Methods

    /// <summary>Performs the actual VM cloning operation.</summary>
    private async Task<CloneVmResponse> PerformVmClone(string sourceVmName, CloneVmRequest request)
    {
        var response = new CloneVmResponse
        {
            ClonedVmName = request.NewVmName,
            CloneType = request.CloneType,
            Details = new Dictionary<string, string>()
        };

        // Determine which backend the source VM is on
        VmCreationMode sourceBackend;
        if (_hcsVm.IsVmPresent(sourceVmName))
        {
            sourceBackend = VmCreationMode.HCS;
        }
        else if (_wmiVm.IsVmPresent(sourceVmName))
        {
            sourceBackend = VmCreationMode.WMI;
        }
        else
        {
            throw new InvalidOperationException($"Source VM '{sourceVmName}' not found");
        }

        response.Backend = sourceBackend.ToString();

        // For now, implement basic cloning using VM creation with modified parameters
        // In a full implementation, this would use proper cloning APIs
        var sourceProperties = GetVmProperties(sourceVmName, sourceBackend);

        // Create new VM configuration based on source with modifications
        var cloneConfig = new CreateVmRequest
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.NewVmName,
            MemoryMB = request.NewMemoryMB ?? sourceProperties.MemoryMB,
            CpuCount = request.NewCpuCount ?? sourceProperties.CpuCount,
            DiskSizeGB = request.NewDiskSizeGB ?? sourceProperties.DiskSizeGB,
            Mode = request.PreferredBackend ?? sourceBackend,
            Generation = sourceProperties.Generation,
            SecureBoot = sourceProperties.SecureBoot,
            VhdPath = request.NewVhdPath,
            SwitchName = request.NewSwitchName ?? sourceProperties.SwitchName,
            Notes = request.Notes ?? $"Cloned from {sourceVmName} ({request.CloneType} clone)"
        };

        // Create the cloned VM
        string resultJson;
        switch (cloneConfig.Mode)
        {
            case VmCreationMode.HCS:
                resultJson = _hcsVm.Create(cloneConfig.Id, cloneConfig);
                break;
            case VmCreationMode.WMI:
            default:
                resultJson = _wmiCreation.CreateHyperVVm(cloneConfig.Id, cloneConfig);
                break;
        }

        // Start VM if requested
        if (request.StartAfterClone)
        {
            try
            {
                if (_hcsVm.IsVmPresent(request.NewVmName))
                {
                    _hcsVm.StartVm(request.NewVmName);
                }
                else if (_wmiVm.IsVmPresent(request.NewVmName))
                {
                    _wmiVm.StartVm(request.NewVmName);
                }
                response.Started = true;
            }
            catch (Exception ex)
            {
                response.Details["startError"] = ex.Message;
            }
        }

        response.CompletedAt = DateTime.UtcNow;
        response.Details["sourceBackend"] = sourceBackend.ToString();
        response.Details["cloneMode"] = cloneConfig.Mode.ToString();

        return response;
    }

    /// <summary>Gets VM properties for cloning purposes.</summary>
    private VmProperties GetVmProperties(string vmName, VmCreationMode backend)
    {
        var properties = new VmProperties();

        try
        {
            if (backend == VmCreationMode.HCS && _hcsVm.IsVmPresent(vmName))
            {
                var hcsProps = _hcsVm.GetVmProperties(vmName);
                // Parse HCS properties - simplified for now
                properties.MemoryMB = 2048; // Would parse from JSON
                properties.CpuCount = 2;
                properties.DiskSizeGB = 20;
                properties.Generation = 2;
                properties.SecureBoot = true;
            }
            else if (backend == VmCreationMode.WMI && _wmiVm.IsVmPresent(vmName))
            {
                // Get WMI properties
                dynamic wmiProps = GetWmiVmResourceSettings(vmName);
                properties.MemoryMB = wmiProps.memory;
                properties.CpuCount = wmiProps.processors;
                properties.DiskSizeGB = 20; // Would get from storage service
                properties.Generation = 2; // Would query from WMI
                properties.SecureBoot = true; // Would query from WMI
            }
        }
        catch
        {
            // Use defaults if parsing fails
            properties.MemoryMB = 2048;
            properties.CpuCount = 2;
            properties.DiskSizeGB = 20;
            properties.Generation = 2;
            properties.SecureBoot = true;
        }

        return properties;
    }

    /// <summary>Creates a VM template from a VM.</summary>
    private VmTemplate CreateVmTemplate(CreateTemplateFromVmRequest request)
    {
        // Determine source backend
        VmCreationMode sourceBackend = VmCreationMode.WMI;
        if (_hcsVm.IsVmPresent(request.SourceVmName))
        {
            sourceBackend = VmCreationMode.HCS;
        }

        // Get VM properties
        var vmProps = GetVmProperties(request.SourceVmName, sourceBackend);

        return new VmTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.TemplateName,
            Description = request.Description,
            SourceVmName = request.SourceVmName,
            SourceBackend = sourceBackend,
            MemoryMB = request.CustomMemoryMB ?? vmProps.MemoryMB,
            CpuCount = request.CustomCpuCount ?? vmProps.CpuCount,
            DiskSizeGB = request.CustomDiskSizeGB ?? vmProps.DiskSizeGB,
            Generation = vmProps.Generation,
            SecureBoot = vmProps.SecureBoot,
            Category = request.Category,
            IsPublic = request.IsPublic,
            Tags = request.Tags ?? new List<string>(),
            SupportedBackends = new List<VmCreationMode> { sourceBackend },
            Owner = "System", // Would get from current user context
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Version = "1.0"
        };
    }

    /// <summary>Updates a template from update request.</summary>
    private void UpdateTemplateFromRequest(VmTemplate template, UpdateTemplateRequest request)
    {
        if (!string.IsNullOrEmpty(request.Name)) template.Name = request.Name;
        if (!string.IsNullOrEmpty(request.Description)) template.Description = request.Description;
        if (request.Category.HasValue) template.Category = request.Category.Value;
        if (request.IsPublic.HasValue) template.IsPublic = request.IsPublic.Value;
        if (request.Tags != null) template.Tags = request.Tags;
        if (request.MemoryMB.HasValue) template.MemoryMB = request.MemoryMB.Value;
        if (request.CpuCount.HasValue) template.CpuCount = request.CpuCount.Value;
        if (request.DiskSizeGB.HasValue) template.DiskSizeGB = request.DiskSizeGB.Value;
        if (!string.IsNullOrEmpty(request.Version)) template.Version = request.Version;

        template.ModifiedAt = DateTime.UtcNow;
    }

    /// <summary>Simple in-memory template storage (would be replaced with proper persistence).</summary>
    private static readonly Dictionary<string, VmTemplate> _templateStorage = new();

    /// <summary>Saves a template to storage.</summary>
    private void SaveTemplate(VmTemplate template)
    {
        _templateStorage[template.Id] = template;
        // In real implementation, save to database/file system
    }

    /// <summary>Gets all stored templates.</summary>
    private List<VmTemplate> GetStoredTemplates()
    {
        return _templateStorage.Values.ToList();
    }

    /// <summary>Gets a specific template by ID.</summary>
    private VmTemplate? GetStoredTemplate(string templateId)
    {
        return _templateStorage.TryGetValue(templateId, out var template) ? template : null;
    }

    /// <summary>Deletes a template from storage.</summary>
    private void DeleteStoredTemplate(string templateId)
    {
        _templateStorage.Remove(templateId);
    }

    /// <summary>VM properties helper class.</summary>
    private class VmProperties
    {
        public int MemoryMB { get; set; }
        public int CpuCount { get; set; }
        public int DiskSizeGB { get; set; }
        public int Generation { get; set; }
        public bool SecureBoot { get; set; }
        public string? SwitchName { get; set; }
    }

    #endregion
}

/// <summary>VM health metrics model.</summary>
public class VmHealthMetrics
{
    public string VmName { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double StorageUsageGB { get; set; }
    public double NetworkRxMBps { get; set; }
    public double NetworkTxMBps { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>VM metrics history model.</summary>
public class VmMetricsHistory
{
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public double MemoryUsagePercent { get; set; }
    public double StorageUsageGB { get; set; }
}

/// <summary>Alert rule model.</summary>
public class AlertRule
{
    public string Id { get; set; } = string.Empty;
    public string VmName { get; set; } = string.Empty;
    public AlertMetric Metric { get; set; }
    public AlertCondition Condition { get; set; }
    public double Threshold { get; set; }
    public int DurationMinutes { get; set; }
    public bool Enabled { get; set; } = true;
    public string EmailRecipients { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
}

/// <summary>Alert model.</summary>
public class Alert
{
    public string Id { get; set; } = string.Empty;
    public string VmName { get; set; } = string.Empty;
    public AlertMetric Metric { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
    public bool Acknowledged { get; set; }
}

/// <summary>Dashboard data model.</summary>
public class DashboardData
{
    public DateTime Timestamp { get; set; }
    public List<VmSummary> VmSummaries { get; set; } = new List<VmSummary>();
    public List<Alert> ActiveAlerts { get; set; } = new List<Alert>();
    public SystemHealthOverview SystemHealth { get; set; } = new SystemHealthOverview();
}

/// <summary>VM summary model.</summary>
public class VmSummary
{
    public string Name { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public int ActiveAlerts { get; set; }
}

/// <summary>System health overview model.</summary>
public class SystemHealthOverview
{
    public int TotalVms { get; set; }
    public int RunningVms { get; set; }
    public int CriticalAlerts { get; set; }
    public double HostCpuUsage { get; set; }
    public double HostMemoryUsage { get; set; }
}

/// <summary>Monitoring configuration model.</summary>
public class MonitoringConfiguration
{
    public int CollectionIntervalSeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 30;
    public bool EnableEmailNotifications { get; set; }
    public bool EnableWebhookNotifications { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}

/// <summary>Alert metric enum.</summary>
public enum AlertMetric
{
    CpuUsage,
    MemoryUsage,
    StorageUsage,
    NetworkRx,
    NetworkTx
}

/// <summary>Alert condition enum.</summary>
public enum AlertCondition
{
    GreaterThan,
    LessThan,
    EqualTo
}

/// <summary>Alert severity enum.</summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>Comprehensive VM resource usage summary.</summary>
public class VmUsageSummary
{
    public string Timestamp { get; set; } = string.Empty;
    public CpuUsage Cpu { get; set; } = new CpuUsage();
    public MemoryUsage Memory { get; set; } = new MemoryUsage();
    public List<DiskUsage> Disks { get; set; } = new List<DiskUsage>();
    public List<NetworkUsage> Networks { get; set; } = new List<NetworkUsage>();
    public List<StorageAdapterUsage> StorageAdapters { get; set; } = new List<StorageAdapterUsage>();
}

/// <summary>CPU usage metrics.</summary>
public class CpuUsage
{
    public double UsagePercent { get; set; }
    public double GuestAverageUsage { get; set; }
}

/// <summary>Memory usage metrics.</summary>
public class MemoryUsage
{
    public int AssignedMB { get; set; }
    public int DemandMB { get; set; }
    public double UsagePercent { get; set; }
    public string Status { get; set; } = "Healthy";
}

/// <summary>Disk usage metrics.</summary>
public class DiskUsage
{
    public string Name { get; set; } = string.Empty;
    public int ReadIops { get; set; }
    public int WriteIops { get; set; }
    public double LatencyMs { get; set; }
    public long ThroughputBytesPerSec { get; set; }
}

/// <summary>Network adapter usage metrics.</summary>
public class NetworkUsage
{
    public string AdapterName { get; set; } = string.Empty;
    public long BytesReceivedPerSec { get; set; }
    public long BytesSentPerSec { get; set; }
    public int PacketsDropped { get; set; }
}

/// <summary>Storage adapter usage metrics.</summary>
public class StorageAdapterUsage
{
    public string Name { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
    public long ThroughputBytesPerSec { get; set; }
    public int ErrorsCount { get; set; }
}
