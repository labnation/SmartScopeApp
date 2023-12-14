using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;


using Microsoft.Xna.Framework;

using LabNation.Common;


#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.Hardware;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

using ESuite.Measurements;
using ESuite.DataProcessors;
using ESuite.Drawables;


namespace ESuite
{
    internal partial class UIHandler
    {
        #region fields
        Timer AutoUpdateTimer;
        const int AUTO_UPDATE_INTERVAL = 24 * 60 * 60 * 1000; //1 day
        private DeviceManager deviceManager;
        internal IScope scope;
        IWaveGenerator waveGenerator;
        DeviceConnectHandler scopeConnectHandler;
        AnalogChannel AnalogTriggerChannel { 
            get { 
                return Settings.Current.AnalogTriggerChannel; } 
            set { 
                Settings.Current.AnalogTriggerChannel = value; } }
        float AnalogTriggerLevel
        {
            get { return Settings.Current.AnalogChannelSettings[AnalogTriggerChannel].triggerLevel; }
            set { Settings.Current.AnalogChannelSettings[AnalogTriggerChannel].triggerLevel = value; }
        }
        double TriggerPulseWidthMin
        {
            get {
                return Settings.Current.TriggerPulseWidthMin.Value;
            }
            set { Settings.Current.TriggerPulseWidthMin = value; }
        }
        double TriggerPulseWidthMax
        {
            get {
                return Settings.Current.TriggerPulseWidthMax.Value;
            }
            set { Settings.Current.TriggerPulseWidthMax = value; }
        }
        
        TriggerEdge TriggerEdge
        {
            get { return Settings.Current.TriggerEdge.Value; }
            set { Settings.Current.TriggerEdge = value; }
        }
        Dictionary<string, DigitalTriggerValue> TriggerDigital {
            get { return Settings.Current.TriggerDigital;  } 
            set { Settings.Current.TriggerDigital = value; } 
        }
        AcquisitionMode AcquisitionMode { get { return Settings.Current.acquisitionMode.Value; } set { Settings.Current.acquisitionMode = value; } }

        // Auto trigger and arrange
        bool autoTrigger = false;
        List<float> automaticTriggerChannelAverages = new List<float>();
        bool autoArrangeAfterAutoTrigger = false;
        bool readyForAutoTrigger = false;
        DataProcessorSmartScope smartscopeProcessor;
        #endregion

        void InitializeScope(
            #if ANDROID
                Android.Content.Context context,
            #endif
            InterfaceChangeHandler interfaceChangeHandler, DeviceConnectHandler scopeConnectHandler)
        {
            smartscopeProcessor = new DataProcessorSmartScope();
            engine.dataProcessors.Add(smartscopeProcessor);

            deviceManager = new DeviceManager(
#if ANDROID
                        context,
#endif
interfaceChangeHandler, scopeConnectHandler);

            deviceManager.Start(false);
            if(deviceManager.ZeroconfFailure != null)
            {
                //Callback to notify missing DNSSD
                Logger.Warn("Failed to initialise zeroconf. Network scopes won't be detected.\n" + deviceManager.ZeroconfFailure.Message);
            }
        }

        bool logicAnalyserChannelWasEnabledBeforeSelectingIt = true;

        bool logicAnalyserEnabled { get { return this.logicAnalyserChannel != null; } }
        AnalogChannel logicAnalyserChannel
        {
            get { 
                return (AnalogChannel)Settings.Current.logicAnalyserChannel; 
            }
            set
            {
                //Disable logic analyser - restore state of sacrificed channel
                if(value == null && logicAnalyserChannel != null)
                    EnableChannel(logicAnalyserChannel, logicAnalyserChannelWasEnabledBeforeSelectingIt);
                
                //Enabling logic analyser - store state of channel being sacrificed & disable it
                if (value != null) 
                {
                    logicAnalyserChannelWasEnabledBeforeSelectingIt = WaveformAnalog.EnabledWaveforms.ContainsKey(value);
                    if (triggerMode != TriggerMode.Digital && AnalogTriggerChannel == value)
                    {
                        AnalogTriggerChannel = (AnalogChannel)WaveformAnalog.EnabledWaveformsVisible.Keys.Where(x => x is AnalogChannel).ToList().Next(value, 1);
                        SetTrigger();
                    }
                    EnableChannel(value, false);
                }
                Settings.Current.logicAnalyserChannel = value;
                scope.ChannelSacrificedForLogicAnalyser = value;
            }
        }

        internal void ConfigureScope()
        {
            CloseMenusOnGraphArea();

            logicAnalyserChannel = this.logicAnalyserChannel;

            ApplySettingsToScope(Settings.Current);
            Settings.ObserveMe(this.ApplySettingsToScope);            
        }

