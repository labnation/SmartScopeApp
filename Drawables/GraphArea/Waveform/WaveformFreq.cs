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
    internal class WaveformFreq: Waveform
    {
        new static public Dictionary<Channel, WaveformFreq> Waveforms
        {
            get
            {
                return Waveform.Waveforms.
                    Where(x => x.Value is WaveformFreq).
                    ToDictionary(x => x.Key, x => (WaveformFreq)x.Value);
            }
        }
        public override bool Enabled
        {
            get { return base.Enabled; }
            set
            {
                base.Enabled = value;
            }
        }

        private float sampleFrequency = 0;
        public float SampleFrequency {
            get { return sampleFrequency; }
            set
            {
                if (sampleFrequency != value)
                {
                    sampleFrequency = value;
                    RebuildVertexBuffer();
                }
                else
                {
                    sampleFrequency = value;
                }
            }
        }

        private LinLog scale;
        public LinLog Scale
        {
            get { return scale; }
            set
            {
                if (scale != value)
                {
                    scale = value;
                    RebuildVertexBuffer();
                }
                else
                {
                    scale = value;
                }
            }
        }

        public WaveformFreq(Graph graph, MappedColor graphColor, Channel channel)
            : base(graph, graphColor, PrimitiveType.LineStrip, 2048, channel, null)
		{
            Scale = LinLog.Logarithmic;

            LoadContent();
        }

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {
            if (dataBuffers == null) return null;
            if (dataBuffers.Length < 1) return null;
            if (dataBuffers[0] == null) return null;
            WaveDataBuffer dataBuffer = dataBuffers[0];
            if (dataBuffer.SubsampledData.Length == 0) //in case analogwave is clipped, FFT will return empty array
                return null;

            float[] voltages = (float[])dataBuffer.SubsampledData;
			List<VertexPositionColor> vertList = new List<VertexPositionColor>();

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
            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)1; //freqwave uses identity matrix as worldmatrix -> 1
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)1; //freqwave uses identity matrix as worldmatrix -> 1
            float voltageThickness = onePixelHeight /2f * (Waveform.Thickness+1f);
            float timeThickness = onePixelWidth / 2f * (Waveform.Thickness+1f);

            Color color = GraphColor.C();
            float actualThickness = Waveform.Thickness;

            GridFrequency grid = Graph.Grid as GridFrequency;
            float endFreq = SampleFrequency / 2f;
            float logStartFreq = grid.LeftLimitFreq;
            float freqOffet = (float)Math.Log10(logStartFreq);
            float scaler = (float)(Math.Log10(grid.RightLimitFreq) - Math.Log10(grid.LeftLimitFreq));            
            float gridScreenFreqSpan = grid.RightLimitFreq - grid.LeftLimitFreq;

            int startIndex = (int)((float)grid.LeftLimitFreq / (float)grid.NyquistFrequency * (float)voltages.Length);
            int endIndex = (int)((float)grid.RightLimitFreq / (float)grid.NyquistFrequency * (float)voltages.Length);

            for (int i = startIndex; i < endIndex; i++)
            {
                float yPos = 1f-voltages[i];
				if(yPos < min) min = yPos;
				if(yPos > max) max = yPos;
                float xPos;
                if (scale == LinLog.Logarithmic)
                {
                    float currFreq = ((float)i / (float)voltages.Length) * grid.NyquistFrequency;
                    xPos = (float)Math.Max(0, (Math.Log10(currFreq) - freqOffet) / scaler);
                }
                else
                {
                    float currFreq = ((float)i / (float)voltages.Length) * grid.NyquistFrequency;
                    xPos = (currFreq - grid.LeftLimitFreq) / gridScreenFreqSpan;
                }

                if (actualThickness == 0)
                {                    
                    vertList.Add(new VertexPositionColor(new Vector3(xPos, yPos, 0.0f), color));
                }
                else
                {
                    if (i == 0)
                    {
                        vertList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, yPos - voltageThickness, 0.0f), color));
                        vertList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, yPos + voltageThickness, 0.0f), color));
                    }
                    else
                    {
                        if (voltages[i] < voltages[i - 1])
                        {
                            vertList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, yPos - voltageThickness, 0.0f), color));
                            vertList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, yPos + voltageThickness, 0.0f), color));
                        }
                        else
                        {
                            vertList.Add(new VertexPositionColor(new Vector3(xPos + timeThickness, yPos - voltageThickness, 0.0f), color));
                            vertList.Add(new VertexPositionColor(new Vector3(xPos - timeThickness, yPos + voltageThickness, 0.0f), color));
                        }
                    }
                }
            }
#endif
            if (vertList.Count == 0)
                return null;

            VertexPositionColor[] vertexArray = vertList.ToArray();
            if (actualThickness == 0)
            {
                vertexArray[vertexArray.Length - 1] = vertexArray[vertexArray.Length - 2];
                primitiveTypeToRender = PrimitiveType.LineStrip;
            }
            else
            {
                primitiveTypeToRender = PrimitiveType.TriangleStrip;
            }            

			this.Minimum = min;
			this.ActiveRange = max - Minimum;

            return vertexArray;
		}

        protected override void LoadContentInternal()
        {
        }

        public CursorHorizontal AddCursor (Vector2 location, double precision)
		{
            /*CursorHorizontal cursor = new CursorHorizontal(((Graph)this.parent).Grid, this, location, precision);
			AddChild (cursor);

            CheckToAddDeltaCursor();
            */
			return null;
		}

        protected override void CalculateBoundaries()
        {
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
                //CursorHorizontalDelta newDeltaCursor = new CursorHorizontalDelta(((GraphManager)this.parent.parent).Graphs[GraphType.Analog].Grid, this, (parent1.Location + parent2.Location) / 2f, parent1.Precision, parent1, parent2);
                //AddChild(newDeltaCursor);
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

        public void SetHorizontalLogarithmic(double freqRange)
        {
            base.TimeRange = Math.Log10(freqRange);
        }

		protected override void UpdateScaleOffsetMatrix ()
		{
            scaleOffsetLerpMatrix.UpdateTarget(localWorld);

			foreach (EDrawable child in children) {
				if (child is CursorHorizontal) {
					(child as CursorHorizontal).RecomputePosition ();
				}
			}
		}
	}
}
