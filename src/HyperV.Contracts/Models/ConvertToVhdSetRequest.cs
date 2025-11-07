using System;

namespace HyperV.Contracts.Models
{
    public class ConvertToVhdSetRequest
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public uint? BlockSize { get; set; }
        public uint? LogicalSectorSize { get; set; }
        public uint? PhysicalSectorSize { get; set; }
    }
}
