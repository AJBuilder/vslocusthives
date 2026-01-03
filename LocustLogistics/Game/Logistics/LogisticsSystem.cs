using LocustHives.Game.Logistics.Locust;
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
    /// This mod system tracks locustthat are tuned to a hive.
    /// </summary>
    public class LogisticsSystem : ModSystem
    {
        ICoreAPI api;

        MembershipRegistry<ILogisticsWorker> workerRegistry;
        MembershipRegistry<ILogisticsStorage> storageRegistry;
        Dictionary<int, LogisticsNetwork> networks;

        public IMembershipRegistry<ILogisticsWorker> WorkerMembership => workerRegistry;
        public IMembershipRegistry<ILogisticsStorage> StorageMembership => storageRegistry;
        public ILogisticsNetwork GetNetworkFor(int hiveId) => networks.GetValueOrDefault(hiveId, null);

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            api.RegisterEntityBehaviorClass("hivelogisticsworker", typeof(EntityBehaviorLocustLogisticsWorker));

            api.RegisterBlockEntityBehaviorClass("HiveAccessPort", typeof(BEBehaviorHiveAccessPort));
            api.RegisterBlockEntityBehaviorClass("HivePushBeacon", typeof(BEBehaviorHivePushBeacon));
            api.RegisterBlockClass("BlockHivePushBeacon", typeof(BlockHivePushBeacon));

            workerRegistry = new MembershipRegistry<ILogisticsWorker>();
            storageRegistry = new MembershipRegistry<ILogisticsStorage>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskLocustLogisticsOperation>("doLogisticsAccessTasks");

            networks = new Dictionary<int, LogisticsNetwork>();

            api.Event.RegisterGameTickListener((dt) =>
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



        public void UpdateLogisticsWorkerMembership(ILogisticsWorker worker, int? hiveId)
        {
            workerRegistry.AssignMembership(worker, hiveId);
            if (hiveId.HasValue) EnsureNetwork(hiveId.Value);
        }

        public void UpdateLogisticsStorageMembership(ILogisticsStorage storage, int? hiveId)
        {
            storageRegistry.AssignMembership(storage, hiveId);
            if (hiveId.HasValue) EnsureNetwork(hiveId.Value);
        }

        public void EnsureNetwork(int hiveId)
        {
            if (api is ICoreServerAPI && !networks.ContainsKey(hiveId))
            {
                var workers = workerRegistry.GetMembersOf(hiveId);
                var storages = storageRegistry.GetMembersOf(hiveId);
                networks[hiveId] = new LogisticsNetwork(api, workers, storages);
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
