using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers;

/// <summary>Operacje na maszynach wirtualnych (VM).</summary>
[ApiController]
[Route("api/v1/vms")]
public class VmsController : ControllerBase
{
    private readonly IVmProvider _vmProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IMetricsProvider _metricsProvider;
    private readonly IMigrationProvider _migrationProvider;
    private readonly IJobService _jobService;
    private readonly ILogger<VmsController> _logger;

    public VmsController(
        IVmProvider vmProvider,
        IStorageProvider storageProvider,
        IMetricsProvider metricsProvider,
        IMigrationProvider migrationProvider,
        IJobService jobService,
        ILogger<VmsController> logger)
    {
        _vmProvider = vmProvider;
        _storageProvider = storageProvider;
        _metricsProvider = metricsProvider;
        _migrationProvider = migrationProvider;
        _jobService = jobService;
        _logger = logger;
    }

    #region VM Lifecycle

    /// <summary>Utwórz nową maszynę wirtualną.</summary>
    /// <param name="spec">Parametry VM.</param>
    /// <returns>JSON odpowiedzi.</returns>
    [HttpPost]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(string), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create new VM", Description = "Creates a VM using the configured provider.")]
    public async Task<IActionResult> Create([FromBody] CreateVmSpec spec)
    {
        _logger.LogInformation("Creating VM with Name: {Name}", spec?.Name);
        try
        {
            if (spec == null)
            {
                _logger.LogWarning("Create VM request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(spec.Name))
            {
                return BadRequest(new { error = "Name field is required" });
            }

            var result = await _vmProvider.CreateVmAsync(spec);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VM");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>Listuje wszystkie maszyny wirtualne.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<VmSummaryDto>), 200)]
    [SwaggerOperation(Summary = "List VMs", Description = "Lists all VMs from the configured provider with optional pagination.")]
    public async Task<IActionResult> ListVms([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var vms = await _vmProvider.ListVmsAsync();
            var totalCount = vms.Count;
            var items = vms.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new PaginatedResult<VmSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to list VMs: {ex.Message}" });
        }
    }

    /// <summary>Sprawdza, czy istnieje maszyna o podanej nazwie.</summary>
    [HttpGet("{name}/present")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Check VM presence", Description = "Checks if a VM with the given name exists.")]
    public async Task<IActionResult> VmPresent([FromRoute] string name)
    {
        try
        {
            var vm = await _vmProvider.GetVmAsync(name);
            return Ok(new { present = vm != null });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to check VM presence: {ex.Message}" });
        }
    }

    /// <summary>Uruchamia maszynę wirtualną.</summary>
    [HttpPost("{name}/start")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(Summary = "Start VM", Description = "Starts a VM with the given name.")]
    public async Task<IActionResult> StartVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.StartVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to start VM: {ex.Message}" });
        }
    }

    /// <summary>Zatrzymuje maszynę wirtualną.</summary>
    [HttpPost("{name}/stop")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Stop VM", Description = "Stops a VM with the given name.")]
    public async Task<IActionResult> StopVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.StopVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to stop VM: {ex.Message}" });
        }
    }

    /// <summary>Wyłącza maszynę wirtualną (graceful shutdown).</summary>
    [HttpPost("{name}/shutdown")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Shutdown VM", Description = "Gracefully shuts down a VM with the given name.")]
    public async Task<IActionResult> ShutdownVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.ShutdownVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to shutdown VM: {ex.Message}" });
        }
    }

    /// <summary>Terminuje maszynę wirtualną (force stop).</summary>
    [HttpPost("{name}/terminate")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Terminate VM", Description = "Forcefully terminates a VM with the given name.")]
    public async Task<IActionResult> TerminateVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.StopVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to terminate VM: {ex.Message}" });
        }
    }

