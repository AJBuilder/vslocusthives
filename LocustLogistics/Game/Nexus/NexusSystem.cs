using HarmonyLib;
using LocustHives.Game.Core;
using LocustHives.Game.Logistics;
using LocustHives.Game.Nest;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Membership;
using LocustHives.Systems.Nests;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#nullable disable warnings

namespace LocustHives.Game.Nexus
{

    /// <summary>
    /// This mod system tracks things that are tuned to a hive.
    /// </summary>
    public class NexusSystem : ModSystem
    {
        CoreSystem coreSystem;

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("HiveNexus", typeof(BEBehaviorHiveNexus));
            coreSystem = api.ModLoader.GetModSystem<CoreSystem>();
            coreSystem.RegisterMembershipType<NexusMembership>("locusthives:nexus", NexusMembership.ToBytes, (bytes) => NexusMembership.FromBytes(bytes, api));
        }

    }
}
