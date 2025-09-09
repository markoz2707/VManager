using HyperV.Contracts.Models;

namespace HyperV.Contracts.Interfaces
{
    public interface IStorageService
    {
        void CreateVirtualHardDisk(CreateVhdRequest request);
        void AttachVirtualHardDisk(string vmName, string vhdPath);
        void DetachVirtualHardDisk(string vmName, string vhdPath);
        void ResizeVirtualHardDisk(ResizeVhdRequest request);
    }
}
