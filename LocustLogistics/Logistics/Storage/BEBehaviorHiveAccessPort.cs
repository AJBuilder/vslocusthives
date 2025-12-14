using LocustLogistics.Core;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LocustLogistics.Logistics.Storage
{
    //public enum StorageBeaconClientPacketId
    //{
    //    // General
    //    Enable,
    //    Disable,
    //
    //    // Storage mode
    //    EnterStorageMode,
    //    EnableSlot,
    //    DisableSlot,
    //    SetFilter,
    //
    //    // Push mode
    //    EnterPushMode,
    //    SetSlotToBePushed,
    //    UnsetSlotToBePushed,
    //
    //    // Pull mode
    //    EnterPullMode,
    //    PullStack,
    //    CancelPull,
    //
    //}
    //
    //public enum StorageBeaconServerPackedId
    //{
    //    CloseGui,
    //}



    public class BEBehaviorHiveAccessPort : BlockEntityBehavior, IHiveStorage
    {
        BlockFacing facing;
        public Vec3d Position => Blockentity.Pos.ToVec3d();

        public IInventory Inventory
        {
            get
            {
                if (facing == null) return null;
                BlockPos targetPos = Pos.AddCopy(facing);
                return (Api.World.BlockAccessor.GetBlockEntity(targetPos) as IBlockEntityContainer)?.Inventory;
            }
        }


        public BEBehaviorHiveAccessPort(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            var facingCode = properties["facingCode"].AsString("orientation");
            facing = BlockFacing.FromCode(facingCode);

            var tunableBehavior = Blockentity.GetBehavior<BEBehaviorHiveTunable>();
            if (tunableBehavior != null)
            {
                tunableBehavior.OnTuned += (prevHive, hive) =>
                {
                    api.ModLoader.GetModSystem<StorageLogisticsModSystem>().UpdateStorageHiveMembership(this, prevHive, hive);
                };
            }
        }

    }
}
