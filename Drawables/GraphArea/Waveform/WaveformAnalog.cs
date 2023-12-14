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
    internal class WaveformAnalog : Waveform
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

        public WaveformAnalog(Graph graph, MappedColor graphColor, Channel channel, GridDivisionLabel label, WaveformAnalog panoramaWaveParent = null)
            : base(graph, graphColor, PrimitiveType.LineStrip, 2048, channel, panoramaWaveParent)
		{
            scale = LinLog.Linear;
            this.label = label;

            LoadContent();
        }

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {


            if (dataBuffers == null) return null;
            if (dataBuffers.Length < 1) return null;
            if (dataBuffers[0] == null) return null;
            WaveDataBuffer dataBuffer = dataBuffers[0];

            //next: find indices in buffer, corresponding to cropStartTime and cropStopTime
            int bufferDrawStartIndex = FindBufferIndexByTimeSafeLeft(dataBuffer.SubsampledTimeData, (float)visibleStartTime);
            int bufferDrawStopIndex = FindBufferIndexByTimeSafeRight(dataBuffer.SubsampledTimeData, (float)(visibleEndTime));

            float[] voltages = (float[])dataBuffer.SubsampledData;
            float[] timeData = dataBuffer.SubsampledTimeData;

            //crashprotection https://www.lab-nation.com/forum/software/topics/a-few-problems-report
            if (voltages.Length < bufferDrawStopIndex)
            {
                LabNation.Common.Logger.Error("Crash avoided: voltages " + voltages.Length.ToString() + "< bufferDrawStopIndex " + bufferDrawStopIndex.ToString());
                return null;
            }
            if (timeData.Length < bufferDrawStopIndex)
            {
                LabNation.Common.Logger.Error("Crash avoided: timeData " + timeData.Length.ToString() + "< bufferDrawStopIndex " + bufferDrawStopIndex.ToString());
                return null;
            }
            if (bufferDrawStartIndex > bufferDrawStopIndex)
            {
                LabNation.Common.Logger.Error("Crash avoided: bufferDrawStartIndex " + bufferDrawStartIndex.ToString() + "> bufferDrawStopIndex " + bufferDrawStopIndex.ToString());
                return null;
            }

            //TO_ADD: use array instead of list (performance)
            VertexPositionColor[] outputArray;
            float actualThickness = PanoramaWave == null ? 0 : Waveform.Thickness;
            if (actualThickness == 0)
                outputArray = new VertexPositionColor[bufferDrawStopIndex - bufferDrawStartIndex];
            else
                outputArray = new VertexPositionColor[(bufferDrawStopIndex - bufferDrawStartIndex)*2];

            float max = float.NegativeInfinity;
            float min = float.PositiveInfinity;
#if DEBUG &&  false
            for (int i = 0; i < vertices; i++)
            {
                double offset = i / (double)vertices; //relative offset from left edge
                double t = timeRange * offset;

                float xPos;
                if (scale == LinLog.Logarithmic)
                    xPos = (float)Math.Max(0, Math.Log10(i / samplingPeriod / verticesToRenderFrom / 2));
                else
                    xPos = (float)(t + firstSampleTime);

                float yPos = voltages[(int)Math.Floor((xPos - firstSampleTime) / samplingPeriod)];
                //float yPos = Utils.SincReconstruct((float)t, (float)samplingPeriod, voltages);
                if (yPos < min) min = yPos;
                if (yPos > max) max = yPos;

                vertexList[i] = new VertexPositionColor(new Vector3(xPos, yPos, 0.0f), blendColor);
            }
#else
            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)VoltageRange; //VoltageRange represents full vertical size
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)TimeRange; //TimeRange represents full vertical size
            float voltageThickness = onePixelHeight / 2f * (Waveform.Thickness + 1f);
            float timeThickness = onePixelWidth / 2f * (Waveform.Thickness + 1f);

            Color color = GraphColor.C();
            int outputPointer = 0;

            for (int i = bufferDrawStartIndex; i < bufferDrawStopIndex; i++)
            {
                float yPos = voltages[i];
                if (yPos < min) min = yPos;
                if (yPos > max) max = yPos;
                float xPos;
                if (scale == LinLog.Logarithmic)
                {
                    xPos = (float)Math.Max(0, Math.Log10(i / dataBuffer.SubsampledSamplePeriod / voltages.Length / 2));
                }
                else
                    xPos = timeData[i];

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
                        if (voltages[i] < voltages[i - 1])
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
#endif
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

            if (isPanoramaWave && this is WaveformAnalog)
                UpdateScaleOffsetMatrix(); //adjust to make sure wave spans entire panorama (and is never going out of it) FIXME: better to do this at end of UpdateVertices method for panorama waves ### 20161027: which is here

            CalculateBoundaries();

            return outputArray;
        }

        protected override void LoadContentInternal()
        {
        }

        public CursorHorizontal AddCursor (Vector2 location, double precision)
		{
            CursorHorizontal cursor = new CursorHorizontal(((Graph)this.parent).Grid, this, location, precision);
			AddChild (cursor);

            CheckToAddDeltaCursor();

			return cursor;
		}

        public void CheckToAddDeltaCursor()
        {
            /* if more than 1: display delta measurment */
            List<CursorHorizontal> horCursorList = children.Where(x => x is CursorHorizontal).ToList().Cast<CursorHorizontal>().ToList();
            if (horCursorList.Count > 1)
            {
                //first remove possibly existing one
                RemoveHorizontalDeltaCursor();

                //define and add deltacursor
                CursorHorizontal parent1 = horCursorList[horCursorList.Count - 1];
                CursorHorizontal parent2 = horCursorList[horCursorList.Count - 2];
                CursorHorizontalDelta newDeltaCursor = new CursorHorizontalDelta(((GraphManager)this.parent.parent.parent).Graphs[GraphType.Analog].Grid, this, (parent1.Location + parent2.Location) / 2f, parent1.Precision, parent1, parent2);
                AddChild(newDeltaCursor);
            }
        }

        public void CycleHorizontalDeltaCursor(CursorHorizontal parent1)
        {
            //first make sure the given cursor is positioned as 'most recent'
            RemoveChild(parent1);
            AddChild(parent1);

            //now make delta cursor between 2 most recent cursors
            CheckToAddDeltaCursor();
        }

        public void RemoveHorizontalDeltaCursor()
        {
			CursorHorizontalDelta deltaCursor = (CursorHorizontalDelta)children.FirstOrDefault(x => x is CursorHorizontalDelta);
            if (deltaCursor != null)
            {
                deltaCursor.Unlink();
                RemoveChild(deltaCursor);
            }
        }

		public void RemoveCursor (Cursor cursor)
		{
            bool removedDeltaCursor = false;

            //first check whether a deltaindicator was depending on this one
            CursorHorizontalDelta deltaCursor = (CursorHorizontalDelta)children.FirstOrDefault(x => x is CursorHorizontalDelta);
            if (deltaCursor != null)
            {
                if ((deltaCursor.ParentCursor1 == cursor) || (deltaCursor.ParentCursor2 == cursor))
                {
                    deltaCursor.Unlink();
                    RemoveChild(deltaCursor);
                    removedDeltaCursor = true;
                }
            }

            RemoveChild(cursor);

            if (removedDeltaCursor)
                CheckToAddDeltaCursor();
		}

		protected override void UpdateScaleOffsetMatrix ()
		{
			base.UpdateScaleOffsetMatrix ();

            CalculateBoundaries();

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
	}
}
