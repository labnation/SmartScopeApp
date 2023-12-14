using System;
using System.Collections.Generic;
using System.Linq;


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

using LabNation.Common;


#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.Devices;

using ESuite.Measurements;
using ESuite.DataProcessors;
using ESuite.Drawables;
using LabNation.DeviceInterface.DataSources;

namespace ESuite
{
    internal partial class UIHandler
    {
        TickStickyNess tickStickyNess { get { return Settings.Current.tickStickyNess.Value; } set { Settings.Current.tickStickyNess = value; } }
        GridAnchor gridAnchor { get { return Settings.Current.gridAnchor.Value; } set { Settings.Current.gridAnchor = value; } }
        Dictionary<MeasurementBox, EToggleButtonTextInImage> measurementBoxToggleButtons = new Dictionary<MeasurementBox, EToggleButtonTextInImage>();
        Dictionary<Cursor, Grid> verticalCursors = new Dictionary<Cursor, Grid>();
        Dictionary<Type, Graph> channelGraphMap;
        Channel selectedChannel;
        Dictionary<String, ESuite.Settings.ChannelSetting> channelSettings { get { return Settings.Current.ChannelSettings; } }

        Dictionary<GraphType, Graph> Graphs { get { return graphManager.Graphs; } }
        private void InitializeMainGrid()
        {
            channelGraphMap = new Dictionary<Type, Graph>() {
                { typeof(AnalogChannel), Graphs[GraphType.Analog] },
                { typeof(DigitalChannel), Graphs[GraphType.Digital] },
                { typeof(AnalogChannelRaw), Graphs[GraphType.Analog] },
                { typeof(ProtocolDecoderChannel), Graphs[GraphType.Analog] },
                { typeof(MathChannel), Graphs[GraphType.Analog]},
                { typeof(OperatorAnalogChannel), Graphs[GraphType.Analog]},
                { typeof(ReferenceChannel), Graphs[GraphType.Analog]},
                { typeof(OperatorDigitalChannel), Graphs[GraphType.Digital]},
                { typeof(FFTChannel), Graphs[GraphType.Frequency]},
                { typeof(XYChannel), Graphs[GraphType.XY]},
#if DEBUG
                { typeof(DebugChannel), Graphs[GraphType.Analog] },
#endif
            };

            foreach (Channel ch in AnalogChannel.List.Select(x => x as Channel).Concat(DigitalChannel.List.Select(x=>x as Channel)))
            {
                if (channelSettings.ContainsKey(ch))
                {
                    EnableChannel(ch, channelSettings[ch].enabled);
                    if (ch is AnalogChannel)
                        SetVerticalRange(ch, Settings.Current.AnalogChannelSettings[ch].range);
                    SetYOffset(ch, channelSettings[ch].offset);
                }
            }

            SwitchMainMode((MainModes)Settings.Current.mainMode, null);
            ChangeWaveformThickness((int)Settings.Current.WaveformThickness.Value);
        }

        //Don't allow interaction while recording or storing (UIHandler works so that when data can be stored it *is* stored)
        internal bool InteractionAllowed { get { return !(engine.RecordingBusy); } }

        #region Measurements

        internal void ToggleMeasurement(int n)
        {
            ShowMeasurementBox(!panoramaSplitter.MeasurementBox.Visible);
        }
        internal void ShowMeasurementBox(bool visible)
        {
            if (measurementManager.ActiveChannelMeasurements[AnalogChannel.ChA].Count + measurementManager.ActiveChannelMeasurements[AnalogChannel.ChB].Count + measurementManager.ActiveSystemMeasurements.Count == 0)
                visible = false;

            Settings.Current.MeasurementBoxVisible = visible;
            if(visible)
                panoramaSplitter.MeasurementBox.SetTopLeftLocation(Settings.Current.MeasurementBoxPosition);
            panoramaSplitter.MeasurementBox.Visible = visible;
        }

        internal void SetMeasurementBoxMode(MeasurementBoxMode mode)
        {
            Settings.Current.MeasurementBoxMode = mode;
            panoramaSplitter.SetMeasurementBoxMode(mode);

            if (mode == MeasurementBoxMode.DockedRight)
                helpButton.Visible = false;
            else
                helpButton.Visible = true;
        }

        #endregion

        #region Channel select / show / hided

        public void RemoveCustomWaveName(Channel ch)
        {
            if (Settings.Current.CustomWaveNames.ContainsKey(ch.Name))
            {
                Settings.Current.CustomWaveNames.Remove(ch.Name);
                UpdateOffsetIndicatorNames();
            }
        }

        public void SetCustomWaveName(Channel ch, string customName)
        {   
            //exit in case this channel is used as input for a decoder
            foreach (DataProcessorDecoder decoder in ProcessorChannel.List.Select(x => x.decoder))
            {
                Dictionary<string, Channel> decoderChannelNames = decoder.SourceChannelMapCopy.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                foreach (var kvp in decoderChannelNames)
                    if (ch == kvp.Value)
                    {
                        ShowSimpleToast("Cannot rename a channel which is used by a decoder", 3000);
                        return;
                    }
            }

            if (Settings.Current.CustomWaveNames.ContainsKey(ch.Name))
                Settings.Current.CustomWaveNames[ch.Name] = customName;
            else
                Settings.Current.CustomWaveNames.Add(ch.Name, customName);

            UpdateOffsetIndicatorNames();
        }

