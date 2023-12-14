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

    internal class Indicator : EDrawableVertices
    {
        protected MappedColor color;
        protected MappedColor textColor;
        public Location selectorSide { get; protected set; }
        protected SpriteFont centerFont;
        protected SpriteFont bottomFont;
        private bool muted = false;
        public bool Muted { get { return muted; } set { if (muted != value) redrawRequest = true; muted = value; } }
        protected Point screenCenterPosition = new Point();

        public override Point? Size
        {
            get
            {
                return new Point(textureSelected.Width, textureSelected.Height);
            }
        }

        SpriteEffects indicatorEffect;
        protected Texture2D textureNotSelected;
        protected Texture2D textureSelected;
        protected Texture2D textureHiContrast;
        protected Rectangle textureRect;
        float textureRotation;
        public Point Center
        {
            get
            {
                if (this.textureAreaRect.Center.X == 0 && this.textureAreaRect.Center.Y == 0) OnBoundariesChangedInternal();    //FIXME: this is a flaw in the entire Drawable scheme. Components should call OnChangedInternal when instantiated.
                return this.textureAreaRect.Center;
            }
        }
        
        Rectangle textureAreaRect;
        private string centerText;
        public string CenterText { get { return centerText; } set { this.centerText = value; RecomputeTextPositions(); } }
        private string bottomText;
        public string BottomText { get { return bottomText; } set { this.bottomText = value; RecomputeTextPositions(); } }
        float textureAngle;
        Vector2 centerTextPosition;
        Vector2 bottomTextPosition;

        string contentIconImageName;
        Vector2 contentIconPosition;
        protected Texture2D contentIconTexture;
        private bool showContentIcon = false;
        internal bool ShowContentIcon
        {
            set
            {
                LoadContentIcon();
                RecomputePosition();
                showContentIcon = value;
            }
            get
            {
                return showContentIcon;
            }
        }


        internal bool contextMenuShown = false;
        
        protected float relativeLocation = 0;
        public bool Selected { get; set; }

        public MappedColor Color { set { this.color = value; } }
        private string imageNotSelected, imageSelected, imageHighContrast;

        public Indicator(
            Grid grid, 
            string imageNotSelected, string imageSelected, string imageHighContrast, 
            MappedColor color, MappedColor textColor,
            Location selectorSide, 
            string centerText, string bottomText,
            string contentIconImageName = null)
            : base()
        {
            this.centerText = centerText;
            this.bottomText = bottomText;

            this.imageHighContrast = imageHighContrast;
            this.imageNotSelected = imageNotSelected;
            this.imageSelected = imageSelected;

            this.contentIconImageName = contentIconImageName;

            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
            this.color = color;
            this.textColor = textColor;
            this.selectorSide = selectorSide;

            grid.OnBoundariesChangedDelegate += this.SetBoundaries;

            this.Boundaries = grid.Boundaries;
            MustRecalcNextUpdateCycle = true;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            textureNotSelected = LoadPng(Scaler.ScaledImageName(imageNotSelected));
            textureSelected = LoadPng(Scaler.ScaledImageName(imageSelected));
            textureHiContrast = LoadPng(Scaler.ScaledImageName(imageHighContrast));
            centerFont = content.Load<SpriteFont>(Scaler.GetFontIndicatorCenter());
            bottomFont = content.Load<SpriteFont>(Scaler.GetFontIndicatorBottom());
            LoadContentIcon();
        }

        public void LoadContentIcon(string name = null)
        {
            if (name != null)
                this.contentIconImageName = name;

            if (contentIconImageName != null)
                contentIconTexture = LoadPng(Scaler.ScaledImageName(contentIconImageName));
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw covering images, because the graph is sticking out of its render area
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            Vector2 origin = new Vector2(textureNotSelected.Width, textureNotSelected.Height / 2f);
            spriteBatch.Draw(contextMenuShown ? textureHiContrast : Selected ? textureSelected : textureNotSelected, textureRect, null, color.C(), textureRotation, origin, indicatorEffect, 0);
            if (!Muted)
            {

                if (ShowContentIcon && contentIconTexture != null)
                {
                    spriteBatch.Draw(contentIconTexture, contentIconPosition, textColor.C());
                }
                else
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
                    if (centerText != null)
                        spriteBatch.DrawString(centerFont, centerText, centerTextPosition, contextMenuShown ? color.CC() : textColor.C());
                    if (bottomText != null)
                        spriteBatch.DrawString(bottomFont, BottomText, bottomTextPosition, contextMenuShown ? color.CC() : textColor.C());
                }
            }  
            spriteBatch.End();
        }

        public float Position
        {
            set
            {
                if (value.Equals(float.PositiveInfinity))
                    this.relativeLocation = 0f;
                else
                    this.relativeLocation = value;
                RecomputePosition();
            }
            get
            {
                return this.relativeLocation;
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            RecomputePosition();
        }

        public virtual void RecomputePosition()
        {
            redrawRequest = true;
            if (!this.contentLoaded) LoadContent();

            Vector3 indicatorLocation = Vector3.Zero;
            bool clipping = false;
            float relativeLocationClipped = relativeLocation;
            if (Math.Abs(relativeLocation) > 0.5)
            {
                clipping = true;
                relativeLocationClipped = Math.Sign(relativeLocation) * 0.5f;
            }

            switch (selectorSide)
            {
                case Location.Left:
                    indicatorLocation = new Vector3(0, 0.5f-relativeLocationClipped, 0);
                    textureRotation = 0f;
                    indicatorEffect = SpriteEffects.None;
                    break;
                case Location.Right:
                    indicatorLocation = new Vector3(1, 0.5f-relativeLocationClipped, 0);
                    textureRotation = MathHelper.Pi;
                    indicatorEffect = SpriteEffects.FlipVertically;
                    break;
                case Location.Top:
                    indicatorLocation = new Vector3(0.5f+ relativeLocationClipped, 0, 0);
                    textureRotation = MathHelper.PiOver2;
                    indicatorEffect = SpriteEffects.None;
                    break;
                case Location.Bottom:
                    textureRotation = -MathHelper.PiOver2;
                    indicatorLocation = new Vector3(0.5f+ relativeLocationClipped, 1, 0);
                    indicatorEffect = SpriteEffects.FlipVertically;
                    break;
            }

            if (clipping)
            {
                int sign = (selectorSide == Location.Left || selectorSide == Location.Top) ? -1 : 1;
                textureRotation += sign * MathHelper.PiOver2 * Math.Sign(relativeLocationClipped);
            }

            //calculate once the location of the VOffset indicator            
            indicatorLocation.X = (float)Boundaries.X+((float)Boundaries.Width)*indicatorLocation.X;
            indicatorLocation.Y = (float)Boundaries.Y + ((float)Boundaries.Height) * indicatorLocation.Y;

            int indicatorWidth = textureNotSelected.Width;
            int indicatorHeight = textureNotSelected.Height;

            //store rectangle info once -- in this case, only the tip location is required, as this will be used as pivot point
            this.textureRect = new Rectangle((int)indicatorLocation.X, (int)indicatorLocation.Y, indicatorWidth, indicatorHeight);
            
            Vector2 arrowTipLocationV2 = new Vector2(textureRect.X, textureRect.Y);

            Vector2 topLeft = new Vector2(-indicatorWidth, -indicatorHeight / 2);
            Vector2 botRight = new Vector2(0, indicatorHeight / 2);
            Matrix rotMatrix = Matrix.CreateRotationZ(textureRotation);
            Vector2 topLeftScreen = Vector2.Transform(topLeft, rotMatrix) + arrowTipLocationV2;
            Vector2 botRightScreen = Vector2.Transform(botRight, rotMatrix) + arrowTipLocationV2;
            //now need to make sure rectangle only contains positive values
            int screenTopX = (int)Math.Min(topLeftScreen.X, botRightScreen.X);
            int screenTopY = (int)Math.Min(topLeftScreen.Y, botRightScreen.Y);
            int screenWidth = (int)Math.Abs(topLeftScreen.X - botRightScreen.X);
            int screenHeight = (int)Math.Abs(topLeftScreen.Y - botRightScreen.Y);

            //and throw into rectangle structure
            textureAreaRect = new Rectangle(screenTopX, screenTopY, screenWidth, screenHeight);
            textureAngle = Math.Abs(textureRotation % MathHelper.Pi);            
            if (contentIconTexture != null)
                contentIconPosition = new Vector2((int)(textureAreaRect.Center.X - contentIconTexture.Width / 2), (int)(textureAreaRect.Center.Y - contentIconTexture.Height / 2));
            
            interactiveAreas[0] = textureAreaRect;

            RecomputeTextPositions();     
        }

        private void RecomputeTextPositions()
        {
            //Calculate positions for accopagnying text and value
            if (centerText != null)
            {
                Vector2 centerTextSize = centerFont.MeasureString(centerText);
                centerTextPosition = new Vector2((int)(textureAreaRect.Center.X - centerTextSize.X / 2), (int)(textureAreaRect.Center.Y - centerTextSize.Y / 2));
            }
            if (BottomText != null)
            {
                int bottomTextBaseline = (int)(textureAreaRect.Height * 4 / 5);
                Vector2 bottomTextSize = bottomFont.MeasureString(BottomText);
                if (bottomText != null)
                    centerTextPosition.Y -= bottomTextSize.Y / 4;
                bottomTextPosition = new Vector2((int)(textureAreaRect.Center.X - bottomTextSize.X / 2), textureAreaRect.Y + bottomTextBaseline - bottomTextSize.Y);
            }
            screenCenterPosition = textureAreaRect.Center;
        }
    }
}
