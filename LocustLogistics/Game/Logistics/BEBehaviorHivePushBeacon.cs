using LocustHives.Game.Core;
using LocustHives.Game.Util;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace LocustHives.Game.Logistics
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


    public class BEBehaviorHivePushBeacon : BlockEntityBehavior
    {
        BlockFacing facing;
        LogisticsSystem logisticsSystem;
        CoreSystem coreSystem;
        List<LogisticsPromise> requests;
        int clientRequestCount;

        public ILogisticsStorage AttachedStorage
        {
            get
            {
                if (facing == null) return null;
                BlockPos targetPos = Pos.AddCopy(facing.Opposite);
                var be = Api.World.BlockAccessor.GetBlockEntity(targetPos);
                return be?.GetAs<IHiveTunable>()?.HiveMembershipHandle as ILogisticsStorage;
            }
        }

        ILogisticsNetwork Network
        {
            get
            {
                var storage = AttachedStorage;
                if (storage is IHiveMember member && coreSystem.GetHiveOf(member, out var hive))
                {
                    return logisticsSystem.GetNetworkFor(hive.Id);
                }
                else
                {
                    return null;
                }
            }
        }


        public BEBehaviorHivePushBeacon(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            coreSystem = api.ModLoader.GetModSystem<CoreSystem>();
            if(api is ICoreServerAPI)
            {
                requests = new List<LogisticsPromise>();

                var facingCode = properties["facingCode"].AsString();
                facing = BlockFacing.FromCode(Blockentity.Block.Variant[facingCode]);

                logisticsSystem = api.ModLoader.GetModSystem<LogisticsSystem>();
            }
        }


        public void PushAll()
        {
            var storage = AttachedStorage;
            if (storage == null) return;

            CancelAll();
            foreach (var stack in storage.Stacks)
            {
                if(stack != null) TryPush(storage, stack);
            }
        }

        public void CancelAll()
        {
            requests.ForEach(r => r.Cancel());
            Blockentity.MarkDirty();
        }

        private bool TryPush(ILogisticsStorage storage, ItemStack stack)
        {
            var request = Network?.Request(stack.CloneWithSize(-stack.StackSize), storage);
            if (request == null) return false;

            request.CompletedEvent += (state) =>
            {
                requests.Remove(request);
                Blockentity.MarkDirty();
            };

            requests.Add(request);
            Blockentity.MarkDirty();
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api is ICoreServerAPI)
            {
                CancelAll();
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded(); ;
            if (Api is ICoreServerAPI)
            {
                CancelAll();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("requestCount", requests.Count);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            clientRequestCount = tree.GetInt("requestCount");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Active requests: {clientRequestCount}");
        }
    }
}
