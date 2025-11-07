using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers
{
    /// <summary>Operacje na zadaniach związanych z pamięcią masową.</summary>
    [ApiController]
    [Route("api/v1/jobs")]
    public class JobsController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobsController(IJobService jobService)
        {
            _jobService = jobService;
        }

        /// <summary>Pobiera listę wszystkich zadań związanych z pamięcią masową.</summary>
        [HttpGet("storage")]
        [ProducesResponseType(typeof(List<StorageJobResponse>), 200)]
        [SwaggerOperation(Summary = "Get Storage Jobs", Description = "Gets list of all storage jobs.")]
        public async Task<IActionResult> GetStorageJobs()
        {
            try
            {
                var jobs = await _jobService.GetStorageJobsAsync();
                return Ok(jobs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get storage jobs: {ex.Message}" });
            }
        }

        /// <summary>Pobiera szczegóły konkretnego zadania związanego z pamięcią masową.</summary>
        [HttpGet("storage/{jobId}")]
        [ProducesResponseType(typeof(StorageJobResponse), 200)]
        [ProducesResponseType(404)]
        [SwaggerOperation(Summary = "Get Storage Job", Description = "Gets details of a specific storage job.")]
        public async Task<IActionResult> GetStorageJob([FromRoute] string jobId)
        {
            try
            {
                var job = await _jobService.GetStorageJobAsync(jobId);
                return Ok(job);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = $"Job '{jobId}' not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get storage job: {ex.Message}" });
            }
        }

        /// <summary>Pobiera elementy, na które wpływa zadanie.</summary>
        [HttpGet("storage/{jobId}/affected-elements")]
        [ProducesResponseType(typeof(List<AffectedElementResponse>), 200)]
        [ProducesResponseType(404)]
        [SwaggerOperation(Summary = "Get Job Affected Elements", Description = "Gets elements affected by a storage job.")]
        public async Task<IActionResult> GetJobAffectedElements([FromRoute] string jobId)
        {
            try
            {
                var elements = await _jobService.GetJobAffectedElementsAsync(jobId);
                return Ok(elements);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = $"Job '{jobId}' not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to get affected elements: {ex.Message}" });
            }
        }

        /// <summary>Anuluje zadanie związane z pamięcią masową.</summary>
        [HttpPost("storage/{jobId}/cancel")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [SwaggerOperation(Summary = "Cancel Storage Job", Description = "Cancels a storage job.")]
        public async Task<IActionResult> CancelStorageJob([FromRoute] string jobId)
        {
            try
            {
                await _jobService.CancelStorageJobAsync(jobId);
                return Ok(new { message = "Storage job cancelled successfully", jobId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = $"Job '{jobId}' not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to cancel storage job: {ex.Message}" });
            }
        }

        /// <summary>Usuwa zakończone zadanie związane z pamięcią masową.</summary>
        [HttpDelete("storage/{jobId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [SwaggerOperation(Summary = "Delete Storage Job", Description = "Deletes a completed storage job.")]
        public async Task<IActionResult> DeleteStorageJob([FromRoute] string jobId)
        {
            try
            {
                await _jobService.DeleteStorageJobAsync(jobId);
                return Ok(new { message = "Storage job deleted successfully", jobId });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { error = $"Job '{jobId}' not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to delete storage job: {ex.Message}" });
            }
        }
    }
}