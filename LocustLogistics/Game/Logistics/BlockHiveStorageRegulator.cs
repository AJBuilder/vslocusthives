using LocustHives.Game.Logistics.Lattice;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core.Interfaces;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Game.Logistics
{
    public class BlockHiveStorageRegulator : Block
    {
        const int ITEM_BOX = 2;
        const int INCREMENT_BOX = 3;
        const int DECREMENT_BOX = 4;

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.Side != EnumAppSide.Server) return true;

            var be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            var gauge = be?.GetBehavior<IStorageRegulator>();
            if (gauge == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            switch (blockSel.SelectionBoxIndex)
            {
                case ITEM_BOX:
                    var heldStack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;
                    if (heldStack != null)
                    {
                        gauge.TrackedItem = heldStack.CloneWithSize((int)gauge.CurrentLevel);
                    }
                    else
                    {
                        gauge.TrackedItem = null;
                    }
                    return true;
                case INCREMENT_BOX:
                    {
                        var stack = gauge.TrackedItem = gauge.TrackedItem;
                        stack.StackSize++;
                        gauge.TrackedItem = stack;
                    }
                    return true;

                case DECREMENT_BOX:
                    {
                        var stack = gauge.TrackedItem = gauge.TrackedItem;
                        if (stack.StackSize > 0)
                        {
                            stack.StackSize--;
                        }
                        gauge.TrackedItem = stack;
                    }
                    return true;

                default:
                    return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }
        }
    }
}
