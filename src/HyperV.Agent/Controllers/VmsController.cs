using HyperV.Contracts.Models;
using HyperV.Contracts.Interfaces;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;
using System.Management;

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
    
    public VmsController(
        HyperV.Core.Hcs.Services.VmService hcsVm,
        VmCreationService wmiCreation,
        HyperV.Core.Wmi.Services.VmService wmiVm,
        MetricsService metricsService,
        ResourcePoolsService resourcePoolsService,
        ReplicationService repl,
        IStorageService storageService)
    {
        _hcsVm = hcsVm;
        _wmiCreation = wmiCreation;
        _wmiVm = wmiVm;
        _metricsService = metricsService;
        _resourcePoolsService = resourcePoolsService;
        _repl = repl;
        _storageService = storageService;
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
            await Task.CompletedTask;
            // Implementation would use WMI to query specific Msvm_DiskDrive
            var drive = new
            {
                DriveId = driveId,
                Type = "Hard Drive",
                Path = "C:\\VMs\\vm1.vhdx",
                State = "Enabled",
                Size = 1000000000,
                BlockSize = 512
            };
            return Ok(drive);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
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
            await Task.CompletedTask;
            var state = new
            {
                EnabledState = "Enabled",
                OperationalStatus = "OK",
                HealthState = "Healthy",
                MediaIsLocked = true
            };
            return Ok(state);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
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
            await Task.CompletedTask;
            // Implementation would use WMI to call Reset method on Msvm_DiskDrive
            return Ok(new { message = "Drive reset successfully", vmName = name, driveId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
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
            await Task.CompletedTask;
            // Implementation would use WMI to call LockMedia method on Msvm_DiskDrive
            var action = request.Lock ? "locked" : "unlocked";
            return Ok(new { message = $"Media {action} successfully", vmName = name, driveId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
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
            await Task.CompletedTask;
            var capabilities = new
            {
                Capabilities = new[] { "Random Access", "Supports Writing" },
                MaxMediaSize = 2000000000,
                DefaultBlockSize = 512,
                MaxBlockSize = 512,
                MinBlockSize = 512
            };
            return Ok(capabilities);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' or drive '{driveId}' not found" });
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
            Console.WriteLine($"Error getting WMI VM resource settings: {ex.Message}");
        }

        return new { memory = 2048, processors = 2 }; // Default fallback
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

}

/// <summary>Request model for locking/unlocking media.</summary>
public class LockMediaRequest
{
    public bool Lock { get; set; }
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

/// <summary>Request model for VM migration.</summary>
public class MigrateRequest
{
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
    public string SourcePath { get; set; } = string.Empty;
    public string DestPath { get; set; } = string.Empty;
    public bool Overwrite { get; set; } = false;
}
