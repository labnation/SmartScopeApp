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

namespace ESuite.Drawables
{
    internal class MeasurementBox : EDrawable
    {
        private delegate void UpdateTextLocationsDelegate();
        public BoundariesChangedDelegate OnMeasurementBoxPositionChanged;
        public struct MeasurementDrawInfo
        {
            public string channel;
            public string title;
            public string value;
            public string valueUnit;
            public string mean;
            public string meanUnit;
            public string min;
            public string minUnit;
            public string max;
            public string maxUnit;
            public string std;
            public string stdUnit;
            public MappedColor color;
        }

        private struct DrawBatchItem
        {
            public string text;
            public Vector2 location;
            public Color color;
            public SpriteFont font;
        }

        private List<DrawBatchItem> drawBatch = new List<DrawBatchItem>();
        private List<MeasurementDrawInfo> drawInfoList = new List<MeasurementDrawInfo>();
        private MeasurementBoxMode mode = MeasurementBoxMode.Floating;
        private PanoramaSplitter panoSplitter;
        private UpdateTextLocationsDelegate UpdateTextLocations;
        private List<Rectangle> multimeterPatchList = new List<Rectangle>();

        private SpriteFont headerFont;
        private SpriteFont valueFont;
        private SpriteFont multimeterFont;
        private int textSizeMeas = 0;
        private int textSizeValue = 0;
        private int textSizeErrorSymbol = 0;
        private int textSizeUnit = 0;
        private int textSizeMeasValueUnit = 0;
        private int textSizeWhiteSpace = 0;
        private int textHeight = 0;
        Vector2 offPushyNess = new Vector2();
        const double OFFPUSHYNESS_LIMIT = 100;
        const string ERROR_SYMBOL = "±";
        private Rectangle _boxBoundaries = new Rectangle();
        private bool shapeShiftDuringThisGesture = false;
        private Vector2 lastDraggedPosition = new Vector2(); //needed to know position when docked is dragged to floating
        private Rectangle originalBoundaryRequest = new Rectangle(); //this is needed; as the startup value is loaded while boundaries are (0,0,0,0) and the position of the box would be set to (0,0)

        private Rectangle boxBoundaries
        {
            get { return _boxBoundaries; }
            set
            {
                originalBoundaryRequest = value;

                //this stores the position to the settings file. So store unaltered values.
                if (OnMeasurementBoxPositionChanged != null)
                {
                    OnMeasurementBoxPositionChanged(originalBoundaryRequest);
                }

                //verify box is not outside of window
                _boxBoundaries = value;
                if (_boxBoundaries.Right > Boundaries.Right)
                    _boxBoundaries.X = Boundaries.Right - _boxBoundaries.Width;
                if (_boxBoundaries.Left < Boundaries.Left)
                    _boxBoundaries.X = Boundaries.Left;
                if (_boxBoundaries.Bottom > Boundaries.Bottom)
                    _boxBoundaries.Y = Boundaries.Bottom - _boxBoundaries.Height;
                if (_boxBoundaries.Top < Boundaries.Top)
                    _boxBoundaries.Y = Boundaries.Top;
            }
        }
        new internal bool Visible
        {
            get { return base.Visible; }
            set
            {
                offPushyNess = new Vector2();
                base.Visible = value;
            }
        }

        public void SetTopLeftLocation(Vector2 topLeftLoc)
        {
            Rectangle newBoxBoundaries = boxBoundaries;
            newBoxBoundaries.X = (int)topLeftLoc.X;
            newBoxBoundaries.Y = (int)topLeftLoc.Y;
            boxBoundaries = newBoxBoundaries;
        }

        private int preferredHeight;
        public int Height { get { return preferredHeight; } }
        private int preferredWidth;
        public int Width { get { return preferredWidth; } }

        private Matrix oldWorldMatrix = Matrix.Identity;
        public DrawableCallback DropCallback;

        public MeasurementBox(PanoramaSplitter panoSplitter)
            : base()
        {
            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };

            this.panoSplitter = panoSplitter;
            this.UpdateTextLocations = this.UpdateTextLocationsNormal;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            valueFont = content.Load<SpriteFont>(Scaler.GetFontMeasurementValue());
            headerFont = content.Load<SpriteFont>(Scaler.GetFontMeasurementValueBold());
            multimeterFont = content.Load<SpriteFont>(Scaler.GetFontMeasurementMultimeter());
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw background
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(whiteTexture, boxBoundaries, MappedColor.MeasurementBoxBackground.C());

