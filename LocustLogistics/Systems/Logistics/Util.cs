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
        /// <summary>
        /// Returns how much room this inventory has for the given stack.
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
        public static uint CanAccept(this IInventory inventory, ItemStack stack)
        {
            return (uint)inventory.Sum(slot =>
            {
                var s = slot.Itemstack;
                if (s == null) return stack.Collectible.MaxStackSize;
                // TODO: Should this be stack.Satisfies(s)?
                else return s.Satisfies(stack) ? Math.Max(0, stack.Collectible.MaxStackSize - s.StackSize) : 0;
            });
        }

        /// <summary>
        /// Returns a count of all items matching the stack.
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
        public static uint CanProvide(this IInventory inventory, ItemStack stack)
        {
            return (uint)inventory.Sum(slot =>
            {
                var s = slot.Itemstack;
                if (s == null || !s.Satisfies(stack)) return 0;
                else return s.StackSize;
            });
        }


        public static ItemStack CloneWithSize(this ItemStack stack, int size)
        {
            var clone = stack.GetEmptyClone();
            clone.StackSize = size;
            return clone;
        }
    }
}
