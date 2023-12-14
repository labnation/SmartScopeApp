using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementFrequency : ChannelMeasurement
    {
        public MeasurementFrequency(AnalogChannel channel) : base(channel, "Frequency", "Hz", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public override double? FetchValueFromProcessor(DataProcessorMeasurements processor, Channel channel)
        {
            return processor.MeasFrequency.GetValue(channel);
        }
    }
}
