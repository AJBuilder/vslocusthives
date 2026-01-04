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

namespace LocustHives.Game.Logistics.Locust
{
    public struct AccessTask
    {
        public ILogisticsStorage storage;
        public IStorageAccessMethod method;
        public ItemStack stack;
        public LogisticsOperation operation;
        public LogisticsPromise promise;

        public uint TryDo(ILogisticsWorker worker, IWorldAccessor accessorForResolve)
        {
            var (to, from) = operation switch {
                LogisticsOperation.Take => (worker.Inventory, storage.Inventory),
                LogisticsOperation.Give => (storage.Inventory, worker.Inventory)
            };

            // Transfer stack targeting this storage using the given other inventory as source/sink
            // depending on the operation.
            // Iterate through the target inventory, taking/giving items until:
            // 1. The requested amount has been transferred
            // 2. Sink has no more room.
            // 3. Source has no more to transfer
            uint toTransfer = (uint)stack.StackSize;
            
            // For each source slot
            foreach(var fromSlot in from)
            {
                // While there is count to transfer
                if (toTransfer <= 0) break;

                // and if it has the item
                if (fromSlot.Itemstack?.Satisfies(stack) ?? false)
                {
                    // and while it isn't empty
                    while (!fromSlot.Empty)
                    {
                        // keep transfering to slots
                        var toSlot = to.GetBestSuitedSlot(fromSlot).slot;
                        if (toSlot == null) break; // No more room

                        var transfered = fromSlot.TryPutInto(accessorForResolve, toSlot, stack.StackSize);
                        if (transfered == 0) break; // No more room. (Should have been caught above though...)
                        toTransfer -= (uint)transfered;

                        toSlot.MarkDirty();
                        fromSlot.MarkDirty();
                    }
                }
            }
            var total =  (uint)stack.StackSize - toTransfer;
            if (promise != null) promise.Fulfill(total, worker);
            return total;
        }
    }

    public class EntityBehaviorLocustLogisticsWorker : EntityBehavior, ILogisticsWorker
    {
        public event Action TasksCancelled;

        static LogisticsSystem logisticsSystem;
        static PathfindSystem pathfindSystem;

        InventoryGeneric inventory;

        /// <summary>
        /// For now, this worker can only promise one thing at a time.
        /// </summary>
        LogisticsPromise currentPromise;

        /// <summary>
        /// The queue of storage access methods that this worker should try to perform to fulfill it's current logistics promise
        /// </summary>
        Queue<AccessTask> queuedStorageAccess = new Queue<AccessTask>();

        public IInventory Inventory => inventory;

        public Queue<AccessTask> AccessTasks { get => queuedStorageAccess; }

        float moveSpeed = 0.03f;

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

