using System;
using System.Data.SqlTypes;
using Vintagestory.API.Common;

namespace LocustHives.Game.Core
{
    /// <summary>
    /// Core interface for hive membership.
    /// The implementation must have a stable identity throughout the loading/unloading
    /// of chunks and the world. (i.e block position, entity id, etc.)
    /// </summary>
    public interface IHiveMember : IEquatable<IHiveMember>
    {
        /// <summary>
        /// Checks if this handle still refers to a valid object in the world.
        /// Returns false if the membership has been removed or doesn't exist.
        /// Used for automatic cleanup of stale handles.
        /// 
        /// Do not rely on this method to cleanup. The implementation should handle
        /// the cleanup as soon as it becomes stale.
        /// </summary>
        bool IsValid(ICoreAPI api);
        
        /// <summary>
        /// Will be invoked when this member is tuned.
        ///
        /// First argument is the previous hive id, second is the new hive id.
        /// </summary>
        void OnTuned(int? prevHiveId, int? hiveId){}
    }
}
