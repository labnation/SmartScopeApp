using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.Interfaces;

namespace ESuite.Drawables
{
    internal abstract class WaveDataBuffer
    {        
        public virtual float[] SubsampledTimeData { get; protected set; }
        public double SubsampledStartTime { get; protected set; }
        public double SubsampledEndTime { get; protected set; }
        public double SubsampledSamplePeriod { get; protected set; }
        public double RawSamplePeriod { get; private set; }
        public double RawStartTime { get; private set; }
        private Array fullData = null;
        private int decimation;
        
        //mechanism which makes sure data is called only once, and only when needed
        private Array subsampledData = null;
        public Array SubsampledData {
            get
            {
                if (this.subsampledData == null)
                {
                    if (fullData is float[])
                        this.subsampledData = PeakDetectFloat((float[])fullData);
                    else if (fullData is bool[])
                        this.subsampledData = PeakDetectBool((bool[])fullData);
                    else if (fullData is DecoderOutput[])
                        this.subsampledData = PeakDetectDecoder((DecoderOutput[])fullData);
                    else
                        throw new Exception("No software peakdetect found for this datatype");
                }
                
                return this.subsampledData;                
            }
        }

        protected WaveDataBuffer(Array fullData, int decimation, double rawStartTime, double rawSamplePeriod)
        {
            this.fullData = fullData;
            this.decimation = decimation;
            this.RawStartTime = rawStartTime;
            this.RawSamplePeriod = rawSamplePeriod;

            //in case of no decimation, save processing time by simply using full array instead of copying
            if (decimation == 1)
                subsampledData = fullData;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////
        // Peak-Detect algorithms
        // 
        private float[] PeakDetectFloat(float[] fullData)
        {
            int bufferLength = (int)((SubsampledEndTime - SubsampledStartTime) / SubsampledSamplePeriod) + 1;
            float[] buffer = new float[bufferLength];

            float lastValue = fullData[0];
            int bufferPointer = 0;
            int fullArrayPointer = 0;

            int currBinLength = fullData.Length / bufferLength; //small profiler optimization

            //walk over entire array
            while (bufferPointer < bufferLength)
            {
                //for this bin: find extreme value compared to previous bin value (lastValue)
                float extremeVal = lastValue;
                float extremeDiff = 0;
                for (int i = 0; i < currBinLength; i++)
                {
                    float thisVal = fullData[fullArrayPointer++];
                    float thisDiff = Math.Abs(thisVal - lastValue);
                    if (thisDiff > extremeDiff)
                    {
                        extremeDiff = thisDiff;
                        extremeVal = thisVal;
                    }
                }
                lastValue = extremeVal;
                buffer[bufferPointer++] = extremeVal;
            }

            return buffer;
        }

        private bool[] PeakDetectBool(bool[] fullData)
        {
            int bufferLength = (int)((SubsampledEndTime - SubsampledStartTime) / SubsampledSamplePeriod) + 1;
            bool[] buffer = new bool[bufferLength];

            bool lastValue = fullData[0];
            int bufferPointer = 0;
            int fullArrayPointer = 0;

            int currBinLength = fullData.Length / bufferLength; //small profiler optimization

            //walk over entire array
            while (bufferPointer < bufferLength)
            {
                //for this bin: find extreme value compared to previous bin value (lastValue)
                bool extremeVal = lastValue;
                for (int i = 0; i < currBinLength; i++)
                {
                    bool thisVal = fullData[fullArrayPointer++];
                    bool thisDiff = thisVal != lastValue;
                    if (thisDiff)
                        extremeVal = thisVal;
                }
                lastValue = extremeVal;
                buffer[bufferPointer++] = extremeVal;
            }

            return buffer;
        }

        private DecoderOutput[] PeakDetectDecoder(DecoderOutput[] fullData)
        {
            int fullArrayLength = fullData.Length;
            List<DecoderOutput> buffer = new List<DecoderOutput>();
            int fullArrayPointer = 0;

            //walk over entire array, and don't store elements which end in the same bin
            int lastBin = -1;
            while (fullArrayPointer < fullArrayLength)
            {
                DecoderOutput currValue = fullData[fullArrayPointer++];
                int currEndBin = currValue.EndIndex/decimation; //this is where the bin mapping happens
                if (lastBin != currEndBin)
                {
                    lastBin = currEndBin;
                    buffer.Add(currValue);
                }
            }

            return buffer.ToArray();
        }
    }

    //for 2048 and Acquisition data
    internal class WaveDataBufferRegular : WaveDataBuffer
    {
        private float[] internalTimeData;
        public override float[] SubsampledTimeData
        {
            get
            {
                //mechanism to ensure time axis is generated only once
                if (internalTimeData == null)
                {
                    int nrSamples = SubsampledData.Length;
                    internalTimeData = new float[nrSamples];
                    for (int i = 0; i < nrSamples; i++)
                        internalTimeData[i] = (float)(SubsampledStartTime + (float)i * SubsampledSamplePeriod);
                }
                return internalTimeData;
            }
        }

        public WaveDataBufferRegular(Array fullData, double fullArrayStartTime, double fullArraySamplePeriod, double subsampledStartTime, int decimation)
            : base(fullData, decimation, fullArrayStartTime, fullArraySamplePeriod) //decimation is the divider, so 1 indicates no decimation. typically 1,2,4,8,16,...
        {
            this.SubsampledStartTime = subsampledStartTime;
            this.SubsampledSamplePeriod = fullArraySamplePeriod * (double)decimation;
            this.SubsampledEndTime = SubsampledStartTime + (double)(fullData.Length / decimation - 1) * SubsampledSamplePeriod;
        }
    }

    //for ETS data
    internal class WaveDataBufferETS : WaveDataBuffer
    {
        public WaveDataBufferETS(Array fullData, double fullArrayStartTime, double fullArraySamplePeriod, float[] timeData)
            : base(fullData, 1, fullArrayStartTime, fullArraySamplePeriod) //1 indicates no decimation
        {
            this.SubsampledTimeData = timeData;
            this.SubsampledSamplePeriod = (timeData[timeData.Length - 1] - timeData[0]) / (timeData.Length - 1.0);
            if (timeData.Length > 0)
            {
                this.SubsampledStartTime = timeData[0];
                this.SubsampledEndTime = timeData[timeData.Length - 1];
            }
            else
            {
                this.SubsampledStartTime = 0;
                this.SubsampledEndTime = 0;
            }
        }
    }
}
