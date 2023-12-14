using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;

namespace ESuite.Drawables
{
    public enum VerticalTextPosition { Top, Above, Center, Below, Bottom }
    public enum ContextMenuTextSizes { Large, Medium, Small, Tiny }

    public delegate string GetFontDelegate();
    public struct ButtonTextDefinition
    {
        public string Text;
        public VerticalTextPosition VerticalTextPosition;
        public MappedColor Color;
        public ContextMenuTextSizes Size;
        public ButtonTextDefinition(string text, VerticalTextPosition verticalTextPostion, MappedColor color, ContextMenuTextSizes size)
        {
            this.Text = text;
            this.VerticalTextPosition = verticalTextPostion;
            this.Color = color;
            this.Size = size;
        }
    }

    internal class StringManager:EDrawable
    {
        private Dictionary<ContextMenuTextSizes, SpriteFont> fonts = new Dictionary<ContextMenuTextSizes, SpriteFont>();
        private List<Vector2> textLocations;
        public List<ButtonTextDefinition> Strings
        {
            get { return this.strings; }
            set {
                //only recalc positions when contents of strings was actually changed
                bool needsUpdate = true;
                if (strings == null)
                    needsUpdate = true;
                else if (strings.Count != value.Count)
                    needsUpdate = true;
                else
                    for (int i = 0; i < value.Count; i++)
                        if (value[i].Text != strings[i].Text)
                            needsUpdate = true;

                this.strings = value; 

                if (needsUpdate)
                    OnBoundariesChanged(); 
            }
        }
        private List<ButtonTextDefinition> strings;
        
        public StringManager() : base()
        {
            LoadContent();
        }

        protected override void LoadContentInternal() 
        {
            fonts.Clear();
            fonts.Add(ContextMenuTextSizes.Large, content.Load<SpriteFont>(Scaler.GetFontContextMenuLarge()));
            fonts.Add(ContextMenuTextSizes.Medium, content.Load<SpriteFont>(Scaler.GetFontContextMenuMedium()));
            fonts.Add(ContextMenuTextSizes.Small, content.Load<SpriteFont>(Scaler.GetFontContextMenuSmall()));
            fonts.Add(ContextMenuTextSizes.Tiny, content.Load<SpriteFont>(Scaler.GetFontContextMenuTiny()));
        }

        protected override void UpdateInternal(GameTime now) { }

        protected override void DrawInternal(GameTime time) 
        {
            //render text
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            for (int i = 0; i < Strings.Count; i++)
            {
                if (Strings[i].Text != null)
                {
                    /* check whether text would fit, otherwise accomodate */
                    Vector2 textLocation = textLocations[i];
                    float scaler = 1f;
                    Vector2 textSize = fonts[Strings[i].Size].MeasureString(Strings[i].Text);
                    if (textSize.X > Boundaries.Width)
                    {
                        scaler = (float)Boundaries.Width / textSize.X;
                        textLocation += 0.5f * textSize * (1f - scaler);
                    }

                    /* draw string */
                    spriteBatch.DrawString(fonts[Strings[i].Size], Strings[i].Text, textLocation, Strings[i].Color.C(), 0, Vector2.Zero, scaler, SpriteEffects.None, 0);
                }
            }
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal() 
        {
            if (fonts.Count == 0) return; // not yet initialized

            //find out Y positions for different VerticalTextPositions
            Dictionary<ContextMenuTextSizes, Dictionary<VerticalTextPosition, float>> textYPositions = new Dictionary<ContextMenuTextSizes, Dictionary<VerticalTextPosition, float>>();

            foreach (ContextMenuTextSizes size in Enum.GetValues(typeof(ContextMenuTextSizes)))
            {
                Vector2 textSize = fonts[size].MeasureString("Q");
                Dictionary<VerticalTextPosition, float> currSizeDict = new Dictionary<VerticalTextPosition, float>();
                currSizeDict.Add(VerticalTextPosition.Top, Boundaries.Top);
                currSizeDict.Add(VerticalTextPosition.Above, Boundaries.Top + Boundaries.Height / 3 - textSize.Y / 2f);
                currSizeDict.Add(VerticalTextPosition.Center, Boundaries.Center.Y - textSize.Y / 2f);
                currSizeDict.Add(VerticalTextPosition.Below, Boundaries.Bottom - Boundaries.Height / 3 - textSize.Y / 2f);
                currSizeDict.Add(VerticalTextPosition.Bottom, Boundaries.Bottom - textSize.Y);

                textYPositions.Add(size, currSizeDict);
            }

            //calc XY pos for each string to draw
            textLocations = new List<Vector2>();
            for (int i = 0; i < Strings.Count; i++)
            {
                if (Strings[i].Text != null)
                {
                    Vector2 textSize = fonts[Strings[i].Size].MeasureString(Strings[i].Text);
                    textLocations.Add(new Vector2((int)(Boundaries.Center.X - textSize.X / 2f), (int)(textYPositions[Strings[i].Size][Strings[i].VerticalTextPosition])));
                }
            }
        }
    }
}
