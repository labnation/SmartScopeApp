using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;
using LabNation.DeviceInterface.DataSources;

namespace ESuite.Measurements
{
    abstract internal class ChannelMeasurementCalculator
    {
        protected Dictionary<Channel, double?> values;
        protected DataProcessorMeasurements processor;        

        abstract protected double? GetValueInternal(Channel channel, Array data);

        public ChannelMeasurementCalculator(DataProcessorMeasurements processor)
        {
            this.processor = processor;
            values = new Dictionary<Channel, double?>();
        }

        public double? GetValue(Channel channel)
        {
            if (!values.ContainsKey(channel))
            {
                Array data;
                if (!OkToProcess(channel, out data))
                    values.Add(channel, null);
                else
                    values[channel] = GetValueInternal(channel, data);
            }
            return values[channel];
        }

        public void Reset()
        {
            values.Clear();
        }

        private bool OkToProcess(Channel channel, out Array data)
        {
            data = null;

            if (processor.LatestScopeDataCollection == null)
                return false;

            ChannelData d = processor.LatestScopeDataCollection.GetBestData(channel);
            if (d == null)
                return false;

            data = d.array;
            if (data.Length == 0)
                return false;

            return true;
        }

        //more optimal to perform all measurments related to frequency one. Hence, this method must be able to set the values of multiple Calculators.
        protected void ComputeFrequencyMeasurementValues(Channel channel)//, float minVoltage, float maxVoltage, double peakToPeak, out double frequency, out double frequencyError, out double dutyCycle, out double dutyCycleError, out double riseTime, out double fallTime)
        {
            double frequency, frequencyError, dutyCycle, dutyCycleError = 0;
            double minVoltage = 0;
            double maxVoltage = 0;
            double riseTime = 0;
            double fallTime = 0;

            if (channel is AnalogChannel)
            {
                minVoltage = processor.MeasMin.GetValue(channel).Value;
                maxVoltage = processor.MeasMax.GetValue(channel).Value;
            }            

            ChannelData data = processor.LatestScopeDataCollection.GetBestData(channel);
            Dictionary<int, bool> risingNFallingEdges;
            LabNation.DeviceInterface.Tools.ComputeFrequencyDutyCycle(data, out frequency, out frequencyError, out dutyCycle, out dutyCycleError, out risingNFallingEdges, (float)minVoltage, (float)maxVoltage);
            //FIXME: should split this up even more

            if (channel is AnalogChannel)
            {
                double p2p = processor.MeasPeakToPeak.GetValue(channel).Value;
                if (p2p < 3 * processor.LatestScopeDataCollection.resolution[channel as AnalogChannel])
                {
                    processor.MeasFrequency.values[channel] = null;
                    processor.MeasDutyCycle.values[channel] = null;
                    processor.MeasRiseTime.values[channel] = null;
                    processor.MeasFallTime.values[channel] = null;
                    return;
                }

                LabNation.DeviceInterface.Tools.ComputeRiseFallTimes((float[])data.array, (float)minVoltage, (float)maxVoltage, data.samplePeriod, 0.9f, risingNFallingEdges, out riseTime, out fallTime);
            }

            //update values of all relevant calculators
            processor.MeasFrequency.values[channel] = frequency;
            processor.MeasDutyCycle.values[channel] = dutyCycle;
            processor.MeasRiseTime.values[channel] = riseTime;
            processor.MeasFallTime.values[channel] = fallTime;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    internal class CalculatorMean : ChannelMeasurementCalculator
    {
        public CalculatorMean(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            if (!(data is float[]))
                throw new Exception("CalculatorMean execuded on non-float-array");

            double? meanVal = ((float[])data).Average();
            if ((processor.MeasMin.GetValue(channel) == processor.LatestScopeDataCollection.Data.SaturationLowValue[channel]) || (processor.MeasMax.GetValue(channel) == processor.LatestScopeDataCollection.Data.SaturationHighValue[channel]))
                meanVal = double.NaN;
            return meanVal;
        }
    }

    internal class CalculatorMax : ChannelMeasurementCalculator
    {
        public CalculatorMax(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            if (!(data is float[]))
                throw new Exception("CalculatorMax execuded on non-float-array");

            double? maxVal = ((float[])data).Max();
            if (maxVal == processor.LatestScopeDataCollection.Data.SaturationHighValue[channel])
                maxVal = double.NaN;
            return maxVal;
        }
    }

    internal class CalculatorMin : ChannelMeasurementCalculator
    {
        public CalculatorMin(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            if (!(data is float[]))
                throw new Exception("CalculatorMin execuded on non-float-array");

            double? minVal = ((float[])data).Min();
            if (minVal == processor.LatestScopeDataCollection.Data.SaturationLowValue[channel])
                minVal = double.NaN;
            return minVal;
        }
    }

    internal class CalculatorRMS : ChannelMeasurementCalculator
    {
        public CalculatorRMS(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            if (!(data is float[]))
                throw new Exception("CalculatorRMS execuded on non-float-array");

            return Math.Sqrt(((float[])data).Sum(n => n * n) / (float)data.Length);
        }
    }

    internal class CalculatorPeakToPeak : ChannelMeasurementCalculator
    {
        public CalculatorPeakToPeak(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            double max = processor.MeasMax.GetValue(channel).Value;
            double min = processor.MeasMin.GetValue(channel).Value;
            return max - min;
        }
    }

    internal class CalculatorFrequency : ChannelMeasurementCalculator
    {
        public CalculatorFrequency(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            //at this point, values does not contain an entry for channel

            //but by calling the following method, all freq values are filled in for this channel
            ComputeFrequencyMeasurementValues(channel);

            //so here we can simply query the value and return it
            return values[channel];
        }
    }

    internal class CalculatorDutyCycle : ChannelMeasurementCalculator
    {
        public CalculatorDutyCycle(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            //at this point, values does not contain an entry for channel

            //but by calling the following method, all freq values are filled in for this channel
            ComputeFrequencyMeasurementValues((AnalogChannel)channel);

            //so here we can simply query the value and return it
            return values[channel];
        }
    }

    internal class CalculatorFallTime : ChannelMeasurementCalculator
    {
        public CalculatorFallTime(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            //at this point, values does not contain an entry for channel

            //but by calling the following method, all freq values are filled in for this channel
            ComputeFrequencyMeasurementValues((AnalogChannel)channel);

            //so here we can simply query the value and return it
            return values[channel];
        }
    }

    internal class CalculatorRiseTime : ChannelMeasurementCalculator
    {
        public CalculatorRiseTime(DataProcessorMeasurements processor) : base(processor) { }
        protected override double? GetValueInternal(Channel channel, Array data)
        {
            //at this point, values does not contain an entry for channel

            //but by calling the following method, all freq values are filled in for this channel
            ComputeFrequencyMeasurementValues((AnalogChannel)channel);

            //so here we can simply query the value and return it
            return values[channel];
        }
    }
}
