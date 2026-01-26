using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Logistics
{
    public static class Util
    {

        public static uint CanProvide(this IInventory inventory, ItemStack stack)
        {
            return (uint)inventory.Sum(slot =>
            {
                var s = slot.Itemstack;
                if (s == null || !s.Satisfies(stack)) return 0;
                else return s.StackSize;
            });
        }

        public static uint CanAccept(this IInventory inventory, ItemStack stack)
        {
            return (uint)Math.Max(0, inventory.Sum(slot =>
            {
                var s = slot.Itemstack;
                if (s == null) return stack.Collectible.MaxStackSize;
                else return s.Satisfies(stack) ? Math.Max(0, stack.Collectible.MaxStackSize - s.StackSize) : 0;
            }));
        }

        /// <summary>
        /// Returns how much of the operation can be performed.
        /// Positive stack size = how much room for receiving (Give)
        /// Negative stack size = how much available to provide (Take)
        /// Will not be more in quantity than the stack size.
        /// </summary>
        public static uint CanDo(this IInventory inventory, ItemStack stack)
        {
            if (stack.StackSize > 0)
            {
                // Give: check room
                return Math.Min((uint)stack.StackSize, CanAccept(inventory, stack));
            }
            else if (stack.StackSize < 0)
            {
                // Take: check available
                return Math.Min((uint)stack.StackSize, CanProvide(inventory, stack));
            }
            return 0;
        }

        public static ItemStack CloneWithSize(this ItemStack stack, int size)
        {
            var clone = stack.GetEmptyClone();
            clone.StackSize = size;
            return clone;
        }

        public static BlockPos InBlockPos(this Vec3d pos)
        {
            return new BlockPos((int)Math.Floor(pos.X), (int)Math.Floor(pos.Y), (int)Math.Floor(pos.Z));
        }

        /// <summary>
        /// Attempts to take matching items from inventory slots and transfer them to a sink slot.
        /// </summary>
        /// <returns>The amount successfully transferred.</returns>
        public static uint TryTakeMatching(this IInventory inventory, IWorldAccessor world, ItemStack stack, ItemSlot sinkSlot, uint maxQuantity)
        {
            uint remaining = maxQuantity;
            uint transferred = 0;

            foreach (var slot in inventory)
            {
                if (remaining <= 0) break;

                if (slot.Itemstack?.Satisfies(stack) ?? false)
                {
                    int moved = slot.TryPutInto(world, sinkSlot, (int)remaining);
                    if (moved > 0)
                    {
                        transferred += (uint)moved;
                        remaining -= (uint)moved;
                    }
                }
            }

            return transferred;
        }

        /// <summary>
        /// Attempts to put items from a source slot into the best suited slots in the inventory.
        /// </summary>
        /// <returns>The amount successfully transferred.</returns>
        public static uint TryPutIntoBestSlots(this IInventory inventory, IWorldAccessor world, ItemSlot sourceSlot, uint maxQuantity)
        {
            uint remaining = maxQuantity;
            uint transferred = 0;

            while (remaining > 0 && !sourceSlot.Empty)
            {
                var bestSlot = inventory.GetBestSuitedSlot(sourceSlot);
                if (bestSlot.slot == null) break;

                int moved = sourceSlot.TryPutInto(world, bestSlot.slot, (int)remaining);
                if (moved <= 0) break;

                transferred += (uint)moved;
                remaining -= (uint)moved;
            }

            return transferred;
        }
    }
}
