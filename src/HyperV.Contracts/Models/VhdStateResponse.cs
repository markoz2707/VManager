namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Response model for VHD state information
    /// </summary>
    public class VhdStateResponse
    {
        /// <summary>
        /// Path to the VHD file
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether the VHD is currently attached
        /// </summary>
        public bool IsAttached { get; set; }

        /// <summary>
        /// Physical path when attached (drive letter or device path)
        /// </summary>
        public string? PhysicalPath { get; set; }

        /// <summary>
        /// Current operational state
        /// </summary>
        public string OperationalState { get; set; } = "Unknown";

        /// <summary>
        /// Health status of the VHD
        /// </summary>
        public string HealthStatus { get; set; } = "Unknown";

        /// <summary>
        /// Whether the VHD is read-only
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Current access mode
        /// </summary>
        public string AccessMode { get; set; } = "Unknown";
    }
}
