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
        TuningSystem modSystem;
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            modSystem = api.ModLoader.GetModSystem<TuningSystem>();
            ICoreClientAPI capi = api as ICoreClientAPI;


            toolModes = ObjectCacheUtil.GetOrCreate(api, "logiTunerToolModes", () =>
            {
                SkillItem[] modes;

                // Modes
                // 1. Tune to nest
                // 2. Detune
                modes = new SkillItem[3];
                modes[(int)HiveTunerMode.Calibrate] = new SkillItem() { Code = new AssetLocation("calibrate"), Name = "Calibrate" };
                modes[(int)HiveTunerMode.Tune] = new SkillItem() { Code = new AssetLocation("tune"), Name = "Tune" };
                modes[(int)HiveTunerMode.Zero] = new SkillItem() { Code = new AssetLocation("zero"), Name = "Zero" };



                //if (capi != null)
                //{
                //    modes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/heatmap.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                //    modes[0].TexturePremultipliedAlpha = false;
                //    if (modes.Length > 1)
                //    {
                //        modes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/rocks.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                //        modes[1].TexturePremultipliedAlpha = false;
                //    }
                //}

                return modes;
            });

        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IHiveMember target = null;
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
                        target = be.GetAs<IHiveMember>();
                        if (target != null)
                        {
                            be.MarkDirty();
                        }
                    }
                }
            }
            else if (entitySel != null)
            {
                target = entitySel.Entity.GetAs<IHiveMember>();
            }

            var attributes = slot.Itemstack.Attributes;
            var mode = (HiveTunerMode)Math.Min(toolModes.Length - 1, attributes.GetInt("toolMode"));

            // If there is no target, and mode is calibrate, clear the calibration
            if (target == null)
            {
                if(mode == HiveTunerMode.Calibrate)
                {
                    handling = EnumHandHandling.PreventDefaultAction;
                    attributes.RemoveAttribute("calibratedHive");
                }
            }
            else
            {
                // If there is a target, operate on it.
                handling = EnumHandHandling.PreventDefaultAction;
                switch (mode)
                {
                    case HiveTunerMode.Calibrate:
                        {
                            if (modSystem.Membership.GetMembershipOf(target, out var hiveId))
                            {
                                attributes.SetInt("calibratedHive", hiveId);
                                if (api is ICoreClientAPI capi) capi.ShowChatMessage($"Calibrated to Hive {hiveId}");
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
                                modSystem.Tune(target, hiveId.Value);
                                if (api is ICoreClientAPI capi) capi.ShowChatMessage($"Tuned to Hive {hiveId.Value}");

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
                            modSystem.Tune(target, null);
                            if (api is ICoreClientAPI capi) capi.TriggerIngameDiscovery(this, "Zeroed target", $"Removed Hive tuning.");

                            // Not sure I like this logic here...
                            // But let's just set the guardedPlayer or guardedEntity whenever we zero
                            var player = byEntity as EntityPlayer;
                            if (player != null) entitySel?.Entity.WatchedAttributes.SetString("guardedPlayerUid", player.PlayerUID);
                            else entitySel?.Entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                            entitySel?.Entity.WatchedAttributes.SetString("guardedName", byEntity.GetName() ?? "");
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
            dsc.AppendLine($"Calibrated Hive: {(id.HasValue ? id.Value : "None")}");
        }
    }
}
