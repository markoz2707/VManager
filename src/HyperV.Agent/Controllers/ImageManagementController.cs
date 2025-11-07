using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers
{
    /// <summary>
    /// Controller for managing virtual hard disk (VHD/VHDX) operations including compact, merge, convert, resize, and snapshot operations.
    /// </summary>
    [ApiController]
    [Route("api/v1/images")]
    public class ImageManagementController : ControllerBase
    {
        private readonly IImageManagementService _imageManagementService;

        public ImageManagementController(IImageManagementService imageManagementService)
        {
            _imageManagementService = imageManagementService ?? throw new ArgumentNullException(nameof(imageManagementService));
        }

        /// <summary>
        /// Compacts a virtual hard disk by removing unused space.
        /// </summary>
        /// <param name="request">Compact operation parameters including path and mode.</param>
        /// <returns>Success response when compaction completes.</returns>
        [HttpPost("compact")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Compact Virtual Hard Disk", Description = "Compacts a VHD/VHDX file by removing unused space and optionally reducing file size.")]
        public async Task<IActionResult> CompactVirtualHardDisk([FromBody] CompactVhdRequest request)
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

                if (string.IsNullOrEmpty(request.Path))
                {
                    return BadRequest(new { error = "Path field is required" });
                }

                await _imageManagementService.CompactVirtualHardDiskAsync(request);
                return Ok(new { message = "Virtual hard disk compacted successfully", path = request.Path });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Compaction failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during compaction: {ex.Message}" });
            }
        }

        /// <summary>
        /// Merges a child virtual hard disk with its parent.
        /// </summary>
        /// <param name="request">Merge operation parameters including source and destination paths.</param>
        /// <returns>Success response when merge completes.</returns>
        [HttpPost("merge")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Merge Virtual Hard Disk", Description = "Merges a differencing disk with its parent disk.")]
        public async Task<IActionResult> MergeVirtualHardDisk([FromBody] MergeDiskRequest request)
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

                if (string.IsNullOrEmpty(request.ChildPath) || string.IsNullOrEmpty(request.DestinationPath))
                {
                    return BadRequest(new { error = "ChildPath and DestinationPath fields are required" });
                }

                await _imageManagementService.MergeVirtualHardDiskAsync(request);
                return Ok(new { message = "Virtual hard disk merged successfully", source = request.ChildPath, destination = request.DestinationPath });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Merge failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during merge: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets virtual hard disk setting data including type, format, and size information.
        /// </summary>
        /// <param name="path">Path to the virtual hard disk file.</param>
        /// <returns>Virtual hard disk setting data.</returns>
        [HttpGet("settings")]
        [ProducesResponseType(typeof(VirtualHardDiskSettingData), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Get Virtual Hard Disk Settings", Description = "Retrieves detailed setting data for a virtual hard disk including type, format, and size information.")]
        public async Task<IActionResult> GetVirtualHardDiskSettingData([FromQuery][Required] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return BadRequest(new { error = "Path parameter is required" });
                }

                var settingData = await _imageManagementService.GetVirtualHardDiskSettingDataAsync(path);
                return Ok(settingData);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"Virtual hard disk not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get setting data: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets the current state of a virtual hard disk including operational status.
        /// </summary>
        /// <param name="path">Path to the virtual hard disk file.</param>
        /// <returns>Virtual hard disk state information.</returns>
        [HttpGet("state")]
        [ProducesResponseType(typeof(VirtualHardDiskState), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Get Virtual Hard Disk State", Description = "Retrieves the current operational state of a virtual hard disk.")]
        public async Task<IActionResult> GetVirtualHardDiskState([FromQuery][Required] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return BadRequest(new { error = "Path parameter is required" });
                }

                var state = await _imageManagementService.GetVirtualHardDiskStateAsync(path);
                return Ok(state);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"Virtual hard disk not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get state: {ex.Message}" });
            }
        }

        /// <summary>
        /// Converts a virtual hard disk to a different format or type.
        /// </summary>
        /// <param name="request">Conversion parameters including source, destination, and format options.</param>
        /// <returns>Success message with conversion details.</returns>
        [HttpPost("convert")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Convert Virtual Hard Disk", Description = "Converts a virtual hard disk to a different format (VHD to VHDX, fixed to dynamic, etc.).")]
        public async Task<IActionResult> ConvertVirtualHardDisk([FromBody] ConvertVhdRequest request)
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

                if (string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath))
                {
                    return BadRequest(new { error = "SourcePath and DestinationPath fields are required" });
                }

                var result = await _imageManagementService.ConvertVirtualHardDiskAsync(request);
                return Ok(new { message = result, source = request.SourcePath, destination = request.DestinationPath });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Conversion failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during conversion: {ex.Message}" });
            }
        }

        /// <summary>
        /// Converts a virtual hard disk to VHD Set format.
        /// </summary>
        /// <param name="request">VHD Set conversion parameters.</param>
        /// <returns>Success message with conversion details.</returns>
        [HttpPost("convert-to-vhdset")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Convert to VHD Set", Description = "Converts a virtual hard disk to VHD Set format for improved performance and features.")]
        public async Task<IActionResult> ConvertVirtualHardDiskToVHDSet([FromBody] ConvertToVhdSetRequest request)
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

                if (string.IsNullOrEmpty(request.SourcePath) || string.IsNullOrEmpty(request.DestinationPath))
                {
                    return BadRequest(new { error = "SourcePath and DestinationPath fields are required" });
                }

                var result = await _imageManagementService.ConvertVirtualHardDiskToVHDSetAsync(request);
                return Ok(new { message = result, source = request.SourcePath, destination = request.DestinationPath });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"VHD Set conversion failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during VHD Set conversion: {ex.Message}" });
            }
        }

        /// <summary>
        /// Deletes a VHD snapshot from a VHD Set.
        /// </summary>
        /// <param name="vhdSetPath">Path to the VHD Set file.</param>
        /// <param name="snapshotId">ID of the snapshot to delete.</param>
        /// <returns>Success response when snapshot is deleted.</returns>
        [HttpDelete("snapshots")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Delete VHD Snapshot", Description = "Deletes a specific snapshot from a VHD Set.")]
        public async Task<IActionResult> DeleteVHDSnapshot([FromQuery][Required] string vhdSetPath, [FromQuery][Required] string snapshotId)
        {
            try
            {
                if (string.IsNullOrEmpty(vhdSetPath))
                {
                    return BadRequest(new { error = "vhdSetPath parameter is required" });
                }

                if (string.IsNullOrEmpty(snapshotId))
                {
                    return BadRequest(new { error = "snapshotId parameter is required" });
                }

                await _imageManagementService.DeleteVHDSnapshotAsync(vhdSetPath, snapshotId);
                return Ok(new { message = "VHD snapshot deleted successfully", vhdSetPath, snapshotId });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"VHD Set or snapshot not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to delete snapshot: {ex.Message}" });
            }
        }

        /// <summary>
        /// Finds mounted storage image instances for a given image path.
        /// </summary>
        /// <param name="imagePath">Path to the storage image file.</param>
        /// <returns>Mounted storage image information.</returns>
        [HttpGet("mounted")]
        [ProducesResponseType(typeof(MountedStorageImageResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Find Mounted Storage Image", Description = "Finds mounted storage image instances for a given image path.")]
        public async Task<IActionResult> FindMountedStorageImageInstance([FromQuery][Required] string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    return BadRequest(new { error = "imagePath parameter is required" });
                }

                var mountedImage = await _imageManagementService.FindMountedStorageImageInstanceAsync(imagePath);
                return Ok(mountedImage);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"Mounted image not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to find mounted image: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets VHD Set information including snapshots and properties.
        /// </summary>
        /// <param name="vhdSetPath">Path to the VHD Set file.</param>
        /// <returns>VHD Set information including snapshots.</returns>
        [HttpGet("vhdset-info")]
        [ProducesResponseType(typeof(VhdSetInformationResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Get VHD Set Information", Description = "Retrieves comprehensive information about a VHD Set including snapshots and properties.")]
        public async Task<IActionResult> GetVHDSetInformation([FromQuery][Required] string vhdSetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(vhdSetPath))
                {
                    return BadRequest(new { error = "vhdSetPath parameter is required" });
                }

                var vhdSetInfo = await _imageManagementService.GetVHDSetInformationAsync(vhdSetPath);
                return Ok(vhdSetInfo);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"VHD Set not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get VHD Set information: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets information about a specific VHD snapshot.
        /// </summary>
        /// <param name="vhdSetPath">Path to the VHD Set file.</param>
        /// <param name="snapshotId">ID of the snapshot to retrieve.</param>
        /// <returns>VHD snapshot information.</returns>
        [HttpGet("snapshot-info")]
        [ProducesResponseType(typeof(VhdSnapshotInformationResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Get VHD Snapshot Information", Description = "Retrieves detailed information about a specific VHD snapshot.")]
        public async Task<IActionResult> GetVHDSnapshotInformation([FromQuery][Required] string vhdSetPath, [FromQuery][Required] string snapshotId)
        {
            try
            {
                if (string.IsNullOrEmpty(vhdSetPath))
                {
                    return BadRequest(new { error = "vhdSetPath parameter is required" });
                }

                if (string.IsNullOrEmpty(snapshotId))
                {
                    return BadRequest(new { error = "snapshotId parameter is required" });
                }

                var snapshotInfo = await _imageManagementService.GetVHDSnapshotInformationAsync(vhdSetPath, snapshotId);
                return Ok(snapshotInfo);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = $"VHD Set or snapshot not found: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get snapshot information: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets virtual disk changes since a specific change tracking ID.
        /// </summary>
        /// <param name="request">Request parameters including VHD Set path and change tracking information.</param>
        /// <returns>Virtual disk changes information.</returns>
        [HttpPost("changes")]
        [ProducesResponseType(typeof(VirtualDiskChangesResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Get Virtual Disk Changes", Description = "Retrieves changes made to a virtual disk since a specific change tracking ID.")]
        public async Task<IActionResult> GetVirtualDiskChanges([FromBody] GetVirtualDiskChangesRequest request)
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

                if (string.IsNullOrEmpty(request.VhdSetPath))
                {
                    return BadRequest(new { error = "VhdSetPath field is required" });
                }

                var changes = await _imageManagementService.GetVirtualDiskChangesAsync(request);
                return Ok(changes);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Failed to get disk changes: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error getting disk changes: {ex.Message}" });
            }
        }

        /// <summary>
        /// Optimizes a VHD Set by performing maintenance operations.
        /// </summary>
        /// <param name="vhdSetPath">Path to the VHD Set file to optimize.</param>
        /// <returns>Success response when optimization completes.</returns>
        [HttpPost("optimize")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Optimize VHD Set", Description = "Performs optimization operations on a VHD Set to improve performance.")]
        public async Task<IActionResult> OptimizeVHDSet([FromQuery][Required] string vhdSetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(vhdSetPath))
                {
                    return BadRequest(new { error = "vhdSetPath parameter is required" });
                }

                await _imageManagementService.OptimizeVHDSetAsync(vhdSetPath);
                return Ok(new { message = "VHD Set optimized successfully", vhdSetPath });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Optimization failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during optimization: {ex.Message}" });
            }
        }

        /// <summary>
        /// Sets information for a VHD snapshot including name, description, and active state.
        /// </summary>
        /// <param name="request">Snapshot information update parameters.</param>
        /// <returns>Success response when snapshot information is updated.</returns>
        [HttpPut("snapshot-info")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Set VHD Snapshot Information", Description = "Updates information for a VHD snapshot including name, description, and active state.")]
        public async Task<IActionResult> SetVHDSnapshotInformation([FromBody] SetVhdSnapshotInfoRequest request)
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

                if (string.IsNullOrEmpty(request.VhdSetPath) || string.IsNullOrEmpty(request.SnapshotId))
                {
                    return BadRequest(new { error = "VhdSetPath and SnapshotId fields are required" });
                }

                await _imageManagementService.SetVHDSnapshotInformationAsync(request);
                return Ok(new { message = "VHD snapshot information updated successfully", vhdSetPath = request.VhdSetPath, snapshotId = request.SnapshotId });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Failed to update snapshot information: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error updating snapshot information: {ex.Message}" });
            }
        }

        /// <summary>
        /// Validates whether a storage path supports persistent reservations.
        /// </summary>
        /// <param name="path">Path to validate for persistent reservation support.</param>
        /// <returns>Boolean indicating whether persistent reservations are supported.</returns>
        [HttpGet("validate-persistent-reservations")]
        [ProducesResponseType(typeof(bool), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        [SwaggerOperation(Summary = "Validate Persistent Reservation Support", Description = "Validates whether a storage path supports persistent reservations.")]
        public async Task<IActionResult> ValidatePersistentReservationSupport([FromQuery][Required] string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return BadRequest(new { error = "path parameter is required" });
                }

                var isSupported = await _imageManagementService.ValidatePersistentReservationSupportAsync(path);
                return Ok(new { path, persistentReservationSupported = isSupported });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, new { error = $"Validation failed: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Unexpected error during validation: {ex.Message}" });
            }
        }
    }
}