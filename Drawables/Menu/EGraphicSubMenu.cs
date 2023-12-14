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
    public class EGraphicSubMenu:EGraphicMenuItem
    {
        private List<EGraphicMenuItem> subItems = new List<EGraphicMenuItem>();
        private EGraphicMenuButton button;        

        public EGraphicSubMenu(EDrawable parent, EXNAController xnaController, string text, EGraphicSubMenu parentMenuItem)
            : base(parent, xnaController, parentMenuItem)
        {
            this.button = new EGraphicMenuButton(this, xnaController, text, this.ActivateChildren, parentMenuItem);
            children.Add(this.button);
        }

        public void AddMenuItem(EGraphicMenuItem item)
        {
            children.Add(item);
            subItems.Add(item);

            //whenever a child item is added,the yPos of all children needs to be adjusted accordingly
            float verSpacing = 0.2f;
            for (int i = 0; i < subItems.Count; i++)
            {
                subItems[i].SetTopYPos(0.5f - (subItems.Count-0.5f) * verSpacing / 2f + i * verSpacing);
            }
        }

        public void RemoveAllChildren()
        {            
            subItems.Clear();
            for (int i = children.Count - 1; i > 0; i--)
                children.RemoveAt(i);
        }

        public override void Deactivate(float delaySeconds)
        {
            //if this button was not activated, then one of its children might be activated
            if (button.CurrentStatus == ButtonStatus.Deactivated)
                DeactivateChildren();
            else
                this.button.Deactivate(0);
        }

        public void DeactivateChildren()
        {
            for (int i = 0; i < subItems.Count; i++)
                subItems[i].Deactivate(i * 0.1f);
        }

        public override void SetTopYPos(float topYPos)
        {
            this.button.SetTopYPos(topYPos);
        }

        override public void Activate(float delaySeconds)
        {
            this.button.Activate(delaySeconds);            
        }

        public void ActivateChildren()
        {  
            //if (subItems.coun
            EXNAController.MenuShown = true;
            for (int i = 0; i < subItems.Count; i++)
                subItems[i].Activate(i*0.1f);
        }

        protected override void LoadContentInternal(ContentManager Content)
        {
        }

        protected override void DrawInternal()
        {
        }

        protected override void ReactOnNewWVP()
        {
        }

        override protected void UpdateInternal(DateTime now, List<GestureSample> gestureList)
        {
        }
    }
}
