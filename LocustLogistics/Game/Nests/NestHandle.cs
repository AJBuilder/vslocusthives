using LocustHives.Game.Core;
using LocustHives.Game.Nest;
using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Nests
{
    /// <summary>
    /// Handle for nest blocks.
    /// Identity based on block position.
    /// </summary>
    public struct NestHandle : IHiveMember, ILocustNest
    {
        private readonly BlockPos position;
        private readonly ICoreAPI api;

        public NestHandle(BlockPos position, ICoreAPI api = null)
        {
            this.position = position?.Copy();
            this.api = api;
        }

        // IHiveMember implementation
        public bool IsValid(ICoreAPI api)
        {
            if (position == null) return false;
            var be = api.World.BlockAccessor.GetBlockEntity(position);
            return be?.GetBehavior<BEBehaviorHiveLocustNest>() != null;
        }

        public static byte[] ToBytes(NestHandle handle)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write position
                writer.Write(handle.position.X);
                writer.Write(handle.position.Y);
                writer.Write(handle.position.Z);
                writer.Write(handle.position.dimension);

                return ms.ToArray();
            }
        }

        public static NestHandle FromBytes(byte[] data, ICoreAPI api)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Read position
                var x = reader.ReadInt32();
                var y = reader.ReadInt32();
                var z = reader.ReadInt32();
                var dimension = reader.ReadInt32();

                var pos = new BlockPos(x, y, z, dimension);
                return new NestHandle(pos, api);
            }
        }

        // ILocustNest implementation
        public Vec3d Position
        {
            get
            {
                var behavior = GetBehavior();
                return behavior?.Position ?? position?.ToVec3d();
            }
        }

        public int Dimension
        {
            get
            {
                return position?.dimension ?? 0;
            }
        }

        private BEBehaviorHiveLocustNest GetBehavior()
        {
            if (api == null || position == null) return null;
            var be = api.World.BlockAccessor.GetBlockEntity(position);
            return be?.GetBehavior<BEBehaviorHiveLocustNest>();
        }

        public override bool Equals(object obj)
        {
            return obj is NestHandle handle &&
                   position != null && handle.position != null &&
                   position.Equals(handle.position);
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }
    }
}
