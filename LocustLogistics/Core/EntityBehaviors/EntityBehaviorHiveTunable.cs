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
        int? hiveId;
        AutomataLocustsCore modSystem;

        public Vec3d Position => entity.Pos.XYZ;

        public int Dimension => entity.Pos.Dimension;

        public EntityBehaviorHiveTunable(Entity entity) : base(entity)
        {
            modSystem = entity.Api.ModLoader.GetModSystem<AutomataLocustsCore>();
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            modSystem.Tune(hiveId, this);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
             modSystem.Tune(null, this);

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
            // If modSystem not set yet, then this is on-load. We'll do it later in Initialize.
            if ((id.HasValue != hiveId.HasValue) ||
                ((id.HasValue && hiveId.HasValue) && id != hiveId)) modSystem?.Tune(id, this);

            // hiveId is already set in OnTuned. Eh.
            // This way we don't need a second variable just
            // for getting this id to Initialize.
            hiveId = id;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            infotext.AppendLine($"Hive: {(hiveId == null ? "None" : hiveId)}");
        }
        public override string PropertyName()
        {
            return "hiveworker";
        }
        public void OnTuned(int? hive) => hiveId = hive;

    }
}
