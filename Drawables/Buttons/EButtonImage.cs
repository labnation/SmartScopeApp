using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables
{
    internal class EButtonImage : EButtonImageAndText
    {
        public EButtonImage(string image, Location verticalAlign, Location horizontalAlign, MappedColor imageColor = MappedColor.ButtonImageOverlay, MappedColor backgroundColor = MappedColor.Transparent)
            : base(image, imageColor, null, MappedColor.Font, verticalAlign, Location.Center, horizontalAlign, backgroundColor) { }
    }
}