            //draw multimeter patches
            foreach (Rectangle patch in multimeterPatchList)
                spriteBatch.Draw(whiteTexture, patch, MappedColor.MultimeterPatchBackground.C());

			spriteBatch.End(); spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            foreach (DrawBatchItem di in drawBatch)
                spriteBatch.DrawString(di.font, di.text, di.location, di.color);
            spriteBatch.End();
        }

        public void UpdateMeasurements(Dictionary<SystemMeasurementType, Measurement> systemMeasurements, Dictionary<AnalogChannel, Dictionary<Type, ChannelMeasurement>> channelMeasurements)
        {
            if (!contentLoaded) return;            
            if (systemMeasurements.Count == 0 && channelMeasurements.Count == 0) return;

            drawInfoList.Clear();

            //convert all active measurements to DrawInfos
            foreach (AnalogChannel ch in AnalogChannel.List)
                foreach (Measurement m in channelMeasurements[ch].Values)
                    drawInfoList.Add(ConvertMeasurementToDrawInfo(m));
            foreach (Measurement m in systemMeasurements.Values)
                drawInfoList.Add(ConvertMeasurementToDrawInfo(m));

            UpdateTextLocations();
        }

        private MeasurementDrawInfo ConvertMeasurementToDrawInfo(Measurement m)
        {
            string valueFormatted = "";
            string meanFormatted = "";
            string minFormatted = "";
            string maxFormatted = "";
            string stdFormatted = "";
            string valueUnitFormatted = "";
            string meanUnitFormatted = "";
            string minUnitFormatted = "";
            string maxUnitFormatted = "";
            string stdUnitFormatted = "";

            valueFormatted = MeasurementValueToString(m, m.CurrentValue);
            valueUnitFormatted = MeasurementUnitToString(m, m.CurrentValue);

            if (m is StochasticMeasurement)
            {
                StochasticMeasurement sm = m as StochasticMeasurement;
                meanFormatted = MeasurementValueToString(sm, sm.Mean);
                minFormatted = MeasurementValueToString(sm, sm.Min);
                maxFormatted = MeasurementValueToString(sm, sm.Max);
                stdFormatted = MeasurementValueToString(sm, sm.Std);

                //yes, each type can have a different unit prefix...                    
                meanUnitFormatted = MeasurementUnitToString(sm, sm.Mean);
                minUnitFormatted = MeasurementUnitToString(sm, sm.Min);
                maxUnitFormatted = MeasurementUnitToString(sm, sm.Max);
                stdUnitFormatted = MeasurementUnitToString(sm, sm.Std);
            }
            else
            {
                meanFormatted = MeasurementValueToString(m, m.CurrentValue);
            }

            string chanName = "";
            MappedColor color = MappedColor.Undefined;
            if (m is ChannelMeasurement)
            {
                ChannelMeasurement ch = (m as ChannelMeasurement);
                chanName = ch.Channel.Name;
                color = ch.Channel.ToManagedColor();
            }
            else
            {
                chanName = "SYS";
                color = MappedColor.System;
            }

            return new MeasurementBox.MeasurementDrawInfo() { channel = chanName, color = color, max = maxFormatted, mean = meanFormatted, min = minFormatted, std = stdFormatted, title = m.Name, value = valueFormatted, valueUnit = valueUnitFormatted, meanUnit = meanUnitFormatted, minUnit = minUnitFormatted, maxUnit = maxUnitFormatted, stdUnit = stdUnitFormatted };

        }

        private static string MeasurementUnitToString(Measurement m, double value)
        {
            string unitFormatted;
            if (double.IsNaN(value))
                unitFormatted = "";
            else {
                string rawUnit = m.Unit;
                if ((m is ChannelMeasurement) && (m as ChannelMeasurement).Channel is AnalogChannel && !(m as ChannelMeasurement).HasDedicatedUnit)
                    rawUnit = ((m as ChannelMeasurement).Channel as AnalogChannel).Probe.Unit;
                unitFormatted = m.DisplayMethod != DisplayMethod.Normal ? LabNation.Common.Utils.siPrefix(value, m.Precision, rawUnit) : rawUnit;
            }
            return unitFormatted;
        }

