using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vintagestory.Server.Timer;

namespace LocustHives.Systems.Logistics.Core
{
    public class LogisticsNetwork : ILogisticsNetwork
    {
        ICoreAPI api;
        /// <summary>
        /// Promises that the network has yet to commision enough worker promises for.
        /// </summary>
        Queue<LogisticsPromise> queuedPromises = new Queue<LogisticsPromise>();

        Dictionary<LogisticsPromise, uint> countCommissioned = new Dictionary<LogisticsPromise, uint>();

        public IReadOnlySet<ILogisticsWorker> Workers { get; }

        public IReadOnlySet<ILogisticsStorage> Storages { get; }

        /// <summary>
        /// Promises awaiting processing/servicing.
        /// </summary>
        public IReadOnlyCollection<LogisticsPromise> QueuedPromises => queuedPromises;

        public LogisticsNetwork(ICoreAPI api, IReadOnlySet<ILogisticsWorker> workers, IReadOnlySet<ILogisticsStorage> storage)
        {
            this.api = api;
            Workers = workers;
            Storages = storage;
        }

                //.Select(t =>
                //{
                //    var bestWorker =
                //        Workers
                //            .Select(w => (
                //                worker: w,
                //                effort: w.GetEfforts(
                //                    promise.Stack,
                //                    promise.TargetStorage,
                //                    LogisticsOperation.Take)))
                //            .Where(x => x.effort != null)
                //            .MaxBy(x =>
                //                x.effort!.time /
                //                Math.Min(x.effort.count, t.storageEffort));
                //
                //    return (
                //        storage: t.storage,
                //        bestWorker
                //    );
                //});

                // Optimization idea: instead of find the absolute best access time, we can stop if we find one that is "acceptable".
                // Acceptable could be a changing calculated threshold based on the historic performance of the network?
                //.OrderBy(tuple => tuple.time!.Value)
                //.TakeWhile(_ => countLeftover > 0)
                //.Foreach(tuple =>
                //{
                //    // If this worker can fulfill the promise alone, then use the original promise.
                //    // If it can't, then create a child promise that will fulfill it partially.
                //    var givePromise = tuple.bestEffort >= countLeftover ? promise : promise.CreateChild(Math.Min(countLeftover, tuple.bestEffort));
                //    if (tuple.worker.Ask(givePromise))
                //    {
                //        // If accepted, decrement the counter
                //        countLeftover -= tuple.itemCount;
                //    }
                //    else
                //    {
                //        // Should be unecessary, but just incase the worker is listening for some reason...
                //        givePromise.Cancel();
                //    }
                //});

                // If not, then we try to take from storage.
                // For each storage, score how suitable each transfer scenario between each of it's and the target's methods and how many items it would fulfill.
                //var allStorages = storageSystem.GetHiveStorages(hive);
                //allStorages
                //    .Select(storage => (storage, itemCount: storage.Inventory.Where(slot => slot.Itemstack.Equals(promise.Stack)).Sum(stack => stack.StackSize)))
                //    .Where(storageWithItemCount => storageWithItemCount.itemCount > 0) // Skip storages without the item
                //    .Select(storageWithItemCount =>
                //        // Yield the pair of access methods that will presumably be the fastest
                //        // to transfer from the source to the target.
                //        (storageWithItemCount.storage,
                //        storageWithItemCount.itemCount,
                //        transferMethod: promise.Target.AccessMethods
                //            .SelectMany(targetMethod =>
                //                storageWithItemCount.storage.AccessMethods.Select(sourceMethod =>
                //                {
                //                    // score of 0: bad but viable
                //                    // score of 1: perfect
                //                    // score of null: not viable
                //                    float? score = sourceMethod switch
                //                    {
                //                        InWorldStorageAccessMethod ba => 1 / ((float)ba.DistanceTo(targetMethod) + 1), // For in world, we assume proximity is a good heuristic: 1/(dist+1)
                //                        _ => null
                //                    };
                //                    if (score.HasValue) score = Math.Clamp(score.Value, 0, 1);
                //
                //                    return (targetMethod, sourceMethod, score);
                //                })
                //            )
                //            .Where(x => x.score.HasValue)
                //            .MinBy(x => x.score!.Value))
                //    )
                //    .OrderByDescending(scenario => scenario.transferMethod.score)
                //    .TakeWhile(_ => countLeftover > 0)
                //    .Foreach(scenario =>
                //    {
                //        // Find the best worker for this transfer scenario
                //        var bestWorker = allWorkers
                //            .Select(w => (worker: w, time: w.GetAccessTime(scenario.storage)))
                //            .Where(x => x.time.HasValue)
                //            .MinBy(x => x.time.Value).worker;
                //
                //        // If we couldn't find a worker for this scenario
                //        if (bestWorker == null) return;
                //
                //        // Otherwise we want it to take from the source storage
                //        var stack = promise.Stack.GetEmptyClone();
                //        stack.StackSize = scenario.itemCount;
                //        var takePromise = new LogisticsPromise(stack, scenario.storage, LogisticsOperation.Take);
                //        if (bestWorker.TryAssignPromise(takePromise))
                //        {
                //            // If accepted, decrement the counter
                //            countLeftover -= scenario.itemCount;
                //
                //            // Listen for when it is completed, and if so
                //        }
                //        else
                //        {
                //            // Should be unecessary, but just incase the worker is listening for some reason...
                //            takePromise.Cancel();
                //        }
                //    });


