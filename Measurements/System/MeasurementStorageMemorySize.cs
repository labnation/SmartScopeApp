using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementStorageMemorySize : Measurement
    {
        public MeasurementStorageMemorySize() : base("Recording", "B", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
        }

        public void Reset()
        {
            this.CurrentValue = 0;
        }
    }
}
