using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using LabNation.Interfaces;
using Microsoft.Xna.Framework;



#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;
using ESuite.DataStorage;
using ESuite.Measurements;
using ESuite.DataProcessors;
using ESuite.Drawables;
using System.Threading;

namespace ESuite
{
    [Flags]
    public enum ScopeMode
    {
        ScopeTime,
        ScopeFrequency,
        LogicAnalyser,
        Any = ScopeTime | ScopeFrequency | LogicAnalyser
    };

    internal enum LinLog
    {
        Linear,
        Logarithmic,
    };

    internal partial class UIHandler
    {
        # region variables and fields

        EMainEngine engine;

        internal DropboxStorage dropboxStorage = null;

        // Decoders
        private CompositionContainer compositionContainer;
        private bool saveSettingsOnExit = true;

        double voltagePrecision = ColorMapper.voltagePrecision;
        double averagedVoltagePrecision = ColorMapper.averagedVoltagePrecision;
        double timePrecision = ColorMapper.timePrecision;
        double cursorTimePrecision = ColorMapper.cursorTimePrecision;

        private bool ShowPanoramaWhenScopeStops = false;
        private ScopeDataCollection currentDataCollection = null;

        private float MIN_VOLTAGE_PER_DIVISION = 0.00f; //loaded from Settings at startup
        const float MAX_VOLTAGE_PER_DIVISION = 5f;
        const int FORCE_TRIGGER_FRAMES_SELECTED = 10;
        int keepForceTriggerSelectedForThisManyFrames = 0;
        int previousEtsBooster = 0;
        bool ForceTriggerButtonDisabled { get { return Settings.CurrentRuntime.acquisitionMode == AcquisitionMode.AUTO || scope.Rolling || (scope.Running && !(scope.Armed && scope.AwaitingTrigger)) || !scope.Running; } }

        // Measurements & processors
        DataProcessorSincTriggering sincProcessor;
        DataProcessorETS etsProcessor;
        DataProcessorDifferenceDetector differenceProcessor;

        // Misc
        bool canPerformVerticalScaling = true;
        LinLog frequencyModeTimeScale = LinLog.Linear;
        private DateTime lastTimeSettingsSaved;

        ScopeDataCollection lastScopeData = null;
        DateTime stopPendingStartTime, recordingStartTime;
        bool stopPendingWasBlinking = false;

        //FIXME: no expose plz
        internal ScopeApp scopeApp;
        Dictionary<string, DigitalTriggerValue> TriggerDigitalDefault = new Dictionary<string, DigitalTriggerValue>();

        const string SITE_URL =
#if DEBUG
            "192.168.0.164:3000";
#else
			"www.lab-nation.com";
#endif

        Version currentVersion
        {
            get
            {
                return
#if ANDROID
				new Version(context.PackageManager.GetPackageInfo (context.PackageName, 0).VersionName);
#elif IOS
            	new Version(Foundation.NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString").ToString());
#else
                System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
#endif
            }
        }
        Queue<DrawableCallback> callbackQueue;
        private MeasurementManager measurementManager;
        private DataProcessorMeasurements measurementDataProcessor;

        #endregion

        #region Constructor, initializer and cleaner

        public UIHandler(ScopeApp xnaController
#if WINDOWS && DEBUG
        , DeviceConnectHandler connectHandler
#endif
        )
        {
            lastTimeSettingsSaved = DateTime.Now;
            UICallbacks.uiHandler = this;
            this.scopeApp = xnaController;
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (o, certificate, chain, errors) => true;
            this.engine = new EMainEngine();

            measurementManager = new Measurements.MeasurementManager();
            measurementDataProcessor = new DataProcessorMeasurements(measurementManager);
            measurementManager.MeasurementDataProcessor = measurementDataProcessor;

            sincProcessor = new DataProcessorSincTriggering(this.triggerValue);
            differenceProcessor = new DataProcessorDifferenceDetector(measurementDataProcessor);
            etsProcessor = new DataProcessorETS(differenceProcessor);
            differenceProcessor.Init(etsProcessor);
            engine.dataProcessors.Add(measurementDataProcessor);
            engine.dataProcessors.Add(sincProcessor);
            engine.dataProcessors.Add(differenceProcessor);
            engine.dataProcessors.Add(etsProcessor);

            InitializeMeasurements();

            foreach (DigitalChannel dc in DigitalChannel.List)
                TriggerDigitalDefault[dc] = DigitalTriggerValue.X;

            this.callbackQueue = new Queue<DrawableCallback>();
            this.dropboxStorage = new DropboxStorage(this);
#if WINDOWS && DEBUG
            scopeConnectHandler += connectHandler;
#endif
            xnaController.OnUpdate += Update;
        }
#if ANDROID
        internal Android.Content.Context context;
#endif
        public EDrawable Initialize(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice
#if ANDROID
            , Android.Content.Context context
#endif
            )
        {
#if ANDROID
            this.context = context;
#endif

            //First create all GUI elements
            EDrawable ui = BuildUI();

            //position here, as it influences some UI elements (eg: usb/wifi icon) which need to be instantiated first
            scopeConnectHandler += OnDeviceConnect;
            InitializeScope(
#if ANDROID
                context,
#endif
                OnInterfaceChange, scopeConnectHandler);

            //then this one, as it requires both UI elements and scope elements
            InitializeCallbacks();

            //Then load settings
            ApplySettings(Settings.Current);
            Settings.ObserveMe(this.ApplySettings);

            //Finally initialize stuff which is not dependant on Settings
            if (!Utils.IsFirstRun() && HelpImage.Visible)
                ToggleHelp();
            InitProbes(); //should probably just save and recall to/from Settings
            LoadDecoders(); //preferably after BuildUI, so the debuglog is shown on the on-screen log

            //Set children matrices after all initialization, ensuring that all GUI elements are scaled and positioned correctly
            ui.SetBoundaries(Utils.ScreenBoundaries(graphicsDevice, Matrix.Identity, ui.View, ui.Projection));

            return ui;
        }

        private void InitWaveforms()
        {
            //add waveforms. to be done after GUI has been finished; and after settings have been loaded
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                if (!Waveform.Waveforms.ContainsKey(ch))
                {
                    float probeGain = ch.Probe.Gain;
                    gm.AddWaveform(ch, MIN_VOLTAGE_PER_DIVISION * probeGain, MAX_VOLTAGE_PER_DIVISION * probeGain);
                }
            }
            foreach (DigitalChannel ch in DigitalChannel.List)
            {
                if (!Waveform.Waveforms.ContainsKey(ch))
                {
                    gm.AddWaveform(ch, 0, 0);
                    intervalsVisibility.Add(ch, IntervalsVisibility.WhenActive);
                }
            }
        }

