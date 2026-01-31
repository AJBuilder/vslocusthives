



using System.IO;
using LocustHives.Game.Core;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;


namespace LocustHives.Game.Nexus
{
    public struct NexusMembership : IHiveMember
    {
        private readonly BlockPos position;

        public NexusMembership(BlockPos position)
        {
            this.position = position?.Copy();
        }

        public bool IsValid(ICoreAPI api)
        {
            return api.World.BlockAccessor.GetBlockEntity(position)?.GetAs<BEBehaviorHiveNexus>() != null;
        }

        public static byte[] ToBytes(NexusMembership handle)
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

        public static NexusMembership FromBytes(byte[] data)
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
                return new NexusMembership(pos);
            }
        }


        public override bool Equals(object obj)
        {
            return obj is NexusMembership handle &&
                position != null && handle.position != null &&
                position.Equals(handle.position);
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }
    }
}