using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LocustHives.Game.Logistics
{
    /// <summary>
    /// Dialog for picking an item from a list of available ItemStacks
    /// </summary>
    public class GuiDialogItemPicker : GuiDialog
    {
        private List<ItemStack> availableItems;
        private Action<ItemStack> onItemSelected;
        private int selectedIndex = -1;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogItemPicker(ICoreClientAPI capi, List<ItemStack> items, Action<ItemStack> onSelected)
            : base(capi)
        {
            availableItems = items ?? new List<ItemStack>();
            onItemSelected = onSelected;
        }

        public void SetupDialog()
        {
            Compose();
        }

        private void Compose()
        {
            // Dialog dimensions
            double dialogWidth = 400;
            double dialogHeight = 500;

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle)
                .WithFixedAlignmentOffset(0, 0);

            double y = 30;

            // Create a scrollable list area
            ElementBounds clipBounds = ElementBounds.Fixed(0, y, dialogWidth - 40, dialogHeight - 100);
            ElementBounds insetBounds = ElementBounds.Fixed(0, y, dialogWidth - 40, dialogHeight - 100);

            SingleComposer = capi.Gui
                .CreateCompo("itempickerdialog", dialogBounds)
                .AddShadedDialogBG(bgBounds, true)
                .AddDialogTitleBar(Lang.Get("locusthives:select-item"), OnTitleBarClose)
                .BeginChildElements(bgBounds);

            // Add inset for the list
            SingleComposer.AddInset(insetBounds, 2);

            // Create a clip area for scrolling
            ElementBounds scrollbarBounds = insetBounds.CopyOffsetedSibling(insetBounds.fixedWidth + 5, 0).WithFixedWidth(20);

            // For now, add items as a simple vertical list
            y = 10;
            for (int i = 0; i < availableItems.Count; i++)
            {
                int index = i; // Capture for lambda
                ItemStack stack = availableItems[i];
                if (stack == null) continue;

                ElementBounds itemBounds = ElementBounds.Fixed(10, y, dialogWidth - 80, 30);

                string itemName = stack.GetName();
                SingleComposer.AddSmallButton(itemName, () => OnItemButtonClicked(index), itemBounds, EnumButtonStyle.Normal, $"itembutton{i}");

                y += 35;
            }

            // Buttons at bottom
            y = dialogHeight - 70;
            ElementBounds cancelBounds = ElementBounds.Fixed(EnumDialogArea.LeftBottom, 0, 0, 0, 0)
                .FixedUnder(clipBounds, 10)
                .WithFixedWidth(100);

            SingleComposer
                .AddSmallButton(Lang.Get("Cancel"), OnCancelClicked, cancelBounds)
                .EndChildElements()
                .Compose();
        }

        private bool OnItemButtonClicked(int index)
        {
            selectedIndex = index;
            if (selectedIndex >= 0 && selectedIndex < availableItems.Count)
            {
                onItemSelected?.Invoke(availableItems[selectedIndex]);
            }
            TryClose();
            return true;
        }

        private bool OnCancelClicked()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override bool TryOpen()
        {
            if (availableItems == null || availableItems.Count == 0)
            {
                return false;
            }
            return base.TryOpen();
        }
    }
}
