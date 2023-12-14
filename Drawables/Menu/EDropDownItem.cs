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
    internal class EDropDownItem : EDrawable
    {
        private Vector2 textLocation;
        private string text, shortText;
        private DrawableCallback onSelectCallback;
        EDropDown dropDownControl { get { return (EDropDown)parent; } }

        private SpriteFont font;
        private Texture2D backgroundTexture;
        private bool disabled = false;
        public bool Disabled
        {
            get { return disabled; }
            set
            {
                //If currently selected and disabling, make sure the dropdown doesn't
                //have this as its selected item.
                if (value && dropDownControl.CurrentlySelectedItem == this)
                    dropDownControl.SelectItem(null);
                disabled = value;
            }
        }

        public string Text { get { return dropDownControl.useShortText && this.shortText != null ? this.shortText : this.text; } }

        public EDropDownItem(string text, DrawableCallback callBackOnSelect, string shortText = null, bool disabled = false, object tag = null)
            : base()
        {
            this.text = text;
            this.shortText = shortText;
            this.onSelectCallback = callBackOnSelect;

            this.interactiveAreas = new List<Rectangle> { new Rectangle() };

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;            
            this.disabled = disabled;
            this.Tag = tag;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            this.font = content.Load<SpriteFont>(Scaler.GetFontButtonBar());
            this.backgroundTexture = whiteTexture;
        }

        protected override void DrawInternal(GameTime time)
        {
            //background
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            if (dropDownControl.CurrentlySelectedItem == this)
                spriteBatch.Draw(backgroundTexture, Boundaries, MappedColor.Selected.C());
            else
                spriteBatch.Draw(backgroundTexture, Boundaries, new Color(255,255,255,220));
			spriteBatch.End(); 

			//text
			spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            if (dropDownControl.CurrentlySelectedItem == this)
                spriteBatch.DrawString(font, Text, textLocation, Color.White);
            else
                spriteBatch.DrawString(font, Text, textLocation, disabled ? MappedColor.DisabledFont.C() : MappedColor.Font.C());
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            this.interactiveAreas = new List<Rectangle>() { Boundaries };
            var textSize = font.MeasureString(Text);
            textLocation = new Vector2(this.Boundaries.Center.X - (int)(textSize.X / 2), Boundaries.Center.Y - (int)(textSize.Y / 2f));
        }

        protected void OnTap()
        {
            FullRedrawRequired = true;
            if (!Disabled)
            {
                if(onSelectCallback != null)
                    onSelectCallback.Call(this);
                dropDownControl.SelectItem(this);
            }
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DragComplete:
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    OnTap();
                    ReleaseGestureControl();
                    break;
                case GestureType.FreeDrag:
                case GestureType.Pinch:
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
