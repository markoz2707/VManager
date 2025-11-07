using System;

namespace HyperV.Contracts.Services
{
    public interface IStorageQoSService
    {
        string CreateQoSPolicy(string policyId, uint maxIops, uint maxBandwidth, string description);
        void DeleteQoSPolicy(Guid policyId);
        string GetQoSPolicyInfo(Guid policyId);
        void ApplyQoSPolicyToVm(string vmName, Guid policyId);
    }
}
