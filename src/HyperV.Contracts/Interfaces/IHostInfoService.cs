using System.Collections.Generic;
using System.Threading.Tasks;
using HyperV.Contracts.Models;

namespace HyperV.Contracts.Interfaces
{
    public interface IHostInfoService
    {
        Task<HostHardwareInfo> GetHostHardwareInfoAsync();
        Task<SystemInfo> GetSystemInfoAsync();
        Task<PerformanceSummary> GetPerformanceSummaryAsync();
        Task<List<RecentTask>> GetRecentTasksAsync(int limit = 10);
    }
}