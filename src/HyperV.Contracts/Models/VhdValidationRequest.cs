namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for VHD validation operations
    /// </summary>
    public class VhdValidationRequest
    {
        /// <summary>
        /// Path to the VHD file to validate
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether to perform deep validation (slower but more thorough)
        /// </summary>
        public bool DeepValidation { get; set; } = false;

        /// <summary>
        /// Whether to check parent chain for differencing disks
        /// </summary>
        public bool ValidateParentChain { get; set; } = true;

        /// <summary>
        /// Whether to validate persistent reservation support
        /// </summary>
        public bool ValidatePersistentReservation { get; set; } = false;
    }

    /// <summary>
    /// Response model for VHD validation results
    /// </summary>
    public class VhdValidationResponse
    {
        /// <summary>
        /// Path to the validated VHD file
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether the VHD is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of validation errors found
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// List of validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Whether parent chain is valid (for differencing disks)
        /// </summary>
        public bool? ParentChainValid { get; set; }

        /// <summary>
        /// Whether persistent reservation is supported
        /// </summary>
        public bool? PersistentReservationSupported { get; set; }

        /// <summary>
        /// Validation completion time
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }
}
