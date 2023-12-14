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
    internal class EContextMenuItemButton : EContextMenuItem
    {
        private Rectangle iconBoundaries;
        private DrawableCallback callback;
        private Texture2D backgroundTexture;
        private Texture2D icon;
        public string iconName = "";
        private StringManager stringManager;
        public List<ButtonTextDefinition> buttonStrings
        {
            get { return stringManager.Strings; }
            set { stringManager.Strings = value; }
        }

        private int iconMargin = 5;

        public EContextMenuItemButton(string icon, DrawableCallback callback) : this(null, "", icon, callback) { }
        public EContextMenuItemButton(string text, string icon, DrawableCallback callback) : this(null, text, icon, callback) { }
        public EContextMenuItemButton(EDrawable parent, string text, string icon, DrawableCallback callback) : this(parent, new List<ButtonTextDefinition>(new ButtonTextDefinition[] { new ButtonTextDefinition(text, VerticalTextPosition.Bottom, MappedColor.Font, ContextMenuTextSizes.Medium) }), icon, callback) { }
        public EContextMenuItemButton(EDrawable parent, List<ButtonTextDefinition> buttonStrings, string icon, DrawableCallback callback) : base()
        {
            this.iconName = icon;
            this.callback = callback;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;

            this.stringManager = new StringManager();
            stringManager.Strings = buttonStrings;
            this.AddChild(stringManager);

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            this.backgroundTexture = LoadPng("white");
            if (iconName != "" && iconName != null)
                this.icon = LoadPng(Scaler.ScaledImageName(iconName));
        }

        public void SetCallback(DrawableCallbackDelegate callback, object argument = null)
        {
            this.callback = new DrawableCallback(callback, argument);
        }

        public void Fire()
        {
            callback.Call(this);
        }

        protected override void DrawInternal(GameTime time)
        {
            //render background and icon
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(backgroundTexture, Boundaries, MappedColor.ContextMenuBackground.C());
            if (icon != null)
                spriteBatch.Draw(icon, new Vector2(Boundaries.X, Boundaries.Y), MappedColor.ContextMenuText.C());
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            this.interactiveAreas = new List<Rectangle>() { Boundaries };
            stringManager.SetBoundaries(this.Boundaries);

            if (stringManager.Strings.Count == 0)
                iconBoundaries = new Rectangle(Boundaries.Left + (int)((float)Boundaries.Width * 0.2f), Boundaries.Top, (int)((float)Boundaries.Width * 0.6f), (int)((float)Boundaries.Width * 0.6f));
            else
                iconBoundaries = new Rectangle(Boundaries.Left + iconMargin, Boundaries.Top + iconMargin, Boundaries.Width - 2 * iconMargin, Boundaries.Height - 2 * iconMargin);
        }

        protected virtual void OnTap()
        {
            Fire();
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
