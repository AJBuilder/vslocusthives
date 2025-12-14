using LocustLogistics.Core;
using LocustLogistics.Logistics.Storage;
using LocustLogistics.Nests;
using LocustLogistics.Util;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace LocustLogistics.Logistics.Retrieval
{

    public class AiTaskHiveWorkerFulfillPushRequest : AiTaskBase
    {
        IReadOnlyDictionary<IHiveMember, int> membership;
        PushLogisticsModSystem pushModSystem;
        IReadOnlyDictionary<ILogisticsWorker, PushRequest> assignments;
        IHiveMember member;
        ILogisticsWorker worker;
        PushRequest request;
        bool pathfindingActive;

        float moveSpeed = 0.03f;


        public AiTaskHiveWorkerFulfillPushRequest(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.entity = entity;

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);

            pushModSystem = entity.Api.ModLoader.GetModSystem<PushLogisticsModSystem>();
            membership = entity.Api.ModLoader.GetModSystem<LocustHivesModSystem>().Membership;
            assignments = pushModSystem.Assignments;
        }

        public override void AfterInitialize()
        {
            base.AfterInitialize();
            member = entity.GetAs<IHiveMember>();
            worker = entity.GetAs<ILogisticsWorker>();
        }

        public override bool ShouldExecute()
        {
            if (worker == null ||
                !assignments.TryGetValue(worker, out request)) return false;

            return false;
        }

        public override void StartExecute()
        {
            if (request == null) return;
            base.StartExecute();

            pathfindingActive = true;

            pathTraverser.NavigateTo_Async(
                request.From.Position,
                moveSpeed,
                1.0f,
                OnGoalReached,
                OnStuck,
                OnNoPath
            );
        }


        public override bool ContinueExecute(float dt)
        {
            if(!base.ContinueExecute(dt)) return false;

            // Finish if no longer pathfinding, or the request is no longer active.
            return pathfindingActive && request.Active ;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            pathfindingActive = false;

            var inrange = entity.Pos.InRangeOf(request.From.Position.ToVec3f(), 1.5f);
            if (!cancelled && inrange)
            {
                var targetSlot = worker.Inventory.Where( slot =>
                {
                    return slot.Itemstack.Item == request.Stack.Item && slot.Itemstack.StackSize >= request.Stack.StackSize;
                }).FirstOrDefault();
                if (targetSlot != null)
                {
                    ItemSlot sourceSlot = request.From.Inventory.GetBestSuitedSlot(targetSlot).slot;
                    if (sourceSlot != null)
                    {
                        var leftover = sourceSlot.TryPutInto(entity.Api.World, targetSlot, request.Stack.StackSize);
                        if (leftover < request.Stack.StackSize)
                        {
                            // We put something in
                            request.Complete();
                            // For now, let's consider partial success as success.
                        }
                        else
                        {
                            request.Fail();
                        }
                        sourceSlot.MarkDirty();
                        targetSlot.MarkDirty();
                    }
                }
            }

        }

        private void OnGoalReached()
        {
            pathfindingActive = false;
        }

        private void OnStuck()
        {
            pathfindingActive = false;
        }

        private void OnNoPath()
        {
            pathfindingActive = false;
        }

    }
}
