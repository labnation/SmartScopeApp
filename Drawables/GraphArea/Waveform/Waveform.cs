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
using LabNation.Interfaces;

namespace ESuite.Drawables
{
    abstract internal class Waveform : EDrawableVertices
    {
        protected double latestTimeRange;
        protected double latestVoltageRange;
        public static float Thickness = 0f;
        public Graph Graph { get; private set; }
        private List<WaveDataBuffer[]> waveDataBuffers;
        private ChannelData[] latestChannelData;
        protected int samplesToDrawOnScreen;
        public static Dictionary<Channel, Waveform> Waveforms = new Dictionary<Channel, Waveform>();
        public static Dictionary<Channel, Waveform> EnabledWaveforms
        {
            get
            {
                return Waveforms.Where(x => x.Value.Enabled && !(x.Key is ESuite.DataProcessors.MeasurementChannel)).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        public static Dictionary<Channel, Waveform> EnabledWaveformsVisible
        {
            get
            {
                return Waveforms.Where(x => x.Value.Enabled && x.Value.Visible && !(x.Key is ESuite.DataProcessors.MeasurementChannel)).ToDictionary(x => x.Key, x => x.Value);
            }
        }

        protected Channel channel;
        public Channel Channel { get { return this.channel; } }
        protected VertexBuffer vertexBuffer;
        
        protected LerpMatrix scaleOffsetLerpMatrix = new LerpMatrix(Matrix.Identity, Matrix.Identity, 0f);

        public Waveform PanoramaWave { get; private set; }
        public bool RedrawRequest { get { return this.redrawRequest; } set { this.redrawRequest = value; } }
        public Waveform PanoramaWaveParent { get; private set; }
        protected Rectangle backgroundRectangle = new Rectangle();
        public Rectangle BackgroundRectangle { get { return this.backgroundRectangle; } }
        protected bool isPanoramaWave = false;
        abstract protected void CalculateBoundaries();

        private double timeOffset;
        public virtual double TimeOffset
        {
            get { return timeOffset; }
            set
            {
                double oldValue = this.timeOffset;
                timeOffset = value;

                if (oldValue != value)
                {
                    RebuildVertexBuffer();
                    UpdateScaleOffsetMatrix();
                }
            }
        }

        protected double timeRange;
        virtual public double TimeRange
        {
            get { return timeRange; }
            set 
            {
                double oldValue = this.timeRange;
                this.timeRange = value;

                if (oldValue != value)
                {
                    RebuildVertexBuffer();
                    UpdateScaleOffsetMatrix();
                }
            }
        }

        protected double voltageRange;
        virtual public double VoltageRange { 
            get { return voltageRange; }
            set {
                //FIXME: be intelligen
                voltageRange = Math.Max(0.02, value); 
                UpdateScaleOffsetMatrix();

                if (PanoramaWave != null)
                    PanoramaWave.VoltageRange = value;
            }
        }
        public new bool Visible
        {
            get { return base.Visible; }
            set
            {
                base.Visible = value;
                if (PanoramaWave != null)
                    PanoramaWave.Visible = this.Visible;
                if (this is WaveformAnalog)
                    if (this.OffsetIndicator != null)
                        this.OffsetIndicator.Visible = value;
                if (this is WaveformDigital)
                {
                    if ((this as WaveformDigital).OffsetIndicator != null)
                        (this as WaveformDigital).OffsetIndicator.Visible = value;                    
                    if ((this as WaveformDigital).TriggerIndicator != null)
                        (this as WaveformDigital).TriggerIndicator.Visible = value;
                }
            }
        }
        //The height of the wave in volts as it was last drawn
        public float ActiveRange { get; protected set; }
        public float Minimum { get; protected set; }

        protected float voltageOffset;
        public float VoltageOffset { 
            get { return voltageOffset; }
            set {
                if (float.IsNaN(value))
                    voltageOffset = 0f;
                else
                    voltageOffset = value;
                //this.OffsetIndicator.Value = value; 
                UpdateScaleOffsetMatrix(); 
            }
        }

        private bool enabled = false;
        public virtual bool Enabled
        {
            get { return enabled; }
            set
            {
                this.Visible = value;
                if (this.OffsetIndicator != null) //null for fft waves! 
                    this.OffsetIndicator.Visible = value;
                if (this.channel is DigitalChannel)
                    (this as WaveformDigital).TriggerIndicator.Visible = value;
                enabled = value;
            }
        }

        public bool ShowBackground { get; set; }

        protected PrimitiveType primitiveTypeToRender;

        public IndicatorInteractive OffsetIndicator;
        public Matrix ScaleOffsetMatrix { get { return this.scaleOffsetLerpMatrix.CurrentValue; } }

        private MappedColor _graphColor;
        public MappedColor GraphColor
        {
            get { return _graphColor; }
            set
            {
                _graphColor = value;
                RebuildVertexBuffer();
            }
        }

        public Waveform(Graph graph, MappedColor graphColor, PrimitiveType primitiveTypeToRender, int samplesToDrawOnScreen, Channel channel, Waveform panoramaWaveParent = null)
            : base()
        {
            this.Graph = graph;
            this.GraphColor = graphColor;
            this.primitiveTypeToRender = primitiveTypeToRender;
            this.samplesToDrawOnScreen = samplesToDrawOnScreen;

            this.voltageRange = 10;
            this.voltageOffset = 0;
            this.timeRange = 20f/ 1000000f;
            this.ShowBackground = false;
            this.channel = channel;

            if (panoramaWaveParent == null)
            {
                isPanoramaWave = false;
                Waveforms.Add(channel, this);
            }
            else
            {
                isPanoramaWave = true;
                this.PanoramaWaveParent = panoramaWaveParent;
                panoramaWaveParent.PanoramaWave = this;
            }

            if (graph != null)
            {
                this.Boundaries = graph.Boundaries;
                this.MustRecalcNextUpdateCycle = true;
            }
        }
        public static void ActivateWaveformOffsetSelector(Channel channel, bool activate)
        {
            Waveform w = Waveforms[channel];
            //must remove both the waveform and its selector
            if (w != null)
                w.OffsetIndicator.Selected = activate;
        }
		
		public static List<Channel> ChannelsAt (Point location)
		{
			return ChannelsWithin(new Rectangle(location.X, location.Y, 1, 1));
		}

		public static List<Channel> ChannelsWithin(Rectangle location, float minimalVerticalOverlapPercentage = 0)
        {
            if (location.Height == 0)
            {
                LabNation.Common.Logger.Warn("Intersecting a zero height rectangle with waves is not going to result in much");
                return new List<Channel>();
            }

			List<KeyValuePair<Channel, float>> wavesIntersectingRectangle = new List<KeyValuePair<Channel, float>>();
            foreach (KeyValuePair<Channel, Waveform> kvp in Waveform.EnabledWaveformsVisible)
            {
            	Rectangle overlap = Rectangle.Intersect(location, kvp.Value.BackgroundRectangle);
            	float verticalOverlap = (float)overlap.Height / kvp.Value.BackgroundRectangle.Height;
				if(verticalOverlap > minimalVerticalOverlapPercentage || overlap.Height == location.Height)
					wavesIntersectingRectangle.Add(new KeyValuePair<Channel, float>(kvp.Key, verticalOverlap));
            }

			//Order list by distance from rectangle center
			wavesIntersectingRectangle.Sort((x, y) => { return x.Value.CompareTo(y.Value); });
            return wavesIntersectingRectangle.Select(x => x.Key).ToList();
        }

        public void UpdateData(ChannelData[] newData, ChannelData timeData = null)
        {
            if (newData == null) return;
            if (newData.Length == 0) return;
            if (newData[0] == null) return;
            if (newData[0].array == null) return;

            //check whether data is new. The object passed is simply a wrapper, made each call. So we need to compare the internal DataPackage objects
            bool identical = false;
            if (latestChannelData != null && newData.Length == latestChannelData.Length)
            {
                identical = true;
                for (int i = 0; i < newData.Length; i++)
                    if (newData[i] != latestChannelData[i])
                        identical = false;
            }
            if (identical) return;

            this.latestChannelData = newData;            

              //for each channel that has provided data: create databuffers
            waveDataBuffers = new List<WaveDataBuffer[]>();
            foreach (var data in newData)
                waveDataBuffers.Add(ConvertRawDataToDataBuffers(data, timeData));

            RebuildVertexBuffer();
        }

        /// <summary>
        /// Converts ChannelData into array of WaveDataBuffers. 2K and ETS packages result in 1 WaveDataBuffer, FullAcq packages in more
        /// </summary>
        /// <param name="rawData"></param>
        /// <returns></returns>
        private WaveDataBuffer[] ConvertRawDataToDataBuffers(ChannelData rawData, ChannelData timeData = null)
        {
            if (rawData == null) 
                return null;

            if (timeData == null) //all samples are equally spaced in time == non-ETS data
            {
                if (rawData.array.Length < samplesToDrawOnScreen*2)//small dataset -> no need to divide into multiple buffers
                {
                    return new WaveDataBuffer[1] { new WaveDataBufferRegular(rawData.array, rawData.timeOffset, rawData.samplePeriod, rawData.timeOffset, 1) }; //1 indicates no decimation
                }
                else // full acquisition! needs to be decomposed into multiple buffers
                {
                    //FIXME: probably want to do this in the Waveform subclass
                    double rawTimeSpan = (double)rawData.array.Length * rawData.samplePeriod;
                    double rawSamplePeriod = rawData.samplePeriod;

                    int smallestBufferLength = samplesToDrawOnScreen;
                    double rawSampleFreq = 1 / rawSamplePeriod;
                    double minSampleFreq = 1 / (rawTimeSpan / (double)smallestBufferLength);
                    int nrBuffers = (int)Math.Ceiling(Math.Log(rawSampleFreq, 2)) - (int)Math.Floor(Math.Log(minSampleFreq, 2)) + 1;

                    //allocate buffers and calculate their binlength
                    List<int> binLengths = new List<int>();
                    for (int i = 0; i < nrBuffers; i++)
                        binLengths.Add((int)Math.Pow(2, i));

                    //create WaveDataBuffer from arrays
                    WaveDataBuffer[] output = new WaveDataBuffer[nrBuffers];
                    for (int i = 0; i < nrBuffers; i++)
                        output[i] = new WaveDataBufferRegular(rawData.array, rawData.timeOffset, rawSamplePeriod, rawData.timeOffset + rawSamplePeriod * (double)binLengths[i] / 2.0 /*move startpoint to center of bin*/, binLengths[i]);

                    return output;
                }
            }
            else //ETS data. small dataset -> no need to divide into multiple buffers
            {
                WaveDataBuffer dataBuffer = new WaveDataBufferETS((float[])rawData.array, rawData.timeOffset, rawData.samplePeriod, (float[])timeData.array);
                return new WaveDataBuffer[1] { dataBuffer };
            }
        }

        public void RebuildVertexBuffer()
        {
            if (waveDataBuffers == null) return;

            //select buffer which corresponds best to required samplePeriod
            List<WaveDataBuffer> optimalBuffers = new List<WaveDataBuffer>();
            foreach (var channelBuffers in waveDataBuffers)
                optimalBuffers.Add(SelectOptimalWaveDataBuffer(channelBuffers, TimeRange / (double)samplesToDrawOnScreen));

            if (optimalBuffers.Count == 0) return;

            //convert subset of buffer into array of vertices
            VertexPositionColor[] optimalVertices = CreateVertices(optimalBuffers.ToArray(), TimeOffset, (float)(TimeOffset + TimeRange));

            if (vertexBuffer != null && optimalVertices != null && vertexBuffer.VertexCount != optimalVertices.Length)
            {
                vertexBuffer.Dispose();
                vertexBuffer = null;
            }
            if (optimalVertices != null && optimalVertices.Length > 0)
            {
                if(vertexBuffer == null)
                    vertexBuffer = new VertexBuffer(device, typeof(VertexPositionColor), optimalVertices.Length, BufferUsage.WriteOnly);
                vertexBuffer.SetData(optimalVertices);
            }

            this.redrawRequest = true;
        }

        private WaveDataBuffer SelectOptimalWaveDataBuffer(WaveDataBuffer[] buffers, double requiredSamplePeriod)
        {
            if (buffers == null || buffers.Length == 0) return null;
            if (buffers.Length == 1) return buffers[0];

            int optimalBuffer = 0;
            double diff = double.MaxValue;
            for (int i = 0; i < buffers.Length; i++)
            {
                double currentDifference = requiredSamplePeriod - buffers[i].SubsampledSamplePeriod;
                if (currentDifference >= 0 && currentDifference < diff)
                {
                    diff = currentDifference;
                    optimalBuffer = i;
                }
            }

            debugOptimalBufferID = optimalBuffer; //variable for printing decimation debuglines.. FIXME: should be depending on global compile flag
            return buffers[optimalBuffer];
        }
        private int debugOptimalBufferID = 0;

        protected abstract VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime);

        protected override void DrawInternal(GameTime time)
        {            
            //safety checks
            if (vertexBuffer == null) return;
            int nrVerticesToRenderFrom = vertexBuffer.VertexCount;

#if false
            //WaveDataBuffer decimation debugtexts
            ScopeApp.DebugTextList.Add(this.ToString() + " numberOfBuffers: " + waveDataBuffers.Length.ToString());
            ScopeApp.DebugTextList.Add(this.ToString() + " optimalBufferID: " + debugOptimalBufferID.ToString());
            ScopeApp.DebugTextList.Add(this.ToString() + " vertexbufferSize: " + nrVerticesToRenderFrom.ToString());
            ScopeApp.DebugTextList.Add("---------");
#endif

            if (this.primitiveTypeToRender == PrimitiveType.LineList && nrVerticesToRenderFrom < 2) return;
            else if (this.primitiveTypeToRender == PrimitiveType.LineStrip && nrVerticesToRenderFrom < 2) return;
            else if (this.primitiveTypeToRender == PrimitiveType.TriangleList && nrVerticesToRenderFrom < 3) return;
            else if (this.primitiveTypeToRender == PrimitiveType.TriangleStrip && nrVerticesToRenderFrom < 3) return;                        

            if (ShowBackground)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                Color areaColor = GraphColor.C();
                areaColor *= 0.1f; //this sets the transparency for premultiplied colors
                spriteBatch.Draw(whiteTexture, backgroundRectangle, areaColor);
                spriteBatch.End();
            }            
          
            effect.World = scaleOffsetLerpMatrix.CurrentValue;// this.currentScaleOffsetMatrix;//this one is already containing localWorld
            effect.View = this.View;
            effect.Projection = this.Projection;

            device.RasterizerState = RasterizerState.CullNone;
            device.SetVertexBuffer(vertexBuffer);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                if (this.primitiveTypeToRender == PrimitiveType.LineList)
                    device.DrawPrimitives(this.primitiveTypeToRender, 0, nrVerticesToRenderFrom / 2);
                else if (this.primitiveTypeToRender == PrimitiveType.LineStrip)
                    device.DrawPrimitives(this.primitiveTypeToRender, 0, nrVerticesToRenderFrom - 1);
                else if (this.primitiveTypeToRender == PrimitiveType.TriangleList)
                    device.DrawPrimitives(this.primitiveTypeToRender, 0, nrVerticesToRenderFrom / 3);
                else if (this.primitiveTypeToRender == PrimitiveType.TriangleStrip)
                    device.DrawPrimitives(this.primitiveTypeToRender, 0, nrVerticesToRenderFrom - 2);
            }                        
        }

