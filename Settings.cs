using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;
using ESuite.Drawables;

#if ANDROID
using Android.Preferences;
#endif

namespace ESuite
{
    internal delegate void ApplySettingsDelegate(Settings settings);
    
    internal enum MainModes { Analog, Digital, Mixed }
    internal enum TickStickyNess
    {
        Off,
        Major,
        Minor
    };

    [DataContract]
    internal struct ProcessorStateDescription
    {
        [DataMember] public string ProcessorName;
        [DataMember] public Dictionary<string, object> ParameterValue;
        [DataMember] public Dictionary<string, string> SourceChannelTypes;
        [DataMember] public Dictionary<string, string> SourceChannelNames;
        [DataMember] public ESuite.Drawables.RadixType Radix;
        [DataMember] public ESuite.Drawables.GraphType GraphType;
    }

    internal enum GridAnchor
    {
        AcquisitionBuffer,
        Viewport
    };

    internal enum AcquisitionBufferScalingBehaviour
    {
        AcquisitionBufferMovesUnderViewport,
        ViewportAnchoredToAcquisitionBuffer
    };

    //attributes used for classifying which properties to save/recall
    internal class AttributeSettingVisual : Attribute    { internal AttributeSettingVisual() { } }
    internal class AttributeSettingScopeTiming : Attribute { internal AttributeSettingScopeTiming() { } }
    internal class AttributeSettingOperators : Attribute { internal AttributeSettingOperators() { } }
    internal class AttributeSettingMeasurements : Attribute { internal AttributeSettingMeasurements() { } }

    [Serializable()]
    [DataContract]
    internal class Settings
    {
        static private List<ApplySettingsDelegate> observers = new List<ApplySettingsDelegate>();
        static internal void ObserveMe(ApplySettingsDelegate del)
        {
            observers.Add(del);
        }

        [DataContract]
        public class ChannelSetting
        {
            [DataMember] public float offset;
            [DataMember] public double range;
            [DataMember] public bool enabled;
            public ChannelSetting(float yoff, double range, bool enabled)
            {
                this.offset = yoff;
                this.range = range;
                this.enabled = enabled;
            }
        }
        float VOLTAGE_OFFSET_DEFAULT_CHA = 1f;
        float VOLTAGE_OFFSET_DEFAULT_CHB = -2f;
        float VOLTAGE_RANGE_DEFAULT = (float)(ESuite.Drawables.Grid.DivisionsVerticalMax * 1f);
        [DataContract]
        public class AnalogChannelSetting
        {
            [DataMember] public double range;
            [DataMember] private Probe _probeDivsion = null;
            public Probe Probe
            {
                get { return _probeDivsion != null ? _probeDivsion : LabNation.DeviceInterface.Devices.Probe.DefaultX1Probe; }
                set { _probeDivsion = value; }
            }
            [DataMember] public Coupling coupling;
            [DataMember] public float triggerLevel;
            [DataMember] public bool Invert;
            public AnalogChannelSetting(float range, Probe div)
            {
                this.range = range;
                this.Probe = div;
                this.triggerLevel = 0f;
                this.coupling = Coupling.DC;
            }
        }


        [DataMember] private string id;

        [DataMember] public uint? succesfulDummyAndroidRuns;
        [DataMember] public DropNet.Models.UserLogin dropboxLogin;
        [DataMember] public bool? dropboxAuthenticated;
        [DataMember] public bool? firstRun;
        [DataMember] [AttributeSettingScopeTiming] public MainModes? mainMode;
        [DataMember] public bool? AutoUpdate;
        [DataMember] public bool? StoreToDropbox;
        [DataMember] [AttributeSettingScopeTiming] public RenderMode? RenderMode;
        [DataMember] [AttributeSettingVisual] public TickStickyNess? tickStickyNess;
        [DataMember] [AttributeSettingVisual] public GridAnchor? gridAnchor;
        [DataMember] [AttributeSettingVisual] public bool? SwitchAutomaticallyToRollingMode;
        [DataMember] [AttributeSettingScopeTiming] public AcquisitionBufferScalingBehaviour? acquistitionBufferScalingBehaviour;
        [DataMember] [AttributeSettingVisual] public int? WaveformThickness;
        [DataMember] [AttributeSettingVisual] public Dictionary<string, string> CustomWaveNames;
        [DataMember] [AttributeSettingScopeTiming] public TimeSpan? MeasurementAcquisitionTimespan;

