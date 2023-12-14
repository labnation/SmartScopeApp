using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementDutyCycle : ChannelMeasurement
    {
        public MeasurementDutyCycle(AnalogChannel channel) : base(channel, "Duty cycle", "%", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public override double? FetchValueFromProcessor(DataProcessorMeasurements processor, Channel channel)
        {
            return processor.MeasDutyCycle.GetValue(channel);
        }
    }
}
