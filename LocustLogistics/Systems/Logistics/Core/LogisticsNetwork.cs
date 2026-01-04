using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

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

        public LogisticsPromise Push(ItemStack stack, ILogisticsStorage from)
        {
            return Request(stack, from, LogisticsOperation.Take);
        }

        public LogisticsPromise Pull(ItemStack stack, ILogisticsStorage into)
        {
            return Request(stack, into, LogisticsOperation.Give);
        }

        public LogisticsPromise Request(ItemStack stack, ILogisticsStorage target, LogisticsOperation operation)
        {
            var promise = new LogisticsPromise(stack, target, operation);
            queuedPromises.Enqueue(promise);
            return promise;
        }

        /// <summary>
        /// Get promises from workers to contribute towards the next queued promise made by the network.
        /// 
        /// </summary>
        /// <returns></returns>
        public void CommisionWorkersForNextQueuedPromise() {
            // If no workers, can't do anything
            if (!Workers.Any()) return;

            var promise = queuedPromises.Dequeue();
            if(promise.State != LogisticsPromiseState.Unfulfilled)
            {
                countCommissioned.Remove(promise);
                return;
            }

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
                            // Mark unfulfilled count as needing recommisioned.
                            countCommissioned[promise] -= (countPromised - bestPromise.Fulfilled);
                            // Requeue main promise if needed
                            if (!queuedPromises.Contains(promise)) queuedPromises.Enqueue(promise);
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