        public void EnableChannel(Channel channel, bool enabled = true, bool destroy = false, Graph graph = null)
        {
            if (graph == null)
                graph = channelGraphMap[channel.GetType()];

            if (channel is LogicAnalyserChannel || channel is AnalogChannelRaw/* || channel is FFTChannel*/) return;

            if (!Waveform.Waveforms.ContainsKey(channel))
            {
                if (channel is AnalogChannel)
                {
                    float probeGain = (float)Math.Abs((channel as AnalogChannel).Probe.Gain);
                    graph.AddWaveform(channel, MIN_VOLTAGE_PER_DIVISION * probeGain, MAX_VOLTAGE_PER_DIVISION * probeGain);
                }
                else
                    graph.AddWaveform(channel, MIN_VOLTAGE_PER_DIVISION * 1.0f, MAX_VOLTAGE_PER_DIVISION * 10.0f);

                if (channel is MathChannel || channel is OperatorAnalogChannel || channel is ReferenceChannel)
                {
                    SetVerticalRange(channel, WaveformAnalog.EnabledWaveforms.First().Value.VoltageRange);
                }
                UpdateUiRanges(graph.Grid);
            }
            
            if (!enabled)
            {
                //if this channel was source channel of decoder -> remove that decoder as well
                foreach(ProcessorChannel p in ProcessorChannel.List)
                    if (p.decoder.SourceChannelMapCopy.ContainsValue(channel))
                        this.DestroyChannel(p);

                FFTChannel.List.Where(x => x.processor.analogChannel == channel).ToList().ForEach(x => DestroyChannel(x));
            }

            if (channel == selectedChannel && destroy)
                SelectNextChannel(Waveform.EnabledWaveformsVisible.Keys.ToList(), 1);
            Waveform.Waveforms[channel].Enabled = enabled;

            if(!enabled)
            {
                if (destroy && channel is ChannelDestructable)
                {
                    graph.RemoveWaveform(channel);
                    ((ChannelDestructable)channel).Destroy();
                }
            }

            //set visibility
            if (enabled)
                if (channel is AnalogChannel || channel is DigitalChannel)
                    ShowChannel(channel, Settings.Current.ChannelVisible[channel.Name], false);
            
            UpdateOffsetIndicatorNames();
            AutoSpaceDigiWaves(null);
        }
        internal void ShowChannel(Channel channel, bool visible = true, bool autoSpaceDigiwaves = true)
        {
            if (channel == null)
                return;

			//in case of audioscope: show a toast when ChB is asked for
			if (scope is DummyScope) {
				DummyScope dummy = scope as DummyScope;
				if (dummy.isAudio) {
					if (channel is AnalogChannel && channel != AnalogChannel.ChA && visible) {
						ShowSimpleToast ("A SmartScope is required to scope more than 1 channel at once", 3000);
						return;
					}
				}
			}

            Waveform.Waveforms[channel].Visible = visible;

            if (channel is DigitalChannel || channel is AnalogChannel)
            {
                Settings.Current.ChannelVisible[channel.Name] = visible;

                if (visible)
                    UpdateUiRanges(gm.Graphs[GraphType.Digital].Grid); //need to refresh as the timing of a hidden digital wave is not updated

                gm.UpdateParkedIndicators();

                gm.Graphs[GraphType.Analog].UpdateGridDivLabelPositions();
            }

            //when hiding a Digital channel, remove its trigger condition
            if (channel is DigitalChannel)
            {
                if (!visible)
                {
                    Settings.Current.TriggerDigitalBeforeHiding[channel] = TriggerDigital[channel];
                    TriggerDigital[channel] = DigitalTriggerValue.X;
                    SetTrigger();
                }
                else
                {
                    TriggerDigital[channel] = Settings.Current.TriggerDigitalBeforeHiding[channel];
                    SetTrigger();
                }
            }

            //cannot call AutoSpaceDigiWaves here just like that, or showing a digiwave will automatically update yPos of all Settings.Digiwaveconfigs
            if (autoSpaceDigiwaves)
                if (channel is DigitalChannel)
                    AutoSpaceDigiWaves(null);

            if (!visible)
                SelectNextChannel(Waveform.EnabledWaveformsVisible.Keys.ToList(), 1);

            CloseMenusOnGraphArea();
        }
        internal void SelectChannel(Channel channel)
        {
            if (channel is AnalogChannel || channel is OperatorAnalogChannel || channel is ReferenceChannel)
                Settings.Current.LastSelectedAnalogChannel = channel.Name;
            else if (channel is DigitalChannel)
                Settings.Current.LastSelectedDigitalChannel = channel.Name;

            //if this was a deactivated wave: make it active
            if (channel == null)
            {
                Logger.Warn("Selecting a NULL channel shouldn't be allowed, ignoring this");
                return;
            }
            if (channel is FFTChannel)
            {
                Logger.Warn("Selecting a FFT channel shouldn't be allowed, ignoring this");
                return;
            }

            if (selectedChannel != channel)
            {
                if (selectedChannel != null && Waveform.Waveforms.ContainsKey(selectedChannel))
                {
                    Waveform.Waveforms[selectedChannel].OffsetIndicator.Selected = false;
                    Waveform.Waveforms[selectedChannel].ShowBackground = false;
                }
                selectedChannel = channel;
                if (selectedChannel != null && Waveform.Waveforms.ContainsKey(selectedChannel))
                {
                    Waveform.Waveforms[selectedChannel].OffsetIndicator.Selected = true;
                    Waveform.Waveforms[channel].ShowBackground = true;
                    Logger.Debug("Selected channel " + selectedChannel.Name);
                    UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
                    graphManager.BringWaveformToFront(selectedChannel);
                }
                else
                {
                    Logger.Debug("No more channel selected");
                }
            }
            else
            {
                Logger.Debug("Not selecting channel since already selected");
            }

            //might need to change which intervals become visible
            UpdateAllIntervalVisibilities();
        }
        internal void SelectNextChannel(List<Channel> availableChannels, int jump)
        {
            //FIXME: quite dirty hack to do this here...
            availableChannels = availableChannels.Where(x => !((x is FFTChannel) || (x is XYChannel) || (x is MeasurementChannel))).ToList();
            if (availableChannels.Count == 0) return;
            SelectChannel(availableChannels.Next(selectedChannel, jump));
        }
        internal void DestroyChannel(Channel channel)
        {
            EnableChannel(channel, false, true);
        }
        internal void HideActiveChannel()
        {
            HideContextMenu();
            if (selectedChannel.Destructable)
                DestroyChannel(selectedChannel);
            else
                ShowChannel(selectedChannel, false);
        }
        void HideAllEnabledChannels()
        {
            foreach (Channel ch in Waveform.EnabledWaveformsVisible.Keys)
                ShowChannel(ch, false);
        }
        void ShowAllEnabledChannels()
        {
            foreach (Channel ch in Waveform.EnabledWaveforms.Keys)
                ShowChannel(ch, true);
        }

        #endregion

        internal void ChangeCursorReference(bool waveReferenced_nScreenReferenced)
        {
            gm.Graphs[GraphType.Analog].Grid.ChangeVerticalCursorReference(waveReferenced_nScreenReferenced);
            HideContextMenu();
        }

        internal void ShowChannelMeasurementInBox(Type type, bool show, AnalogChannel ch)
        {
            measurementManager.ShowChannelMeasurementInBox(type, show, ch);
            channelMeasurementGraphCheckboxes[ch][type].leftChecked = show;

            if (show)
                ShowMeasurementBox(true);

            //also remove graph
            if (!show)
                ShowChannelMeasurementInGraph(type, false, ch, true);

            //if there are no more measurements: hide mbox
            if (Settings.Current.MeasurementBoxVisible.Value)
                if (measurementManager.ActiveChannelMeasurements[AnalogChannel.ChA].Count + measurementManager.ActiveChannelMeasurements[AnalogChannel.ChB].Count + measurementManager.ActiveSystemMeasurements.Count == 0)
                    ShowMeasurementBox(false);
        }

        internal void ShowChannelMeasurementInGraph(Type type, bool show, AnalogChannel ch, bool storeInSettings)
        {
            if (show)
            {
                ShowChannelMeasurementInBox(type, show, ch);
                StochasticMeasurement m = measurementManager.ActiveChannelMeasurements[ch][type];

                //when already listed in box: return
                if (activeMeasurementGraphs.ContainsKey(m))
                    return;

                //add graph and its channel
                Graph graph = gm.AddMeasurementGraph(m);
                activeMeasurementGraphs.Add(m, graph); //indicates which graphs are currently shown on the screen
                EnableChannel(new MeasurementChannel(m, 0), true, false, graph);

                //if needed: correct FFT/XY checkboxes
                if (Settings.Current.analogGraphCombo != AnalogGraphCombo.AnalogMeasurements)
                {
                    Settings.Current.analogGraphCombo = EnableMeasurementMode(true);
                    UpdateGraphCheckboxesTicks();
                }

                //store in Settings
                if (storeInSettings)
                    if (!Settings.Current.requestedChannelGraphs[ch.Name].Contains(type.FullName))
                        Settings.Current.requestedChannelGraphs[ch.Name].Add(type.FullName); //needed when switching between modes or sessions
            }
            else
            {
                //remove channel                
                ChannelMeasurement m = null;
                foreach (var item in activeMeasurementGraphs)
                {
                    if (item.Key.GetType() == type && (item.Key as ChannelMeasurement).Channel == ch)
                    {
                        m = item.Key as ChannelMeasurement;
                        break;
                    }
                }

                if (m == null)
                    return; //already removed

                Graph graph = activeMeasurementGraphs[m];
                Channel measChannel = graph.Waveforms.First().Key;
                EnableChannel(measChannel, false, true, graph);

                //remove graph
                activeMeasurementGraphs.Remove(m);
                gm.RemoveMeasurementGraph(graph);

                if (activeMeasurementGraphs.Count == 0)
                {
                    Settings.Current.analogGraphCombo = EnableMeasurementMode(false);
                    UpdateGraphCheckboxesTicks();
                }

                if (storeInSettings)
                {
                    if (Settings.Current.requestedChannelGraphs[ch.Name].Contains(type.FullName))
                        Settings.Current.requestedChannelGraphs[ch.Name].Remove(type.FullName);
                }
            }

            //update tick
            channelMeasurementGraphCheckboxes[ch][type].Checked = show;
        }

