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
    internal class ButtonInfo {
        public List<KeyCombo> listenKeys = null;
        public DrawableCallback callback;
        public string text;
        public ButtonInfo(string text, DrawableCallbackDelegate callback = null, object argument = null, List<KeyCombo> listenKeys = null)
        {
            this.text = text;
            this.callback = new DrawableCallback(callback, argument);
            this.listenKeys = listenKeys;
        }
    }

    internal class EButton:EDrawable
    {
        public string text { get; private set; }
        protected Vector2 center;
        protected Color colorSelected;
        protected Color colorUnselected;
        public bool selected = false;
        private DrawableCallback callback;
        private SpriteFont font;
        private float textScale;

        private float textMaxHeight { get { return font.MeasureString("Hpg").Y * textScale; } }
        private float margin { get { return 0.5f * textMaxHeight; } }
        public virtual float desiredWidth { get { return font.MeasureString(text).X * textScale; } }
        public float desiredHeight { get { return textMaxHeight + margin; } }

        public EButton(string text, Color colorSelected, float textScale, Color? colorUnselected = null) :
            this(new ButtonInfo(text), colorSelected, textScale, colorUnselected) { }

        public EButton(ButtonInfo info, Color colorSelected, float textScale, Color? colorUnselected = null)
            : base()
        {            
            if (colorUnselected == null)
                this.colorUnselected = colorSelected;
            else
                this.colorUnselected = colorUnselected.Value;
            this.textScale = textScale;
            this.colorSelected = colorSelected;
            this.callback = info.callback;

            this.text = info.text;

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            font = content.Load<SpriteFont>(Scaler.GetFontButtonBar());
        }

        public void SetCallback(DrawableCallbackDelegate callback, object argument = null)
        {
            this.callback = new DrawableCallback(callback, argument);
        }

        protected override void DrawInternal(GameTime time)
        {
            //simply draw the image to span the entire screen
            spriteBatch.Begin();//SpriteSortMode.FrontToBack, BlendState.Additive);
            Vector2 textSz = font.MeasureString(text) * textScale;
            DrawBoundingBox();
            spriteBatch.DrawString(font, this.text, center - textSz / 2f, selected ? colorUnselected : colorSelected, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            spriteBatch.End();
        }

        protected void DrawBoundingBox()
        {
            int margin = 1;
            if (selected)
            {
                Rectangle bounds = Boundaries;
                bounds.X += margin;
                bounds.Y += margin;
                bounds.Width -= 2*margin;
                bounds.Height -= 2* margin;
                spriteBatch.Draw(whiteTexture, bounds, colorSelected);
            }
            else
            {
                spriteBatch.Draw(whiteTexture, Boundaries, colorUnselected);
                Rectangle top = new Rectangle(Boundaries.X + margin, Boundaries.Y + margin, Boundaries.Width - 2 * margin, 1);
                Rectangle bottom = new Rectangle(Boundaries.X + margin, Boundaries.Bottom - 1 - margin, Boundaries.Width - 2 * margin, 1);
                Rectangle left = new Rectangle(Boundaries.X + margin, Boundaries.Y + margin, 1, Boundaries.Height - 2 * margin);
                Rectangle right = new Rectangle(Boundaries.Right - 1 - margin, Boundaries.Y + margin, 1, Boundaries.Height - 2 * margin);
                spriteBatch.Draw(whiteTexture, top, colorSelected);
                spriteBatch.Draw(whiteTexture, left, colorSelected);
                spriteBatch.Draw(whiteTexture, bottom, colorSelected);
                spriteBatch.Draw(whiteTexture, right, colorSelected);
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            center = new Vector2(Boundaries.X + Boundaries.Width / 2f, Boundaries.Y + Boundaries.Height / 2f);
            this.interactiveAreas[0] = Boundaries;
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DragComplete:
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    callback.Call(this);
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
