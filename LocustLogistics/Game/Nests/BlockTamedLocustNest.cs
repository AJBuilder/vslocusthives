using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace LocustHives.Game.Nest
{
    public class BlockTamedLocustNest : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //var success = GetBlockEntity<BlockEntity>(blockSel).GetBehavior<ILocustNest>()?.TryUnstoreLocust(0);
            //if (success.HasValue && !success.Value && api is ICoreClientAPI capi) capi.TriggerIngameError(this, "Failed to unstore locust", "No stored Locusts");
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
