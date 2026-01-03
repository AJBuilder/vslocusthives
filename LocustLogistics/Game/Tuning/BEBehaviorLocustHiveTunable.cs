using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace LocustHives.Game.Core
{
    public class BEBehaviorLocustHiveTunable : BlockEntityBehavior, IHiveMember
    {
        int? hiveId;
        TuningSystem modSystem;

        public Action<int?, int?> OnTuned { get; set; }

        public BEBehaviorLocustHiveTunable(BlockEntity blockentity) : base(blockentity)
        {
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            OnTuned += (_, newId) =>
            {
                hiveId = newId;
            };

            modSystem = api.ModLoader.GetModSystem<TuningSystem>();

            if (api is ICoreServerAPI)
            {
                if (!hiveId.HasValue && properties["createsHive"].AsBool()) hiveId = modSystem.CreateHive();
            }

            // Kinda hacky, but we have to delay tuning so that other behaviors have a chanto register to the OnTuned event.
            api.Event.RegisterCallback((dt) => {
                modSystem.Tune(this, hiveId);
            }, 0);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modSystem?.Tune(this, null);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            modSystem?.Tune(this, null);
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (hiveId.HasValue) tree.SetInt("hiveId", hiveId.Value);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var id = tree.TryGetInt("hiveId");
            // If modSystem not set yet, then this is on-load or the client. If on-load we'll do it later in Initialize.
            if (modSystem == null) hiveId = id;
            else if (id.HasValue != hiveId.HasValue ||
                id.HasValue && hiveId.HasValue && id != hiveId) modSystem?.Tune(this, id);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine($"Hive: {(hiveId.HasValue ? hiveId.Value : "None")}");
        }
    }
}
