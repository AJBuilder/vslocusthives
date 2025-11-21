using LocustLogistics.Core.EntityBehaviors;
using LocustLogistics.Core.Interfaces;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.Essentials;
using Vintagestory.GameContent;

namespace LocustLogistics.Core.AiTasks
{
    public class AiTaskReturnToNest : IAiTask
    {
        float priority;
        EntityAgent entity;
        AnimationMetaData travellingAnimation;
        WaypointsTraverser pathTraverser;
        ILocustNest targetNest;
        bool pathfindingActive;

        public string Id => "returnToNest";
        public int Slot => 0;
        public float Priority => priority;
        public float PriorityForCancel => priority;
        public string ProfilerName { get; set; }

        public AiTaskReturnToNest(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
        {
            this.entity = entity;
            priority = taskConfig["priority"].AsFloat(0.5f);

            JsonObject travellingAnimationCode = taskConfig["travellingAnimation"];
            if (travellingAnimationCode.Exists)
            {
                var code = travellingAnimationCode.AsString()?.ToLowerInvariant();
                travellingAnimation = this.entity.Properties.Client.Animations
                    .FirstOrDefault(a => a.Code == code);
            }

            pathTraverser = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser;
        }

        public void AfterInitialize()
        {
        }

        public bool ShouldExecute()
        {
            var behavior = entity.GetBehavior<EntityBehaviorHiveTunable>();
            if (behavior?.Hive == null) return false;

            // Find nearest nest with room
            ILocustNest nearest = null;
            double minDistSq = double.MaxValue;

            foreach (var nest in behavior.Hive.Nests)
            {
                if (!nest.HasRoom) continue;

                double distSq = entity.ServerPos.SquareDistanceTo(nest.Position);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = nest;
                }
            }

            targetNest = nearest;
            return targetNest != null;
        }

        public void StartExecute()
        {
            if (targetNest == null) return;

            pathfindingActive = true;

            if (travellingAnimation != null)
            {
                entity.AnimManager?.StartAnimation(travellingAnimation);
            }

            pathTraverser.NavigateTo_Async(
                targetNest.Position,
                1.0f,  // minDist
                1.1f,  // maxDist
                OnGoalReached,
                OnStuck
            );
        }

        public bool CanContinueExecute()
        {
            return pathfindingActive && targetNest != null && targetNest.HasRoom;
        }

        public bool ContinueExecute(float dt)
        {
            return pathfindingActive;
        }

        public void FinishExecute(bool cancelled)
        {
            pathTraverser.Stop();
            pathfindingActive = false;

            if (travellingAnimation != null)
            {
                entity.AnimManager?.StopAnimation(travellingAnimation.Code);
            }

            if (!cancelled && targetNest != null)
            {
                targetNest.TryStoreLocust(entity as EntityLocust);
            }

            targetNest = null;
        }

        private void OnGoalReached()
        {
            pathfindingActive = false;
        }

        private void OnStuck()
        {
            pathfindingActive = false;
        }

        public bool Notify(string key, object data)
        {
            return false;
        }

        public void OnEntityDespawn(EntityDespawnData reason)
        {
        }

        public void OnEntityHurt(DamageSource source, float damage)
        {
        }

        public void OnEntityLoaded()
        {
        }

        public void OnEntitySpawn()
        {
        }

        public void OnStateChanged(EnumEntityState beforeState)
        {
        }
    }
}