        public void ApplySettings(Settings settings)
        {
            measurementManager.ReloadStoredMeasurements();

            SetHighBandwidthMode(settings.HighBandwidthMode.Value);

            EnableETS(settings.EnableETS.Value);
            InvertXY(settings.xyInverted.Value);
            SquareXY(settings.xySquared.Value);
            ShowSideMenu(settings.MainMenuVisible.Value, false);
            ShowMeasurementMenu(settings.MeasurementMenuVisible.Value, false);
#if WINDOWS || MONOMAC || LINUX || ANDROID
            SetAutoUpdate(settings.AutoUpdate.Value);
#endif
            ShowPanorama(settings.PanoramaVisible.Value); //this needs to be before any other GUI init; as otherwise they will override the stored PanoramaVisible by false
            fileTypeDropDown.SelectItemByTag(settings.StorageFormat);
            SetMeasurementsTimespan(settings.MeasurementAcquisitionTimespan.Value); //needs to be called before building the main menu
            fileTypeDropDown.SelectItemByTag(settings.StorageFormat);

            //Finally finish off configuring all GUI elements
            InitWaveforms();

            InitializeMainGrid();
            if (!scope.Rolling)
                SetAcquisitionMode(AcquisitionMode);
            UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);

            LoadProcessorSettings();
            SetMeasurementBoxMode(settings.MeasurementBoxMode.Value);
        }

        private List<Probe> knownProbes = new List<Probe>();
        private void InitProbes()
        {
            knownProbes.Add(Probe.DefaultX1Probe);
            knownProbes.Add(Probe.DefaultX10Probe);

            List<Probe> probesFromFile = LoadProbesFromFile();
            foreach (Probe p in probesFromFile)
            {
                //provision allowing to save offset to default probes
                if (p.Name == Probe.DefaultX1Probe.Name && p.Unit == Probe.DefaultX1Probe.Unit)
                    Probe.DefaultX1Probe.ChangeOffset(p.Offset);
                else if (p.Name == Probe.DefaultX10Probe.Name && p.Unit == Probe.DefaultX10Probe.Unit)
                    Probe.DefaultX10Probe.ChangeOffset(p.Offset);
                else
                    AddProbe(p);
            }
        }

        internal void UpdateProbe(Probe origProbe, string name, double gain, double offset, string unit)
        {
            //if probe is no longer in list
            if (!knownProbes.Contains(origProbe))
                return;

            //if one of the default probes -> return
            if (origProbe == Probe.DefaultX1Probe || origProbe == Probe.DefaultX10Probe)
            {
                if (origProbe.Name != name || origProbe.Unit != unit || origProbe.Gain != gain)
                {
                    ShowSimpleToast("Can only change offset of a built-in probe", 5000);
                    return;
                }
            }

            //remove and add new
            knownProbes.Remove(origProbe);
            Probe newProbe = new Probe(name, unit, (float)gain, (float)offset, false);
            knownProbes.Add(newProbe);

            //if old probe was active on any channel -> replace with new probe which automatically arranges divgainstage etc
            foreach (var ch in AnalogChannel.List)
                if (ch.Probe == origProbe)
                    this.SetProbeDivision(ch, newProbe);

            SaveProbesToFile(knownProbes);
            RebuildSideMenu();
        }

        //trigger might need to be refreshed; eg when probe is inverted.
        internal void RefreshTrigger()
        {
            SetTrigger(triggerValue);
        }

        internal void RemoveProbe(Probe probe)
        {
            //if probe is no longer in list
            if (!knownProbes.Contains(probe))
                return;

            //if probe is currently used on a channel -> return
            foreach (var ch in AnalogChannel.List)
            {
                Probe currentProbe = ch.Probe;
                if (currentProbe == probe)
                {
                    ShowSimpleToast("Cannot remove probe " + probe.Name + " [" + probe.Unit + "] as it is currently active on channel " + ch.Name, 5000);
                    return;
                }
            }

            //if one of the default probes -> return
            if (probe == Probe.DefaultX1Probe || probe == Probe.DefaultX10Probe)
            {
                ShowSimpleToast("Cannot remove probe " + probe.Name + " [" + probe.Unit + "] as it's built in", 5000);
                return;
            }

            knownProbes.Remove(probe);
            SaveProbesToFile(knownProbes);
            ShowSimpleToast("Probe " + probe.Name + " [" + probe.Unit + "] removed", 5000);
            RebuildSideMenu();
        }

        internal void AddProbe(Probe probe)
        {
            if (float.IsNaN(probe.Gain) || float.IsNegativeInfinity(probe.Gain) || float.IsPositiveInfinity(probe.Gain)
                || float.IsNaN(probe.Offset) || float.IsNegativeInfinity(probe.Offset) || float.IsPositiveInfinity(probe.Offset))
            {
                ShowSimpleToast("Probe definition failed -- divide by 0", 5000);
                return;
            }

            foreach (Probe p in knownProbes)
            {
                if (p.Name == probe.Name && p.Unit == probe.Unit)
                {
                    ShowSimpleToast("A probe named " + probe.Name + " with unit " + probe.Unit + " has already been defined", 5000);
                    return;
                }
            }
            knownProbes.Add(probe);
            SaveProbesToFile(knownProbes);
            RebuildSideMenu();
        }

