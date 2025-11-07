using System;

namespace HyperV.Contracts.Services
{
    /// <summary>
    /// Interface for FibreChannel service operations.
    /// </summary>
    public interface IFibreChannelService
    {
        /// <summary>
        /// Creates a SAN pool.
        /// </summary>
        /// <param name="sanName">The name of the SAN pool.</param>
        /// <param name="wwpnArray">Array of WWPNs.</param>
        /// <param name="wwnnArray">Array of WWNNs.</param>
        /// <param name="notes">Optional notes.</param>
        /// <returns>JSON result of the operation.</returns>
        string CreateSan(string sanName, string[] wwpnArray, string[] wwnnArray, string notes);

        /// <summary>
        /// Deletes a SAN pool.
        /// </summary>
        /// <param name="poolId">The ID of the SAN pool.</param>
        void DeleteSan(Guid poolId);

        /// <summary>
        /// Gets information about a SAN pool.
        /// </summary>
        /// <param name="poolId">The ID of the SAN pool.</param>
        /// <returns>JSON result with SAN information.</returns>
        string GetSanInfo(Guid poolId);

        /// <summary>
        /// Creates a virtual FibreChannel port for a VM.
        /// </summary>
        /// <param name="vmName">The name of the VM.</param>
        /// <param name="sanPoolId">The ID of the SAN pool.</param>
        /// <param name="wwpn">WWPN for the port.</param>
        /// <param name="wwnn">WWNN for the port.</param>
        /// <returns>JSON result of the operation.</returns>
        string CreateVirtualFcPort(string vmName, string sanPoolId, string wwpn, string wwnn);
    }
}
