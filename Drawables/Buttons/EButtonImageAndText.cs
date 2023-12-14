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
    internal class EButtonImageAndText : EDrawable
    {
        public float Rotation = 0f;
        protected Vector2 textLocation;
        internal DrawableCallback DoubleTapCallback;
        internal DrawableCallback TapCallback { set; get; }

        private DrawableCallback dragCallback;
        public DrawableCallback DragCallback
        {
            set
            {
                TapOnDragComplete = value == null;
                this.dragCallback = value;
            }
            get { return this.dragCallback; }
        }
        private DrawableCallback dropCallback;
        public DrawableCallback DropCallback
        {
            set
            {
                TapOnDragComplete = value == null;
                this.dropCallback = value;
            }
            get { return this.dropCallback; }
        }


        protected Texture2D buttonTexture;
        protected SpriteFont font;
        public bool disabled = false;
        public int HorizontalTextPosition { get { return (int)this.textLocation.X; } }

        private Vector4 _margin = Vector4.Zero;
        //Vector indicating margin (top, right, bottom, left) in inches
        public Vector4 Margin
        {
            get { return _margin; }
            set { _margin = value; OnBoundariesChanged(); }
        }
        private float? fontSize;
        public float? FontSize
        {
            get { return fontSize; }
            set { fontSize = value; LoadContent(); }
        }
        private bool _tightBoundaries = false;
        /// <summary>
        /// When true, in case the texture's height is larger than the computed Boundaries,
        /// the Boundaries will be heightened.
        /// </summary>
        public bool TightBoundaries
        {
            get { return _tightBoundaries; }
            set { _tightBoundaries = value; OnBoundariesChanged(); }
        }
        private bool _stretchTexture = false;
        public bool StretchTexture
        {
            get { return _stretchTexture; }
            set { _stretchTexture = value; OnBoundariesChanged(); }
        }
        public Location TextPosition
        {
            get { return textPosition; }
            set { textPosition = value; OnBoundariesChanged(); }
        }

        protected string text;
        public string Text
        {
            get { return text; }
            set
            {
                this.text = value;
                OnBoundariesChanged();
            }
        }
        protected MappedColor textureColor;
        protected MappedColor textColor;
        protected bool textVisible = true;
        public bool TextVisible
        {
            get { return textVisible; }
            set
            {
                textVisible = value;
                OnBoundariesChanged();
            }
        }

        public bool TapOnDragComplete = true;

        private string image;
        public override Point? Size { get { return new Point(Boundaries.Width, Boundaries.Height); } }
        protected Rectangle buttonTextureRectangle;
        private Location verticalAlign;
        private Location horizontalAlign;
        private Location textPosition;
        public StringManager stringManager;
        MappedColor backgroundColor = MappedColor.Transparent;

        internal override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                base.Visible = value;
                if (stringManager != null)
                    stringManager.Visible = value;
            }
        }

        public EButtonImageAndText(string image, MappedColor imageColor, List<ButtonTextDefinition> textDefinitions)
            : base()
        {
            this.image = image;
            this.backgroundColor = MappedColor.Transparent;
            this.textureColor = imageColor; //FIXME: should rethink how singleText and textDefinitions can work together in a better way, or split up this class
            this.verticalAlign = Location.Center;

            this.stringManager = new StringManager();
            this.stringManager.Strings = textDefinitions;
            this.AddChild(stringManager);
            this.textColor = textDefinitions.First().Color;

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;

            LoadContent();
        }

        public EButtonImageAndText(
            string image, MappedColor imageColor,
            string text, MappedColor textColor,
            Location vAlign = Location.Center,
            Location textPosition = Location.Right,
            Location hAlign = Location.Left,
            MappedColor backgroundColor = MappedColor.Transparent)
            : base()
        {
            this.image = image;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
            this.text = text;
            this.textColor = textColor;
            this.textureColor = imageColor;
            this.verticalAlign = vAlign;
            this.horizontalAlign = hAlign;
            this.textPosition = textPosition;
            this.backgroundColor = backgroundColor;

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            if (image == null)
                this.buttonTexture = whiteTexture;
            else
                this.buttonTexture = LoadPng(Scaler.ScaledImageName(image));
            this.font = content.Load<SpriteFont>(fontSize.HasValue ? Scaler.FontBuilder(fontSize.Value) : Scaler.GetFontButtonBar());
        }

        protected override void DrawInternal(GameTime time)
        {
            DrawInternal(
                (disabled ? MappedColor.Disabled : textureColor).C(),
                (disabled ? MappedColor.DisabledFont : textColor).C(),
                text,
                buttonTexture
            );
        }

        protected void DrawInternal(Color buttonColor, Color txtColor, string text, Texture2D texture)
        {
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            Rectangle finalDrawRectangle = new Rectangle(buttonTextureRectangle.X + (int)(buttonTextureRectangle.Width / 2), buttonTextureRectangle.Y + (int)(buttonTextureRectangle.Height / 2), buttonTextureRectangle.Width, buttonTextureRectangle.Height);
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);

            if (backgroundColor != MappedColor.Transparent)
                spriteBatch.Draw(whiteTexture, buttonTextureRectangle, backgroundColor.C());

            if (Rotation == 0f)
                spriteBatch.Draw(texture, buttonTextureRectangle, buttonColor);
            else
                spriteBatch.Draw(texture, finalDrawRectangle, null, buttonColor, Rotation, origin, SpriteEffects.None, 0);
            if (stringManager == null && text != null && text != "" && TextVisible)
            {
                spriteBatch.End(); spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
                spriteBatch.DrawString(font, text, textLocation, txtColor);
            }
            spriteBatch.End();
        }

        internal void UpdateImage(string image)
        {
            this.image = image;
            LoadContent();
            OnBoundariesChanged();
        }

        protected override void OnBoundariesChangedInternal()
        {
            if (StretchTexture)
            {
                buttonTextureRectangle = Boundaries;
            }
            else if (stringManager == null || stringManager.Strings == null || this is EButtonImageAndTextSelectable)  //FIXME: dirty hack, as when textDefinitions the boundaries should be defined by the stringManager. Again: should rethink how singleText and textDefinitions can work together in a better way, or split up this class
            {
                //Stretch Boundaries height to image
                if (buttonTexture.Height > Boundaries.Height || TightBoundaries)
                    this.Boundaries = new Rectangle(Boundaries.X, Boundaries.Y, Boundaries.Width, buttonTexture.Height);

                Vector2 buttonTextureTopLeft = new Vector2();
                switch (verticalAlign)
                {
                    case Location.Top:
                        buttonTextureTopLeft = new Vector2(0, Boundaries.Y);
                        break;
                    case Location.Center:
                        buttonTextureTopLeft = new Vector2(0, Boundaries.Center.Y - buttonTexture.Height / 2);
                        break;
                    case Location.Bottom:
                        buttonTextureTopLeft = new Vector2(0, Boundaries.Height - buttonTexture.Height);
                        break;
                }
                switch (horizontalAlign)
                {
                    case Location.Left:
                        buttonTextureTopLeft.X = Boundaries.X;
                        this.Boundaries = new Rectangle(Boundaries.X, Boundaries.Y, buttonTexture.Width, Boundaries.Height);
                        break;
                    case Location.Center:
                        if (Boundaries.Width < buttonTexture.Width)
                            this.Boundaries = new Rectangle(Boundaries.X, Boundaries.Y, buttonTexture.Width, Boundaries.Height);
                        buttonTextureTopLeft.X = Boundaries.Center.X - buttonTexture.Width / 2;
                        break;
                    case Location.Right:
                        buttonTextureTopLeft.X = Boundaries.Right - buttonTexture.Width;
                        this.Boundaries = new Rectangle(Boundaries.X, Boundaries.Y, buttonTexture.Width, Boundaries.Height);
                        break;
                }
                buttonTextureRectangle = new Rectangle((int)(buttonTextureTopLeft.X), (int)(buttonTextureTopLeft.Y), (int)(buttonTexture.Width), (int)(buttonTexture.Height));
            }

            if (Margin != Vector4.Zero)
            {
                buttonTextureRectangle = new Rectangle(buttonTextureRectangle.X + Margin.W.InchesToPixels(), buttonTextureRectangle.Y + Margin.X.InchesToPixels(), buttonTextureRectangle.Width - (Margin.Y + Margin.W).InchesToPixels(), buttonTextureRectangle.Height - (Margin.X + Margin.Z).InchesToPixels());
            }

            if (text != null && text != "" && TextVisible)
            {
                Vector2 textSize = font.MeasureString(text);
                int verticalPosition = (int)(buttonTextureRectangle.Y + buttonTextureRectangle.Height / 2f - textSize.Y / 2f);
                switch (textPosition)
                {
                    case Location.Left:
                        throw new NotImplementedException("Lalalaa not implemented");
                    case Location.Right: //Text on right of image, stretch up Boundaries
                        if (StretchTexture)
                            textLocation = new Vector2((int)(buttonTextureRectangle.Right - textSize.X - Scaler.MenuItemMargin.X), verticalPosition);
                        else
                            textLocation = new Vector2((int)(Boundaries.Left + buttonTexture.Width + Scaler.MenuItemMargin.X), verticalPosition);
                        if (!StretchTexture)
                        {
                            int newWidth = (int)(textLocation.X + font.MeasureString(text).X - Boundaries.Left);
                            this.Boundaries = new Rectangle(Boundaries.X, Boundaries.Y, newWidth, Boundaries.Height);
                        }
                        break;
                    case Location.Center: //Center text within current Boundaries and don't stretch them up
                        textLocation = new Vector2((int)(Boundaries.Center.X - textSize.X / 2), verticalPosition);
                        break;
                }
            }

            this.interactiveAreas = new List<Rectangle>() { Boundaries };

            if (stringManager != null)
                stringManager.SetBoundaries(Boundaries);
        }

        protected virtual void OnTap(object argument = null)
        {
            if (!disabled && TapCallback != null)
                TapCallback.Call(this, argument);
        }

        protected virtual void OnDoubleTap(object argument = null)
        {
            if (!disabled && DoubleTapCallback != null)
                DoubleTapCallback.Call(this, argument);
            else
                OnTap(argument);
        }

        protected virtual void OnDrag(GestureSample gesture)
        {
            if (DragCallback != null)
                DragCallback.Call(this, gesture);
        }

        protected virtual void OnDrop(GestureSample gesture)
        {
            if (DropCallback != null)
                DropCallback.Call(this, gesture);
            else if (TapOnDragComplete)
                OnTap();
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DragComplete:
                    OnDrop(gesture);
                    ReleaseGestureControl();
                    break;
                case GestureType.Tap:
                    OnTap();
                    ReleaseGestureControl();
                    break;
                case GestureType.DoubleTap:
                    OnDoubleTap();
                    ReleaseGestureControl();
                    break;
                case GestureType.FreeDrag:
                    //ReleaseGestureControl must not be called here! Or DragComplete will not cause a Tap
                    OnDrag(gesture);
                    break;
                case GestureType.Pinch:
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