        void InitializeCallbacks()
        {
            //Bind callbacks
            graphManager.SelectorTriggerVerPos.BindSlideCallback(UICallbacks.ChangeTriggerLevel);
            graphManager.SelectorTriggerVerPos.BindTapCallback(UICallbacks.TriggerLevelIndicatorClicked);
            graphManager.SelectorTriggerVerPos.BindDoubleTapCallback(UICallbacks.VerTriggerDoubleClicked);
            graphManager.SelectorTriggerVerPos.BindHoldCallback(UICallbacks.TriggerLevelIndicatorClicked);

            graphManager.SelectorTriggerHorPos.BindSlideCallback(UICallbacks.ChangeTriggerHoldoffRelativeToViewport);
            graphManager.SelectorTriggerHorPos.BindTapCallback(UICallbacks.TriggerHoldoffIndicatorClicked);
            graphManager.SelectorTriggerHorPos.BindDoubleTapCallback(UICallbacks.HorTriggerDoubleClicked);
            graphManager.SelectorTriggerHorPos.BindHoldCallback(UICallbacks.TriggerHoldoffIndicatorClicked);
            graphManager.SelectorTriggerHorPos.BindDropCallback(UICallbacks.TriggerIndicatorDropped);

            startButton.TapCallback = new DrawableCallback(UICallbacks.ToggleRunning);
            forceTriggerButton.TapCallback = new DrawableCallback(UICallbacks.ForceTrigger);
            recordButton.TapCallback = new DrawableCallback(UICallbacks.ToggleRecord);
            screenshotButton.TapCallback = new DrawableCallback(UICallbacks.ScreenCapture);

            //Init calls here, as main screen boundaries will be 0,0 rectangle, but UI elements need to be created
            RebuildSideMenu();
            RebuildMeasurementMenu();
            RebuildWifiMenu(false, null);

            panoramaSplitter.PanoramaShading.OnScaleViewport = new DrawableCallback(UICallbacks.PanZoomViewportFromPanorama);
            panoramaSplitter.PanoramaShading.OnScalePanorama = new DrawableCallback(UICallbacks.PanZoomPanoramaFromPanorama);
            panoramaSplitter.PanoramaShading.OnDoubleTap = new DrawableCallback(UICallbacks.TogglePanoramaByUser);
            gm.Graphs[GraphType.Analog].Grid.PinchDragCallback = new DrawableCallback(UICallbacks.PanAndZoomGrid);
            gm.Graphs[GraphType.Analog].Grid.PinchDragEndCallback = new DrawableCallback(UICallbacks.PanAndZoomGridEnd);
            gm.Graphs[GraphType.Analog].Grid.DragEndCallback = new DrawableCallback(UICallbacks.WaveDragEnd);
            gm.Graphs[GraphType.Analog].Grid.TapCallback = new DrawableCallback(UICallbacks.GridClicked);
            gm.Graphs[GraphType.Analog].Grid.DoubleTapCallback = new DrawableCallback(UICallbacks.DoubleTapGrid);

            gm.Graphs[GraphType.Digital].Grid.PinchDragCallback = new DrawableCallback(UICallbacks.PanAndZoomGrid);
            gm.Graphs[GraphType.Digital].Grid.PinchDragEndCallback = new DrawableCallback(UICallbacks.PanAndZoomGridEnd);
            gm.Graphs[GraphType.Digital].Grid.DragEndCallback = new DrawableCallback(UICallbacks.WaveDragEnd);
            gm.Graphs[GraphType.Digital].Grid.TapCallback = new DrawableCallback(UICallbacks.GridClicked);
            gm.Graphs[GraphType.Digital].Grid.DoubleTapCallback = new DrawableCallback(UICallbacks.DoubleTapGrid);

            gm.Graphs[GraphType.Frequency].Grid.PinchDragCallback = new DrawableCallback(UICallbacks.PanAndZoomGrid);
            gm.Graphs[GraphType.Frequency].Grid.PinchDragEndCallback = new DrawableCallback(UICallbacks.PanAndZoomGridEnd);
            gm.Graphs[GraphType.Frequency].Grid.DragEndCallback = new DrawableCallback(UICallbacks.WaveDragEnd);
            gm.Graphs[GraphType.Frequency].Grid.TapCallback = new DrawableCallback(UICallbacks.GridClicked);
            gm.Graphs[GraphType.Frequency].Grid.DoubleTapCallback = new DrawableCallback(UICallbacks.DoubleTapGrid);

            HelpImage.TapCallback = new DrawableCallback((s, a) =>
            {
                UICallbacks.ToggleHelp(s, a);
                QueueCallback(
                    UICallbacks.ShowToast,
                    new object[] { "You can recall the help message by pressing F1 or clicking the help button up there", 3000 },
                    false);
            });

            helpButton.TapCallback = new DrawableCallback(UICallbacks.ToggleHelp);
            etsButton.TapCallback = new DrawableCallback(UICallbacks.EnableETS, DataProcessors.DataProcessorETS.Disable);
        }
        void InitializeMeasurements()
        {
            if (scope != null)
                (measurementManager.SystemMeasurements[SystemMeasurementType.DataSourceFrameRate] as MeasurementDataSourceFrameRate).UpdateSource(scope.DataSourceScope);
            else
                (measurementManager.SystemMeasurements[SystemMeasurementType.DataSourceFrameRate] as MeasurementDataSourceFrameRate).UpdateSource(null);
            if (scope is SmartScope)
                (measurementManager.SystemMeasurements[SystemMeasurementType.SampleRate] as MeasurementSampleRate).UpdateSource(scope as SmartScope);
        }