        [DataMember] [AttributeSettingScopeTiming] public bool? PanoramaVisible;
        [DataMember] [AttributeSettingScopeTiming] public bool? HighBandwidthMode;
        [DataMember] [AttributeSettingMeasurements] public MeasurementBoxMode? MeasurementBoxMode;

        [DataMember] [AttributeSettingScopeTiming] public LinLog? fftVoltageScale;
        [DataMember] [AttributeSettingScopeTiming] public LinLog? fftFrequencyScale;
        [DataMember] [AttributeSettingScopeTiming] public Drawables.AnalogGraphCombo? analogGraphCombo;
        [DataMember] [AttributeSettingScopeTiming] public bool? xySquared;
        [DataMember] [AttributeSettingScopeTiming] public bool? xyInverted;
        [DataMember] [AttributeSettingScopeTiming] public DataProcessors.FFTWindow? fftWindowType;
        [DataMember] [AttributeSettingMeasurements] public List<SystemMeasurementType> activeSystemMeasurements;
        [DataMember] [AttributeSettingMeasurements] public Dictionary<string, List<string>> activeChannelMeasurements;
        [DataMember] [AttributeSettingMeasurements] public Dictionary<string, List<string>> requestedChannelGraphs;
        [DataMember] [AttributeSettingMeasurements] public List<SystemMeasurementType> requestedSystemGraphs;
        [DataMember] [AttributeSettingMeasurements] public TimeSpan? RecordingInterval;
        [DataMember] [AttributeSettingMeasurements] public int? RecordingAcquisitionsPerInterval;

        [DataMember] [AttributeSettingMeasurements] public Microsoft.Xna.Framework.Vector2 MeasurementBoxPosition;
        [DataMember] [AttributeSettingMeasurements] public bool? MeasurementBoxVisible;
        [DataMember] [AttributeSettingMeasurements] public bool? MainMenuVisible;
        [DataMember] [AttributeSettingMeasurements] public bool? WifiMenuVisible;
        [DataMember] [AttributeSettingMeasurements] public bool? MeasurementMenuVisible;
        [DataMember] [AttributeSettingScopeTiming] public bool? EnableETS;
        [DataMember] [AttributeSettingVisual] public ESuite.Scale? GuiScale;
        [DataMember] [AttributeSettingVisual] public ColorMapper.Mode? GuiColor;
        [DataMember] [AttributeSettingMeasurements] public DataStorage.StorageFileFormat? StorageFormat;
        [DataMember] [AttributeSettingScopeTiming] public Dictionary<String, ChannelSetting> ChannelSettings;
        [DataMember] [AttributeSettingScopeTiming] public Dictionary<String, AnalogChannelSetting> AnalogChannelSettings;
        [DataMember] [AttributeSettingScopeTiming] public Dictionary<String, bool> ChannelVisible;
        [DataMember] [AttributeSettingOperators] public List<ProcessorStateDescription> ProcessorStateDescriptions;

        [DataMember] [AttributeSettingScopeTiming] public string LastSelectedAnalogChannel;
        [DataMember] [AttributeSettingScopeTiming] public string LastSelectedDigitalChannel;
        [DataMember] [AttributeSettingScopeTiming] public ScopeMode? scopeMode;
        [DataMember] [AttributeSettingVisual] public Rectangle windowPositionAndSize;

