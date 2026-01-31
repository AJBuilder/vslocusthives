using System.Text;
using LocustHives.Game.Core;
using LocustHives.Game.Nexus;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

public class BEBehaviorHiveNexus : BlockEntityBehavior, IHiveTunable
{
    CoreSystem coreSystem;
    public BEBehaviorHiveNexus(BlockEntity blockentity) : base(blockentity)
    {
    }

    public IHiveMember HiveMembershipHandle => new NexusMembership(Pos);


    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        coreSystem = api.ModLoader.GetModSystem<CoreSystem>();

        // If on the server and this doesn't have a hive (freshly placed), make one.
        var m = HiveMembershipHandle;
        if(api is ICoreServerAPI && !coreSystem.GetHiveOf(m, out var _))
        {
            coreSystem.CreateHive().Tune(m);
        }
    }
    
    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        coreSystem.Zero(HiveMembershipHandle);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        // Should always have a hive
        if(coreSystem.GetHiveOf(HiveMembershipHandle, out var hive))
        {
            dsc.AppendLine($"Hive: {hive.Name}");
        }
    }
}