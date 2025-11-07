namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for creating a differencing disk
    /// </summary>
    public class DifferencingDiskRequest
    {
        /// <summary>
        /// Path where the child (differencing) disk will be created
        /// </summary>
        public string ChildPath { get; set; } = string.Empty;

        /// <summary>
        /// Path to the parent disk
        /// </summary>
        public string ParentPath { get; set; } = string.Empty;
    }
}