        [DataMember] [AttributeSettingScopeTiming] public Dictionary<string, DigitalTriggerValue> TriggerDigital;
        [DataMember] [AttributeSettingScopeTiming] public Dictionary<string, DigitalTriggerValue> TriggerDigitalBeforeHiding;
        [DataMember] [AttributeSettingScopeTiming] public double? TriggerHoldoff;
        [DataMember] [AttributeSettingScopeTiming] public uint? AcquisitionDepthUserMaximum;
        [DataMember] [AttributeSettingScopeTiming] public double? TriggerPulseWidthMin;
        [DataMember] [AttributeSettingScopeTiming] public double? TriggerPulseWidthMax;
        [DataMember] [AttributeSettingScopeTiming] public TriggerMode? AnalogTriggerMode;
        [DataMember] [AttributeSettingScopeTiming] public TriggerMode? TriggerMode;
        [DataMember] [AttributeSettingScopeTiming] public TriggerSource? TriggerSource;
        [DataMember] [AttributeSettingScopeTiming] public TriggerEdge? TriggerEdge;
        const double ACQUISITION_DEPTH_DEFAULT = 0.001;
        [DataMember] [AttributeSettingScopeTiming] public double? acquisitionLength;
        [DataMember] [AttributeSettingScopeTiming] public AcquisitionMode? acquisitionMode;
        const double VIEWPORT_TIMESPAN_DEFAULT = ACQUISITION_DEPTH_DEFAULT / 2.0;
        [DataMember] [AttributeSettingScopeTiming] public double? ViewportTimespan;
        [DataMember] [AttributeSettingScopeTiming] public double? ViewportOffset;
        [DataMember] [AttributeSettingScopeTiming] public string AnalogTriggerChannel;        
        [DataMember] [AttributeSettingScopeTiming] public double? samplePeriod;
        [DataMember] [AttributeSettingScopeTiming] public string logicAnalyserChannel;
        [DataMember] [AttributeSettingScopeTiming] public bool? awgEnabled;
        [DataMember] [AttributeSettingScopeTiming] public SmartScope.DigitalOutputVoltage? digitalOutputVoltage;
        [DataMember][AttributeSettingVisual] public Dictionary<MappedColor, Color> WaveColorsNormal;
        [DataMember][AttributeSettingVisual] public Dictionary<MappedColor, Color> WaveColorsDark;
        const double SAMPLE_PERIOD_DEFAULT = 10e-9; //10ns

        //Private parameterless ctor reqd for deser
        private Settings() { }
        public Settings(string id) : this() { this.id = id; }

