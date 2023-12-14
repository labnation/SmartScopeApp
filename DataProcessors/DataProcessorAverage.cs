using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    class DataProcessorAverage: IDataProcessor
    {
        private ChannelData lastProcessedChannel;
        private AnalogChannel channel;
        private List<float[]> waves;
        private uint averagingWindow;
        private object averagingWindowLock = new object();
        public bool IsTimeVariant { get { return true; } }

        public DataProcessorAverage(AnalogChannel channel, uint averagingWindow)
        {
            this.channel = channel;
            lastProcessedChannel = new ChannelData(ChannelDataSourceScope.Viewport, channel, null, false, 0);
            this.averagingWindow = averagingWindow;
            waves = new List<float[]>();
        }

        public void SetWindow(uint window)
        {
            lock(averagingWindowLock) { averagingWindow = window; }
        }
        
        public void Process(ScopeDataCollection scopeDataCollection)
        {
            //Always use viewport, can't average with entire Acquisition
            ChannelData newWave = scopeDataCollection.GetUnprocessedData(ChannelDataSourceScope.Viewport, this.channel);
            if(newWave == null) return;
            if (lastProcessedChannel.Equals(newWave)) return;
            lastProcessedChannel = newWave;

            waves.Add((float[])newWave.array);
            uint window;
            lock (averagingWindowLock) { window = averagingWindow; }
            
            while (waves.Count > window) waves.RemoveAt(0);

            double[] waveSum = new double[newWave.array.Length];
            Func<float, double, double> sum = (x, y) => (x + y);
            for(int i = 0; i < waves.Count; i++) {
                waveSum = LabNation.Common.Utils.CombineArrays<float, double, double>(
                    waves.ElementAt(i), waveSum, sum);
            }
            float[] result = LabNation.Common.Utils.TransformArray<double, float>(waveSum, (x) => ((float)(x / waves.Count)));

            scopeDataCollection.OverrideData(newWave, result);
        }
        public void Reset() {
            waves = new List<float[]>();
        }
    }
}
