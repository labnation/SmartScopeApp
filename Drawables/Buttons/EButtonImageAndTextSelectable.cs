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
    internal class EButtonImageAndTextSelectable : EButtonImageAndText
    {
        private Texture2D buttonTexture2;
        private string text2;
        public List<ButtonTextDefinition> enabledDefinitions, disabledDefintions;
        private MappedColor textureColor2;
        private MappedColor textureColorHighlighted;
        private MappedColor textColor2;
        private MappedColor textColorHighlighted;
        private bool selected = false;
        public bool Selected
        {
            get { return this.selected; }
            set
            {
                if (this.selected != value)
                    redrawRequest = true;

                this.selected = value;
                if (this.stringManager != null)
                {
                    if (value)
                        this.stringManager.Strings = enabledDefinitions;
                    else
                        this.stringManager.Strings = disabledDefintions;
                }
            }
        }
        private bool highlighted = false;
        public bool Highlighted
        {
            get { return this.highlighted; }
            set
            {
                if (this.highlighted != value)
                    redrawRequest = true;
                this.highlighted = value;
            }
        }
        private string imageName;

        public EButtonImageAndTextSelectable(string image1, MappedColor imageColor1, List<ButtonTextDefinition> textDefinitions1, string image2, MappedColor imageColor2, List<ButtonTextDefinition> textDefinitions2)
            : base(image1, imageColor1, textDefinitions1)
        {
            this.imageName = image2;
            this.textureColor2 = imageColor2;
            this.enabledDefinitions = textDefinitions1;
            this.disabledDefintions = textDefinitions2;

            LoadContent();
        }

        public EButtonImageAndTextSelectable(string image1, MappedColor imageColor1, string image2, MappedColor imageColor2, string text1, MappedColor textColor1, string text2, MappedColor textColor2, MappedColor highlightImageColor, MappedColor highlightTextColor) :
            this(image1, imageColor1, image2, imageColor2, text1, textColor2, text2, textColor2, false, true, Location.Center, Location.Right, highlightImageColor, highlightTextColor)
        { }

        public EButtonImageAndTextSelectable(
            string image1,
            MappedColor imageColor1,
            string image2,
            MappedColor imageColor2,
            string text1, MappedColor textColor1,
            string text2, MappedColor textColor2,
            bool selected = false, bool selfUpdate = true,
            Location vAlign = Location.Center, Location textPosition = Location.Right,
            MappedColor? highlightImageColor = null, MappedColor? highlightTextColor = null,
            Location hAlign = Location.Left)
            : base(image1, imageColor1, text1, textColor1, vAlign, textPosition, hAlign)
        {
            this.Selected = selected;
            this.imageName = image2;
            this.text2 = text2;
            this.textureColor2 = imageColor2;
            this.textColor2 = textColor2;

            this.textColorHighlighted = highlightTextColor.HasValue ? highlightTextColor.Value : textColor2;
            this.textureColorHighlighted = highlightImageColor.HasValue ? highlightImageColor.Value : textureColor2;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            if (imageName == null)
                this.buttonTexture2 = whiteTexture;
            else
                this.buttonTexture2 = LoadPng(Scaler.ScaledImageName(imageName));
            base.LoadContentInternal();
        }

        protected override void DrawInternal(GameTime time)
        {
            Color buttonColor = (disabled ? MappedColor.Disabled :
                                        Highlighted ? textureColorHighlighted :
                                            Selected ? textureColor2 : textureColor).C();

            Texture2D texture = (Selected || Highlighted) ? buttonTexture2 : buttonTexture;
            Color txtColor = Color.White;
            string buttonText = "";
            if (this.stringManager == null)
            {
                txtColor = (disabled ? MappedColor.Disabled :
                                    Highlighted ? textColorHighlighted :
                                        Selected ? textColor2 : textColor).C();
                buttonText = (Selected && text2 != null) ? text2 : text;
            }

            DrawInternal(buttonColor, txtColor, buttonText, texture);
        }

        protected override void OnTap(object argument)
        {
            base.OnTap(!Selected);
        }

        protected override void OnDoubleTap(object argument)
        {
            base.OnDoubleTap(!Selected);
        }

        internal void UpdateImage2(string image)
        {
            this.imageName = image;
            LoadContent();
            OnBoundariesChanged();
        }
    }
}
