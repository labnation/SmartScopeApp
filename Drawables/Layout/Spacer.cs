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
    internal class Spacer:EDrawable
    {
        public override Point? Size
        {
            get
            {
                if (orientation == Orientation.Horizontal)
                    return new Point(spacing.InchesToPixels(), 0);
                else //Vertical
                    return new Point(0, spacing.InchesToPixels());
            }
        }
        
        public Orientation orientation = Orientation.Horizontal;
        public float spacing = 0f;

        public Spacer(Orientation orientation, float spacing)
            : base()
        {
            this.orientation = orientation;
            this.spacing = spacing;

            LoadContent();
        }

        protected override void LoadContentInternal() { }

        protected override void UpdateInternal(GameTime now) { }

        protected override void DrawInternal(GameTime time) { }

        protected override void OnBoundariesChangedInternal() 
        {
        }
    }
}
