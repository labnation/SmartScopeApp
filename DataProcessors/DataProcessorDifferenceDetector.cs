using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;

namespace ESuite.DataProcessors
{
    internal class DataProcessorDifferenceDetector : IDataProcessor
    {
        private struct Differentiators
        {
            public double? frequency;
            public double? dutyCycle;
            public double? riseTime;
            public double? fallTime;
            public double? min;
            public double? max;
            public double? mean;
        }

        public Dictionary<AnalogChannel, bool> DifferenceDetected = new Dictionary<AnalogChannel, bool>();

        private DataProcessorMeasurements measurementProcessor;
        private DataProcessorETS etsProcessor;
        private List<AnalogChannel> enabledAnalogChannels;
        private Dictionary<AnalogChannel, StochasticMeasurement> previousMeasurements = new Dictionary<AnalogChannel, StochasticMeasurement>();
        private Dictionary<AnalogChannel, Differentiators> previousDifferentiators = new Dictionary<AnalogChannel, Differentiators>();

        public DataProcessorDifferenceDetector(DataProcessorMeasurements measurementProcessor)
        {
            this.measurementProcessor = measurementProcessor;            
        }

        public void Init(DataProcessorETS etsProcessor)
        {
            this.etsProcessor = etsProcessor;
        }

        public void UpdateGUIValues(List<AnalogChannel> enabledAnalogChannels)
        {
            this.enabledAnalogChannels = enabledAnalogChannels;
        }

        private Differentiators FillDifferentiators(Channel ch)
        {
            Differentiators diff = new Differentiators() { frequency = measurementProcessor.MeasFrequency.GetValue(ch), dutyCycle = measurementProcessor.MeasDutyCycle.GetValue(ch), fallTime = measurementProcessor.MeasFallTime.GetValue(ch), max = measurementProcessor.MeasMax.GetValue(ch), mean = measurementProcessor.MeasMean.GetValue(ch), min = measurementProcessor.MeasMin.GetValue(ch), riseTime = measurementProcessor.MeasRiseTime.GetValue(ch) };
            return diff;
        }

        private bool IsValid(double? val)
        {
            if (val == null) return false;
            if (val.Value == double.NaN) return false;
            return true;
        }

        public void Process(ScopeDataCollection dataCollection)
        {
            DifferenceDetected.Clear();
            if (!etsProcessor.ETSCandidate) return; //don't process this data when timing doesn't allow ETS

            Dictionary<AnalogChannel, Differentiators> newDifferentiators = new Dictionary<AnalogChannel, Differentiators>();

            foreach (var ch in enabledAnalogChannels)
            {
                newDifferentiators.Add(ch, FillDifferentiators(ch));
                if (previousDifferentiators.ContainsKey(ch))
                {
                    float differenceThreshold = 0.03f; //percent
                    double minTimeDifference = dataCollection.Data.samplePeriod[ChannelDataSourceScope.Viewport]*2;

                    bool difference = false;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].max) && IsValid(newDifferentiators[ch].max))
                            if (Math.Abs(newDifferentiators[ch].max.Value - previousDifferentiators[ch].max.Value) > dataCollection.Data.Resolution[ch] * 4.0)
                                difference = true;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].min) && IsValid(newDifferentiators[ch].min))
                            if (Math.Abs(newDifferentiators[ch].min.Value - previousDifferentiators[ch].min.Value) > dataCollection.Data.Resolution[ch] * 4.0)
                                difference = true;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].mean) && IsValid(newDifferentiators[ch].mean))
                            if (Math.Abs(newDifferentiators[ch].mean.Value - previousDifferentiators[ch].mean.Value) > dataCollection.Data.Resolution[ch])
                                difference = true;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].frequency) && IsValid(newDifferentiators[ch].frequency))
                            if (Math.Abs(newDifferentiators[ch].frequency.Value - previousDifferentiators[ch].frequency.Value) / newDifferentiators[ch].frequency.Value > differenceThreshold)
                                    difference = true;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].dutyCycle) && IsValid(newDifferentiators[ch].dutyCycle))
                            if (Math.Abs(newDifferentiators[ch].dutyCycle.Value - previousDifferentiators[ch].dutyCycle.Value) > 15f *100f) //DC already in %! So need to *100. Then use a large margin, because at highfreq the DC value goes in large steps
                                difference = true;
                    if (!difference)
                        if (IsValid(previousDifferentiators[ch].riseTime) && IsValid(newDifferentiators[ch].riseTime))
                            if (Math.Abs(newDifferentiators[ch].riseTime.Value - previousDifferentiators[ch].riseTime.Value) / newDifferentiators[ch].riseTime.Value > differenceThreshold)
                                if (Math.Abs(newDifferentiators[ch].riseTime.Value - previousDifferentiators[ch].riseTime.Value) > minTimeDifference)
                                    difference = true;

                    DifferenceDetected.Add(ch, difference);
                }
                else
                {
                    DifferenceDetected.Add(ch, true);
                }
            }

            //store data for next comparison round
            previousDifferentiators = newDifferentiators;
        }

        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }
    }
}
