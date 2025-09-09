namespace HyperV.Contracts.Models
{
    public class CreateVhdRequest
    {
        public string? Path { get; set; }
        public ulong MaxInternalSize { get; set; }
        public string? Format { get; set; } // VHD, VHDX
        public string? Type { get; set; } // Dynamic, Fixed
    }
}
