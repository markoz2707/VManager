using HyperV.Contracts.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HyperV.Contracts.Interfaces
{
    /// <summary>
    /// Interface for managing storage jobs
    /// </summary>
    public interface IJobService
    {
        /// <summary>
        /// Gets list of all storage jobs
        /// </summary>
        Task<List<StorageJobResponse>> GetStorageJobsAsync();

        /// <summary>
        /// Gets details of a specific storage job
        /// </summary>
        Task<StorageJobResponse> GetStorageJobAsync(string jobId);

        /// <summary>
        /// Gets elements affected by a storage job
        /// </summary>
        Task<List<AffectedElementResponse>> GetJobAffectedElementsAsync(string jobId);

        /// <summary>
        /// Cancels a storage job
        /// </summary>
        Task CancelStorageJobAsync(string jobId);

        /// <summary>
        /// Deletes a completed job
        /// </summary>
        Task DeleteStorageJobAsync(string jobId);
    }
}