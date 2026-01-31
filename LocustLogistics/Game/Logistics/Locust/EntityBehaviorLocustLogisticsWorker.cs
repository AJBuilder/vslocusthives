using LocustHives.Game.Core;
using LocustHives.Game.Logistics;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.AccessMethods;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.Essentials;
using Vintagestory.GameContent;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vintagestory.Server.Timer;

namespace LocustHives.Game.Logistics.Locust
{
    public struct AccessTask
    {
        public ILogisticsStorage storage;
        public IStorageAccessMethod method;
        /// <summary>
        /// The stack to transfer.
        /// Positive = Give (worker inventory -> storage inventory)
        /// Negative = Take (storage inventory -> worker inventory)
        /// </summary>
        public ItemStack stack;

        /// <summary>
        /// The promise that the completion of this task will completely fulfill.
        /// If this task fails to fulfill it completely, it will be cancelled.
        /// </summary>
        public LogisticsPromise promise;

        public uint TryDo(ILogisticsWorker worker)
        {
            bool isTake = stack.StackSize < 0;
            uint quantity = (uint)Math.Abs(stack.StackSize);
            uint transferred = 0;

            if (isTake)
            {
                // Take: find slots in worker inventory to receive items
                foreach (var workerSlot in worker.Inventory)
                {
                    if (transferred >= quantity) break;
                    var remaining = stack.CloneWithSize((int)(quantity - transferred));
                    transferred += method.TryTakeOut(remaining, workerSlot);
                }
            }
            else
            {
                // Give: find slots in worker inventory with matching items
                foreach (var workerSlot in worker.Inventory)
                {
                    if (transferred >= quantity) break;
                    if (workerSlot.Itemstack?.Satisfies(stack) ?? false)
                    {
                        uint remaining = (quantity - transferred);
                        transferred += method.TryPutInto(workerSlot, remaining);
                    }
                }
            }

            if (promise != null) promise.Fulfill(transferred, worker);
            return transferred;
        }
    }

    public class EntityBehaviorLocustLogisticsWorker : EntityBehavior, ILogisticsWorker, IHiveTunable
    {
        public event Action TasksCancelled;

        LogisticsSystem logisticsSystem;
        PathfindSystem pathfindSystem;
        CoreSystem coreSystem;

        InventoryGeneric inventory;

        /// <summary>
        /// The queue of storage access methods that this worker should try to perform to fulfill it's current logistics promise.
        /// </summary>
        Queue<AccessTask> queuedStorageAccess;

        AccessTask? lastRememberedTask;
        private const int MsToForgetLastTask = 60000;

        long putAwayListenerId;
        long forgetLastTaskListenerId;

        float moveSpeed = 0.03f;

        public IInventory Inventory => inventory;

        public AccessTask? CurrentAccessTask
        {
            get {
                if (queuedStorageAccess.Any())
                {
                    return queuedStorageAccess.Peek();
                }
                else
                {
                    return null;
                }
            }
        }

        public IHiveMember HiveMembershipHandle => new GenericEntityLogisticsWorker(entity.EntityId, entity.Api);


        public EntityBehaviorLocustLogisticsWorker(Entity entity) : base(entity)
        {
            inventory = new InventoryGeneric(1, $"logisticsworker-{entity.GetName()}-{entity.EntityId}", entity.Api);
            inventory.SlotModified += (slotid) =>
            {
                ITreeAttribute tree = new TreeAttribute();
                entity.WatchedAttributes["logisticsInventory"] = tree;
                entity.WatchedAttributes.MarkPathDirty("logisticsInventory");
                inventory.ToTreeAttributes(tree);

                if (entity.Api is ICoreServerAPI sapi)
                {
                    sapi.Network.BroadcastEntityPacket(entity.EntityId, 1235, SerializerUtil.ToBytes((w) => tree.ToBytes(w)));
                }
            };
            (entity as EntityAgent).LeftHandItemSlot = inventory[0];
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            ITreeAttribute tree = entity.WatchedAttributes["logisticsInventory"] as ITreeAttribute;
            if (tree != null) inventory.FromTreeAttributes(tree);

            coreSystem = entity.Api.ModLoader.GetModSystem<CoreSystem>();

            if (entity.Api is ICoreServerAPI sapi)
            {
                queuedStorageAccess = new Queue<AccessTask>();

                logisticsSystem = sapi.ModLoader.GetModSystem<LogisticsSystem>();
                pathfindSystem = sapi.ModLoader.GetModSystem<PathfindSystem>();

                putAwayListenerId = sapi.Event.RegisterGameTickListener(TryToPutAwaySomeInventory, 3000);
            }
        }

