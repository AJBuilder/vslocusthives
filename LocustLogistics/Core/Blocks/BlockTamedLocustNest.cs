using LocustLogistics.Core.BlockEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace LocustLogistics.Core.Blocks
{
    public class BlockTamedLocustNest : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            base.OnBlockInteractStart(world, byPlayer, blockSel);
            GetBlockEntity<BETamedLocustNest>(blockSel)?.TryUnstoreLocust(0);
            return true;
        }
    }
}
