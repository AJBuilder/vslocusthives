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

    public readonly struct BlockFaceAccessible : IInWorldStorageAccessMethod
    {
        readonly System.Func<ItemStack, LogisticsOperation, uint> onCanDo;
        public BlockPos BlockPosition { get; }
        public BlockFacing Face { get; }

        public int Priority { get; }

        /// <summary>
        /// Returns the center of the face.
        /// </summary>
        public Vec3d Position
        {
            get
            {
                return Face.Index switch
                {
                    BlockFacing.indexUP => BlockPosition.ToVec3d().AddCopy(-0.5f, 0, -0.5f),
                    BlockFacing.indexDOWN => BlockPosition.ToVec3d().AddCopy(-0.5f, -1f, -0.5f),
                    BlockFacing.indexNORTH => BlockPosition.ToVec3d().AddCopy(-0.5f, -0.5f, -1f),
                    BlockFacing.indexSOUTH => BlockPosition.ToVec3d().AddCopy(-0.5f, -0.5f, 0),
                    BlockFacing.indexEAST => BlockPosition.ToVec3d().AddCopy(0f, -0.5f, -0.5f),
                    BlockFacing.indexWEST => BlockPosition.ToVec3d().AddCopy(-1f, -0.5f, -0.5f),
                };
            }
        }

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