        public void AutoSpaceDigiWaves(Channel channelToExclude)
        {
            //first sort all visible waves according to YOffset
            Dictionary<Channel, Waveform> chansSortedByYOffset = gm.Graphs[GraphType.Digital].EnabledWaveformsVisible.OrderBy(x => -Waveform.Waveforms[x.Key].VoltageOffset / Waveform.Waveforms[x.Key].VoltageRange).ToDictionary(x => x.Key, x => x.Value);

            //get all digichans, sorted by name
            List<KeyValuePair<Channel, Waveform>> chansFinalSorted = gm.Graphs[GraphType.Digital].EnabledWaveformsVisible.Where(x => x.Key is DigitalChannel).OrderBy(x => x.Key.Name).ToList();

            //next, inject decoders at correct location
            //List<KeyValuePair<Channel, Waveform>> decoderChannels = chansSortedByYOffset.Where(x => x.Key is ProcessorChannel).ToList();
            for (int i = 0; i < chansSortedByYOffset.Count; i++)
                if (!(chansSortedByYOffset.ElementAt(i).Key is DigitalChannel))
                    chansFinalSorted.Insert(i, chansSortedByYOffset.ElementAt(i));

            //next: space them equally
            for (int i = 0; i < chansFinalSorted.Count; i++)
            {
                KeyValuePair<Channel, Waveform> kvp = chansFinalSorted.ElementAt(i);

                //map YPos between 0 and 1
                if ((kvp.Key != channelToExclude) && !(kvp.Key is AnalogChannel))
                {
                    float newYPos = 1f / (float)(chansFinalSorted.Count * 2) * (float)(i * 2 + 1) - 0.5f;
                    newYPos *= (float)kvp.Value.VoltageRange; //digiwaves have 1V VoltageRange, so this line is required for other waves
                    SetYOffset(kvp.Key, -newYPos);
                }

                //scale digital signals so they fill the screen nicely
                if (kvp.Value is WaveformDigital)
                {
                    WaveformDigital digiWave = (WaveformDigital)kvp.Value;
                    digiWave.RelHeight = 1f / (float)(chansFinalSorted.Count + 2); //2 added to keep some spacing between channels
                }
            }
        }
        public void Cleanup()
        {
            if (saveSettingsOnExit)
                Settings.SaveCurrent(Settings.IntersessionSettingsId, scope);

            StopScope();
            if (scope is IDisposable)
            {
                (scope as IDisposable).Dispose();
            }
            deviceManager.Stop();
#if !IOS
            while (webclient.IsBusy)
            {
                webclient.CancelAsync();
                Thread.Sleep(10);
            }
            if (AutoUpdateTimer != null)
            {
                AutoUpdateTimer.Dispose();
                AutoUpdateTimer = null;
            }

#endif
        }

        #endregion

        #region Protocol decoders, buses and math
        internal void ChangeFFTWindowFunction(FFTWindow window)
        {
            Settings.Current.fftWindowType = window;
        }
        internal DataProcessorDecoder AddProcessor(IProcessor pluginProcessor, GraphType graphType, bool showContextMenuAndStore)
        {
            Graph graph = gm.Graphs[graphType];

            /* find first available indexer */
            //fetch which number to paste after name as identifier
            int index = 0;
            List<KeyValuePair<Channel, Waveform>> existingPluginProcessors = Waveform.EnabledWaveforms.Where(x => x.Value.Channel is ProcessorChannel).ToList();
            while (true)
            {
                List<KeyValuePair<Channel, Waveform>> channelWithThisIndex = existingPluginProcessors.Where(x => x.Key.Value == index).ToList();
                if (channelWithThisIndex.Count == 0) break;
                index++;
            }

            //create processor, called each update to decode waves into array of DecoderOutputs
            DataProcessorDecoder internalProcessor = new DataProcessorDecoder(pluginProcessor, index, lastScopeData);

            //try to bind existing channels to the required inputs of the decoder, taking types into account
            if (internalProcessor.BindExistingChannelsToInput())
            {
                //show channel
                internalProcessor.Process(lastScopeData, true);
                double storedRange = 1;
                if (Settings.Current.ChannelSettings.Keys.Contains(internalProcessor.dataProcessorChannel))
                    storedRange = Settings.Current.ChannelSettings[internalProcessor.dataProcessorChannel].range; //need to make backup here, as EnableChannel for a ProcessorChannel sets its voltageRange to 1V, hence overriding the value stored in Settings!
                EnableChannel(internalProcessor.dataProcessorChannel, true, false, graph);
                if (Settings.Current.ChannelSettings.Keys.Contains(internalProcessor.dataProcessorChannel))
                    Settings.Current.ChannelSettings[internalProcessor.dataProcessorChannel].range = storedRange; //put back orig value, see above
                graph.UpdateGridDivLabelPositions();
                CloseMenusOnGraphArea();
                if (showContextMenuAndStore)
                    ShowMenuChannel(internalProcessor.dataProcessorChannel);
            }
            else
            {
                CloseMenusOnGraphArea();

                //kill channel so its Process method doesn't get called anymore
                DestroyChannel(internalProcessor.dataProcessorChannel);

                //report error
                ShowToast("NO_VALID_INPUT_CHANNELS_FOR_DECODER", null, null, Color.White, "This decoder requires more/different input channels than currently available", Location.Center, Location.Center, Location.Center, 5000);

                return null;
            }

            if (internalProcessor.dataProcessorChannel is OperatorDigitalChannel)
                intervalsVisibility.Add(internalProcessor.dataProcessorChannel, IntervalsVisibility.WhenActive);
            AutoSpaceDigiWaves(null);
            UpdateUiRanges(graph.Grid); //to make sure the time-origin has been aligned to viewport
            UpdateOffsetIndicatorNames();

            if (showContextMenuAndStore)
                SaveProcessorSettings();

            return internalProcessor;
        }

        //this method should be called:
        //- on exit
        //- on each Save
        //- on each change to processors
        //  - adding processors             OK  (AddProcessor)
        //  - removing processor            OK  (DestroyChannel)
        //  - changing input channel        OK  (SetDecoderSourceChannel)
        //  - changing parameter            OK  (SetDecoderParameter)
        //  - changing radix                OK  (SetDecoderRadix)
        internal void SaveProcessorSettings()
        {
            Settings.Current.ProcessorStateDescriptions = new List<ProcessorStateDescription>();

            //for each active Waveform: see if it's a processor; if so: extract all required information and store in Settings
            foreach (var kvp in Waveform.Waveforms)
            {
                if (kvp.Key is ProcessorChannel)
                {
                    DataProcessorDecoder decoder = (kvp.Key as ProcessorChannel).decoder;
                    IProcessor processor = decoder.Processor;

                    string processorName = processor.GetType().AssemblyQualifiedName;
                    Dictionary<string, object> parameterValues = decoder.ParameterValuesCopy;
                    Dictionary<string, Channel> sourceChannelMap = decoder.SourceChannelMapCopy;
                    Dictionary<string, string> sourceChannelTypes = sourceChannelMap.ToDictionary(x => x.Key, x => x.Value == null ? "null" : x.Value.GetType().AssemblyQualifiedName); //source channels can be null
                    Dictionary<string, string> sourceChannelNames = sourceChannelMap.ToDictionary(x => x.Key, x => x.Value == null ? "null" : x.Value.Name);                            //source channels can be null
                    GraphType graphType = kvp.Value.Graph.Grid.GraphType;

                    //for processors: store radix
                    RadixType radix = RadixType.Hex;
                    if (kvp.Key is ProtocolDecoderChannel)
                    {
                        ProtocolDecoderChannel decoderChannel = (ProtocolDecoderChannel)kvp.Key;
                        radix = decoderChannel.RadixType;
                    }

                    Settings.Current.ProcessorStateDescriptions.Add(new ProcessorStateDescription() { ProcessorName = processorName, ParameterValue = parameterValues, SourceChannelTypes = sourceChannelTypes, SourceChannelNames = sourceChannelNames, Radix = radix, GraphType = graphType });
                }
            }
        }

