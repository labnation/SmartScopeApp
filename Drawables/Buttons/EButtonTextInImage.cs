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
    internal class EToggleButtonTextInImage : EButtonImageAndTextSelectable
    {
        public EToggleButtonTextInImage(
            string text, 
            string textureOnName, string textureOffName,
            MappedColor imageColorOn, MappedColor imageColorOff, 
            MappedColor textColorOn,  MappedColor textColorOff,  
            bool initState = false, bool selfUpdate = true) : 
            base(
                textureOffName,
                imageColorOff, textureOnName, imageColorOn, text, textColorOff, text, textColorOn, initState, selfUpdate, Location.Center, Location.Center)
        {
        }
    }
}
