using LocustHives.Game.Core;
using LocustHives.Game.Locust;
using LocustHives.Game.Logistics.Lattice;
using LocustHives.Game.Logistics.Locust;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using LocustHives.Systems.Membership;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustHives.Game.Logistics
{
    /// <summary>
    /// This mod system manages logistics networks per hive.
    /// Queries coreSystem for membership instead of maintaining parallel registries.
    /// </summary>
    public class LogisticsSystem : ModSystem
    {
        ICoreServerAPI sapi;
        CoreSystem coreSystem;
        Dictionary<uint, HiveLogisticsNetwork> networks;

        public ILogisticsNetwork GetNetworkFor(uint hiveId) => networks.GetValueOrDefault(hiveId, null);

        public override void Start(ICoreAPI api)
        {
            api.RegisterEntityBehaviorClass("hivelogisticsworker", typeof(EntityBehaviorLocustLogisticsWorker));

            api.RegisterBlockEntityBehaviorClass("HiveAccessPort", typeof(BEBehaviorHiveAccessPort));
            api.RegisterBlockEntityBehaviorClass("HivePushBeacon", typeof(BEBehaviorHivePushBeacon));
            api.RegisterBlockEntityBehaviorClass("HiveStorageRegulator", typeof(BEBehaviorHiveStorageRegulator));
            api.RegisterBlockClass("BlockHivePushBeacon", typeof(BlockHivePushBeacon));
            api.RegisterBlockClass("BlockHiveStorageRegulator", typeof(BlockHiveStorageRegulator));
            api.RegisterBlockEntityClass("HiveLattice", typeof(BEHiveLattice));
            
            this.coreSystem = api.ModLoader.GetModSystem<CoreSystem>();
            coreSystem.RegisterMembershipType("locusthives:storage", GenericBlockEntityLogisticsStorage.ToBytes, GenericBlockEntityLogisticsStorage.FromBytes);
            coreSystem.RegisterMembershipType("locusthives:lattice", LatticeStorageGroup.ToBytes, LatticeStorageGroup.FromBytes);
            coreSystem.RegisterMembershipType("locusthives:worker", GenericEntityLogisticsWorker.ToBytes, GenericEntityLogisticsWorker.FromBytes);

        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
            this.sapi = sapi;

            AiTaskRegistry.Register<AiTaskLocustLogisticsOperation>("doLogisticsAccessTasks");

            this.networks = new Dictionary<uint, HiveLogisticsNetwork>();

            // Networks are transient, so we have to make them on load for all the existing
            // hives which are not transient.
            this.sapi.Event.SaveGameLoaded += () =>
            {
                foreach(var hive in coreSystem.Hives) EnsureNetwork(hive);
            };

            // And setup events so that we can continue to have a matching network.
            coreSystem.HiveCreated += EnsureNetwork;
            coreSystem.HiveDeleted += (id) => networks.Remove(id);

            sapi.Event.RegisterGameTickListener((dt) =>
            {
                foreach (var network in networks.Values)
                {
                    // Iterate over current queue (or max of 10) and try to assign.
                    // TODO: Better optimization than capping number of processed requests to 10.
                    int count = Math.Min(network.QueuedPromises.Count, 10);
                    for (int i = 0; i < count; i++)
                    {
                        network.CommisionWorkersForNextQueuedPromise();
                    }
                }
            }, 3000);
        }

        public void EnsureNetwork(HiveHandle hive)
        {
            // Ensure a network exists.
            if (!networks.ContainsKey(hive.Id))
            {
                // Query coreSystem for members
                networks[hive.Id] = new HiveLogisticsNetwork(sapi, hive.Members);
            }
        }

        public override double ExecuteOrder()
        {
            return base.ExecuteOrder() + 0.1; // Must be called after hives are all loaded.
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


            // Create the doLogisticsAccessTasks task
            var doLogisticsAccessTasksTask = new JObject
            {
                ["code"] = "doLogisticsAccessTasks",
                ["priority"] = 1.351f,
                ["priorityForCancel"] = 1.351f,
                ["animationSpeed"] = 4,
                ["animation"] = "run"
            };

            (aiTasksArray.Token as JArray)?.Add(doLogisticsAccessTasksTask);

            api.Logger.Notification("[LocustLogistics] Added AI tasks to locust-hacked entity");
        }
    }
}
