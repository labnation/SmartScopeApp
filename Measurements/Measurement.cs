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
    /// <summary>
    /// Used to tell SI scaling method whether thousend is 1000 or 1024
    /// </summary>
    public enum DisplayMethod
    {
        Normal = 1,
        SI = 1000,
        SI_1024 = 1024,
    }

    internal class Measurement
    {
        public double CurrentValue { get; protected set; }
        public string Unit { get; }
        public string Name { get; }
        public DisplayMethod DisplayMethod { get; }

        /// <summary>
        /// The precision to which we round
        /// In SI scale, this is the absolute precision (as opposed to the relative significance below)
        /// </summary>
        public double Precision { get; }

        /// <summary>
        /// The number of digits to consider significant when displaying.
        /// Note: when using SI notation, this is the number of significant
        /// digits in the SI scaled number
        /// 
        /// 0 means infinite significance
        /// </summary>
        public int Significance { get; }

        public Measurement(string name, string unit, double precision, int significance, DisplayMethod displayMethod)
        {
            this.Name = name;
            this.Unit = unit;
            this.Precision = precision;
            this.Significance = significance;
            this.DisplayMethod = displayMethod;
        }

        public void UpdateValueInternal(double val)
        {
            this.CurrentValue = val;
        }
    }

    internal abstract class StochasticMeasurement:Measurement
    {
        public static int LTSamplesToStore = 0;
        protected StochasticMeasurement(string name, string unit, double precision, int significance, DisplayMethod displayMethod) : base(name, unit, precision, significance, displayMethod)
        {
            ResetInternalValuesForCleanStart();
        }        

        //adding these as separate lists, as we'll want an performant way to take their min/max
        public Queue<double> LongTermMeanValues = new Queue<double>();
        public Queue<double> LongTermMaxValues = new Queue<double>();
        public Queue<double> LongTermMinValues = new Queue<double>();
        public Queue<double> LongTermStdValues = new Queue<double>();
        public Queue<DateTime> LongTermTimestamps = new Queue<DateTime>();

        private int accIterations;
        private double accMean;
        private double accMax;
        private double accMin;
        private double accStd;        
        
        public double Mean { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Std { get; private set; }

        private void ResetInternalValuesForCleanStart()
        {
            accIterations = 0;
            accMean = 0;
            accMax = double.MinValue;
            accMin = double.MaxValue;
            accStd = 0;
        }

        protected void UpdateValueInternal(double val)
        {
            this.CurrentValue = val;

            //update all accumulation values
            lock (this)
            {
                accIterations++;
                accMean += CurrentValue;
                if (accMax < CurrentValue) accMax = CurrentValue;
                if (accMin > CurrentValue) accMin = CurrentValue;
                accStd += Math.Pow(CurrentValue - Mean, 2);
            }
        }

        internal void ClearAllLTValues()
        {
            LongTermMeanValues.Clear();
            LongTermMaxValues.Clear();
            LongTermMinValues.Clear();
            LongTermStdValues.Clear();
            LongTermTimestamps.Clear();
        }

        public void FinalizeAccumulation(DateTime now)
        {
            if (accIterations == 0) return;

            lock (this)
            {
                double iterations = (double)accIterations;

                //calc to finalize            
                Mean = accMean / iterations;                                
                Std = Math.Sqrt(accStd / iterations);

                if (accMax == double.MinValue)
                    Max = double.NaN;
                else
                    Max = accMax;

                if (accMin == double.MaxValue)
                    Min = double.NaN;
                else
                    Min = accMin;

                ResetInternalValuesForCleanStart();
            }

            this.LongTermMeanValues.Enqueue(Mean);
            this.LongTermMaxValues.Enqueue(Max);
            this.LongTermMinValues.Enqueue(Min);
            this.LongTermStdValues.Enqueue(Std);
            this.LongTermTimestamps.Enqueue(now);

            while (LongTermMeanValues.Count > LTSamplesToStore) LongTermMeanValues.Dequeue();
            while (LongTermMaxValues.Count > LTSamplesToStore) LongTermMaxValues.Dequeue();
            while (LongTermMinValues.Count > LTSamplesToStore) LongTermMinValues.Dequeue();
            while (LongTermStdValues.Count > LTSamplesToStore) LongTermStdValues.Dequeue();
            while (LongTermTimestamps.Count > LTSamplesToStore) LongTermTimestamps.Dequeue();
        }
    }

    abstract internal class ChannelMeasurement : StochasticMeasurement
    {
        public Channel Channel { get; }
        private ScopeDataCollection lastIncludedDataCollection = null;
        public bool HasDedicatedUnit { get; private set; } //indicates whether probeUnit must be shown for measurement, or the dedicated unit

        abstract public double? FetchValueFromProcessor(DataProcessorMeasurements processor, Channel channel);
        
        public ChannelMeasurement(Channel channel, string name, double precision, int significance, DisplayMethod displayMethod) : base(name, "", precision, significance, displayMethod)
        {            
            this.Channel = channel;
            this.HasDedicatedUnit = false;
        }
        public ChannelMeasurement(Channel channel, string name, string unit, double precision, int significance, DisplayMethod displayMethod) : base(name, unit, precision, significance, displayMethod)
        {
            this.Channel = channel;
            this.HasDedicatedUnit = true;
        }

        public void UpdateValue(DataProcessorMeasurements processor)
        {
            //make sure we don't add the same value multiple times
            if (processor.LatestScopeDataCollection == lastIncludedDataCollection) return;
            lastIncludedDataCollection = processor.LatestScopeDataCollection;

            double? fetchedValue = FetchValueFromProcessor(processor, Channel);
            if (fetchedValue.HasValue) //null indicates it's impossible to calc this value based on the current dataset
                UpdateValueInternal(fetchedValue.Value);
        }
    }
}
