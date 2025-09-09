using Xunit;
using HyperV.Contracts.Models;

namespace HyperV.Tests;

public class ContractsTests
{
    [Fact]
    public void CreateVmRequest_HasDefaults()
    {
        var req = new CreateVmRequest { Id = "vm1", Name = "vm1" };
        Assert.Equal(2048, req.MemoryMB);
        Assert.Equal(2, req.CpuCount);
    }
}