        private void ApplySettingsToScope(Settings settings)
        {
            bool isAudioScope = false;
            if (scope is DummyScope)
            {
                DummyScope dummy = scope as DummyScope;
                if (dummy.isAudio)
                {
                    isAudioScope = true;
                }
            }

            //Save trigger settings before moving into configuring all channels
            //as that configuration may screw with the limits and modify
            //the trigger.
            TriggerValue triggerFromSettings = new TriggerValue()
            {
                channel = AnalogTriggerChannel,
                edge = TriggerEdge,
                level = AnalogTriggerLevel,
                mode = triggerMode,
                source = triggerSource,
                Digital = TriggerDigital.ToDictionary(x => (DigitalChannel)x.Key, x => (DigitalTriggerValue)x.Value),
                pulseWidthMax = settings.TriggerPulseWidthMax.Value,
                pulseWidthMin = settings.TriggerPulseWidthMin.Value
            };

            if (isAudioScope)
            {
                //dirty override for ultimate beginners
                Settings.Current.ChannelSettings[AnalogChannel.ChA].offset = 0;
                Settings.Current.AnalogChannelSettings[AnalogChannel.ChA].range = 0.05 * (double)Grid.DivisionsVerticalMax; //set to default of 50mV/div, which seems to be OK for showing audio waves
                SetProbeDivision(AnalogChannel.ChA, Probe.DefaultX1Probe);
                SetYOffset(AnalogChannel.ChA, 0);
                triggerFromSettings.channel = AnalogChannel.ChA;
                triggerFromSettings.level = 0;
                ViewportTimespan = 0.001 * (double)Grid.DivisionsHorizontalMax;
                AcquisitionLength = ViewportTimespan * 2.0;
                TriggerHoldoff = 0;
            }

            //Configure analog channels
			foreach (AnalogChannel ch in AnalogChannel.List) {
				SetProbeDivision (ch, Settings.Current.AnalogChannelSettings [ch].Probe);
                SetChannelInvert(ch, Settings.Current.AnalogChannelSettings[ch].Invert);
                SetChannelCoupling (ch, Settings.Current.AnalogChannelSettings [ch].coupling);
				//Store desired yoffset before calling SetVerticalRange as it can adjust the yoffset
				float yOffset = Settings.Current.ChannelSettings [ch].offset;
				SetVerticalRange (ch, Settings.Current.AnalogChannelSettings [ch].range);
				SetYOffset (ch, yOffset);
			}

            SetTrigger(triggerFromSettings);
            GeneratorSetDigitalVoltage(settings.digitalOutputVoltage.Value);

            //Save settings so propagation of previously set settings
            //don't change the defaults
            double holdoff = TriggerHoldoff;
            double vpCenter = ViewportCenter;
            double vpTimeSpan = ViewportTimespan;

            if (isAudioScope)
                vpCenter = 0; //set VP in middle of AcquisitionBuffer          

            SetAcquisitionLength(AcquisitionLength, Settings.Current.SwitchAutomaticallyToRollingMode.Value);
            SetViewportCenterAndTimespan(vpCenter, vpTimeSpan);
            SetTriggerHoldoff(TriggerHoldoff);
            SetAcquisitionDepthUserMaximum(settings.AcquisitionDepthUserMaximum.Value);
            if (!scope.Rolling)
                SetAcquisitionMode(AcquisitionMode);
            ShowPanorama(PanoramaEnabledPreference);
            RebuildSideMenu();
        }

        #region trigger

        TriggerSource triggerSource
        {
            get { return Settings.CurrentRuntime.TriggerSource.Value; }
            set { Settings.CurrentRuntime.TriggerSource = value; }
        }

        //TriggerMode in analog mode, used to select formerly used mode
        //when switching from digital triggering back to analog
        TriggerMode analogTriggerMode { 
            get { return Settings.CurrentRuntime.AnalogTriggerMode.Value; }
            set { Settings.CurrentRuntime.AnalogTriggerMode = value; } 
        }
        TriggerMode triggerMode { 
            get { return Settings.CurrentRuntime.TriggerMode.Value; } 
            set { 
                Settings.CurrentRuntime.TriggerMode = value;
                if (triggerMode != TriggerMode.Digital)
                {
                    graphManager.SelectorTriggerVerPos.Muted = false;
                    foreach (var kvp in WaveformDigital.Waveforms)
                        if (kvp.Value.TriggerIndicator != null) //only real digiwaves have triggers. eg digital operators not. this check is more safe
                            kvp.Value.TriggerIndicator.Muted = true;
                }
                else
                {
                    graphManager.SelectorTriggerVerPos.Muted = true;
                    foreach (var kvp in WaveformDigital.Waveforms)
                        if (kvp.Key is DigitalChannel) //only digiwaves have trigger indicator
                            kvp.Value.TriggerIndicator.Muted = false;
                }
            }
 
        }

        internal void SetTriggerOnSelectedChannel()
        {
            if (selectedChannel is AnalogChannel)
            {
                SetTriggerAnalogChannel((AnalogChannel)selectedChannel);
                CloseMenusOnGraphArea();
            }
        }
        internal void SetTriggerAnalogChannel(AnalogChannel channel)
        {
            AnalogTriggerChannel = channel;
            SetTrigger();
        }
        internal void MoveTriggerAnalogLevelRelative(float rate)
        {
            SetTriggerAnalogLevel(AnalogTriggerLevel + rate * (float)Waveform.Waveforms[AnalogTriggerChannel].VoltageRange);
        }
        internal void SetTriggerAnalogLevelRelative(float arg)
        {
            SetTriggerAnalogLevel((float)arg * (float)Waveform.Waveforms[AnalogTriggerChannel].VoltageRange - Waveform.Waveforms[AnalogTriggerChannel].VoltageOffset);
        }
        internal void SetTriggerAnalogLevel(float level)
        {
            AnalogTriggerLevel = level;
            SetTrigger();

            etsProcessor.RequestETSReset();
        }
        internal void SetTriggerPulseWidthMin(double min)
        {
            TriggerPulseWidthMin = min;
            SetTrigger();
        }
        internal void SetTriggerPulseWidthMax(double max)
        {
            TriggerPulseWidthMax = max;
            SetTrigger();
        }

        internal void SetTriggerEdge(TriggerEdge edge)
        {
            TriggerEdge = edge;
            SetTrigger();
        }

        TriggerValue triggerValue
        {
            get
            {
                Dictionary<DigitalChannel, DigitalTriggerValue> d = new Dictionary<DigitalChannel, DigitalTriggerValue>();
                foreach (var kvp in TriggerDigital)
                    d.Add((DigitalChannel)kvp.Key, kvp.Value);

                return new TriggerValue()
                    {
                        mode = triggerMode,
                        source = triggerSource,
                        channel = AnalogTriggerChannel,
                        Digital = d,
                        edge = TriggerEdge,
                        level = AnalogTriggerLevel,
                        pulseWidthMax = TriggerPulseWidthMax,
                        pulseWidthMin = TriggerPulseWidthMin
                    };
            }
        }

