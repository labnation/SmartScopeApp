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
using ESuite.DataProcessors;
using LabNation.Common;

namespace ESuite.Drawables
{
    internal enum GridBehaviour { SnapActiveWave }
    internal enum PinchingBehaviour { RestrictedHorVerWithoutPanning, RestrictedHorVerWithPanning }

    internal abstract class Grid : EDrawableVertices
    {        
        private DateTime lastGestureTimestamp = DateTime.Now;
        private Channel activeChannel = null;
        public GraphType GraphType { get; private set; }
        
        private float leftLimitFreq = 0;
        public float LeftLimitFreq
        {
            get 
            {
                if (TimeScale == LinLog.Linear)
                {
                    return leftLimitFreq;
                }
                else
                {
                    float minimumFreq = ((float)this.freqNyquist / (float)this.numberOfBins);
                    if (leftLimitFreq > minimumFreq)
                        return leftLimitFreq;
                    else
                        return minimumFreq;
                }
            }
            set
            {
                bool valChanged = false;
                if (leftLimitFreq != value)
                    valChanged = true;
                leftLimitFreq = value;

                if (leftLimitFreq < 0)
                    leftLimitFreq = 0;
                if (leftLimitFreq > rightLimitFreq)
                    leftLimitFreq = rightLimitFreq;

                if (valChanged)
                    DefineSceneContents();
            }
        }

        private float rightLimitFreq = 0;
        public float RightLimitFreq 
        {
            get { return this.rightLimitFreq; }
            set
            {
                bool valChanged = false;
                if (rightLimitFreq != value)
                    valChanged = true;
                rightLimitFreq = value;

                if (rightLimitFreq < LeftLimitFreq)
                    rightLimitFreq = LeftLimitFreq;
                if (rightLimitFreq > NyquistFrequency)
                    rightLimitFreq = NyquistFrequency;

                if (valChanged)
                    DefineSceneContents();
            }
        }

        private float numberOfBins = 0;
        public float NumberOfBins
        {
            get { return numberOfBins; }
            set 
            {
                if (value != numberOfBins)
                {
                    numberOfBins = value;
                    DefineSceneContents();
                }
                else
                {
                    numberOfBins = value;
                }
            } 
        }

        private float freqNyquist = 0;
        public float NyquistFrequency
        {
            get { return freqNyquist; }
            set
            {
                if (value != freqNyquist)
                {
                    freqNyquist = value;

                    //this code causes the FFT axis to reset whenever the analog grid is rescaled
                    leftLimitFreq = 0;
                    rightLimitFreq = value;
                    DefineSceneContents();
                }
                else
                {
                    freqNyquist = value;
                }
            }
        }

        internal override bool Visible
        {
            get
            {
                return base.Visible;
            }

            set
            {
                base.Visible = value;
                if (!value)
                    gm.LabelPrinter.RemoveGridLabelDefinitions(this);
                else
                    DefineSceneContents();
            }
        }

        public DrawableCallback PinchDragCallback, PinchDragEndCallback, TapCallback, DoubleTapCallback, DragEndCallback;

        VertexPositionColor[] gridVertices;
        private float spokeLengthVertical;
        private float spokeLengthHorizontal;
        protected bool ShowVerticalGridLinesMajor = true;
        protected bool ShowVerticalGridLinesMinor = true;
        protected bool ShowHorizontalGridLines = true;
        protected bool SupportsLogarithmicScale = false;
        protected bool ShowCross = true;
        protected bool ShowCenterSpoke = true;
        protected bool ShowHorizontalGridLineLabelsLeft = false;
        protected bool ShowHorizontalGridLineLabelsRight = false;
        protected bool ShowVerticalGridLineLabelsBottom = false;
        public LinLog TimeScale;
        public LinLog VoltageScale;
        public float HorizontalTickSpacingMajor;
        public float VerticalTickSpacingMajor;
        public float HorizontalOffsetModuloMajorTickSpacing;
        float HorizontalOffset;
        private double triggerHoldoff;
        private double viewFinderCenter;
        private SpriteFont labelFont;

        int subDivisions = 5;
        public float HorizontalTickSpacingMinor { get { return HorizontalTickSpacingMajor / subDivisions; } }
        public float VerticalTickSpacingMinor { get { return VerticalTickSpacingMajor / subDivisions; } }

        public GridBehaviour GridBehaviour = GridBehaviour.SnapActiveWave;
        public PinchingBehaviour PinchingBehaviour = PinchingBehaviour.RestrictedHorVerWithPanning;

        public static readonly double DivisionsHorizontalMax = 16;
        public int GraphsSharingHorizontalSpace = 1;
        public static readonly double DivisionsVerticalMax = 8; //if you change this, make sure you also change VOLTAGE_RANGE_DEFAULT in UIHandler for best startup effects.

        public GridDivision DivisionHorizontal {get; protected set;}
        public GridDivision DivisionVertical {get; private set;}

        //Gesture handling
        private List<Rectangle> dragRectangleList = new List<Rectangle>();
        private Vector2 rectangleStartingPoint;
        private bool panZooming = false;
        private bool pinching = false;
        internal bool Pinching { get { return pinching; } }
        private bool pinchedThroughMirror = false;
        private Vector2? pinchPreviousPosition1 = null;
        private Vector2? pinchPreviousPosition2 = null;
        private GraphManager gm;
        private string gridLabelUnit = "";
        public MappedColor GridLabelColor = MappedColor.GridLabel;
        private List<Cursor> cursors = new List<Cursor>();