        /// <summary>
        /// Called to indicate that this current task was done.
        /// </summary>
        public void DidCurrentTask()
        {
            if (queuedStorageAccess.Any())
            {
                // Remember the completed task and set a callback to forget later.
                lastRememberedTask = queuedStorageAccess.Dequeue();
                forgetLastTaskListenerId = entity.Api.Event.RegisterCallback(ForgetLastTask, MsToForgetLastTask);

                var promise = lastRememberedTask.Value.promise;
                if (promise != null)
                {
                    // If we were unable to fulfill the promise, cancel it.
                    if (promise.State == LogisticsPromiseState.Unfulfilled) promise.Cancel();
                }
            }
        }

        private void ForgetLastTask(float dt)
        {
            lastRememberedTask = null;
        }

        private void TryToPutAwaySomeInventory(float dt)
        {
            // If no current task, there is something in the inventory, and a member of a hive
            var handle = HiveMembershipHandle;
            if (!queuedStorageAccess.Any() && !inventory.Empty && coreSystem.GetHiveOf(handle, out var hive))
            {
                // Try puting the a slot's contents away.
                // Note that until this task is done, this will block making any more promises.
                // TODO: Don't block

                var giveStack = inventory.Where(slot => !slot.Empty).Select(slot => slot.Itemstack).First();

                // Find where to put it
                // but skip the last remembered task's storage if it was a take (negative stack size).
                // We don't want to give the item right back if we just took it.
                var skipStorage = lastRememberedTask.HasValue && lastRememberedTask.Value.stack.StackSize < 0 ? lastRememberedTask.Value.storage : null;

                // The give strategy in this instance is to give as soon as possible, event if there isn't enough room.
                // This may not be best? But this behavior isn't exactly time critical, so no need to do exhaustive computations. (unlike computing efforts)
                IStorageAccessMethod bestMethod = null;
                ILogisticsStorage bestStorage = null;
                uint bestCount = uint.MinValue;
                float bestTime = float.MaxValue;
                foreach (var storage in hive.Members.OfType<ILogisticsStorage>())
                {
                    if (storage.Equals(skipStorage)) continue;

                    foreach (var method in storage.AccessMethods)
                    {
                        if (!(method is IInWorldStorageAccessMethod iwmethod)) continue;

                        // Make sure we can give with this method (positive stack = Give).
                        uint canAccept = method.CanDo(giveStack);

                        // Skip methods that don't have room
                        if (canAccept == 0) continue;

                        var givePath = ComputePath(entity.Pos.AsBlockPos, iwmethod.Position.AsBlockPos);
                        if (givePath == null) continue;

                        var giveTime = ComputeTravelTime(givePath);
                        if (giveTime < bestTime)
                        {
                            bestMethod = method;
                            bestStorage = storage;
                            bestCount = canAccept;
                            bestTime = giveTime;
                        }
                    }
                }
                if (bestStorage != null)
                {
                    giveStack = giveStack.Clone();
                    giveStack.StackSize = Math.Min((int)bestCount, giveStack.StackSize); // Don't give more than we have.
                    queuedStorageAccess.Enqueue(new AccessTask
                    {
                        storage = bestStorage,
                        method = bestMethod,
                        stack = giveStack, // Positive = Give
                        promise = null
                    });
                }
            }
        }
        
        public override void OnReceivedServerPacket(int packetid, byte[] data, ref EnumHandling handled)
        {
            if (packetid == 1235)
            {
                TreeAttribute tree = new TreeAttribute();
                SerializerUtil.FromBytes(data, (r) => tree.FromBytes(r));
                inventory.FromTreeAttributes(tree);
            }
        }

        public override string PropertyName()
        {
            return "locustlogisticsworker";
        }


        private List<Vec3d> ComputePath(BlockPos fromPos, BlockPos toPos)
        {
            var bh = entity.GetBehavior<EntityBehaviorControlledPhysics>();
            float stepHeight = bh == null ? 0.6f : bh.StepHeight;
            int maxFallHeight = entity.Properties.FallDamage ? Math.Min(8, (int)Math.Round(3.51 / Math.Max(0.01, entity.Properties.FallDamageMultiplier))) - (int)(moveSpeed * 30) : 8;   // fast moving entities cannot safely fall so far (might misstarget /block below due to outward drift)

            return pathfindSystem.FindPathAsWaypoints(
                fromPos,
                toPos,
                maxFallHeight,
                stepHeight,
                entity.CollisionBox);
        }

        private float ComputeTravelTime(List<Vec3d> path)
        {
            //if (path == null) continue;
            double distance = 0;
            for (int i = 1; i < path.Count; i++)
            {
                distance += path[i - 1].DistanceTo(path[i]);
            }
            return (float)distance / moveSpeed;
        }

