using LocustHives.Systems.Logistics.Core;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Logistics.Core.Interfaces
{

    /// <summary>
    /// A logistics storage can have logistics operations performed on it.
    /// </summary>
    public interface ILogisticsStorage
    {
        /// <summary>
        /// The inventory of this storage. NOT to be used when checking for when an operation can be done.
        /// Use the access methods as they should account for reserved stacks.
        /// </summary>
        IInventory Inventory { get; }

        IEnumerable<IStorageAccessMethod> AccessMethods { get; }

        /// <summary>
        /// Reserve the stack/room for the given stack from being considered when it's access methods are queried.
        /// 
        /// Should return null if unable.
        /// </summary>
        /// <param name="stack"></param>
        /// <returns></returns>
        LogisticsReservation TryReserve(ItemStack stack, LogisticsOperation operation);

    }
}