        //Called when
        //- program starts                                  OK
        //- config is loaded from file                      OK
        //- user changes between Analog and Digital mode    OK
        internal void LoadProcessorSettings()
        {
            //first kill all existing processors
            List<ProcessorChannel> existingProcessorChannels = new List<ProcessorChannel>();
            foreach (var kvp in Waveform.Waveforms)
                if (kvp.Key is ProcessorChannel)
                    existingProcessorChannels.Add(kvp.Key as ProcessorChannel);
            for (int i = existingProcessorChannels.Count - 1; i >= 0; i--)
                DestroyChannel(existingProcessorChannels[i]);

            //then add according to settings
            foreach (var procDesc in Settings.Current.ProcessorStateDescriptions)
            {
                //if current graph is not the correct one: skip to next processor
                bool graphActive = false;
                foreach (var graph in gm.ActiveGraphs)
                    if (graph.Grid.GraphType == procDesc.GraphType)
                        graphActive = true;

                if (graphActive)
                {
                    //init and add processor
                    Type procType = Type.GetType(procDesc.ProcessorName);
                    IProcessor decoder = (IProcessor)Activator.CreateInstance(procType);

                    DataProcessorDecoder processor = AddProcessor(decoder, procDesc.GraphType, false);

                    if (processor != null)
                    {
                        //config all input channels
                        foreach (var inChan in procDesc.SourceChannelNames)
                        {
                            string paramName = inChan.Key;
                            string chanName = inChan.Value;
                            string chanTypeStr = procDesc.SourceChannelTypes[paramName];
                            Type chanType = Type.GetType(chanTypeStr);

                            if (chanName == "null" && chanTypeStr == "null") //support for nullable channels
                            {
                                UpdateDecoderSourceChannel(processor as DataProcessorDecoder, paramName, null);
                            }
                            else
                            {
                                foreach (var kvp in Waveform.Waveforms)
                                    if (kvp.Key.GetType() == chanType)
                                        if (kvp.Key.Name == chanName)
                                            UpdateDecoderSourceChannel(processor as DataProcessorDecoder, paramName, kvp.Key);
                            }
                        }

                        //config all parameters
                        foreach (var param in procDesc.ParameterValue)
                            UpdateDecoderParameter(processor, param.Key, param.Value);

                        //config radix
                        if (processor.dataProcessorChannel is ProtocolDecoderChannel)
                            (processor.dataProcessorChannel as ProtocolDecoderChannel).RadixType = procDesc.Radix;

                        if (Settings.Current.ChannelSettings.Keys.Contains(processor.dataProcessorChannel))
                        {
                            //set range
                            SetVerticalRange(processor.dataProcessorChannel, Settings.Current.ChannelSettings[processor.dataProcessorChannel].range);

                            //set yoffset
                            SetYOffset(processor.dataProcessorChannel, Settings.Current.ChannelSettings[processor.dataProcessorChannel].offset);
                        }
                    }
                }
            }
        }

        internal void UpdateDecoderParameter(DataProcessorDecoder processorDecoder, string parameter, object value)
        {
            processorDecoder.UpdateParameterValues(parameter, value);
            //In case channel inputs changed
            UpdateOffsetIndicatorNames();
        }

        internal void UpdateDecoderSourceChannel(DataProcessorDecoder decoderProcessor, string sourcename, Channel ch)
        {
            decoderProcessor.UpdateSourceChannel(sourcename, ch);
            UpdateOffsetIndicatorNames();
        }

        internal void AddMathChannel()
        {
            if (MathChannel.List.Count >= 2)
            {
                ShowScreenWideDialog(
                    "Can't handle more than 2 math channels, sorry!",
                    new List<ButtonInfo>() { new ButtonInfo("OK", UICallbacks.HideDialog) });
                return;

            }
            DataProcessorMath proc = new DataProcessorMath();
            EnableChannel(proc.channel);
            UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
        }
        internal void AddFFTChannel(AnalogChannel ch)
        {
            if (FFTChannel.List.Where(x => x.analogChannel == ch).ToList().Count > 0)
                return;

            DataProcessorFFT proc = new DataProcessorFFT(ch);
            EnableChannel(proc.channel, true, false, graphManager.Graphs[GraphType.Frequency]);
        }
        internal void UpdateMathChannelOperation(MathChannel ch, DataProcessorMath.Operation operation)
        {
            ch.processor.operation = operation;
        }
        internal void SetFFTVoltageAxis(LinLog scale)
        {
            Settings.Current.fftVoltageScale = scale;
            gm.Graphs[GraphType.Frequency].Grid.VoltageScale = scale;
            gm.Graphs[GraphType.Frequency].Grid.DefineSceneContents();
        }
        internal void SetFFTFrequencyAxis(LinLog scale)
        {
            Settings.Current.fftFrequencyScale = scale;
            gm.Graphs[GraphType.Frequency].Grid.TimeScale = scale;
            gm.Graphs[GraphType.Frequency].Grid.DefineSceneContents();

            var fftWaves = Waveform.EnabledWaveforms.Where(x => x.Key is FFTChannel).ToDictionary(x => x.Key, x => x.Value);
            foreach (var kvp in fftWaves)
                (kvp.Value as WaveformFreq).Scale = scale;
        }
        internal void HWAutoArrangeStart()
        {
            if (!scope.Running)
            {
                ShowSimpleToast("Cannot do this now -- scope must be running first", 2000);
                return;
            }

            var progressDialog = ShowProgressDialog("Determining wave properties and adjusting GUI ...", 0);
            ShowPanorama(false);

            //stop datasource and perform auto-magic
            scope.DataSourceScope.Stop();
            new Thread(new ThreadStart(() =>
            {
                Dictionary<AnalogChannel, LabNation.DeviceInterface.AnalogWaveProperties> waveProperties =
                    LabNation.DeviceInterface.Tools.MeasureAutoArrangeSettings(scope, AnalogChannel.ChA,
                                (progress) =>
                                {
                                    progressDialog.Progress = progress;
                                });
                this.QueueCallback((EDrawable e, object arg) => { HWAutoArrangeFinish(waveProperties); });
            })).Start();

        }
        internal void HWAutoArrangeFinish(Dictionary<AnalogChannel, LabNation.DeviceInterface.AnalogWaveProperties> waveProperties)
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // VERTICAL == CHANNELS

