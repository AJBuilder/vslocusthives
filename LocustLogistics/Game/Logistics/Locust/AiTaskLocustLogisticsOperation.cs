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
        bool seekingAccess;

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
                seekingAccess = false;
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

            if (curTask.method is IInWorldStorageAccessMethod method)
            {
                seekingAccess = pathTraverser.NavigateTo_Async(
                    method.Position,
                    moveSpeed,
                    0.5f,
                    OnGoalReached,
                    OnStuck,
                    OnNoPath
                );
            }
        }


        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt)) return false;

            // Finish if no longer seeking.
            return seekingAccess;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            seekingAccess = false;
        }

        private void OnGoalReached()
        {
            seekingAccess = false;
            curTask.TryDo(worker, entity.Api.World);
            worker.AccessTasks.Dequeue();
        }

        private void OnStuck()
        {
            seekingAccess = false;
        }

        private void OnNoPath()
        {
            seekingAccess = false;
        }

    }
}