        public Grid(GraphType graphType, GraphManager gm, string gridLabelUnit)
            : base()
        {
            this.GraphType = graphType;
            this.gridLabelUnit = gridLabelUnit;
            this.gm = gm;
            this.supportedGestures = GestureType.Pinch | GestureType.PinchComplete | GestureType.FreeDrag | GestureType.DragComplete | GestureType.Tap | GestureType.Hold | GestureType.DoubleTap | GestureType.MouseScroll;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
            DivisionHorizontal = new GridDivision(DivisionsHorizontalMax, 1, 0);
            DivisionVertical = new GridDivision(DivisionsVerticalMax, 1, 0);

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            labelFont = content.Load<SpriteFont>(Scaler.GetFontInternalArrows());
        }
        public void UpdateScalersOffsets(double horizontalRange, double horizontalOffset, double verticalRange, LinLog timeScale, double voltageOffset, Channel activeChannel)
        {
            this.TimeScale = timeScale;
            this.activeChannel = activeChannel;

            if (activeChannel is AnalogChannel)
                this.gridLabelUnit = (activeChannel as AnalogChannel).Probe.Unit;

            verticalRange = LabNation.Common.Utils.significanceTruncate(verticalRange, 3);
            DivisionVertical = Utils.divisionRangeFinder(verticalRange, DivisionsVerticalMax);
            DivisionVertical.Offset = voltageOffset;

            if (timeScale == LinLog.Logarithmic)
            {
                DivisionHorizontal = null;
            }
            else
            {
                DivisionHorizontal = Utils.divisionRangeFinder(horizontalRange, DivisionsHorizontalMax/GraphsSharingHorizontalSpace);
                DivisionHorizontal.Offset = horizontalOffset;
            }

            DefineSceneContents();
        }        

