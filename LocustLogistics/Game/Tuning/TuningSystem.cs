using HarmonyLib;
using LocustHives.Game.Logistics;
using LocustHives.Game.Nest;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Membership;
using LocustHives.Systems.Nests;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustHives.Game.Core
{
    /// <summary>
    /// This mod system tracks locustthat are tuned to a hive.
    /// </summary>
    public class TuningSystem : ModSystem
    {
        int nextHiveId;
        MembershipRegistry<IHiveMember> baseRegistry;
        public IMembershipRegistry<IHiveMember> Membership => baseRegistry;

        public override void Start(ICoreAPI api)
        {
            api.RegisterItemClass("ItemHiveTuner", typeof(ItemHiveTuner));
            api.RegisterEntityBehaviorClass("hivetunable", typeof(EntityBehaviorHiveTunable));
            api.RegisterBlockEntityBehaviorClass("HiveTunable", typeof(BEBehaviorLocustHiveTunable));

            baseRegistry = new MembershipRegistry<IHiveMember>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            //api.Event.RegisterGameTickListener((dt) =>
            //{
            //    // Iterate over current queue (or max of 10) and try to assign.
            //    // TODO: Better optimization than capping number of processed requests to 10.
            //    int count = Math.Min(queuedRequests.Count, 10);
            //    for (int i = 0; i < count; i++)
            //    {
            //        AssignOrQueuePromise(queuedRequests.Dequeue());
            //    }
            //}, 3000);

            api.Event.GameWorldSave += () =>
            {
                api.WorldManager.SaveGame.StoreData("LocustHivesNextHiveId", nextHiveId);
            };
            api.Event.SaveGameLoaded += () =>
            {
                nextHiveId = api.WorldManager.SaveGame.GetData<int>("LocustHivesNextHiveId");
            };
        }

        public void Tune(IHiveMember locust, int? hiveId)
        {
            var prevId = baseRegistry.AssignMembership(locust, hiveId);

            // Set the next id if it somehow fell out of sync (or this is the client)
            if(hiveId.HasValue && hiveId.Value > nextHiveId) nextHiveId = hiveId.Value + 1;

            locust.OnTuned?.Invoke(prevId, hiveId);
        }

        /// <summary>
        /// Creates a new hive that doesn't exist yet.
        /// Should only be called server side
        /// Note: Not a perfect allocator as there is no explicit check.
        ///       Not guaranteed to work after int.MaxValue memberships
        ///       have been ever touched.
        /// </summary>
        /// <returns></returns>
        public int CreateHive()
        {
            // Post increment for the next time.
            return nextHiveId++;
        }

    }
}
