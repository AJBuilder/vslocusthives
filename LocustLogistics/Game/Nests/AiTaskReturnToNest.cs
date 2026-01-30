using LocustHives.Systems.Nests;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using LocustHives.Game.Core;

namespace LocustHives.Game.Locust
{
    public class AiTaskReturnToNest : AiTaskBase, IAiTask
    {
        bool pathfindingActive;

        ILocustNest targetNest;
        TuningSystem tuningSystem;

        IHiveMember member;

        float moveSpeed = 0.02f;


        public AiTaskReturnToNest(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            this.entity = entity;
            entity.Attributes.SetLong("unstoredMs", 0);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);

            tuningSystem = entity.Api.ModLoader.GetModSystem<TuningSystem>();
        }

        public override void AfterInitialize()
        {
            base.AfterInitialize();
            member = entity as IHiveMember;
            if (member == null)
            {
                member = entity
                        .SidedProperties
                        .Behaviors
                        .OfType<IHiveMember>()
                        .FirstOrDefault();
            }
        }

        public override bool ShouldExecute()
        {
            if (member == null ||
                entity.WatchedAttributes.HasAttribute("guardedPlayerUid") ||
                entity.WatchedAttributes.HasAttribute("guardedEntityId") ||
                entity.Attributes.GetLong("unstoredMs") + 10000 > entity.Api.World.ElapsedMilliseconds ||
                cooldownUntilMs > entity.World.ElapsedMilliseconds ||
                !tuningSystem.GetMembershipOf(member, out var hive)) return false;

            // Find nearest nest with room
            ILocustNest nearest = null;
            double minDistSq = double.MaxValue;

            foreach (var nest in tuningSystem.GetMembersOf(hive).OfType<ILocustNest>())
            {
                //if (!nest.HasRoom) continue;

                double distSq = entity.ServerPos.DistanceTo(nest.Position);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = nest;
                }
            }

            targetNest = nearest;
            return targetNest != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();

            pathfindingActive = true;

            pathTraverser.NavigateTo_Async(
                targetNest.Position,
                moveSpeed,
                3.0f, // TODO: Will need changed when storing is reimplemented
                OnGoalReached,
                OnStuck
            );
        }


        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt)) return false;

            // Bail if targetNest is no longer part of the hive, this entity is no longer a member, or their membership no longer matches.
            // Note: targetNest itself is an ILocustNest, not an IHiveMember handle, so we need to get the handle
            // This is a problem - we can't get the handle from just the ILocustNest interface
            // For now, just check if the member is still in a hive
            var thisIsTuned = tuningSystem.GetMembershipOf(member, out var memberHive);
            if (!thisIsTuned) return false;

            // Finish if no longer pathfinding.
            return pathfindingActive;
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();
            pathfindingActive = false;


            //var inrange = entity.Pos.InRangeOf(targetNest.Position.ToVec3f(), 1.5f);
            //if (!cancelled && inrange)
            //{
            //    // Check again if the target nest still exists.
            //    var nestIsTuned = nestMembership.TryGetValue(targetNest, out var nestHive);
            //    var thisIsTuned = membership.TryGetValue(member, out var memberHive);
            //    if (nestIsTuned && thisIsTuned && nestHive == memberHive)
            //    {
            //        targetNest?.TryStoreLocust(entity as EntityLocust);
            //    }
            //}

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

    }
}
