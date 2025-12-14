using LocustLogistics.Logistics.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace LocustLogistics.Logistics.Retrieval
{
    public class BEBehaviorHivePushBeacon : BEBehaviorHiveAccessPort
    {
        BlockFacing facing;
        PushLogisticsModSystem modSystem;
        List<PushRequest> requests;

        IEnumerable<PushRequest> PushRequests => requests;

        IHiveStorage AttachedStorage
        {
            get
            {
                if (facing == null) return null;
                BlockPos targetPos = Pos.AddCopy(facing);
                var be = Api.World.BlockAccessor.GetBlockEntity(targetPos);
                if (be is IHiveStorage storage) return storage;
                return be.Behaviors.OfType<IHiveStorage>().FirstOrDefault();
            }
        }

        public BEBehaviorHivePushBeacon(BlockEntity blockentity) : base(blockentity)
        {
            requests = new List<PushRequest>();
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            var facingCode = properties["facingCode"].AsString("orientation");
            facing = BlockFacing.FromCode(facingCode);
        }


        public void PushAll()
        {
            var storage = AttachedStorage;
            var inventory = storage?.Inventory;
            if (storage == null || inventory == null) return;

            CancelAll();
            foreach (var slot in inventory)
            {
                Push(slot.Itemstack);
            }
        }

        public void CancelAll()
        {
            requests.ForEach(r => r.Cancel());
        }

        private void Push(ItemStack stack)
        {
            var request = modSystem.Request(stack, this);
            request.CompletedEvent += () =>
            {
                requests.Remove(request);
            };
            request.FailedEvent += () =>
            {
                requests.Remove(request);
                Push(stack);
            };
            request.CancelledEvent += () =>
            {
                requests.Remove(request);
            };

            requests.Add(request);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            CancelAll();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            CancelAll();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"Active requests: {requests.Count}");
        }
    }
}
