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
using ESuite.DataProcessors;
using ESuite.Measurements;

namespace ESuite.Drawables
{
    internal class WaveformMeasurement: Waveform
    {
        const double con_minMeasGraphSpan = 0.000001;
        DateTime latestLTTimestamp;

        string titleText;
        Vector2 titleLocation;
        SpriteFont titleFont;
        MappedColor titleColor;
        VertexBuffer minMaxVertexBuffer;

        public static int SecsPerSample;

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

        public WaveformMeasurement(Graph graph, MeasurementChannel channel)
            : base(graph, channel.Color, PrimitiveType.LineStrip, 2048, channel, null)
		{
            Scale = LinLog.Linear;
            titleText = channel.Measurement.Name;
            titleColor = channel.Color;

            LoadContent();
        }

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {
            throw new NotImplementedException();
        }

        //these values need to be remembered, as they're required when the boundaries are being changed
        private double span;
        private double lowestMin;
        private int nrDataPoints;

        public void UpdateVertices()
        {
            this.redrawRequest = true;

            StochasticMeasurement measurement = (channel as MeasurementChannel).Measurement as StochasticMeasurement;
            if (measurement == null) throw new NotImplementedException(); //non-stochastical measurement not implemented

            if (measurement.LongTermTimestamps.Count < 2) return;

            //don't update when there's no change
            if (measurement.LongTermTimestamps.Last() == latestLTTimestamp) return;
            latestLTTimestamp = measurement.LongTermTimestamps.Last();

            nrDataPoints = measurement.LongTermMeanValues.Count;
            List<VertexPositionColor> vertList = new List<VertexPositionColor>();

            double highestMax = Utils.MaxNanSafe(measurement.LongTermMaxValues);
            lowestMin = Utils.MinNanSafe(measurement.LongTermMinValues);

            //to make graph absolute (meaning: including 0-line)
            //if (highestMax < 0) highestMax = 0;
            //if (lowestMin > 0) lowestMin = 0;

            //do some convertions so hor gridlines map to canonical values
            span = highestMax - lowestMin;
            if (span == 0) span = con_minMeasGraphSpan; //happens when a measurement contains only the same value. In that case, set to some value, as otherwise all division calcs will return NaN; which doesn't make app crash but displays NaN measGraphs
            double gridDivisions = this.Graph.Grid.DivisionVertical.Divisions;
            double VDiv = Utils.getRoundDivisionRange(span / gridDivisions, Utils.RoundDirection.Up);
            span = VDiv * gridDivisions;

            //in case of negative values: make sure bottom horline is a canonical value
            if (lowestMin > 0)
                lowestMin = Utils.roundToPreviousMultiple(lowestMin, span / gridDivisions);
            else
                lowestMin = Utils.roundToNextMultiple(lowestMin, span / gridDivisions);
            //but this can cause the max value to go above the max grid value. in this case -> one more iteration
            if (highestMax > lowestMin + span)
            {
                span = highestMax - lowestMin;
                gridDivisions = this.Graph.Grid.DivisionVertical.Divisions;
                VDiv = Utils.getRoundDivisionRange(span / gridDivisions, Utils.RoundDirection.Up);
                span = VDiv * gridDivisions;
                if (lowestMin > 0)
                    lowestMin = Utils.roundToPreviousMultiple(lowestMin, span / gridDivisions);
                else
                    lowestMin = Utils.roundToNextMultiple(lowestMin, span / gridDivisions);
            }

            //if resulting span is different from grid span: update grid span
            double vertOffset = lowestMin + span / 2.0;
            //see whether a specific unit should be used on the vertical axis of the grid
            AnalogChannel correspondingAnalogChannel = null;
            if ((measurement is ChannelMeasurement) && !(measurement as ChannelMeasurement).HasDedicatedUnit)
                correspondingAnalogChannel = ((measurement as ChannelMeasurement).Channel as AnalogChannel);
            if ((span != this.Graph.Grid.DivisionVertical.FullRange) || (vertOffset != this.Graph.Grid.DivisionVertical.Offset) || nrDataPoints != this.Graph.Grid.DivisionHorizontal.FullRange)
                this.Graph.Grid.UpdateScalersOffsets(nrDataPoints*SecsPerSample, (double)nrDataPoints*SecsPerSample/2.0, span, LinLog.Linear, vertOffset, correspondingAnalogChannel);

            UpdateScaleOffsetMatrixForMeasurementWave();

            float onePixelHeight = 1.0f / (float)parent.Boundaries.Height * (float)span;
            float onePixelWidth = 1.0f / (float)parent.Boundaries.Width * (float)(nrDataPoints - 1);
            float voltageThickness = onePixelHeight / 2f * (Waveform.Thickness + 1f);
            float timeThickness = onePixelWidth / 2f * (Waveform.Thickness + 1f);

            float actualThickness = Waveform.Thickness;
            Color color = this.GraphColor.C();

            List<VertexPositionColor> minMaxVertList = new List<VertexPositionColor>();

            int i = 0;
            double prevMean = double.NegativeInfinity;
            foreach (double mean in measurement.LongTermMeanValues)
            {
                //first sample hack, so we can foreach over Queue which is preferred iteration method for a Queue
                if (prevMean == double.NegativeInfinity) prevMean = mean;

                float yPos = (float)mean;// 1f -(float)((measurement.LongTermMeanValues[i] - min) / span);
                float xPos = (float)i;// / (float)(nrDataPoints-1);                

                if (actualThickness == 0)
                {
                    vertList.Add(new VertexPositionColor(new Vector3(xPos, yPos, 0.0f), Color.Red));
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
                        if (mean < prevMean)
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

                //minmax background
                float maxYPos = (float)measurement.LongTermMaxValues.ElementAt(i);
                float minYPos = (float)measurement.LongTermMinValues.ElementAt(i);
                minMaxVertList.Add(new VertexPositionColor(new Vector3(xPos, minYPos, 0.0f), color*0.2f));
                minMaxVertList.Add(new VertexPositionColor(new Vector3(xPos, maxYPos, 0.0f), color * 0.2f));

                i++;
            }

            VertexPositionColor[] vertexArray = vertList.ToArray();
            if (actualThickness == 0)
                primitiveTypeToRender = PrimitiveType.LineStrip;
            else
                primitiveTypeToRender = PrimitiveType.TriangleStrip;

            this.Minimum = (float)lowestMin;
            this.ActiveRange = (float)span;

            ////////////////////////////////////////////////////////
            //convert to suitable vertexbuffer

            //reset vertbuffer if length has changed
            if (vertexBuffer != null && vertexBuffer.VertexCount != vertexArray.Length)
            {
                vertexBuffer.Dispose();
                vertexBuffer = null;
            }

            //if needed: create new vertbuffer
            if (vertexBuffer == null)
                vertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), vertexArray.Length, BufferUsage.WriteOnly);