        void SetTrigger()
        {
            SetTrigger(triggerValue);
        }
        internal void SetTrigger(TriggerValue value)
        {
			//in case of audioscope: throw TeasingToast and recall with correct value argument
			if (scope is DummyScope) {
				DummyScope dummy = scope as DummyScope;
				if (dummy.isAudio) {
					if (value.source == TriggerSource.External) {						
						ShowSimpleToast ("A SmartScope is required for external triggering.", 3000);
						value.source = TriggerSource.Channel;
						SetTrigger (value);
						return;
					} else if (value.source == TriggerSource.Channel && value.channel == AnalogChannel.ChB) {			
						ShowSimpleToast ("A SmartScope is required for scoping multiple channels.", 3000);
						value.channel = AnalogChannel.ChA;
						SetTrigger (value);
						return;
					} else if (value.mode != TriggerMode.Edge) {			
						ShowSimpleToast ("A SmartScope is required for pulse/timeout triggering", 3000);
						value.mode = TriggerMode.Edge;
						SetTrigger (value);
						return;
					}
				}
			}

            //Store analog mode for when we switch back from digital
            //to analog triggering
            if (value.mode != TriggerMode.Digital)
                analogTriggerMode = value.mode;

            scope.TriggerValue = value;
            //readback
            /*TriggerValue v = scope.TriggerValue;*/

            triggerSource = value.source;
            triggerMode = value.mode;
            AnalogTriggerLevel = value.level;
            AnalogTriggerChannel = value.channel;
            TriggerEdge = value.edge;

            //Update the sinc processor with the UI value
            sincProcessor.triggerValue = this.triggerValue;

            //Update digital trigger indicators
            foreach (DigitalChannel ch in DigitalChannel.List)
            {
                DigitalTriggerValue digval = DigitalTriggerValue.X;
                TriggerDigital.TryGetValue(ch, out digval);
                UpdateDigitalTriggerIndicator(ch, digval);
            }

            graphManager.SelectorTriggerVerPos.Position = (AnalogTriggerLevel + Waveform.Waveforms[AnalogTriggerChannel].VoltageOffset) / (float)Waveform.Waveforms[AnalogTriggerChannel].VoltageRange;
            graphManager.SelectorTriggerVerPos.CenterText = LabNation.Common.Utils.siScale(AnalogTriggerLevel, voltagePrecision, ColorMapper.NumberDisplaySignificance);
            graphManager.SelectorTriggerVerPos.BottomText = LabNation.Common.Utils.siPrefix(AnalogTriggerLevel, voltagePrecision, AnalogTriggerChannel.Probe.Unit);

            engine.ResetTimeVariantProcessors();

            graphManager.SelectorTriggerHorPos.Color = Waveform.Waveforms[AnalogTriggerChannel].GraphColor;
            graphManager.SelectorTriggerVerPos.Color = Waveform.Waveforms[AnalogTriggerChannel].GraphColor;
        }
        internal void DigitalTriggerIndicatorTapped(DigitalChannel ch)
        {
            /* If we're still in a different mode than digital triggering,
             * first activate digital triggering without changing the
             * trigger condition.
             */
            CloseMenusOnGraphArea();
            SelectChannel(ch);
            if (triggerMode != TriggerMode.Digital)
            {
                triggerMode = TriggerMode.Digital;
                SetTrigger();
            }
            else
                CycleDigitalTrigger(ch);
        }
        internal void CycleDigitalTrigger(DigitalChannel ch)
        {
            CloseMenusOnGraphArea();
            DigitalTriggerValue v = LabNation.Common.Utils.GetNextEnum(TriggerDigital[ch]);
            TriggerDigital[ch] = v;
            triggerMode = TriggerMode.Digital;
            SetTrigger();
        }
        internal void UpdateTriggerOfSelectedChannel(DigitalTriggerValue value, bool advanceChannel, bool previousChannel)
        {
            if (selectedChannel is AnalogChannel)
            {
                if (value != DigitalTriggerValue.F && value != DigitalTriggerValue.R && value != DigitalTriggerValue.X)
                    return;
                triggerMode = analogTriggerMode;
                TriggerEdge = value == DigitalTriggerValue.R ? TriggerEdge.RISING : 
                                value == DigitalTriggerValue.F ? TriggerEdge.FALLING : TriggerEdge.ANY;
                AnalogTriggerChannel = (AnalogChannel)selectedChannel;

                SetTrigger();
            }
            if (selectedChannel is DigitalChannel)
            {
                TriggerDigital[(DigitalChannel)selectedChannel] = value;
                triggerMode = TriggerMode.Digital;
                SetTrigger();
                if (advanceChannel)
                    SelectNextChannel(Waveform.EnabledWaveformsVisible.Keys.Where(x => x is DigitalChannel).ToList(), previousChannel ? -1 : 1);
            }
        }

