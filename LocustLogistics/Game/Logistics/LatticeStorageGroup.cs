using LocustHives.Game.Logistics;
using LocustHives.Systems.Logistics;
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
    /// <summary>
    /// Represents a shared storage instance for a group of connected lattice blocks.
    /// Multiple IStorageLattice instances that are physically connected will share
    /// the same LatticeStorageGroup instance, enabling reference equality comparison.
    /// </summary>
    public struct LatticeStorage : ILogisticsStorage
    {
        private readonly ImmutableHashSet<IStorageLattice> members;

        /// <summary>
        /// Gets the set of all lattices that belong to this storage.
        /// </summary>
        public ImmutableHashSet<IStorageLattice> Members => members;

        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                // Aggregate stacks from all lattices in the group
                foreach (var member in members)
                {
                    foreach(var stack in member.Stacks)
                    {
                        yield return stack;
                    }
                }
            }
        }

        public IEnumerable<IStorageAccessMethod> AccessMethods
        {
            get
            {
                // Aggregate access methods from all lattices in the group
                foreach (var member in members)
                {
                    foreach(var face in member.AvailableFaces)
                    {
                        yield return new BlockFaceAccessible(member.Pos, face, 0, CanDo, TryTakeOut, TryPutInto, TryReserve);
                    }
                }
            }
        }

        private uint CanDo(ItemStack stack)
        {
            return (uint)members.Sum(m => m.CanDo(stack));
        }

        private uint TryTakeOut(ItemStack stack, ItemSlot sinkSlot)
        {
            var remaining = (uint)Math.Max(0, stack.StackSize);
            uint transferred = 0;

            foreach(var member in members)
            {
                if(remaining <= 0) break;
                var moved = member.TryTakeOut(stack.CloneWithSize((int)remaining), sinkSlot);
                if (moved > 0)
                {
                    transferred += moved;
                    remaining -= moved;
                }
            }
            return transferred;
        }

        private uint TryPutInto(ItemSlot sourceSlot, uint quantity)
        {
            var remaining = quantity;
            uint transferred = 0;

            foreach(var member in members)
            {
                if(remaining <= 0) break;

                var moved = member.TryPutInto(sourceSlot, remaining);
                if (moved > 0)
                {
                    transferred += moved;
                    remaining -= moved;
                }
            }
            return transferred;
        }

        LogisticsReservation TryReserve(ItemStack stack)
        {
            var remaining = Math.Max(0, stack.StackSize);
            var reservations = new List<LogisticsReservation>(members.Count);
            foreach(var member in members)
            {
                if(remaining <= 0) break;
                var reservation = member.TryReserve(stack.CloneWithSize(remaining));
                if(reservation != null)
                {
                    reservations.Add(reservation);
                    remaining -= reservation.Stack.StackSize;
                }
            }


            // If we weren't able to get them all, release them.
            if(remaining > 0)
            {
                foreach(var reservation in reservations)
                {
                    reservation.Release();
                }
                return null;
            }
            else
            {
                // otherwise make a parent reservation
                var reservation = new LogisticsReservation(stack);
                reservation.ReleasedEvent += () =>
                {
                    foreach(var r in reservations)
                    {
                        r.Release();
                    }
                };

                // and if the children cancel, cancel this
                foreach(var r in reservations)
                {
                    r.ReleasedEvent += () =>
                    {
                        reservation.Release();
                    };
                }
                return reservation;
            }
        }

        public LatticeStorage(IEnumerable<IStorageLattice> latticePositions)
        {
            this.members = latticePositions.ToImmutableHashSet();
        }
    }
}
