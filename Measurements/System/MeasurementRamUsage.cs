using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;
using System.Diagnostics;

namespace ESuite.Measurements
{
    class MeasurementRamUsage : StochasticMeasurement
    {
        private Process process;

        public MeasurementRamUsage() : base("RAM usage", "B", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
            this.process = Process.GetCurrentProcess();
        }

        public void Update()
        {
            UpdateValueInternal(process.PrivateMemorySize64);
        }
    }
}

