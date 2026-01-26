namespace LocustHives.Systems.Logistics.Core.Interfaces
{
    /// <summary>
    /// Represents a block that provides logistics storage capability.
    /// This interface wraps ILogisticsStorage to allow blocks to return
    /// either themselves (for single-block storage) or a shared instance
    /// (for connected structures like lattices).
    /// </summary>
    public interface IBlockHiveStorage
    {
        /// <summary>
        /// Gets the logical storage instance that this block provides.
        /// For single blocks, returns the block itself.
        /// For connected structures (like lattices), returns a shared instance
        /// representing all connected blocks as one storage unit.
        /// </summary>
        ILogisticsStorage LogisticsStorage { get; }
    }
}
