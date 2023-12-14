using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementSampleRate : Measurement
    {
        private SmartScope scope;

        public MeasurementSampleRate() : base("Sample rate", "Hz", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public void UpdateSource(SmartScope scope)
        {
            this.scope = scope;
            scope.OnSamplePeriodChanged += SamplePeriodChangedHandler;
        }

        private void SamplePeriodChangedHandler()
        {
            UpdateValueInternal(1.0 / scope.SamplePeriod);
        }
    }
}
