using LocustLogistics.Logistics.Retrieval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable warnings

namespace LocustLogistics.Logistics
{
    public interface ILogisticsWorker
    {
        IInventory Inventory { get; }
        Vec3d Position { get; }

        /// <summary>
        /// If returns try, then the given request will be assigned to this worker.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public bool TryAssignPushRequest(PushRequest request);

    }
}
