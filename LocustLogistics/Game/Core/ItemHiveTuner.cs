using LocustHives.Game.Util;
using LocustHives.Systems.Membership;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LocustHives.Game.Core
{
    enum HiveTunerMode
    {
        Calibrate,
        Tune,
        Zero,
    }
    public class ItemHiveTuner : Item
    {
        CoreSystem coreSystem;
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            coreSystem = api.ModLoader.GetModSystem<CoreSystem>();
            ICoreClientAPI capi = api as ICoreClientAPI;


            toolModes = ObjectCacheUtil.GetOrCreate(api, "logiTunerToolModes", () =>
            {
                SkillItem[] modes;

                // Modes
                // 1. Calibrate
                // 2. Tune
                // 3. Zero
                modes = new SkillItem[3];
                modes[(int)HiveTunerMode.Calibrate] = new SkillItem() { Code = new AssetLocation("calibrate"), Name = "Calibrate" };
                modes[(int)HiveTunerMode.Tune] = new SkillItem() { Code = new AssetLocation("tune"), Name = "Tune" };
                modes[(int)HiveTunerMode.Zero] = new SkillItem() { Code = new AssetLocation("zero"), Name = "Zero" };

                if (capi != null)
                {
                    modes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("locusthives:textures/icons/zero.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[0].TexturePremultipliedAlpha = false;

                    modes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("locusthives:textures/icons/zero.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[1].TexturePremultipliedAlpha = false;

                    modes[2].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("locusthives:textures/icons/zero.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                    modes[2].TexturePremultipliedAlpha = false;
                }

                return modes;
            });

        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IHiveTunable target = null;
            bool canBeTuned = true;
            if (blockSel != null)
            {
                BlockPos onBlockPos = blockSel.Position;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                if (byPlayer == null) return;

                if (byEntity.World.Claims.TryAccess(byPlayer, onBlockPos, EnumBlockAccessFlags.BuildOrBreak))
                {
                    BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos);
                    if(be != null)
                    {
                        canBeTuned = be.Block.Attributes?["canBeTuned"].AsBool(true) ?? true;
                        target = be.GetAs<IHiveTunable>();
                    }
                }
            }
            else if (entitySel != null)
            {
                canBeTuned = entitySel.Entity.Attributes?.GetBool("canBeTuned", true) ?? true;
                target = entitySel.Entity.GetAs<IHiveTunable>();
            }

            var attributes = slot.Itemstack.Attributes;
            var mode = (HiveTunerMode)Math.Min(toolModes.Length - 1, attributes.GetInt("toolMode"));

            if (target != null) {
                handling = EnumHandHandling.PreventDefaultAction;
                var handle = target.HiveMembershipHandle;

                switch (mode)
                {
                    case HiveTunerMode.Calibrate:
                        {
                            if (coreSystem.GetHiveOf(handle, out var hive))
                            {
                                if (api is ICoreClientAPI capi)
                                {
                                    capi.ShowChatMessage($"Calibrated to Hive {hive.Name}");
                                }
                                attributes.SetInt("calibratedHive", (int)hive.Id);
                            }
                            else if (api is ICoreClientAPI capi)
                            {
                                capi.TriggerIngameError(this, "No target hive", "Target is not tuned to a Hive.");
                            }
                        }
                        break;
                    case HiveTunerMode.Tune:
                        {
                            var hiveId = attributes.TryGetInt("calibratedHive");
                            if (hiveId.HasValue)
                            {
                                if (canBeTuned)
                                {
                                    if(coreSystem.GetHiveOf((uint)hiveId.Value, out var hive)){
                                        if(api is ICoreClientAPI capi)
                                        {
                                            capi.ShowChatMessage($"Tuned to Hive {hive.Name} ({(uint)hiveId.Value})");
                                        }
                                        hive.Tune(handle);
                                    } else if(api is ICoreClientAPI capi)
                                    {
                                        capi.ShowChatMessage("Calibrated hive no longer exists.");
                                    }
                                } else if (api is ICoreClientAPI capi)
                                {
                                    capi.TriggerIngameError(this, "Untunable", "The tuning of this cannot be changed.");
                                }

                                // Not sure I like this logic here...
                                // Let's clear the guarded Player/Entity when tuning to a new hive
                                entitySel?.Entity.WatchedAttributes.RemoveAttribute("guardedPlayerUid");
                                entitySel?.Entity.WatchedAttributes.RemoveAttribute("guardedEntityId");
                                entitySel?.Entity.WatchedAttributes.RemoveAttribute("guardedName");
                            }
                            else if (api is ICoreClientAPI capi)
                            {
                                capi.TriggerIngameError(this, "No calibrated hive", "Not calibrated to any Hive.");
                            }
                        }
                        break;
                    case HiveTunerMode.Zero:
                        {
                            if (canBeTuned)
                            {
                                coreSystem.Zero(handle);
                                if (api is ICoreClientAPI capi) capi.TriggerIngameDiscovery(this, "Zeroed target", $"Zeroed tuning.");

                                // Not sure I like this logic here...
                                // But let's just set the guardedPlayer or guardedEntity whenever we zero
                                var player = byEntity as EntityPlayer;
                                if (player != null) entitySel?.Entity.WatchedAttributes.SetString("guardedPlayerUid", player.PlayerUID);
                                else entitySel?.Entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                                entitySel?.Entity.WatchedAttributes.SetString("guardedName", byEntity.GetName() ?? "");
                            } else if (api is ICoreClientAPI capi)
                            {
                                capi.TriggerIngameError(this, "Unzeroable", "This cannot be zeroed");
                            }
                        }
                        break;
                }
            }
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return Math.Min(toolModes.Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            var id = inSlot.Itemstack.Attributes.TryGetInt("calibratedHive");
            if(id.HasValue && coreSystem.GetHiveOf((uint)id, out var hive))
            {
                dsc.AppendLine($"Calibrated Hive: {hive.Name}");
            }
            else
            {
                dsc.AppendLine("Calibrated hive no longer exists");
            }
        }
    }
}
