using LocustHives.Game.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;



namespace LocustHives.Game.Logistics
{
    public interface IStorageLattice
    {
        BlockPos Pos { get; }
        IEnumerable<ItemStack> Stacks { get; }
        IEnumerable<BlockFacing> AvailableFaces { get; }
        uint CanDo(ItemStack stack);

        uint TryTakeOut(ItemStack stack, ItemSlot sinkSlot);
        uint TryPutInto(ItemSlot sourceSlot, uint quantity);
        LogisticsReservation TryReserve(ItemStack stack);
    }

}