        internal void UpdateDigitalTriggerIndicator(DigitalChannel channel, DigitalTriggerValue value)
        {
            IndicatorInteractive triggerIndicator = WaveformDigital.Waveforms[channel].TriggerIndicator;
            switch (value)
            {
                case DigitalTriggerValue.F:
                    triggerIndicator.LoadContentIcon("icon-falling");
                    triggerIndicator.ShowContentIcon = true;
                    break;
                case DigitalTriggerValue.R:
                    triggerIndicator.LoadContentIcon("icon-rising");
                    triggerIndicator.ShowContentIcon = true;
                    break;
                case DigitalTriggerValue.H:
                    triggerIndicator.CenterText = "1";
                    triggerIndicator.ShowContentIcon = false;
                    break;
                case DigitalTriggerValue.L:
                    triggerIndicator.CenterText = "0";
                    triggerIndicator.ShowContentIcon = false;
                    break;
                case DigitalTriggerValue.X:
                    triggerIndicator.CenterText = "X";
                    triggerIndicator.ShowContentIcon = false;
                    break;
            }            
        }
        internal void ForceTrigger()
        {
            forceTriggerButton.Selected = true;
            scope.ForceTrigger();
        }
        internal void SetTriggerHoldoffRelativeToViewport(float arg)
        {
            SetTriggerHoldoff(ViewportOffset + (arg + 0.5f) * ViewportTimespan - AcquisitionLength / 2.0);
            SetViewportCenterAndTimespan(ViewportCenter, ViewportTimespan);
        }
        internal void ChangeTriggerHoldoffRelativeToAcquisitionBuffer(float arg)
        {
            SetTriggerHoldoff(TriggerHoldoff + arg * AcquisitionLength);
        }
        internal void ZeroTriggerHoldoff()
        {
            if (gridAnchor == GridAnchor.AcquisitionBuffer)
                SetTriggerHoldoff(0);
            else
                SetTriggerHoldoff(ViewportCenter);
            SetViewportCenterAndTimespan(ViewportCenter, ViewportTimespan);
        }
        internal void ShowTriggerClippedToast()
        {
			ShowSimpleToast("Trigger holdoff at maximum\nHide the panorama to relieve limit", 2000, panoramaSplitter.Panorama);
        }
        internal void SetTriggerHoldoff(double holdoff)
        {
            double scopeAcqLen = scope.AcquisitionLength;
            if (holdoff > scopeAcqLen / 2.0)
            {
                holdoff = scopeAcqLen / 2.0;
                ShowPanorama(true);
                ShowTriggerClippedToast();
            }
            //Trasnlate to scope's time-origin
            scope.TriggerHoldOff = holdoff + scope.AcquisitionLength / 2.0;
            TriggerHoldoff = holdoff;

            graphManager.SelectorTriggerHorPos.Position = (float)((TriggerHoldoff + AcquisitionLength / 2.0 - ViewportOffset) / ViewportTimespan) - 0.5f;
            graphManager.SelectorTriggerHorPos.CenterText = LabNation.Common.Utils.siScale(TriggerHoldoffForIndicator, timePrecision, 3);
            graphManager.SelectorTriggerHorPos.BottomText = LabNation.Common.Utils.siPrefix(TriggerHoldoffForIndicator, timePrecision, "s");

            panoramaSplitter.PanoramaShading.UpdateTrigger((float)((TriggerHoldoff + AcquisitionLength / 2.0 ) / AcquisitionLength));

            //If context menu shown, update the position
            if (ContextMenu.Owner == gm.SelectorTriggerHorPos && ContextMenu.Visible)
                ShowTriggerContextMenu(gm.SelectorTriggerHorPos);
            engine.ResetTimeVariantProcessors();
        }

        #endregion

        #region Vertical

        internal void SetYOffset(Channel channel, float offset)
        {            
            //Update scope
            if (channel is AnalogChannel)
            {
                AnalogChannel ch = (AnalogChannel)channel;
                
                //clip requested offset voltage between allowed range. this gets a bit cluttered because a probe can be inverted or not.
                float limit1 = scope.GetYOffsetLimit1(ch);
                float limit2 = scope.GetYOffsetLimit2(ch);
                float minLimit = Math.Min(limit1, limit2);
                float maxLimit = Math.Max(limit1, limit2);
                offset = Math.Min(maxLimit, Math.Max(minLimit, offset));

                scope.SetYOffset(ch, offset);
            }
            
            if(Settings.CurrentRuntime.ChannelSettings.ContainsKey(channel))
                Settings.CurrentRuntime.ChannelSettings[channel].offset = offset;
            else
                Settings.CurrentRuntime.ChannelSettings[channel] = new Settings.ChannelSetting(offset, Waveform.Waveforms[channel].VoltageRange, true);
            //Update UI
            Waveform.Waveforms[channel].VoltageOffset = offset;
            SetTrigger();
        }
        internal void SetHighBandwidthMode(bool highBandWidth)
        {
            Settings.CurrentRuntime.HighBandwidthMode = highBandWidth;
            if (highBandWidth)
                MIN_VOLTAGE_PER_DIVISION = 0.05f;
            else
                MIN_VOLTAGE_PER_DIVISION = 0.02f;

            //repopulate Vdiv wheel items
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                float probeGain = (float)Math.Abs(ch.Probe.Gain);
                gm.Graphs[GraphType.Analog].RepopulateVDivWheelItems(ch, MIN_VOLTAGE_PER_DIVISION * probeGain, MAX_VOLTAGE_PER_DIVISION * probeGain);
            }

            //propagate to scope
            if (scope is SmartScope)
                (scope as SmartScope).HighBandwidthMode = highBandWidth;

