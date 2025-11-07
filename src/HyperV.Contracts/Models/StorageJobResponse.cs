using System;
using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    public class StorageJobResponse
    {
        public string JobId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StorageJobState State { get; set; }
        public int PercentComplete { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public List<string> AffectedElements { get; set; } = new List<string>();
        public string OperationType { get; set; }
    }

    public enum StorageJobState
    {
        Unknown = 0,
        New = 2,
        Starting = 3,
        Running = 4,
        Suspended = 5,
        ShuttingDown = 6,
        Completed = 7,
        Terminated = 8,
        Killed = 9,
        Exception = 10,
        Service = 11
    }
}