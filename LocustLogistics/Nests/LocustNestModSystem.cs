using HarmonyLib;
using LocustLogistics.Nests;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustLogistics.Core
{
    /// <summary>
    /// This mod system tracks nests that are tuned to a hive.
    /// </summary>
    public class LocustNestModSystem : ModSystem
    {
        // Events
        public event Action<ILocustNest, int?, int?> NestTuned;

        Dictionary<ILocustNest, int> allTunedNests = new Dictionary<ILocustNest, int>();
        Dictionary<int, HashSet<ILocustNest>> hiveNests = new Dictionary<int, HashSet<ILocustNest>>();

        public IReadOnlyDictionary<ILocustNest, int> Membership => allTunedNests;
        public IReadOnlySet<ILocustNest> GetHiveNests(int hive)
        {
            if (hiveNests.TryGetValue(hive, out var nests)) return nests;
            else return new HashSet<ILocustNest>();
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("HiveLocustNest", typeof(BEBehaviorHiveLocustNest));
            api.RegisterBlockClass("BlockTamedLocustNest", typeof(BlockTamedLocustNest));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskReturnToNest>("returnToNest");
        }

        public void UpdateNestHiveMembership(ILocustNest nest, int? prevHive, int? hive)
        {
            if (hive.HasValue) allTunedNests[nest] = hive.Value;
            else allTunedNests.Remove(nest);


            // Clean up prior caching
            if (prevHive.HasValue)
            {
                if (hiveNests.TryGetValue(prevHive.Value, out var nests))
                {
                    nests.Remove(nest);
                    if (nests.Count == 0) hiveNests.Remove(prevHive.Value);
                }
            }
            ;

            // Add new caching
            if (hive.HasValue)
            {
                if (!hiveNests.TryGetValue(hive.Value, out var nests))
                {
                    nests = new HashSet<ILocustNest>();
                    hiveNests[hive.Value] = nests;
                }
                nests.Add(nest);
            }
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
            var taskConfig = new JObject
            {
                ["code"] = "returnToNest",
                ["priority"] = 1.35f,
                ["priorityForCancel"] = 1.35f,
                ["mincooldown"] = 5000,
                ["maxcooldown"] = 15000,
                ["animationSpeed"] = 4,
                ["animation"] = "run"
            };

            (aiTasksArray.Token as JArray)?.Add(taskConfig);

            api.Logger.Notification("[LocustLogistics] Added returnToNest AI task to locust-hacked entity");
        }

    }
}
