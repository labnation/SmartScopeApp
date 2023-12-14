using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using ESuite;

namespace ESuite.Drawables
{
    internal class MenuItemRadioGroup
    {
        List<MenuItem> items;
        internal MenuItem selectedItem;
        public MenuItemRadioGroup(List<MenuItem> items = null)
        {
            if (items == null)
                this.items = new List<MenuItem>();
            else
                this.items = items;
        }
        internal void AddItem(MenuItem i)
        {
            this.items.Add(i);
            if (this.selectedItem == null)
                Select(i);
        }
        internal void Select(MenuItem i)
        {
            if (!items.Contains(i))
                throw new Exception("Cannot select item which is not part of radio group");
            selectedItem = i;
        }
    }
}
