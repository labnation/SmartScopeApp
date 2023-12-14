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
using LabNation.DeviceInterface.DataSources;

namespace ESuite.Drawables
{
    internal struct IntervalTimeDefinition
    {
        public float startTime;
        public float endTime;
        public IntervalTimeDefinition(float startTime, float endTime)
        {
            this.startTime = startTime;
            this.endTime = endTime;
        }
    }

    internal struct LabelDefinition
    {
        public string text;
        public Vector2 topLeftScreenCoord;
        public Color color;
        public LabelDefinition(string text, Vector2 topLeftScreenCoord, Color color)
        {
            this.text = text;
            this.topLeftScreenCoord = topLeftScreenCoord;
            this.color = color;
        }
    }

    internal class WaveformDigital : Waveform
    {
        public float RelHeight = 0.1f;
        private bool showIntervals = false;
        public bool ShowIntervals
        {
            get { return showIntervals; }
            set
            {
                if (value != showIntervals)
                    this.redrawRequest = true;
                 showIntervals = value;
            }
        }
        public IndicatorInteractive TriggerIndicator;
        private SpriteFont internalArrowFont;
        private float requiredInternalArrowSpacing = 0;
        private MappedColor internalArrowMappedColor = MappedColor.InternalArrow;

        private float startTime;
        private float highLevel;
        private float lowLevel;
        private int nrDataElements;
        private float samplingPeriod;

        private VertexPositionColor[] intervalVertices = new VertexPositionColor[0];
        private List<LabelDefinition> intervalLabelDefinitions = new List<LabelDefinition>();
        private List<IntervalTimeDefinition> intervalTimeDefinitions = new List<IntervalTimeDefinition>();

        public override bool Enabled
        {
            get { return base.Enabled; }
            set
            {
                //Panorama digiwaves don't have trigger indicators
                if (this.TriggerIndicator != null) 
                    this.TriggerIndicator.Visible = value;
                base.Enabled = value;
            }
        }

        new static public Dictionary<Channel, WaveformDigital> Waveforms
        {
            get
            {
                return Waveform.Waveforms.
                    Where(x => x.Value is WaveformDigital).
                    ToDictionary(x => x.Key, x => (WaveformDigital)x.Value);
            }
        }
        public WaveformDigital(Graph graph, MappedColor graphColor, Channel channel, WaveformDigital panoramaWaveParent = null)
            : base (graph, graphColor, PrimitiveType.LineStrip, 2048, channel, panoramaWaveParent)
		{
			this.voltageRange = 1f; //define height of digital wave as 1

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            internalArrowFont = content.Load<SpriteFont>(Scaler.GetFontInternalArrows());
            requiredInternalArrowSpacing = internalArrowFont.MeasureString("0.00 ms").X;
        }

        protected override void DrawInternal(GameTime time)
        {
            base.DrawInternal(time);

            if (ShowIntervals)
                DrawIntervals();
        }

		public override double VoltageRange {
			get { return voltageRange; }
			set { throw new Exception ("Cannot set voltage range of digital channel"); }
		}

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {
            if (dataBuffers == null) return null;
            if (dataBuffers.Length < 1) return null;
            if (dataBuffers[0] == null) return null;
            WaveDataBuffer dataBuffer = dataBuffers[0];

            bool[] dataToRender = (bool[])dataBuffer.SubsampledData;
            int vertices = dataToRender.Length;
            nrDataElements = dataToRender.Length;

            //next: find indices in buffer, corresponding to cropStartTime and cropStopTime
            int bufferDrawStartIndex = FindBufferIndexByTimeSafeLeft(dataBuffer.SubsampledTimeData, (float)visibleStartTime);
            int bufferDrawStopIndex = FindBufferIndexByTimeSafeRight(dataBuffer.SubsampledTimeData, (float)(visibleEndTime));

            startTime = (float)dataBuffer.SubsampledStartTime;
            samplingPeriod = (float)dataBuffer.SubsampledSamplePeriod;
			
            highLevel = (float)voltageRange / 2f * RelHeight; 
            lowLevel = -(float)voltageRange / 2f * RelHeight;
            this.ActiveRange = highLevel - lowLevel;
			this.Minimum = lowLevel;
            
            intervalTimeDefinitions = new List<IntervalTimeDefinition>();            

            Color color = GraphColor.C();
            float startYPos = dataToRender[bufferDrawStartIndex] ? highLevel : lowLevel;

            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)VoltageRange; //VoltageRange represents full vertical size
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)TimeRange; //TimeRange represents full vertical size            
            float voltageThickness = onePixelHeight / 2f * (Waveform.Thickness + 1f);
            float timeThickness = onePixelWidth / 2f * (Waveform.Thickness + 1f);
            float actualThickness = PanoramaWave == null ? 0 : Waveform.Thickness;

