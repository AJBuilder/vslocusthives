using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.Essentials;
using Vintagestory.GameContent;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using LocustHives.Systems.Logistics;

namespace LocustHives.Game.Logistics.Locust
{

    public class AiTaskLocustLogisticsOperation : AiTaskBase
    {

        EntityBehaviorLocustLogisticsWorker worker;
        AccessTask curTask;
        bool pathfindingActive;

        float moveSpeed = 0.03f;


        public AiTaskLocustLogisticsOperation(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.entity = entity;

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
        }

        public override void AfterInitialize()
        {
            base.AfterInitialize();
            worker = entity.GetAs<EntityBehaviorLocustLogisticsWorker>();
            worker.TasksCancelled += () =>
            {
                pathfindingActive = false;
                pathTraverser.Stop();
            };
        }

        public override bool ShouldExecute()
        {
            return worker.AccessTasks.TryPeek(out curTask);
        }

        public override void StartExecute()
        {
            base.StartExecute();

            pathfindingActive = pathTraverser.NavigateTo_Async(
                Systems.Logistics.Util.GetTargetPosForMethod(curTask.method).ToVec3d().Add(0.5f, -0.5f, 0.5f),
                moveSpeed,
                0.5f,
                OnGoalReached,
                OnStuck,
                OnNoPath
            );
        }


        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt)) return false;

            // Finish if no longer pathfinding.
            return pathfindingActive;
        }

        public override void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            pathfindingActive = false;
        }

        private void OnGoalReached()
        {
            pathfindingActive = false;
            curTask.TryDo(worker, entity.Api.World);
            worker.AccessTasks.Dequeue();
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
