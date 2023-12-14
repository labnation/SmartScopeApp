using ESuite.Measurements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;
using System.Collections.Concurrent;

namespace ESuite.Drawables
{
    internal class EToast : EDrawable
    {
        private EDrawable anchor;
        private Location anchorRelativePosition;
        private Location anchorAlignment;

        private SpriteFont font;
        private Texture2D background;
        private Vector2 iconLocation;
        private Vector2 textSize;
        private Vector2 textLocation;
        private Location textAlignment;
        private Color iconColor;
        private string text;
        private string iconName;
        private Texture2D icon;
        private DateTime displayedSince = DateTime.Now;
        private float opacity = 0;

        private DateTime? hidingSince;
        private int hideTimeout = -1;
        private int fadeTime = ColorMapper.ToastFadeTime;

        public EToast(EDrawable anchor, string iconName, Color iconMultiplyColor, string text, int hideTimeout, Location position, Location alignment, Location textAlignment = Location.Left, bool clickToHide = true)
            : base()
        {
            this.text = text;
            this.textAlignment = textAlignment;
            this.iconName = iconName;
            this.iconColor = iconMultiplyColor;
            this.Visible = false;
            this.hideTimeout = hideTimeout;

            this.anchor = anchor;
            this.anchorRelativePosition = position;
            this.anchorAlignment = alignment;
            if (clickToHide)
            {
                supportedGestures = GestureType.Tap;
            }

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            font = content.Load<SpriteFont>(Scaler.GetFontLogBox() + "Bold");
            background = LoadPng("background-texture");
            icon = iconName == null ? null : LoadPng(Scaler.ScaledImageName(iconName));
        }

        protected override void DrawInternal(GameTime time)
        {
            int whiteBlending = (int)(255f * opacity);
            //draw covering images, because the graph is sticking out of its render area
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            spriteBatch.Draw(background, Boundaries, Boundaries, new Color(50, 50, 50, (int)(200f * opacity)));
            if (icon != null)
                spriteBatch.Draw(icon, iconLocation, new Color(iconColor.R, iconColor.G, iconColor.B, whiteBlending));
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            Vector2 topleft = textLocation;
            foreach (string line in text.Split('\n'))
            {

                if (textAlignment == Location.Right)
                    topleft.X = (int)(textLocation.X + textSize.X - font.MeasureString(line).X);
                else if (textAlignment == Location.Center)
                    topleft.X = (int)(textLocation.X + textSize.X / 2 - font.MeasureString(line).X / 2);
                else
                    topleft.X = (int)(textLocation.X);

                spriteBatch.DrawString(font, line, topleft, new Color(whiteBlending, whiteBlending, whiteBlending, whiteBlending));

                topleft.Y += font.LineSpacing;
            }
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            Rectangle fullScreenBoundaries = Utils.ScreenBoundaries(device, Matrix.Identity, View, Projection);

            int margin = 10;
            textSize = font.MeasureString(text);
            Point size = new Point(
                (int)textSize.X + (icon == null ? 0 : icon.Width) + (icon == null ? 2 : 3) * margin,
                Math.Max((int)textSize.Y, icon == null ? 0 : icon.Height) + 2 * margin
                );

            //FIXME: Only supports relative location top and align right for the moment
            if (anchorRelativePosition != Location.Top || anchorAlignment != Location.Right)
                Logger.Warn("this toast location ain't supported");

            Rectangle newBoundaries;
            if (anchor != null)
            {
                newBoundaries = new Rectangle(0, 0, size.X, size.Y);
                switch (anchorRelativePosition)
                {
                    case Location.Top:
                        newBoundaries.Y = anchor.Boundaries.Top - size.Y - margin;
                        break;
                    case Location.Bottom:
                        newBoundaries.Y = anchor.Boundaries.Bottom + margin;
                        break;
                    case Location.Left:
                        newBoundaries.X = anchor.Boundaries.Left - size.X - margin;
                        break;
                    case Location.Right:
                        newBoundaries.X = anchor.Boundaries.Right + margin;
                        break;
                    case Location.Center:
                        newBoundaries.X = anchor.Boundaries.Center.X - size.X / 2;
                        newBoundaries.Y = anchor.Boundaries.Center.Y - size.Y / 2;
                        break;
                }

                switch (anchorAlignment)
                {
                    case Location.Top:
                        newBoundaries.Y = anchor.Boundaries.Top;
                        break;
                    case Location.Bottom:
                        newBoundaries.Y = anchor.Boundaries.Bottom - size.Y;
                        break;
                    case Location.Left:
                        newBoundaries.X = anchor.Boundaries.Left;
                        break;
                    case Location.Right:
                        newBoundaries.X = anchor.Boundaries.Right - size.X;
                        break;
                    case Location.Center:
                        if (anchorRelativePosition != Location.Left && anchorRelativePosition != Location.Right)
                            newBoundaries.X = anchor.Boundaries.Center.X - size.X / 2;
                        if (anchorRelativePosition != Location.Top && anchorRelativePosition != Location.Bottom)
                            newBoundaries.Y = anchor.Boundaries.Center.Y - size.Y / 2;
                        break;
                }
            }
            else
                newBoundaries = new Rectangle(fullScreenBoundaries.Center.X - size.X / 2, fullScreenBoundaries.Center.Y - size.Y / 2,
                    size.X, size.Y);

            //make sure the full Toast is inside the screen
            if (newBoundaries.Right > fullScreenBoundaries.Right - margin)
                newBoundaries.X = fullScreenBoundaries.Right - Boundaries.Width - margin;
            if (newBoundaries.Left < fullScreenBoundaries.Left + margin)
                newBoundaries.X = fullScreenBoundaries.Left + margin;
            if (newBoundaries.Bottom > fullScreenBoundaries.Bottom - margin)
                newBoundaries.Y = fullScreenBoundaries.Bottom - Boundaries.Height - margin;
            if (newBoundaries.Top < fullScreenBoundaries.Top + margin)
                newBoundaries.Y = fullScreenBoundaries.Top + margin;

            this.Boundaries = newBoundaries;

            this.interactiveAreas = new List<Rectangle>() { Boundaries };

            //now construct final variables needed for drawing
            textLocation = new Vector2(Boundaries.X + margin, (int)(Boundaries.Center.Y - textSize.Y / 2));
            if (icon != null)
            {
                iconLocation = new Vector2((int)(Boundaries.X + margin), (int)(Boundaries.Y + margin));
                textLocation.X += icon.Width + margin;
            }
        }

        internal void Show()
        {
            hidingSince = null;
            displayedSince = DateTime.Now;
            opacity = 1f;
            Visible = true;
        }

        internal void Hide()
        {
            if (hidingSince == null)
                hidingSince = DateTime.Now;
        }

        protected override void UpdateInternal(GameTime now)
        {
            if (hideTimeout > 0 && (DateTime.Now - displayedSince).TotalMilliseconds > hideTimeout)
                Hide();

            if (Visible)
            {
                if (hidingSince != null)
                {
                    if ((DateTime.Now - hidingSince.Value).TotalMilliseconds > fadeTime)
                    {
                        Visible = false;
                    }
                    else
                    {
                        opacity = (float)(1 - ((DateTime.Now - hidingSince.Value).TotalMilliseconds / fadeTime));
                    }
                }
            }
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            this.Hide();
            ReleaseGestureControl();
        }
    }
}
