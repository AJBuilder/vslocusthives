using LocustHives.Game.Core;
using LocustHives.Game.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LocustHives.Systems.Logistics
{
    /// <summary>
    /// Simple membership handle for block-based logistics storage.
    /// Identity based on block position.
    /// </summary>
    public struct GenericBlockEntityLogisticsStorage : IHiveMember, ILogisticsStorage
    {
        private readonly BlockPos position;
        private readonly ICoreAPI api;

        public GenericBlockEntityLogisticsStorage(BlockPos position, ICoreAPI api = null)
        {
            this.position = position?.Copy();
            this.api = api;
        }

        // IHiveMember implementation
        public bool IsValid(ICoreAPI api)
        {
            return GetStorage() != null;
        }

        public static byte[] ToBytes(GenericBlockEntityLogisticsStorage handle)
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

        public static GenericBlockEntityLogisticsStorage FromBytes(byte[] data, ICoreAPI api)
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
                return new GenericBlockEntityLogisticsStorage(pos, api);
            }
        }

        // ILogisticsStorage implementation - direct, no resolve
        public IEnumerable<ItemStack> Stacks
        {
            get
            {
                var behavior = GetStorage();
                if (behavior == null) yield break;

                foreach (var stack in behavior.Stacks)
                {
                    yield return stack;
                }
            }
        }

        public IEnumerable<IStorageAccessMethod> AccessMethods
        {
            get
            {
                var behavior = GetStorage();
                if (behavior == null) yield break;

                foreach (var method in behavior.AccessMethods)
                {
                    yield return method;
                }
            }
        }

        private ILogisticsStorage GetStorage()
        {
            return api.World.BlockAccessor.GetBlockEntity(position)?.GetBehavior<ILogisticsStorage>();
        }

        public override bool Equals(object obj)
        {
            return obj is GenericBlockEntityLogisticsStorage handle &&
                   position != null && handle.position != null &&
                   position.Equals(handle.position);
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }
    }
}