        /// <summary>
        /// Create an effort for the given details.
        /// </summary>
        /// <param name="bestCount"></param>
        /// <param name="time"></param>
        /// <param name="promiseStack">Stack with sign indicating promise direction (+ = Give, - = Take)</param>
        /// <param name="promiseTarget"></param>
        /// <param name="accessTasks">Tuples with signedCount (+ = Give, - = Take)</param>
        /// <returns></returns>
        private WorkerEffort CreateEffort(
            uint bestCount,
            float time,
            ItemStack promiseStack,
            ILogisticsStorage promiseTarget,
            (ILogisticsStorage storage, IStorageAccessMethod method, int signedCount)[] accessTasks)
        {
            return new WorkerEffort(bestCount, time, (requestedCount) =>
            {
                // TODO: Don't bail. Instead accept if it is targeting the same storage.
                if (queuedStorageAccess.Any()) return null;

                // First, try to get the necessary reservations for all tasks
                var reservations = new LogisticsReservation[accessTasks.Length];
                for (int i = 0; i < accessTasks.Length; i++)
                {
                    var at = accessTasks[i];
                    var reservation = at.method.TryReserve(promiseStack.CloneWithSize(at.signedCount));

                    // If we fail to get one
                    if (reservation == null)
                    {
                        // Release only what we've successfully reserved so far
                        for (int j = 0; j < i; j++)
                        {
                            reservations[j]?.Release();
                        }

                        return null;
                    }

                    reservations[i] = reservation;
                }

                // With everything reserved, we can create and setup a promise to return.
                // Preserve the sign but update the magnitude to requested amount
                int promiseSign = promiseStack.StackSize >= 0 ? 1 : -1;
                var promise = new LogisticsPromise(promiseStack.CloneWithSize(promiseSign * (int)Math.Min(requestedCount, bestCount)), promiseTarget);
                promise.CompletedEvent += (state) =>
                {
                    if(state == LogisticsPromiseState.Cancelled){
                        queuedStorageAccess.Clear();
                        TasksCancelled?.Invoke();
                    }
                    
                    // Release all the reservations
                    for (int i = 0; i < reservations.Length; i++)
                    {
                        reservations[i]?.Release();
                    }
                };

                // If any of the reservations cancel, cancel the promise
                for(int i = 0; i < reservations.Length; i++)
                {
                    reservations[i].ReleasedEvent += () =>
                    {
                        promise.Cancel(); // Cancel doesn't do anything if this was released due to the promise completing.
                    };
                }

                // Now queue up the tasks
                bool promiseIsTake = promiseStack.StackSize < 0;
                for (int i = 0; i < accessTasks.Length; i++)
                {
                    var at = accessTasks[i];
                    bool taskIsTake = at.signedCount < 0;
                    queuedStorageAccess.Enqueue(new AccessTask {
                        storage = at.storage,
                        method = at.method,
                        stack = reservations[i].Stack,
                        // If this task operates on the target with the correct operation, it should fulfill the promise.
                        promise = (promiseTarget == at.storage && promiseIsTake == taskIsTake) ? promise : null,
                    });
                }

                return promise;
            });
        }

