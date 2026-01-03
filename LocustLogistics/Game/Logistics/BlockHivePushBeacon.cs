using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LocustHives.Game.Logistics
{
    public class BlockHivePushBeacon : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.OnBlockInteractStart(world, byPlayer, blockSel);
            if (api is ICoreServerAPI coreServerAPI)
            {
                GetBlockEntity<BlockEntity>(blockSel).GetBehavior<BEBehaviorHivePushBeacon>()?.PushAll();
            }
            return true;
        }
    }
}
