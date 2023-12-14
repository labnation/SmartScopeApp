using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;

namespace ESuite.Drawables
{
    internal class Stack:EDrawable
    {
        public Stack() : base() { LoadContent(); }

        protected override void LoadContentInternal() { }

        protected override void UpdateInternal(GameTime now) { }

        protected override void DrawInternal(GameTime time) { }

        protected override void OnBoundariesChangedInternal() 
        {
            foreach (var child in children)
                child.SetBoundaries(this.Boundaries);
        }

        public void AddItem(EDrawable item)
        {
            if (children.Contains(item))
                throw new Exception("Can't add item to stack, already in it!");
            this.AddChild(item);
        }
        public void RemoveItem(EDrawable item)
        {
            if (item == null) return;
            this.RemoveChild(item);
        }

        internal void SetChildBoundaries(EDrawable floater, Rectangle newbounds)
        {
            if (!(floater.parent == this))
                throw new Exception("Illegal Drawable operation - child not member of stack");
            floater.SetBoundaries(newbounds);
        }
    }
}
