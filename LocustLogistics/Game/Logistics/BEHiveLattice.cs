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
using Vintagestory.GameContent;

namespace LocustHives.Game.Logistics
{
    // Making this a BlockEntity rather than a behavior in order to reuse BlockEntityContainer. I hate inheritance... >:(
    public class BEHiveLattice : BlockEntityContainer, IBlockHiveStorage, IStorageLattice
    {
        InventoryBase inventory;
        HashSet<LogisticsReservation> reservations;
        LogisticsSystem logisticsSystem;
        TuningSystem tuningSystem;

        IHiveMember member;


        /// <summary>
        /// For lattices, this returns a LatticeStorageGroup instance
        /// that represents all connected lattices as one logical storage unit.
        /// </summary>
        public ILogisticsStorage LogisticsStorage {
            get
            {
                var positions = new[] { this }.Concat(TraverseConnected());
                return new LatticeStorage(positions);;
            }
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


            if (api is ICoreServerAPI)
            {
                logisticsSystem = api.ModLoader.GetModSystem<LogisticsSystem>();
                tuningSystem = api.ModLoader.GetModSystem<TuningSystem>();
                reservations = new HashSet<LogisticsReservation>();
                member = this.GetAs<IHiveMember>();
            
                var tunableBehavior = GetBehavior<IHiveMember>();
                if (tunableBehavior != null)
                {
                    tunableBehavior.OnTuned += (prevHive, hive) =>
                    {
                        // Look at all nearby groupings and deregister old groups and register new ones.
                        var combinedPosToUpdate = new HashSet<BEHiveLattice>{ this };
                        foreach(var group in FindSurroundingGroups()) // For some reason it's not seeing other lattice????
                        {
                            var otherMember = group.FirstOrDefault()?.GetAs<IHiveMember>();
                            if(otherMember != null &&
                            tuningSystem.Membership.GetMembershipOf(otherMember, out var otherHiveId))
                            {
                                if(otherHiveId == hive)
                                {
                                    // If nearby matches the new ID, deregister it's old group and
                                    // accumulate it's positions to be registered.
                                    var arr = group.ToArray();
                                    logisticsSystem.UpdateLogisticsStorageMembership(new LatticeStorage(arr), null);
                                    combinedPosToUpdate.AddRange(arr);
                                }
                                else if(otherHiveId == prevHive)
                                {
                                    // If it matches the previous id, then it used to be apart of this group
                                    // and it should be deregistered.
                                    var arr = group.Append(this);
                                    logisticsSystem.UpdateLogisticsStorageMembership(new LatticeStorage(arr), null);
                                }
                                // Do nothing if it is neither the new or previous hive
                            }
                        }

                        // Finally, update the combined groupings.
                        logisticsSystem.UpdateLogisticsStorageMembership(new LatticeStorage(combinedPosToUpdate), hive);
                    };
                }
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

        /// <summary>
        /// BFS on connected lattices.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BEHiveLattice> TraverseConnected(HashSet<BlockPos> visited = null)
        {
            if(member == null || !tuningSystem.Membership.GetMembershipOf(member, out int hiveId)) yield break;

            if(visited == null) visited = new HashSet<BlockPos>();
            visited.Add(Pos);
            var queue = new Queue<BEHiveLattice>();

            // BFS traversal
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if(current != this) yield return current;

                foreach (var face in BlockFacing.ALLFACES)
                {
                    var be = Api.World.BlockAccessor.GetBlockEntity<BEHiveLattice>(current.Pos.AddCopy(face));
                    var otherMember = be?.GetAs<IHiveMember>();
                    if(otherMember != null &&
                        tuningSystem.Membership.GetMembershipOf(otherMember, out var otherHiveId) &&
                        otherHiveId == hiveId &&
                        visited.Add(be.Pos)){
                        queue.Enqueue(be);
                    }
                }
            }
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            if (inventory == null) InitInventory();
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            // First, release all reservations
            foreach(var r in reservations)
            {
                r.Release();
            }

            // Then, unregister the old group
            var old = LogisticsStorage;
            if(!logisticsSystem.StorageMembership.GetMembershipOf(old, out var oldMembership)) return;
            logisticsSystem.UpdateLogisticsStorageMembership(old, null);

            // Then, find all groups surrounding this position.
            foreach(var grouping in FindSurroundingGroups())
            {
                logisticsSystem.UpdateLogisticsStorageMembership(new LatticeStorage(grouping), oldMembership); 
            }
        }

        /// <summary>
        /// Get's all groupings surrounding this lattics. As if the lattice is not there.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<BEHiveLattice[]> FindSurroundingGroups()
        {
            var visited = new HashSet<BlockPos>{ Pos };
            foreach(var face in BlockFacing.ALLFACES)
            {
                var pos = Pos.AddCopy(face);

                // Skip positions that we've already captured in a group.
                if(visited.Contains(pos)) continue;

                var be = Api.World.BlockAccessor.GetBlockEntity<BEHiveLattice>(pos);

                var grouping = be?.TraverseConnected(visited).ToArray();
                if(grouping != null && grouping.Count() > 0)
                {
                    // TODO: Should we not use old membership?
                    yield return grouping;
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
