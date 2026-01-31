using LocustHives.Game.Core;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;

namespace LocustHives.Systems.Logistics
{
    /// <summary>
    /// Simple membership handle for entity-based logistics workers.
    /// Identity based on entity ID.
    /// </summary>
    public struct GenericEntityLogisticsWorker : IHiveMember, ILogisticsWorker
    {
        private readonly long entityId;
        private readonly ICoreAPI api;

        public GenericEntityLogisticsWorker(long entityId, ICoreAPI api = null)
        {
            this.entityId = entityId;
            this.api = api;
        }

        // IHiveMember implementation
        public bool IsValid(ICoreAPI api)
        {
            return GetWorker() != null;
        }


        public static byte[] ToBytes(GenericEntityLogisticsWorker handle)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write entity ID
                writer.Write(handle.entityId);

                return ms.ToArray();
            }
        }

        public static GenericEntityLogisticsWorker FromBytes(byte[] data, ICoreAPI api)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Read entity ID
                var entityId = reader.ReadInt64();

                return new GenericEntityLogisticsWorker(entityId, api);
            }
        }

        // ILogisticsWorker implementation
        public IInventory Inventory
        {
            get
            {
                var behavior = GetWorker();
                return behavior?.Inventory;
            }
        }

        public IEnumerable<WorkerEffort> GetEfforts(ItemStack stack, ILogisticsStorage storage)
        {
            var behavior = GetWorker();
            if (behavior == null) yield break;

            foreach (var effort in behavior.GetEfforts(stack, storage))
            {
                yield return effort;
            }
        }

        private ILogisticsWorker GetWorker()
        {
            return api.World.GetEntityById(entityId)?.GetAs<ILogisticsWorker>();
        }


        public override bool Equals(object obj)
        {
            return obj is GenericEntityLogisticsWorker handle &&
                   entityId == handle.entityId;
        }

        public override int GetHashCode()
        {
            return entityId.GetHashCode();
        }

    }
}
