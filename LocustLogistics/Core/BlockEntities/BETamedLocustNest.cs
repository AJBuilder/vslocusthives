using HarmonyLib;
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
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LocustLogistics.Core.BlockEntities
{
    public class BETamedLocustNest : BlockEntity, IHiveMember
    {
        int? hiveId;
        AutomataLocustsCore modSystem;
        List<(string code, byte[] data)> storedLocustData;

        public BETamedLocustNest()
        {
            storedLocustData = new List<(string code, byte[] data)>();
        }

        public IEnumerable<EntityLocust> StoredLocusts
        {
            get
            {
                if (Api == null) yield break;

                foreach (var (code, data) in storedLocustData) yield return CreateEntityClass(code, data);
            }
        }

        public int MaxCapacity => 5;
        public bool HasRoom => storedLocustData.Count < MaxCapacity;

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
            if (storedLocustData.Count >= MaxCapacity) return false;

            // Serialize the locust to raw data
            byte[] data = SerializerUtil.ToBytes((writer) => locust.ToBytes(writer, false));
            string code = locust.Code.ToString();

            storedLocustData.Add((code, data));

            // Despawn the locust from the world
            locust.Die(EnumDespawnReason.PickedUp, null);

            MarkDirty(true);
            return true;
        }

        public bool TryUnstoreLocust(int index)
        {
            if (index < 0 || index >= storedLocustData.Count) return false;

            // Get and remove the raw data entry
            var (code, data) = storedLocustData[index];
            storedLocustData.RemoveAt(index);

            // Create a fresh EntityLocust from the raw data
            var entity = CreateEntityClass(code, data);

            // Spawn the entity at the nest position
            entity.Pos.SetFrom(entity.ServerPos);
            entity.ServerPos.SetPosWithDimension(Pos.ToVec3d());
            Api.World.SpawnEntity(entity);

            entity.Attributes.SetLong("unstoredMs", Api.World.ElapsedMilliseconds);

            MarkDirty(true);
            return true;
        }

        private EntityLocust CreateEntityClass(string code, byte[] bytes)
        {
            var entityType = Api.World.GetEntityType(new AssetLocation(code));
            var entity = Api.World.ClassRegistry.CreateEntity(entityType) as EntityLocust;
            SerializerUtil.FromBytes(bytes, (reader) => entity.FromBytes(reader, false));
            return entity;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (hiveId.HasValue) tree.SetInt("hiveId", hiveId.Value);

            for (int i = 0; i < storedLocustData.Count; i++)
            {
                var (code, data) = storedLocustData[i];
                tree.SetBytes($"locust_{i}_data", data);
                tree.SetString($"locust_{i}_code", code);
            }
            tree.SetInt("locustCount", storedLocustData.Count);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var id = tree.TryGetInt("hiveId");
            // If modSystem not set yet, then this is on-load. We'll do it later in Initialize.
            if ((id.HasValue != hiveId.HasValue) ||
                ((id.HasValue && hiveId.HasValue) && id != hiveId)) modSystem?.Tune(id, this);

            // hiveId is already set in OnTuned. Eh.
            // This way we don't need a second variable just
            // for getting this id to Initialize.
            hiveId = id;

            int count = tree.GetInt("locustCount");

            storedLocustData = Enumerable.Range(0, count)
                .Select(i => (
                    code: tree.GetString($"locust_{i}_code"),
                    data: tree.GetBytes($"locust_{i}_data")
                ))
                .Where(x => !string.IsNullOrEmpty(x.code) && x.data != null)
                .ToList();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine($"Locusts: {storedLocustData.Count}/{MaxCapacity}");
            dsc.AppendLine($"Hive: {(hiveId.HasValue ? hiveId.Value : "None")}");
        }

        public void OnTuned(int? hive) => hiveId = hive;

    }
}
