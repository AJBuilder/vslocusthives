using LocustHives.Systems.Logistics.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable warnings

namespace LocustHives.Systems.Logistics.Core.Interfaces
{

    /// <summary>
    /// A logistics worker is an entity that can do logistics operations.
    /// 
    /// It does this by first providing all of it's "efforts", or ways it can fulfill a given operation.
    /// This effort can then be used to get a promise.
    /// 
    /// The worker is responsible for the fulfillment of promises returned by it's efforts.
    /// They can abandon it, partialy fill it, pass it off, or delegate it. As long
    /// as whatever action ensures it is eventually fulfilled and not dropped.
    /// </summary>
    public interface ILogisticsWorker
    {
        IInventory Inventory { get; }

        /// <summary>
        /// Get all the ways this worker could could perform the given operation.
        /// 
        /// The returned efforts do not need to be gaurenteed to work or be valid
        /// after they are returned. (i.e. between game ticks)
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="storage"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        IEnumerable<WorkerEffort> GetEfforts(ItemStack stack, ILogisticsStorage storage, LogisticsOperation operation);
    }
}
