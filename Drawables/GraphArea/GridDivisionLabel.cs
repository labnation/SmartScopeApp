using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.Devices;

namespace ESuite.Drawables
{
    struct LabelTextFormat
    {
        public Vector2 location;
        public Vector2 origin;
        public float scale;
        public Color color;
        public string text;
        public LabelTextFormat(string text)
        {
            this.text = text;
            this.location = new Vector2();
            this.origin = new Vector2();
            this.scale = 1;
            this.color = Color.White;
        }
    }

    internal class GridDivisionLabel : EDrawable
    {
        private SpriteFont font;
        private SpriteFont fontBold;
        private Texture2D backgroundTexture;
        private MappedColor textColor;
        private string unit;
        private double precision;
        private Rectangle drawRectangle = new Rectangle();
        private DrawableCallback scrollUpCallback;
        private DrawableCallback scrollDownCallback;
        private DrawableCallback slideCallback;
        private VertexPositionColor[] vertices;
        private VertexPositionColor[] wheelVertices;
        private Matrix drawRectangleWorld = Matrix.Identity;
        private Matrix wheelWorld = Matrix.Identity;
        private Rectangle wheelRectangle = new Rectangle();
        private bool wheelShown = false;
        private Dictionary<double, LabelTextFormat> wheelItems = new Dictionary<double, LabelTextFormat>();
        private LabelTextFormat defaultTextFormat = new LabelTextFormat();
        public Channel channel { get; private set; }
        private double currentValue;
        private int wheelStartIndex = 0;

        //from the outside, this constructor accepts either a fixed color, or a MappedColor
        public GridDivisionLabel(MappedColor textColor, float minVal, float maxVal, string unit, double precision, Channel channel)
            : base()
        {
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };            
            this.textColor = textColor;
            this.unit = unit;
            this.precision = precision;
            this.supportedGestures = GestureType.FreeDrag | GestureType.DragComplete | GestureType.MouseScroll | GestureType.DoubleTap | GestureType.Tap;
            this.channel = channel;

            RepopulateItems(minVal, maxVal);
            DefineVertices();

            LoadContent();
        }

        public void RepopulateItems(float minVal, float maxVal)
        {
            if (channel is AnalogChannel)
                unit = (channel as AnalogChannel).Probe.Unit;

            wheelItems.Clear();
            double curVal = minVal;

            curVal = LabNation.Common.Utils.precisionRound(curVal, precision);
            wheelItems.Add(curVal, new LabelTextFormat(LabNation.Common.Utils.siPrint(curVal, precision, ColorMapper.NumberDisplaySignificance, unit)));
            while (curVal < maxVal)
            {
                curVal = (float)Utils.getRoundDivisionRange(curVal * 1.9999f, Utils.RoundDirection.Up);
                curVal = LabNation.Common.Utils.precisionRound(curVal, precision);
                wheelItems.Add(curVal, new LabelTextFormat(LabNation.Common.Utils.siPrint(curVal, precision, ColorMapper.NumberDisplaySignificance, unit)));
            }
        }

        public void BindSlideUpCallback(DrawableCallbackDelegate del)
        { BindScrollUpCallback(new DrawableCallback(del, null)); }
        public void BindScrollUpCallback(DrawableCallback callback)
        {
            this.scrollUpCallback = callback;
        }

        public void BindSlideDownCallback(DrawableCallbackDelegate del)
        { BindScrollDownCallback(new DrawableCallback(del, null)); }
        public void BindScrollDownCallback(DrawableCallback callback)
        {
            this.scrollDownCallback = callback;
        }

        public void BindSlideCallback(DrawableCallbackDelegate del)
        { BindSlideCallback(new DrawableCallback(del, null)); }
        public void BindSlideCallback(DrawableCallback callback)
        {
            this.slideCallback = callback;
        }

        protected override void LoadContentInternal()
        {
            this.font = content.Load<SpriteFont>(Scaler.GetFontMeasurementValue());
            this.fontBold = content.Load<SpriteFont>(Scaler.GetFontMeasurementValueBold());
            this.backgroundTexture = whiteTexture;
        }

