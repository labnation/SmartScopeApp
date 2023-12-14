using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class EContextMenuDropdown: EContextMenu
    {
        bool collapsed = true;
        EContextMenuItemButton selectedItem;
        Dictionary<EContextMenuItemButton, bool> items;
        public EContextMenuDropdown(GraphManager gm, Dictionary<EContextMenuItemButton, bool> items)
            : base(gm, Orientation.Vertical)
        {
            this.Visible = true;
            this.items = items;
            EContextMenuItemButton item = items.Single(x => x.Value).Key;
            
            //Can't just take the same object since the world is different
            this.selectedItem = new EContextMenuItemButton(this, item.buttonStrings, item.iconName, new DrawableCallback(UICallbacks.ToggleContextDropdown, this));
            this.AddChild(this.selectedItem);

            LoadContent();
        }
        public void Toggle()
        {
            if (collapsed)
                Show(Owner, Boundaries.Location, this.items.Keys.Select(x => x as EContextMenuItem).ToList());
            else
                Collapse();
        }
        public void Collapse()
        {
            items.Keys.ToList().ForEach(x => x.Visible = false);
            collapsed = true;
        }

        public override void Show(EDrawable owner, Point location, List<EContextMenuItem> itemList, DrawableCallback closeCallback = null)
        {
            if (itemList == null) return;
            if (itemList.Count == 1) // don't expand if there's only 1 option
            {
                if (itemList[0] is EContextMenuItemButton)
                {
                    EContextMenuItemButton myButt = (EContextMenuItemButton)itemList[0];
                    myButt.Fire();
                }
                return;
            }

            (parent as EContextMenu).Collapse();
            int childSize = Scaler.ContextMenuSize.InchesToPixels();
            base.Show(owner, location + new Point(childSize / 2, childSize/2), itemList, closeCallback);
            //Re-add selected item since base.Show clears all children. but put it as first, or it will be drawn on top of dropdown list
            this.selectedItem.Visible = true;
            InsertChild(0, this.selectedItem);
            collapsed = false;
        }

        protected override void DrawInternal(GameTime time) { }

        protected override void OnBoundariesChangedInternal()
        {
            this.selectedItem.SetBoundaries(this.Boundaries);
        }
    }
}
