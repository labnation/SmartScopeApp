using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    class MeasurementDataSourceFrameRate : StochasticMeasurement
    {
        private FrameRateCounter framerateCounter;
        private int lastIdentifier = -1;
        private DataSource source = null;

        public MeasurementDataSourceFrameRate() : base("Data refresh", "Hz", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI)
        {
            this.framerateCounter = new FrameRateCounter();
        }

        public void UpdateSource(DataSource newSource)
        {
            if (source != null)
                source.OnNewDataAvailable -= NewDataAvailable;

            this.source = newSource;

            if (newSource != null)
                newSource.OnNewDataAvailable += NewDataAvailable;
        }

        public void NewDataAvailable(DataPackageScope dataPackage, DataSource dataSource)
        {
            if (dataPackage.Identifier != lastIdentifier)
            {
                lastIdentifier = dataPackage.Identifier;
                framerateCounter.IncrementCounter(this, null);
            }

            UpdateValueInternal(framerateCounter.FrameRate);
        }
    }
}
