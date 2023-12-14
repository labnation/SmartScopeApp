using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;
using LabNation.Common;

namespace ESuite.DataProcessors
{
    internal class DataProcessorMeasurements: IDataProcessor
    {
        public ChannelMeasurementCalculator MeasMin { get; private set; }
        public ChannelMeasurementCalculator MeasMax { get; private set; }
        public ChannelMeasurementCalculator MeasMean { get; private set; }
        public ChannelMeasurementCalculator MeasPeakToPeak { get; private set; }
        public ChannelMeasurementCalculator MeasRMS { get; private set; }
        public ChannelMeasurementCalculator MeasFrequency { get; private set; }
        public ChannelMeasurementCalculator MeasDutyCycle { get; private set; }
        public ChannelMeasurementCalculator MeasRiseTime { get; private set; }
        public ChannelMeasurementCalculator MeasFallTime { get; private set; }

        public ScopeDataCollection LatestScopeDataCollection { get; private set; }
        public bool IsTimeVariant { get { return false; } }

        private List<ChannelMeasurementCalculator> calculators = new List<ChannelMeasurementCalculator>();
        private MeasurementManager measurementManager;

        public DataProcessorMeasurements(MeasurementManager measurementManager)
        {
            this.measurementManager = measurementManager;

            MeasMin = new CalculatorMin(this);
            MeasMax = new CalculatorMax(this);
            MeasMean = new CalculatorMean(this);
            MeasRMS = new CalculatorRMS(this);
            MeasPeakToPeak = new CalculatorPeakToPeak(this);
            MeasFrequency = new CalculatorFrequency(this);
            MeasDutyCycle = new CalculatorFrequency(this);
            MeasRiseTime = new CalculatorRiseTime(this);
            MeasFallTime = new CalculatorFallTime(this);

            calculators.Add(MeasMin);
            calculators.Add(MeasMax);
            calculators.Add(MeasMean);
            calculators.Add(MeasRMS);
            calculators.Add(MeasPeakToPeak);
            calculators.Add(MeasFrequency);
            calculators.Add(MeasDutyCycle);
            calculators.Add(MeasRiseTime);
            calculators.Add(MeasFallTime);
        }

        public void Reset() { }

        public void Process(ScopeDataCollection scopeDataCollection)
        {
            //lock to make sure all measurement values are coherent
            lock (this) lock (measurementManager.ActiveChannelMeasurements)
            {
                LatestScopeDataCollection = scopeDataCollection;

                //first reset all values of all calculators
                foreach (ChannelMeasurementCalculator calc in calculators)
                    calc.Reset();

                //then update all active measurements, which causes only the strictly required calculators to be executed
                foreach (AnalogChannel ch in AnalogChannel.List)
                    foreach (ChannelMeasurement chanMeas in measurementManager.ActiveChannelMeasurements[ch].Values)
                        chanMeas.UpdateValue(this);
            }
        }
    }
}
