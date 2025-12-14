using LocustLogistics.Logistics.Retrieval;
using LocustLogistics.Nests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LocustLogistics.Logistics.Push
{
    public class BlockHivePushBeacon : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.OnBlockInteractStart(world, byPlayer, blockSel);
            GetBlockEntity<BlockEntity>(blockSel).GetBehavior<BEBehaviorHivePushBeacon>()?.PushAll();
            return true;
        }
    }
}
