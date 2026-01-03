using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Logistics.AccessMethods
{

    public readonly struct BlockFaceAccessible : IStorageAccessMethod
    {
        readonly System.Func<ItemStack, LogisticsOperation, uint> onCanDo;
        public BlockPos BlockPosition { get; }
        public BlockFacing Face { get; }

        public int Priority { get; }

        public BlockFaceAccessible(BlockPos pos, BlockFacing face, int priority, System.Func<ItemStack, LogisticsOperation, uint> onCanDo)
        {
            BlockPosition = pos;
            Face = face;
            Priority = priority;
            this.onCanDo = onCanDo;
        }

        public uint CanDo(ItemStack stack, LogisticsOperation operation)
        {
            return onCanDo(stack, operation);
        }

    };

}
