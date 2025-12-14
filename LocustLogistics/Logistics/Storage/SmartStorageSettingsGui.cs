//using System;
//using System.Collections.Generic;
//using Vintagestory.API.Client;
//using Vintagestory.API.Common;
//using Vintagestory.API.Config;
//using Vintagestory.API.MathTools;
//
//namespace LocustLogistics.Logistics.Storage
//{
//
//    public enum InventoryMode
//    {
//        Storage,
//        Push,
//        Pull,
//    }
//
//    public enum PullSubMode
//    {
//        Single,
//        Quota,
//    }
//
//    public class SmartStorageSettingsGui : GuiDialogBlockEntity
//    {
//        private InventoryMode mode;
//        private PullSubMode pullSubMode;
//
//
//        // Storage Tab Callbacks
//        public Action<int, bool> OnStorageSlotToggled { get; set; }
//        public Action<int> OnStorageSlotFilterClicked { get; set; }
//        public Action<int, ItemStack> OnStorageSlotItemChanged { get; set; }
//
//        // Request Tab Callbacks
//        public Action<bool> OnPullQuotaModeChanged { get; set; }
//        public Func<List<ItemStack>> GetAvailableItems { get; set; }
//        public Action OnAddRequestClicked { get; set; }
//        public Action<int> OnRequestCleared { get; set; }
//        public Action<int, bool> OnRequestToggled { get; set; }
//        public Action<int, int> OnRequestQuantityChanged { get; set; }
//
//        // Push Tab Callbacks
//        public Action<bool> OnPushModeToggled { get; set; }
//
//
//        private IInventory filterInventory;
//
//        public SmartStorageSettingsGui(BlockPos blockEntityPos, ICoreClientAPI capi, InventoryMode mode, PullSubMode pullSubMode)
//            : base(Lang.Get("locustlogistics:hive-storage-settings"), blockEntityPos, capi)
//        {
//            this.mode = mode;
//            this.pullSubMode = pullSubMode;
//        }
//
//        public void SetupDialog()
//        {
//            Compose();
//        }
//
//        private void Compose()
//        {
//            // Dialog bounds
//            ElementBounds bgBounds = ElementBounds.Fixed(0, 0, 450, 350);
//
//            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
//                .WithAlignment(EnumDialogArea.CenterMiddle)
//                .WithFixedAlignmentOffset(0, 0);
//
//            // Tab bounds
//            ElementBounds tabBounds = ElementBounds.Fixed(0, -24, 400, 25);
//
//            var tabs = new GuiTab[]
//            {
//                new GuiTab() { Name = Lang.Get("locustlogistics:tab-storage"), DataInt = 0 },
//                new GuiTab() { Name = Lang.Get("locustlogistics:tab-request"), DataInt = 1 },
//                new GuiTab() { Name = Lang.Get("locustlogistics:tab-push"), DataInt = 2 }
//            };
//
//            CairoFont tabFont = CairoFont.WhiteDetailText();
//
//            SingleComposer = capi.Gui
//                .CreateCompo("hivestoragesettingsdialog-" + BlockEntityPosition, dialogBounds)
//                .AddShadedDialogBG(bgBounds, true)
//                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
//                .AddHorizontalTabs(tabs, tabBounds, OnTabClicked, tabFont,
//                    tabFont.Clone().WithColor(GuiStyle.ActiveButtonTextColor), "tabs")
//                .BeginChildElements(bgBounds);
//
//            // Compose the appropriate tab content
//            switch (mode)
//            {
//                case InventoryMode.Storage:
//                    {
//                        // For now, nothing.
//                        // Later, a filter grid.
//
//                        // Calculate slot grid dimensions
//                        //int cols = 4;
//                        //int rows = (int)Math.Ceiling(filterInventory.Count / (double)cols);
//                        //
//                        //// Slot grid bounds
//                        //ElementBounds slotGridBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30, cols, rows);
//                        //
//                        //// Custom slot grid that intercepts clicks
//                        //SingleComposer.AddInteractiveElement(
//                        //    new GuiElementStorageSlotGrid(capi, filterInventory, SendInvPacket, cols, slotGridBounds, this),
//                        //    "storageslotgrid"
//                        //);
//                    }
//                    break;
//
//                case InventoryMode.Pull:
//                    {
//                        double y = 30;
//
//                        // Quota/Queue mode switch
//                        ElementBounds switchBounds = ElementBounds.Fixed(0, y, 35, 25);
//                        ElementBounds switchTextBounds = ElementBounds.Fixed(40, y + 3, 200, 25);
//
//                        SingleComposer
//                            .AddSwitch(OnPullQuotaModeChanged, switchBounds, "pullquotamodeswitch", 25)
//                            .AddStaticText(Lang.Get("locustlogistics:pull-quota-mode"), CairoFont.WhiteSmallText(), switchTextBounds);
//
//                        // Set initial switch state
//                        SingleComposer.GetSwitch("pullquotamodeswitch").On = pullSubMode == PullSubMode.Quota;
//
//                        y += 40;
//
//                        // Add Request button
//                        ElementBounds addButtonBounds = ElementBounds.Fixed(0, y, 150, 30);
//                        SingleComposer.AddSmallButton(Lang.Get("locustlogistics:add-pull-request"), OnAddRequestButtonClicked, addButtonBounds);
//
//                        y += 40;
//
//                        // Request list area (placeholder for now)
//                        ElementBounds requestListBounds = ElementBounds.Fixed(0, y, 400, 200);
//                        SingleComposer.AddStaticText(Lang.Get("locustlogistics:pull-request-list-placeholder"),
//                            CairoFont.WhiteSmallText(), requestListBounds);
//                    }
//                    break;
//                case InventoryMode.Push:
//                    {
//                        double y = 30;
//
//                        // Push mode enable/disable switch
//                        ElementBounds switchBounds = ElementBounds.Fixed(0, y, 35, 25);
//                        ElementBounds switchTextBounds = ElementBounds.Fixed(40, y + 3, 200, 25);
//
//                        SingleComposer
//                            .AddSwitch(OnPullQuotaModeChanged, switchBounds, "pushmodeswitch", 25)
//                            .AddStaticText(Lang.Get("locustlogistics:enable-push-mode"), CairoFont.WhiteSmallText(), switchTextBounds);
//                    }
//                    break;
//            }
//
//            SingleComposer.EndChildElements().Compose();
//
//            // Set the active tab
//            SingleComposer.GetHorizontalTabs("tabs").activeElement = mode;
//        }
//
//        private void OnTabClicked(int tabIndex)
//        {
//            if (mode != tabIndex)
//            {
//                mode = tabIndex;
//                Compose();
//            }
//        }
//
//
//        private void ComposePushTab()
//        {
//        }
//
//        #region Internal Event Handlers
//
//
//        private bool OnAddRequestButtonClicked()
//        {
//            OnAddRequestClicked?.Invoke();
//            return true;
//        }
//
//        private void OnPushModeToggleInternal(bool enabled)
//        {
//            OnPushModeToggled?.Invoke(enabled);
//        }
//
//        #endregion
//
//        #region Internal Methods Called by Custom Slot Grid

        //internal void HandleStorageSlotLeftClick(int slotId)
        //{
        //    OnStorageSlotToggled?.Invoke(slotId, true); // Will need state management
        //}
        //
        //internal void HandleStorageSlotRightClick(int slotId)
        //{
        //    OnStorageSlotFilterClicked?.Invoke(slotId);
        //}
        //
        //#endregion
//
//        private void OnTitleBarClose()
//        {
//            TryClose();
//        }
//
//        public override void OnGuiClosed()
//        {
//            SingleComposer?.GetSlotGrid("storageslotgrid")?.OnGuiClosed(capi);
//            base.OnGuiClosed();
//        }
//
//        private void SendInvPacket(object packet)
//        {
//            capi.Network.SendPacketClient(packet);
//        }
//    }
//}