        public IEnumerable<WorkerEffort> GetEfforts(ItemStack stack, ILogisticsStorage target)
        {
            // Get this worker's hive. Bail if not in a hive.
            var handle = HiveMembershipHandle;
            if (!coreSystem.GetHiveOf(handle, out var hive)) yield break;

            bool isTake = stack.StackSize < 0;
            uint absStackSize = (uint)Math.Abs(stack.StackSize);

            if (isTake)
            {
                // Check how much room this worker has
                uint canAccept = inventory.CanAccept(stack);

                // If no room, bail
                if (canAccept == 0) yield break;

                foreach(var method in target.AccessMethods)
                {
                    if(!(method is IInWorldStorageAccessMethod iwmethod)) continue;

                    // Check how much can be taken
                    uint canProvide = method.CanDo(stack);

                    // Skip methods that don't have the item
                    if (canProvide == 0) continue;

                    var path = ComputePath(entity.Pos.AsBlockPos, iwmethod.Position.AsBlockPos);
                    if(path == null) continue;

                    var toTransfer = Math.Min(absStackSize, Math.Min(canAccept, canProvide));
                    if (toTransfer == 0) continue;

                    // To take, we only have one effort that simply takes once.
                    // This could be improved to try and drop off items first, etc.
                    yield return CreateEffort(
                        toTransfer,
                        ComputeTravelTime(path),
                        stack, // negative = Take
                        target,
                        [
                            (
                                target,
                                method,
                                -(int)toTransfer // negative = Take
                            )
                        ]);
                }
            }
            else // Give (positive stack size)
            {
                uint workerAlreadyHas = inventory.CanProvide(stack);
                uint missingCount = (uint)Math.Max(absStackSize - workerAlreadyHas, 0);


                // If we still need more items, we have one strategy for now: search storages of the hive this worker is in for stuff to take first.
                // For now we'll just compute a single stop at another storage.
                // This is not an enumerable because we will have to iterate over it for each target access method.
                List<(ILogisticsStorage storage, IStorageAccessMethod method, uint canProvide)> potentialTakeOps = null;
                if(missingCount != 0)
                {
                    potentialTakeOps = new List<(ILogisticsStorage, IStorageAccessMethod, uint)>();

                    // But we can only take so much
                    uint canAccept = inventory.CanAccept(stack);
                    if(canAccept != 0)
                    {
                        // Use negative stack for Take query
                        var takeStack = stack.CloneWithSize(-(int)Math.Min(missingCount, canAccept));
                        uint availableToTake;
                        foreach (var storage in hive.Members.OfType<ILogisticsStorage>())
                        {
                            if (storage.Equals(target)) continue;
                            foreach (var method in storage.AccessMethods)
                            {
                                availableToTake = method.CanDo(takeStack);
                                if (availableToTake != 0) potentialTakeOps.Add((storage, method, availableToTake));
                            }
                        }
                    }
                }

                // Find how to give
                foreach (var method in target.AccessMethods)
                {
                    if (!(method is IInWorldStorageAccessMethod iwmethod)) continue;

                    // Make sure we can give with this method (positive stack = Give).
                    // a. we might not be able to give with this particular method
                    // b. there may no longer be room since the promise/request was made.
                    uint canAccept = method.CanDo(stack);

                    // Skip methods that don't have room
                    if (canAccept == 0) continue;

                    var givePos = iwmethod.Position.AsBlockPos;

                    // If we need to take first,
                    if (potentialTakeOps != null)
                    {
                        // yield efforts that have a task to take first from another storage
                        foreach (var (takeStorage, takeMethod, availableToTake) in potentialTakeOps)
                        {
                            if (!(takeMethod is IInWorldStorageAccessMethod iwTakeMethod)) continue;
                            var takePos = iwTakeMethod.Position.AsBlockPos;

                            var takePath = ComputePath(entity.Pos.AsBlockPos, takePos);
                            if (takePath == null) continue;

                            var transferPath = ComputePath(takePos, givePos);
                            if (transferPath == null) continue;

                            var toTake = Math.Min(missingCount, availableToTake);
                            var toGive = Math.Min(canAccept, workerAlreadyHas + toTake);
                            yield return CreateEffort(
                                toGive,
                                ComputeTravelTime(takePath) + ComputeTravelTime(transferPath),
                                stack, // positive = Give
                                target,
                                [
                                    (
                                        takeStorage,
                                        takeMethod,
                                        -(int)toTake // negative = Take
                                    ),
                                    (
                                        target,
                                        method,
                                        (int)toGive // positive = Give
                                    )
                                ]);
                        }
                    } else
                    {
                        // otherwise just give what this worker currently has
                        var toGive = Math.Min(canAccept, workerAlreadyHas);

                        var givePath = ComputePath(entity.Pos.AsBlockPos, givePos);
                        if (givePath == null) continue;

                        yield return CreateEffort(
                            toGive,
                            ComputeTravelTime(givePath),
                            stack, // positive = Give
                            target,
                            [
                                (
                                    target,
                                    method,
                                    (int)toGive // positive = Give
                                )
                            ]);
                    }
                }
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            Cleanup();
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                inventory.DropAll(entity.ServerPos.XYZ);
            }
            base.OnEntityDeath(damageSourceForDeath);
            Cleanup();
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);
            if(coreSystem.GetHiveOf(HiveMembershipHandle, out var hive))
            {
                infotext.AppendLine($"Hive: {hive.Name}");
            }
            if (!inventory.Empty)
            {
                infotext.AppendLine($"Carrying: {string.Join(", ", inventory
                    .Where(s => !s.Empty)
                    .Select(s => $"{s.Itemstack.StackSize}x {s.Itemstack.GetName()}"))}");
            }
        }

        public void Cleanup()
        {
            if (entity.Api is ICoreServerAPI)
            {
                entity.Api.Event.UnregisterGameTickListener(putAwayListenerId);
                entity.Api.Event.UnregisterCallback(forgetLastTaskListenerId);
            }
        }
    }
}
