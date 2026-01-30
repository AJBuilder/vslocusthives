using HarmonyLib;
using LocustHives.Game.Core;
using LocustHives.Game.Locust;
using LocustHives.Game.Logistics;
using LocustHives.Game.Nest;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Membership;
using LocustHives.Systems.Nests;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustHives.Game.Nests
{
    /// <summary>
    /// This mod system manages nest-related functionality.
    /// Queries TuningSystem for nest membership.
    /// </summary>
    public class NestsSystem : ModSystem
    {
        TuningSystem tuningSystem;

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("HiveLocustNest", typeof(BEBehaviorHiveLocustNest));
            api.RegisterBlockClass("BlockTamedLocustNest", typeof(BlockTamedLocustNest));

            tuningSystem = api.ModLoader.GetModSystem<TuningSystem>();
            tuningSystem.RegisterMembershipType("locusthives:nest", NestHandle.ToBytes, (bytes) => NestHandle.FromBytes(bytes, api));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskReturnToNest>("returnToNest");
        }

        /// <summary>
        /// Get all nests of a specific hive.
        /// </summary>
        public IEnumerable<ILocustNest> GetNestsOfHive(int hiveId)
        {
            return tuningSystem.GetMembersOf(hiveId).OfType<ILocustNest>();
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            // Only run on server side to prevent double-patching
            if (api.Side != EnumAppSide.Server)
            {
                return;
            }

            // Find the locust-hacked entity
            var locustEntity = api.World.EntityTypes.FirstOrDefault(e =>
                e.Code.Path.Contains("locust") &&
                e.Variant.ContainsKey("state") &&
                e.Variant["state"] == "hacked");

            if (locustEntity == null)
            {
                api.Logger.Warning("[LocustLogistics] Could not find locust-hacked entity to patch");
                return;
            }

            // Find the taskai behavior by searching for its code
            var taskAiBehavior = locustEntity.Server?.BehaviorsAsJsonObj.FirstOrDefault(b =>
            {
                var code = b["code"];
                if (code.Exists)
                {
                    if ("taskai" == code.AsString()) return true;

                }
                return false;
            });

            if (!taskAiBehavior.Exists)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai behavior in locust-hacked entity");
                return;
            }

            // Get the aitasks array
            var aiTasksArray = taskAiBehavior["aitasks"];

            if (!aiTasksArray.Exists)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai task array in locust-hacked entity");
                return;
            }

            // Create the returnToNest task
            var returnToNestTask = new JObject
            {
                ["code"] = "returnToNest",
                ["priority"] = 1.35f,
                ["priorityForCancel"] = 1.35f,
                ["mincooldown"] = 5000,
                ["maxcooldown"] = 10000,
                ["animationSpeed"] = 4,
                ["animation"] = "run"
            };

            (aiTasksArray.Token as JArray)?.Add(returnToNestTask);

            api.Logger.Notification("[LocustLogistics] Added AI tasks to locust-hacked entity");
        }

    }
}
