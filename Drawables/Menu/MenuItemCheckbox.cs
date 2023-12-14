using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables

{
    internal class MenuItemCheckbox : MenuItem
    {
		Rectangle checkboxArea;
		DrawableCallback onMultipleValuesChanged = null;
        DrawableCallback onCheckedChange = null;
		private Texture2D checkboxSelected, checkboxUnselected;

        private bool _checked = false;
        internal bool Checked { get { return this._checked; } set { this._checked = value; this.redrawRequest = true; } }

        internal MenuItemCheckbox(string text, DrawableCallbackDelegate onCheckedChange,
            object delegateArgument,
            bool is_checked = false, DrawableCallbackDelegate onMultipleValuesChanged = null) :
            this(text, onCheckedChange, delegateArgument, null, ExpansionMode.None, is_checked, false, false, onMultipleValuesChanged) { } 
            
        internal MenuItemCheckbox(string text, DrawableCallbackDelegate onCheckedChange, 
            object delegateArgument, List<MenuItem> childMenuItems = null, ExpansionMode expansionmode = ExpansionMode.None, 
            bool is_checked = false, bool selected = false, bool active = false,
            DrawableCallbackDelegate onMultipleValuesChanged = null) :
			base (text, null, delegateArgument, childMenuItems, expansionmode, selected, active, true, -1, true)
		{
            this.onCheckedChange = new DrawableCallback(onCheckedChange, delegateArgument);
			this.Checked = is_checked;
            if(onMultipleValuesChanged != null)
			    this.onMultipleValuesChanged = new DrawableCallback(onMultipleValuesChanged, delegateArgument);
            this.Tag = delegateArgument;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
            checkboxSelected = LoadPng(Scaler.ScaledImageName("widget-checkbox-on-1"));
            checkboxUnselected = LoadPng(Scaler.ScaledImageName("widget-checkbox-off"));
        }

        protected override void DrawInternal(GameTime time)
		{
            base.DrawInternal(time);
            this.redrawRequest = false;
            //Background & checkbox
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
			spriteBatch.Draw (Checked ? checkboxSelected : checkboxUnselected, checkboxArea, fgColor);
			spriteBatch.End(); 
		}

		protected override void ComputeScales ()
		{
            base.ComputeScales();

            if (checkboxUnselected == null)
                return;
			Vector2 checkboxTopLeft = new Vector2(Boundaries.Right - checkboxUnselected.Width - Margin.X, Boundaries.Center.Y - (int)(checkboxUnselected.Height / 2));
            if (ExpansionMode == ExpansionMode.Sidemenu)
                checkboxTopLeft.X -= arrowTexture.Width;
            checkboxArea = new Rectangle((int)checkboxTopLeft.X, (int)checkboxTopLeft.Y, checkboxUnselected.Width, checkboxUnselected.Height);
		}

        protected override void DoTap(Point position, object argument = null)
		{
            if (checkboxArea.Contains(position))
                Toggle();
            else
                base.DoTap(position, argument);
		}

		private void Toggle ()
		{
            this.Checked = !this.Checked;

            if (onCheckedChange != null)
                onCheckedChange.Call(this, this.Checked);                    
		}

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            base.HandleGestureInternal(gesture);
        }
    }
}
