using LocustLogistics.Core.EntityBehaviors;
using Newtonsoft.Json.Linq;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LocustLogistics.Core
{
    /// <summary>
    /// Handles code-based patching of entity assets to add hive functionality to locusts.
    /// Uses code searching instead of index-based patching for better compatibility with other mods.
    /// </summary>
    public class EntityPatchingSystem : ModSystem
    {
        public override void AssetsFinalize(ICoreAPI api)
        {
            // Only run on server side to prevent double-patching
            if (api.Side != EnumAppSide.Server)
            {
                return;
            }

            PatchLocustEntity(api);
        }

        private void PatchLocustEntity(ICoreAPI api)
        {
            // Find the locust-hacked entity
            var locustEntity = api.World.EntityTypes.FirstOrDefault(e =>
                e.Code.Path.Contains("locust") &&
                e.Variant.ContainsKey("state") &&
                e.Variant["state"] == "hacked");

            if (locustEntity == null)
            {
                api.Logger.Warning("[LocustLogistics] Could not find locust-hacked entity to patch");
                return;
            }

            // Find and patch the taskai behavior
            PatchTaskAiBehavior(api, locustEntity);
        }

        private void PatchTaskAiBehavior(ICoreAPI api, EntityProperties entity)
        {
            var serverBehaviors = entity.Server?.Behaviors;
            if (serverBehaviors == null) return;

            // Find the taskai behavior by searching for its code
            var taskAiBehavior = serverBehaviors.FirstOrDefault(b => b is EntityBehaviorTaskAI);

            if (taskAiBehavior == null)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai behavior in locust-hacked entity");
                return;
            }


            // Get or create the aitasks array
            var aiTasksArray = entity.Attributes["aiTasks"]?.AsArray();

            if(aiTasksArray == null)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai task array in locust-hacked entity");
                return;
            }

            // Create the returnToNest task
            var returnToNestTask = new JObject
            {
                ["code"] = "returnToNest",
                ["priority"] = 0.5,
                ["mincooldown"] = 5000,
                ["maxcooldown"] = 15000
            };

            aiTasksArray.Append(new JsonObject(returnToNestTask));

            api.Logger.Notification("[LocustLogistics] Added returnToNest AI task to locust-hacked entity");
        }
    }
}