            //update all analog channels, ensuring new setting is effective
            if (scope is SmartScope)
                foreach (AnalogChannel ch in AnalogChannel.List.Select(x => x as AnalogChannel))
                    if (channelSettings.ContainsKey(ch))       
                        if (Waveform.Waveforms.ContainsKey(ch))                 
                            if (ch is AnalogChannel)
                                SetVerticalRange(ch, Settings.Current.AnalogChannelSettings[ch].range);
        }
        internal double LimitVerticalRange(Channel ch, double range)
        {
            float probeGain = 1;
            if (ch is AnalogChannel)
                probeGain = (float)Math.Abs(((AnalogChannel)ch).Probe.Gain);
            
            if (!(ch is OperatorAnalogChannel)) //difficult case, as analog channels can have both x1 and x10 sources... so let's not impose any constraint
            {
                //FIXME: scope should dictate ranges!
                //first detect whether the current operation would bring the V/div out of allowable range
                //if so: set ratioChange.Y so the final result will equal the allowable range
                if (range > MAX_VOLTAGE_PER_DIVISION * probeGain * Grid.DivisionsVerticalMax)
                    range = MAX_VOLTAGE_PER_DIVISION * probeGain * Grid.DivisionsVerticalMax;
                if (range < MIN_VOLTAGE_PER_DIVISION * probeGain * Grid.DivisionsVerticalMax)   //20161110: evaluating case in Win64 where range = 0.4, MIN_V = 0.05 and DivVMax = 8, and evaluates TRUE! resulting range becomes 0.40000000596046448
                    range = MIN_VOLTAGE_PER_DIVISION * probeGain * Grid.DivisionsVerticalMax;

                //MUSTVALIDATE: OK not to take probeoffset into account here?
            }

            return range;
        }
        internal void SetVerticalRange(Channel channel, double range)
        {
            range = LimitVerticalRange(channel, range);

            //truncate to 1mV, as otherwise error accumulation causes nasty effects after a lot of zooming operations
            //20161110: need to do this AFTER LimitVerticalRange, as that method can also produce rounding issues on doubles! see comment inside that method
            range = LabNation.Common.Utils.precisionRound(range, voltagePrecision);            

            //Update UI
            if (!(channel is DigitalChannel))
            {
                Waveform.Waveforms[channel].VoltageRange = range;
            }
            if (channel is AnalogChannel)
            {
                scope.SetVerticalRange((AnalogChannel)channel, -(float)range / 2f, (float)range / 2f);
                Settings.CurrentRuntime.AnalogChannelSettings[channel].range = range;

                //adjust XY graph axis
                WaveformXY.SetVoltageRange(channel as AnalogChannel, range);
            }

            if (Settings.CurrentRuntime.ChannelSettings.ContainsKey(channel))
                Settings.CurrentRuntime.ChannelSettings[channel].range = range;
            else
                Settings.CurrentRuntime.ChannelSettings[channel] = new Settings.ChannelSetting(Waveform.Waveforms[channel].VoltageOffset, range, true);

            UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
        }
        internal void SetChannelCoupling(AnalogChannel ch, Coupling coupling)
        {
            scope.SetCoupling(ch, coupling);
            Settings.CurrentRuntime.AnalogChannelSettings[ch].coupling = coupling;

			//in case of audioscope: throw TeasingToast
			if (coupling == Coupling.DC && scope is DummyScope) {
				DummyScope dummy = scope as DummyScope;
				if (dummy.isAudio) {
					ShowSimpleToast ("Only AC coupling possible with audio scoping.\nA SmartScope is required to switch between AC/DC coupling.", 5000);
					SetChannelCoupling (ch, Coupling.AC);
				}
			}
        }
        internal void SetChannelInvert(AnalogChannel ch, bool Invert)
        {
            Settings.Current.AnalogChannelSettings[ch].Invert = Invert;
            ch.Inverted = Invert;
            CloseMenusOnGraphArea();
            RefreshTrigger();
        }
        internal void SetProbeDivision(AnalogChannel ch, Probe probe)
        {
			//in case of audioscope: throw TeasingToast
			if (probe != Probe.DefaultX1Probe && scope is DummyScope) { //MUSTVALIDATE
				DummyScope dummy = scope as DummyScope;
				if (dummy.isAudio) {
					ShowSimpleToast ("When using the AudioScope, voltages cannot be absolute/correct", 5000);
				}
			}

            Settings.CurrentRuntime.AnalogChannelSettings[ch].Probe = probe;
            Dictionary<Channel, Waveform> allWaves = Waveform.Waveforms;
            WaveformAnalog.Waveforms[ch].OffsetIndicator.BottomText = probe.Name;
            ch.SetProbe(probe); 

            float range = (float)Settings.Current.AnalogChannelSettings[ch].range;
            scope.SetVerticalRange(ch, -range / 2f, range / 2f);
            
            //call panzoomgrid to make sure V/div is within new acceptable range
            PanZoomGrid(new Vector2(1, 1), new Vector2(), new Vector2(), false, false);

            //repopulate Vdiv wheel items
            gm.Graphs[GraphType.Analog].RepopulateVDivWheelItems(ch, (float)Math.Abs(MIN_VOLTAGE_PER_DIVISION * probe.Gain), (float)Math.Abs(MAX_VOLTAGE_PER_DIVISION * probe.Gain)); //MUSTVALIDATE
        }

        #endregion

        #region Acquisition

        internal void SetAcquisitionMode(AcquisitionMode mode, bool updateRollingDefault = false)
        {
            AcquisitionMode oldAcqMode = this.AcquisitionMode;
            this.AcquisitionMode = mode;
            scope.AcquisitionMode = AcquisitionMode;
            EnableRolling(false);

            //this solves the messy situation the user ends up in when he 1/ goes to require/single trigger mode 2/ stops the scope 3/ switches to auto triggering
            //after 2/ the scope will not stop but keep on waiting for a next trigger. This is sort of OK, as you cannot force a trigger at that time as the incoming data would ruin a valuable waveform captured using require trigger.
            if ((oldAcqMode == AcquisitionMode.NORMAL || oldAcqMode == AcquisitionMode.SINGLE) && this.AcquisitionMode == AcquisitionMode.AUTO && scope.Running)
                ForceTrigger();
        }
        internal void ToggleAcquisitionRunning()
        {
            SetAcquisitionRunning(!scope.Running);
        }
        internal void SetAcquisitionRunning(bool running)
        {
            scope.Running = running;
            if (running)
            {
                scope.SuspendViewportUpdates = false;
                currentDataCollection = null;
                if (scope.Rolling)
                {
                    MinimizeAcquisitionLengthToFitViewport(AcquisitionLength);
                }
                else if (MinimizeAcquisitionLengthPending)
                {
                    MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
                }
                SetViewportCenterAndTimespan(ViewportCenter, ViewportTimespan);
                SetTriggerHoldoff(TriggerHoldoff);
				ShowPanorama(PanoramaEnabledPreference && !scope.Rolling);
                scope.CommitSettings();
            }
            else
            {
                ShowPanoramaWhenScopeStops = true;                
            }
        }

        internal void SetAcquisitionDepthUserMaximum(uint maxAcquisitionDepth)
        {
            if (maxAcquisitionDepth > scope.AcquisitionDepthUserMaximum && maxAcquisitionDepth > 512 * 1024)
                ShowSimpleToast("Increased acquisition depth requires more time to fetch data when stopped and to save data to disk", 3000);

            scope.AcquisitionDepthUserMaximum = maxAcquisitionDepth;
            SetAcquisitionLength(AcquisitionLength, scope.Rolling);
            SetViewportCenterAndTimespan(ViewportCenter, ViewportTimespan);

            Settings.Current.AcquisitionDepthUserMaximum = scope.AcquisitionDepthUserMaximum;
        }

        #endregion