    /// <summary>Wstrzymuje maszynę wirtualną.</summary>
    [HttpPost("{name}/pause")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Pause VM", Description = "Pauses a VM with the given name.")]
    public async Task<IActionResult> PauseVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.PauseVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to pause VM: {ex.Message}" });
        }
    }

    /// <summary>Wznawia maszynę wirtualną.</summary>
    [HttpPost("{name}/resume")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Resume VM", Description = "Resumes a paused VM with the given name.")]
    public async Task<IActionResult> ResumeVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.ResumeVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to resume VM: {ex.Message}" });
        }
    }

    /// <summary>Zapisuje stan maszyny wirtualnej.</summary>
    [HttpPost("{name}/save")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Save VM", Description = "Saves the state of a VM with the given name.")]
    public async Task<IActionResult> SaveVm([FromRoute] string name)
    {
        try
        {
            await _vmProvider.SaveVmAsync(name);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException ex)
        {
            return StatusCode(501, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to save VM: {ex.Message}" });
        }
    }

    #endregion

    #region VM Properties and Configuration

    /// <summary>Pobiera właściwości maszyny wirtualnej.</summary>
    [HttpGet("{name}/properties")]
    [ProducesResponseType(typeof(VmPropertiesDto), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Properties", Description = "Gets properties of a VM with the given name.")]
    public async Task<IActionResult> GetVmProperties([FromRoute] string name)
    {
        try
        {
            var properties = await _vmProvider.GetVmPropertiesAsync(name);
            if (properties == null)
            {
                return NotFound();
            }
            return Ok(properties);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get VM properties: {ex.Message}" });
        }
    }

    /// <summary>Modyfikuje konfigurację VM (pamięć, CPU, notatki).</summary>
    [HttpPost("{name}/configure")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Configure VM", Description = "Modifies VM configuration (memory, CPU, notes).")]
    public async Task<IActionResult> ConfigureVm([FromRoute] string name, [FromBody] VmConfigurationSpec spec)
    {
        try
        {
            await _vmProvider.ConfigureVmAsync(name, spec);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "VM configuration not supported by this provider" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to configure VM: {ex.Message}" });
        }
    }

    /// <summary>Modyfikuje konfigurację maszyny wirtualnej (backwards compatibility).</summary>
    [HttpPost("{name}/modify")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(501)]
    [SwaggerOperation(Summary = "Modify VM", Description = "Modifies configuration of a VM with the given name (backwards compatibility).")]
    public async Task<IActionResult> ModifyVm([FromRoute] string name, [FromBody] VmConfigurationSpec spec)
    {
        try
        {
            await _vmProvider.ConfigureVmAsync(name, spec);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "VM modification not supported by this provider" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to modify VM: {ex.Message}" });
        }
    }

    /// <summary>Gets boot order for a VM.</summary>
    [HttpGet("{name}/bootorder")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Boot Order", Description = "Gets the boot device order for a VM.")]
    public async Task<IActionResult> GetBootOrder([FromRoute] string name)
    {
        try
        {
            var vm = await _vmProvider.GetVmAsync(name);
            if (vm == null)
            {
                return NotFound();
            }

            // Boot order is available via ExtendedProperties
            var bootOrder = vm.ExtendedProperties?.ContainsKey("BootOrder") == true
                ? vm.ExtendedProperties["BootOrder"]
                : new string[] { "Network", "HardDrive", "DVD", "Floppy" };

            return Ok(new { bootOrder });
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
    public async Task<IActionResult> SetBootOrder([FromRoute] string name, [FromBody] string[] bootOrder)
    {
        try
        {
            var vm = await _vmProvider.GetVmAsync(name);
            if (vm == null)
            {
                return NotFound();
            }

            // Set boot order via ConfigureVmAsync with ExtendedProperties
            var spec = new VmConfigurationSpec
            {
                ExtendedProperties = new Dictionary<string, object>
                {
                    ["BootOrder"] = bootOrder
                }
            };
            await _vmProvider.ConfigureVmAsync(name, spec);
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Snapshots

    /// <summary>Listuje snapshoty maszyny wirtualnej.</summary>
    [HttpGet("{name}/snapshots")]
    [ProducesResponseType(typeof(List<VmSnapshotDto>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "List VM Snapshots", Description = "Lists all snapshots for a VM.")]
    public async Task<IActionResult> ListVmSnapshots([FromRoute] string name)
    {
        try
        {
            var snapshots = await _vmProvider.ListSnapshotsAsync(name);
            return Ok(snapshots);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "Snapshots not supported by this provider" });
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
    public async Task<IActionResult> CreateVmSnapshot([FromRoute] string name, [FromBody] CreateSnapshotRequest request)
    {
        try
        {
            var snapshotId = await _vmProvider.CreateSnapshotAsync(name, request.SnapshotName);
            return Ok(new { snapshotId, vmName = name, snapshotName = request.SnapshotName });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "Snapshots not supported by this provider" });
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
    public async Task<IActionResult> DeleteVmSnapshot([FromRoute] string name, [FromRoute] string snapshotId)
    {
        try
        {
            await _vmProvider.DeleteSnapshotAsync(name, snapshotId);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "Snapshots not supported by this provider" });
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
    public async Task<IActionResult> RevertVmToSnapshot([FromRoute] string name, [FromRoute] string snapshotId)
    {
        try
        {
            await _vmProvider.ApplySnapshotAsync(name, snapshotId);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound();
        }
        catch (NotSupportedException)
        {
            return StatusCode(501, new { error = "Snapshots not supported by this provider" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to revert VM to snapshot: {ex.Message}" });
        }
    }

    #endregion

    #region Console

    /// <summary>Gets VM console connection info.</summary>
    [HttpGet("{name}/console")]
    [ProducesResponseType(typeof(ConsoleInfoDto), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Console Info", Description = "Gets connection information for VM console access.")]
    public async Task<IActionResult> GetConsoleInfo([FromRoute] string name)
    {
        try
        {
            var info = await _vmProvider.GetConsoleInfoAsync(name);
            if (info == null)
            {
                return NotFound(new { error = $"VM '{name}' not found or console not available" });
            }
            return Ok(info);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Storage Management

    /// <summary>Pobiera listę urządzeń pamięci masowej w maszynie wirtualnej.</summary>
    [HttpGet("{name}/storage/devices")]
    [ProducesResponseType(typeof(List<StorageDeviceDto>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Devices", Description = "Gets list of storage devices in a VM.")]
    public async Task<IActionResult> GetVmStorageDevices([FromRoute] string name)
    {
        try
        {
            var devices = await _storageProvider.GetVmStorageDevicesAsync(name);
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
    public async Task<IActionResult> AddVmStorageDevice([FromRoute] string name, [FromBody] AddStorageDeviceSpec spec)
    {
        try
        {
            await _storageProvider.AddStorageDeviceToVmAsync(name, spec);
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
            await _storageProvider.RemoveStorageDeviceFromVmAsync(name, deviceId);
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
    [ProducesResponseType(typeof(List<StorageControllerDto>), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Storage Controllers", Description = "Gets list of storage controllers in a VM.")]
    public async Task<IActionResult> GetVmStorageControllers([FromRoute] string name)
    {
        try
        {
            var controllers = await _storageProvider.GetVmStorageControllersAsync(name);
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

    #endregion

    #region Bulk VM Operations

    /// <summary>Starts multiple VMs in bulk.</summary>
    [HttpPost("bulk/start")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultDto), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Start VMs", Description = "Starts multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkStartVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var result = await _vmProvider.BulkStartAsync(request.VmIds.ToArray());
        return Ok(result);
    }

    /// <summary>Stops multiple VMs in bulk.</summary>
    [HttpPost("bulk/stop")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultDto), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Stop VMs", Description = "Stops multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkStopVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var result = await _vmProvider.BulkStopAsync(request.VmIds.ToArray());
        return Ok(result);
    }

    /// <summary>Shuts down multiple VMs in bulk.</summary>
    [HttpPost("bulk/shutdown")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultDto), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Shutdown VMs", Description = "Gracefully shuts down multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkShutdownVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var result = await _vmProvider.BulkShutdownAsync(request.VmIds.ToArray());
        return Ok(result);
    }

    /// <summary>Terminates multiple VMs in bulk.</summary>
    [HttpPost("bulk/terminate")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BulkOperationResultDto), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Bulk Terminate VMs", Description = "Forcefully terminates multiple VMs asynchronously with error handling for individual VMs.")]
    public async Task<IActionResult> BulkTerminateVms([FromBody] BulkVmOperationRequest request)
    {
        if (request == null || !request.VmIds.Any())
        {
            return BadRequest(new { error = "VM IDs list is required and cannot be empty" });
        }

        var result = await _vmProvider.BulkTerminateAsync(request.VmIds.ToArray());
        return Ok(result);
    }

    #endregion

    #region Metrics

    /// <summary>Gets comprehensive VM resource usage metrics.</summary>
    [HttpGet("{name}/metrics/usage")]
    [ProducesResponseType(typeof(VmUsageSummary), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get VM Usage Metrics", Description = "Retrieves comprehensive CPU, memory, disk, network, and storage adapter usage metrics for a VM.")]
    public async Task<IActionResult> GetVmUsageMetrics([FromRoute] string name)
    {
        try
        {
            var vmUsage = await _metricsProvider.GetVmUsageAsync(name);
            var diskMetrics = await _metricsProvider.GetDiskMetricsAsync(name);
            var networkMetrics = await _metricsProvider.GetNetworkMetricsAsync(name);

            var usage = new VmUsageSummary
            {
                Timestamp = DateTime.UtcNow.ToString("o"),
                Cpu = new CpuUsage
                {
                    UsagePercent = vmUsage.CpuUsagePercent,
                    GuestAverageUsage = vmUsage.CpuUsagePercent
                },
                Memory = new MemoryUsage
                {
                    AssignedMB = (int)vmUsage.MemoryAssignedMB,
                    DemandMB = (int)vmUsage.MemoryDemandMB,
                    UsagePercent = vmUsage.MemoryUsagePercent,
                    Status = vmUsage.MemoryUsagePercent < 70 ? "Healthy" :
                             vmUsage.MemoryUsagePercent < 90 ? "Warning" : "Critical"
                },
                Disks = diskMetrics != null
                    ? new List<DiskUsage>
                    {
                        new DiskUsage
                        {
                            Name = "System VHD",
                            ReadIops = (int)diskMetrics.ReadOperationsPerSec,
                            WriteIops = (int)diskMetrics.WriteOperationsPerSec,
                            LatencyMs = 0.0,
                            ThroughputBytesPerSec = diskMetrics.ReadBytesPerSec + diskMetrics.WriteBytesPerSec
                        }
                    }
                    : new List<DiskUsage>(),
                Networks = networkMetrics != null
                    ? new List<NetworkUsage>
                    {
                        new NetworkUsage
                        {
                            AdapterName = "Network Adapter",
                            BytesReceivedPerSec = networkMetrics.BytesReceivedPerSec,
                            BytesSentPerSec = networkMetrics.BytesSentPerSec,
                            PacketsDropped = 0
                        }
                    }
                    : new List<NetworkUsage>(),
                StorageAdapters = new List<StorageAdapterUsage>()
            };

            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{name}' not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Migration

    /// <summary>Migrates a VM to another host.</summary>
    [HttpPost("{name}/migrate")]
    [ProducesResponseType(typeof(object), 202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Migrate VM", Description = "Migrates a VM to a destination host.")]
    public async Task<IActionResult> MigrateVm([FromRoute] string name, [FromBody] MigrateRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.DestinationHost))
            {
                return BadRequest(new { error = "DestinationHost is required" });
            }

            var vm = await _vmProvider.GetVmAsync(name);
            if (vm == null)
            {
                return NotFound(new { error = $"VM '{name}' not found" });
            }

            var result = await _migrationProvider.MigrateVmAsync(name, request.DestinationHost, request.Live, request.Storage);
            return Accepted(new { jobId = result.JobId, success = result.Success, message = result.Message });
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
    public async Task<IActionResult> MigrateVmStorage([FromRoute] string name, [FromBody] MigrateStorageRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request?.DestinationPath))
            {
                return BadRequest(new { error = "DestinationPath is required" });
            }

            var vm = await _vmProvider.GetVmAsync(name);
            if (vm == null)
            {
                return NotFound(new { error = $"VM '{name}' not found" });
            }

            var result = await _migrationProvider.MigrateVmAsync(name, request.DestinationPath, false, true);
            return Accepted(new { jobId = result.JobId, message = $"Storage migration initiated for VM '{name}'" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    #endregion

    #region Clone

    /// <summary>Clone an existing VM with optional modifications.</summary>
    [HttpPost("{sourceVmName}/clone")]
    [Authorize]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Clone VM", Description = "Creates a clone of an existing VM.")]
    public async Task<IActionResult> CloneVm([FromRoute] string sourceVmName, [FromBody] CloneVmRequest request)
    {
        _logger.LogInformation("Cloning VM {SourceVm} to {NewVm}", sourceVmName, request?.NewVmName);
        try
        {
            if (request == null)
            {
                _logger.LogWarning("Clone VM request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(request.NewVmName))
            {
                return BadRequest(new { error = "New VM name is required" });
            }

            // Check if source VM exists
            var sourceVm = await _vmProvider.GetVmAsync(sourceVmName);
            if (sourceVm == null)
            {
                return NotFound(new { error = $"Source VM '{sourceVmName}' not found" });
            }

            // Check if target VM name already exists
            var targetVm = await _vmProvider.GetVmAsync(request.NewVmName);
            if (targetVm != null)
            {
                return BadRequest(new { error = $"VM with name '{request.NewVmName}' already exists" });
            }

            var result = await _vmProvider.CloneVmAsync(sourceVmName, request.NewVmName);
            return Ok(new
            {
                clonedVmName = request.NewVmName,
                cloneType = request.CloneType.ToString(),
                result,
                completedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning VM");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    #endregion

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
    public async Task<IActionResult> CreateVmFromTemplate([FromBody] CreateVmFromTemplateRequest req)
    {
        _logger.LogInformation("Creating VM from template with Id: {Id}, Template: {Template}", req?.Id, req?.Template);
        try
        {
            if (req == null)
            {
                _logger.LogWarning("Create VM from template request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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

            // Build CreateVmSpec from template and overrides
            var spec = new CreateVmSpec
            {
                Name = req.Name,
                CpuCount = req.CpuCount ?? template.DefaultCpuCount,
                MemoryMB = req.MemoryMB ?? template.DefaultMemoryMB,
                Generation = req.Generation ?? template.DefaultGeneration,
                DiskSizeGB = req.DiskSizeGB ?? template.DefaultDiskSizeGB,
                IsoPath = null,
                DiskPath = req.VhdPath,
                NetworkName = req.SwitchName,
                ExtendedProperties = new Dictionary<string, object>
                {
                    ["TemplateType"] = req.Template.ToString(),
                    ["SecureBoot"] = req.SecureBoot ?? template.DefaultSecureBoot,
                    ["Notes"] = req.Notes ?? $"Created from {template.Type} template"
                }
            };

            var result = await _vmProvider.CreateVmAsync(spec);
            return Content(result, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating VM from template");
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
    public async Task<IActionResult> CreateTemplateFromVm([FromBody] CreateTemplateFromVmRequest request)
    {
        _logger.LogInformation("Creating template from VM {VmName}", request?.SourceVmName);
        try
        {
            if (request == null)
            {
                _logger.LogWarning("Create template request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(request.SourceVmName))
            {
                return BadRequest(new { error = "Source VM name is required" });
            }

            if (string.IsNullOrEmpty(request.TemplateName))
            {
                return BadRequest(new { error = "Template name is required" });
            }

            // Check if source VM exists
            var sourceVm = await _vmProvider.GetVmAsync(request.SourceVmName);
            if (sourceVm == null)
            {
                return NotFound(new { error = $"Source VM '{request.SourceVmName}' not found" });
            }

            // Create template from VM properties
            var template = new VmTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.TemplateName,
                Description = request.Description,
                SourceVmName = request.SourceVmName,
                SourceBackend = VmCreationMode.WMI,
                MemoryMB = request.CustomMemoryMB ?? (int)sourceVm.MemoryMB,
                CpuCount = request.CustomCpuCount ?? sourceVm.CpuCount,
                DiskSizeGB = request.CustomDiskSizeGB ?? 20,
                Generation = sourceVm.Generation,
                SecureBoot = true,
                Category = request.Category,
                IsPublic = request.IsPublic,
                Tags = request.Tags ?? new List<string>(),
                SupportedBackends = new List<VmCreationMode> { VmCreationMode.WMI, VmCreationMode.HCS },
                Owner = "System",
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow,
                Version = "1.0"
            };

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
    public async Task<IActionResult> CreateVmViaWizard([FromBody] VmWizardRequest req)
    {
        _logger.LogInformation("Creating VM via wizard with Id: {Id}, Workload: {Workload}", req?.Id, req?.WorkloadType);
        try
        {
            if (req == null)
            {
                _logger.LogWarning("Create VM via wizard request is null");
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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

            // Build CreateVmSpec from wizard recommendation
            var config = wizardResponse.RecommendedConfiguration;
            var spec = new CreateVmSpec
            {
                Name = config.Name,
                CpuCount = config.CpuCount,
                MemoryMB = config.MemoryMB,
                Generation = config.Generation,
                DiskSizeGB = config.DiskSizeGB,
                DiskPath = config.VhdPath,
                NetworkName = config.SwitchName,
                ExtendedProperties = new Dictionary<string, object>
                {
                    ["SecureBoot"] = config.SecureBoot,
                    ["Notes"] = config.Notes ?? $"Created via wizard for {req.WorkloadType} workload"
                }
            };

            var result = await _vmProvider.CreateVmAsync(spec);
            return Content(result, "application/json");
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
                MemoryMB = Math.Min(adjustedMemory, 1048576),
                CpuCount = Math.Min(adjustedCpu, 128),
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
            response.Recommendations["backend"] = selectedBackend == VmCreationMode.HCS
                ? "HCS backend selected for container-like behavior"
                : "WMI backend selected for full Hyper-V VM features";
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

    #region Template Storage Helper Methods

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

    #endregion
}

#region Inline Model Classes

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

/// <summary>Request model for bulk VM operations.</summary>
public class BulkVmOperationRequest
{
    [Required(ErrorMessage = "VM IDs list is required")]
    [MinLength(1, ErrorMessage = "At least one VM ID must be provided")]
    public List<string> VmIds { get; set; } = new List<string>();
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

#endregion
