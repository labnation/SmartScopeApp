using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementRedrawRate : StochasticMeasurement
    {
        private FrameRateCounter framerateCounter;

        public MeasurementRedrawRate() : base("Draw refresh", "Hz", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
            this.framerateCounter = new FrameRateCounter();
        }

        public void Increment()
        {
            framerateCounter.IncrementCounter(this, null);
            UpdateValueInternal(framerateCounter.FrameRate);
        }
    }
}

