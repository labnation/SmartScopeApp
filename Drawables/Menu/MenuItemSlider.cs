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
    internal class MenuItemSlider : MenuItem
    {
        Vector2 interItemMarginScaler = new Vector2(0.4f, 1f);
        int sliderHeight = 1; //Slider height in px

        Vector2 knobTopLeft;
        Texture2D knobTexture;

        Rectangle iconRect;
        string iconName;
        Texture2D iconTexture;

        Rectangle sliderRect;
        
        SpriteFont valueFont;
        Vector2 valueTextLocation;
        Vector2 valueTextSize;

        internal bool dragging = false;
        
        bool logScale;

        private double minimum;
        public double Minimum { get { return minimum; } private set { minimum = value; redrawRequest = true; } }

        private double maximum;
        public double Maximum { get { return maximum; } private set { maximum = value; redrawRequest = true; } }

        private double precision;
        public double Precision { get { return precision; } private set { precision = value; redrawRequest = true; } }

        int significance = 3;
        public string Unit { get; private set; }
        double _value = 0f;
        
        public double Value
        {
            get { return _value; }
            set
            {
                redrawRequest = true;
                bool valueChanged = _value != value;
                if (value > Maximum || value < Minimum) return;
                this._value = value;
                this.text = LabNation.Common.Utils.siScale(_value, Precision, significance) + " " + LabNation.Common.Utils.siPrefix(_value, Precision, Unit);
                ComputeScales();
                if (action != null && valueChanged)
                    action.Call(this, value);
            }
        }
        internal float _position;
        internal float Position
        {
            get {
                if (dragging)
                    return _position;
                else
                {
                    if (logScale)
                        return (float)((Math.Log10(Value) - Math.Log10(Minimum)) / (Math.Log10(Maximum) - Math.Log10(Minimum)));
                    else
                        return (float)((Value - Minimum) / (Maximum - Minimum));
                }
            }
            set
            {
                redrawRequest = true;
                //Write directly to the _position variable when dragging to retain accuracy
                //instead of passing through the conversion to value
                if (dragging)
                    _position = value;
                //When not draggin, the position is not settable. It is then computed from
                //the value
            }
        }

        internal MenuItemSlider(DrawableCallbackDelegate action, object delegateArgument, string icon, double minimum, double maximum, double precision, double value = 0, string unit = "", bool logScale = false) :
            this("", action, delegateArgument, icon, minimum, maximum, precision, value, unit, logScale) { }
        internal MenuItemSlider(string text, DrawableCallbackDelegate action, object delegateArgument, string icon, double minimum, double maximum, double precision, double value = 0, string unit = "", bool logScale = false) :
            base(text, action, delegateArgument, null, ExpansionMode.None, false)
        {
            this.logScale = logScale;
            this.Minimum = minimum;
            this.Maximum = maximum;
            this.Precision = precision;
            this.Unit = unit;
            this.iconName = icon;
            
            if (value <= maximum && value >= minimum) 
                this._value = value;
            else
                this._value = (maximum + minimum) / 2f;

            LoadContent();
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(whiteTexture, Boundaries, bgColor);
            spriteBatch.Draw(whiteTexture, sliderRect, fgColor);
            spriteBatch.Draw(iconTexture, iconRect, fgColor);
            spriteBatch.Draw(knobTexture, knobTopLeft, MappedColor.SliderKnob.C());
			spriteBatch.End(); 

			spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            spriteBatch.DrawString(valueFont, text, valueTextLocation, fgColor);
            spriteBatch.End();
        }

        protected override void ComputeScales()
        {
            redrawRequest = true;
            base.ComputeScales();

            if (!contentLoaded)
                return;
            //|<margin/2><icon><margin/2><slider><text><margin>|
            iconRect = new Rectangle(Boundaries.X + Margin.X/2, Boundaries.Center.Y - iconTexture.Height / 2, iconTexture.Width, iconTexture.Height);

            int sliderWidth = (int)(Boundaries.Width - iconRect.Width - valueFont.MeasureString("99.9 MMM").X - 2.5 * Margin.X);
            sliderRect = new Rectangle((int)(iconRect.Right + Margin.X/2), Boundaries.Center.Y - sliderHeight / 2, sliderWidth, sliderHeight);

            knobTopLeft = new Vector2((float)Math.Round(sliderRect.X + sliderRect.Width * Position - knobTexture.Width / 2f), (float)Math.Round(sliderRect.Y - knobTexture.Height / 2f));

            valueTextSize = valueFont.MeasureString(text);
            valueTextLocation = new Vector2(Boundaries.Right - valueTextSize.X - Margin.X, Boundaries.Center.Y - valueTextSize.Y / 2);
        }

        private void DoMove(Vector2 gesturePosition, int deltaX)
        {
            redrawRequest = true;
            Rectangle screen = device.Viewport.Bounds;
            /*
             * +-------------------+
             * + --o--          ^  |
             * +  ^             |  | Take the largest Y space availabel to scale
             * +  |             v  | 
             * +  v         --o--  |
             * +-------------------+
             */
            int availableYSpace = screen.Height / 2;
            double relativeYDistance = Math.Abs(sliderRect.Center.Y - gesturePosition.Y) / availableYSpace;
            double scaler = Math.Pow(0.01, relativeYDistance);

            double newValue = 0;

            if (scaler > 0.9)
                Position = (gesturePosition.X - sliderRect.X) / (float)sliderRect.Width;
            else
            {
                Position = (float)(Position + ((double)deltaX / sliderRect.Width) * scaler);
            }
                   
            if (Position < 0)
                Position = 0;
            if (Position > 1)
                Position = 1;
            if (logScale)
            {
                double logMin = Math.Log10(Minimum);
                double logMax = Math.Log10(Maximum);
                double logRelative = Position * (logMax - logMin) + logMin;
                newValue = Math.Max(Minimum, Math.Min(Maximum, Math.Pow(10, logRelative)));
            }
            else
                newValue = Math.Max(Minimum, Math.Min(Maximum, (Maximum - Minimum) * Position + Minimum));

            newValue = LabNation.Common.Utils.precisionRound(newValue, Precision);
            newValue = LabNation.Common.Utils.significanceTruncate(newValue, significance);

            this.Value = newValue;
        }

        private bool LocationIsOnSlider(int locationX)
        {
            return locationX < sliderRect.Right && locationX > sliderRect.Left;
        }

        protected override void LoadContentInternal()
        {
            if (iconName == null) return; //nasty. this object's constr calls MenuItem's constr FIRST, which then calls this method.
            base.LoadContentInternal();
            valueFont = content.Load<SpriteFont>(Scaler.GetFontSliderValue());
            this.knobTexture = LoadPng(Scaler.ScaledImageName("widget-slider-knob"));
            this.iconTexture = LoadPng(Scaler.ScaledImageName("slider-icon-" + iconName));
        }

        protected override void OnBoundariesChangedInternal()
        {
            //Set Value to trigger recomputation of text scales
            this.Value = _value;
            base.OnBoundariesChangedInternal();
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.FreeDrag:
                    if (LocationIsOnSlider((int)gesture.Position.X) || dragging)
                        DoMove(gesture.Position, (int)gesture.Delta.X);
                    dragging = true;
                    break;
                case GestureType.Tap:
                    if (LocationIsOnSlider((int)gesture.Position.X))
                    {
                        dragging = true;
                        DoMove(gesture.Position, (int)gesture.Delta.X);
                        dragging = false;
                    }
                    ReleaseGestureControl();
                    break;
                case GestureType.DragComplete:
                    dragging = false;
                    ComputeScales();
                    ReleaseGestureControl();
                    break;
                case GestureType.Hold:
                case GestureType.DoubleTap:
                    if (DoubleTapCallback != null)
                        DoubleTapCallback.Call(this, gesture);
                    ReleaseGestureControl();
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