        #region defaults
        [System.Runtime.Serialization.OnDeserialized]
        void OnDeserialized(System.Runtime.Serialization.StreamingContext c)
        {
            SanityCheck();
        }
        void SanityCheck(bool reset = false)
        {
            if (reset || !succesfulDummyAndroidRuns.HasValue) succesfulDummyAndroidRuns = 0;
            if (reset || (windowPositionAndSize.Width == 0)) windowPositionAndSize = new Rectangle(0, 0, 1200, 800);
            if (reset || !dropboxAuthenticated.HasValue) dropboxAuthenticated = false;
            if (reset || !firstRun.HasValue) firstRun = true;
            if (reset || !mainMode.HasValue) mainMode = MainModes.Analog;
            if (reset || !AutoUpdate.HasValue) AutoUpdate = true;
            if (reset || !StoreToDropbox.HasValue) StoreToDropbox = false;
            if (reset || !SwitchAutomaticallyToRollingMode.HasValue) SwitchAutomaticallyToRollingMode = true;
            if (reset || !tickStickyNess.HasValue) tickStickyNess = TickStickyNess.Major;
            if (reset || !gridAnchor.HasValue) gridAnchor = GridAnchor.AcquisitionBuffer;
            if (reset || !acquistitionBufferScalingBehaviour.HasValue) acquistitionBufferScalingBehaviour = AcquisitionBufferScalingBehaviour.ViewportAnchoredToAcquisitionBuffer;
            if (reset || !PanoramaVisible.HasValue) PanoramaVisible = true;
            if (reset || !WaveformThickness.HasValue) WaveformThickness = 1;
            if (reset || !RenderMode.HasValue) RenderMode = ESuite.RenderMode.Immediate;
            if (reset || !MeasurementAcquisitionTimespan.HasValue) MeasurementAcquisitionTimespan = new TimeSpan(0, 15, 0);
            if (MeasurementAcquisitionTimespan.HasValue && MeasurementAcquisitionTimespan.Value > new TimeSpan(30, 0, 0)) MeasurementAcquisitionTimespan = new TimeSpan(0, 15, 0);
            if (reset || !MeasurementBoxMode.HasValue) MeasurementBoxMode = Drawables.MeasurementBoxMode.Floating;

            if (reset || !MainMenuVisible.HasValue) MainMenuVisible = true;
            if (reset || !MeasurementMenuVisible.HasValue) MeasurementMenuVisible = false;
            if (reset || !WifiMenuVisible.HasValue) WifiMenuVisible = false;
            if (reset || !EnableETS.HasValue) EnableETS = true;            
            if (reset || !GuiScale.HasValue) GuiScale = ESuite.Scale.Normal;
            if (reset || !GuiColor.HasValue) GuiColor = ColorMapper.Mode.NORMAL;
            if (reset || !StorageFormat.HasValue) StorageFormat = DataStorage.StorageFileFormat.CSV;
            if (reset || (ProcessorStateDescriptions == null)) ProcessorStateDescriptions = new List<ESuite.ProcessorStateDescription>();
            if (reset || !RecordingInterval.HasValue) RecordingInterval = new TimeSpan(0);
            if (reset || !RecordingAcquisitionsPerInterval.HasValue) RecordingAcquisitionsPerInterval = 1;

            int measurementBoxInitX = 100;
            int measurementBoxInitY = 100;
            if (reset || MeasurementBoxPosition == null) MeasurementBoxPosition = new Vector2(measurementBoxInitX, measurementBoxInitY);

            if (reset || MeasurementBoxVisible == null) MeasurementBoxVisible = false;

            if (reset || ChannelSettings == null) ChannelSettings = new Dictionary<string, ChannelSetting>();
            if (reset || !ChannelSettings.ContainsKey(AnalogChannel.ChA)) ChannelSettings[AnalogChannel.ChA] = new ChannelSetting(VOLTAGE_OFFSET_DEFAULT_CHA, VOLTAGE_RANGE_DEFAULT, true);
            if (reset || !ChannelSettings.ContainsKey(AnalogChannel.ChB)) ChannelSettings[AnalogChannel.ChB] = new ChannelSetting(VOLTAGE_OFFSET_DEFAULT_CHB, VOLTAGE_RANGE_DEFAULT, true);
            if (reset || ChannelSettings[AnalogChannel.ChA].offset == float.NaN) ChannelSettings[AnalogChannel.ChA].offset = VOLTAGE_OFFSET_DEFAULT_CHA;
            if (reset || ChannelSettings[AnalogChannel.ChB].offset == float.NaN) ChannelSettings[AnalogChannel.ChB].offset = VOLTAGE_OFFSET_DEFAULT_CHB;

            if (reset || fftVoltageScale == null) fftVoltageScale = LinLog.Linear;
            if (reset || fftFrequencyScale == null) fftFrequencyScale = LinLog.Logarithmic;
            if (reset || fftWindowType == null) fftWindowType = DataProcessors.FFTWindow.BlackmanHarris;
            if (reset || analogGraphCombo == null) analogGraphCombo = Drawables.AnalogGraphCombo.Analog;
            if (reset || xySquared == null) xySquared = false;
            if (reset || xyInverted == null) xyInverted = false;
            if (reset || HighBandwidthMode == null) HighBandwidthMode = false;

            if (reset || ChannelVisible == null) ChannelVisible = new Dictionary<String, bool>();
            if (reset || !ChannelVisible.ContainsKey("0")) ChannelVisible.Add("0", true);
            if (reset || !ChannelVisible.ContainsKey("1")) ChannelVisible.Add("1", true);
            if (reset || !ChannelVisible.ContainsKey("2")) ChannelVisible.Add("2", true);
            if (reset || !ChannelVisible.ContainsKey("3")) ChannelVisible.Add("3", true);
            if (reset || !ChannelVisible.ContainsKey("4")) ChannelVisible.Add("4", true);
            if (reset || !ChannelVisible.ContainsKey("5")) ChannelVisible.Add("5", true);
            if (reset || !ChannelVisible.ContainsKey("6")) ChannelVisible.Add("6", true);
            if (reset || !ChannelVisible.ContainsKey("7")) ChannelVisible.Add("7", true);
            if (reset || !ChannelVisible.ContainsKey("A")) ChannelVisible.Add("A", true);
            if (reset || !ChannelVisible.ContainsKey("B")) ChannelVisible.Add("B", true);

            if (reset || AnalogChannelSettings == null) AnalogChannelSettings = new Dictionary<string, AnalogChannelSetting>();
            if (reset || !AnalogChannelSettings.ContainsKey(AnalogChannel.ChA)) AnalogChannelSettings[AnalogChannel.ChA] = new AnalogChannelSetting(VOLTAGE_RANGE_DEFAULT, Probe.DefaultX10Probe);
            if (reset || !AnalogChannelSettings.ContainsKey(AnalogChannel.ChB)) AnalogChannelSettings[AnalogChannel.ChB] = new AnalogChannelSetting(VOLTAGE_RANGE_DEFAULT, Probe.DefaultX10Probe);
            if (reset || AnalogChannelSettings[AnalogChannel.ChA].range == double.NaN) AnalogChannelSettings[AnalogChannel.ChA].range = VOLTAGE_RANGE_DEFAULT;
            if (reset || AnalogChannelSettings[AnalogChannel.ChB].range == double.NaN) AnalogChannelSettings[AnalogChannel.ChB].range = VOLTAGE_RANGE_DEFAULT;
            if (reset || LastSelectedAnalogChannel == null) LastSelectedAnalogChannel = AnalogChannel.ChA.Name;
            if (reset || LastSelectedDigitalChannel == null) LastSelectedDigitalChannel = DigitalChannel.Digi0.Name;
            if (reset || activeChannelMeasurements == null)
            {
                //by default: add mean measurements of both analog channels
                activeChannelMeasurements = new Dictionary<string, List<string>>();
                activeChannelMeasurements.Add("A", new List<string>());
                activeChannelMeasurements["A"].Add(typeof(MeasurementMeanVoltage).AssemblyQualifiedName);
                activeChannelMeasurements.Add("B", new List<string>());
                activeChannelMeasurements["B"].Add(typeof(MeasurementMeanVoltage).AssemblyQualifiedName);
            }
            if (reset || activeSystemMeasurements == null) activeSystemMeasurements = new List<Measurements.SystemMeasurementType>();
            if (reset || CustomWaveNames == null) CustomWaveNames = new Dictionary<string, string>();
            if (reset || requestedSystemGraphs == null) requestedSystemGraphs = new List<Measurements.SystemMeasurementType>();
            if (reset || requestedChannelGraphs == null)
            {
                requestedChannelGraphs = new Dictionary<string, List<string>>();
                requestedChannelGraphs.Add("A", new List<string>());
                requestedChannelGraphs.Add("B", new List<string>());
            }

            if (reset || !scopeMode.HasValue) scopeMode = ScopeMode.ScopeTime;

            if (reset || TriggerDigital == null) TriggerDigital = new Dictionary<string, DigitalTriggerValue>();
            foreach (DigitalChannel dc in DigitalChannel.List)
                if (reset || !TriggerDigital.ContainsKey(dc)) TriggerDigital[dc] = DigitalTriggerValue.X;
            if (reset || TriggerDigitalBeforeHiding == null) TriggerDigitalBeforeHiding = new Dictionary<string, DigitalTriggerValue>();
            foreach (DigitalChannel dc in DigitalChannel.List)
                if (reset || !TriggerDigitalBeforeHiding.ContainsKey(dc)) TriggerDigitalBeforeHiding[dc] = DigitalTriggerValue.X;

            if (reset || !AcquisitionDepthUserMaximum.HasValue) AcquisitionDepthUserMaximum = 512 * 1024;
            if (reset || !TriggerHoldoff.HasValue) TriggerHoldoff = 0.0;
            if (reset || !TriggerMode.HasValue) TriggerMode = LabNation.DeviceInterface.Devices.TriggerMode.Edge;
            if (reset || !AnalogTriggerMode.HasValue) AnalogTriggerMode = LabNation.DeviceInterface.Devices.TriggerMode.Edge;
            if (reset || AnalogTriggerChannel == null) AnalogTriggerChannel = AnalogChannel.ChA;
            if (reset || !TriggerEdge.HasValue) TriggerEdge = LabNation.DeviceInterface.Devices.TriggerEdge.RISING;
            if (reset || !TriggerSource.HasValue) TriggerSource = LabNation.DeviceInterface.Devices.TriggerSource.Channel;
            if (reset || !TriggerPulseWidthMax.HasValue) TriggerPulseWidthMax = 100e-6; //random 100us default
            if (reset || !TriggerPulseWidthMin.HasValue) TriggerPulseWidthMin = 10e-6; //random 10us default

            if (reset || !acquisitionLength.HasValue) acquisitionLength = ACQUISITION_DEPTH_DEFAULT;
            if (reset || !acquisitionMode.HasValue) acquisitionMode = AcquisitionMode.AUTO;
            if (reset || !ViewportTimespan.HasValue) ViewportTimespan = VIEWPORT_TIMESPAN_DEFAULT;

            if (reset || !ViewportOffset.HasValue) ViewportOffset = (acquisitionLength.Value - ViewportTimespan) / 2.0;

            if (reset || !samplePeriod.HasValue) samplePeriod = SAMPLE_PERIOD_DEFAULT;

            if (reset || logicAnalyserChannel == null) logicAnalyserChannel = null;
            if (reset || !awgEnabled.HasValue) awgEnabled = false;
            if (reset || !digitalOutputVoltage.HasValue) digitalOutputVoltage = SmartScope.DigitalOutputVoltage.V3_0;

            if (reset || WaveColorsNormal == null)
            {
                WaveColorsNormal = new Dictionary<MappedColor, Color>();
                foreach (var kvp in ColorMapperLight.DefaultWaveColors)
                    WaveColorsNormal.Add(kvp.Key, kvp.Value);
            }
            if (reset || WaveColorsDark == null)
            {
                WaveColorsDark = new Dictionary<MappedColor, Color>();
                foreach (var kvp in ColorMapperDark.DefaultWaveColors)
                    WaveColorsDark.Add(kvp.Key, kvp.Value);
            }
        }
        #endregion