            if (entity.Api is ICoreServerAPI)
            {
                logisticsSystem = entity.Api.ModLoader.GetModSystem<LogisticsSystem>();
                pathfindSystem = entity.Api.ModLoader.GetModSystem<PathfindSystem>();

                entity.GetBehavior<EntityBehaviorHiveTunable>().OnTuned += (prevHive, hive) =>
                {
                    logisticsSystem.UpdateLogisticsWorkerMembership(this, hive);
                };
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
                entity.CollisionBox,
                999);
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
        /// <param name="stackForPromise"></param>
        /// <param name="promiseTarget"></param>
        /// <param name="promiseOperation"></param>
        /// <param name="accessTasks"></param>
        /// <returns></returns>
        private WorkerEffort CreateEffort(
            uint bestCount,
            float time,
            ItemStack stackForPromise,
            ILogisticsStorage promiseTarget,
            LogisticsOperation promiseOperation,
            (ILogisticsStorage storage, IStorageAccessMethod method, LogisticsOperation operation, uint count)[] accessTasks)
        {
            return new WorkerEffort(bestCount, time, (requestedCount) =>
            {
                if (currentPromise != null) return null;

                // First, try to get the necessary reservations for all tasks
                var reservations = new LogisticsReservation[accessTasks.Length];
                for (int i = 0; i < accessTasks.Length; i++)
                {
                    var at = accessTasks[i];
                    var reservation = promiseTarget.TryReserve(stackForPromise.CloneWithSize((int)at.count), at.operation);

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
                var promise = new LogisticsPromise(stackForPromise.CloneWithSize((int)Math.Min(requestedCount, bestCount)), promiseTarget, promiseOperation);
                promise.CompletedEvent += (state) =>
                {
                    currentPromise = null;
                    queuedStorageAccess.Clear();
                };

                // If any of the reservations cancel, cancel the promise
                for(int i = 0; i < reservations.Length; i++)
                {
                    reservations[i].ReleasedEvent += () =>
                    {
                        promise.Cancel(); // Cancel doesn't do anything if this was released due to the promise completing.
                    };
                }

                // Now set the promise and queue up the tasks
                currentPromise = promise;
                for (int i = 0; i < accessTasks.Length; i++)
                {
                    var at = accessTasks[i];
                    queuedStorageAccess.Enqueue(new AccessTask {
                        storage = at.storage,
                        method = at.method,
                        stack = reservations[i].Stack,
                        operation = at.operation,
                        // If this task operates on the target with the correct operation, it should fulfill the promise.
                        promise = promiseTarget == at.storage && promiseOperation == at.operation ? promise : null,
                    });
                }

                return promise;
            });
        }

        public IEnumerable<WorkerEffort> GetEfforts(ItemStack stack, ILogisticsStorage target, LogisticsOperation operation)
        {
            // Get this worker's hive. Bail if not in a hive.
            if (!logisticsSystem.WorkerMembership.GetMembershipOf(this, out var hiveId)) yield break;

            if (operation == LogisticsOperation.Take)
            {
                // Check for room and bail if we don't have room
                uint canAccept = inventory.CanAccept(stack);
                if (canAccept == 0) yield break;


                foreach(var method in target.AccessMethods)
                {
                    if(!(method is IInWorldStorageAccessMethod iwmethod)) continue;

                    uint canProvide = method.CanDo(stack, operation);

                    // Skip methods that don't have the item
                    if (canProvide == 0) continue;

                    var path = ComputePath(entity.Pos.AsBlockPos, iwmethod.Position.AsBlockPos);
                    if(path == null) continue;

                    var toTransfer = Math.Min(canAccept, canProvide);

                    // To take, we only have one effort that simply takes once.
                    // This could be improved to try and drop off items first, etc.
                    yield return CreateEffort(
                        toTransfer,
                        ComputeTravelTime(path),
                        stack,
                        target,
                        LogisticsOperation.Take,
                        [
                            (
                                target,
                                method,
                                LogisticsOperation.Take,
                                toTransfer
                            )
                        ]);
                }
            }
            else if(operation == LogisticsOperation.Give)
            {
                uint workerCanProvide = inventory.CanProvide(stack);
                uint missingCount = Math.Min((uint)stack.StackSize - workerCanProvide, 0);


                // If we still need more items, we have one strategy for now: search storages of the hive this worker is in for stuff to take first.
                // For now we'll just compute a single stop at another storage.
                List<(ILogisticsStorage storage, IStorageAccessMethod method, uint canProvide)> potentialTakeOps = null;
                if(missingCount != 0)
                {
                    potentialTakeOps = new List<(ILogisticsStorage, IStorageAccessMethod, uint)>();

                    // But we can only take so much
                    uint canAccept = inventory.CanAccept(stack);
                    if(canAccept != 0)
                    {
                        var takeStack = stack.CloneWithSize((int)Math.Min(missingCount, canAccept));
                        uint potentialTakeCount;
                        foreach (var storage in logisticsSystem.StorageMembership.GetMembersOf(hiveId))
                        {
                            if (storage == target) continue;
                            foreach (var method in storage.AccessMethods)
                            {
                                potentialTakeCount = method.CanDo(takeStack, LogisticsOperation.Take);
                                if (potentialTakeCount != 0) potentialTakeOps.Append((storage, method, potentialTakeCount));
                            }
                        }
                    }
                }

                // Find how to give
                foreach (var method in target.AccessMethods)
                {
                    if (!(method is IInWorldStorageAccessMethod iwmethod)) continue;
                    var givePos = iwmethod.Position.AsBlockPos;

                    // Make sure we can give with this method.
                    // a. we might not be able to give with this particular method
                    // b. there may no longer be room since the promise/request was made.
                    uint canAccept = method.CanDo(stack, LogisticsOperation.Give);

                    // Skip methods that don't have roome
                    if (canAccept == 0) continue;

                    // If we need to take first, 
                    if (potentialTakeOps != null)
                    {
                        // yield efforts that have a task to take first from another storage
                        foreach (var (takeStorage, takeMethod, toTake) in potentialTakeOps)
                        {
                            if (!(takeMethod is IInWorldStorageAccessMethod iwTakeMethod)) continue;
                            var takePos = iwTakeMethod.Position.AsBlockPos;

                            var takePath = ComputePath(entity.Pos.AsBlockPos, takePos);
                            if (takePath == null) continue;

                            var transferPath = ComputePath(takePos, givePos);
                            if (transferPath == null) continue;

                            var toGive = Math.Min(canAccept, workerCanProvide + toTake);
                            yield return CreateEffort(
                                toGive,
                                ComputeTravelTime(takePath) + ComputeTravelTime(transferPath),
                                stack,
                                target,
                                LogisticsOperation.Take,
                                [
                                    (
                                        takeStorage,
                                        takeMethod,
                                        LogisticsOperation.Take,
                                        toTake
                                    ),
                                    (
                                        target,
                                        method,
                                        LogisticsOperation.Give,
                                        toGive
                                    )
                                ]);
                        }
                    } else
                    {
                        // otherwise just give what this worker currently has
                        var toGive = Math.Min(canAccept, workerCanProvide);

                        var givePath = ComputePath(entity.Pos.AsBlockPos, givePos);
                        if (givePath == null) continue;

                        yield return CreateEffort(
                            toGive,
                            ComputeTravelTime(givePath),
                            stack,
                            target,
                            LogisticsOperation.Take,
                            [
                                (
                                    target,
                                    method,
                                    LogisticsOperation.Give,
                                    toGive
                                )
                            ]);
                    }
                }
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);
            if (!inventory.Empty)
            {
                infotext.AppendLine($"Carrying: {string.Join(", ", inventory
                    .Where(s => !s.Empty)
                    .Select(s => $"{s.Itemstack.StackSize}x {s.Itemstack.GetName()}"))}");
            }
        }
    }
}
