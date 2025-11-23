using HarmonyLib;
using LocustLogistics.Core.AiTasks;
using LocustLogistics.Core.BlockEntities;
using LocustLogistics.Core.Blocks;
using LocustLogistics.Core.EntityBehaviors;
using LocustLogistics.Core.Interfaces;
using LocustLogistics.Core.Items;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustLogistics.Core
{
    /// <summary>
    /// This core mod system has to do with the tuning and synchronizing of "members" within a "hive".
    /// </summary>
    public class AutomataLocustsCore : ModSystem
    {
        // Events
        public event Action<int?, IHiveMember> MemberTuned;

        int nextId;
        Dictionary<IHiveMember, int> allMembers = new Dictionary<IHiveMember, int>();
        Dictionary<int, HashSet<IHiveMember>> hiveMembers = new Dictionary<int, HashSet<IHiveMember>>();
        Dictionary<int, HashSet<BETamedLocustNest>> hiveNests = new Dictionary<int, HashSet<BETamedLocustNest>>();

        public IReadOnlyDictionary<IHiveMember, int> AllMembers => allMembers;

        public IReadOnlySet<IHiveMember> GetHiveMembers(int hive) => hiveMembers[hive];
        public IReadOnlySet<BETamedLocustNest> GetHiveNests(int hive) => hiveNests[hive];

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));
            api.RegisterEntityBehaviorClass("hivetunable", typeof(EntityBehaviorHiveTunable));
            api.RegisterBlockEntityClass("TamedLocustNest", typeof(BETamedLocustNest));
            api.RegisterBlockClass("BlockTamedLocustNest", typeof(BlockTamedLocustNest));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskReturnToNest>("returnToNest");
        }

        /// <summary>
        /// Add a member to a hive.
        /// Will trigger the MemberTuned event and call OnTuned for the member.
        /// </summary>
        /// <param name="hive"></param>
        /// <param name="member"></param>
        public void Tune(int? hive, IHiveMember member)
        {
            // If already tuned
            if (allMembers.TryGetValue(member, out int prevHive))
            {
                // Bail if same hive
                if (hive.HasValue && hive.Value == prevHive) return;

                // Delete all relationships
                allMembers.Remove(member);

                // Cleanup member caching
                if (hiveMembers.TryGetValue(prevHive, out var members))
                {
                    members.Remove(member);
                    if (members.Count == 0) hiveMembers.Remove(prevHive);
                }

                // Cleanup nest caching
                if (member is BETamedLocustNest nest)
                {
                    if (hiveNests.TryGetValue(prevHive, out var nests))
                    {
                        nests.Remove(nest);
                        if (nests.Count == 0) hiveNests.Remove(prevHive);
                    }
                }
            }

            if (hive.HasValue)
            {
                var val = hive.Value;
                allMembers[member] = val;

                // Cache reverse relationship
                if (!this.hiveMembers.TryGetValue(val, out var members))
                {
                    members = new HashSet<IHiveMember>();
                    this.hiveMembers[val] = members;
                }
                members.Add(member);

                if (member is BETamedLocustNest nest)
                {
                    if (!hiveNests.TryGetValue(val, out var nests))
                    {
                        nests = new HashSet<BETamedLocustNest>();
                        hiveNests[val] = nests;
                    }
                    nests.Add(nest);
                }
            }

            member.OnTuned(hive);
            MemberTuned?.Invoke(hive, member);
        }


        /// <summary>
        /// Creates a new hive that doesn't exist yet.
        /// Note: Not a perfect allocator as there is no explicit check.
        ///       Not guaranteed to work after int.MaxValue.
        /// </summary>
        /// <param name="firstMember"></param>
        /// <returns></returns>
        public int CreateHive()
        {
            // Get the next (hopefully) free id.
            while (hiveMembers.ContainsKey(nextId))
                nextId++;

            // Post increment for the next time.
            return nextId++;
        }
        public override void AssetsFinalize(ICoreAPI api)
        {
            // Only run on server side to prevent double-patching
            if (api.Side != EnumAppSide.Server)
            {
                return;
            }

            PatchHackedLocust(api);
        }

        private void PatchHackedLocust(ICoreAPI api)
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
            // Find the taskai behavior by searching for its code
            var taskAiBehavior = entity.Server?.BehaviorsAsJsonObj.FirstOrDefault(b =>
            {
                var code = b["code"];
                if (code.Exists)
                {
                    if ("taskai" == code.AsString()) return true;
                    
                }
                return false;
            });

            if (!taskAiBehavior.Exists)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai behavior in locust-hacked entity");
                return;
            }

            // Get the aitasks array
            var aiTasksArray = taskAiBehavior["aitasks"];

            if (!aiTasksArray.Exists)
            {
                api.Logger.Warning("[LocustLogistics] Could not find taskai task array in locust-hacked entity");
                return;
            }

            // Create the returnToNest task
            var taskConfig = new JObject
            {
                ["code"] = "returnToNest",
                ["priority"] = 0.5,
                ["mincooldown"] = 5000,
                ["maxcooldown"] = 15000,
                ["travellingAnimation"] = "walk"
            };

            (aiTasksArray.Token as JArray)?.Add(taskConfig);

            api.Logger.Notification("[LocustLogistics] Added returnToNest AI task to locust-hacked entity");
        }
    }
}
