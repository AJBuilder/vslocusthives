using LocustHives.Game.Core;
using LocustHives.Game.Nexus;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

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

        var m = HiveMembershipHandle;
        if(!coreSystem.GetHiveOf(m, out var hive))
        {
            hive = coreSystem.CreateHive();
            hive.Tune(m);
        }
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        coreSystem.Zero(HiveMembershipHandle);
    }
}