            if (Waveform.EnabledWaveformsVisible.Where(x => x.Key is OperatorAnalogChannel).ToList().Count > 0)
            {
                //if analogProcessorWaves are present -> just split up vertical area
                AutoArrange();
            }
            else
            {
                //if no analogProcessorWaves are present: set all waves to measured specs
                int waveCounter = 0;
                foreach (var kvp in waveProperties)
                {
                    float offset = kvp.Value.offset;
                    float range = kvp.Value.amplitude;

                    //in this case, the signal is small and offset is large. pin indicator to grid and make wave float
                    if (range < Math.Abs(offset))
                    {
                        range = Math.Abs(3f * offset);
                        offset = 0;
                    }
                    else //in this case, the amplitude is large. nail lowvoltage to grid.
                    {
                        range = 3f * range;
                        offset = -kvp.Value.minValue;
                    }

                    //aestatic correction: in case of flatline, make offset 0 so it aligns to grid division and set to 1V/div
                    if (kvp.Value.isFlatline)
                    {
                        offset = 0;
                        range = (float)(1 * Drawables.Grid.DivisionsVerticalMax);
                    }

                    double clippedVDiv = Utils.getRoundDivisionRange(range / Drawables.Grid.DivisionsVerticalMax, Utils.RoundDirection.Up);
                    float probeGain = (float)Math.Abs(kvp.Key.Probe.Gain);
                    if (clippedVDiv < MIN_VOLTAGE_PER_DIVISION * probeGain) clippedVDiv = MIN_VOLTAGE_PER_DIVISION * probeGain;
                    if (clippedVDiv > MAX_VOLTAGE_PER_DIVISION * probeGain) clippedVDiv = MAX_VOLTAGE_PER_DIVISION * probeGain;

                    if (waveCounter++ == 1)
                        offset -= (float)(3f / 8f * clippedVDiv * Drawables.Grid.DivisionsVerticalMax);

                    SetVerticalRange(kvp.Key, clippedVDiv * 8.0);
                    SetYOffset(kvp.Key, offset);
                }
            }

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // HORIZONTAL == TIMEBASE

            AnalogChannel slowestChannel = AnalogChannel.ChA;
            AnalogChannel fastestChannel = AnalogChannel.ChA;
            foreach (var kvp in waveProperties)
            {
                if (!kvp.Value.isFlatline && kvp.Value.frequency < waveProperties[slowestChannel].frequency)
                    slowestChannel = kvp.Key;
                if (!kvp.Value.isFlatline && kvp.Value.frequency > waveProperties[fastestChannel].frequency)
                    fastestChannel = kvp.Key;
            }

            //trigger on slowest signal
            AnalogChannel triggerChannel = slowestChannel;
            double triggerFreq = waveProperties[slowestChannel].frequency;
            if (triggerFreq < waveProperties[fastestChannel].frequency / 10.0) //if slowest signal is MUCH slower than fastest signal, only use the slow signal for triggering but use timescale of fast signal
                triggerFreq = waveProperties[fastestChannel].frequency / 10.0;

            int periodsOnScreen = 5;
            float viewFinderAcquisitionRatio = 0.5f;
            double acquisitionLength = 1.0 / triggerFreq * (double)periodsOnScreen / viewFinderAcquisitionRatio;

            //in case of 2 flatlines: set timescale so updates are frequent
            if (waveProperties.Where(x => x.Value.isFlatline).ToList().Count == waveProperties.Count)
                acquisitionLength = 0.01f;

            //forward timesettings to scope
            SetAcquisitionLength(acquisitionLength, Settings.Current.SwitchAutomaticallyToRollingMode.Value);
            SetTriggerHoldoff(0);
            SetViewportCenterAndTimespan(0, acquisitionLength * viewFinderAcquisitionRatio);
            SetTriggerAnalogChannel(triggerChannel);
            SetTriggerAnalogLevel(waveProperties[triggerChannel].offset);

            scope.DataSourceScope.Start();

            HideDialog();
        }

        #endregion

        #region Draw / update
#if ANDROID
        internal void RateApp()
        {
            Thread t = new Thread(RateAppStart);
            t.Start();
        }
        private void RateAppStart()
        {
            string url = "https://play.google.com/store/apps/details?id=" + context.PackageName;
            var uri = Android.Net.Uri.Parse(url);
            var intent = new Intent(Intent.ActionView, uri);
            context.StartActivity(intent);
            return;
        }        