        virtual protected void UpdateScaleOffsetMatrix()
        {
            this.redrawRequest = true;
            if (isPanoramaWave)
            {
                float fullTimeSpan;
                float range;
                float offset;

                if (this is WaveformAnalog)
                {
                    fullTimeSpan = (float)timeRange;
                    range = ActiveRange;
                    offset = (2f * Minimum + ActiveRange) / 2.0f;

                    float minimalVoltageSpan = (float)(VoltageRange * 0.2);
                    if (range < minimalVoltageSpan)
                        range = minimalVoltageSpan;

                }
                else if ((this is WaveformDigital) || (this is WaveformDecoded))
                {
                    fullTimeSpan = (float)timeRange;
                    int channelCount = DigitalChannel.List.Count;
                    range = (float)VoltageRange;
                    offset = ((float)this.channel.Value - (float)(channelCount - 1) / 2f) / (float)channelCount;
                }
                else
                {
                    throw new Exception("Uh ooh - panorama of unhandled wave");
                }

                Matrix newMat = CalcScaleOffsetMatrix(range * 1.1f, -offset, fullTimeSpan, (float)timeOffset);
                scaleOffsetLerpMatrix.UpdateTarget(newMat);
            }
            else
            {
                Matrix prevValue = scaleOffsetLerpMatrix.target;
                Matrix targetScaleOffsetMatrix = CalcScaleOffsetMatrix((float)voltageRange, voltageOffset, (float)timeRange, (float)timeOffset);
                
                //if there was no change in matrix: no need to run further updates
                if (targetScaleOffsetMatrix == prevValue) return;

                scaleOffsetLerpMatrix.UpdateTarget(targetScaleOffsetMatrix);

                float probeOffset = 0;
                if (channel is AnalogChannel)
                    probeOffset = (channel as AnalogChannel).Probe.Offset;


                if (OffsetIndicator != null)
                    OffsetIndicator.Position = (voltageOffset-probeOffset) / (float)voltageRange;

                CalculateBoundaries();
            }

            //redefine vertices, needed because vertices of thick waves need to know the new XAxis and YAxis
            //but do this only when the new X and Y scales have sufficiently changed! otherwise pinching will drain the CPU in case 4M samples need to be processed on each gesture...
            if ((Math.Abs(latestTimeRange - TimeRange) > latestTimeRange * 0.25) || (Math.Abs(latestVoltageRange - VoltageRange) > latestVoltageRange* 0.25))
            {
                latestTimeRange = TimeRange;
                latestVoltageRange = VoltageRange;

                if (Waveform.Thickness > 0)
                    RebuildVertexBuffer();
            }
        }