        internal void ShowSystemMeasurementInGraph(SystemMeasurementType type, bool show, bool storeInSettings)
        {
            if (show)
            {
                ShowSystemMeasurementInBox(type, true);
                StochasticMeasurement m = measurementManager.ActiveSystemMeasurements[type] as StochasticMeasurement;

                if (m == null) return;// when not a stochastic measurement

                //when already listed in box: return
                if (activeMeasurementGraphs.ContainsKey(m))
                    return;

                //add graph and its channel
                Graph graph = gm.AddMeasurementGraph(m);
                activeMeasurementGraphs.Add(m, graph); //indicates which graphs are currently shown on the screen
                EnableChannel(new MeasurementChannel(m, 0), true, false, graph);

                //if needed: correct FFT/XY checkboxes
                if (Settings.Current.analogGraphCombo != AnalogGraphCombo.AnalogMeasurements)
                {
                    Settings.Current.analogGraphCombo = EnableMeasurementMode(true);
                    UpdateGraphCheckboxesTicks();
                }

                //store in Settings
                if (storeInSettings)
                    if (!Settings.Current.requestedSystemGraphs.Contains(type))
                        Settings.Current.requestedSystemGraphs.Add(type); //needed when switching between modes or sessions
            }
            else
            {
                //remove channel                                
                if (!activeMeasurementGraphs.Keys.Contains(measurementManager.ActiveSystemMeasurements[type]))
                    return;
                StochasticMeasurement m = measurementManager.ActiveSystemMeasurements[type] as StochasticMeasurement;

                if (m == null)
                    return; //already removed

                Graph graph = activeMeasurementGraphs[m];
                Channel measChannel = graph.Waveforms.First().Key;
                EnableChannel(measChannel, false, true, graph);

                //remove graph
                activeMeasurementGraphs.Remove(m);
                gm.RemoveMeasurementGraph(graph);

                if (activeMeasurementGraphs.Count == 0)
                {
                    Settings.Current.analogGraphCombo = EnableMeasurementMode(false);
                    UpdateGraphCheckboxesTicks();
                }

                if (storeInSettings)
                {
                    if (Settings.Current.requestedSystemGraphs.Contains(type))
                        Settings.Current.requestedSystemGraphs.Remove(type);
                }
            }

            //update tick
            systemMeasurementGraphCheckboxes[type].Checked = show;
        }

        private Dictionary<StochasticMeasurement, Graph> activeMeasurementGraphs = new Dictionary<StochasticMeasurement, Graph>();

        internal void ShowSystemMeasurementInBox(SystemMeasurementType type, bool show)
        {
            //also remove graph
            if (!show)
                ShowSystemMeasurementInGraph(type, false, true);

            measurementManager.ShowSystemMeasurmentInBox(type, show);
            systemMeasurementGraphCheckboxes[type].leftChecked = show;

            if (show)
                ShowMeasurementBox(true);           
        }

        internal void UpdateGraphCheckboxesTicks()
        {
            switch (Settings.Current.analogGraphCombo)
            {
                case AnalogGraphCombo.Analog:
                    checkboxFftEnabled.Checked = false;
                    checkboxXyEnabled.Checked = false;
                    break;
                case AnalogGraphCombo.AnalogFFT:
                    checkboxFftEnabled.Checked = true;
                    checkboxXyEnabled.Checked = false;
                    break;
                case AnalogGraphCombo.AnalogXY:
                    checkboxFftEnabled.Checked = false;
                    checkboxXyEnabled.Checked = true;
                    break;
                case AnalogGraphCombo.AnalogMeasurements:
                    checkboxFftEnabled.Checked = false;
                    checkboxXyEnabled.Checked = false;
                    break;
            }
        }

        internal AnalogGraphCombo EnableFFT(bool enable)
        {
            if (enable && Settings.Current.mainMode != MainModes.Analog)
                enable = false;

            //cancel when scope is rolling
            if (enable && scope.Rolling)
            {
                enable = false;
                ShowSimpleToast("Cannot show XY plot while in rolling mode", 2000);
            }

            if (enable)
            {
                //make sure XY mode is closed properly
                EnableXY(false);
                HideAllMeasurementGraphs();

                gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Analog, GraphType.Frequency }));

                //add fftchannel for each analog wave
                var analogWaves = Waveform.Waveforms.Where(x => x.Key is AnalogChannel).ToDictionary(x => x.Key, x => x.Value);
                foreach (var kvp in analogWaves)
                    AddFFTChannel(kvp.Key as AnalogChannel);

                //set grid and waveform parameter
                SetFFTVoltageAxis(Settings.Current.fftVoltageScale.Value);
                SetFFTFrequencyAxis(Settings.Current.fftFrequencyScale.Value);