#endif

        public void AcquisitionTransferFinishedHandler(IScope scope, EventArgs e)
        {
            scope.SuspendViewportUpdates = true;
        }
        bool GetBlinkState(DateTime origin, double interval = 1000)
        {
            double y = (DateTime.Now - origin).TotalMilliseconds;
            int x = (int)((y % interval) / (interval / 2.0));
            return x == 0;
        }
        private bool shouldStillIncrementSuccesfulRuns = true;
        private void SaveSettingsInterval()
        {
            TimeSpan interval = new TimeSpan(0, 1, 0); //save settings every 1min
            int nbrRunsToAskForReview = 10;

            if (DateTime.Now.Subtract(lastTimeSettingsSaved) > interval)
            {
#if ANDROID
                //mechanism which only triggers RateReview when the app has been started 10 times, where the first minute no real SmartScope was attached
                if (shouldStillIncrementSuccesfulRuns)
                {
                    shouldStillIncrementSuccesfulRuns = false;
                    
                    Settings.Current.succesfulDummyAndroidRuns++;

                    if (Settings.Current.succesfulDummyAndroidRuns == nbrRunsToAskForReview)
                    {
                        UICallbacks.AskForPlayStoreReview(null, null);
                    }
                }                
#endif

                Settings.SaveCurrent(Settings.IntersessionSettingsId, scope);
                lastTimeSettingsSaved = DateTime.Now;
            }
        }
        void Update(object o, EventArgs e)
        {
            while (callbackQueue.Count > 0)
                callbackQueue.Dequeue().Call();

            SaveSettingsInterval();

            List<AnalogChannel> enabledAnalogChannels = new List<AnalogChannel>(new AnalogChannel[] { AnalogChannel.ChA, AnalogChannel.ChB }); //FIXME: needs generalized code;
            differenceProcessor.UpdateGUIValues(enabledAnalogChannels);
            etsProcessor.UpdateGUIValues(enabledAnalogChannels, ViewportTimespan);
            engine.Update();

            measurementManager.Update();
            panoramaSplitter.MeasurementBox.UpdateMeasurements(measurementManager.ActiveSystemMeasurements, measurementManager.ActiveChannelMeasurements);

            //update recording filesize -- thread-safe
            RecordingScope currentRecording = engine.Recording;
            if (currentRecording != null)
                (measurementManager.SystemMeasurements[SystemMeasurementType.StorageFileSize] as Measurements.MeasurementStorageMemorySize).UpdateValueInternal(currentRecording.DataStorageSize);

            //FIXME: not sure if this is the best place?
            gm.Graphs[GraphType.Analog].SDivLabel.Value = graphManager.Graphs[GraphType.Analog].Grid.DivisionHorizontal.DivisionRange;
            gm.Graphs[GraphType.Digital].SDivLabel.Value = graphManager.Graphs[GraphType.Digital].Grid.DivisionHorizontal.DivisionRange;

            // update ETS button contents
            etsButton.Visible = etsProcessor.ETSEffective || (DataProcessorETS.Disable && etsProcessor.EquivalentSamplingBoost > 1);
            if (etsProcessor.EquivalentSamplingBoost != previousEtsBooster)
            {
                previousEtsBooster = etsProcessor.EquivalentSamplingBoost;

                string str = LabNation.Common.Utils.siRound(etsProcessor.EquivalentSamplingRate);

                List<ButtonTextDefinition> etsOnText = new List<ButtonTextDefinition>();
                etsOnText.Add(new ButtonTextDefinition(str + "S/s", VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Tiny));
                etsOnText.Add(new ButtonTextDefinition("ETS", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Small));

                etsButton.enabledDefinitions = etsOnText;       //set text when not displayed
                if (etsButton.Selected)
                    etsButton.stringManager.Strings = etsOnText;    //set text when displayed                
            }

            /* Generate blink bool so all blinking items are synced */
            graphManager.SelectorTriggerHorPos.Visible = !scope.Rolling;
            graphManager.SelectorTriggerVerPos.Visible = !scope.Rolling && Settings.CurrentRuntime.mainMode != MainModes.Digital;// triggerSource == TriggerSource.Channel;
            panoramaSplitter.PanoramaShading.TriggerVisible = !scope.Rolling;

            forceTriggerButton.disabled = ForceTriggerButtonDisabled;
            startButton.Selected = scope.Running && !scope.StopPending;

            if (scope.Running && (scope.StopPending || scope.AwaitingTrigger))
            {
                //blink the start button
                if (!stopPendingWasBlinking)
                {
                    stopPendingStartTime = DateTime.Now;
                    stopPendingWasBlinking = true;
                }
                if (scope.AwaitingTrigger)
                {
                    forceTriggerButton.Highlighted = scope.Armed ? GetBlinkState(stopPendingStartTime) : true;
                    startButton.disabled = scope.StopPending;
                    startButton.Visible = true;
                }
                else
                {
                    startButton.Visible = !GetBlinkState(stopPendingStartTime);
                    startButton.disabled = false;
                    forceTriggerButton.Highlighted = false;
                }
            }
            else
            {
                forceTriggerButton.Highlighted = false;
                startButton.Visible = true;
                startButton.disabled = false;
                stopPendingWasBlinking = false;
            }

            if (!scope.Running && ShowPanoramaWhenScopeStops)
            {
                ShowPanoramaWhenScopeStops = false;
                ShowPanorama(true);
            }

            connectedIndicator.Selected = scope.Ready && !(scope is DummyScope);
#if WINDOWS
            if (deviceManager.BadDriver)
            {
                ShowToast("BadDriver", connectedIndicator, "indicator-usb", Color.Red, "Smartscope detected but failed accessing it\n - Make sure only 1 SmartScope app is running, or\n - Install driver from Menu > System > Install driver", Location.Top, Location.Right, Location.Left);
            }
            else
            {
                HideToast("BadDriver");
            }
#endif

            if (engine.RecordingBusy)
            {
                recordButton.Selected = GetBlinkState(recordingStartTime);
            }
            else
                recordButton.Selected = false;

            //process last scopeData (signal conditioning and measurements)
            if (engine.ScopeData != null)
            {
                //update main datapointer
                currentDataCollection = engine.ScopeData;

                //update panorama progress shading
                if ((scope is DummyScope) && (scope as DummyScope).isFile)
                {
                    DummyInterfaceFromFile fileIf = (scope as DummyScope).HardwareInterface as LabNation.DeviceInterface.Hardware.DummyInterfaceFromFile;
                    if (panoramaSplitter.PanoramaShading.AcquisitionFetchProgress != fileIf.RelativeFilePosition)
                        panoramaSplitter.PanoramaShading.AcquisitionFetchProgress = fileIf.RelativeFilePosition;
                }
                else
                {
                    if (panoramaSplitter.PanoramaShading.AcquisitionFetchProgress != currentDataCollection.FullAcquisitionFetchProgress)
                        panoramaSplitter.PanoramaShading.AcquisitionFetchProgress = currentDataCollection.FullAcquisitionFetchProgress;
                }
            }
            UpdateWaveformData(currentDataCollection);

            //Do automatic triggering and auto arranging
            HandleAutoTriggerAndArrangement();

            //Commit settings to scope
            if (scope.Ready)
                scope.CommitSettings();

            //FIXME: do this fallback in the button itself - implement a property that
            //makes the button fallback to not selected after a certain time
            if (forceTriggerButton.Selected && keepForceTriggerSelectedForThisManyFrames == 0)
                keepForceTriggerSelectedForThisManyFrames = FORCE_TRIGGER_FRAMES_SELECTED;
            else if (keepForceTriggerSelectedForThisManyFrames > 0)
                keepForceTriggerSelectedForThisManyFrames--;

            if (keepForceTriggerSelectedForThisManyFrames == 0)
                forceTriggerButton.Selected = false;
        }

        void UpdateWaveformData(ScopeDataCollection dataCollection)
        {
            //If we have data from LA, parse it into bool arrays
            if (dataCollection == null)
                return;

            double acquisitionOffset;
            double triggerMovedSinceAcquisititon;
            double leftSideSampleTimeInScopeTimeAxis;

            triggerMovedSinceAcquisititon = TriggerHoldoff - dataCollection.HoldoffCenter;
            acquisitionOffset = (dataCollection.AcquisitionLength - AcquisitionLength) / 2;
            leftSideSampleTimeInScopeTimeAxis = acquisitionOffset + ViewportOffset - triggerMovedSinceAcquisititon;

            //update all active measurment waveforms. Called code contains protection so nothing happens when source data is not changed
            foreach (var kvp in activeMeasurementGraphs)
                (kvp.Value.Waveforms.First().Value as WaveformMeasurement).UpdateVertices();

            foreach (KeyValuePair<Channel, Waveform> kvp in Waveform.EnabledWaveforms)
            {
                if (kvp.Key is XYChannel)
                {
                    XYChannel chan = kvp.Key as XYChannel;
                    ChannelData dCh1 = dataCollection.GetBestData(chan.analogChannelX);
                    ChannelData dCh2 = dataCollection.GetBestData(chan.analogChannelY);
                    kvp.Value.UpdateData(new ChannelData[] { dCh1, dCh2 });
                    kvp.Value.TimeOffset = leftSideSampleTimeInScopeTimeAxis;
                }
                else
                {
                    ChannelData etsVoltagesCD = dataCollection.GetData(ChannelDataSourceProcessor.ETSVoltages, kvp.Key);
                    if (etsVoltagesCD != null)
                    {
                        kvp.Value.UpdateData(new ChannelData[] { etsVoltagesCD }, dataCollection.GetData(ChannelDataSourceProcessor.ETSTimestamps, kvp.Key));
                        kvp.Value.TimeOffset = leftSideSampleTimeInScopeTimeAxis;
                    }
                    else
                    {
                        ChannelData d = dataCollection.GetBestData(kvp.Key);
                        kvp.Value.UpdateData(new ChannelData[] { d });
                        kvp.Value.TimeOffset = leftSideSampleTimeInScopeTimeAxis + dataCollection.TriggerAdjustment;
                    }


                    if (kvp.Value.PanoramaWave != null && PanoramaVisible)
                    {
                        kvp.Value.PanoramaWave.UpdateData(new ChannelData[] { dataCollection.GetData(ChannelDataSourceScope.Overview, kvp.Key) });
                        kvp.Value.PanoramaWave.TimeOffset = acquisitionOffset - triggerMovedSinceAcquisititon;
                    }

                    //FIXME: shouldn't be done each update cycle
                    //update V/div labels
                    if (selectedChannel != null && graphManager.Graphs[GraphType.Analog].EnabledWaveforms.ContainsKey(selectedChannel))
                    {
                        double value = kvp.Key == selectedChannel ? graphManager.Graphs[GraphType.Analog].Grid.DivisionVertical.DivisionRange : graphManager.Graphs[GraphType.Analog].Grid.DivisionVertical.DivisionRange / Waveform.Waveforms[selectedChannel].VoltageRange * Waveform.Waveforms[kvp.Key].VoltageRange;
                        graphManager.Graphs[GraphType.Analog].UpdateChannelDivisionLabel(kvp.Key, value);
                    }

                }

                //FIXME: this section needs to be outside this foreach loop??
                var fftWaves = Waveform.EnabledWaveforms.Where(x => x.Key is FFTChannel).ToDictionary(x => x.Key, x => x.Value);
                if (fftWaves.Count > 0)
                {
                    //update FFT grid based on sampleFreq and #samples
                    ChannelData analogData = dataCollection.GetBestData((fftWaves.First().Key as FFTChannel).processor.analogChannel);
                    if (analogData != null)
                    {
                        gm.Graphs[GraphType.Frequency].Grid.NyquistFrequency = (float)(1.0 / analogData.samplePeriod) / 2f;
                        gm.Graphs[GraphType.Frequency].Grid.NumberOfBins = analogData.array.Length / 2;

                        foreach (var fftWave in fftWaves)
                        {
                            (fftWave.Value as WaveformFreq).SampleFrequency = (float)(1.0f / analogData.samplePeriod);
                        }
                    }
                }
            }

            //if streaming from file, and file contains only 1 dataset: arrange data and stop scope
            if (scope.Running && (scope is DummyScope) && ((scope as DummyScope).HardwareInterface is DummyInterfaceFromFile) && ((scope as DummyScope).HardwareInterface as DummyInterfaceFromFile).NrWaveforms == 1)
            {
                //need to make sure filedata has already been transfered from dummyfilescope into waveforms
                if (((scope as DummyScope).HardwareInterface as DummyInterfaceFromFile).AcquisitionLenght == dataCollection.AcquisitionLength)
                {
                    ShowAllEnabledChannels();
                    AutoArrange();
                    QueueCallback(new DrawableCallback(UICallbacks.SetRunning, false));
                }
            }

        }



        #endregion

        #region Callback queueing 

        public void QueueCallback(DrawableCallback callback, bool blocking = false)
        {
            if (callback == null) return;
            this.callbackQueue.Enqueue(callback);
            if (blocking)
            {
                while (callbackQueue.Contains(callback))
                {
                    System.Threading.Thread.Sleep(10);
                }
            }

        }
        public void QueueCallback(DrawableCallbackDelegate del, object argument = null, bool blocking = false)
        {
            if (del == null) return;
            this.QueueCallback(new DrawableCallback(del, argument), blocking);
        }

        #endregion
    }
}
