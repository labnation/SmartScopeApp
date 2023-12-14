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
    internal class LerpFloat : Lerp<float>
    {
        public LerpFloat(float start, float end, float transitionSpeed) : 
            base (start, end, transitionSpeed, 
                (float f_start, float f_end, float progress) => f_start + (end - f_start) * LerpingSine.SmoothStopPower(progress))
        { }

    }
}
