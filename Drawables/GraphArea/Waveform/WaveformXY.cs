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
    internal class WaveformXY : Waveform
    {
        new static public Dictionary<Channel, WaveformAnalog> Waveforms
        {
            get
            {
                return Waveform.Waveforms.
                    Where(x => x.Value is WaveformAnalog).
                    ToDictionary(x => x.Key, x => (WaveformAnalog)x.Value);
            }
        }

        public override bool Enabled
        {
            get { return base.Enabled; }
            set
            {
                base.Enabled = value;
                if (this.label != null)
                    this.label.Visible = value;
            }
        }

        public LinLog scale;
        private GridDivisionLabel label;
        private static double[] voltageRanges = new double[2];
        private GridXY grid;

        public WaveformXY(Graph graph, MappedColor graphColor, Channel channel, GridXY grid)
            : base(graph, graphColor, PrimitiveType.LineStrip, 2048, channel, null)
		{
            scale = LinLog.Linear;
            this.grid = grid;

            LoadContent();
        }

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {
            if (dataBuffers == null) return null;
            if (dataBuffers.Length < 2) return null;
            if (dataBuffers[0] == null) return null;
            if (dataBuffers[1] == null) return null;
            WaveDataBuffer dataBuffer = dataBuffers[0];

            //next: find indices in buffer, corresponding to cropStartTime and cropStopTime
            int bufferDrawStartIndex = FindBufferIndexByTimeSafeLeft(dataBuffer.SubsampledTimeData, (float)visibleStartTime);
            int bufferDrawStopIndex = FindBufferIndexByTimeSafeRight(dataBuffer.SubsampledTimeData, (float)(visibleEndTime));            

            float vRangeX;
            float vRangeY;
            float[] voltagesX;
            float[] voltagesY;
            if (!grid.InvertAxes)
            {
                vRangeX = (float)voltageRanges[0];
                vRangeY = (float)voltageRanges[1];
                voltagesX = (float[])dataBuffers[0].SubsampledData;
                voltagesY = (float[])dataBuffers[1].SubsampledData;
            }
            else
            {
                vRangeX = (float)voltageRanges[1];
                vRangeY = (float)voltageRanges[0];
                voltagesX = (float[])dataBuffers[1].SubsampledData;
                voltagesY = (float[])dataBuffers[0].SubsampledData;
            }

            //crashprotection https://www.lab-nation.com/forum/software/topics/a-few-problems-report
            if (voltagesX.Length < bufferDrawStopIndex)
            {
                LabNation.Common.Logger.Error("Crash avoided: voltages " + voltagesX.Length.ToString() + "< bufferDrawStopIndex " + bufferDrawStopIndex.ToString());
                return null;
            }
            if (voltagesX.Length != voltagesY.Length)
            {
                LabNation.Common.Logger.Error("Crash avoided: voltagesX.Length " + voltagesX.Length.ToString() + "!= voltagesY.Length " + voltagesY.Length.ToString());
                return null;
            }
            if (bufferDrawStartIndex > bufferDrawStopIndex)
            {
                LabNation.Common.Logger.Error("Crash avoided: bufferDrawStartIndex " + bufferDrawStartIndex.ToString() + "> bufferDrawStopIndex " + bufferDrawStopIndex.ToString());
                return null;
            }            
                        
            VertexPositionColor[] outputArray;
            float actualThickness = Waveform.Thickness;
            float max = float.NegativeInfinity;
            float min = float.PositiveInfinity;
            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)1; //XYwave uses identity matrix as worldmatrix -> 1
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)1; //XYwave uses identity matrix as worldmatrix -> 1
            float voltageThickness = onePixelHeight / 2f * (Waveform.Thickness + 1f);
            float timeThickness = onePixelWidth / 2f * (Waveform.Thickness + 1f);

            Color color = GraphColor.C();
            int outputPointer = 0;
            
            //init empty output array
            int dataPointsToRender = bufferDrawStopIndex - bufferDrawStartIndex;
            if (actualThickness == 0)
                outputArray = new VertexPositionColor[dataPointsToRender];
            else
                outputArray = new VertexPositionColor[dataPointsToRender * 2];

            for (int i = bufferDrawStartIndex; i < bufferDrawStopIndex; i++) 
            {
                float xPos = voltagesX[i] / vRangeX;
                float yPos = -voltagesY[i] / vRangeY;                
                
                //limit within graph. this is really required, as the FFT/XY graphs are drawn above the blockers
                if (xPos < -0.5f) xPos = -0.5f;
                if (xPos > 0.5f) xPos = 0.5f;
                if (yPos < -0.5f) yPos = -0.5f;
                if (yPos > 0.5f) yPos = 0.5f;

                if (actualThickness == 0)
                {
                    outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos, yPos, 0.0f), color);
                }
                else
                {
                    if (i == 0)
                    {
                        outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos - timeThickness, yPos - voltageThickness, 0.0f), color);
                        outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos + timeThickness, yPos + voltageThickness, 0.0f), color);
                    }
                    else
                    {
                        if ((voltagesX[i] - voltagesX[i - 1]) / (voltagesY[i] - voltagesY[i - 1]) < 0)
                        {
                            outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos - timeThickness, yPos - voltageThickness, 0.0f), color);
                            outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos + timeThickness, yPos + voltageThickness, 0.0f), color);
                        }
                        else
                        {
                            outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos + timeThickness, yPos - voltageThickness, 0.0f), color);
                            outputArray[outputPointer++] = new VertexPositionColor(new Vector3(xPos - timeThickness, yPos + voltageThickness, 0.0f), color);
                        }
                    }
                }
            }

            if (actualThickness == 0)
            {
				if (outputArray.Length > 1)
                	outputArray[outputArray.Length - 1] = outputArray[outputArray.Length - 2];
                primitiveTypeToRender = PrimitiveType.LineStrip;
            }
            else
            {
                primitiveTypeToRender = PrimitiveType.TriangleStrip;
            }

            this.Minimum = min;
            this.ActiveRange = max - Minimum;
            CalculateBoundaries();

            return outputArray;
        }

        protected override void LoadContentInternal()
        {
        }

		protected override void UpdateScaleOffsetMatrix ()
		{
            scaleOffsetLerpMatrix.UpdateTarget(Matrix.CreateTranslation(0.5f, 0.5f, 0) * localWorld);

			foreach (EDrawable child in children) {
				if (child is CursorHorizontal) {
					(child as CursorHorizontal).RecomputePosition ();
				}
			}
		}

        override protected void CalculateBoundaries()
        {
            Vector3 waveAreaTopLeftWorldCoords = new Vector3((float)TimeOffset, this.Minimum + this.ActiveRange, 1);
            Vector3 waveAreaBottomRightWorldCoords = new Vector3((float)(TimeOffset + TimeRange), Minimum, 1);
            Vector3 waveAreaTopLeftScreenCoords = device.Viewport.Project(waveAreaTopLeftWorldCoords, this.Projection, this.View, this.scaleOffsetLerpMatrix.CurrentValue);
            Vector3 waveAreaBottomRightScreenCoords = device.Viewport.Project(waveAreaBottomRightWorldCoords, this.Projection, this.View, this.scaleOffsetLerpMatrix.CurrentValue);

            //extend in case the height would make it untouchable
            //FIXME: make this dependant on screen size! no clue yet how
            int minimalTouchSize = Scaler.MinimalTouchDimension;
            float touchableSizeInPixels = waveAreaBottomRightScreenCoords.Y - waveAreaTopLeftScreenCoords.Y;
            if (touchableSizeInPixels < minimalTouchSize)
            {
                float centerYPos = (waveAreaTopLeftScreenCoords.Y + waveAreaBottomRightScreenCoords.Y) / 2f;
                waveAreaTopLeftScreenCoords.Y = centerYPos - minimalTouchSize / 2;
                waveAreaBottomRightScreenCoords.Y = centerYPos + minimalTouchSize / 2;
            }

            backgroundRectangle = new Rectangle((int)waveAreaTopLeftScreenCoords.X, (int)waveAreaTopLeftScreenCoords.Y, (int)(waveAreaBottomRightScreenCoords.X - waveAreaTopLeftScreenCoords.X), (int)(waveAreaBottomRightScreenCoords.Y - waveAreaTopLeftScreenCoords.Y));
        }

        public static void SetVoltageRange(AnalogChannel ch, double voltageRange)
        {
            if (ch == AnalogChannel.ChA)
                voltageRanges[0] = voltageRange;
            else
                voltageRanges[1] = voltageRange;
        }
	}
}
