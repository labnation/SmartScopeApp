using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementMinVoltage : ChannelMeasurement
    {
        public MeasurementMinVoltage(AnalogChannel channel) : base(channel, "Min", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public override double? FetchValueFromProcessor(DataProcessorMeasurements processor, Channel channel)
        {
            return processor.MeasMin.GetValue(channel);
        }
    }
}