            //starting point
            List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
            if (actualThickness == 0)
            {
                vertexList.Add(new VertexPositionColor(new Vector3(startTime, startYPos, 0), color));
            }
            else {
                if (startYPos == lowLevel)
                {
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime, lowLevel + voltageThickness, 0.0f), color));
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime, lowLevel - voltageThickness, 0.0f), color));
                }
                else
                {
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime, highLevel + voltageThickness, 0.0f), color));
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime, highLevel - voltageThickness, 0.0f), color));
                }
            }

            //only draw changes in edges
            float lastEdgeXPos = startTime;
            for (int i = bufferDrawStartIndex+1; i < bufferDrawStopIndex; i++)
            {
                //only render edges
                if (dataToRender[i - 1] != dataToRender[i])
                {
                    float xPos = startTime + samplingPeriod * i;
                    if (actualThickness == 0)
                    {
                        //thin waves
                        if (dataToRender[i]) //rising edge
                        {
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos, lowLevel, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos, highLevel, 0.0f), color));
                        }
                        else
                        {
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos, highLevel, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos, lowLevel, 0.0f), color));
                        }
                    }
                    else
                    {                        
                        if (dataToRender[i]) //rising edge
                        {
                            //              c----------------
                            //              |d---------------
                            //              ||
                            //      --------a|
                            //      ---------b
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos-timeThickness, lowLevel+voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos+timeThickness, lowLevel-voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, highLevel + voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, highLevel - voltageThickness, 0.0f), color));
                        }
                        else //falling edge
                        {
                            //      ---------e
                            //      --------f|
                            //              ||
                            //              |g-----------------
                            //              h------------------
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, highLevel + voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, highLevel - voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, lowLevel + voltageThickness, 0.0f), color));
                            vertexList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, lowLevel - voltageThickness, 0.0f), color));
                        }
                    }

                    if ((lastEdgeXPos != startTime) && !(parent is WaveformDigital))
                        intervalTimeDefinitions.Add(new IntervalTimeDefinition(lastEdgeXPos, xPos));
                    lastEdgeXPos = xPos;
                }
			}

            //finish point
            float lastYpos = vertexList[vertexList.Count - 1].Position.Y; //don't use value of last sample, as that's probably not the last sample displayed!
            if (actualThickness == 0)
            {
                vertexList.Add(new VertexPositionColor(new Vector3(startTime + samplingPeriod * (float)dataToRender.Length, lastYpos, 0), color));
                primitiveTypeToRender = PrimitiveType.LineStrip;
            }
            else
            {
                if (lastYpos< (highLevel+lowLevel)/2f)
                {
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime + samplingPeriod * (float)dataToRender.Length, lowLevel + voltageThickness, 0.0f), color));
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime + samplingPeriod * (float)dataToRender.Length, lowLevel - voltageThickness, 0.0f), color));
                }
                else
                {
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime + samplingPeriod * (float)dataToRender.Length, highLevel + voltageThickness, 0.0f), color));
                    vertexList.Add(new VertexPositionColor(new Vector3(startTime + samplingPeriod * (float)dataToRender.Length, highLevel - voltageThickness, 0.0f), color));
                }
                primitiveTypeToRender = PrimitiveType.TriangleStrip;
            }

            VertexPositionColor[] vertexArray = vertexList.ToArray();

            CalculateBoundaries();
			
            if (isPanoramaWave)
                UpdateScaleOffsetMatrix();

            ConvertIntervalTimesToDrawlists();

            return vertexArray;
		}

        override protected void CalculateBoundaries()
        {
            Vector3 waveAreaTopLeftWorldCoords = new Vector3(startTime, highLevel, 1);
            Vector3 waveAreaBottomRightWorldCoords = new Vector3(startTime + ((float)nrDataElements - 1f) * samplingPeriod, lowLevel, 1);
            Vector3 waveAreaTopLeftScreenCoords = device.Viewport.Project(waveAreaTopLeftWorldCoords, this.Projection, this.View, this.scaleOffsetLerpMatrix.CurrentValue);
            Vector3 waveAreaBottomRightScreenCoords = device.Viewport.Project(waveAreaBottomRightWorldCoords, this.Projection, this.View, this.scaleOffsetLerpMatrix.CurrentValue);
            backgroundRectangle = new Rectangle((int)waveAreaTopLeftScreenCoords.X, (int)waveAreaTopLeftScreenCoords.Y, (int)(waveAreaBottomRightScreenCoords.X - waveAreaTopLeftScreenCoords.X), (int)(waveAreaBottomRightScreenCoords.Y - waveAreaTopLeftScreenCoords.Y));
        }

        protected override void UpdateScaleOffsetMatrix()
        {
            Matrix prevMatrix = ScaleOffsetMatrix;
            base.UpdateScaleOffsetMatrix();

            //if there was no change in matrix: no need to run further updates
            if (prevMatrix == ScaleOffsetMatrix) return;

            if (TriggerIndicator != null)
                TriggerIndicator.Position = voltageOffset / (float)voltageRange;


            //FIXME: Is this not already called through base.UpdateScaleOffsetMatrix() > RebuildVertexBuffer() > CreateVertices()
            //FIXME: Should we not only call this when the intervals need to be shown? And call 
            //       ConvertIntervalTimesToDrawList() whenever ShowIntervals changes? (Make it a property)
            ConvertIntervalTimesToDrawlists();
        }

        #region Intervals

        private void ConvertIntervalTimesToDrawlists()
        {
            if (intervalTimeDefinitions == null) return;

            //FIXME: make dependant on ppi
            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)VoltageRange; //VoltageRange represents full vertical size
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)TimeRange; //TimeRange represents full vertical size
            float minimumInternalArrowWidth = requiredInternalArrowSpacing * onePixelWidth;
            float internalArrowMarginHor = 3f * onePixelWidth;
            float internalArrowMarginVer = 3f * onePixelHeight;

            float high = (float)voltageRange / 2f * RelHeight;
            float low = -(float)voltageRange / 2f * RelHeight;
            float mid = (high + low) / 2f;

            Color color = GraphColor.C();
            Color internalArrowColor = MappedColor.InternalArrow.C();

            List<VertexPositionColor> intervalVertexList = new List<VertexPositionColor>();
            intervalLabelDefinitions.Clear();

            float screenLeftTime = 0;
            float screenRightTime = 0;
            CalcScreenTimeForIntervalTimes(out screenLeftTime, out screenRightTime);            

            foreach (IntervalTimeDefinition interv in intervalTimeDefinitions)
            {
                //check whether interval is contained in screen
                if (interv.startTime > screenLeftTime && interv.endTime < screenRightTime)
                {
                    //check whether interval is large enough to be displayed
                    if (interv.endTime - interv.startTime > minimumInternalArrowWidth)
                    {
                        AddIntervalVertices(internalArrowMarginHor, internalArrowMarginVer, mid, color, internalArrowColor, intervalVertexList, interv);
                        AddInteralText(mid, internalArrowColor, interv);
                    }
                }
            }

            intervalVertices = intervalVertexList.ToArray();
        }

        private void AddInteralText(float mid, Color internalArrowColor, IntervalTimeDefinition interv)
        {
            //add text
            string labelText = LabNation.Common.Utils.siPrint(interv.endTime - interv.startTime, 0.0000000001, ColorMapper.NumberDisplaySignificance, "s");
            Vector3 worldLoc = new Vector3((interv.endTime + interv.startTime) / 2f, mid, 0);
            Vector3 screenLoc = device.Viewport.Project(worldLoc, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            Vector2 size = internalArrowFont.MeasureString(labelText);
            intervalLabelDefinitions.Add(new LabelDefinition(labelText, new Vector2((int)(screenLoc.X - size.X / 2), (int)(screenLoc.Y - size.Y)), internalArrowColor));
        }

        private static void AddIntervalVertices(float internalArrowMarginHor, float internalArrowMarginVer, float mid, Color color, Color internalArrowColor, List<VertexPositionColor> intervalVertexList, IntervalTimeDefinition interv)
        {
            //left arrow
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.startTime + internalArrowMarginHor, mid, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.startTime + 2f * internalArrowMarginHor, mid - internalArrowMarginVer, 0.0f), color));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.startTime + internalArrowMarginHor, mid, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.startTime + 2f * internalArrowMarginHor, mid + internalArrowMarginVer, 0.0f), internalArrowColor));

            //right arrow
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.endTime - internalArrowMarginHor, mid, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.endTime - 2f * internalArrowMarginHor, mid - internalArrowMarginVer, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.endTime - internalArrowMarginHor, mid, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.endTime - 2f * internalArrowMarginHor, mid + internalArrowMarginVer, 0.0f), internalArrowColor));

            //connecting line
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.startTime + internalArrowMarginHor, mid, 0.0f), internalArrowColor));
            intervalVertexList.Add(new VertexPositionColor(new Vector3(interv.endTime - internalArrowMarginHor, mid, 0.0f), internalArrowColor));
        }

        private void CalcScreenTimeForIntervalTimes(out float screenLeftTime, out float screenRightTime)
        {
            Vector3 screenLeftScreenPos = new Vector3(parent.Boundaries.Left, 0, 0);
            Vector3 screenLeftTimePos = device.Viewport.Unproject(screenLeftScreenPos, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            Vector3 screenRightScreenPos = new Vector3(parent.Boundaries.Right, 0, 0);
            Vector3 screenRightTimePos = device.Viewport.Unproject(screenRightScreenPos, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);

            screenLeftTime = screenLeftTimePos.X;
            screenRightTime = screenRightTimePos.X;
        }

        private void DrawIntervals()
        {
            if (intervalVertices.Length > 1)
            {
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    effect.CurrentTechnique.Passes[0].Apply();
                    device.DrawUserPrimitives(PrimitiveType.LineList, intervalVertices, 0, intervalVertices.Length / 2);
                }

                spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
                foreach (LabelDefinition ld in intervalLabelDefinitions)
                    spriteBatch.DrawString(internalArrowFont, ld.text, ld.topLeftScreenCoord, ld.color);
                spriteBatch.End();
            }
        }

        #endregion
    }
}
