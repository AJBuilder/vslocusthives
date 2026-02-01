using LocustHives.Game.Core;
using LocustHives.Game.Logistics.Lattice;
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
        LogisticsSystem logisticsSystem;
        CoreSystem coreSystem;
        List<LogisticsPromise> promises;
        int clientPromisedAmount;

        long? queuedInventoryCheck;

        public ItemStack TrackedItem
        {
            get => trackedItem;
            set
            {
                trackedItem = value;
                Blockentity.MarkDirty();

                // Any modification to the tracked item will have a buffer.
                // This allows small incremental changes.
                // Not sure I like this logic here as it's for human buffering? It should be in the BlockBheavior
                // but implementing that is more effort so eh for now.
                if (queuedInventoryCheck.HasValue) Api.Event.UnregisterCallback(queuedInventoryCheck.Value);
                queuedInventoryCheck = Api.Event.RegisterCallback((dt) => {
                    queuedInventoryCheck = null;
                    CheckInventoryLevel();
                }, 3000);
            }
        }

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

        public uint CurrentLevel => (uint)(AttachedStorage?.Stacks.Where(s => s.Satisfies(trackedItem)).Sum(s => Math.Max(0, s.StackSize)) ?? 0);

        public BEBehaviorHiveStorageRegulator(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (trackedItem != null && !trackedItem.ResolveBlockOrItem(Api.World)) trackedItem = null;

            coreSystem = api.ModLoader.GetModSystem<CoreSystem>();
            if (api is ICoreServerAPI serverAPI)
            {
                promises = new List<LogisticsPromise>();

                var facingCode = properties["facingCode"].AsString();
                facing = BlockFacing.FromCode(Blockentity.Block.Variant[facingCode]);

                logisticsSystem = api.ModLoader.GetModSystem<LogisticsSystem>();

                Blockentity.RegisterGameTickListener((dt) =>
                {
                    CheckInventoryLevel();
                }, 15000);
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

            var need = (int)targetCount - (int)currentLevel;

            var promised = promises
                    .Where(p => p.State == LogisticsPromiseState.Unfulfilled)
                    .Sum(p => p.Stack.StackSize);

            // If sign of the need changed (i.e. promised and need have different signs)
            // Cancel all promises. This assumes that all promises are of the same sign, which this
            // logic should guarantee?
            // If the need is zero, this also cancels promises
            if((need == 0) || (need > 0 != promised > 0)) {
                while(promises.Count > 0) promises.First().Cancel();
                promised = 0;
            }

            var adjusted = need - promised;
            if(adjusted != 0)
            {


                if(adjusted != 0)
                {
                    var stack = trackedItem.CloneWithSize(adjusted);

                    if (storage is IHiveMember member && coreSystem.GetHiveOf(member, out var hive))
                    {
                        var promise = logisticsSystem.GetNetworkFor(hive.Id)?.Request(stack, AttachedStorage);
                        if (promise != null)
                        {
                            promise.CompletedEvent += (state) =>
                            {
                                promises.Remove(promise);
                                Blockentity.MarkDirty();
                                if(state == LogisticsPromiseState.Fulfilled) CheckInventoryLevel();
                            };
                            promises.Add(promise);
                            Blockentity.MarkDirty();
                        }
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
