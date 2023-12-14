using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;

namespace ESuite.Measurements
{
    internal delegate void ActiveMeasurementsChangedDelegate(MeasurementManager measurementManager);

    enum SystemMeasurementType { DataSourceFrameRate, SampleRate, AcquisitionLength, AcquisitionDepth, ViewportOffset, ViewportLength, RedrawRate, RamUsage, StorageFileSize }

    internal struct LongTermMeasurementValue
    {
        public DateTime Timestamp;
        public double Mean;
        public double Min;
        public double Max;
        public double Std;

        public LongTermMeasurementValue(DateTime timestamp, double mean, double min, double max, double std)
        {
            this.Timestamp = timestamp;
            this.Mean = mean;
            this.Min = min;
            this.Max = max;
            this.Std = std;
        }
    }

    internal class MeasurementManager
    {
        private const int maxLLSamples = 1000;
        public Dictionary<SystemMeasurementType, Measurement> SystemMeasurements = new Dictionary<SystemMeasurementType, Measurement>();
        public Dictionary<SystemMeasurementType, Measurement> ActiveSystemMeasurements { get; private set; }
        public Dictionary<AnalogChannel, Dictionary<Type, ChannelMeasurement>> ActiveChannelMeasurements { get; private set; }
        public event ActiveMeasurementsChangedDelegate ActiveMeasurementsChanged;

        private TimeSpan updateLTPeriod = new TimeSpan(0, 0, 1);   //update long-term measurements ever 1 sec

        private DateTime lastLTUpdateTimestamp = DateTime.Now;
        public DataProcessorMeasurements MeasurementDataProcessor;

        internal MeasurementManager()
        {
            ActiveSystemMeasurements = new Dictionary<Measurements.SystemMeasurementType, Measurements.Measurement>();
            ActiveChannelMeasurements = new Dictionary<AnalogChannel, Dictionary<Type, ChannelMeasurement>>();
            foreach (var ch in AnalogChannel.List)
                ActiveChannelMeasurements.Add(ch, new Dictionary<Type, ChannelMeasurement>());

            SystemMeasurements.Add(SystemMeasurementType.DataSourceFrameRate, new MeasurementDataSourceFrameRate());
            SystemMeasurements.Add(SystemMeasurementType.SampleRate, new MeasurementSampleRate());
            SystemMeasurements.Add(SystemMeasurementType.AcquisitionLength, new Measurement("Acq length", "s", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI));
            SystemMeasurements.Add(SystemMeasurementType.AcquisitionDepth, new Measurement("Acq depth", "S", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI));
            SystemMeasurements.Add(SystemMeasurementType.ViewportOffset, new Measurement("VP offset", "s", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI));
            SystemMeasurements.Add(SystemMeasurementType.ViewportLength, new Measurement("VP length", "s", ColorMapper.averagedVoltagePrecision, ColorMapper.NumberDisplaySignificance, DisplayMethod.SI));
            SystemMeasurements.Add(SystemMeasurementType.RedrawRate, new MeasurementRedrawRate());            
            SystemMeasurements.Add(SystemMeasurementType.StorageFileSize, new Measurements.MeasurementStorageMemorySize());
#if DEBUG
            SystemMeasurements.Add(SystemMeasurementType.RamUsage, new Measurements.MeasurementRamUsage());
#endif            

            //important: need to remove/readd these listeners at beginning/end of  ReloadStoredMeasurements, as otherwise the two Settings variables will be overridden (emptied) as soon as the measurement is restored.
            ActiveMeasurementsChanged += StoreActiveChannelMeasurments;
            ActiveMeasurementsChanged += StoreActiveSystemMeasurments;
        }

        public TimeSpan LLTimespan
        {
            get
            {
                TimeSpan ts = new TimeSpan(0, 0, (int)(updateLTPeriod.TotalSeconds) * StochasticMeasurement.LTSamplesToStore);
                return ts;
            }
            set
            {
                if (value.TotalSeconds < 1) return;

                double requestedSeconds = value.TotalSeconds;
                int secsPerSample = (int)Math.Ceiling(requestedSeconds / maxLLSamples);


                if (updateLTPeriod.TotalSeconds != secsPerSample)
                {
                    updateLTPeriod = new TimeSpan(0, 0, secsPerSample);

                    //clean all queues
                    //system measurments
                    foreach (Measurement m in ActiveSystemMeasurements.Values)
                        if (m is StochasticMeasurement)
                            (m as StochasticMeasurement).ClearAllLTValues();

                    //channel measurements
                    foreach (AnalogChannel ch in AnalogChannel.List)
                        foreach (ChannelMeasurement m in ActiveChannelMeasurements[ch].Values)
                            m.ClearAllLTValues();
                }

                StochasticMeasurement.LTSamplesToStore = (int)(requestedSeconds / secsPerSample);
                ESuite.Drawables.WaveformMeasurement.SecsPerSample = secsPerSample;
            }
        }

