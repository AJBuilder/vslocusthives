using LocustHives.Systems.Logistics.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Logistics.Core.Interfaces
{
    public interface IStorageAccessMethod
    {
        /// <summary>
        /// The priority of using this method over others for the same storage.
        /// </summary>
        int Priority { get; }

        uint CanDo(ItemStack stack, LogisticsOperation operation);

    }

    public interface IInWorldStorageAccessMethod : IStorageAccessMethod
    {
        Vec3d Position { get; }
    }
}
