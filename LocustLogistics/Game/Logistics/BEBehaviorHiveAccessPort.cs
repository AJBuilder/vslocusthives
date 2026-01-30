using LocustHives.Game.Core;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace LocustHives.Game.Logistics
{



    public class BEBehaviorHiveAccessPort : BlockEntityBehavior, ILogisticsStorage, IHiveTunable
    {
        // Faces towards the inventory. Access opening is opposite of facing.
        BlockFacing facing;
        HashSet<LogisticsReservation> reservations;
        TuningSystem tuningSystem;

        /// <summary>
        /// For single-block storage like access ports, this simply returns itself.
        /// </summary>
        public ILogisticsStorage LogisticsStorage => this;

        public IInventory Inventory
        {
            get
            {
                if (facing == null) return null;
                BlockPos targetPos = Pos.AddCopy(facing);
                return (Api.World.BlockAccessor.GetBlockEntity(targetPos) as IBlockEntityContainer)?.Inventory;
            }
        }

        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                var inv = Inventory;
                if (inv == null) yield break;
                foreach (var slot in inv)
                {
                    if (slot?.Itemstack != null)
                        yield return slot.Itemstack;
                }
            }
        }

        public IEnumerable<IStorageAccessMethod> AccessMethods
        {
            get
            {
                yield return new BlockFaceAccessible(Blockentity.Pos, facing.Opposite, 0, CanDo, TryTakeOut, TryPutInto, TryReserve);
            }
        }

        public IEnumerable<LogisticsReservation> Reservations => reservations;

        public BEBehaviorHiveAccessPort(BlockEntity blockentity) : base(blockentity)
        {
        }

        // IHiveTunable implementation
        public IHiveMember GetHiveMemberHandle()
        {
            return new GenericBlockEntityLogisticsStorage(Blockentity.Pos, Api);
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            var facingCode = properties["facingCode"].AsString();
            facing = BlockFacing.FromCode(Blockentity.Block.Variant[facingCode]);

            tuningSystem = api.ModLoader.GetModSystem<TuningSystem>();
            if (api is ICoreServerAPI sapi)
            {
                reservations = new HashSet<LogisticsReservation>();
            }
        }

        private LogisticsReservation TryReserve(ItemStack stack)
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

        private uint CanDo(ItemStack stack)
        {
            var inventory = Inventory;
            if (inventory == null) return 0;
            bool isTake = stack.StackSize < 0;
            var reserved = (uint)reservations
                .Where(r => r.Stack.Satisfies(stack) && (r.Stack.StackSize < 0) == isTake)
                .Sum(r => Math.Abs(r.Stack.StackSize));
            var able = inventory.CanDo(stack);
            return (uint)Math.Max(0, (int)able - (int)reserved);
        }

        private uint TryTakeOut(ItemStack stack, ItemSlot sinkSlot)
        {
            var inventory = Inventory;
            if (inventory == null) return 0;

            return inventory.TryTakeMatching(Api.World, stack, sinkSlot, (uint)Math.Abs(stack.StackSize));
        }

        private uint TryPutInto(ItemSlot sourceSlot, uint quantity)
        {
            var inventory = Inventory;
            if (inventory == null) return 0;

            return inventory.TryPutIntoBestSlots(Api.World, sourceSlot, quantity);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            // Detune from hive
            if (tuningSystem != null)
            {
                var handle = GetHiveMemberHandle();
                tuningSystem.Tune(handle, null);
            }

            CleanupLogistics();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            // Don't detune on unload - membership persists!
            // Just cleanup reservations
            CleanupLogistics();
        }

        public void CleanupLogistics()
        {
            reservations?.Foreach(r => r.Release());
        }
    }
}
