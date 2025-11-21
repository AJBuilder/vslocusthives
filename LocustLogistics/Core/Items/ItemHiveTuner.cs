using LocustLogistics.Core.EntityBehaviors;
using LocustLogistics.Core.Interfaces;
using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LocustLogistics.Core.Items
{
    enum HiveTunerMode
    {
        Calibrate,
        Tune,
        Detune
    }
    public class ItemHiveTuner : Item
    {
        ICoreAPI api;
        AutomataLocustsCore modSystem;
        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            this.api = api;
            modSystem = api.ModLoader.GetModSystem<AutomataLocustsCore>();
            ICoreClientAPI capi = api as ICoreClientAPI;


            toolModes = ObjectCacheUtil.GetOrCreate(api, "logiTunerToolModes", () =>
            {
                SkillItem[] modes;

                // Modes
                // 1. Tune to nest
                // 2. Detune
                modes = new SkillItem[4];
                modes[(int)HiveTunerMode.Calibrate] = new SkillItem() { Code = new AssetLocation("calibrate"), Name = "calibrate" };
                modes[(int)HiveTunerMode.Tune] = new SkillItem() { Code = new AssetLocation("tune"), Name = "Tune" };
                modes[(int)HiveTunerMode.Detune] = new SkillItem() { Code = new AssetLocation("detune"), Name = "Detune" };


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
                        target = be as IHiveMember;
                        if (target == null)
                        {
                            target = be.GetBehavior<IHiveMember>();
                        }

                        if (target != null)
                        {
                            be.MarkDirty();
                        }
                    }
                }
            }
            else if (entitySel != null)
            {
                target = entitySel.Entity as IHiveMember;
                if (target == null)
                {
                    target = entitySel.Entity
                            .SidedProperties
                            .Behaviors
                            .OfType<IHiveMember>()
                            .FirstOrDefault();
                }
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
                        var h = target.Hive;
                        if(h != null) attributes.SetInt("calibratedHive", target.Hive.Id);
                        else if(api is ICoreClientAPI capi){
                            capi.TriggerIngameError(this, "No target hive", "Target is not tuned to a Hive.");
                        }
                        break;
                    case HiveTunerMode.Tune:
                        var hiveId = attributes.TryGetInt("calibratedHive");
                        if (hiveId.HasValue)
                        {
                            modSystem.GetHive(hiveId.Value).Tune(target);
                        }
                        else {
                            modSystem.CreateHive(target);
                        }
                        break;
                    case HiveTunerMode.Detune:
                        var hive = target.Hive;
                        if(hive != null) hive.Detune(target);
                        else if (api is ICoreClientAPI capi)
                        {
                            capi.TriggerIngameError(this, "No target hive", "Target is not tuned to a Hive.");
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
