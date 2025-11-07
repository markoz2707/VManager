using HyperV.Contracts.Services;
using HyperV.Core.Hcn.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HyperV.Agent.Controllers;

/// <summary>Operacje sieciowe (HCN).</summary>
[ApiController]
[Route("api/v1/networks")]
public sealed class NetworksController : ControllerBase
{
    private readonly NetworkService _svc;
    private readonly WmiNetworkService _wmiSvc;
    private readonly FibreChannelService _fibreChannelService;
    private readonly ILogger<NetworksController> _logger;
    
    public NetworksController(NetworkService svc, WmiNetworkService wmiSvc, FibreChannelService fibreChannelService, ILogger<NetworksController> logger)
    {
        _svc = svc;
        _wmiSvc = wmiSvc;
        _fibreChannelService = fibreChannelService;
        _logger = logger;
    }

    /// <summary>Listuje wszystkie sieci HCN i WMI.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "List networks", Description = "Lists all HCN and WMI networks.")]
    public IActionResult ListNetworks()
    {
        try
        {
            _logger.LogDebug("Starting to list HCN networks");
            var networksJson = _svc.ListNetworks();
            _logger.LogDebug("Successfully retrieved networks data");

            // Parse HCN networks
            var hcsNetworks = new List<object>();
            if (!string.IsNullOrEmpty(networksJson))
            {
                try
                {
                    var networkIds = JsonSerializer.Deserialize<string[]>(networksJson);
                    foreach (var networkId in networkIds ?? Array.Empty<string>())
                    {
                        try
                        {
                            // Get detailed properties for each network
                            var properties = _svc.QueryNetworkProperties(Guid.Parse(networkId));
                            var networkData = JsonSerializer.Deserialize<JsonElement>(properties);
                            
                            hcsNetworks.Add(new
                            {
                                id = networkId,
                                name = networkData.TryGetProperty("Name", out var nameEl) ? nameEl.GetString() : networkId,
                                type = networkData.TryGetProperty("Type", out var typeEl) ? typeEl.GetString() : "NAT",
                                subnet = GetSubnetFromNetwork(networkData)
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Failed to get properties for network {NetworkId}: {Message}", networkId, ex.Message);
                            // Add basic info if we can't get details
                            hcsNetworks.Add(new
                            {
                                id = networkId,
                                name = networkId,
                                type = "NAT",
                                subnet = "N/A"
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("Failed to parse networks JSON: {Message}", ex.Message);
                }
            }

            // Get WMI networks
            var wmiNetworks = new List<object>();
            try
            {
                _logger.LogDebug("Starting to list WMI virtual switches");
                var switches = _wmiSvc.ListVirtualSwitchesSummary();
                foreach (var sw in switches)
                {
                    wmiNetworks.Add(new
                    {
                        id = sw.Id,
                        name = sw.Name,
                        type = sw.Type,
                        subnet = "N/A" // WMI switches don't have subnet concept like HCN
                    });
                }
                _logger.LogDebug("Successfully retrieved {Count} WMI virtual switches", wmiNetworks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to list WMI virtual switches: {Message}", ex.Message);
                // Continue with empty WMI list on error
            }

            // Return in the format expected by frontend (same format as VMs API)
            var result = new NetworksResponse
            {
                WMI = wmiNetworks.ToArray(),
                HCS = hcsNetworks.ToArray()
            };

            return Ok(result);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied when listing networks. Error code: {ErrorCode}, Message: {Message}", ex.NativeErrorCode, ex.Message);
            return StatusCode(403, new { error = "Access denied. HCN API requires administrator privileges. Please run the application as administrator.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list networks: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to list networks: {ex.Message}" });
        }
    }

    /// <summary>Listuje fizyczne adaptery sieciowe.</summary>
    [HttpGet("adapters")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "List physical adapters", Description = "Lists physical network adapters for external switch creation.")]
    public IActionResult ListPhysicalAdapters()
    {
        try
        {
            _logger.LogDebug("Listing physical adapters");
            var adapters = _wmiSvc.ListPhysicalAdapters();
            var result = adapters.ToArray();
            
            _logger.LogDebug("Successfully retrieved {Count} physical adapters", result.Length);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list physical adapters: {Message}", ex.Message);
            return StatusCode(500, new { error = $"Failed to list physical adapters: {ex.Message}" });
        }
    }

    private static string GetSubnetFromNetwork(JsonElement networkData)
    {
        try
        {
            if (networkData.TryGetProperty("Ipam", out var ipam) &&
                ipam.TryGetProperty("Subnets", out var subnets) &&
                subnets.ValueKind == JsonValueKind.Array)
            {
                var firstSubnet = subnets.EnumerateArray().FirstOrDefault();
                if (firstSubnet.TryGetProperty("IpAddressPrefix", out var prefix))
                {
                    return prefix.GetString() ?? "N/A";
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return "N/A";
    }

    [HttpPost("nat")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    [SwaggerOperation(Summary = "Create NAT network", Description = "Creates a NAT virtual switch via HCN API.")]
    public IActionResult CreateNat([FromQuery] string name, [FromQuery] string prefix = "192.168.100.0/24")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("CreateNat called with empty or null name");
            return BadRequest(new { error = "Network name is required" });
        }

        try
        {
            _logger.LogDebug("Creating NAT network with name: {NetworkName}, prefix: {Prefix}", name, prefix);
            var id = _svc.CreateNATNetwork(name, prefix);
            _logger.LogInformation("Successfully created NAT network {NetworkName} with ID: {NetworkId}", name, id);
            return Ok(new { id });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied when creating NAT network {NetworkName}. Error code: {ErrorCode}, Message: {Message}", name, ex.NativeErrorCode, ex.Message);
            return StatusCode(403, new { error = "Access denied. HCN API requires administrator privileges. Please run the application as administrator.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create NAT network {NetworkName}: {Message}", name, ex.Message);
            return StatusCode(500, new { error = $"Failed to create NAT network: {ex.Message}" });
        }
    }

    /// <summary>Tworzy wirtualny przełącznik WMI.</summary>
    [HttpPost("wmi")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    [SwaggerOperation(Summary = "Create WMI virtual switch", Description = "Creates a virtual switch via WMI API (Internal/Private/External types supported).")]
    public IActionResult CreateWmiSwitch([FromQuery] string name, [FromQuery] string type = "Internal", [FromQuery] string? notes = null, [FromQuery] string? externalAdapterName = null, [FromQuery] bool allowManagementOS = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("CreateWmiSwitch called with empty or null name");
            return BadRequest(new { error = "Switch name is required" });
        }

        if (!Enum.TryParse<WmiNetworkService.WmiSwitchType>(type, true, out var switchType))
        {
            _logger.LogWarning("CreateWmiSwitch called with invalid type: {Type}", type);
            return BadRequest(new { error = $"Invalid switch type. Supported types: {string.Join(", ", Enum.GetNames<WmiNetworkService.WmiSwitchType>())}" });
        }

        if (switchType == WmiNetworkService.WmiSwitchType.External && string.IsNullOrWhiteSpace(externalAdapterName))
        {
            _logger.LogWarning("CreateWmiSwitch called with External type but no externalAdapterName");
            return BadRequest(new { error = "External adapter name is required for External switch type" });
        }

        try
        {
            _logger.LogDebug("Creating WMI virtual switch with name: {SwitchName}, type: {SwitchType}, adapter: {ExternalAdapterName}, allowManagementOS: {AllowManagementOS}", name, switchType, externalAdapterName, allowManagementOS);
            var id = _wmiSvc.CreateVirtualSwitch(name, switchType, notes, externalAdapterName, allowManagementOS);
            _logger.LogInformation("Successfully created WMI virtual switch {SwitchName} with ID: {SwitchId}", name, id);
            return Ok(new { id = id.ToString() });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid argument for WMI virtual switch {SwitchName}: {Message}", name, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning("Unsupported switch type {SwitchType} for switch {SwitchName}: {Message}", switchType, name, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied when creating WMI virtual switch {SwitchName}", name);
            return StatusCode(403, new { error = "Access denied. WMI operations require administrator privileges. Please run the application as administrator." });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied when creating WMI virtual switch {SwitchName}. Error code: {ErrorCode}, Message: {Message}", name, ex.NativeErrorCode, ex.Message);
            return StatusCode(403, new { error = "Access denied. WMI operations require administrator privileges. Please run the application as administrator.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create WMI virtual switch {SwitchName}: {Message}", name, ex.Message);
            return StatusCode(500, new { error = $"Failed to create WMI virtual switch: {ex.Message}" });
        }
    }

    /// <summary>Edytuje wirtualny przełącznik WMI.</summary>
    [HttpPut("wmi/{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(Summary = "Update WMI virtual switch", Description = "Updates a WMI virtual switch settings (name, notes).")]
    public IActionResult UpdateWmiSwitch([FromRoute] Guid id, [FromQuery] string? name = null, [FromQuery] string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(notes))
        {
            _logger.LogWarning("UpdateWmiSwitch called without any changes for switch {SwitchId}", id);
            return BadRequest(new { error = "At least one property (name or notes) must be provided" });
        }

        try
        {
            _logger.LogDebug("Updating WMI virtual switch {SwitchId} with name: {Name}, notes: {Notes}", id, name, notes);
            _wmiSvc.UpdateVirtualSwitch(id, name, notes);
            _logger.LogInformation("Successfully updated WMI virtual switch {SwitchId}", id);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("WMI virtual switch {SwitchId} not found: {Message}", id, ex.Message);
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied when updating WMI virtual switch {SwitchId}", id);
            return StatusCode(403, new { error = "Access denied. WMI operations require administrator privileges. Please run the application as administrator." });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied when updating WMI virtual switch {SwitchId}. Error code: {ErrorCode}, Message: {Message}", id, ex.NativeErrorCode, ex.Message);
            return StatusCode(403, new { error = "Access denied. WMI operations require administrator privileges. Please run the application as administrator.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update WMI virtual switch {SwitchId}: {Message}", id, ex.Message);
            return StatusCode(500, new { error = $"Failed to update WMI virtual switch: {ex.Message}" });
        }
    }

    /// <summary>Usuwa sieć.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete network", Description = "Deletes a network by ID (supports both HCN and WMI).")]
    public IActionResult DeleteNetwork([FromRoute] Guid id, [FromQuery] string environment = "HCS")
    {
        try
        {
            _logger.LogDebug("Deleting {Environment} network with ID: {NetworkId}", environment, id);
            
            if (string.Equals(environment, "WMI", StringComparison.OrdinalIgnoreCase))
            {
                _wmiSvc.DeleteVirtualSwitch(id);
                _logger.LogInformation("Successfully deleted WMI virtual switch with ID: {NetworkId}", id);
            }
            else
            {
                _svc.DeleteNetwork(id);
                _logger.LogInformation("Successfully deleted HCN network with ID: {NetworkId}", id);
            }
            
            return Ok();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("{Environment} network with ID {NetworkId} not found when attempting to delete", environment, id);
            return NotFound();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("{Environment} network with ID {NetworkId} not found: {Message}", environment, id, ex.Message);
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Access denied when deleting {Environment} network {NetworkId}", environment, id);
            return StatusCode(403, new { error = $"Access denied. {environment} operations require administrator privileges. Please run the application as administrator." });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied when deleting {Environment} network {NetworkId}. Error code: {ErrorCode}, Message: {Message}", environment, id, ex.NativeErrorCode, ex.Message);
            return StatusCode(403, new { error = $"Access denied. {environment} operations require administrator privileges. Please run the application as administrator.", details = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Environment} network {NetworkId}: {Message}", environment, id, ex.Message);
            return StatusCode(500, new { error = $"Failed to delete {environment} network: {ex.Message}" });
        }
    }

    /// <summary>Pobiera właściwości sieci.</summary>
    [HttpGet("{id}/properties")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get network properties", Description = "Gets properties of a network by ID.")]
    public IActionResult GetNetworkProperties([FromRoute] Guid id, [FromQuery] string query = "")
    {
        try
        {
            _logger.LogDebug("Getting properties for network {NetworkId} with query: {Query}", id, query);
            var properties = _svc.QueryNetworkProperties(id, query);
            _logger.LogDebug("Successfully retrieved properties for network {NetworkId}", id);
            return Ok(properties);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("Network with ID {NetworkId} not found when getting properties", id);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get properties for network {NetworkId}: {Message}", id, ex.Message);
            return StatusCode(500, new { error = $"Failed to get network properties: {ex.Message}" });
        }
    }

    /// <summary>Tworzy endpoint w sieci.</summary>
    [HttpPost("{networkId}/endpoints")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Create endpoint", Description = "Creates an endpoint in the specified network.")]
    public IActionResult CreateEndpoint([FromRoute] Guid networkId, [FromQuery] string name, [FromQuery] string ipAddress = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("CreateEndpoint called with empty or null name for network {NetworkId}", networkId);
                return BadRequest(new { error = "Endpoint name is required" });
            }

            _logger.LogDebug("Creating endpoint {EndpointName} in network {NetworkId} with IP: {IpAddress}", name, networkId, ipAddress);
            var endpointId = _svc.CreateEndpoint(networkId, name, ipAddress);
            _logger.LogInformation("Successfully created endpoint {EndpointName} with ID: {EndpointId} in network {NetworkId}", name, endpointId, networkId);
            return Ok(new { id = endpointId });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("Network with ID {NetworkId} not found when creating endpoint {EndpointName}", networkId, name);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create endpoint {EndpointName} in network {NetworkId}: {Message}", name, networkId, ex.Message);
            return StatusCode(500, new { error = $"Failed to create endpoint: {ex.Message}" });
        }
    }

    /// <summary>Usuwa endpoint.</summary>
    [HttpDelete("endpoints/{endpointId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete endpoint", Description = "Deletes an endpoint by ID.")]
    public IActionResult DeleteEndpoint([FromRoute] Guid endpointId)
    {
        try
        {
            _logger.LogDebug("Deleting endpoint with ID: {EndpointId}", endpointId);
            _svc.DeleteEndpoint(endpointId);
            _logger.LogInformation("Successfully deleted endpoint with ID: {EndpointId}", endpointId);
            return Ok();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("Endpoint with ID {EndpointId} not found when attempting to delete", endpointId);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete endpoint {EndpointId}: {Message}", endpointId, ex.Message);
            return StatusCode(500, new { error = $"Failed to delete endpoint: {ex.Message}" });
        }
    }

    /// <summary>Pobiera właściwości endpoint.</summary>
    [HttpGet("endpoints/{endpointId}/properties")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get endpoint properties", Description = "Gets properties of an endpoint by ID.")]
    public IActionResult GetEndpointProperties([FromRoute] Guid endpointId, [FromQuery] string query = "")
    {
        try
        {
            _logger.LogDebug("Getting properties for endpoint {EndpointId} with query: {Query}", endpointId, query);
            var properties = _svc.QueryEndpointProperties(endpointId, query);
            _logger.LogDebug("Successfully retrieved properties for endpoint {EndpointId}", endpointId);
            return Ok(properties);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            _logger.LogWarning("Endpoint with ID {EndpointId} not found when getting properties", endpointId);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get properties for endpoint {EndpointId}: {Message}", endpointId, ex.Message);
            return StatusCode(500, new { error = $"Failed to get endpoint properties: {ex.Message}" });
        }
    }

    /// <summary>Enables or disables a switch extension.</summary>
    [HttpPut("{switchId}/extensions/{extensionName}/enable")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Set Switch Extension State", Description = "Enables or disables a specific extension on a virtual switch.")]
    public IActionResult SetSwitchExtensionState([FromRoute] Guid switchId, [FromRoute] string extensionName, [FromQuery] bool enabled)
    {
        try
        {
            _logger.LogDebug("Setting extension '{ExtensionName}' to {Enabled} on switch '{SwitchId}'", extensionName, enabled, switchId);
            _wmiSvc.SetExtensionEnabledState(switchId.ToString(), extensionName, enabled);
            _logger.LogInformation("Extension '{ExtensionName}' successfully {State} on switch '{SwitchId}'", extensionName, enabled ? "enabled" : "disabled", switchId);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("Switch '{SwitchId}' or extension '{ExtensionName}' not found", switchId, extensionName);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set extension state: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Checks if a switch supports trunk mode.</summary>
    [HttpGet("{switchId}/trunk")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Check Switch Trunk Mode Support", Description = "Checks if the virtual switch supports trunk mode.")]
    public IActionResult CheckTrunkMode([FromRoute] Guid switchId)
    {
        try
        {
            _logger.LogDebug("Checking trunk mode for switch '{SwitchId}'", switchId);
            var supportsTrunk = _wmiSvc.SupportsTrunkMode(switchId.ToString());
            _logger.LogDebug("Switch '{SwitchId}' trunk mode support: {SupportsTrunk}", switchId, supportsTrunk);
            return Ok(new { supportsTrunk });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("Switch '{SwitchId}' not found", switchId);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check trunk mode: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Creates a SAN pool via FibreChannel service.</summary>
    [HttpPost("fibrechannel/san")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create SAN", Description = "Creates a FibreChannel SAN pool.")]
    public IActionResult CreateSan([FromBody] CreateSanRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SanName) || request.WwpnArray == null || request.WwnnArray == null)
            {
                return BadRequest(new { error = "SanName, WwpnArray, and WwnnArray are required" });
            }

            var result = _fibreChannelService.CreateSan(request.SanName, request.WwpnArray, request.WwnnArray, request.Notes);
            return Ok(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SAN: {Message}", ex.Message);
            return BadRequest(new { error = $"Failed to create SAN: {ex.Message}" });
        }
    }

    /// <summary>Deletes a SAN pool.</summary>
    [HttpDelete("fibrechannel/san/{poolId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Delete SAN", Description = "Deletes a FibreChannel SAN pool by ID.")]
    public IActionResult DeleteSan([FromRoute] Guid poolId)
    {
        try
        {
            _fibreChannelService.DeleteSan(poolId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SAN {PoolId}: {Message}", poolId, ex.Message);
            return BadRequest(new { error = $"Failed to delete SAN: {ex.Message}" });
        }
    }

    /// <summary>Gets SAN pool information.</summary>
    [HttpGet("fibrechannel/san/{poolId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Get SAN Info", Description = "Retrieves information for a FibreChannel SAN pool.")]
    public IActionResult GetSanInfo([FromRoute] Guid poolId)
    {
        try
        {
            var result = _fibreChannelService.GetSanInfo(poolId);
            return Ok(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SAN info for {PoolId}: {Message}", poolId, ex.Message);
            return BadRequest(new { error = $"Failed to get SAN info: {ex.Message}" });
        }
    }

    /// <summary>Creates a virtual FC port for a VM.</summary>
    [HttpPost("vms/{vmName}/fibrechannel/port")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create Virtual FC Port", Description = "Creates a virtual FibreChannel port for a VM.")]
    public IActionResult CreateVirtualFcPort([FromRoute] string vmName, [FromBody] CreateFcPortRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(vmName) || string.IsNullOrWhiteSpace(request.SanPoolId) ||
                string.IsNullOrWhiteSpace(request.Wwpn) || string.IsNullOrWhiteSpace(request.Wwnn))
            {
                return BadRequest(new { error = "vmName, SanPoolId, Wwpn, and Wwnn are required" });
            }

            var result = _fibreChannelService.CreateVirtualFcPort(vmName, request.SanPoolId, request.Wwpn, request.Wwnn);
            return Ok(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create virtual FC port for VM {VmName}: {Message}", vmName, ex.Message);
            return BadRequest(new { error = $"Failed to create virtual FC port: {ex.Message}" });
        }
    }

    /// <summary>Request model for creating SAN.</summary>
    public class CreateSanRequest
    {
        public string SanName { get; set; } = string.Empty;
        public string[] WwpnArray { get; set; } = Array.Empty<string>();
        public string[] WwnnArray { get; set; } = Array.Empty<string>();
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>Request model for creating FC port.</summary>
    public class CreateFcPortRequest
    {
        public string SanPoolId { get; set; } = string.Empty;
        public string Wwpn { get; set; } = string.Empty;
        public string Wwnn { get; set; } = string.Empty;
    }

    public class NetworksResponse
    {
        [JsonPropertyName("WMI")]
        public object[] WMI { get; set; } = Array.Empty<object>();
        
        [JsonPropertyName("HCS")]
        public object[] HCS { get; set; } = Array.Empty<object>();
    }
}
