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
        AutomataLocustsCore modSystem;
        HashSet<EntityLocust> locusts;
        public int? HiveId { get; set; }
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
            if (HiveId.HasValue)
            {
                modSystem.GetHive(HiveId.Value).Add(this);
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if(HiveId.HasValue) modSystem.GetHive(HiveId.Value)?.Detune(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if(HiveId.HasValue) modSystem.GetHive(HiveId.Value)?.Remove(this); // Is still part of the hive, just unloaded.
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
            if (HiveId.HasValue)
            {
                tree.SetInt("hiveId", HiveId.Value);
            }

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

            HiveId = tree.TryGetInt("hiveId");
            if (HiveId.HasValue && modSystem != null) // If modSystem not set yet, then this is on load. We'll do it in Initialize.
            {
                modSystem.GetHive(HiveId.Value).Add(this);
            }

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
            dsc.AppendLine($"Hive: {(HiveId.HasValue ? HiveId.Value : "None")}");
        }
    }
}
