namespace HyperV.Contracts.Models
{
    public class ResizeVhdRequest
    {
        public string? Path { get; set; }
        public ulong MaxInternalSize { get; set; }
    }
}