        #region Data conditioning
        internal void ToggleSmartScopeOutputBit(MenuItem item, int gpioNr)
        {
            if (!(scope is SmartScope))
                return;

            if (gpioNr > 3 || gpioNr < 0)
                return;

            SmartScope ss = (SmartScope)scope;
            byte gpioState = ss.DigitalOutput;
            bool currentState = item.Selected;

            if (!currentState)
                LabNation.Common.Utils.SetBit(ref gpioState, gpioNr);
            else
                LabNation.Common.Utils.ClearBit(ref gpioState, gpioNr);

            ss.DigitalOutput = gpioState;
        }

        #endregion

        #region Auto arrange

        internal void DoMagic()
        {
            foreach (Channel ch in AnalogChannel.List)
                ShowChannel(ch);

            AutoEverything(AnalogTriggerChannel, true);
        }
        //FIXME: expand functionality so that the logic hops channels until it finds
        // a reliable trigger, check frequency multiples and triggers on the slowest
        // channel. Or something, you know, smarter than this crap.
        internal void AutoEverything(AnalogChannel ch, bool autoArrange)
        {
            SetTriggerAnalogChannel(ch);
            autoArrangeAfterAutoTrigger = autoArrange;
            SetAcquisitionMode(AcquisitionMode.AUTO);
            SetAcquisitionRunning(true);
            autoTrigger = true;
        }
        void AutoArrange()
        {
            if (triggerMode == TriggerMode.Digital)
                return;
            List<Channel> wavesToArrange = Waveform.EnabledWaveformsVisible.Keys.Where(x => x is AnalogChannel || x is MathChannel || x is OperatorAnalogChannel || x is ReferenceChannel).ToList();
            wavesToArrange.Sort(Channel.CompareByOrder);

            //Now distribute so each wave takes an equal part of the scope
            for (int i = 0; i < wavesToArrange.Count; i++)
            {
                Channel ch = wavesToArrange[i];
                SetVerticalRange(ch, Waveform.Waveforms[ch].ActiveRange * wavesToArrange.Count * 1.1f);
                WaveformAnalog w = Waveform.Waveforms[ch] as WaveformAnalog;
                SetYOffset(ch, (0.5f - (i + .5f) / (wavesToArrange.Count)) * (float)w.VoltageRange - (w.ActiveRange / 2 + w.Minimum));
                SnapActiveWaveToFixedGrid(Grid.DivisionsVerticalMax, ch);
            }
            
        }
        //FIXME: should this be part of the engine?
        private void HandleAutoTriggerAndArrangement()
        {
            if (engine == null || engine.ScopeData == null)
                return;

            bool newDataAvailable = engine.ScopeData != null && lastScopeData != engine.ScopeData;

            if (!newDataAvailable)
                return;
            lastScopeData = engine.ScopeData;


            if (readyForAutoTrigger)
            {
                readyForAutoTrigger = false;
                ShowAllEnabledChannels();
                AutoArrange();
            }

            if (autoTrigger)
            {
                if (automaticTriggerChannelAverages == null)
                    return;

                if (automaticTriggerChannelAverages.Count == 0)
                    HideAllEnabledChannels();

                ChannelData cd = lastScopeData.GetData(ChannelDataSourceScope.Viewport, AnalogTriggerChannel);
                if (cd == null)
                    return;
                float[] data = (float[])cd.array;
                if (data.Length == 0) return;
                automaticTriggerChannelAverages.Add((data.Min() + data.Max()) / 2);

                if (automaticTriggerChannelAverages.Count == 10)
                {
                    //FIXME: a more decent algorithm wouldn't be bad to assure the best trigger
                    //possible is selected. Also channel hopping in case of poor triggering
                    //would be a great plus
                    SetTriggerAnalogLevel(automaticTriggerChannelAverages.Average());
                    SetTriggerEdge(TriggerEdge.RISING);

                    automaticTriggerChannelAverages = new List<float>();

                    if (!autoArrangeAfterAutoTrigger)
                        ShowAllEnabledChannels();
                    else
                        readyForAutoTrigger = true;
                    autoTrigger = false;
                }
            }
        }

        public void SyncWavesToGrid(AnalogChannel referenceChannel)
        {
            foreach (var kvp in WaveformAnalog.Waveforms)
            {
                if (kvp.Key == referenceChannel)
                    continue;
                double newRange = Utils.roundFullRangeFinder(
                        kvp.Value.VoltageRange,
                        gm.Graphs[GraphType.Analog].Grid.DivisionVertical.Divisions);
                SetVerticalRange(kvp.Key, newRange);
            }

        }

        #endregion

        #region Debug
#if DEBUG
        public void SetMultiplier(AnalogChannel channel, double multiplier)
        {
            if (scope is SmartScope)
                (scope as SmartScope).SetMultiplier(channel, multiplier);
        }

        public void SetDivider(AnalogChannel channel, double divider)
        {
            if (scope is SmartScope)
                (scope as SmartScope).SetDivider(channel, divider);
        }

        public void SetYOffsetByte(AnalogChannel channel, byte offset)
        {
            if (scope is SmartScope)
                (scope as SmartScope).SetYOffsetByte(channel, offset);
        }

        public int AcquisitionsRecorded()
        {
            if (engine.RecordingBusy)
                return engine.AcquisitionsRecorded;
            else
                return -1;
        }
#endif
        #endregion

        #region AWG

        internal bool GotWaveGenerator
        {
            get { return waveGenerator != null; }
        }
        internal bool EnableGeneratorAnalogOutput(bool enable, EDrawable failureToastLocationDrawable)
        {
            if (waveGenerator == null)
                return false;

            if (enable && (mainMode == MainModes.Mixed || mainMode == MainModes.Digital))
            {
                ShowToast("DIGITAL_MODE_BLOCKING AWG", failureToastLocationDrawable, null, Color.White, "Cannot enable AWG while in digital/mixed mode is on\nSwitch to analog first", Location.Bottom, Location.Center, Location.Left, 3000);
                return false;
            }
            waveGenerator.GeneratorToAnalogEnabled = enable;
            Settings.Current.awgEnabled = enable;
            return enable;
            //FIXME: recover from lost LA capabilities.
        }
        internal void EnableGeneratorDigitalOutput(bool enable)
        {
            if (waveGenerator == null)
                return;

            waveGenerator.GeneratorToDigitalEnabled = enable;
            //FIXME: recover from lost LA capabilities.
        }

