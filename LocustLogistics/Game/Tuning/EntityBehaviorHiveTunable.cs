using LocustHives.Systems.Membership;
using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LocustHives.Game.Core
{
    public class EntityBehaviorHiveTunable : EntityBehavior, IHiveMember
    {

        public int? hiveId;
        public TuningSystem modSystem;
        public Action<int?, int?> OnTuned { get; set; }

        public EntityBehaviorHiveTunable(Entity entity) : base(entity)
        {

            OnTuned += (_, newId) =>
            {
                hiveId = newId;
            };

            modSystem = entity.Api.ModLoader.GetModSystem<TuningSystem>();
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);
            if (!hiveId.HasValue && entity.SidedProperties.Attributes.GetAsBool("createsHive")) hiveId = modSystem.CreateHive();
            modSystem?.Tune(this, hiveId);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            modSystem?.Tune(this, null);

        }

        public override void ToBytes(bool forClient)
        {
            if(hiveId.HasValue)
            {
                entity.WatchedAttributes.SetInt("hiveId", hiveId.Value);
            }
        }

        public override void FromBytes(bool isSync)
        {
            var id = entity.WatchedAttributes.TryGetInt("hiveId");
            // If modSystem not set yet, then this is on-load or the client. If on-load we'll do it later in Initialize.
            if(modSystem == null) hiveId = id;
            else if (id.HasValue != hiveId.HasValue ||
                id.HasValue && hiveId.HasValue && id != hiveId) modSystem?.Tune(this, id);
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            infotext.AppendLine($"Hive: {(hiveId == null ? "None" : hiveId)}");
        }
        public override string PropertyName()
        {
            return "hiveworker";
        }

    }
}
