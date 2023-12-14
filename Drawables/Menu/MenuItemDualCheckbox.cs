using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables

{
    internal class MenuItemDualCheckbox : MenuItem
    {
        internal bool leftChecked = false;
        internal DrawableCallback leftAction { get; private set; }

        Rectangle rightCheckboxArea;
        Rectangle leftCheckboxArea;
        DrawableCallback onLeftCheckedChange = null;
        DrawableCallback onRightCheckedChange = null;
        private Texture2D leftCheckBoxActive, leftCheckBoxInactive, rightCheckBoxActive, rightCheckBoxInactive;

        private string leftIconInactive = "widget-checkbox-off"; //need to pre-define them here, as LoadContent is called before constructor gets the chance to correct these values..
        private string leftIconActive = "widget-checkbox-on-1";
        private string rightIconInactive = "widget-checkbox-off";
        private string rightIconActive = "widget-checkbox-on-1";
        private bool fadeInactive;

        private bool _checked = false;
        internal bool Checked { get { return this._checked; } set { this._checked = value; this.redrawRequest = true; } }

        /*
        internal MenuItemDualCheckbox(string text, DrawableCallbackDelegate onCheckedChange,
            object delegateArgument,
            bool is_checked = false, DrawableCallbackDelegate onMultipleValuesChanged = null) :
            this(text, onCheckedChange, delegateArgument, null, ExpansionMode.None, is_checked, false, false, onMultipleValuesChanged) { } 
          */  
        internal MenuItemDualCheckbox(string text, DrawableCallbackDelegate onLeftCheckedChange, 
            object leftDelegateArgument, DrawableCallbackDelegate onRightCheckedChange, object rightDelegateArgument,
            List<MenuItem> childMenuItems = null, ExpansionMode expansionmode = ExpansionMode.None,            
            bool is_checked = false, bool selected = false, bool active = false,
            bool autoSelectAndActivate = true, bool left_is_checked = false,
            string leftIconInactive = "widget-checkbox-off", string leftIconActive = "widget-checkbox-on-1", string rightIconInactive = "widget-checkbox-off", string rightIconActive = "widget-checkbox-on-1", bool fadeInactive = false) :
			base (text, null, null, childMenuItems, expansionmode, selected, active, false, null, autoSelectAndActivate)
		{
            this.onLeftCheckedChange = new DrawableCallback(onLeftCheckedChange, rightDelegateArgument);
            this.onRightCheckedChange = new DrawableCallback(onRightCheckedChange, rightDelegateArgument);
			this.Checked = is_checked;
            this.leftChecked = left_is_checked;

            this.leftIconActive = leftIconActive;
            this.leftIconInactive = leftIconInactive;
            this.rightIconActive = rightIconActive;
            this.rightIconInactive = rightIconInactive;
            this.fadeInactive = fadeInactive;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
            leftCheckBoxActive = LoadPng(Scaler.ScaledImageName(leftIconActive));
            leftCheckBoxInactive = LoadPng(Scaler.ScaledImageName(leftIconInactive));
            if (rightIconActive != null)
                rightCheckBoxActive = LoadPng(Scaler.ScaledImageName(rightIconActive));
            else
                rightCheckBoxActive = null;

            if (rightIconInactive != null)
                rightCheckBoxInactive = LoadPng(Scaler.ScaledImageName(rightIconInactive));
            else
                rightCheckBoxInactive = null;
        }

        protected override void DrawInternal(GameTime time)
		{
            float fadingFactor = 0.2f;

            base.DrawInternal(time);
            this.redrawRequest = false;
                   
            //Background & checkbox
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(leftChecked ? leftCheckBoxActive : leftCheckBoxInactive, leftCheckboxArea, leftChecked ? fgColor : fgColor * fadingFactor);
            if (rightCheckBoxInactive != null && rightCheckBoxActive != null)
                spriteBatch.Draw(Checked ? rightCheckBoxActive : rightCheckBoxInactive, rightCheckboxArea, Checked ? fgColor : fgColor * fadingFactor);
            spriteBatch.End();
		}

		protected override void ComputeScales ()
		{
            base.ComputeScales();

            Vector2 textSize = usedFont.MeasureString(this.text);
            this.textLocation = new Vector2((int)(this.Boundaries.Center.X - textSize.X / 2), (int)(this.Boundaries.Center.Y - textSize.Y / 2f)); //text in center of item

            Vector2 leftcheckboxTopLeft = new Vector2(Boundaries.Left + Margin.X, Boundaries.Center.Y - (int)(leftCheckBoxInactive.Height / 2));
            leftCheckboxArea = new Rectangle((int)leftcheckboxTopLeft.X, (int)leftcheckboxTopLeft.Y, leftCheckBoxInactive.Width, leftCheckBoxInactive.Height);

            Vector2 rightcheckboxTopLeft = new Vector2(Boundaries.Right - leftCheckBoxInactive.Width - Margin.X, Boundaries.Center.Y - (int)(leftCheckBoxInactive.Height / 2));
            rightCheckboxArea = new Rectangle((int)rightcheckboxTopLeft.X, (int)rightcheckboxTopLeft.Y, leftCheckBoxInactive.Width, leftCheckBoxInactive.Height);
		}

        protected override void DoTap(Point position, object argument = null)
		{
            if (position.X < Boundaries.Center.X)
            {
                leftChecked = !leftChecked;
                if (onLeftCheckedChange != null)
                    onLeftCheckedChange.Call(this, leftChecked);
            }
            else
            {
                Checked = !Checked;
                if (onRightCheckedChange != null)
                    onRightCheckedChange.Call(this, Checked);
            }
        }

		private void Toggle ()
		{
            if (onRightCheckedChange != null)
                onRightCheckedChange.Call(this, !this.Checked);
            if (AutoChangeOnTap)
                this.Checked = !this.Checked;
		}

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            base.HandleGestureInternal(gesture);
        }
    }
}
