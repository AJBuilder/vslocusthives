using LocustHives.Game.Core;
using LocustHives.Game.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LocustHives.Systems.Logistics
{
    /// <summary>
    /// Simple membership handle for block members.
    /// Identity based on block position.
    /// </summary>
    public struct GenericBlockMembership : IHiveMember
    {
        private readonly BlockPos position;

        public GenericBlockMembership(BlockPos position)
        {
            this.position = position?.Copy();
        }

        // IHiveMember implementation
        public bool IsValid(ICoreAPI api)
        {
            return api.World.BlockAccessor.GetBlock(position).Id != 0;
        }

        public static byte[] ToBytes(GenericBlockMembership handle)
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

        public static GenericBlockMembership FromBytes(byte[] data, ICoreAPI api)
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
                return new GenericBlockMembership(pos);
            }
        }

        public bool Equals(IHiveMember other)
        {
            return other is GenericBlockMembership handle &&
                   position != null && handle.position != null &&
                   position.Equals(handle.position);
        }

        public override bool Equals(object obj)
        {
            return obj is GenericBlockMembership handle && Equals(handle);
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }
    }
}
