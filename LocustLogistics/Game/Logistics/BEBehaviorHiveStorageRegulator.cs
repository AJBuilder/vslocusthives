using LocustHives.Game.Util;
using LocustHives.Systems.Logistics;
using LocustHives.Systems.Logistics.Core;
using LocustHives.Systems.Logistics.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace LocustHives.Game.Logistics
{
    public class BEBehaviorHiveStorageRegulator : BlockEntityBehavior, IStorageRegulator
    {
        ItemStack trackedItem;
        BlockFacing facing;
        LogisticsSystem modSystem;
        List<LogisticsPromise> promises;
        int clientPromisedAmount;

        public ItemStack TrackedItem
        {
            get => trackedItem;
            set
            {
                trackedItem = value;
                Blockentity.MarkDirty();
                CheckInventoryLevel();
            }
        }

        public ILogisticsStorage AttachedStorage
        {
            get
            {
                if (facing == null) return null;
                BlockPos targetPos = Pos.AddCopy(facing.Opposite);
                var be = Api.World.BlockAccessor.GetBlockEntity(targetPos);
                return be?.GetAs<IBlockHiveStorage>()?.LogisticsStorage;
            }
        }

        public BEBehaviorHiveStorageRegulator(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (trackedItem != null && !trackedItem.ResolveBlockOrItem(Api.World)) trackedItem = null;

            if (api is ICoreServerAPI serverAPI)
            {
                promises = new List<LogisticsPromise>();

                var facingCode = properties["facingCode"].AsString();
                facing = BlockFacing.FromCode(Blockentity.Block.Variant[facingCode]);

                modSystem = api.ModLoader.GetModSystem<LogisticsSystem>();

                Blockentity.RegisterGameTickListener((dt) =>
                {
                    CheckInventoryLevel();
                }, 3000);
            }
        }

        private void CheckInventoryLevel()
        {
            if (trackedItem == null) return;

            var storage = AttachedStorage;
            if(storage == null) return;

            var stacks = storage.Stacks;

            // Get current level
            uint currentLevel = (uint)stacks.Where(s => s.Satisfies(trackedItem)).Sum(s => Math.Max(0, s.StackSize));

            var targetCount = (uint)Math.Max(0, trackedItem.StackSize);

            var promised = promises
                    .Where(p => p.State == LogisticsPromiseState.Unfulfilled)
                    .Sum(p => p.Stack.StackSize);

            var need = (int)targetCount - (int)currentLevel - promised;
            if(need != 0)
            {
                var stack = trackedItem.CloneWithSize(need);

                if (modSystem.StorageMembership.GetMembershipOf(storage, out var hiveId))
                {
                    var promise = modSystem.GetNetworkFor(hiveId)?.Request(stack, AttachedStorage);
                    if (promise != null)
                    {
                        promise.CompletedEvent += (state) =>
                        {
                            promises.Remove(promise);
                            Blockentity.MarkDirty();
                            CheckInventoryLevel();
                        };
                        promises.Add(promise);
                        Blockentity.MarkDirty();
                    }
                }

            }
        }

        public void Cleanup()
        {
            if(Api is ICoreServerAPI)
            {
                while(promises.Count > 0) promises.First().Cancel();
            }
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (Api is ICoreServerAPI)
            {
                Cleanup();
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            if (Api is ICoreServerAPI)
            {
                Cleanup();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (trackedItem != null)
            {
                tree.SetItemstack("trackedItem", trackedItem);
            }
            tree.SetInt("promisedAmount", promises?.Sum(p => p.Stack.StackSize) ?? 0);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            trackedItem = tree.GetItemstack("trackedItem");
            if(Api != null && trackedItem != null && !trackedItem.ResolveBlockOrItem(Api.World)) trackedItem = null;
            clientPromisedAmount = tree.GetInt("promisedAmount");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (trackedItem != null)
            {
                dsc.AppendLine($"Tracking: {trackedItem.GetName()}");
                dsc.AppendLine($"Target: {Math.Max(0, trackedItem.StackSize)}");
                var storage = AttachedStorage;
                var accessMethod = storage?.AccessMethods?.FirstOrDefault();
                if (accessMethod != null)
                {
                    uint currentLevel = accessMethod.CanDo(trackedItem.CloneWithSize(-1));
                    dsc.AppendLine($"Current: {currentLevel}");
                }
                dsc.AppendLine($"Active promises: {clientPromisedAmount}");
            }
            else
            {
                dsc.AppendLine("Not configured");
            }
        }
    }
}