        #region drawing helpers

        
        public void DefineSceneContents()
        {
            if (gm.LabelPrinter == null) return;
            if (!Visible) return;

            Color leftLabelColor = GridLabelColor.C();
            if (activeChannel != null)
                leftLabelColor = ColorMapper.EnhanceContrast(activeChannel.ToManagedColor().C());

            redrawRequest = true;
            gm.LabelPrinter.RemoveGridLabelDefinitions(this);
            List<VertexPositionColor> gridVertexList = new List<VertexPositionColor>();
            
            HorizontalTickSpacingMajor = (float)(1f / DivisionHorizontal.Divisions);
            VerticalTickSpacingMajor = (float)(1f /  DivisionVertical.Divisions);
            HorizontalOffset = (float)(DivisionHorizontal.Offset / DivisionHorizontal.FullRange);

            /* 
             * Wrap the HorizontalOffset into the +/- HorizontalTickSpacingMajor range
             * using the modulo operator. Otherwise, vertical lines will not be all drawn 
             * since we only draw (in relative coordinates) from -1f to 1f
             */
            HorizontalOffsetModuloMajorTickSpacing = HorizontalOffset % HorizontalTickSpacingMajor;

            //add horizontal lines
            if (ShowHorizontalGridLines)
            {
                if (VoltageScale == LinLog.Linear)
                {
                    int line = 0;
                    for (float pos = 0; pos <= 0.5f; pos += VerticalTickSpacingMinor)
                    {
                        if (line == 0)
                        {
                            DrawHorizontalGridLine(gridVertexList, 0.5f + pos + VerticalTickSpacingMajor, MappedColor.GridMajor.C(), HorizontalOffset, ConvertYPosToVerticalLabel(1-(0.5f + pos + VerticalTickSpacingMajor)), leftLabelColor);
                            DrawHorizontalGridLine(gridVertexList, 0.5f - pos, MappedColor.GridMajor.C(), HorizontalOffset, ConvertYPosToVerticalLabel(1-(0.5f - pos)), leftLabelColor);

                            line = subDivisions - 1;
                        }
                        else
                        {
                            DrawHorizontalGridLine(gridVertexList, 0.5f + pos, MappedColor.GridMinor.C(), HorizontalOffset, "", leftLabelColor);
                            DrawHorizontalGridLine(gridVertexList, 0.5f - pos, MappedColor.GridMinor.C(), HorizontalOffset, "", leftLabelColor);
                            line--;
                        }
                    }
                }
                else
                {
                    float maxDB = (float)DataProcessorFFT.MaxDb;
                    for (int i = 0; i < maxDB; i++)
                    {
                        if (i % 10 == 0)
                        {
                            float yPos = (float)i / maxDB;
                            string labelText = "-" + i.ToString() + "dB";
                            DrawHorizontalGridLine(gridVertexList, yPos, MappedColor.GridMajor.C(), HorizontalOffset, labelText, GridLabelColor.C());
                        }
                        else
                            DrawHorizontalGridLine(gridVertexList, (float)i / maxDB, MappedColor.GridMinor.C(), HorizontalOffset, "", leftLabelColor);
                    }
                }
            }

            //add vertical lines
            //if (offset < 0)
            //    offset += 1;
            if (ShowVerticalGridLinesMajor)
            {
                if (SupportsLogarithmicScale)
                {
                    if (TimeScale == LinLog.Linear)
                    {
                        if (freqNyquist == 0) return;

                        //find starting details
                        float endFreqCopy = freqNyquist;

                        //first find decade = order of magnitude of largest freq
                        int decade = 0;
                        while (endFreqCopy > 9)
                        {
                            endFreqCopy /= 10f;
                            decade++;
                        }
                        float hzPerMinorDiv = (float)Math.Pow(10, decade - 1); //1 decade lower, to have at least 10 divisions                    
                        while ((RightLimitFreq - LeftLimitFreq) / hzPerMinorDiv < 20)
                        {
                            hzPerMinorDiv /= 10;
                        }

                        //draw vertical gridlines
                        int i = (int)Math.Ceiling(LeftLimitFreq / hzPerMinorDiv);
                        while (i * hzPerMinorDiv <= RightLimitFreq)
                        {
                            float xPos = ((float)i * hzPerMinorDiv - LeftLimitFreq) / (RightLimitFreq - LeftLimitFreq);
                            if (i % 10 == 0)
                            {
                                DrawVerticalGridLine(gridVertexList, xPos, MappedColor.GridMajor.C());

                                float freq = ((float)i * hzPerMinorDiv);
                                string labelText = LabNation.Common.Utils.siPrint(freq, 1, 2, "Hz");
                                AddGridLabelVerBottom(labelText, xPos, GridLabelColor.C());
                            }
                            else
                                DrawVerticalGridLine(gridVertexList, xPos, MappedColor.GridMinor.C());

                            i++;
                        }
                    }
                    else
                    {
                        if (freqNyquist == 0) return;
                        if (numberOfBins == 0) return;

                        //this.FreqZoom = 2f;

                        int startFreq = (int)LeftLimitFreq;

                        //find starting details. decade = order of magnitude of starting freq
                        int startFreqCopy = startFreq;
                        int decade = 0;
                        while (startFreqCopy > 9)
                        {
                            startFreqCopy /= 10;
                            decade++;
                        }

                        //decade = current order of magnitude
                        //line = current sub-line inside the current magnitude
                        int line = startFreqCopy;

                        double scaler = Math.Log10(this.RightLimitFreq) - Math.Log10(startFreq);
                        while (line * Math.Pow(10, decade) <= RightLimitFreq)
                        {
                            double logFreq = Math.Log10(line * Math.Pow(10, decade));
                            double logFreqOffset = logFreq - Math.Log10(startFreq);
                            double xPos = logFreqOffset / scaler;
                            if (xPos >= 0 && xPos <= 1)
                            {
                                if (line == 1)
                                {   
                                    DrawVerticalGridLine(gridVertexList, (float)xPos, MappedColor.GridMajor.C());

                                    double freq = line * Math.Pow(10, decade);
                                    string labelText = LabNation.Common.Utils.siPrint(freq, 1, 1, "Hz");
                                    AddGridLabelVerBottom(labelText, (float)xPos, MappedColor.GridLabel.C());
                                }
                                else
                                    DrawVerticalGridLine(gridVertexList, (float)xPos, MappedColor.GridMinor.C());
                            }

                            //after 9 sub-lines have been drawn for this decade
                            line++;
                            if (line == 10)
                            {
                                line = 1;
                                decade++;
                            }
                        }
                    }
                }
                else
                {
                    int line = 0;
                    for (float pos = 0; pos <= 0.5f; pos += HorizontalTickSpacingMinor)
                    {
                        if (line == 0)
                        {
                            DrawVerticalGridLine(gridVertexList, 0.5f + pos, MappedColor.GridMajor.C(), HorizontalOffsetModuloMajorTickSpacing);
                            DrawVerticalGridLine(gridVertexList, 0.5f - pos, MappedColor.GridMajor.C(), HorizontalOffsetModuloMajorTickSpacing);
                            line = subDivisions - 1;

                            if (ShowVerticalGridLineLabelsBottom)
                            {
                                float finalXPos1 = 0.5f + pos + HorizontalOffsetModuloMajorTickSpacing;
                                if (finalXPos1 < 1)
                                {
                                    double sec1 = DivisionHorizontal.FullRange * (1f - finalXPos1);
                                    AddGridLabelVerBottom(LabNation.Common.Utils.siTime(sec1), finalXPos1, GridLabelColor.C());
                                }

                                if (pos != 0)
                                {
                                    float finalXPos2 = 0.5f - pos + HorizontalOffsetModuloMajorTickSpacing;
                                    double sec2 = DivisionHorizontal.FullRange * (1f - finalXPos2);
                                    AddGridLabelVerBottom(LabNation.Common.Utils.siTime(sec2), finalXPos2, GridLabelColor.C());
                                }
                            }
                        }
                        else
                        {
                            if (ShowVerticalGridLinesMinor)
                            {
                                DrawVerticalGridLine(gridVertexList, 0.5f + pos, MappedColor.GridMinor.C(), HorizontalOffsetModuloMajorTickSpacing);
                                DrawVerticalGridLine(gridVertexList, 0.5f - pos, MappedColor.GridMinor.C(), HorizontalOffsetModuloMajorTickSpacing);
                            }
                            line--;
                        }
                    }
                }
            }

            //now add blue box
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), MappedColor.GridHilite.C()));

            //add the cross
            if (ShowCross)
            {
                gridVertexList.Add(new VertexPositionColor(new Vector3(0, 0.5f, 0), MappedColor.GridHilite.C()));
                gridVertexList.Add(new VertexPositionColor(new Vector3(1, 0.5f, 0), MappedColor.GridHilite.C()));
                gridVertexList.Add(new VertexPositionColor(new Vector3(0.5f + HorizontalOffset, 0, 0), MappedColor.GridHilite.C()));
                gridVertexList.Add(new VertexPositionColor(new Vector3(0.5f + HorizontalOffset, 1, 0), MappedColor.GridHilite.C()));
            }

            gridVertices = gridVertexList.ToArray();

            //FIXME: this is not the place to call this method. In case of 2 grids it will be called 2 times.
            if (!SupportsLogarithmicScale) //don't do this for freqgrid, as arguments below would be 0 and screw up cursors
                UpdateCursors(this.triggerHoldoff, this.viewFinderCenter);
        }

        private string ConvertYPosToVerticalLabel(float yPos)
        {
            double value = DivisionVertical.FullRange * yPos;
            value -= (DivisionVertical.FullRange/2.0 - DivisionVertical.Offset);
            value = LabNation.Common.Utils.significanceTruncate(value, 3);
            //for improved readability, all labels must have the same unit-prefix. therefore: 1/ user referencedScale and provide max value as reference, 2/use the max value in siPrefix to ensure all labels have the same prefix
            string labelText = LabNation.Common.Utils.siReferencedScale(DivisionVertical.FullRange, value, 0.01, 3) + LabNation.Common.Utils.siPrefix(DivisionVertical.FullRange, ColorMapper.averagedVoltagePrecision, gridLabelUnit);
            return labelText;
        }

        private void AddGridLabelHorLeft(string labelText, float yPos, Color color)
        {
            Vector3 worldLoc = new Vector3(0, yPos, 0);
            Vector3 screenLoc = device.Viewport.Project(worldLoc, Matrix.Identity, View, localWorld);

            screenLoc.X += Scaler.GridLabelMargin.InchesToPixels();
            screenLoc.Y -= Scaler.GridLabelMargin.InchesToPixels();

            AddGridLabel(labelText, screenLoc, color);
        }
        private void AddGridLabelHorRight(string labelText, float yPos, Color color)
        {
            Vector3 worldLoc = new Vector3(1, yPos, 0);
            Vector3 screenLoc = device.Viewport.Project(worldLoc, Matrix.Identity, View, localWorld);

            screenLoc.X -= Scaler.GridLabelMargin.InchesToPixels();
            screenLoc.Y -= Scaler.GridLabelMargin.InchesToPixels();

            Vector2 size = labelFont.MeasureString(labelText);
            screenLoc.X -= size.X;

            AddGridLabel(labelText, screenLoc, color);
        }
        private void AddGridLabelVerBottom(string labelText, float xPos, Color color)
        {
            Vector3 worldLoc = new Vector3(xPos, 1, 0);
            Vector3 screenLoc = device.Viewport.Project(worldLoc, Matrix.Identity, View, localWorld);

            screenLoc.X += Scaler.GridLabelMargin.InchesToPixels();
            screenLoc.Y -= Scaler.GridLabelMargin.InchesToPixels();

            AddGridLabel(labelText, screenLoc, color);
        }

        private void AddGridLabel(string labelText, Vector3 screenLoc, Color color)
        {            
            Vector2 size = labelFont.MeasureString(labelText);

            //adjust for font height and give small notch to top-right
            screenLoc.Y -= size.Y;            

            //make sure entire label is drawn within boundary of grid
            if (screenLoc.X +size.X > Boundaries.Right) screenLoc.X = Boundaries.Right-size.X;
            if (screenLoc.X < Boundaries.Left) screenLoc.X = Boundaries.Left;
            if (screenLoc.Y +size.Y > Boundaries.Bottom) screenLoc.Y = Boundaries.Bottom - size.Y;
            if (screenLoc.Y < Boundaries.Top) screenLoc.Y = Boundaries.Top;

            gm.LabelPrinter.AddGridLabelDefinition(this, new LabelDefinition(labelText, new Vector2((int)(screenLoc.X), (int)(screenLoc.Y)), color));
        }

        private void DrawVerticalGridLine(List<VertexPositionColor>gridVertexList, float xPos, Color lineColor, float offset = 0f)
        {
            xPos += offset;
            //normal gray line
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 1, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0, 0), lineColor));

            //top spoke
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, spokeLengthVertical, 0), lineColor));

            //bottom spoke
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 1f-spokeLengthVertical, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 1f, 0), MappedColor.GridHilite.C()));

            //center spoke
            //need to divide by 4, because this one is not blended: it looks stronger, and should thus be shorter
            if (ShowCenterSpoke)
            {
                gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f - spokeLengthVertical, 0), lineColor));
                if (ShowVerticalGridLinesMajor)
                {
                    gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f, 0), MappedColor.GridHilite.C()));
                    gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f, 0), MappedColor.GridHilite.C()));
                }
                gridVertexList.Add(new VertexPositionColor(new Vector3(xPos, 0.5f + spokeLengthVertical, 0), lineColor));
            }
        }
        private void DrawHorizontalGridLine(List<VertexPositionColor> gridVertexList, float yPos, Color lineColor, float horizontalOffset, string labelText, Color labelColor)
        {
            //normal gray line
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, yPos, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0.5f, yPos, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0.5f, yPos, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, yPos, 0), lineColor));

            //left spoke
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, yPos, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(spokeLengthHorizontal, yPos, 0), lineColor));

            //right spoke
            gridVertexList.Add(new VertexPositionColor(new Vector3(1-spokeLengthHorizontal, yPos, 0), lineColor));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, yPos, 0), MappedColor.GridHilite.C()));

            //center spoke
            if (ShowCenterSpoke)
            {
                float x = 0.5f + horizontalOffset;
                gridVertexList.Add(new VertexPositionColor(new Vector3(x - spokeLengthHorizontal, yPos, 0), lineColor));
                gridVertexList.Add(new VertexPositionColor(new Vector3(x, yPos, 0), MappedColor.GridHilite.C()));
                gridVertexList.Add(new VertexPositionColor(new Vector3(x, yPos, 0), MappedColor.GridHilite.C()));
                gridVertexList.Add(new VertexPositionColor(new Vector3(x + spokeLengthHorizontal, yPos, 0), lineColor));
            }

            if (labelText != "")
            {                
                if (ShowHorizontalGridLineLabelsLeft)
                    AddGridLabelHorLeft(labelText, yPos, labelColor);
                if (ShowHorizontalGridLineLabelsRight)
                    AddGridLabelHorRight(labelText, yPos, labelColor);
            }
        }
        private void DrawRectangle(Rectangle rect)
        {
            Vector3 pcp1 = device.Viewport.Unproject(new Vector3(rect.X, rect.Y, 1), Matrix.Identity, Matrix.Identity, Matrix.Identity);
            Vector3 pcp2 = device.Viewport.Unproject(new Vector3(rect.Right, rect.Bottom, 1), Matrix.Identity, Matrix.Identity, Matrix.Identity);

            List<VertexPositionColor> rectVertList = new List<VertexPositionColor>();

            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp1.Y, 0), Color.White));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp2.X, pcp1.Y, 0), Color.White));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp2.X, pcp2.Y, 0), Color.White));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp2.Y, 0), Color.White));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp1.Y, 0), Color.White));

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.Identity;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineStrip, rectVertList.ToArray(), 0, rectVertList.Count - 1);
            }
        }

        #endregion

        #region Cursors

        public CursorVertical AddCursor(Vector2 location, string unit, ValueFromLocation convertor, double precision)
        {
            CursorVertical cursor = new CursorVertical(this, location, unit, convertor, precision, triggerHoldoff);
            gm.AddCursor(cursor);
            cursors.Add(cursor);
            CheckToAddDeltaCursor();
            return cursor;
        }
        public void CheckToAddDeltaCursor()
        {
            /* if more than 1: display delta measurment */
            List<CursorVertical> vertCursorList = gm.Cursors.Where(x => x is CursorVertical && x.Grid == this).ToList().Cast<CursorVertical>().ToList();
            if (vertCursorList.Count > 1)
            {
                //first remove possibly existing one
                RemoveVerticalDeltaCursor(gm.Cursors.Where(x => x is CursorVerticalDelta && x.Grid == this).Select(x => x as CursorVerticalDelta).FirstOrDefault());

                //define and add deltacursor             
                CursorVertical parent1 = vertCursorList[vertCursorList.Count - 1];
                CursorVertical parent2 = vertCursorList[vertCursorList.Count - 2];
                CursorVerticalDelta newDeltaCursor = new CursorVerticalDelta(this, (parent1.Location + parent2.Location) / 2f, parent1, parent2, parent1.Precision);
                gm.AddCursor(newDeltaCursor);
                cursors.Add(newDeltaCursor);
            }
        }
        public void UpdateCursors(double triggerHoldoff, double viewFinderCenter)
        {
            if (!(this is GridAnalog)) return;

            //need to store this locally, so newly added cursors can be positioned correctly
            this.triggerHoldoff = triggerHoldoff;
            this.viewFinderCenter = viewFinderCenter;

            foreach (Cursor child in gm.Cursors)
                if (child is CursorVertical)
                    (child as CursorVertical).Recompute(triggerHoldoff, DivisionHorizontal.Offset, DivisionHorizontal.FullRange);
        }
        public void ChangeVerticalCursorReference(bool waveReferenced_nScreenReferenced)
        {
            foreach (Cursor child in gm.Cursors)
                if (child is CursorVertical)
                    (child as CursorVertical).ChangeReference(waveReferenced_nScreenReferenced);
        }
        public void RemoveVerticalCursor(CursorVertical cursor)
        {
            bool removedDeltaCursor = false;

            //first check whether a deltaindicator was depending on this one
            CursorVerticalDelta deltaCursor = (CursorVerticalDelta)gm.Cursors.FirstOrDefault(x => x is CursorVerticalDelta && x.Grid == this);
            if (deltaCursor != null)
            {
                if ((deltaCursor.ParentCursor1 == cursor) || (deltaCursor.ParentCursor2 == cursor))
                {
                    deltaCursor.Unlink();
                    gm.RemoveCursor(deltaCursor);
                    removedDeltaCursor = true;
                }
            }

            gm.RemoveCursor(cursor);
            cursors.Remove(cursor);

            if (removedDeltaCursor)
                CheckToAddDeltaCursor();
        }
        public void RemoveVerticalDeltaCursor(CursorVerticalDelta deltaCursor)
        {
            if (deltaCursor == null) return;
            deltaCursor.Unlink();
            gm.RemoveCursor(deltaCursor);
            cursors.Remove(deltaCursor);
        }
        public void CycleVerticalDeltaCursor(CursorVertical parent1)
        {
            //first make sure the given cursor is positioned as 'most recent'
            gm.RemoveCursor(parent1);
            gm.AddCursor(parent1);
            
            //now make delta cursor between 2 most recent cursors
            CheckToAddDeltaCursor();
        }        
        #endregion

        #region EDrawable

        protected override void OnBoundariesChangedInternal()
        {
            this.interactiveAreas[0] = Boundaries;
                        
            //calc spoke length, based on pixel size
            int spokeLengthInPixels = 10;
            this.spokeLengthHorizontal = spokeLengthInPixels / (float)Boundaries.Width;
            this.spokeLengthVertical = spokeLengthInPixels / (float)Boundaries.Height;

            //now calculate the coordinates of all grid lines
            DefineSceneContents();
        }

        protected override void UpdateInternal(GameTime now)
        {
            base.UpdateInternal(now);

            //watchdog to clean up potential mess left behind by mousescrolls
            //mousescrolls stack up 2 pinch + 1 pinchComplete gestures
            //in case for some reason the pinchComplete is missed, the grid remains GestureClaimer and the GUI is locked up forever
            if ((ScopeApp.GestureClaimer == this) && (DateTime.Now.Subtract(lastGestureTimestamp).TotalMilliseconds > 3000))
            {
                ReleaseGestureControl();
                pinching = false;
                if (PinchDragEndCallback != null)
                    PinchDragEndCallback.Call(this);
                pinchPreviousPosition1 = null;
                pinchPreviousPosition2 = null;
            }
        }

        protected override void DrawInternal(GameTime time)
        {
            if (gridVertices == null) return;

            effect.World = localWorld;
            effect.View = this.View;
            effect.Projection = this.Projection;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineList, gridVertices, 0, gridVertices.Length / 2);
            }

            if (dragRectangleList.Count > 0)
                DrawRectangle(dragRectangleList[0]);
        }

        private enum ZoomDirection 
        {
            Undefined,
            Horizontal,
            Vertical
        };
        override protected void HandleGestureInternal(GestureSample gesture)
        {
            lastGestureTimestamp = DateTime.Now;
            Point gestureLocation = new Point((int)(gesture.Position.X - gesture.Delta.X), (int)(gesture.Position.Y - gesture.Delta.Y));
            switch (gesture.GestureType)
            {
                case GestureType.MouseScroll:
                    Vector2 ratio = Vector2.Zero;
                    if (gesture.Delta == new Vector2(0, 1)) //mouseScroll UP 
                        ratio = new Vector2(0.5f, 1f);
                    else if (gesture.Delta == new Vector2(0, -1)) //mouseScroll DOWN
                        ratio = new Vector2(2f, 1f);
                    else if (gesture.Delta == new Vector2(1, 0)) //mouseScroll RIGHT
                        ratio = new Vector2(1f, 2f);
                    else if (gesture.Delta == new Vector2(-1, 0)) //mouseScroll LEFT
                        ratio = new Vector2(1f, 0.5f);

                    Vector2 boundCenterScroll = Utils.PointToVector(Boundaries.Center);
                    Vector2 distanceToOriginScroll = gesture.Position - boundCenterScroll;
                    Vector3 DTOWorldSpaceCoordsScroll = device.Viewport.Unproject(new Vector3(distanceToOriginScroll, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                    Vector2 finalPinchCenterScroll = new Vector2(DTOWorldSpaceCoordsScroll.X, DTOWorldSpaceCoordsScroll.Y);

                    if (PinchDragCallback != null)
                    {
                        PinchDragCallback.Call(this, new object[] {
                            ratio, //Change ratio
                            finalPinchCenterScroll, //Pinch center
                            Vector2.Zero, //Offset delta
							false, //Check if wave is under cursor
                            new Rectangle((int)gesture.Position.X, (int)gesture.Position.Y, 0, 1), //at least height 1 needed to set canPerformVerticalScaling = true
                            true
                        });
                    }
                    if (PinchDragEndCallback != null)
                        PinchDragEndCallback.Call(this, null);
                    ReleaseGestureControl();
                    break;
                case GestureType.Pinch:
					if (pinchPreviousPosition1 == null || pinchPreviousPosition2 == null) //start of pinch gesture
                    {
                        pinchedThroughMirror = false;
                        pinchPreviousPosition1 = gesture.Position;
                        pinchPreviousPosition2 = gesture.Position2;
                        UICallbacks.PinchGridBegin(this, null);
                    }
                    else
                    {
                        ZoomDirection zoomDirection = ZoomDirection.Undefined;

                        Vector2 previousDistance = pinchPreviousPosition1.Value - pinchPreviousPosition2.Value;
                        Vector2 currentDistance = gesture.Position - gesture.Position2;
                        Rectangle pinchRect = new Rectangle(
                        	(int)Math.Min(gesture.Position.X, gesture.Position2.X),
							(int)Math.Min(gesture.Position.Y, gesture.Position2.Y),
							(int)Math.Abs(gesture.Position.X - gesture.Position2.X),
							(int)Math.Abs(gesture.Position.Y - gesture.Position2.Y)
                        );
                        //Find out if we're zooming horizontally or vertically
                        Vector2 displacement = currentDistance - previousDistance;
                        if(Math.Abs(displacement.X) > Math.Abs(displacement.Y))
                            zoomDirection = ZoomDirection.Horizontal;
                        else
                            zoomDirection = ZoomDirection.Vertical;
                        
                        //protection against going too small, which would result in infinite values
                        int minimalDistance = Scaler.MinimalPinchSize;

                        if ((zoomDirection == ZoomDirection.Horizontal) && (Math.Abs(currentDistance.X) < minimalDistance))
                            currentDistance.X = 1;
                        if ((zoomDirection == ZoomDirection.Vertical) && (Math.Abs(currentDistance.Y) < minimalDistance))
                            currentDistance.Y = 1;

                        //when too small: set previous to current, so ratios are 1 AND in case of smallstart ratio doesn't blow up (as would happen when just set to 1)
                        if ((zoomDirection == ZoomDirection.Horizontal) && (Math.Abs(previousDistance.X) < minimalDistance))
                            previousDistance.X = currentDistance.X;
                        if ((zoomDirection == ZoomDirection.Vertical) && (Math.Abs(previousDistance.Y) < minimalDistance))
                            previousDistance.Y = currentDistance.Y;

                        float ratioX = 1f;
                        float ratioY = 1f;
                        if (Math.Abs(currentDistance.Y) >= minimalDistance && zoomDirection == ZoomDirection.Vertical) 
                            ratioY = previousDistance.Y / currentDistance.Y;
                        
                        if (Math.Abs(currentDistance.X) >= minimalDistance && zoomDirection == ZoomDirection.Horizontal) 
                            ratioX = previousDistance.X / currentDistance.X;

                        //protection against flipping the waveforms
                        if (ratioX < 0) pinchedThroughMirror = true;
                        if (ratioY < 0) pinchedThroughMirror = true;

                        if (pinchedThroughMirror)
                            break;
                    
                        //scaling is happening through the origin point of the graph.
                        //so: wave first needs to be moved to origin, then scaled, then moved back
                        Vector2 boundCenter = Utils.PointToVector(Boundaries.Center);
                        Vector2 distanceToOrigin = (gesture.Position + gesture.Position2) / 2 - boundCenter;
                        Vector3 DTOWorldSpaceCoords = device.Viewport.Unproject(new Vector3(distanceToOrigin, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                        
                        Vector2 offsetDelta = (gesture.Position + gesture.Position2) / 2 - (pinchPreviousPosition1.Value + pinchPreviousPosition2.Value) / 2f;
                        Vector3 offsetDeltaWorldSpaceCoords = device.Viewport.Unproject(new Vector3(offsetDelta, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                        Vector2 offsetDeltaWorldSpaceCoordsV2 = new Vector2(offsetDeltaWorldSpaceCoords.X, offsetDeltaWorldSpaceCoords.Y);

                        if (zoomDirection == ZoomDirection.Horizontal) offsetDeltaWorldSpaceCoordsV2.Y = 0;
                        if (zoomDirection == ZoomDirection.Vertical) offsetDeltaWorldSpaceCoordsV2.X = 0;

                        //summarize and commit
                        Vector2 finalRatio = new Vector2(ratioX, ratioY);
                        Vector2 finalPinchCenter = new Vector2(DTOWorldSpaceCoords.X, DTOWorldSpaceCoords.Y);
                        Vector2 finalOffset = Vector2.Zero;

                        switch (PinchingBehaviour)
                        {
                            case PinchingBehaviour.RestrictedHorVerWithoutPanning:
                                finalOffset = Vector2.Zero;
                                break;
                            case Drawables.PinchingBehaviour.RestrictedHorVerWithPanning:
                                finalOffset = offsetDeltaWorldSpaceCoordsV2;
                                break;
                            default:
                                throw new Exception("PinchingBehaviour not fully implemented");
                        }

                        if (PinchDragCallback != null)
                        {
                            PinchDragCallback.Call(this, new object[] {
                                finalRatio, //Change ratio
                                finalPinchCenter, //Pinch center
                                finalOffset, //Offset delta
							    !pinching, //Check if wave is under cursor
                                pinchRect,
                                false
                            });
                        }
                        pinching = true;

                        //prep for next cycle
                        pinchPreviousPosition1 = gesture.Position;
                        pinchPreviousPosition2 = gesture.Position2;
                    }
                    break;

                case GestureType.PinchComplete:
                    pinching = false;
                    if (PinchDragEndCallback != null)
                        PinchDragEndCallback.Call(this);
                    pinchPreviousPosition1 = null;
                    pinchPreviousPosition2 = null;
                    pinching = false;
                    this.ReleaseGestureControl();
                    break;

                case GestureType.FreeDrag:
                    if ((gesture.Delta2.X == 1) && (gesture.Delta2.Y == 1)) //dirty hack, which indicates it's a modified drag
                    {
                        //if out of bounds: cancel!
                        if (!Boundaries.Contains((int)gesture.Position.X, (int)gesture.Position.Y))
                        {
                            dragRectangleList.Clear();
                            ReleaseGestureControl();
                            return;
                        }

                        if (dragRectangleList.Count == 0) //begin of drag => needs to store initial point
                            rectangleStartingPoint = gesture.Position;

                        dragRectangleList.Clear();
                        dragRectangleList.Add(new Rectangle((int)rectangleStartingPoint.X, (int)rectangleStartingPoint.Y, (int)(gesture.Position.X - rectangleStartingPoint.X), (int)(gesture.Position.Y - rectangleStartingPoint.Y)));
                    }
                    else //it's a touchpad drag, or a right-click-drag => move the graphs
                    {
                        if (this is GridMeasurement)
                            ReleaseGestureControl(); //the call to PinchDragCallback below will dispose of this grid, so let's make sure to release GestureControl first

                        Vector3 deltaWorldSpaceCoords = device.Viewport.Unproject(new Vector3(gesture.Delta, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                        if (PinchDragCallback != null)
                        {
                            PinchDragCallback.Call(this, new object[] {
                                new Vector2(1, 1), 
                                new Vector2(0, 0), 
                                new Vector2(deltaWorldSpaceCoords.X, deltaWorldSpaceCoords.Y), 
                                !panZooming, 
                                gestureLocation
                            });
                        }
                        panZooming = true;
                    }
                    break;
                case GestureType.DragComplete:
                    if ((gesture.Delta2.X == 1) && (gesture.Delta2.Y == 1)) //dirty hack, which indicates it's a modified drag
                    {
                        //remove rectangle
                        dragRectangleList.Clear();

                        //could potentially reuse the last-stored rectangle
                        Rectangle xnaRectangle = new Rectangle((int)rectangleStartingPoint.X, (int)rectangleStartingPoint.Y, (int)(gesture.Position.X - rectangleStartingPoint.X), (int)(gesture.Position.Y - rectangleStartingPoint.Y));

                        //calc offset -- this must be done BEFORE the scaling, as the offset was done with the current scaling
                        Vector2 deltaToCenter = new Vector2(xnaRectangle.Center.X - Boundaries.Center.X, xnaRectangle.Center.Y - Boundaries.Center.Y);
                        Vector3 deltaWorldSpaceCoords = device.Viewport.Unproject(new Vector3(deltaToCenter, 1), Matrix.Identity, View, Utils.RemoveTranslation(this.localWorld));

                        //calc scale -- need to take abs, otherwise image is mirrored!
                        Vector2 scaleRatio = new Vector2();
                        scaleRatio.X = (float)Math.Abs((float)(xnaRectangle.Width) / (float)Boundaries.Width);
                        scaleRatio.Y = (float)Math.Abs((float)(xnaRectangle.Height) / (float)Boundaries.Height);

                        //if zoom would be too large: cancel, as this is probably an unwanted small rectangle
                        if (scaleRatio.X + scaleRatio.Y < 0.1f)
                        {
                            Logger.Debug("Dropped zoom-in call as rectangle was so small it was probably not intended");
                            this.ReleaseGestureControl();
                            return;
                        }

                        //update scale and offsets according to rectangle!
                        if (PinchDragCallback != null)
                        {
                            PinchDragCallback.Call(this, new object[] {
                                scaleRatio, 
                                new Vector2(0, 0), 
                                new Vector2(-deltaWorldSpaceCoords.X, -deltaWorldSpaceCoords.Y), 
                                false
                            });
                        }
                        if (PinchDragEndCallback != null)
                            PinchDragEndCallback.Call(this);
                    }
                    else //it's a touchpad dragComplete, or a right-click-dragComplete
                    {
                        panZooming = false;
                        if (DragEndCallback != null)
                            DragEndCallback.Call(this, gestureLocation);
                    }
                    ReleaseGestureControl();
                    break;
                case GestureType.DoubleTap:
                    Vector2 boundCenterDoubleTap = Utils.PointToVector(Boundaries.Center);
                    Vector2 distanceToOriginDoubleTap = gesture.Position - boundCenterDoubleTap;
                    Vector3 DTOWorldSpaceCoordsDoubleTap = device.Viewport.Unproject(new Vector3(distanceToOriginDoubleTap, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                    Vector2 finalPositionDoubleTap = new Vector2(DTOWorldSpaceCoordsDoubleTap.X, DTOWorldSpaceCoordsDoubleTap.Y);

                    if (DoubleTapCallback != null)
                        DoubleTapCallback.Call(this, finalPositionDoubleTap);
                    ReleaseGestureControl();
                    break;
                case GestureType.Tap:
                    if (TapCallback != null)
                        TapCallback.Call(this, gestureLocation);
                    ReleaseGestureControl();
                    break;
                case GestureType.Hold:
                    //removing the following line for now. If grid-hold gesture needs to be reactivated, keep in mind the following method makes a cast to an indicator
                    //uiHandler.GUI_GridRightClicked(this, gestureLocation);
                    ReleaseGestureControl();
                    break;
            }
        }

        #endregion
    }
}
