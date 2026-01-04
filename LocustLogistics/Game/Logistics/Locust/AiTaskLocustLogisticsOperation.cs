using LocustHives.Game.Util;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.Client.NoObf;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace LocustHives.Game.Logistics.Locust
{

    public class AiTaskLocustLogisticsOperation : AiTaskBase
    {

        EntityBehaviorLocustLogisticsWorker worker;
        AccessTask? curTask;
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
            curTask = worker.CurrentAccessTask;
            return curTask.HasValue;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            if (curTask.HasValue && curTask.Value.method is IInWorldStorageAccessMethod method)
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

            // If the cur task has changed, stop.
            if (!curTask.Equals(worker.CurrentAccessTask))
            {
                pathTraverser.Stop();
                seekingAccess = false;
            }

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
            if (curTask.HasValue)
            {
                curTask.Value.TryDo(worker, entity.Api.World);
                worker.DidCurrentTask();
            }
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
