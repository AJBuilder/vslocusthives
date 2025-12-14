using LocustLogistics.Core;
using LocustLogistics.Logistics.Push;
using LocustLogistics.Logistics.Storage;
using LocustLogistics.Nests;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustLogistics.Logistics.Retrieval
{
    public class PushLogisticsModSystem : ModSystem
    {
        ICoreServerAPI sapi;
        Dictionary<ILogisticsWorker, PushRequest> assignments = new Dictionary<ILogisticsWorker, PushRequest>();
        Dictionary<PushRequest, ILogisticsWorker> activeRequests = new Dictionary<PushRequest, ILogisticsWorker>();
        Queue<PushRequest> queuedRequests = new Queue<PushRequest>();


        LogisticsWorkersModSystem workerSystem;
        StorageLogisticsModSystem storageSystem;

        public IReadOnlyDictionary<ILogisticsWorker, PushRequest> Assignments => assignments;

        public IReadOnlyDictionary<PushRequest, ILogisticsWorker> ActiveRequests => activeRequests;

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("HivePushBeacon", typeof(BEBehaviorHivePushBeacon));
            api.RegisterBlockClass("BlockHivePushBeacon", typeof(BlockHivePushBeacon));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            AiTaskRegistry.Register<AiTaskHiveWorkerFulfillPushRequest>("fulfillPushRequest");
            workerSystem = api.ModLoader.GetModSystem<LogisticsWorkersModSystem>();

            api.Event.RegisterGameTickListener((dt) =>
            {
                // Iterate over current queue (or max of 10) and try to assign.
                // TODO: Better optimization than capping number of processed requests to 10.
                int count = Math.Min(queuedRequests.Count, 10);
                for (int i = 0; i < count; i++)
                {
                    AssignOrQueueRequest(queuedRequests.Dequeue());
                }
            }, 3000);

        }

        /// <summary>
        /// Should only be called server side.
        /// </summary>
        /// <param name="stack"></param>
        /// <param name="from"></param>
        /// <returns></returns>
        public PushRequest Request(ItemStack stack, IHiveStorage from)
        {
            PushRequest request = new PushRequest(stack, from);
            request.CompletedEvent += () =>
            {
                if (activeRequests.TryGetValue(request, out var worker)) assignments.Remove(worker);
            };
            request.AbandonedEvent += () =>
            {
                if (activeRequests.TryGetValue(request, out var worker)) assignments.Remove(worker);
                queuedRequests.Enqueue(request);
            };
            request.FailedEvent += () =>
            {
                if (activeRequests.TryGetValue(request, out var worker)) assignments.Remove(worker);
            };
            request.CancelledEvent += () =>
            {
                if (activeRequests.TryGetValue(request, out var worker)) assignments.Remove(worker);

                // Cancelling shouldn't happen often?
                // Let's just naively iterate and remove it.
                int count = queuedRequests.Count;
                for (int i = 0; i < count; i++)
                {
                    PushRequest current = queuedRequests.Dequeue();
                    if (current != request)
                    {
                        queuedRequests.Enqueue(current);
                    }
                }
            };

            sapi?.Logger.Debug($"{from.Position} is requesting retrieval of {stack.ToString()}");
            AssignOrQueueRequest(request);
            return request;
        }


        private void AssignOrQueueRequest(PushRequest request)
        {
            // Queue if not part of a hive
            if (!storageSystem.Membership.TryGetValue(request.From, out var hive))
            {
                queuedRequests.Enqueue(request);
            }

            // Enumerate over workers that don't have assignments and are closest.
            var hiveWorkers = workerSystem.GetHiveWorkers(hive)
                // Filter out workers already assigned
                .Where(w => !assignments.ContainsKey(w))
                // Order them by distance to the request source
                .OrderBy(w => w.Position.DistanceTo(request.From.Position));

            foreach (var worker in hiveWorkers)
            {
                // Attempt assignment
                if (worker.TryAssignPushRequest(request))
                {
                    assignments[worker] = request;
                    activeRequests[request] = worker;
                    return;
                }
                // else: worker refused, try next best
            }

            // Nobody accepted, queue for later
            queuedRequests.Enqueue(request);
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
                ["code"] = "fulfillPushRequest",
                ["priority"] = 1.351f,
                ["priorityForCancel"] = 1.351f,
                ["animationSpeed"] = 4,
                ["animation"] = "run"
            };

            (aiTasksArray.Token as JArray)?.Add(taskConfig);

            api.Logger.Notification("[LocustLogistics] Added returnToNest AI task to locust-hacked entity");
        }
    }
}
