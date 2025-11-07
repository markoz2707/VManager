using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using System.Collections.Concurrent;

namespace HyperV.Agent.Services
{
    /// <summary>
    /// Temporary in-memory implementation of IJobService. In a real scenario this would
    /// query underlying WMI / VHD APIs or a persistent store for long-running storage jobs.
    /// </summary>
    public class InMemoryJobService : IJobService
    {
        private readonly ConcurrentDictionary<string, StorageJobResponse> _jobs = new();
        private readonly ConcurrentDictionary<string, List<AffectedElementResponse>> _jobAffected = new();

        public Task<List<StorageJobResponse>> GetStorageJobsAsync()
        {
            // Simulate some demo jobs if empty
            if (_jobs.IsEmpty)
            {
                var demo = new StorageJobResponse
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    Name = "Demo VHD Compact",
                    Description = "Compacting virtual hard disk",
                    State = StorageJobState.Running,
                    PercentComplete = 42,
                    StartTime = DateTime.UtcNow.AddMinutes(-2),
                    OperationType = "CompactVhd"
                };
                _jobs[demo.JobId] = demo;
                _jobAffected[demo.JobId] = new List<AffectedElementResponse>
                {
                    new AffectedElementResponse
                    {
                        ElementId = "disk-1",
                        ElementName = "Primary virtual disk",
                        ElementType = "VHD",
                        Effects = new List<ElementEffect>
                        {
                            new ElementEffect { Type = ElementEffectType.PerformanceImpact, Description = "Compaction in progress" }
                        }
                    }
                };
            }
            return Task.FromResult(_jobs.Values.OrderBy(j => j.StartTime).ToList());
        }

        public Task<StorageJobResponse> GetStorageJobAsync(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                throw new InvalidOperationException($"Job '{jobId}' not found");
            return Task.FromResult(job);
        }

        public Task<List<AffectedElementResponse>> GetJobAffectedElementsAsync(string jobId)
        {
            if (!_jobAffected.TryGetValue(jobId, out var list))
                throw new InvalidOperationException($"Job '{jobId}' not found");
            return Task.FromResult(list);
        }

        public Task CancelStorageJobAsync(string jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                throw new InvalidOperationException($"Job '{jobId}' not found");
            if (job.State is StorageJobState.Running or StorageJobState.Starting)
            {
                job.State = StorageJobState.Terminated;
                job.EndTime = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task DeleteStorageJobAsync(string jobId)
        {
            if (!_jobs.ContainsKey(jobId))
                throw new InvalidOperationException($"Job '{jobId}' not found");
            _jobs.TryRemove(jobId, out _);
            _jobAffected.TryRemove(jobId, out _);
            return Task.CompletedTask;
        }
    }
}
