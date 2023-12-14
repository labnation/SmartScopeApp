using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementFallTime : ChannelMeasurement
    {
        public MeasurementFallTime(AnalogChannel channel) : base(channel, "Fall time", "s", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public override double? FetchValueFromProcessor(DataProcessorMeasurements processor, Channel channel)
        {
            return processor.MeasFallTime.GetValue(channel);
        }
    }
}