        protected override void DrawInternal(GameTime time)
        {
            if (vertices == null) return;
            if (wheelItems.Count == 0) return;
            if (defaultTextFormat.text == null) return;

            //set all renderstates for alpha blending
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.End();


            //draw from vertices
            if (!wheelShown)
                effect.World = drawRectangleWorld;
            else
                effect.World = wheelWorld;
            effect.View = this.View;
            effect.Projection = this.Projection;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                if (!wheelShown)
                    device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleStrip, vertices, 0, vertices.Length - 2);
                else
                    device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleStrip, wheelVertices, 0, wheelVertices.Length - 2);
            }

            
            //simply draw the image to screen
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            if (!wheelShown)
                spriteBatch.Draw(backgroundTexture, drawRectangle, MappedColor.GridDivisionLabelBackground.C());            
			spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            if (!wheelShown)
            {
                spriteBatch.DrawString(font, defaultTextFormat.text, defaultTextFormat.location, defaultTextFormat.color, 0, defaultTextFormat.origin, new Vector2(1, defaultTextFormat.scale), SpriteEffects.None, 0);
            }
            else
                foreach (var kvp in wheelItems)
                    if (kvp.Value.scale > 0)
                        if (kvp.Key == currentValue)
                            spriteBatch.DrawString(fontBold, kvp.Value.text, kvp.Value.location, kvp.Value.color, 0, kvp.Value.origin, new Vector2(1, kvp.Value.scale), SpriteEffects.None, 0);
                        else
                            spriteBatch.DrawString(font, kvp.Value.text, kvp.Value.location, kvp.Value.color, 0, kvp.Value.origin, new Vector2(1, kvp.Value.scale), SpriteEffects.None, 0);
            spriteBatch.End();            
		}

        //private double value;
        public double Value
        {
            set
            {
                if (!wheelShown) //needed, as otherwise app sometimes overrides newly selected setting
                {
                    this.currentValue = value;
					this.currentValue = (float)Utils.getRoundDivisionRange(this.currentValue * 0.9999f, Utils.RoundDirection.Up);
					this.currentValue = LabNation.Common.Utils.precisionRound(this.currentValue, precision);
                    defaultTextFormat.text = LabNation.Common.Utils.siPrint(value, precision, ColorMapper.NumberDisplaySignificance, unit);
                    ComputeDefaultTextPosition();
                }
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            int margin = 5;

            //recalc starting from bottom-right position and textSize
            Vector2 textSize = fontBold.MeasureString("999KV");
            this.Boundaries = new Rectangle(Boundaries.Right - (int)textSize.X - 2 * margin, Boundaries.Bottom - (int)textSize.Y - 2 * margin, (int)textSize.X + 2 * margin, (int)textSize.Y + 2 * margin);
            this.interactiveAreas = new List<Rectangle>() { new Rectangle(Boundaries.X, Boundaries.Y - Boundaries.Height/2, Boundaries.Width, Boundaries.Height*2) };            

            drawRectangle = Boundaries; 

            float wheelSize = 4;
            wheelRectangle = new Rectangle(drawRectangle.X, (int)(drawRectangle.Center.Y - wheelSize / 2f * (float)drawRectangle.Height), drawRectangle.Width, (int)(wheelSize * (float)drawRectangle.Height));

            ComputeDefaultTextPosition();
            ComputeWheelTextPositions();

            drawRectangleWorld = Utils.RectangleToMatrix(drawRectangle, device, Matrix.Identity, View, Projection);
            wheelWorld = Utils.RectangleToMatrix(wheelRectangle, device, Matrix.Identity, View, Projection);
        }

        private void DefineVertices()
        {
            

            //   0
            //    \
            // 2---1
            //  \
            //    \
            // 4---3
            //  \
            //   5
            Color c = MappedColor.GridDivisionLabelTabs.C()*0.1f;
            List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
            vertexList.Add(new VertexPositionColor(new Vector3(0.5f, -0.5f, 0), c));      //0
            vertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), c));             //1
            vertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), c));             //2
            vertexList.Add(new VertexPositionColor(new Vector3(1, 1, 0), c));             //3
            vertexList.Add(new VertexPositionColor(new Vector3(0, 1, 0), c));             //4
            vertexList.Add(new VertexPositionColor(new Vector3(0.5f, 1.5f, 0), c));       //5

            vertices = vertexList.ToArray();

            Color cBorder = MappedColor.GridDivisionWheelCenter.C()*00f; //sets transparancy in case of premult alpha
            Color cCenter = MappedColor.GridDivisionWheelCenter.C();
            
            List<VertexPositionColor> wheelVertexList = new List<VertexPositionColor>();
            wheelVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), cBorder));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), cBorder));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(0, 0.1f, 0), Color.Lerp(cBorder, cCenter, 0.5f)));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(1, 0.1f, 0), Color.Lerp(cBorder, cCenter, 0.5f)));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(0, 0.5f, 0), cCenter));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(1, 0.5f, 0), cCenter));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(0, 0.9f, 0), Color.Lerp(cBorder, cCenter, 0.5f)));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(1, 0.9f, 0), Color.Lerp(cBorder, cCenter, 0.5f)));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(0, 1f, 0), cBorder));
            wheelVertexList.Add(new VertexPositionColor(new Vector3(1, 1f, 0), cBorder));
            wheelVertices = wheelVertexList.ToArray();            
        }

        protected void ComputeDefaultTextPosition()
        {
            if (defaultTextFormat.text == null) return;

            Vector2 curTextSize = font.MeasureString(defaultTextFormat.text);
            defaultTextFormat.color = textColor.C();
            defaultTextFormat.location = new Vector2((int)(drawRectangle.Center.X), (int)(drawRectangle.Center.Y));
            defaultTextFormat.origin = new Vector2((int)(curTextSize.X / 2f), (int)(curTextSize.Y / 2f));
            defaultTextFormat.scale = 1;
        }

        protected int ComputeWheelTextPositions()
        {
            float wheelHeight = wheelRectangle.Height;
            if (wheelHeight == 0) wheelHeight = 1;
            int selectedItem = -1;

            //prevent wheel from rotating too far
            float textHeight = font.MeasureString("999KV").Y;
            float addHigh = (float)(wheelItems.Count - wheelStartIndex - 1) * textHeight;            
            if (drawRectangle.Y > Boundaries.Y + addHigh)
                drawRectangle.Y = (int)(Boundaries.Y+addHigh);
            float subtLow = (float)(wheelStartIndex) * textHeight;
            if (drawRectangle.Y < (Boundaries.Y - subtLow))
                drawRectangle.Y = (int)(Boundaries.Y - subtLow);

            for (int i = 0; i < wheelItems.Count; i++)
            {
                LabelTextFormat textFormat = wheelItems.ElementAt(i).Value;
                string text = textFormat.text;
                

                Vector2 textSize = font.MeasureString(text);
                float yOffset = (wheelStartIndex - i) * textSize.Y; //in pixels
                textFormat.location = new Vector2((int)(drawRectangle.Center.X), (int)(drawRectangle.Center.Y + yOffset));
                textFormat.origin = new Vector2((int)(textSize.X / 2f), (int)(textSize.Y / 2f));

                float textAngle = (float)(Boundaries.Center.Y - textFormat.location.Y) / (wheelHeight / 2f); //between 1 and -1

                textFormat.scale = (float)(1 - Math.Abs(textAngle));
                textFormat.color = textColor.C();

                wheelItems[wheelItems.Keys.ElementAt(i)] = textFormat;
                if (Math.Abs(textFormat.location.Y - wheelRectangle.Center.Y) < textSize.Y * 0.2f)
                    selectedItem = i;
            }

            redrawRequest = true;

            return selectedItem;
        }

        private void ResetPosition()
        {
            drawRectangle = Boundaries;
        }

        private void OnDrag(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            if (!wheelShown)
                wheelStartIndex = wheelItems.Keys.ToList().IndexOf((double)currentValue);

			wheelShown = true;

            float location = 0f;

            float maxY = Boundaries.Top;
            float minY = Boundaries.Bottom;
            location = (gesture.Position.Y - minY) / (maxY - minY) - 0.5f;

            //makes wheel rotate. quite strange, because drawRectangle is always reset to Boundaries. so it seems its Y coord is being used to indicate the currently selected item... not so clean!
            drawRectangle.Y = Boundaries.Y - (int)(location * (float)Boundaries.Height);

            //calc wheel text positions, detect whether new item was selected and fire callback
            int selectedItem = ComputeWheelTextPositions();
            if (selectedItem > -1)
            {
                var selectedValue = wheelItems.ElementAt(selectedItem);
                if (selectedValue.Key != currentValue)
                {
                    slideCallback.Call(this, new object[] { channel, (float)selectedValue.Key });

                    this.currentValue = selectedValue.Key;
                    defaultTextFormat.text = LabNation.Common.Utils.siPrint(selectedValue.Key, precision, ColorMapper.NumberDisplaySignificance, unit);
                    ComputeDefaultTextPosition();
                }
            }   
        }

        private void OnDrop(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            ResetPosition();
            wheelShown = false;
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.Tap:
                case GestureType.DoubleTap:
                    ReleaseGestureControl();
                    break;
                case GestureType.FreeDrag:
                    OnDrag(gesture);
                    break;
                case GestureType.DragComplete:
                    ReleaseGestureControl();
                    OnDrop(gesture);
                    break;
                case GestureType.MouseScroll:
                    if (gesture.Delta.Y == -1 && scrollDownCallback != null) //-1 means down
                        scrollUpCallback.Call(); //mouseDown == zoom out == slideUp
                    else if (gesture.Delta.Y == 1 && scrollUpCallback != null) //1 means up
                        scrollDownCallback.Call(); //mouseUp = zoom in == slideDown
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