                return AnalogGraphCombo.AnalogFFT;
            }
            else
            {
                //delete all fft channels + decoders + waves
                for (int i = gm.Graphs[GraphType.Frequency].Waveforms.Count - 1; i >= 0; i--)
                    DestroyChannel(gm.Graphs[GraphType.Frequency].Waveforms.ElementAt(i).Key);

                //hide graph
                List<GraphType> activeGraphTypes = new List<GraphType>(gm.Graphs.Where(x => gm.ActiveGraphs.Contains(x.Value) && x.Key != GraphType.Frequency).ToDictionary(x => x.Key, x => x.Value).Keys);
                gm.ShowGraphs(activeGraphTypes);

                RestoreAllMeasurementGraphs();

                return AnalogGraphCombo.Analog;
            }
        }

        internal AnalogGraphCombo EnableMeasurementMode(bool enable)
        {
            if (enable && Settings.Current.mainMode != MainModes.Analog)
                enable = false;

            if (enable)
            {
                //make sure FFT is closed properly                
                EnableFFT(false);
                EnableXY(false);

                //show graph
                gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Analog, GraphType.Measurements }));

                return AnalogGraphCombo.AnalogMeasurements;
            }
            else
            {
                //hide graph
                List<GraphType> activeGraphTypes = new List<GraphType>(gm.Graphs.Where(x => gm.ActiveGraphs.Contains(x.Value) && x.Key != GraphType.Measurements).ToDictionary(x => x.Key, x => x.Value).Keys);
                gm.ShowGraphs(activeGraphTypes);

                return AnalogGraphCombo.Analog;
            }
        }

        internal AnalogGraphCombo EnableXY(bool enable)
        {
            if (enable && Settings.Current.mainMode != MainModes.Analog)
                enable = false;

            //cancel when scope is rolling
            if (enable && scope.Rolling)
                enable = false;

            if (enable)
            {
                HideAllMeasurementGraphs();

                //make sure FFT is closed properly
                if (Settings.Current.analogGraphCombo != AnalogGraphCombo.Analog)
                    EnableFFT(false);

                //show graph
                gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Analog, GraphType.XY }));                                

                //create channel and waveform
                XYChannel chan = new XYChannel(AnalogChannel.ChA, AnalogChannel.ChB);
                EnableChannel(chan, true, false, gm.Graphs[GraphType.XY]);

                //make sure the ranges are set correctly (actually only necessary for first init of XY graph)
                UpdateUiRanges(gm.Graphs[GraphType.XY].Grid);

                return AnalogGraphCombo.AnalogXY;
            }
            else
            {
                //hide graph
                List<GraphType> activeGraphTypes = new List<GraphType>(gm.Graphs.Where(x => gm.ActiveGraphs.Contains(x.Value) && x.Key != GraphType.XY).ToDictionary(x => x.Key, x => x.Value).Keys);
                gm.ShowGraphs(activeGraphTypes);

                //destroy channel and wave
                var select = Waveform.Waveforms.Where(x => x.Value is WaveformXY).FirstOrDefault();
                if (select.Key != null)
                    EnableChannel(select.Key, false, true);

                RestoreAllMeasurementGraphs();

                return AnalogGraphCombo.Analog;
            }
        }

        internal bool SquareXY(bool enable)
        {
            GridXY grid = gm.Graphs[GraphType.XY].Grid as GridXY;
            if (grid.Squared != enable)
            {
                grid.Squared = enable;
                gm.OnBoundariesChanged();
            }

            return enable;
        }

        internal bool InvertXY(bool invert)
        {
            (gm.Graphs[GraphType.XY].Grid as GridXY).InvertAxes = invert;
            return invert;
        }

        internal void AdjustMultigraphHeight(float relHeight)
        {
            List<GraphType> activeGraphTypes = new List<GraphType>(gm.Graphs.Where(x => gm.ActiveGraphs.Contains(x.Value)).ToDictionary(x => x.Key, x => x.Value).Keys);
            if ((activeMeasurementGraphs.Count > 0) && (mainMode == MainModes.Analog)) activeGraphTypes.Add(GraphType.Measurements);
            gm.ShowGraphs(activeGraphTypes, new float[2] { relHeight, 1f - relHeight }, true);
        }

        internal MainModes mainMode
        {
            get { return Settings.Current.mainMode.Value; }
            set { Settings.Current.mainMode = value; }
        }

        internal void SwitchMainMode(MainModes mainMode, EDrawable failureToastLocationDrawable)
        {
            if((mainMode == MainModes.Digital || mainMode == MainModes.Mixed) && (waveGenerator != null && Settings.Current.awgEnabled.Value))
            {
                ShowToast("AWG_BLOCKING_DIGITAL_MODE", failureToastLocationDrawable, null, Color.White, "Cannot switch to digital/mixed mode while analog wave generator is on\nDisable the analog output of the generator first.", Location.Bottom, Location.Center, Location.Left, 3000);
                return;   
            }
            this.mainMode = mainMode;
            etsProcessor.RequestETSReset();

			bool isAudioScope = false;
			if (scope is DummyScope) {
				DummyScope dummy = scope as DummyScope;
				if (dummy.isAudio)
					isAudioScope = true;
			}

            //Analog mode
            if (mainMode == MainModes.Analog)
            {
                HideAllMeasurementGraphs(); //needed, so channels are removed; otherwise they will cause issues on EnableChannel later on
                StoreLATriggerState(); //copy current settings, as LA trigger settings are retreived from TriggerDigitalBeforeHiding when LA channels are being re-added
                logicAnalyserChannel = null;
                triggerMode = analogTriggerMode;
                SetTrigger();

                if (Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogMeasurements)
                    gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Analog, GraphType.Measurements }));
                else
                    gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Analog }));                

                //cannot use foreach <- collection get changed <- disabling wave might cause decoder to be removed
                string channelToSelect = Settings.Current.LastSelectedAnalogChannel; // need to store this here, as it gets changed when channels are disabled below

                //FIXME!!! this nasty correction is needed in case an OperatorAnalog was stored as LastSelectedAnalogChannel. Fix would be to store only AnalogWaves in LastSelectedAnalogChannel
                if (channelToSelect != "A" && channelToSelect != "B")
                    channelToSelect = "A";

                for (int i = 0; i < Waveform.Waveforms.Count; i++)
                {
                    var kvp = Waveform.Waveforms.ElementAt(i);
                    if (gm.Graphs[GraphType.Analog].Waveforms.ContainsValue(kvp.Value) || gm.Graphs[GraphType.Frequency].Waveforms.ContainsValue(kvp.Value))
                    {
                        EnableChannel(kvp.Key, true);
                    }
                    else
                        EnableChannel(kvp.Key, false);
                }

				if (isAudioScope) {
					foreach (var ch in AnalogChannel.List) {
						if (ch == AnalogChannel.ChA) {
							ShowChannel (ch, true);
							channelToSelect = ch.Name;
						}
						else
							ShowChannel (ch, false);
					}
				}

                //reselect last selected analog wave
                SelectChannel(Waveform.Waveforms.Single(x => x.Key.Name == channelToSelect).Key);

                if (Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogFFT) EnableFFT(true);
                if (Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogXY) EnableXY(true);

                ActivateMeasurementFunctionalities(true);
                RestoreAllMeasurementGraphs();                
            }
            //Digital mode
            else if (mainMode == MainModes.Digital)
            {
                //Sequence of importance, since logicAnalyserChannel setter checks
                //if triggerMode is digital. If not, it messes with the trigger
                triggerMode = TriggerMode.Digital;
                logicAnalyserChannel = triggerValue.channel;
                
                SetTrigger();
                EnableFFT(false);
                EnableXY(false);
                gm.ShowGraphs(new List<GraphType>(new GraphType[] { GraphType.Digital }));

                //this one must be after EnableFFT/XY, but before the EnableChannels calls
                HideAllMeasurementGraphs();

                //first hide all channels. cannot use foreach, as disabling a destructable channel will delete it and as such change the collection -> crash
                for (int i = Waveform.Waveforms.Count-1; i >= 0; i--)
                    EnableChannel(Waveform.Waveforms.Keys.ElementAt(i), false);

                //now re-add the enabled digital signals in the same order as they were stored in Settings
                foreach (DigitalChannel ch in DigitalChannel.List)
                {
                    EnableChannel(ch, true);
                    if (channelSettings.ContainsKey(ch))
                        SetYOffset(ch, channelSettings[ch].offset);
                }

                if (selectedChannel == null || !gm.Graphs[GraphType.Digital].EnabledWaveforms.ContainsKey(selectedChannel))
                    SelectChannel(gm.Graphs[GraphType.Digital].EnabledWaveforms.Keys.First());

				//in case of audioscope: throw TeasingToast
				if (isAudioScope)
					ShowToast ("", gm, null, Color.Red, "A SmartScope is required to use Digital Channels", Location.Center, Location.Center, Location.Center, 3000);

                ActivateMeasurementFunctionalities(false);                
            }
            //Mixed mode
            else if (mainMode == MainModes.Mixed)
            {
                HideAllMeasurementGraphs();
                EnableFFT(false);
                EnableXY(false);
                gm.ShowGraphs(new List<GraphType>(new GraphType[]{ GraphType.Analog, GraphType.Digital}));

                for (int i = 0; i < Waveform.Waveforms.Count; i++)
                {
                    var kvp = Waveform.Waveforms.ElementAt(i);
                    if (gm.Graphs[GraphType.Analog].Waveforms.ContainsValue(kvp.Value))
                    {
                        if (kvp.Key == logicAnalyserChannel)
                            EnableChannel(kvp.Key, false);
                        else
                            EnableChannel(kvp.Key, true);
                    }   
                }

                //select which analog channel to activate (one with trigger), and other to sacrifice
                AnalogChannel channelToSacrifice = AnalogChannel.List.Where(x => x != AnalogTriggerChannel).First();
                logicAnalyserChannel = channelToSacrifice; //Will disable channel as well
                EnableChannel(channelToSacrifice, false);
                EnableChannel(AnalogTriggerChannel, true);    

                //FIXME: only 4 digiwaves should be selected in a smart way
                foreach (var kvp in Waveform.Waveforms)
                {
                    if (kvp.Value is WaveformDigital)
                    {
                        EnableChannel(kvp.Key, true);
                    }
                }

                SetTrigger();

				//in case of audioscope: throw TeasingToast
				if (isAudioScope)
					ShowToast ("", gm, null, Color.Red, "A SmartScope is required to use Digital Channels", Location.Center, Location.Center, Location.Center, 3000);

                ActivateMeasurementFunctionalities(true);
            }
            
            /* Hide math channels that take disabled analog channels as input */
            foreach (MathChannel mc in MathChannel.List)
            {
                if (!Waveform.Waveforms[mc].Enabled) continue;
                foreach (AnalogChannel ch in mc.processor.inputChannels)
                    if (!Waveform.EnabledWaveforms.ContainsKey(ch))
                    {
                        EnableChannel(mc, false);
                        continue;
                    }
            }

            AutoSpaceDigiWaves(null);

            foreach (Graph g in graphManager.ActiveGraphs)
                UpdateUiRanges(g.Grid);

            if (selectedChannel == null || !Waveform.EnabledWaveformsVisible.ContainsKey(selectedChannel))
                if (Waveform.EnabledWaveformsVisible.Keys.Count > 0)
                    SelectChannel(Waveform.EnabledWaveformsVisible.Keys.First());

            LoadProcessorSettings(); //this one deletes all active processors and adds the stored ones for this graph
        }


        private void RestoreAllMeasurementGraphs()
        {
            //channel graphs
            foreach (var chanList in Settings.Current.requestedChannelGraphs)
            {
                AnalogChannel ch = chanList.Key == "A" ? AnalogChannel.ChA : AnalogChannel.ChB; //spent 2h trying to serialize and deserialize AnalogChannel... sometimes working but unstable
                foreach (var typeName in chanList.Value)
                    ShowChannelMeasurementInGraph(Type.GetType(typeName), true, ch, false);
            }

            //channel graphs
            foreach (var item in Settings.Current.requestedSystemGraphs)
                ShowSystemMeasurementInGraph(item, true, false);
        }

        private void HideAllMeasurementGraphs()
        {
            //channel graphs
            foreach (var chanList in Settings.Current.requestedChannelGraphs)
            {
                AnalogChannel ch = chanList.Key == "A" ? AnalogChannel.ChA : AnalogChannel.ChB; //spent 2h trying to serialize and deserialize AnalogChannel... sometimes working but unstable
                foreach (var typeName in chanList.Value)
                    ShowChannelMeasurementInGraph(Type.GetType(typeName), false, ch, false);
            }

            //channel graphs
            foreach (var item in Settings.Current.requestedSystemGraphs)
                ShowSystemMeasurementInGraph(item, false, false);
        }

        private void ActivateMeasurementFunctionalities(bool activate)
        {
            if (activate)
            {
                ShowMeasurementBox(Settings.Current.MeasurementBoxVisible.Value);
                ShowMeasurementMenu(Settings.Current.MeasurementMenuVisible.Value, true);
            }
            else
            {
                //hide menu and box, but remember settings;
                bool tempMBoxVisible = Settings.Current.MeasurementBoxVisible.Value;
                ShowMeasurementBox(false);
                Settings.Current.MeasurementBoxVisible = tempMBoxVisible;

                bool tempMMenuVisible = Settings.Current.MeasurementMenuVisible.Value;
                ShowMeasurementMenu(false, true);
                Settings.Current.MeasurementMenuVisible = tempMMenuVisible;
            }
        }

        private void StoreLATriggerState()
        {
            //when re-adding LA channels, their trigger state will be read from TriggerDigitalBeforeHiding.
            //store only the triggerstate of the visible LA channels. The hidden states are already up-to-date.
            foreach (DigitalChannel dc in DigitalChannel.List)
                if (Waveform.EnabledWaveformsVisible.ContainsKey(dc))
                    Settings.Current.TriggerDigitalBeforeHiding[dc] = TriggerDigital[dc];

            //sync on both levels. need to do a deep copy for this, otherwise they are just references to the same dictionary
            TriggerDigital = Settings.Current.TriggerDigitalBeforeHiding.Where(x => true).ToDictionary(x => x.Key, x => x.Value);
        }

        internal void AddRefChannel(AnalogChannel masterChannel)
        {
            //added a few try-catch statements to separate a "Object reference not set to an instance of an object." thrown in the method. see crashreport 3/28/2017 8:09:18 PM

            int index = 0;
            try
            {
                //find first available indexer                
                List<KeyValuePair<Channel, Waveform>> existingRefWaves = Waveform.EnabledWaveforms.Where(x => x.Value.Channel is DataProcessors.ReferenceChannel).ToList();
                while (true)
                {
                    List<KeyValuePair<Channel, Waveform>> channelWithThisIndex = existingRefWaves.Where(x => x.Key.Value == index).ToList();
                    if (channelWithThisIndex.Count == 0) break;
                    index++;                    
                }
            }
            catch { throw new Exception("AddRefChannel crashtype 1 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            //add reference channel and wave
            DataProcessors.ReferenceChannel ch;
            try {  ch = new DataProcessors.ReferenceChannel("REF" + index.ToString(), index); }
            catch { throw new Exception("AddRefChannel crashtype 2 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            try { EnableChannel(ch, true, false, gm.Graphs[GraphType.Analog]); }
            catch { throw new Exception("AddRefChannel crashtype 3 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            WaveformReference refWave;
            try { refWave = Waveform.Waveforms[ch] as WaveformReference; }
            catch { throw new Exception("AddRefChannel crashtype 4 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            try { refWave.CopyWave(Waveform.Waveforms[masterChannel] as WaveformAnalog); }
            catch { throw new Exception("AddRefChannel crashtype 5 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            try { Waveform.Waveforms[ch].UpdateData(new ChannelData[] { currentDataCollection.Data.GetData(ChannelDataSourceScope.Viewport, masterChannel) }); }
            catch { throw new Exception("AddRefChannel crashtype 6 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }

            try { SyncWavesToGrid(masterChannel as AnalogChannel); }
            catch { throw new Exception("AddRefChannel crashtype 7 -- please send this to bughunt@lab-nation.com so we can fix this!!"); }
        }

        internal void UpdateOffsetIndicatorNames()
        {
            //decoder names have priority over custom names, which have priority over original channel names
            Dictionary<Channel, string> channelNames = new Dictionary<Channel, string>();

            //Set default names
            foreach (Channel ch in Waveform.Waveforms.Keys)
                channelNames.Add(ch, ch.Name);

            //Override names with custom names
            foreach (Channel ch in Waveform.Waveforms.Keys)
                if (Settings.Current.CustomWaveNames.ContainsKey(ch.Name))
                    channelNames[ch] = Settings.Current.CustomWaveNames[ch.Name];

            //Override names with decoder names
            foreach (DataProcessorDecoder decoder in ProcessorChannel.List.Select(x => x.decoder))
            {
                Dictionary<string, Channel> decoderChannelNames = decoder.SourceChannelMapCopy.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                foreach (var kvp in decoderChannelNames)
                    if (channelNames.ContainsKey(kvp.Value))
                        channelNames[kvp.Value] = kvp.Key;
            }

            //Effectively set names
            foreach (var kvp in channelNames)
                if (Waveform.Waveforms[kvp.Key].OffsetIndicator != null)
                    Waveform.Waveforms[kvp.Key].OffsetIndicator.CenterText = kvp.Value;
        }

        #region Log

        static DateTime ToggleLogRepeatLastTap = DateTime.MinValue;
        static int ToggleLogRepeatTapRequired = 5;
        static int ToggleLogRepeatTapMaxInterval = 1000; //ms
        static int ToggleLogRepeatTapCount = 0;
        internal void ToggleLog(EDrawable sender)
        {    
            if ((DateTime.Now - ToggleLogRepeatLastTap).TotalMilliseconds > ToggleLogRepeatTapMaxInterval)
            {
                ToggleLogRepeatLastTap = DateTime.Now;
                ToggleLogRepeatTapCount = 0;
                return;
            }
            else
            {
                ToggleLogRepeatTapCount++;
                if (ToggleLogRepeatTapCount >= ToggleLogRepeatTapRequired)
                {
                    ToggleLog();
                    ToggleLogRepeatTapCount = 0;
                    ToggleLogRepeatLastTap = DateTime.MinValue;
                }
                else
                {
                    ToggleLogRepeatLastTap = DateTime.Now;
                    if (ToggleLogRepeatTapCount >= 1)
                    {
                        ShowToast("LogTap", sender, null, Color.White, "Only " + (ToggleLogRepeatTapRequired - ToggleLogRepeatTapCount) + " taps left for log!", Location.Top, Location.Right, Location.Center, 1000);
                    }
                }

                return;
            }
        }

        #endregion

        #region Grid / indicators 

        internal void DoubleTapGrid(Vector2 location)
        {
            // panorama shown
            //  zoom in when:
            //      OR: zoomed out completely
            //      OR: freq available AND not yet zoomed to 10periods
            //  zoom out otherwise

            //Precalc common stuff
            bool freqMeasAvailable = false;
            double newViewportTimespan = ViewportTimespan / 5.0; //default value: zoom by factor 5

            double? freqValue = measurementDataProcessor.MeasFrequency.GetValue(selectedChannel);
            if (freqValue != null)
            {
                double freq = freqValue.Value;
                if (!(double.IsInfinity(freq) || double.IsNaN(freq) || double.IsNegativeInfinity(freq) || double.IsPositiveInfinity(freq)))
                    newViewportTimespan = 10.0 / freq; //display 10 periods of signal
                else
                    freqMeasAvailable = false;
            }

            //state machine
            if (PanoramaVisible)
            {
                bool zoomedOutEntirely = ViewportTimespan == AcquisitionLength && ViewportCenter == 0 && ViewportOffset == 0;
                if (zoomedOutEntirely || (freqMeasAvailable && Math.Abs((ViewportTimespan - newViewportTimespan)/ViewportTimespan) > 0.05)) //zoomfactor needs to be at least 5% different from what it is now
                {
                    double pinchCenterToTriggerAbsolute = ViewportCenter + location.X * ViewportTimespan - TriggerHoldoff;

                    SetTriggerHoldoff(TriggerHoldoff);
                    double newViewportCenter = pinchCenterToTriggerAbsolute + TriggerHoldoff - (location.X) * newViewportTimespan;

                    SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);
                }
                else
                {
                    //need to zoom out
                    SetViewportCenterAndTimespan(0, AcquisitionLength);
                }
            }
            else // panorama not shown
            {
                double pinchCenterToTriggerAbsolute = ViewportCenter + location.X * ViewportTimespan - TriggerHoldoff;

                SetTriggerHoldoff(TriggerHoldoff);
                double newViewportCenter = pinchCenterToTriggerAbsolute + TriggerHoldoff - (location.X) * newViewportTimespan;

                SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);
                MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
            }
        }

        //only does horizontal (frequency) scaling for now
        internal void PanZoomFreqGrid(Vector2 ratioChange, Vector2 pinchCenter, Vector2 offsetDelta, bool verifyWaveIsUnderCursor, bool wasMouseScroll, Rectangle? gestureRectangle = null)
        {
            PanZoomFreqGridHorizontal(ratioChange.X, offsetDelta.X, pinchCenter.X);
        }

        internal void PanZoomFreqGridHorizontal(float ratio, float offset, float center)
        {
            if (gm.Graphs[GraphType.Frequency].Grid.TimeScale == LinLog.Linear)
                PanZoomFreqGridHorizontalLinear(ratio, offset, center);
            else
                PanZoomFreqGridHorizontalLog(ratio, offset, center);
        }

        internal void PanZoomFreqGridHorizontalLinear(float ratio, float offset, float center)
        {
            Grid g = gm.Graphs[GraphType.Frequency].Grid;
         
            //center contains screenposition between -0.5:left and +0.5:right
            //x contains screenposition between 0:left and 1:right
            float x = 0.5f + center;            

            //1: find out at which freq the mouse is. that freq must remain stable at that position.
            float oldRange = g.RightLimitFreq - g.LeftLimitFreq;
            float mouseFreq = g.LeftLimitFreq + oldRange * (x-offset);
            
            //2: change zoom
            float newRange = oldRange * ratio;

            //3: calc left and right freqs, so mousefreq stays at mouseposition (offset added afterwards, as calcs are exactly the same)
            g.LeftLimitFreq = mouseFreq - newRange * x;
            g.RightLimitFreq = mouseFreq + newRange * (1f - x);

            //need to rebuild FFT vertices, as otherwise the FFT waves will be rendered at correctly positions
            var fftWaves = Waveform.EnabledWaveforms.Where(y => y.Key is FFTChannel).ToDictionary(y => y.Key, y => y.Value);
            foreach (var fftWave in fftWaves)
                (fftWave.Value as WaveformFreq).RebuildVertexBuffer();
        }

        internal void PanZoomFreqGridHorizontalLog(float ratio, float offset, float center)
        {            
            Grid g = gm.Graphs[GraphType.Frequency].Grid;
            if (g.RightLimitFreq == 0)
                return;
            
            //center contains screenposition between -0.5:left and +0.5:right
            //x contains screenposition between 0:left and 1:right
            float x = 0.5f + center;            

            //1: find out at which freq the mouse is. that freq must remain stable at that position.
            float leftDecade = (float)Math.Log(g.LeftLimitFreq, 10);
            if (g.LeftLimitFreq < 1) //log of <1 will give NaN
                leftDecade = 0;
            float rightDecade = (float)Math.Log(g.RightLimitFreq, 10);
            float decadeDifference = rightDecade - leftDecade;
            float mouseDecade = leftDecade + decadeDifference * (x-offset);

            //2: change zoom
            decadeDifference *= ratio;

            //3: calc what centerfreq, so mousefreq stays at mouseposition (offset added afterwards, as calcs are exactly the same)
            float newLeftDecade = mouseDecade - x * decadeDifference;
            float newRightDecade = mouseDecade + (1f - x) * decadeDifference;
            float newLeftFreq = (float)Math.Pow(10, newLeftDecade);
            float newRightFreq = (float)Math.Pow(10, newRightDecade);
            g.LeftLimitFreq = newLeftFreq;
            g.RightLimitFreq = newRightFreq;

            //need to rebuild FFT vertices, as otherwise the FFT waves will be rendered at correctly positions
            var fftWaves = Waveform.EnabledWaveforms.Where(y => y.Key is FFTChannel).ToDictionary(y => y.Key, y => y.Value);
            foreach (var fftWave in fftWaves)
                (fftWave.Value as WaveformFreq).RebuildVertexBuffer();
        }

        internal void PanZoomGrid (Vector2 ratioChange, Vector2 pinchCenter, Vector2 offsetDelta, bool verifyWaveIsUnderCursor, bool wasMouseScroll, Rectangle? gestureRectangle = null)
		{
            //useability 'improvement': at mousescroll, zoom and center on the mousepointer. Except when the trigger is at center position: in such case keep the trigger at center location
            if (wasMouseScroll && (TriggerHoldoff == 0))
                if (scope.Running)
                    pinchCenter = Vector2.Zero;            

            PanZoomGridHorizontal(ratioChange.X, offsetDelta.X, pinchCenter.X);

            if (verifyWaveIsUnderCursor)
            {
                if (gestureRectangle.HasValue && gestureRectangle.Value.Height != 0)
                {
                    List<Channel> channelsWithinGestureRectangle = Waveform.ChannelsWithin(gestureRectangle.Value, 0.05f).ToList();
                    if (channelsWithinGestureRectangle.Contains(selectedChannel))
                    {
                        canPerformVerticalScaling = true;
                    }
                    else
                    {
                        if (channelsWithinGestureRectangle.Count == 0)
                        {
                            canPerformVerticalScaling = false;
                        }
                        else
                        {
                            SelectChannel(channelsWithinGestureRectangle.First());
                            canPerformVerticalScaling = true;
                        }
                    }
                }
                else
                {
                    canPerformVerticalScaling = false;
                }
            }

            PanZoomGridVertical(ratioChange, offsetDelta, canPerformVerticalScaling, selectedChannel);
        }

        internal void PanZoomGridVertical(Vector2 ratioChange, Vector2 offsetDelta, bool canPerformVerticalScaling, Channel ch)
        {
            if (!Waveform.Waveforms.ContainsKey(ch)) return; //crash prevention for crashreport 2017-03-09 5:43:10 PM

            //update voltage axis
            if (ch is AnalogChannel || ch is MathChannel || ch is OperatorAnalogChannel || ch is ReferenceChannel)
            {
                float probeGain = 1;
                if (ch is AnalogChannel)
                    probeGain = (float)Math.Abs(((AnalogChannel)ch).Probe.Gain); //MUSTVALIDATE
                
                //FIXME: scope should dictate ranges!
                //first detect whether the current operation would bring the V/div out of allowable range
                //if so: set ratioChange.Y so the final result will equal the allowable range
                if (Waveform.Waveforms[ch].VoltageRange / Grid.DivisionsVerticalMax * ratioChange.Y > MAX_VOLTAGE_PER_DIVISION * probeGain)
                    ratioChange.Y = (float)(MAX_VOLTAGE_PER_DIVISION * probeGain / Waveform.Waveforms[ch].VoltageRange * Grid.DivisionsVerticalMax);
                if (Waveform.Waveforms[ch].VoltageRange / Grid.DivisionsVerticalMax * ratioChange.Y < MIN_VOLTAGE_PER_DIVISION * probeGain)
                    ratioChange.Y = (float)(MIN_VOLTAGE_PER_DIVISION * probeGain / Waveform.Waveforms[ch].VoltageRange * Grid.DivisionsVerticalMax);

                if (Waveform.Waveforms[ch] is WaveformReference) //reference waveforms don't have a probedivision. it can perfectly have any VoltageRange
                    ratioChange.Y = 1;
                
                //Vector2 centerPosBetween0and1 = pinchCenter + new Vector2(0.5f, 0f);
                float newYOffset = Waveform.Waveforms[ch].VoltageOffset * ratioChange.Y - offsetDelta.Y * (float)Waveform.Waveforms[ch].VoltageRange;                
                //SetYOffset(selectedChannel, 
                //float newYOffset = EGraphicWaveform.Waveforms[selectedChannel].VoltageOffset 
                //	- offsetDelta.Y * (float)EGraphicWaveform.Waveforms[selectedChannel].VoltageRange
                //	+ (1f - ratioChange.Y) * centerPosBetween0and1.Y * (float)EGraphicWaveform.Waveforms[selectedChannel].VoltageRange;
                //Don't move verically when pinching vertically out of the wave's area
                if (ratioChange.Y == 1f || canPerformVerticalScaling)
                    SetYOffset(ch, newYOffset);
                if (ratioChange.Y != 1f && canPerformVerticalScaling)
                    SetVerticalRange(ch, Waveform.Waveforms[ch].VoltageRange * ratioChange.Y);
            }
            else if (ch is ProtocolDecoderChannel || ch is OperatorDigitalChannel)
            {
                ratioChange.Y = 1; //zooming makes no sense on these channels
                float newYOffset = Waveform.Waveforms[ch].VoltageOffset * ratioChange.Y - offsetDelta.Y * (float)Waveform.Waveforms[ch].VoltageRange;
                SetYOffset(ch, newYOffset);
                AutoSpaceDigiWaves(ch);
            }
            else if (ch is DigitalChannel)
            {
                //do nothing
            }
        }

        internal void SetChannelDivVertical(float divVertical, Channel ch)
        {
            //update voltage axis
            if (ch is AnalogChannel || ch is MathChannel || ch is OperatorAnalogChannel || ch is ReferenceChannel)
            {
                float probeGain = 1;
                if (ch is AnalogChannel)
                    probeGain = (float)Math.Abs(((AnalogChannel)ch).Probe.Gain);

                if (!(ch is OperatorAnalogChannel)) //difficult case, as analog channels can have both x1 and x10 sources... so let's not impose any constraint
                {
                    //FIXME: scope should dictate ranges!
                    //first detect whether the current operation would bring the V/div out of allowable range
                    //if so: set ratioChange.Y so the final result will equal the allowable range
                    if (divVertical > MAX_VOLTAGE_PER_DIVISION * probeGain)
                        divVertical = MAX_VOLTAGE_PER_DIVISION * probeGain;
                    if (divVertical < MIN_VOLTAGE_PER_DIVISION * probeGain)
                        divVertical = MIN_VOLTAGE_PER_DIVISION * probeGain;
                }

                double totalRange = divVertical * GridAnalog.DivisionsVerticalMax;
                float ratioChangeY = (float)(totalRange / Waveform.Waveforms[ch].VoltageRange);

                SetVerticalRange(ch, Waveform.Waveforms[ch].VoltageRange * ratioChangeY);
                float newYOffset = Waveform.Waveforms[ch].VoltageOffset * ratioChangeY;
                SetYOffset(ch, newYOffset);                
            }
            else if (ch is DigitalChannel || ch is ProtocolDecoderChannel || ch is OperatorDigitalChannel)
            {
                double totalRange = divVertical * GridAnalog.DivisionsVerticalMax;
                float ratioChangeY = (float)(Waveform.Waveforms[ch].VoltageRange / totalRange);

                float newYOffset = Waveform.Waveforms[ch].VoltageOffset * ratioChangeY;
                SetYOffset(ch, newYOffset);
                AutoSpaceDigiWaves(ch);
            }
        }

        internal void SetGridAnchor(GridAnchor ga)
        {
            gridAnchor = ga;
            SetViewportCenterAndTimespan(ViewportCenter, ViewportTimespan);
        }
        internal void TogglePanoramaByUser()
        {
            if (scope.Rolling && scope.Running)
            {
                ShowToast("NO_TIMEBAR_WHILE_ROLLING", null, null, Color.White, "Can't use panorama while rolling\nYou can however seek the acquistion once stopped", Location.Center, Location.Center, Location.Center, 3000);
                return;
            }
            PanoramaEnabledPreference = !PanoramaVisible;
            ShowPanorama(!PanoramaVisible);
        }

        internal void SetTickStickyNess(TickStickyNess ts)
        {
            tickStickyNess = ts;
            graphManager.SelectorTriggerVerPos.stickyOffset = 0f;
            graphManager.SelectorTriggerHorPos.stickyOffset = graphManager.Graphs[GraphType.Analog].Grid.HorizontalOffsetModuloMajorTickSpacing;

            switch (ts)
            {
                case TickStickyNess.Off:
                    graphManager.SelectorTriggerVerPos.stickyInterval = 0f;
                    graphManager.SelectorTriggerHorPos.stickyInterval = 0f;
                    foreach (WaveformAnalog w in WaveformAnalog.Waveforms.Values)
                        w.OffsetIndicator.stickyInterval = 0f;
                    break;
                case TickStickyNess.Major:
                    graphManager.SelectorTriggerVerPos.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.VerticalTickSpacingMajor; ;
                    graphManager.SelectorTriggerHorPos.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.HorizontalTickSpacingMajor; ;
                    foreach (WaveformAnalog w in WaveformAnalog.Waveforms.Values)
                        w.OffsetIndicator.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.VerticalTickSpacingMajor;
                    break;
                case TickStickyNess.Minor:
                    graphManager.SelectorTriggerVerPos.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.VerticalTickSpacingMinor;
                    graphManager.SelectorTriggerHorPos.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.HorizontalTickSpacingMinor; ;
                    foreach (WaveformAnalog w in WaveformAnalog.Waveforms.Values)
                        w.OffsetIndicator.stickyInterval = graphManager.Graphs[GraphType.Analog].Grid.VerticalTickSpacingMinor;
                    break;
            }
        }
        internal void FixGridVertically()
        {
            FixGridVertically(selectedChannel);
        }
        internal void FixGridVertically(Channel ch)
        {
            if (!(
                selectedChannel is AnalogChannel || 
                selectedChannel is ProtocolDecoderChannel ||
                selectedChannel is OperatorAnalogChannel || 
                selectedChannel is ReferenceChannel ||
                selectedChannel is MathChannel))
                return;
            SnapActiveWaveToFixedGrid(Drawables.Grid.DivisionsVerticalMax, ch);

            if (selectedChannel is AnalogChannel)
                SyncWavesToGrid((AnalogChannel)selectedChannel);
        }
        private void SnapActiveWaveToFixedGrid(double numberOfVerticalGridDivisions, Channel ch)
        {
            //first find to which V/div the grid&wave should be snapped to
            double actualVDiv = Waveform.Waveforms[ch].VoltageRange / numberOfVerticalGridDivisions;
            double minVDiv = Utils.getRoundDivisionRange(actualVDiv, Utils.RoundDirection.Down);
            double maxVDiv = Utils.getRoundDivisionRange(actualVDiv, Utils.RoundDirection.Up);
            double snappedVDiv = 0;
            if (Math.Abs(actualVDiv - minVDiv) < Math.Abs(actualVDiv - maxVDiv))
                snappedVDiv = minVDiv;
            else
                snappedVDiv = maxVDiv;

            //commit
            SetChannelDivVertical((float)snappedVDiv, ch);
        }

        internal void WaveDragEnd()
        {
            if (selectedChannel is DigitalChannel || selectedChannel is ProtocolDecoderChannel || selectedChannel is OperatorDigitalChannel)
            {
                AutoSpaceDigiWaves(null);
            }
        }

        private float pinchBeginOffsetRelative = -100f;
        internal void PinchBegin()
        {
            if (selectedChannel is AnalogChannel)
                pinchBeginOffsetRelative = Waveform.Waveforms[selectedChannel].VoltageOffset / (float)Waveform.Waveforms[selectedChannel].VoltageRange;
        }

        internal void PanZoomEnd()
        {
            PanZoomEnd(selectedChannel);
        }

        internal void PanZoomEnd(Channel ch)
        {
            FixGridVertically(ch);
			canPerformVerticalScaling = true;

            //if the pinch caused only a minor offset shift, this shift probably was not intended at all -> reset to original offset!
            if (pinchBeginOffsetRelative != -100f)
            {
                float pinchEndOffsetRelative = Waveform.Waveforms[selectedChannel].VoltageOffset / (float)Waveform.Waveforms[selectedChannel].VoltageRange;
                if (Math.Abs(pinchEndOffsetRelative - pinchBeginOffsetRelative) < 1.0/Drawables.Grid.DivisionsVerticalMax)
                    SetYOffset(selectedChannel, pinchBeginOffsetRelative * (float)Waveform.Waveforms[selectedChannel].VoltageRange);
            }
            pinchBeginOffsetRelative = -100f;

            if (selectedChannel is DigitalChannel || selectedChannel is ProtocolDecoderChannel || selectedChannel is OperatorDigitalChannel)
                AutoSpaceDigiWaves(null);
        }
        internal void GridRightClicked(Point location)
        {
            List<Channel> channelsClicked = Waveform.ChannelsAt(location);
            //FIXME: dirty FFT select fix
            channelsClicked = channelsClicked.Where(x => !(x is FFTChannel)).ToList();
            if (channelsClicked.Count == 0)
                return;

            if (selectedChannel == null || !channelsClicked.Contains(selectedChannel))
            {
                channelsClicked.Sort(Channel.CompareByOrder);
                SelectChannel(channelsClicked[0]);
            }
            ShowMenuChannel(selectedChannel);
        }
        void UpdateUiRanges(Grid g)
        {
            if (selectedChannel == null)
                return;
            //Update UI
            
            foreach (KeyValuePair<Channel, Waveform> kvp in Waveform.EnabledWaveformsVisible)
            {
                kvp.Value.TimeRange = this.ViewportTimespan;
                if (kvp.Value.PanoramaWave != null)
                    kvp.Value.PanoramaWave.TimeRange = this.AcquisitionLength;
            }

            double horizontalRange = ViewportTimespan;//FIXME: scopeMode == ScopeMode.ScopeFrequency ? TimeRangeToFrequency(ViewportTimespan) : ViewportTimespan;


            //code below contains crash protection based on a crashreport 
            Waveform analogSelectedChannel = Waveform.Waveforms.SingleOrDefault(x => x.Key.Name == Settings.Current.LastSelectedAnalogChannel).Value;
            if (analogSelectedChannel == null)
            {
                Logger.Error("Could not find " + Settings.Current.LastSelectedAnalogChannel + " in Waveforms. Preventing crash.");
                analogSelectedChannel = Waveform.Waveforms.SingleOrDefault(x => x.Key.Name == AnalogChannel.ChA.Name).Value;
            }
            graphManager.Graphs[GraphType.Analog].Grid.UpdateScalersOffsets(
                horizontalRange,
                gridAnchor == GridAnchor.AcquisitionBuffer ? -ViewportCenter : 0,
                analogSelectedChannel.VoltageRange, LinLog.Linear, -analogSelectedChannel.VoltageOffset, Settings.Current.LastSelectedAnalogChannel == "A" ? AnalogChannel.ChA : AnalogChannel.ChB);

            Waveform digitalSelectedChannel = Waveform.Waveforms.SingleOrDefault(x => x.Key.Name == Settings.Current.LastSelectedDigitalChannel).Value;
            if (digitalSelectedChannel == null)
            {
                Logger.Error("Could not find " + Settings.Current.LastSelectedDigitalChannel + " in Waveforms. Preventing crash.");
                digitalSelectedChannel = Waveform.Waveforms.SingleOrDefault(x => x.Key.Name == DigitalChannel.Digi0.Name).Value;
            }
            graphManager.Graphs[GraphType.Digital].Grid.UpdateScalersOffsets(
                horizontalRange,
                gridAnchor == GridAnchor.AcquisitionBuffer ? -ViewportCenter : 0,
                digitalSelectedChannel.VoltageRange, LinLog.Linear, 0, DigitalChannel.Digi0);
            
            double leftside = ViewportOffset / AcquisitionLength;
            double rightside = leftside + (ViewportTimespan / AcquisitionLength);

            panoramaSplitter.PanoramaShading.CoverLeftOffset = (float)leftside;
            panoramaSplitter.PanoramaShading.CoverRightOffset = (float)rightside;

            SetTickStickyNess(tickStickyNess);
            graphManager.Graphs[GraphType.Analog].Grid.UpdateCursors(TriggerHoldoff, ViewportCenter);
            graphManager.Graphs[GraphType.Digital].Grid.UpdateCursors(TriggerHoldoff, ViewportCenter);
        }

        #endregion

        #region Cursors

        internal bool AddCursor(EDrawable sender, Grid grid, Vector2 relativeLocation, GestureSample gesture)
        {
            Cursor cursor;
            //Closer to left or right border than top or bottom border? Time cursor
            bool draggedInHorizontally = Math.Abs(Math.Abs(relativeLocation.X) - 0.5f) < Math.Abs(Math.Abs(relativeLocation.Y) - 0.5f);
            if (draggedInHorizontally)
            {
                ValueFromLocation convertor = null;
                string unit = "";
                //FIXME: FFT
                /*
                switch (scopeMode)
                {
                    case ScopeMode.ScopeFrequency:
                        convertor = CursorLocationToFrequency;
                        unit = "Hz";
                        break;
                    case ScopeMode.LogicAnalyser:
                    case ScopeMode.ScopeTime:*/
                        convertor = CursorLocationToTime;
                        unit = "s";
                        //break;
                //}
                cursor = grid.AddCursor(relativeLocation, unit, convertor, cursorTimePrecision);
                verticalCursors[cursor] = grid;
            }
            else
            {
                bool canMakeCursor = selectedChannel is AnalogChannel || selectedChannel is MathChannel || selectedChannel is OperatorAnalogChannel || selectedChannel is ReferenceChannel;
                if (!canMakeCursor || !(grid is GridAnalog))
                    return false;
                if (!Waveform.Waveforms[selectedChannel].Visible)
                {
                    List<Channel> replacement = Waveform.EnabledWaveformsVisible.Where(x => x.Key is AnalogChannel).Select(x => x.Key).ToList();
                    if (replacement.Count == 0)
                        return false;
                    SelectChannel(replacement.First());
                }
                cursor = (Waveform.Waveforms[selectedChannel] as WaveformAnalog).AddCursor(relativeLocation, averagedVoltagePrecision);

				//in case of audioscope: display warning that voltages are not correct
				if (scope is DummyScope) {
					DummyScope dummy = scope as DummyScope;
					if (dummy.isAudio) {
						ShowSimpleToast ("WARNING: voltages are never absolute/correct when using an AudioScope!", 5000);
					}
				}
            }
            return sender.PassGestureControl(cursor, gesture);
        }
        internal void VerticalCursorTapped(CursorVertical cursor)
        {
            gm.Graphs[GraphType.Analog].Grid.CycleVerticalDeltaCursor(cursor);

            if (ContextMenu.Visible)
                HideContextMenu();
            else
            {
                List<EContextMenuItem> menuItems = new List<EContextMenuItem>();
                //list all matching waves in the dropdown menu
                Dictionary<EContextMenuItemButton, bool> refItems = new Dictionary<EContextMenuItemButton, bool>();

                //define strings to be printed on buttons
                List<ButtonTextDefinition> button1Strings = new List<ButtonTextDefinition>();
                button1Strings.Add(new ButtonTextDefinition("REF", VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                button1Strings.Add(new ButtonTextDefinition("Trigger", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Medium));
                refItems.Add(new EContextMenuItemButton(null, button1Strings, "", new DrawableCallback(UICallbacks.ChangeCursorReference, true)), !CursorVertical.TriggerAttachedToScreen);

                List<ButtonTextDefinition> button2Strings = new List<ButtonTextDefinition>();
                button2Strings.Add(new ButtonTextDefinition("REF", VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                button2Strings.Add(new ButtonTextDefinition("Screen", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Medium));
                refItems.Add(new EContextMenuItemButton(null, button2Strings, "", new DrawableCallback(UICallbacks.ChangeCursorReference, false)), CursorVertical.TriggerAttachedToScreen);

                menuItems.Add(new EContextMenuDropdown(gm, refItems));
                ContextMenu.Show(cursor, cursor.Center, menuItems);
            }
        }
        internal void HorizontalCursorTapped(CursorHorizontal cursor)
        {
            cursor.waveform.CycleHorizontalDeltaCursor(cursor);
        }
        internal void RemoveCursor(Cursor cursor)
        {
            if (cursor is CursorHorizontal)
                (cursor as CursorHorizontal).waveform.RemoveCursor(cursor as CursorHorizontal);
            else if (cursor is CursorVertical)
            {
                verticalCursors[cursor].RemoveVerticalCursor(cursor as CursorVertical);
                verticalCursors.Remove(cursor);
            }
            else if (cursor is CursorVerticalDelta)
            {
                cursor.Grid.RemoveVerticalDeltaCursor(cursor as CursorVerticalDelta);
            }
            else if (cursor is CursorHorizontalDelta)
            {
                CursorHorizontalDelta hdCursor = (CursorHorizontalDelta)cursor;
                hdCursor.waveform.RemoveHorizontalDeltaCursor();
            }
        }

        internal void OffsetIndicatorMoved(Channel ch, float indicatorRelPos)
        {
            if (ch is AnalogChannel)
            {
                SetYOffset(ch, indicatorRelPos * (float)Waveform.Waveforms[ch].VoltageRange + (ch as AnalogChannel).Probe.Offset);
                UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
            }
            else if (ch is OperatorAnalogChannel || ch is ReferenceChannel)
            {
                SetYOffset(ch, indicatorRelPos * (float)Waveform.Waveforms[ch].VoltageRange);
                UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
            }
            else if ((ch is DigitalChannel) || (ch is ProtocolDecoderChannel) || (ch is OperatorDigitalChannel))
            {
                SetYOffset(ch, indicatorRelPos * (float)Waveform.Waveforms[ch].VoltageRange);
                AutoSpaceDigiWaves(ch);
            }
        }
        internal void OffsetIndicatorDropped(Channel ch, float indicatorRelPos)
        {
            List<KeyValuePair<Channel, Waveform>> visibleDigiWaves = Waveform.EnabledWaveformsVisible.Where(x => x.Key is DigitalChannel).ToList();
            if (visibleDigiWaves.Count > 0)
                AutoSpaceDigiWaves(null);
        }

        #endregion
    }
}
