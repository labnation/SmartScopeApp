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
    internal class EGraphicImageBackground : EDrawable
    {
        private Texture2D texture;
        private Color multiplyColor;
        private MappedColor multiplyManagedColor;
        private Rectangle sourceRectangle;
        private bool stretchFullScreen;
        private string textureName;

        //from the outside, this constructor accepts either a fixed color, or a MappedColor
        public EGraphicImageBackground(string textureName, MappedColor managedColor, bool strechFullScreen)
            : base()
        {
            this.textureName = textureName;
            this.stretchFullScreen = strechFullScreen;
            this.multiplyManagedColor = managedColor;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            this.texture = LoadPng(textureName);
        }

        //public EGraphicImageBackground(EDrawable parent, string textureName, bool strechFullScreen) : this(parent, textureName, Color.White, strechFullScreen) { }

        protected override void DrawInternal(GameTime time)
        {
            //simply draw the image to span the entire screen
            spriteBatch.Begin();
            spriteBatch.Draw(texture, Boundaries, sourceRectangle, multiplyColor);
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            if (stretchFullScreen)
                sourceRectangle = new Rectangle(0, 0, (int)((float)Boundaries.Width / (float)texture.Width), (int)((float)Boundaries.Height / (float)texture.Height));
            else
                sourceRectangle = new Rectangle(0, 0, (int)((float)Boundaries.Width / (float)3), (int)((float)Boundaries.Height / (float)3));

            //if color is managed: update
            multiplyColor = ((MappedColor)multiplyManagedColor).C();
        }
    }
}