        private void UpdateTextLocationsMultimeter()
        {
            redrawRequest = true;
            drawBatch.Clear();
            multimeterPatchList.Clear();

            Color measColor = MappedColor.MeasurementFont.C();

            int margin = textSizeWhiteSpace * 2;
            int patchWidth = textSizeMeas * 2;
            int patchHeight = textSizeMeas;
            int firstRowOffset = (int)(patchHeight * 0.08f);
            int valueYOffset = (int)(patchHeight * 0.62f);
            int valueXOffset = (int)(patchWidth * 0.1f);
            int unitXOffset = (int)(patchWidth * 0.6f);
            int multimeterFontHalfHeight = (int)(multimeterFont.MeasureString("0j").Y / 2.0f);

            //all measurements            
            for (int i = 0; i < drawInfoList.Count; i++)
            {
                MeasurementDrawInfo di = drawInfoList[i];            
            
                Rectangle currentPatch = new Rectangle(Boundaries.X + margin * 2, Boundaries.Y + margin*2 + (margin * 2 + patchHeight) *i, patchWidth, patchHeight);
                multimeterPatchList.Add(currentPatch);

                Color c = di.color.C();
                drawBatch.Add(new DrawBatchItem() { font = headerFont, text = di.title, location = new Vector2((int)(currentPatch.Center.X - headerFont.MeasureString(di.title).X/2f), currentPatch.Y + firstRowOffset), color = c });
                string valueAndUnit = di.mean + " " + di.valueUnit;
                drawBatch.Add(new DrawBatchItem() { font = multimeterFont, text = valueAndUnit, location = new Vector2((int)(currentPatch.Center.X - multimeterFont.MeasureString(valueAndUnit).X/2.0f), currentPatch.Y + valueYOffset - multimeterFontHalfHeight), color = c });
            }            

            int newPreferredWidth = (int)(patchWidth + 4 * margin);
            if (preferredWidth != newPreferredWidth)
            {
                preferredWidth = newPreferredWidth;
                panoSplitter.OnMeasurementBoxWidthUpdated();
            }

            if (mode == MeasurementBoxMode.Floating)
            {
                //adjust the height of the box to correspond to the number of measurements displayed
                //should be done in the OnMatrixChangedInternal method
                _boxBoundaries.Width = newPreferredWidth;
                this.interactiveAreas[0] = boxBoundaries;
            }
        }