        #region Default/load/save
        private static Settings currentSettings;
        public static Settings Current
        {
            get
            {
                if (currentSettings == null)
                    throw new Exception("no settings loaded");
                return currentSettings;
            }
        }
        public static Settings CurrentRuntime { get { return Current; } }
        public static string IntersessionSettingsId { get { return "Intersession"; } }
        public static string AudioscopeSettingsId { get { return "AudioScope"; } }

        public static bool SaveExists(string id)
        {
            return File.Exists(SettingsFilename(id));
        }

        public static bool SaveCurrent(string id, IScope scope)
        {
            //in case of audioscope: don't save settings!
            if (scope is DummyScope)
            {
                DummyScope dummy = scope as DummyScope;
                if (dummy.isAudio)
                    return false;
            }

            try
            {
                var serializer = new DataContractSerializer(typeof(Settings));
                using (var sw = new System.IO.FileStream(SettingsFilename(id), FileMode.Create, FileAccess.Write))
                {
                    using (var writer = new XmlTextWriter(sw, new System.Text.UTF8Encoding(false)))
                    {
                        writer.Formatting = Formatting.Indented; // indent the Xml so it's human readable
                        serializer.WriteObject(writer, currentSettings);
                        writer.Flush();
                    }
                    sw.Close();
                }
            }
            catch (Exception e) {
                LabNation.Common.Logger.Error("Failed to save settings with id " + id +". Exception: " + e.Message);
                return false;
            }
            return true;
        }

