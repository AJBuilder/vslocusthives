using LocustLogistics.Core.Interfaces;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LocustLogistics.Core.EntityBehaviors
{
    public class EntityBehaviorHiveTunable : EntityBehavior, IHiveMember
    {
        AutomataLocustsCore modSystem;
        public int? HiveId { get; set; }

        public Vec3d Position => entity.Pos.XYZ;

        public int Dimension => entity.Pos.Dimension;

        public EntityBehaviorHiveTunable(Entity entity) : base(entity)
        {
            modSystem = entity.Api.ModLoader.GetModSystem<AutomataLocustsCore>();
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (HiveId.HasValue)
            {
                modSystem.GetHive(HiveId.Value).Add(this);
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            // If this was picked up, don't remove from the hive.
            if (despawn.Reason == EnumDespawnReason.PickedUp) return;

            // If entity dies
            if (despawn.Reason == EnumDespawnReason.Death || despawn.Reason == EnumDespawnReason.Combusted)
            {
                if(HiveId.HasValue) modSystem.GetHive(HiveId.Value)?.Detune(this);
            }
            else
            {
                // Otherwise, is still part of the hive but just remove it
                if(HiveId.HasValue) modSystem.GetHive(HiveId.Value)?.Remove(this);
            }

        }


        public override void ToBytes(bool forClient)
        {
            if(HiveId.HasValue)
            {
                entity.WatchedAttributes.SetInt("hiveId", HiveId.Value);
            }
        }

        public override void FromBytes(bool isSync)
        {
            HiveId = entity.WatchedAttributes.TryGetInt("hiveId");
            if (HiveId.HasValue && modSystem != null) // If modSystem not set yet, then this is on load. We'll do it on spawn.
            {
                modSystem.GetHive(HiveId.Value).Add(this);
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            infotext.AppendLine($"Hive: {(HiveId == null ? "None" : HiveId)}");
        }
        public override string PropertyName()
        {
            return "hiveworker";
        }

    }
}
