using LocustHives.Systems.Logistics.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace LocustHives.Systems.Logistics.Core.Interfaces
{
    public interface ILogisticsNetwork
    {
        IReadOnlySet<ILogisticsWorker> Workers { get; }
        IReadOnlySet<ILogisticsStorage> Storages { get; }

        LogisticsPromise Push(ItemStack stack, ILogisticsStorage from);
        LogisticsPromise Pull(ItemStack stack, ILogisticsStorage into);
    }
}
