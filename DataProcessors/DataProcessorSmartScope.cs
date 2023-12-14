using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using Microsoft.Xna.Framework;
using System.IO;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;

namespace ESuite.DataProcessors
{
    internal class DataProcessorSmartScope : IDataProcessor
    {
        private Dictionary<Channel, ChannelData> lastProcessedChannels = new Dictionary<Channel, ChannelData>();
        //Select through: AnalogChannel, multiplier, subsampling
        public bool IsTimeVariant { get { return false; } }
        public void Reset() { }
        private IScope scope;
        
        public DataProcessorSmartScope()
        {
            foreach (AnalogChannel ch in AnalogChannel.List)
                lastProcessedChannels[ch] = new ChannelData(ChannelDataSourceScope.Viewport, ch, null, false, 0);
        }

        public static bool TimeSmoothingEnabled = true;

        /// <summary>
        /// Contains precalculated compensation sprectra. Indices: [Channel][Multiplier][Bins][Decimation]
        /// </summary>
        public void Update(IScope scope)
        {
            this.scope = scope;
            if (!(scope is SmartScope))
                return;

            SmartScope ss = (SmartScope)scope;
        }

        public void Process(ScopeDataCollection scopeDataCollection)
        {
            if (scopeDataCollection.ScopeType != typeof(LabNation.DeviceInterface.Devices.SmartScope))
                return;

            if (scopeDataCollection.Rolling)
                return;

            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                ChannelData data = scopeDataCollection.GetBestData(ch);
                if (data == null || data.partial) continue;
                if (lastProcessedChannels[ch].Equals(data)) continue;
                lastProcessedChannels[ch] = data;
                                    	
                if (TimeSmoothingEnabled) 
                {
                    float[] voltages = (float[])data.array;
                    byte[] bytes = (byte[])scopeDataCollection.GetBestData(ch.Raw()).array;
                    //NOTE: we work directly on the array elements here. This should be done with
                    //great caution, and in combination with checks that ensure this operation
                    //hans't been performed before as otherwise we'd cumulatively smooth upon smooth.
                    TimeDomainSmoothing(voltages, bytes);                        
                }                    
            }
        }

        /// <summary>
        /// Detects patches with variations of max 1 value. Replace those by rolling average to get monotonous increasing or decreasing waves.
        /// </summary>
        /// <param name="inVoltages"></param>
        /// <param name="inBytes"></param>
        /// <returns></returns>
        public static void TimeDomainSmoothing(float[] inVoltages, byte[] inBytes)
        {
            if (inVoltages == null)
            {
                LabNation.Common.Logger.Error("TimeDomainSmoothing: inVoltages was null");
                return;
            }
            if (inBytes == null)
            {
                LabNation.Common.Logger.Error("TimeDomainSmoothing: inBytes was null");
                return;
            }
            if (inVoltages.Length != inBytes.Length)
            {
                LabNation.Common.Logger.Error("TimeDomainSmoothing: arrays of different length");
                return;
            }

            int noDifferenceSampleCounter = 0;
            float rollingValue = inVoltages[0];

            float smoothness = 0.6f;
            float invSmoothness = 1f - smoothness;
            for (int i = 1; i < inVoltages.Length; i++)
            {
                rollingValue = smoothness * rollingValue + invSmoothness * inVoltages[i];
                if (inBytes[i] == inBytes[i - 1])
                    noDifferenceSampleCounter++;
                else if (inBytes[i] == inBytes[i - 1] - 1)
                    noDifferenceSampleCounter++;
                else if (inBytes[i] == inBytes[i - 1] + 1)
                    noDifferenceSampleCounter++;
                else
                    noDifferenceSampleCounter = 0;

                if (noDifferenceSampleCounter > 15)
                    inVoltages[i] = rollingValue;
            }
        }
    }
}