        private void UpdateTextLocationsNormal()
        {
            redrawRequest = true;
            drawBatch.Clear();
            multimeterPatchList.Clear();

            Color measColor = MappedColor.MeasurementFont.C();

            int margin = textSizeWhiteSpace *2;
            Vector2 lineSpacer = new Vector2(0, valueFont.LineSpacing);

            Vector2 offsetChannel = new Vector2((int)(boxBoundaries.X + margin), (int)(boxBoundaries.Y + margin));
            Vector2 offsetName = offsetChannel + new Vector2(textSizeUnit*2, 0);
            Vector2 offsetValue = offsetName + new Vector2(textSizeMeas, 0);
            Vector2 offsetValueUnit = offsetValue + new Vector2(textSizeValue, 0);
            Vector2 offsetMean = offsetValueUnit + new Vector2(textSizeUnit, 0);
            Vector2 offsetMeanUnit = offsetMean + new Vector2(textSizeValue, 0);
            Vector2 offsetMin = offsetMeanUnit + new Vector2(textSizeUnit, 0);
            Vector2 offsetMinUnit = offsetMin + new Vector2(textSizeValue, 0);
            Vector2 offsetMax = offsetMinUnit + new Vector2(textSizeUnit, 0);
            Vector2 offsetMaxUnit = offsetMax + new Vector2(textSizeValue, 0);
            Vector2 offsetStd = offsetMaxUnit + new Vector2(textSizeUnit, 0);
            Vector2 offsetStdUnit = offsetStd + new Vector2(textSizeValue, 0);

            //header
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Channel", location = offsetChannel, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Measure", location = offsetName, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Value", location = offsetValue, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Mean", location = offsetMean, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Min", location = offsetMin, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Max", location = offsetMax, color = measColor });
            drawBatch.Add(new DrawBatchItem() { font = headerFont, text = "Std", location = offsetStd, color = measColor });

            //all measurements            
            Vector2 lineCorrection = new Vector2(lineSpacer.X, (int)(lineSpacer.Y + textSizeWhiteSpace));
            foreach (MeasurementDrawInfo di in drawInfoList)
            {
                Color c = di.color.C();
                drawBatch.Add(new DrawBatchItem() { font = headerFont, text = di.channel, location = offsetChannel + lineCorrection, color = c });
                drawBatch.Add(new DrawBatchItem() { font = headerFont, text = di.title, location = offsetName + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.value, location = offsetValue + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.valueUnit, location = offsetValueUnit + lineCorrection, color = c });
                drawBatch.Add(new DrawBatchItem() { font = headerFont, text = di.mean, location = offsetMean + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = headerFont, text = di.meanUnit, location = offsetMeanUnit + lineCorrection, color = c });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.min, location = offsetMin + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.minUnit, location = offsetMinUnit + lineCorrection, color = c });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.max, location = offsetMax + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.maxUnit, location = offsetMaxUnit + lineCorrection, color = c });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.std, location = offsetStd + lineCorrection, color = measColor });
                drawBatch.Add(new DrawBatchItem() { font = valueFont, text = di.stdUnit, location = offsetStdUnit + lineCorrection, color = c });

                lineCorrection += lineSpacer;
            }

            int newPreferredHeight = (int)(lineCorrection.Y - lineSpacer.Y + textHeight + 2 * margin);
            if (preferredHeight != newPreferredHeight)
            {
                preferredHeight = newPreferredHeight;
                panoSplitter.OnMeasurementBoxHeightUpdated();
            }

            int newPreferredWidth = (int)(offsetStdUnit.X + textSizeUnit + 2 * margin - boxBoundaries.X);
            if (preferredWidth != newPreferredWidth)
            {
                preferredWidth = newPreferredWidth;
                panoSplitter.OnMeasurementBoxWidthUpdated();
            }

            if (mode == MeasurementBoxMode.Floating)
            {
                //adjust the height of the box to correspond to the number of measurements displayed
                //should be done in the OnMatrixChangedInternal method
                _boxBoundaries.Height = newPreferredHeight;
                _boxBoundaries.Width = (int)(offsetStdUnit.X + textSizeUnit + 2 * margin - boxBoundaries.X);
                this.interactiveAreas[0] = boxBoundaries;
            }            
        }

        private static string MeasurementValueToString(Measurement m, double valToFormat)
        {
            string nanString = "--.-";
            return double.IsNaN(valToFormat) ? nanString : m.DisplayMethod != DisplayMethod.Normal ?
                                                            LabNation.Common.Utils.siScale(valToFormat, m.Precision, m.Significance, (int)m.DisplayMethod) :
                                                            LabNation.Common.Utils.precisionFormat(valToFormat, m.Precision, m.Significance);
        }

        protected override void OnBoundariesChangedInternal()
        {
            //the following lines define the spacing of all elements in the box!
            textSizeMeas = (int)valueFont.MeasureString("Data refresh   ").X;
            textSizeValue = (int)valueFont.MeasureString("0.000").X;
            textSizeErrorSymbol = (int)valueFont.MeasureString(ERROR_SYMBOL).X;
            textSizeUnit = (int)valueFont.MeasureString("KHz ").X;
            textSizeMeasValueUnit = textSizeMeas + 2*(textSizeValue + textSizeUnit) + textSizeErrorSymbol;
            textSizeWhiteSpace = (int)valueFont.MeasureString(" ").X;
            textHeight = (int)valueFont.MeasureString(" ").Y;

            UpdateTextLocations(); //this will also update the boundarybox

            if (mode == MeasurementBoxMode.Floating)
                boxBoundaries = originalBoundaryRequest; //making sure box is not outside of panoramaSplitter area
            else
                boxBoundaries = Boundaries;

            this.interactiveAreas[0] = boxBoundaries;
        }
        public void SetMode(MeasurementBoxMode mode)
        {
            if ((this.mode != MeasurementBoxMode.Floating) && (mode == MeasurementBoxMode.Floating))
                boxBoundaries = new Rectangle((int)lastDraggedPosition.X, (int)lastDraggedPosition.Y, 0, 0); //basically simply sets the topleft point; width and height are redefined at each call of UpdateTextLocations

            this.mode = mode;
            OnBoundariesChangedInternal();

            if (mode == MeasurementBoxMode.DockedRight)
                this.UpdateTextLocations = this.UpdateTextLocationsMultimeter;
            else
                this.UpdateTextLocations = this.UpdateTextLocationsNormal;
        }
        private void OnDrag(GestureSample gesture)
        {
            lastDraggedPosition = gesture.Position;

            if (mode == MeasurementBoxMode.Floating)
            {
                Rectangle newBoxBoundaries = boxBoundaries;
                newBoxBoundaries.X += (int)gesture.Delta.X;
                newBoxBoundaries.Y += (int)gesture.Delta.Y;
                boxBoundaries = newBoxBoundaries; //this is a setter; which makes sure box is not outside of boundaries
            }

            //in case the box was already docked/undocked during this same gesture -> don't change anymore
            if (shapeShiftDuringThisGesture)
                return;

            Rectangle graphAreaBoundaries = parent.Boundaries;
            //transform from screen -> local/relative coordinates
            float maxY = Boundaries.Y;
            float minY = Boundaries.Bottom;
            float minX = Boundaries.X;
            float maxX = Boundaries.Right;

            Vector2 location = new Vector2((gesture.Position.X - minX) / (maxX - minX) - 0.5f, (gesture.Position.Y - minY) / (maxY - minY) - 0.5f);

            //as long as box is moved out of boundaries: accumulate Deltas
            if (
                boxBoundaries.Right == Boundaries.Right ||
                boxBoundaries.Left == Boundaries.Left ||
                boxBoundaries.Top == Boundaries.Top ||
                boxBoundaries.Bottom == Boundaries.Bottom
            )
            {
                offPushyNess.X += Math.Abs(gesture.Delta.X);
                offPushyNess.Y += Math.Abs(gesture.Delta.Y);
            }
            else { offPushyNess = new Vector2(); }

            double pushynessLimit = double.MaxValue;
            if (mode == MeasurementBoxMode.Floating)
            {
                if (boxBoundaries.Top == Boundaries.Top)
                {
                    pushynessLimit = Math.Abs(Boundaries.Top - gesture.Position.Y) / 2;
                    if (offPushyNess.Y > pushynessLimit)
                    {
                        shapeShiftDuringThisGesture = true;
                        UICallbacks.SetMeasurementBoxVisibility(this, new object[] { this, false }); //hides mbox
                    }
                }

                if (boxBoundaries.Left == Boundaries.Left)
                {
                    pushynessLimit = Math.Abs(Boundaries.Left - gesture.Position.X) / 2;
                    if (offPushyNess.X > pushynessLimit)
                    {
                        shapeShiftDuringThisGesture = true;
                        UICallbacks.SetMeasurementBoxVisibility(this, new object[] { this, false }); //hides mbox
                    }
                }

                if (boxBoundaries.Bottom == Boundaries.Bottom)
                {
                    pushynessLimit = Math.Abs(Boundaries.Bottom - gesture.Position.Y) / 2;
                    if (offPushyNess.Y > pushynessLimit)
                    {
                        shapeShiftDuringThisGesture = true; //needed, as otherwise docked box will undock itself immediately
                        UICallbacks.SetMeasurementBoxMode(this, MeasurementBoxMode.DockedBottom);
                    }
                }

                if (boxBoundaries.Right == Boundaries.Right)
                {
                    pushynessLimit = Math.Abs(Boundaries.Right - gesture.Position.X) / 2;
                    if (offPushyNess.X > pushynessLimit)
                    {
                        shapeShiftDuringThisGesture = true; //needed, as otherwise docked box will undock itself immediately
                        UICallbacks.SetMeasurementBoxMode(this, MeasurementBoxMode.DockedRight);
                    }
                }
            }
            else if (mode == MeasurementBoxMode.DockedBottom)
            {
                if (location.Y > 0.5f)
                {
                    shapeShiftDuringThisGesture = true;
                    UICallbacks.SetMeasurementBoxMode(this, MeasurementBoxMode.Floating);
                }
            }
            else if (mode == MeasurementBoxMode.DockedRight)
            {
                if (location.X < -0.5f)
                {
                    shapeShiftDuringThisGesture = true;
                    UICallbacks.SetMeasurementBoxMode(this, MeasurementBoxMode.Floating);
                }
            }
        }

        protected override void ReleaseGestureControl()
        {
            shapeShiftDuringThisGesture = false;

            base.ReleaseGestureControl();
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.FreeDrag:
                    OnDrag(gesture);
                    break;
                case GestureType.DragComplete:
                    if (DropCallback != null)
                        DropCallback.Call(this);
                    ReleaseGestureControl();
                    break;
                default:
                    offPushyNess = new Vector2();
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
