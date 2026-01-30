using LocustHives.Game.Logistics.Locust;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics.Core.Interfaces;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace LocustHives.Game.Locust
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
                    entity.SelectionBox.XSize,
                    OnGoalReached,
                    OnStuck,
                    OnNoPath,
                    999,
                    0
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
                curTask.Value.TryDo(worker);
                worker.DidCurrentTask();
            }
        }

        private void OnStuck()
        {
            pathTraverser.Stop();
            seekingAccess = false;
        }

        private void OnNoPath()
        {
            pathTraverser.Stop();
            seekingAccess = false;
        }

    }
}
