using System;

namespace HyperV.Contracts.Services
{
    public interface IReplicationService
    {
        string CreateReplicationRelationship(string sourceVm, string targetHost, string authMode = "Certificate");
        void StartReplication(string vmName);
        string InitiateFailover(string vmName, string mode = "Planned");
        void ReverseReplicationRelationship(string vmName);
        string GetReplicationState(string vmName);
        void AddAuthorizationEntry(string relationshipId, string entry);
    }
}