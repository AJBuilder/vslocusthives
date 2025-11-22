using LocustLogistics.Core;
using LocustLogistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LocustLogistics.Core.BlockEntities
{
    public class BETamedLocustNest : BlockEntity, ILocustNest
    {
        int? hiveId;
        AutomataLocustsCore modSystem;
        HashSet<EntityLocust> locusts;
        public BETamedLocustNest()
        {
            locusts = new HashSet<EntityLocust>();
        }

        public IReadOnlyCollection<EntityLocust> StoredLocusts => locusts;
        public int MaxCapacity => 5;
        public bool HasRoom => locusts.Count < MaxCapacity;

        public Vec3d Position => Pos.ToVec3d();

        public int Dimension => Pos.dimension;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            modSystem = api.ModLoader.GetModSystem<AutomataLocustsCore>();
            if (!hiveId.HasValue) hiveId = modSystem.CreateHive();
            modSystem.Tune(hiveId, this);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modSystem.Tune(null, this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            modSystem.Tune(null, this);
        }

        public bool TryStoreLocust(EntityLocust locust)
        {
            if (locusts.Count >= MaxCapacity) return false;
            if (!locusts.Add(locust)) return false; // Already stored

            // We co-opt the reason "PickedUp" because all the others happen
            // when the entity should "no longer exist". "PickedUp" implies that
            // this despawned but still exists, just not in the world.
            // i.e. picked up and stored in a nest.
            locust.Die(EnumDespawnReason.PickedUp, null);
            MarkDirty(true);
            return true;
        }

        public bool TryUnstoreLocust(EntityLocust locust)
        {
            if (!locusts.Remove(locust)) return false; // Not in storage

            locust.ServerPos.SetPos(Pos.ToVec3d().Add(0.5, 0.1, 0.5));
            locust.Pos.SetFrom(locust.ServerPos);
            Api.World.SpawnEntity(locust);
            MarkDirty(true);
            return true;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (hiveId.HasValue) tree.SetInt("hiveId", hiveId.Value);

            int i = 0;
            foreach (var locust in locusts)
            {
                byte[] data = SerializerUtil.ToBytes((writer) => locust.ToBytes(writer, false));
                tree.SetBytes($"locust_{i}_data", data);
                tree.SetString($"locust_{i}_code", locust.Code.ToString());
                i++;
            }
            tree.SetInt("locustCount", i);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var id = tree.TryGetInt("hiveId");
            // If modSystem not set yet, then this is on-load. We'll do it later in Initialize.
            if ((id.HasValue != hiveId.HasValue) ||
                ((id.HasValue && hiveId.HasValue) && id != hiveId)) modSystem?.Tune(id, this);

            // hiveId will get set again in OnTuned. Eh.
            // This way we don't need a second variable just
            // for getting this id to Initialize.
            hiveId = id;

            locusts.Clear();
            int count = tree.GetInt("locustCount");
            for (int i = 0; i < count; i++)
            {
                string code = tree.GetString($"locust_{i}_code");
                byte[] data = tree.GetBytes($"locust_{i}_data");

                if (string.IsNullOrEmpty(code) || data == null) continue;

                var etype = Api.World.GetEntityType(new AssetLocation(code));
                if (etype == null) continue;

                var locust = Api.World.ClassRegistry.CreateEntity(etype) as EntityLocust;
                if (locust == null) continue;

                SerializerUtil.FromBytes(data, (reader) => locust.FromBytes(reader, false));
                locusts.Add(locust); // Add to set but DON'T spawn - they're stored
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine($"Locusts: {locusts.Count}/{MaxCapacity}");
            dsc.AppendLine($"Hive: {(hiveId.HasValue ? hiveId.Value : "None")}");
        }

        public void OnTuned(int? hive) => hiveId = hive;

    }
}