        public void ReloadStoredMeasurements()
        {
            //important: need to remove/readd these listeners at beginning/end of  ReloadStoredMeasurements, as otherwise the two Settings variables will be overridden (emptied) as soon as the measurement is restored.
            ActiveMeasurementsChanged -= StoreActiveChannelMeasurments;
            ActiveMeasurementsChanged -= StoreActiveSystemMeasurments;

            //first remove existing measurements
            for (int i = ActiveSystemMeasurements.Count - 1; i >= 0; i--)
                ShowSystemMeasurmentInBox(ActiveSystemMeasurements.ElementAt(i).Key, false);
            for (int i = ActiveChannelMeasurements[AnalogChannel.ChA].Count - 1; i >= 0; i--)
                ShowChannelMeasurementInBox(ActiveChannelMeasurements[AnalogChannel.ChA].ElementAt(i).Key, false, AnalogChannel.ChA);
            for (int i = ActiveChannelMeasurements[AnalogChannel.ChB].Count - 1; i >= 0; i--)
                ShowChannelMeasurementInBox(ActiveChannelMeasurements[AnalogChannel.ChB].ElementAt(i).Key, false, AnalogChannel.ChB);

            //add system measurements
            foreach (var item in Settings.Current.activeSystemMeasurements)
                ShowSystemMeasurmentInBox(item, true);

            //add channel measurements
            for (int i = 0; i < Settings.Current.activeChannelMeasurements.Count; i++)
            {
                AnalogChannel ch = Settings.Current.activeChannelMeasurements.ElementAt(i).Key == "A" ? AnalogChannel.ChA : AnalogChannel.ChB; //spent 2h trying to serialize and deserialize AnalogChannel... sometimes working but unstable
                foreach (string typeName in Settings.Current.activeChannelMeasurements[ch.Name])
                {
                    try
                    {
                        ShowChannelMeasurementInBox(Type.GetType(typeName), true, ch);
                    }
                    catch
                    {
                        LabNation.Common.Logger.Error("Error getting type of " + typeName);
                    }
                }
            }

            //important: need to remove/readd these listeners at beginning/end of  ReloadStoredMeasurements, as otherwise the two Settings variables will be overridden (emptied) as soon as the measurement is restored.
            ActiveMeasurementsChanged += StoreActiveChannelMeasurments;
            ActiveMeasurementsChanged += StoreActiveSystemMeasurments;
        }

        public void Update()
        {
            //check if Long_Term timer has overflowed
            DateTime now = DateTime.Now;
            if (now.Subtract(lastLTUpdateTimestamp) >= updateLTPeriod)
            {
                //finalize all channel measurements
                foreach (AnalogChannel ch in AnalogChannel.List)
                    foreach (ChannelMeasurement m in ActiveChannelMeasurements[ch].Values)
                        m.FinalizeAccumulation(now);
                //finalize all system measurements
                foreach (Measurement m in ActiveSystemMeasurements.Values)
                    if (m is StochasticMeasurement)
                        (m as StochasticMeasurement).FinalizeAccumulation(now);

                lastLTUpdateTimestamp = now;
            }

            //update system measurements which are not called elsewhere -- might as well gather them here
            (SystemMeasurements[SystemMeasurementType.RedrawRate] as MeasurementRedrawRate).Increment();
#if DEBUG
            (SystemMeasurements[SystemMeasurementType.RamUsage] as MeasurementRamUsage).Update();
#endif
        }

        public void ShowChannelMeasurementInBox(Type type, bool show, AnalogChannel ch)
        {
            lock (ActiveChannelMeasurements) //needed, as DataArrivalThread is reading these values as well
            {
                if (show)
                {
                    try
                    {
                        ChannelMeasurement m = (ChannelMeasurement)Activator.CreateInstance(type, ch);
                        if (!ActiveChannelMeasurements[ch].ContainsKey(type))
                            ActiveChannelMeasurements[ch].Add(type, m);
                        else
                            LabNation.Common.Logger.Error("ERROR: channel measurement already included in box " + type.ToString() + " " + ch.ToString());

                        if (MeasurementDataProcessor != null)
                            m.UpdateValue(MeasurementDataProcessor); //causes measurement to process the last data, to show a value in case scope is stopped
                    }
                    catch
                    {
                        LabNation.Common.Logger.Error("Failed creating instance of " + type.AssemblyQualifiedName);
                    }                    
                }
                else
                {
                    if (ActiveChannelMeasurements[ch].ContainsKey(type))
                        ActiveChannelMeasurements[ch].Remove(type);
                    else
                        LabNation.Common.Logger.Error("ERROR: channel measurement not included in box " + type.ToString());
                }
            }

            if (ActiveMeasurementsChanged != null)
                ActiveMeasurementsChanged(this);
        }

        private void StoreActiveChannelMeasurments(MeasurementManager dummy)
        {
            Settings.Current.activeChannelMeasurements = new Dictionary<string, List<string>>();
            foreach (var kvp in ActiveChannelMeasurements)
            {
                AnalogChannel ch = kvp.Key;
                Settings.Current.activeChannelMeasurements.Add(ch.Name, new List<string>());
                foreach (var kvp2 in ActiveChannelMeasurements[ch])
                {
                    Type type = kvp2.Key;
                    ChannelMeasurement meas = kvp2.Value;
                    Settings.Current.activeChannelMeasurements[ch.Name].Add(type.AssemblyQualifiedName);
                }
            }
        }

        public void ShowSystemMeasurmentInBox(SystemMeasurementType sysMeasurement, bool show)
        {
            if (show)
            {
                if (!ActiveSystemMeasurements.ContainsKey(sysMeasurement))
                    ActiveSystemMeasurements.Add(sysMeasurement, SystemMeasurements[sysMeasurement]);
                else
                    LabNation.Common.Logger.Error("ERROR: channel measurement already included in box " + sysMeasurement.ToString());
            }
            else
            {
                if (ActiveSystemMeasurements.ContainsKey(sysMeasurement))
                    ActiveSystemMeasurements.Remove(sysMeasurement);
                else
                    LabNation.Common.Logger.Error("ERROR: channel measurement not included in box " + sysMeasurement.ToString());
            }

            if (ActiveMeasurementsChanged != null)
                ActiveMeasurementsChanged(this);
        }

        private void StoreActiveSystemMeasurments(MeasurementManager dummy)
        {
            Settings.Current.activeSystemMeasurements = new List<Measurements.SystemMeasurementType>();
            foreach (var kvp in ActiveSystemMeasurements)
                Settings.Current.activeSystemMeasurements.Add(kvp.Key);
        }
    }
}
