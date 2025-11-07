using Microsoft.AspNetCore.Mvc;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers
{
    [ApiController]
    [Route("api/v1/storage")]
    public class StorageController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public StorageController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// Lists all fixed storage devices with details (name, filesystem, size, used, free).
        /// </summary>
        [HttpGet("devices")]
        public async Task<ActionResult<List<StorageDeviceInfo>>> GetStorageDevices()
        {
            try
            {
                var devices = await _storageService.ListStorageDevicesAsync();
                return Ok(devices);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets suitable storage locations for VHDX based on minimum free space.
        /// </summary>
        /// <param name="minGb">Minimum free space in GB (default: 10).</param>
        [HttpGet("locations")]
        public async Task<ActionResult<List<StorageLocation>>> GetSuitableVhdLocations([FromQuery(Name = "minGb"), Range(1, int.MaxValue)] long minGb = 10)
        {
            try
            {
                var locations = await _storageService.GetSuitableVhdLocationsAsync(minGb);
                return Ok(locations);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