        internal void GeneratorSetDigitalVoltage(SmartScope.DigitalOutputVoltage voltage)
        {
            Settings.Current.digitalOutputVoltage = voltage;
            if (scope is SmartScope)
                (scope as SmartScope).SetDigitalOutputVoltage(voltage);
        }        

		internal void GeneratorUploadDigitalWaveform(MenuItem sender, double samplePeriod, DigitalWaveForm waveForm, double pulseDutyCycle)
        {
            new Thread(delegate()
            {
                sender.SubMenuActive = true;

                byte[] wave;
                switch (waveForm)
                {
                    case DigitalWaveForm.Counter:
                        wave = DummyScope.WaveCounter(0, 15);
                        break;
                    case DigitalWaveForm.OneHot:
                        wave = DummyScope.WaveOneHot(4);
                        break;
                    case DigitalWaveForm.Marquee:
                        wave = DummyScope.WaveMarquee(4);
                        break;
					case DigitalWaveForm.Pulse:
						wave = DummyScope.WavePulse(pulseDutyCycle / 100.0);
                        samplePeriod /= 100.0;    //compensating for /100 from % above
						break;
                    default:
                        throw new NotImplementedException();
                }

                SetGeneratorDataDigital(sender, wave, samplePeriod);
                sender.SubMenuActive = false;
            }).Start();
        }

