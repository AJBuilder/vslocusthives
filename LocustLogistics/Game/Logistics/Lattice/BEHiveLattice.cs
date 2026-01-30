using LocustHives.Game.Core;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace LocustHives.Game.Logistics.Lattice
{
    // Making this a BlockEntity rather than a behavior in order to reuse BlockEntityContainer. I hate inheritance... >:(
    public class BEHiveLattice : BlockEntityContainer, IStorageLattice, IHiveTunable
    {
        InventoryBase inventory;
        HashSet<LogisticsReservation> reservations;
        LogisticsSystem logisticsSystem;
        TuningSystem tuningSystem;


        // IHiveTunable implementation
        public IHiveMember GetHiveMemberHandle()
        {
            return LatticeStorageGroup.FromConnected(this, Api);
        }


        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                if(inventory == null) yield break;
                foreach(var slot in Inventory)
                {
                    if(slot?.Itemstack != null) yield return slot.Itemstack;
                }
            }
        }

        public override InventoryBase Inventory => inventory;

        public override string InventoryClassName => "hivelattice";

        public IEnumerable<BlockFacing> AvailableFaces
        {
            get
            {
                foreach (var face in BlockFacing.ALLFACES)
                {
                    var accessPos = Pos.AddCopy(face);
                    if (Api.World.BlockAccessor.GetBlock(accessPos).Id == 0)
                    {
                        // Only faces that are air
                        yield return face;
                    }
                }
            }
        }

        BlockPos IStorageLattice.Pos => Pos;

        public override void Initialize(ICoreAPI api)
        {
            if(inventory == null) InitInventory();

            base.Initialize(api);

            tuningSystem = api.ModLoader.GetModSystem<TuningSystem>();
            if (api is ICoreServerAPI sapi)
            {
                logisticsSystem = sapi.ModLoader.GetModSystem<LogisticsSystem>();
                reservations = new HashSet<LogisticsReservation>();
            }
        }

        protected void InitInventory()
        {
            var quantitySlots = Block?.Attributes["quantitySlots"].AsInt() ?? 0;
            inventory = new InventoryGeneric(quantitySlots, null, null);
            // TODO: Inventory lock and weight stuff
            inventory.SlotModified += (int obj) => {
                MarkDirty(false);
            };
        }

        public LogisticsReservation TryReserve(ItemStack stack)
        {
            var available = CanDo(stack);
            if (available >= (uint)Math.Abs(stack.StackSize))
            {
                var reservation = new LogisticsReservation(stack);
                reservations.Add(reservation);
                reservation.ReleasedEvent += () =>
                {
                    reservations.Remove(reservation);
                };
                return reservation;
            }
            return null;
        }

        public uint CanDo(ItemStack stack)
        {
            var inventory = Inventory;
            bool isTake = stack.StackSize < 0;
            var reserved = (uint)reservations
                .Where(r => r.Stack.Satisfies(stack) && (r.Stack.StackSize < 0) == isTake)
                .Sum(r => Math.Abs(r.Stack.StackSize));
            return Math.Max(0, inventory.CanDo(stack) - reserved);
        }

        public uint TryTakeOut(ItemStack stack, ItemSlot sinkSlot)
        {
            // This method doesn't acutally transfer at the one it is closest too!
            uint quantity = (uint)Math.Abs(stack.StackSize);
            return inventory.TryTakeMatching(Api.World, stack, sinkSlot, quantity);
        }

        public uint TryPutInto(ItemSlot sourceSlot, uint quantity)
        {
            return inventory.TryPutIntoBestSlots(Api.World, sourceSlot, quantity);
        }

        

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (inventory == null) InitInventory();
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            // Release all reservations
            foreach(var r in reservations)
            {
                r.Release();
            }

            var handle = GetHiveMemberHandle();
            if(tuningSystem.GetMembershipOf(handle, out var hiveId))
            {

                // Detune from hive
                tuningSystem.Tune(handle, null);

                // And retune all the neighbors
                // TODO: Skip a neighbor if it already got updated.
                foreach(var face in BlockFacing.ALLFACES)
                {
                    var lattice = Api?.World.BlockAccessor.GetBlockEntity(Pos.AddCopy(face))?.GetAs<IStorageLattice>();
                    if(lattice != null)
                    {
                        var group = LatticeStorageGroup.FromConnected(lattice, Api);
                        tuningSystem.Tune(group, hiveId);
                    }
                }
                
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (!inventory.Empty)
            {
                dsc.AppendLine($"Contains: \n{string.Join("\n", inventory
                    .Where(s => !s.Empty)
                    .Select(s => $"{s.Itemstack.StackSize}x {s.Itemstack.GetName()}"))}");
            }
            else
            {
                dsc.AppendLine(Lang.Get("Empty"));
            }
        }

    }
}
