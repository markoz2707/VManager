using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace HyperV.Agent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StorageController : ControllerBase
    {
        private readonly IStorageService _storageService;

        public StorageController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpPost("vhd")]
        public IActionResult CreateVhd([FromBody] CreateVhdRequest request)
        {
            _storageService.CreateVirtualHardDisk(request);
            return Ok();
        }

        [HttpPut("vhd/attach")]
        public IActionResult AttachVhd([FromQuery] string vmName, [FromQuery] string vhdPath)
        {
            try
            {
                _storageService.AttachVirtualHardDisk(vmName, vhdPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
            }
        }

        [HttpPut("vhd/detach")]
        public IActionResult DetachVhd([FromQuery] string vmName, [FromQuery] string vhdPath)
        {
            _storageService.DetachVirtualHardDisk(vmName, vhdPath);
            return Ok();
        }

        [HttpPut("vhd/resize")]
        public IActionResult ResizeVhd([FromBody] ResizeVhdRequest request)
        {
            _storageService.ResizeVirtualHardDisk(request);
            return Ok();
        }
    }
}
