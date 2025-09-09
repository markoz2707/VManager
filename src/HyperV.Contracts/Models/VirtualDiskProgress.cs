namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Represents progress information for virtual disk operations
    /// </summary>
    public class VirtualDiskProgress
    {
        /// <summary>
        /// Current operation status (0 = success, non-zero = error)
        /// </summary>
        public uint OperationStatus { get; set; }

        /// <summary>
        /// Current progress value
        /// </summary>
        public ulong CurrentValue { get; set; }

        /// <summary>
        /// Total completion value (when CurrentValue equals this, operation is complete)
        /// </summary>
        public ulong CompletionValue { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double PercentComplete => CompletionValue > 0 ? (double)CurrentValue / CompletionValue * 100 : 0;

        /// <summary>
        /// Whether the operation is complete
        /// </summary>
        public bool IsComplete => CurrentValue >= CompletionValue;

        /// <summary>
        /// Whether the operation has an error
        /// </summary>
        public bool HasError => OperationStatus != 0;
    }
}
