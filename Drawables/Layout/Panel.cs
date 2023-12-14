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
    internal class Panel:EDrawable
    {
        public MappedColor background;
        public float margin = 0f;
        public Orientation orientation = Orientation.Horizontal;
        public Direction direction = Direction.Forward;
        public Panel()
            : base()
        {
            LoadContent();
        }

        public void AddItem(EDrawable item)
        {
            this.AddChild(item);
            OnBoundariesChanged();
        }

        public void RemoveItem(EDrawable item)
        {
            this.RemoveChild(item);
            OnBoundariesChanged();
        }

        public void Clear()
        {
            int toClear = children.Count;
            for (int i = toClear - 1; i >= 0; i--)
                RemoveItem(children.ElementAt(i));
        }

        protected override void LoadContentInternal() { }

        protected override void UpdateInternal(GameTime now) { }

        protected override void DrawInternal(GameTime time) 
        {
            if (background == MappedColor.Undefined) return;
            spriteBatch.Begin();
            spriteBatch.Draw(whiteTexture, Boundaries, background.C());
            spriteBatch.End();
        }

        public override Point? Size
        {
            get
            {
                if (orientation == Orientation.Horizontal)
                    return new Point(
                        children.Aggregate(0, (x, y) => x += y.Boundaries.Width) + (children.Count - 1) * margin.InchesToPixels(),
                        children.Select(x => x.Boundaries.Height).Max()
                    );
                else //Vertical
                    return new Point(
                        children.Select(x => x.Boundaries.Width).Max(),
                        children.Aggregate(0, (x, y) => x += y.Boundaries.Height) + (children.Count - 1) * margin.InchesToPixels()
                    );
            }
        }

        protected override void OnBoundariesChangedInternal() 
        {
            // Set height for panels with specified height
            int offset = 0;
            for (int i = 0; i < children.Count; i++)
            {
                Rectangle r;
                if (orientation == Orientation.Horizontal)
                {
                    int size = children[i].Size.Value.X;
                    if(direction == Direction.Forward)
                        r = new Rectangle(Boundaries.X + offset, Boundaries.Y, size, Boundaries.Height);
                    else //Backward
                        r = new Rectangle(Boundaries.Right - offset - size, Boundaries.Y, size, Boundaries.Height);
                    offset += children[i].Boundaries.Width;
                }
                else //Vertical
                {
                    int size = children[i].Size.Value.Y;
                    if (direction == Direction.Forward)
                        r = new Rectangle(Boundaries.X, Boundaries.Y + offset, Boundaries.Width, size);
                    else
                        r = new Rectangle(Boundaries.X, Boundaries.Bottom - offset - size, Boundaries.Width, size);
                    offset += children[i].Boundaries.Height;
                }
                offset += margin.InchesToPixels();
                children[i].SetBoundaries(r);
            }
        }
    }
}