        public static void Load(string id, Type attributeToLoad)
        {
            var serializer = new DataContractSerializer(typeof(Settings));
            try
            {
                using (var sw = new System.IO.StreamReader(SettingsFilename(id), new System.Text.UTF8Encoding(false)))
                {
                    //load full settings file
                    Settings settingsFromFile = (Settings)(serializer.ReadObject(sw.BaseStream));

                    if (currentSettings == null) //at startup
                    {
                        currentSettings = settingsFromFile;
                    }
                    else if (attributeToLoad == null) //at runtime but no specific group specified; just load all
                    {
                        currentSettings = settingsFromFile;
                    }
                    else //reloading settings at runtime with specific group specified
                    {
                        //browse through all fields, and forward only those with attribute to currentSettings
                        var fields = settingsFromFile.GetType().GetFields();
                        foreach (var f in fields)
                        {
                            bool toForward = false;
                            if (Attribute.GetCustomAttributes(f, attributeToLoad).Length > 0)
                                toForward = true;

                            if (toForward)
                            {
                                object o = f.GetValue(settingsFromFile);
                                if (o != null)
                                    f.SetValue(currentSettings, o);
                            }
                        }
                    }

                    LabNation.Common.Logger.Info("Succesfully loaded settings from file with id " + id);

                    if (observers.Count > 0)
                        foreach (var applySettingsDelegate in observers)
                            applySettingsDelegate(currentSettings);
                }
            }
            catch (Exception e)
            {
                LabNation.Common.Logger.Error("Failed to restore settings with id " + id + ". Exception: " + e.Message);
                LabNation.Common.Logger.Error("Stack: " + e.StackTrace);
                if (currentSettings != null)
                {
                    //if loading failed, but an existing config was present -> do nothing
                }
                else
                {
                    //if loading failed, and there's no backup config (first run) -> create default config
                    Settings s = new Settings(id);
                    s.SanityCheck();
                    currentSettings = s;
                }
            }
        }

