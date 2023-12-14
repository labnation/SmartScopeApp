using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    class DataProcessorInvertor: IDataProcessor
    {
        private ChannelData lastProcessedChannel;
        private AnalogChannel channel;
        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }
        public DataProcessorInvertor(AnalogChannel channel)
        {
            this.channel = channel;
            lastProcessedChannel = new ChannelData(ChannelDataSourceScope.Viewport, channel, null, false, 0);
        }
        public void Process(ScopeDataCollection scopeDataCollection)
        {
            ChannelData input = scopeDataCollection.GetBestData(channel);
            if (input == null) return;
            if (lastProcessedChannel.Equals(input)) return;
            lastProcessedChannel = input;

            Func<float, float> invert = x => x * -1f;
            scopeDataCollection.OverrideData(input, LabNation.Common.Utils.TransformArray(input.array, invert));
        }
    }
}
