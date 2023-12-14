using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.Common;

namespace ESuite.Drawables
{
    internal class MenuItemValue : MenuItem
    {
        Vector2 interItemMarginScaler = new Vector2(0.4f, 1f);
        
        SpriteFont valueFont;
        Vector2 valueTextLocation;
        Vector2 valueTextSize;
		Rectangle valueTextArea;
		public DrawableCallback OnValueTapCallback = null;

        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public double Precision { get; private set; }
        int significance = 3;
        public string Unit { get; private set; }
        double _value = 0f;
        public bool UseSiScaling = true;
        private string valueText = "";
        
        public double Value
        {
            get { return _value; }
            set
            {
                if (value > Maximum || value < Minimum) return;
                this._value = value;

                if (Unit == "s")
                    this.valueText = TimeSpan.FromSeconds(_value).ToString();
                else if (Unit == "m")
                    this.valueText = TimeSpan.FromMinutes(_value).ToString();
                else
                {
                    if (UseSiScaling)
                        this.valueText = LabNation.Common.Utils.siScale(_value, Precision, significance) + " " + LabNation.Common.Utils.siPrefix(_value, Precision, Unit);
                    else
                        this.valueText = LabNation.Common.Utils.precisionFormat(_value, Precision, significance) + this.Unit;
                }

                ComputeScales();
                if (action != null)
                    action.Call(this, value);
            }
        }
        
		internal MenuItemValue(string text, DrawableCallbackDelegate action, object delegateArgument, double minimum, double maximum, double precision, double value = 0, string unit = "", bool useSiScale = false, bool selected = false) :
            base(text, action, delegateArgument, null, ExpansionMode.None, selected)
        {
            this.UseSiScaling = useSiScale;
            this.Minimum = minimum;
            this.Maximum = maximum;
            this.Precision = precision;
            this.Unit = unit;
            
            if (value <= maximum && value >= minimum) 
                this.Value = value;
            else
                this.Value = (maximum + minimum) / 2f;

            LoadContent();
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(whiteTexture, Boundaries, bgColor);
			spriteBatch.End(); 

			spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            spriteBatch.DrawString(usedFont, this.text, this.textLocation, fgColor);
            spriteBatch.DrawString(valueFont, valueText, valueTextLocation, fgColor);
            spriteBatch.End();
        }

        protected override void ComputeScales()
        {
            base.ComputeScales();

            if (!contentLoaded)
                return;            
            
            valueTextSize = valueFont.MeasureString(valueText);
            valueTextLocation = new Vector2(Boundaries.Right - valueTextSize.X - Margin.X, Boundaries.Center.Y - valueTextSize.Y / 2);
			valueTextArea = new Rectangle((int)valueTextLocation.X, (int)valueTextLocation.Y, (int)valueTextSize.X, (int)valueTextSize.Y);
        }
        
        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
            valueFont = content.Load<SpriteFont>(Scaler.GetFontSliderValue());
        }

        protected override void DoTap(Point position, object argument = null)
		{
            if (OnValueTapCallback != null)
                OnValueTapCallback.Call(this);

            base.DoTap(position, Utils.extendArgument(this.Value, argument));
        }
    }
}
