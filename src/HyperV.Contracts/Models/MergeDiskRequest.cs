namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for merging a differencing disk with its parent
    /// </summary>
    public class MergeDiskRequest
    {
        /// <summary>
        /// Path to the child (differencing) disk to merge
        /// </summary>
        public string ChildPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the destination disk to merge into
        /// </summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>
        /// Number of parent levels to merge (default: 1)
        /// </summary>
        public uint MergeDepth { get; set; } = 1;
    }
}