                ///// OLD

                // Iterate over every scenario for each worker going to one storage for it's items. (Or no storage)

        public LogisticsPromise Push(ItemStack stack, ILogisticsStorage from)
        {
            var promise = new LogisticsPromise(stack, from, LogisticsOperation.Take);
            queuedPromises.Enqueue(promise);
            return promise;
        }

        public LogisticsPromise Pull(ItemStack stack, ILogisticsStorage into)
        {
            var promise = new LogisticsPromise(stack, into, LogisticsOperation.Give);
            queuedPromises.Enqueue(promise);
            return promise;
        }

        /// <summary>
        /// Get promises from workers to contribute towards the next queued promise made by the network.
        /// 
        /// </summary>
        /// <returns></returns>
        public void CommisionWorkersForNextQueuedPromise() {

            var promise = queuedPromises.Dequeue();
            var countLeft = (uint)promise.Stack.StackSize - countCommissioned.GetValueOrDefault(promise);
            while (countLeft >= 0)
            {
                var stack = promise.Stack.GetEmptyClone();
                stack.StackSize = (int)countLeft;

                LogisticsPromise bestPromise = null;
                Workers
                    // 1. For each worker, get all of it's efforts.
                    // TODO: Call GetEfforts on the main thread.
                    .SelectMany(worker => worker.GetEfforts(stack, promise.Target, promise.Operation))

                    // 2. Order efforts first by how fast they are and second by count.
                    .OrderBy(effort => effort.Time)
                    .ThenBy(effort => effort.CountAvailable)

                    // 3. Try to get a promise for the best possible.
                    //    (When we do, this invalidates all the other efforts, so we have to recalculate)
                    .TakeWhile(effort => bestPromise == null)
                    .Foreach(effort =>
                    {
                        bestPromise = effort.TryStart(Math.Max(countLeft, effort.CountAvailable));
                    });

                // 4. If unable, requeue the promise if there is an uncommisioned count and finish.
                if (bestPromise == null)
                {
                    queuedPromises.Enqueue(promise);
                    break;
                }
                else
                {
                    // 5. Add as a child and track how much more we need promised.
                    var countPromised = (uint)bestPromise.Stack.StackSize;
                    countLeft -= countPromised;
                    promise.AddChild(bestPromise);
                    bestPromise.CompletedEvent += (state) =>
                    {
                        // If the worker cancels on us, re-queue.
                        if (state == LogisticsPromiseState.Cancelled)
                        {
                            // Requeue if needed
                            if (!queuedPromises.Contains(promise)) queuedPromises.Enqueue(promise);
                            // Remove this count as being commissioned
                            countCommissioned[promise] = countCommissioned.GetValueOrDefault(promise) + countPromised;
                        }
                    };
                }
                // 6. If we need more, return to step 1.
            }

            // Update our record of how much was commissioned
            countCommissioned[promise] = (uint)promise.Stack.StackSize - countLeft;
        }

    }
}
