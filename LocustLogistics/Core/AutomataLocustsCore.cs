using LocustLogistics.Core.AiTasks;
using LocustLogistics.Core.BlockEntities;
using LocustLogistics.Core.EntityBehaviors;
using LocustLogistics.Core.Interfaces;
using LocustLogistics.Core.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
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
        public event Action<LocustHive, IHiveMember> MemberTuned;
        public event Action<LocustHive, IHiveMember> MemberDetuned;

        int nextId;
        Dictionary<int, LocustHive> hives = new Dictionary<int, LocustHive>();

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));
            api.RegisterEntityBehaviorClass("hivetunable", typeof(EntityBehaviorHiveTunable));
            api.RegisterBlockEntityClass("TamedLocustNest", typeof(BETamedLocustNest));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            AiTaskRegistry.Register<AiTaskReturnToNest>("returnToNest");
        }

        public LocustHive GetHive(int id)
        {
            return hives[id];
        }

        /// <summary>
        /// Creates a new hive with the given member
        /// </summary>
        /// <param name="firstMember"></param>
        /// <returns></returns>
        /// <exception cref="OutOfMemoryException"></exception>
        public LocustHive CreateHive(IHiveMember firstMember)
        {
            // Find the next free id.
            // We probably don't need to worry about checking if all keys are taken
            // since that's tens of GB of RAM and you'd have bigger problems than
            // not being able to make a new hive... I like the check for completeness though. :)
            if (hives.Count == int.MaxValue) throw new OutOfMemoryException("Cant make any more hives.");
            while (hives.ContainsKey(nextId))
                nextId++;

            var hive = new LocustHive(nextId++, 0); // Post increment for the next time.
            hives[hive.Id] = hive;
            hive.MemberTuned += (member) => MemberTuned?.Invoke(hive, member);
            hive.MemberDetuned += (member) => {
                MemberDetuned?.Invoke(hive, member);
                if (hive.Count == 0) hives.Remove(hive.Id); // If the hive no longer has members, release it's reference.
            };
            hive.Tune(firstMember);
            return hive;
        }

    }
}
