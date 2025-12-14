using LocustLogistics.Core;
using LocustLogistics.Logistics.Retrieval;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LocustLogistics.Logistics
{
    public class EntityBehaviorLogisticsWorker : EntityBehavior, ILogisticsWorker
    {
        public IInventory Inventory { get; }
        public Vec3d Position => entity.Pos.XYZ;

        public EntityBehaviorLogisticsWorker(Entity entity) : base(entity)
        {
            Inventory = new InventoryGeneric(1, $"logisticsworker-{entity.GetName()}-{entity.EntityId}", entity.Api);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            entity.GetBehavior<EntityBehaviorHiveTunable>().OnTuned += (int? prevHive, int? hive) =>
            {
                entity.Api.ModLoader.GetModSystem<LogisticsWorkersModSystem>().UpdateLogisticsWorkerHiveMembership(this, prevHive, hive);
            };
        }

        public override void FromBytes(bool isSync)
        {
            base.FromBytes(isSync);
        }

        public override void ToBytes(bool forClient)
        {
            base.ToBytes(forClient);
        }

        public override string PropertyName()
        {
            return "logisticsworker";
        }

        public bool TryAssignPushRequest(PushRequest request)
        {
            // NOTE: This logic assumes empty == can't take more items. Ok for now, technically incorrect.
            // If we want to support merging stacks or having more than one slot, will have to change.
            if (!Inventory.Empty) return false;
            return true;
        }

    } 
}
