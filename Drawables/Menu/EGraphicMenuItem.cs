using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite
{
    abstract public class EGraphicMenuItem:EDrawable
    {
        protected EGraphicSubMenu parentMenuItem;

        public EGraphicMenuItem(EDrawable parent, EXNAController xnaController, EGraphicSubMenu parentMenuItem)
            : base(parent, xnaController)
        {
            this.parentMenuItem = parentMenuItem;
        }

        public void Activate() { Activate(0); }
        abstract public void Activate(float delaySeconds);

        public void Deactivate() { Deactivate(0); }
        abstract public void Deactivate(float delaySeconds);
        
        //abstract public void DeactivateChildren(float delaySeconds);
        abstract public void SetTopYPos(float topYPos);
    }
}
