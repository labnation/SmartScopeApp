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
    internal class Empty:EDrawable
    {
        public Empty()
            : base()
        {
            LoadContent();
        }

        protected override void LoadContentInternal() { }

        protected override void UpdateInternal(GameTime now) { }

        protected override void DrawInternal(GameTime time) 
        {
        }

        protected override void OnBoundariesChangedInternal() 
        {
        }
    }
}
