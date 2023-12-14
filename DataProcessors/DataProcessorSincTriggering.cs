using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    internal class DataProcessorSincTriggering : IDataProcessor
    {
        public static bool EnableSincInterpolation = true;

        public DataProcessorSincTriggering(TriggerValue tv)
        {
            this.triggerValue = tv;
        }

        public TriggerValue triggerValue = null;

        public void Process(ScopeDataCollection dataCollection)
        {
            //sinc triggering
            TriggerValue tv = dataCollection.Data.TriggerValue;

            if (dataCollection.Data.samplePeriod.ContainsKey(ChannelDataSourceScope.Viewport) && dataCollection.Data.samplePeriod.ContainsKey(ChannelDataSourceScope.Acquisition) && //preventing crash in case either buffer is not present. see crashreport 11/8/2016 12:28:20 PM
                (EnableSincInterpolation && tv.mode != TriggerMode.Digital && tv.source == TriggerSource.Channel && dataCollection.Data.samplePeriod[ChannelDataSourceScope.Viewport] == dataCollection.Data.samplePeriod[ChannelDataSourceScope.Acquisition] && dataCollection.TriggerAdjustment == 0))
			{
				ChannelData d = dataCollection.GetData(ChannelDataSourceScope.Viewport, tv.channel);
				if (d != null) 
				{
					int decimator = (int)Math.Round (dataCollection.Data.samplePeriod [ChannelDataSourceScope.Viewport] / dataCollection.Data.samplePeriod [ChannelDataSourceScope.Acquisition]);
					Int64 triggerPosition = (dataCollection.Data.HoldoffSamples - dataCollection.Data.ViewportOffsetSamples) / decimator;
					dataCollection.TriggerAdjustment = SincInterpolationBasedTimeShift ((float[])d.array, this.triggerValue.level, d.samplePeriod, triggerPosition);
				}
			}
			else
			{
				dataCollection.TriggerAdjustment = 0;
			}
        }

        private double SincInterpolationBasedTimeShift(float[] voltages, float AnalogTriggerLevel, double samplePeriod, Int64 triggerIndex)
        {
            //Only perform when trigger is inside array (inside viewport)
            if (triggerIndex < 1 || triggerIndex > voltages.Length - 2)
                return 0;

            //initialization
            float voltageOfSampleBeforeTrigger = voltages[triggerIndex - 1];
            float voltageOfSampleAfterTrigger  = voltages[triggerIndex + 1];

            double timeOfSampleBeforeTrigger = (triggerIndex - 1) * (float)samplePeriod;
            double timeOfSampleAfterTrigger = (triggerIndex + 1) * (float)samplePeriod;

            double linearInterpolatedTriggerTime = timeOfSampleBeforeTrigger;
            double deltaT = 1;
            const int maxIterations = 50;
            int iterations = 0;

            while (Math.Abs(deltaT) > 1E-10 && (iterations < maxIterations))
            {
                //calc sinc
                double oldTime = linearInterpolatedTriggerTime;
                linearInterpolatedTriggerTime = timeOfSampleBeforeTrigger + (AnalogTriggerLevel - voltageOfSampleBeforeTrigger) / (voltageOfSampleAfterTrigger - voltageOfSampleBeforeTrigger) * (timeOfSampleAfterTrigger - timeOfSampleBeforeTrigger);
                float sincInterpolatedVoltage = Utils.SincReconstruct((float)linearInterpolatedTriggerTime, (float)samplePeriod, voltages);

                //update highlow values
                if (sincInterpolatedVoltage > AnalogTriggerLevel)
                {
                    timeOfSampleAfterTrigger = linearInterpolatedTriggerTime;
                    voltageOfSampleAfterTrigger = sincInterpolatedVoltage;
                }
                else
                {
                    timeOfSampleBeforeTrigger = linearInterpolatedTriggerTime;
                    voltageOfSampleBeforeTrigger = sincInterpolatedVoltage;
                }

                //while loop maintenance
                iterations++;
                deltaT = oldTime - linearInterpolatedTriggerTime;
            }

            if (iterations < maxIterations)
            {
                double timeShift = linearInterpolatedTriggerTime - (triggerIndex - 1) * (float)samplePeriod;
                if (Math.Abs(timeShift) < samplePeriod*2)
                    return timeShift - samplePeriod;
            }

            //failed for one of the reasons above
            return 0;
        }

        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }
    }
}