            //copy data to GPU
            vertexBuffer.SetData(vertexArray);

            //now do the same for minmaxVertexBuffer
            if (minMaxVertexBuffer != null && minMaxVertexBuffer.VertexCount != minMaxVertList.Count)
            {
                minMaxVertexBuffer.Dispose();
                minMaxVertexBuffer = null;
            }
            if (minMaxVertexBuffer == null)
                minMaxVertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), minMaxVertList.Count, BufferUsage.WriteOnly);
            minMaxVertexBuffer.SetData(minMaxVertList.ToArray());
        }

        private void UpdateScaleOffsetMatrixForMeasurementWave()
        {
            //set matrix. this way we don't need to scale X and Y below --> the GPU will do this for us
            Matrix targetScaleOffsetMatrix = CalcScaleOffsetMatrix((float)span, (float)(-lowestMin - span / 2.0), (float)(nrDataPoints - 1), 0f);
            scaleOffsetLerpMatrix.UpdateTarget(targetScaleOffsetMatrix);
        }

        protected override void LoadContentInternal()
        {
            titleFont = content.Load<SpriteFont>(Scaler.GetFontMeasurementGraphTitle());
        }

        public CursorHorizontal AddCursor (Vector2 location, double precision)
		{
            /*CursorHorizontal cursor = new CursorHorizontal(((Graph)this.parent).Grid, this, location, precision);
			AddChild (cursor);

            CheckToAddDeltaCursor();
            */
			return null;
		}

        protected override void OnBoundariesChangedInternal()
        {
            base.OnBoundariesChangedInternal();

            int margin = Scaler.InchesToPixels(Scaler.MeasurementGraphTitleMargin);
            Vector2 textSize = titleFont.MeasureString(titleText);
            titleLocation = new Vector2((int)(Boundaries.Center.X - textSize.X/2f), (int)(margin + Boundaries.Y));
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw minmax background
            if (minMaxVertexBuffer != null && minMaxVertexBuffer.VertexCount > 2)
            {
                effect.World = scaleOffsetLerpMatrix.CurrentValue;// this.currentScaleOffsetMatrix;//this one is already containing localWorld
                effect.View = this.View;
                effect.Projection = this.Projection;
                device.RasterizerState = RasterizerState.CullNone;
                device.SetVertexBuffer(minMaxVertexBuffer);

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    effect.CurrentTechnique.Passes[0].Apply();
                    device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, minMaxVertexBuffer.VertexCount - 2);
                }
            }

            base.DrawInternal(time);

            spriteBatch.Begin();
            spriteBatch.DrawString(titleFont, titleText, titleLocation, titleColor.C());
            spriteBatch.End();
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
            UpdateScaleOffsetMatrixForMeasurementWave();

			foreach (EDrawable child in children) {
				if (child is CursorHorizontal) {
					(child as CursorHorizontal).RecomputePosition ();
				}
			}
		}
	}
}