        internal void GeneratorUploadAnalogWaveform(MenuItem sender, double frequency, AnalogWaveForm waveForm, double timeOffset, double amplitude, double amplitudeOffset, double phase, double awgMultisineHarmonic)
        {
            new Thread(delegate() 
            {
                sender.SubMenuActive = true;

                uint awgPoints = (uint)waveGenerator.GeneratorNumberOfSamplesForFrequency(frequency);
                double awgSamplePeriod = 1.0 / awgPoints;

                float[] wave;
                double one = 1;
                switch (waveForm)
                {
                    case AnalogWaveForm.SINE:
                        wave = DummyScope.WaveSine(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
                    case AnalogWaveForm.SQUARE:
                        wave = DummyScope.WaveSquare(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
                    case AnalogWaveForm.SAWTOOTH:
                        wave = DummyScope.WaveSawTooth(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
                    case AnalogWaveForm.TRIANGLE:
                        wave = DummyScope.WaveTriangle(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
                    case AnalogWaveForm.SAWTOOTH_SINE:
                        wave = DummyScope.WaveSawtoothSine(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
                    case AnalogWaveForm.MULTISINE:
                        float[] wave1 = DummyScope.WaveSine(awgPoints, awgSamplePeriod, timeOffset, one, amplitude / 2.0, phase);
                        float[] wave2 = DummyScope.WaveSine(awgPoints, awgSamplePeriod, timeOffset, one * awgMultisineHarmonic, amplitude / 2.0, phase);
                        wave = wave1.Select((x, i) => x + wave2[i]).ToArray();
                        break;
    #if DEBUG
                    case AnalogWaveForm.HALF_BIG_HALF_UGLY:
                        wave = DummyScope.WaveHalfBigHalfUgly(awgPoints, awgSamplePeriod, timeOffset, one, amplitude, phase);
                        break;
    #endif
                    default:
                        throw new NotImplementedException();
                }

                double[] waveDouble = wave.Select(x => x + amplitude + amplitudeOffset).ToArray();
                SetGeneratorData(sender, waveDouble, frequency);

                sender.SubMenuActive = false;
            }).Start();
        }
        internal void SetGeneratorData(EDrawable sender, Array wave, double frequency)
        {
            this.SetGeneratorData(sender, wave, waveGenerator.GeneratorStretcherForFrequency(frequency));
        }
        internal void SetGeneratorData (EDrawable sender, Array wave, Int32 stretching)
		{
			try {
                Type sampleType = wave.GetType().GetElementType();
                if (sampleType == typeof(double))
                    waveGenerator.GeneratorDataDouble = (double[])wave;
                else if (sampleType == typeof(byte))
                    waveGenerator.GeneratorDataByte = (byte[])wave;
                else
                {
                    ShowToast("AwgSettingFailed", sender, null, Color.White, "Failed to set AWG\n\nUnsupported sample datatype '" + sampleType.ToString() + "'", Location.Center, Location.Center, Location.Center, 2500);
                    return;
                }
				waveGenerator.GeneratorStretching = stretching;

				if (waveGenerator.DataOutOfRange) {
                    ShowToast("AwgOutOfRange", sender, null, Color.White, "Waveform setting out of range", Location.Center, Location.Center, Location.Center, 2500);
				}
			} catch (ScopeIOException) {
                ShowToast("AwgSettingFailed", sender, null, Color.White, "Failed to set AWG", Location.Center, Location.Center, Location.Center, 2500);
			}
        }

        internal void SetGeneratorDataDigital(EDrawable sender, byte[] wave, double samplePeriod)
        {
            try
            {
                waveGenerator.GeneratorDataByte = wave;
                waveGenerator.GeneratorSamplePeriod = samplePeriod;
            }
            catch (ScopeIOException)
            {
                ShowToast("AwgSettingFailed", sender, null, Color.White, "Failed to set AWG", Location.Center, Location.Center, Location.Center, 2500);
            }
        }

        #endregion

        #region Device connect handler

        //method which handles when a new interface has been detected
        private void OnInterfaceChange(DeviceManager devManager, List<IHardwareInterface> connectedList)
        {
            IHardwareInterface currentInterface = null;
            if (scope is SmartScope)
                currentInterface = (scope as SmartScope).HardwareInterface;

            IHardwareInterface preferredInterface = ESuite.Utils.PreferredDevice(connectedList, currentInterface);

            if (preferredInterface != currentInterface)
            {
                //retract dummyMessage as fast as possible (otherwise retracted AFTER flashing scope)
                QueueCallback(delegate
                {
                    HideToast("DummyScope"); 
                });
                //display message while connecting+flashing etherscope
                if (preferredInterface is SmartScopeInterfaceEthernet)
                {
                    QueueCallback(delegate
                    {
                        ShowToast("EtherScope", connectedIndicator,
                            "indicator-usb", Color.Green, //FIXME: should display network icon...
                            "SmartScope found on network, connecting...", Location.Top, Location.Right, Location.Center, 7000);
                    });
                    if (connectedIndicator != null) //shouldn't happen, but since there's multiple threads..
                    {
                        connectedIndicator.UpdateImage("indicator-wifi");
                        connectedIndicator.UpdateImage2("indicator-wifi");
                    }
                }
                else
                {
                    if (connectedIndicator != null) //shouldn't happen, but since there's multiple threads..
                    {
                        connectedIndicator.UpdateImage("indicator-usb");
                        connectedIndicator.UpdateImage2("indicator-usb");
                    }
                }

                devManager.SetActiveDevice(preferredInterface);
            }
        }

        //FIXME: make this synchronous to the EXNAController thread by using a DrawableCallback and calling it blocking
        IDevice previousDevice = null;
        void OnDeviceConnect(IDevice device, bool connected)
        {
            //only save in case new device is audioscope
            if ((device is DummyScope) && (device as DummyScope).isAudio)
                Settings.SaveCurrent(Settings.IntersessionSettingsId, scope); 

            if (connected)
            {
                QueueCallback(delegate {
                	HideDialog();
                	});
                Logger.Info("Scope connected");
                
                if (scope != null)
                {
                    lock (scope) //need to lock, as otherwise other code might start the old scope's datafetchthread
                    {
                        scope.DataSourceScope.Stop();
                        this.scope = device as IScope;
                    }
                }
                else
                {
                    this.scope = device as IScope;
                    //First scope connected since UI init, make it run
                    this.scope.Running = true;
                }

                this.waveGenerator = device as IWaveGenerator;
                if (scope != null)
                {
                    scope.DataSourceScope.OnNewDataAvailable += engine.UpdateNewestDataPointer;
                    scope.OnAcquisitionTransferFinished += this.AcquisitionTransferFinishedHandler;
                }

                //Update measurement references
                if (measurementManager.SystemMeasurements[SystemMeasurementType.DataSourceFrameRate] != null)
                   (measurementManager.SystemMeasurements[SystemMeasurementType.DataSourceFrameRate] as MeasurementDataSourceFrameRate).UpdateSource(this.scope.DataSourceScope);
                if (scope is SmartScope && measurementManager.SystemMeasurements[SystemMeasurementType.SampleRate] != null)
                    (measurementManager.SystemMeasurements[SystemMeasurementType.SampleRate] as MeasurementSampleRate).UpdateSource(this.scope as SmartScope);

                QueueCallback(new DrawableCallback(StartScope), false);

				if (scope is DummyScope)
					QueueCallback(delegate { 
							ShowToast("DummyScope", connectedIndicator, 
								"indicator-usb", Color.Red,
                                "No SmartScope connected, running in dummy mode", Location.Top, Location.Right, Location.Center, 5000);
                    	});
                if (scope is SmartScope)
                {
                    shouldStillIncrementSuccesfulRuns = false;
                    QueueCallback(delegate
                    {
                        HideToast("DummyScope");
                    });
                }
                smartscopeProcessor.Update(scope);
            }
            else
            {
            	Logger.Debug("UIHandler: device disconnect");
                if(device is IScope) {
					Logger.Debug("UIHandler: removing data update event handler");
                    ((IScope)device).DataSourceScope.OnNewDataAvailable -= engine.UpdateNewestDataPointer;
					Logger.Debug("UIHandler: stopping datasource");
                    ((IScope)device).DataSourceScope.Stop();					
                }
				Logger.Debug("UIHandler: disconnect handler complete");
            }

            //only load settings when moving from audioscope to dummy/real scope
            if (
                (previousDevice != null) //at startup, settings are already loaded
                && (previousDevice is DummyScope && (previousDevice as DummyScope).isAudio) //only load settings when moving from audioscope to dummy/real scope
                )
                QueueCallback(delegate
                {
                    Settings.Load(Settings.IntersessionSettingsId, null);
                });

            previousDevice = scope;
        }        

        void StartScope(EDrawable sender, object arg)
        {
            RebuildSideMenu ();
            
            ConfigureScope();

            //first run
            if (previousDevice == null)
                SetAcquisitionRunning(true);
            //always start in case of real scope or audioscope
            else if (previousDevice is SmartScope)
                SetAcquisitionRunning(true);
            else if (scope is DummyScope && ((DummyScope)scope).isAudio)
                SetAcquisitionRunning(true);

            scope.DataSourceScope.Start();
        }

        public void PauseScope()
        {
			deviceManager.Pause();
        }
        public void ResumeScope()
        {
			deviceManager.Resume();
        }
        void StopScope()
        {
            SetAcquisitionRunning(false);
            scope.DataSourceScope.Stop();
        }
#if WINDOWS
        internal void InstallDriver()
        {
            string serial;
            int VID = 0x04D8;
            int PID = 0xF4B5;
            if (!LabNation.Common.Utils.TestUsbDeviceFound(VID, PID, out serial))
            {
                ShowDialog("Make sure the SmartScope is connected",
                    new List<ButtonInfo>() {
                        new ButtonInfo("Alright", new DrawableCallbackDelegate(UICallbacks.InstallDriver)),
                        new ButtonInfo("Cancel", new DrawableCallbackDelegate(UICallbacks.HideDialog))
                });
                return;
            }

            string path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "driver", "wdi-simple.exe");

            try
            {
                Logger.Info("trying to run " + path);
                int ret = LabNation.Common.Utils.RunProcessElevated(path, "-n SmartScope -f smartscope.inf -m LabNation -v 0x04D8 -p 0xF4B5 -g {7d2c7901-f90b-434d-aae1-38e3e39a3ca1} -t 0 -s -b", true);
                deviceManager.WinUsbPoll();
            }
            catch (Exception e)
            {
                ShowDialog("Failed to install driver\n\n" + e.Message);
            }
        }
#endif

        #endregion
    }
}