        protected Matrix CalcScaleOffsetMatrix(float voltageRange, float voltageOffset, float timeRange, float timeOffset)
        {
            return Matrix.CreateTranslation(-timeRange/2f - timeOffset, voltageOffset, 0) * 
                    Matrix.CreateScale(1f / timeRange, -1f / voltageRange, 1) * 
                    Matrix.CreateTranslation(0.5f, 0.5f, 0) *
                    localWorld;
        }

        protected override void OnBoundariesChangedInternal()
        {
            UpdateScaleOffsetMatrix();
        }

        override protected void UpdateInternal(GameTime now)
        {            
            //update all lerpers
            scaleOffsetLerpMatrix.Update(now);
        }

        internal void Destroy()
        {
            Waveform.Waveforms.Remove(this.channel);
        }

        private int FindBufferIndexByTime(float[] timeAxis, float time)
        {
            if (timeAxis.Length == 0)
                throw new Exception("No timepoints to search in");

            int currIndex = timeAxis.Length / 2;
            int stride = timeAxis.Length / 2;
            while (stride > 1)
            {
                stride = (stride + 1) / 2;
                if (currIndex > timeAxis.Length - 1) currIndex = timeAxis.Length - 1;
                if (currIndex < 0) currIndex = 0;

                if (timeAxis[currIndex] < time)
                    currIndex += stride;
                else
                    currIndex -= stride;
            }

            return currIndex;
        }

        protected int FindBufferIndexByTimeSafeLeft(float[] timeAxis, float time)
        {
            int ind = FindBufferIndexByTime(timeAxis, time) - 2; //2 to be sure in case of trianglestrips
            while (ind > 1 && timeAxis[ind] == timeAxis[ind - 1])
                ind--;

            return Math.Max(ind, 0);
        }

        protected int FindBufferIndexByTimeSafeRight(float[] timeAxis, float time)
        {
            int ind = FindBufferIndexByTime(timeAxis, time) + 2; //2 to be sure in case of trianglestrips
            while (ind < (timeAxis.Length - 2) && timeAxis[ind] == timeAxis[ind + 1])
                ind++;

            return Math.Min(ind, timeAxis.Length - 1);
        }
    }
}