        public void Reset()
        {
            this.SanityCheck(true);
        }

        public static List<string> RetrieveCustomConfigFiles()
        {
            string[] fullPaths = Directory.GetFiles(LabNation.Common.Utils.ApplicationDataPath, SettingsFileNamePrefix + "*" + SettingsFileNameSuffix);
            List<string> customSettingFiles = new List<string>();
            for (int i = 0; i < fullPaths.Length; i++)
            {
                string[] splits = fullPaths[i].Split('\\');         //for win systems
                string filename = splits[splits.Length - 1];
                splits = filename.Split('/');                       //for unix systems
                filename = splits[splits.Length - 1];
                fullPaths[i] = filename.Replace(SettingsFileNamePrefix, "").Replace(SettingsFileNameSuffix, "");

                if (fullPaths[i] != IntersessionSettingsId && fullPaths[i] != AudioscopeSettingsId)
                    customSettingFiles.Add(fullPaths[i]);
            }
            return customSettingFiles;
        }

        private static string SettingsFileNamePrefix { get { return  "Settings_"; } }
        private static string SettingsFileNameSuffix { get { return ".xml"; } }
        private static string SettingsFilename(string id)
        {
            return Path.Combine(LabNation.Common.Utils.ApplicationDataPath, Path.Combine(LabNation.Common.Utils.ApplicationDataPath, SettingsFileNamePrefix + id + SettingsFileNameSuffix));
        }
        #endregion
    }
}

