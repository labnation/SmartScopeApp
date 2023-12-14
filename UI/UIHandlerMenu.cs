using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using LabNation.Interfaces;
using LabNation.Common;
using ESuite.DataStorage;


#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Memories;

using ESuite.DataProcessors;
using ESuite.Drawables;
using CsvHelper;
using LabNation.DeviceInterface.DataSources;
using CsvHelper.Configuration;
using System.Reflection;
using LabNation.DeviceInterface.Hardware;
using ESuite.Measurements;


namespace ESuite
{
    internal enum IntervalsVisibility { Never, WhenActive, Always }

    internal partial class UIHandler
    {
        private Dictionary<Channel, IntervalsVisibility> intervalsVisibility;
        MenuItem leftMenu;
        MenuItem measurementMenu;
        MenuItem wifiMenu;

        #region Menu builders

        [ImportMany]
        public List<IProcessor> availableProcessors = new List<IProcessor>();
        public void LoadDecoders()
        {
            Logger.Debug("Starting Decoder subsystem");

            AggregateCatalog catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetAssembly(typeof(LabNation.Decoders.DecoderI2C))));

            foreach (string path in LabNation.Common.Utils.PluginPaths)
            {
                try
                {
                    foreach (string filename in Directory.GetFiles(path, "*.dll"))
                    {
                        //Read files to assemblies so they can be overwritten later and reloaded
                        Assembly s = Assembly.Load(System.IO.File.ReadAllBytes(filename));
                        catalog.Catalogs.Add(new AssemblyCatalog(s));
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    Logger.Debug("Plugin directory " + path + " not found");
                }
                catch (Exception e)
                {
                    Logger.Error(String.Format("Adding directory catalog for path [{0}] failed", path));
                    Logger.Error(String.Format("[{0}]{1}", e.GetType().ToString(), e.Message));
                }

            }

            compositionContainer = new CompositionContainer(catalog);

            try
            {
                Logger.Debug(".Composing decoders");
                compositionContainer.ComposeParts(this);
                Logger.Debug(".Adding detected decoders");
                Logger.Debug(".Ordering decoders");
                //order all available decoders alphabetically by name
                availableProcessors = availableProcessors.OrderBy(o => o.Description.Name).ToList();
                foreach (IProcessor p in availableProcessors)
                {
                    Logger.Debug(String.Format("Found processor {0} with version {1}.{2}", p.Description.Name, p.Description.VersionMajor, p.Description.VersionMinor));
                }

                /* only unique entries */
                Logger.Debug(".Reducing decoders");
                Dictionary<Type, IProcessor> uniqueProcessors = new Dictionary<Type, IProcessor>();
                foreach (IProcessor processor in availableProcessors)
                {
                    var desc = processor.Description;
                    if (uniqueProcessors.ContainsKey(processor.GetType()))
                    {
                        IProcessor existingProc = uniqueProcessors[processor.GetType()];
                        //keep only latest version
                        if ((existingProc.Description.VersionMajor < desc.VersionMajor) || ((existingProc.Description.VersionMajor == desc.VersionMajor) && (existingProc.Description.VersionMinor < desc.VersionMinor)))
                            uniqueProcessors[processor.GetType()] = processor;
                    }
                    else
                        uniqueProcessors.Add(processor.GetType(), processor);
                }

                Logger.Debug(String.Format(".Finalizing {0} decoders", uniqueProcessors.Count.ToString()));
                availableProcessors = uniqueProcessors.Select(x => x.Value).ToList<IProcessor>();
            }
            catch (Exception e)
            {
                Logger.Error(String.Format("Exception thrown while loading decoders: [{0}]{1}", e.GetType().ToString(), e.Message));
                if (e.InnerException != null)
                {
                    Logger.Error(String.Format("Inner exception: [{0}]{1}", e.InnerException.GetType(), e.InnerException.Message));
                }

                Logger.Error("DECODERS: seems we found incompatible decoders");
                Logger.Error("DECODERS: please remove any .dll file from the following locations:");
                foreach (string path in LabNation.Common.Utils.PluginPaths)
                    Logger.Error("DECODERS: " + path);

            }
        }
        public void RebuildSideMenu()
        {
            leftMenu = new MenuItem("leftTopLevelDummyName", SideMenu(), ExpansionMode.Sidemenu);
            int menuDepthZeroOffset = 0;
            int maxItemsAnyPanel = 0;
            leftMenu.Init(leftMenuPanel, -1, null, true, ref menuDepthZeroOffset, ref maxItemsAnyPanel); //init so all top-level entries will be set to level 0                        

            //populate first menu panel
            leftMenuPanel.RedefineNumberOfPanels(menuDepthZeroOffset + 1);
            leftMenuPanel.MaxItemsOnAnyPanel = maxItemsAnyPanel;
            leftMenuPanel.Repopulate(0, leftMenuPanel.Boundaries.Y, leftMenu.ChildMenuItems);

            //set SubMenuActive prop of main menu item, to correspond to whether menu is shown or not. Otherwise first click might do nothing.
            leftMenu.SubMenuActive = mainMenuVisible;
        }
        public void RebuildMeasurementMenu()
        {
            measurementMenu = new MenuItem("rightTopLevelDummyName", MeasurementMenu(), ExpansionMode.Carousel);
            int menuDepthZeroOffset = 0;
            int maxItemsAnyPanel = 0;
            measurementMenu.Init(rightMenuPanel, 0, null, false, ref menuDepthZeroOffset, ref maxItemsAnyPanel); //this carousel element is immediately on level0

            //populate first menu panel
            rightMenuPanel.RedefineNumberOfPanels(1);
            rightMenuPanel.MaxItemsOnAnyPanel = maxItemsAnyPanel;
            rightMenuPanel.Repopulate(0, rightMenuPanel.Boundaries.Y, new MenuItem[] { measurementMenu }.ToList());

            //set SubMenuActive prop of main menu item, to correspond to whether menu is shown or not. Otherwise first click might do nothing.
            measurementMenu.SubMenuActive = true;
        }
        public void RebuildWifiMenu(bool currentlyOwnAP, List<AccessPointInfo> apInfos)
        {
            if (apInfos == null)
                wifiMenu = new MenuItem("wifiMenuDummy", new MenuItem[] { new MenuItem("Refreshing APs ...", null) }.ToList(), ExpansionMode.Sidemenu);
            else
                wifiMenu = new MenuItem("wifiMenuDummy", WifiMenu(apInfos), ExpansionMode.Sidemenu);

            //wifiMenu = new MenuItem("wifiMenuDummy", new MenuItem[] { new MenuItem("Select AP to Connect", WifiMenu(), ExpansionMode.Harmonica) }.ToList(), ExpansionMode.Sidemenu);
            //wifiMenu = new MenuItem("Select AP to Connect", WifiMenu(), ExpansionMode.Harmonica);
            int menuDepthZeroOffset = 0;
            int maxItemsAnyPanel = 0;
            wifiMenu.Init(wifiMenuPanel, -1, null, false, ref menuDepthZeroOffset, ref maxItemsAnyPanel); //this carousel element is immediately on level0

            //populate first menu panel
            wifiMenuPanel.RedefineNumberOfPanels(1);
            wifiMenuPanel.MaxItemsOnAnyPanel = maxItemsAnyPanel;
            wifiMenuPanel.Repopulate(0, wifiMenuPanel.Boundaries.Y, wifiMenu.ChildMenuItems);

            //set SubMenuActive prop of main menu item, to correspond to whether menu is shown or not. Otherwise first click might do nothing.
            //wifiMenu.SubMenuActive = true;
        }

        private EForm leftForm = new EForm();
        public EForm Form { get { return this.leftForm; } }
        internal void ActivateLeftForm(string title, string buttonText, List<FormEntryDefinition> entries, DrawableCallback formSubmitCallback, object extraArgumentToBePassedBackByForm = null)
        {
            CloseMenusOnGraphArea(); //this animation will unfortunately only be executed once the leftMenu is part of the update tree again

            leftForm.Redefine(title, buttonText, entries, formSubmitCallback, extraArgumentToBePassedBackByForm);

            //swap leftmenu with form
            leftPanelStack.ClearAllChildren();
            leftPanelStack.AddItem(leftForm);
            leftPanelStack.OnBoundariesChanged();
        }

        internal void ResetWifiBridge()
        {
            SmartScope sscope = scope as SmartScope;
            if (sscope == null) return;

            LabNation.DeviceInterface.Devices.IWifiBridge wifiBridge = sscope.WifiBridge;
            if (wifiBridge == null) return;

            //abort scope
            scope.Running = false;
            System.Threading.Thread.Sleep(100);

            //tell wifiBridge to reset to own AP
            wifiBridge.SetDefaultAccessPoint();
        }

        internal void DeactivateLeftForm()
        {
            if (leftPanelStack.children.First() != leftMenuPanel)
            {
                //swap form with leftmenu
                leftPanelStack.ClearAllChildren();
                leftPanelStack.AddItem(leftMenuPanel);
                leftPanelStack.OnBoundariesChanged();
            }
        }

        private Dictionary<AnalogChannel, Dictionary<Type, MenuItemDualCheckbox>> channelMeasurementGraphCheckboxes = new Dictionary<AnalogChannel, Dictionary<Type, MenuItemDualCheckbox>>();
        private Dictionary<SystemMeasurementType, MenuItemDualCheckbox> systemMeasurementGraphCheckboxes = new Dictionary<SystemMeasurementType, MenuItemDualCheckbox>();

        private List<MenuItem> ChannelMeasurementsMenu(AnalogChannel ch)
        {
            List<MenuItem> allItems = new List<MenuItem>();

            if (channelMeasurementGraphCheckboxes.ContainsKey(ch))
                channelMeasurementGraphCheckboxes.Remove(ch);
            channelMeasurementGraphCheckboxes.Add(ch, new Dictionary<Type, MenuItemDualCheckbox>());

            var channelMeas = Assembly.GetAssembly(typeof(ChannelMeasurement)).GetTypes().Where(myType => myType.IsSubclassOf(typeof(ChannelMeasurement)));
            foreach (Type type in channelMeas)
            {
                //find out whether this measurement's graph is already display. This can be the case when the measurement menu needs to be reconstructed at runtime.
                bool graphDisplayed = false;
                foreach (var item in activeMeasurementGraphs)
                    if (item.Key is ChannelMeasurement)
                        if ((item.Key as ChannelMeasurement).Channel == ch)
                            if (item.Key.GetType() == type)
                                graphDisplayed = true;

                //find friendly name. Problem: C# doesn't support abstract static props, so we cannot 'force' all channelmeasurements to have a static propery.
                //so let's create an instance just for finding the friendly name of the class.
                ChannelMeasurement m = (Activator.CreateInstance(type, ch) as ChannelMeasurement);
                object[] args = new object[] { type, ch };
                MenuItemDualCheckbox mdc = new MenuItemDualCheckbox(m.Name, UICallbacks.ShowChannelMeasurementInBox, args, UICallbacks.ShowChannelMeasurementInGraph, args, null, ExpansionMode.None, graphDisplayed, false, false, true, measurementManager.ActiveChannelMeasurements[ch].ContainsKey(type), "widget-list", "widget-list", "widget-graph-intense", "widget-graph-intense", true);
                allItems.Add(mdc);
                channelMeasurementGraphCheckboxes[ch].Add(type, mdc);
            }

            return allItems;
        }

        private List<MenuItem> SystemMeasurementsMenu()
        {
            List<MenuItem> allItems = new List<MenuItem>();
            systemMeasurementGraphCheckboxes.Clear();

            foreach (var kvp in measurementManager.SystemMeasurements)
            {
                //find out whether this measurement's graph is already display. This can be the case when the measurement menu needs to be reconstructed at runtime.
                bool graphDisplayed = false;
                foreach (var item in activeMeasurementGraphs)
                    if (item.Key == kvp.Value)
                        graphDisplayed = true;

                object[] args = new object[] { kvp.Key };
                string checkboxRightIcon = "widget-graph-intense";
                if (!(kvp.Value is StochasticMeasurement))
                    checkboxRightIcon = null;

                MenuItemDualCheckbox mdc = new MenuItemDualCheckbox(kvp.Value.Name, UICallbacks.ShowSystemMeasurementInBox, args, UICallbacks.ShowSystemMeasurementInGraph, args, null, ExpansionMode.None, graphDisplayed, false, false, true, measurementManager.ActiveSystemMeasurements.Contains(kvp), "widget-list", "widget-list", checkboxRightIcon, checkboxRightIcon, true);
                allItems.Add(mdc);
                systemMeasurementGraphCheckboxes.Add(kvp.Key, mdc);
            }

            return allItems;
        }

        private Dictionary<string, uint> acqDepths = new Dictionary<string, uint>() {
            {"4M Samples", 4 * 1024 * 1024},
            {"2M Samples", 2 * 1024 * 1024},
            {"1M Samples", 1 * 1024 * 1024},
            {"512k Samples",    512 * 1024},
            {"256k Samples",    256 * 1024},
            { "128k Samples",   128 * 1024},
        };

        private MenuItemCheckbox checkboxFftEnabled;
        private MenuItemCheckbox checkboxXyEnabled;

        List<MenuItem> MeasurementMenu()
        {
            List<MenuItem> measMenu = new List<Drawables.MenuItem>();

            measMenu.Add(new MenuItem("System", SystemMeasurementsMenu(), ExpansionMode.Harmonica, false, false, MappedColor.System.C()));
            foreach (AnalogChannel ch in AnalogChannel.List)
                measMenu.Add(new MenuItem("Channel " + ch.Name, ChannelMeasurementsMenu(ch), ExpansionMode.Harmonica, false, false, ch.ToManagedColor().C()));

            return measMenu;
        }

        private List<MenuItem> WifiMenu(List<AccessPointInfo> wifis)
        {
            const float minSigStrength = -90;
            const float maxSigStrength = -20;

            List<MenuItem> wifiMenuItems = new List<Drawables.MenuItem>();
            foreach (AccessPointInfo apInfo in wifis.OrderBy(x => -x.Strength))
            {
                float sigStrength = (minSigStrength - apInfo.Strength) / (maxSigStrength - minSigStrength)*-100;                
                sigStrength = (float)Math.Min(Math.Max(sigStrength, 1), 100); //clip range between [1%, 100%]
                wifiMenuItems.Add(new MenuItem("("+((int)sigStrength).ToString() + "%) " + apInfo.SSID, UICallbacks.ShowConnectToAPForm, apInfo));
            }

            List<MenuItem> items = new MenuItem[] {
                new MenuItem("Refresh Access Points", UICallbacks.RefreshAccessPoints),
                new MenuItem("Connect to AP", wifiMenuItems, ExpansionMode.Harmonica, true
                )
            }.ToList();

            //in case no SmartScope wifi found -> allow to reset bridge to own AP
            if (wifis.Where(x => x.SSID.Contains("SmartScope")).ToList().Count == 0)
                items.Insert(0, new MenuItem("Reset bridge to own AP", UICallbacks.ResetWifiBridge));

            return items;
        }

        public void SwitchWifiBridgeToAP(AccessPointInfo apInfo, string enc, string pass)
        {
            SmartScope sscope = scope as SmartScope;
            if (sscope == null) return;

            LabNation.DeviceInterface.Devices.IWifiBridge wifiBridge = sscope.WifiBridge;
            if (wifiBridge == null) return;

            //abort scope
            scope.Running = false;
            System.Threading.Thread.Sleep(100);

            //tell wifiBridge to switch AP
            wifiBridge.SetAccessPoint(apInfo.SSID, apInfo.BSSID, enc, pass);
        }

        List<MenuItem> SideMenu()
        {
            List<MenuItem> acqDepthItems = new List<MenuItem>();
            foreach (var kvp in acqDepths)
                acqDepthItems.Add(new MenuItem(kvp.Key, UICallbacks.SetAcquisitionDepthUserMaximum, kvp.Value, scope != null && scope.AcquisitionDepthUserMaximum == kvp.Value));

            //////////////////////////////////////////////////////////////
            // UI Size
            List<MenuItem> uiSizeItems = new List<MenuItem>();
            foreach (Scale i in Enum.GetValues(typeof(Scale)))
            {
                uiSizeItems.Add(new MenuItem(i.ToString("G") + " GUI", UICallbacks.ChangeGuiSize, i, Scaler.CurrentScale == i) { Tag = i });
            }            
            MenuItem uiSizeMenuItem = new MenuItem("UI size", uiSizeItems, ExpansionMode.Sidemenu, false, true);

            //special treatment for these checkboxes, as we need to be able to de/select them through other code
            checkboxFftEnabled = new MenuItemCheckbox("Enabled", UICallbacks.EnabledFFT, null, null, ExpansionMode.None, Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogFFT, false, false);
            checkboxFftEnabled.AutoChangeOnTap = false;
            checkboxXyEnabled = new MenuItemCheckbox("Grid enabled", UICallbacks.EnabledXY, null, Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogXY);
            checkboxXyEnabled.AutoChangeOnTap = false;

            int defaultRadioGroupID = -1;

            return new MenuItem[] {
                new MenuItem("Analog mode", UICallbacks.SwitchGraphMode, MainModes.Analog,
                    new MenuItem[] {
                        new MenuItem ("Auto arrange", UICallbacks.HWAutoArrange),
                        new MenuItem ("Add operator", OperatorMenu(GraphType.Analog), ExpansionMode.Sidemenu),
                        new MenuItem ("Add decoder", DecoderMenu(GraphType.Analog), ExpansionMode.Sidemenu),
                        new MenuItem ("FFT", new List<MenuItem>() {
                            checkboxFftEnabled,
                            new MenuItem ("Voltage axis", new List<MenuItem>() {
                                new MenuItem("Voltages", UICallbacks.SetFFTVoltageAxis, LinLog.Linear, Settings.Current.fftVoltageScale.Value == LinLog.Linear, false),
                                new MenuItem("dB", UICallbacks.SetFFTVoltageAxis, LinLog.Logarithmic, Settings.Current.fftVoltageScale.Value == LinLog.Logarithmic, false),
                            }, ExpansionMode.Sidemenu, false, true),
                            new MenuItem ("Frequency axis", new List<MenuItem>() {
                                new MenuItem("Linear", UICallbacks.SetFFTFrequencyAxis, LinLog.Linear, Settings.Current.fftFrequencyScale.Value == LinLog.Linear, false),
                                new MenuItem("Logarithmic", UICallbacks.SetFFTFrequencyAxis, LinLog.Logarithmic, Settings.Current.fftFrequencyScale.Value == LinLog.Logarithmic, false),
                            }, ExpansionMode.Sidemenu, false, true),
                            new MenuItem ("Window function", fftWindowMenu(), ExpansionMode.Sidemenu, false, true),
                        }, ExpansionMode.Sidemenu),
                        new MenuItem ("X-Y Plot", new List<MenuItem>() {
                            checkboxXyEnabled,
                            new MenuItemCheckbox ("Equal axes", UICallbacks.SquareXY, null, Settings.Current.xySquared.Value),
                            new MenuItem ("Axes", new MenuItem[] {
                                new MenuItem("Hor:A | Ver:B", UICallbacks.InvertXY, false, !Settings.Current.xyInverted.Value, false),
                                new MenuItem("Hor:B | Ver:A", UICallbacks.InvertXY, true, Settings.Current.xyInverted.Value, false),
                            }.ToList(), ExpansionMode.Sidemenu,false, true),
                        }, ExpansionMode.Sidemenu),
                        new MenuItem ("Probes", ProbesMenu(), ExpansionMode.Sidemenu),
                    }.ToList (),
                    ExpansionMode.Harmonica,
                    Settings.Current.mainMode == MainModes.Analog,
                    Settings.Current.mainMode == MainModes.Analog,
                    true, defaultRadioGroupID,
                    false),

                new MenuItem ("Digital mode", UICallbacks.SwitchGraphMode, MainModes.Digital,
                    new MenuItem[] {
                        new MenuItem ("Add operator", OperatorMenu(GraphType.Digital), ExpansionMode.Sidemenu),
                        new MenuItem ("Add decoder", DecoderMenu(GraphType.Digital), ExpansionMode.Sidemenu)
                    }.ToList (),
                    ExpansionMode.Harmonica,
                    Settings.Current.mainMode == MainModes.Digital,
                    Settings.Current.mainMode == MainModes.Digital,
                    true, defaultRadioGroupID,
                    false),

                new MenuItem ("Mixed mode", UICallbacks.SwitchGraphMode, MainModes.Mixed,
                    new MenuItem[] {
                        new MenuItem ("Add operator to analog", OperatorMenu(GraphType.Analog), ExpansionMode.Sidemenu),
                        new MenuItem ("Add decoder to analog", DecoderMenu(GraphType.Analog), ExpansionMode.Sidemenu),
                        new MenuItem ("Add operator to digital", OperatorMenu(GraphType.Digital), ExpansionMode.Sidemenu),
                        new MenuItem ("Add decoder to digital", DecoderMenu(GraphType.Digital), ExpansionMode.Sidemenu)
                    }.ToList (),
                    ExpansionMode.Harmonica,
                    Settings.Current.mainMode == MainModes.Mixed,
                    Settings.Current.mainMode == MainModes.Mixed,
                    true, defaultRadioGroupID,
                    false),

                
#if DEBUG
                DigitalOutputMenu(),
#endif
                AwgMenu(),
                DebugMenu(),
                DummyScopeMenu(),
#if LABDEVICES
                new MenuItem("DG4000", new MenuItem[] {
                    new MenuItem("Shape", new MenuItem[] {
                        new MenuItem("Square", GUI_SetDg4000Waveform, WaveForm.SQUARE),
                        new MenuItem("Sine",       GUI_SetDg4000Waveform, WaveForm.SINE),
                        new MenuItem("Triangle",   GUI_SetDg4000Waveform, WaveForm.TRIANGLE),
                        new MenuItem("Sawtooth",   GUI_SetDg4000Waveform, WaveForm.SAWTOOTH),
                        new MenuItem("Saw + sine", GUI_SetDg4000Waveform, WaveForm.SAWTOOTH_SINE),
                        new MenuItem("Multisine", GUI_SetDg4000Waveform, WaveForm.MULTISINE),
                        new MenuItem("HB-HU", GUI_SetDg4000Waveform, WaveForm.HALF_BIG_HALF_UGLY),
                    }.ToList(), ExpansionModes.Sidemenu),
                    new MenuItemSlider(GUI_SetDg4000Amplitude, null, "A", 0f, 10f, false, (float)1.0, "V"),
                    new MenuItemSlider(GUI_SetDg4000Offset, null, "DC", -10f, 10f, false, (float)1.0, "V"),
                    new MenuItemSlider(GUI_SetDg4000Frequency, null, "F", 1f, 50e6f, false, (float)Dg4000Frequency, "Hz", true),
                    new MenuItemSlider(GUI_SetDg4000MultisineHarmonic, null, "H", 2, 100, false, (float)Dg4000MultisineHarmonic, "N"),
                    new MenuItem("Upload", GUI_UploadDg4000Waveform)
                }.ToList(), ExpansionModes.Harmonica),

#endif

                

                new MenuItem ("System", new MenuItem[] {                    
                    new MenuItem ("UI", new MenuItem[] {
                        uiSizeMenuItem,
                        new MenuItem ("Wave thickness", new MenuItem[] {
                            new MenuItem("Decrease wave thickness", UICallbacks.ChangeWaveformThickness, -1, null, ExpansionMode.None, false, false, false),
                            new MenuItem("Increase wave thickness", UICallbacks.ChangeWaveformThickness, +1, null, ExpansionMode.None, false, false, false),
                        }.ToList(), ExpansionMode.Sidemenu,false, true),
                        new MenuItem ("Color scheme", new MenuItem[] {
                            new MenuItem("Default mode", UICallbacks.ChangeColorMode, ColorMapper.Mode.NORMAL, ColorMapper.CurrentMode == ColorMapper.Mode.NORMAL, false),
                            new MenuItem("Dark mode", UICallbacks.ChangeColorMode, ColorMapper.Mode.DARK, ColorMapper.CurrentMode == ColorMapper.Mode.DARK, false)
                        }.ToList(), ExpansionMode.Sidemenu,false, true),
                        new MenuItem ("Slider stickiness", new MenuItem[] {
                            new MenuItem("Off", UICallbacks.ChangeStickyTicks, TickStickyNess.Off, tickStickyNess == TickStickyNess.Off, false),
                            new MenuItem("To minor grid", UICallbacks.ChangeStickyTicks, TickStickyNess.Minor, tickStickyNess == TickStickyNess.Minor, false),
                            new MenuItem("To major grid", UICallbacks.ChangeStickyTicks, TickStickyNess.Major, tickStickyNess == TickStickyNess.Major, false),
                        }.ToList(), ExpansionMode.Sidemenu,false, true),
                        new MenuItem ("Grid sticks to...", new MenuItem[] {
                            new MenuItem("Acquisition", UICallbacks.ChangeGridAnchor, GridAnchor.AcquisitionBuffer, gridAnchor == GridAnchor.AcquisitionBuffer, false),
                            new MenuItem("Viewport", UICallbacks.ChangeGridAnchor, GridAnchor.Viewport, gridAnchor == GridAnchor.Viewport, false),
                        }.ToList(), ExpansionMode.Sidemenu,false, true),
                        new MenuItem ("Switch to rolling", new MenuItem[] {
                            new MenuItem("Above 20ms/div", UICallbacks.ChangeAutoRolling, true, Settings.Current.SwitchAutomaticallyToRollingMode.Value, false),
                            new MenuItem("Manual", UICallbacks.ChangeAutoRolling, false, !Settings.Current.SwitchAutomaticallyToRollingMode.Value, false),
                        }.ToList(), ExpansionMode.Sidemenu,false, true),
                    }.ToList(), ExpansionMode.Sidemenu,false, true),
                    new MenuItem ("Recording to file", new MenuItem[] {
                        new MenuItemValue("Interval", UICallbacks.GUI_SetRecordingInterval, null, 0f, 1000000f, 0, UICallbacks.GUI_GetRecordingInterval, "s", false)
                            { OnValueTapCallback = (DrawableCallback)UICallbacks.MenuValueShowNumPad },
                        new MenuItemValue("Acquisitions", UICallbacks.GUI_SetRecordingAcquisitionsPerInterval, null, 0f, 500f, 1, UICallbacks.GUI_GetRecordingAcquisitionsPerInterval, "", false)
                            { OnValueTapCallback = (DrawableCallback)UICallbacks.MenuValueShowNumPad },
                    }.ToList(), ExpansionMode.Sidemenu),
                    new MenuItem ("Measurement graphs", new MenuItem[] {
                        new MenuItemValue("Max timespan", UICallbacks.GUI_SetMeasurementsTimespan, null, 1f, 1000000f, 0, Settings.Current.MeasurementAcquisitionTimespan.Value.TotalMinutes, "m", false)
                            { OnValueTapCallback = (DrawableCallback)UICallbacks.MenuValueShowNumPad }
                    }.ToList(), ExpansionMode.Sidemenu),
                    new MenuItem ("Acquisition depth", acqDepthItems, ExpansionMode.Sidemenu),
                    new MenuItem("Configuration", LoadConfigsMenu(), ExpansionMode.Sidemenu),
                    new MenuItem ("General", new MenuItem[] {
                        new MenuItemCheckbox ("Deferred Rendering", UICallbacks.SwitchRenderMode, null, Settings.Current.RenderMode.Value == RenderMode.Deferred),
                        new MenuItemCheckbox ("High bandwidth mode", UICallbacks.SetHighBandwidthMode, null, Settings.Current.HighBandwidthMode.Value),
					    #if WINDOWS || MONOMAC || LINUX || ANDROID
                        new MenuItemCheckbox ("Auto update", UICallbacks.SetAutoUpdate, null, Settings.Current.AutoUpdate.Value),
					    #endif
                        new MenuItemCheckbox ("Store to Dropbox", UICallbacks.SetStoreToDropbox, null, Settings.Current.StoreToDropbox.Value),
                        new MenuItem ("Reset and quit", UICallbacks.ResetAndQuit),
                    }.ToList(), ExpansionMode.Sidemenu),
                    new MenuItem("About", UICallbacks.ShowAbout),
                    new MenuItem("Manual", UICallbacks.OpenUrl, "http://wiki.lab-nation.com/"),
					#if (!IOS) && (!ANDROID)
					new MenuItem("Quit", UICallbacks.Quit),
					#endif
#if WINDOWS
                    new MenuItem("Install driver", UICallbacks.InstallDriver)
#endif
                }.ToList(), ExpansionMode.Sidemenu)
            }.ToList();
        }

        //for each custom config file found on disk: adds an entry to the main menu
        List<MenuItem> LoadConfigsMenu()
        {
            List<MenuItem> loadConfigsMenus = new List<MenuItem>();

            List<string> customConfigFiles = Settings.RetrieveCustomConfigFiles();
            foreach (string s in customConfigFiles)
            {
                loadConfigsMenus.Add(new MenuItem(s, new MenuItem[] {
                                new MenuItem("Load all settings", UICallbacks.LoadConfig, new object[] { s, null }, false),
                                new MenuItem("Load visual settings", UICallbacks.LoadConfig, new object[] { s, typeof(AttributeSettingVisual) }, false),
                                new MenuItem("Load scope/timing settings", UICallbacks.LoadConfig, new object[] { s, typeof(AttributeSettingScopeTiming) }, false),
                                new MenuItem("Load decoders settings", UICallbacks.LoadConfig, new object[] { s, typeof(AttributeSettingOperators) }, false),
                                new MenuItem("Load measurements settings", UICallbacks.LoadConfig, new object[] { s, typeof(AttributeSettingMeasurements) }, false),
                                new MenuItem("Override", UICallbacks.ChangeColorMode, ColorMapper.Mode.NORMAL, false),
                                new MenuItem("Delete", UICallbacks.ChangeColorMode, ColorMapper.Mode.DARK, false)
                        }.ToList(), ExpansionMode.Harmonica));
            }

            loadConfigsMenus.Add(new MenuItem("Save current", UICallbacks.ShowSaveConfigForm));
            return loadConfigsMenus;
        }

        List<MenuItem> fftWindowMenu()
        {
            List<MenuItem> fftWindowMenuItems = new List<MenuItem>();
            var values = Enum.GetValues(typeof(DataProcessors.FFTWindow)).Cast<DataProcessors.FFTWindow>();
            foreach (var window in values)
                fftWindowMenuItems.Add(new MenuItem(window.ToString(), UICallbacks.ChangeFFTWindowFunction, window, Settings.Current.fftWindowType.Value == window, false));

            return fftWindowMenuItems;
        }

        List<MenuItem> DecoderMenu(GraphType graphType)
        {
            List<MenuItem> digitalDecoderMenuItems = new List<MenuItem>();
            List<MenuItem> operatorAnalogMenuItems = new List<MenuItem>();
            List<MenuItem> operatorDigitalMenuItems = new List<MenuItem>();

            foreach (IProcessor proc in availableProcessors)
            {
                if (proc is IDecoder)
                    digitalDecoderMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                else if (proc is IOperatorAnalog)
                    operatorAnalogMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                else if (proc is IOperatorDigital)
                    operatorDigitalMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
            }

            if (digitalDecoderMenuItems.Count == 0)
                digitalDecoderMenuItems.Add(new MenuItem("None found", null));
            if (operatorAnalogMenuItems.Count == 0)
                operatorAnalogMenuItems.Add(new MenuItem("None found", null));
            if (operatorDigitalMenuItems.Count == 0)
                operatorDigitalMenuItems.Add(new MenuItem("None found", null));

            digitalDecoderMenuItems.Add(new MenuItem("Fetch from dropbox", UICallbacks.FetchDecodersFromDropbox));
            digitalDecoderMenuItems.Add(new MenuItem("Refresh", UICallbacks.ReloadDecoders));

            return digitalDecoderMenuItems;
        }

        List<MenuItem> OperatorMenu(GraphType graphType)
        {
            List<MenuItem> digitalDecoderMenuItems = new List<MenuItem>();
            List<MenuItem> operatorAnalogMenuItems = new List<MenuItem>();
            List<MenuItem> operatorDigitalMenuItems = new List<MenuItem>();

            foreach (IProcessor proc in availableProcessors)
            {
                if (proc is IDecoder)
                    digitalDecoderMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                else if (proc is IOperatorAnalog)
                    operatorAnalogMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                else if (proc is IOperatorDigital)
                {
                    operatorDigitalMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                    operatorAnalogMenuItems.Add(new MenuItem(proc.Description.Name, UICallbacks.AddProcessor, new object[] { proc, graphType }, null, ExpansionMode.None, false, false, false));
                }

            }

            if (digitalDecoderMenuItems.Count == 0)
                digitalDecoderMenuItems.Add(new MenuItem("None found", null));
            if (operatorAnalogMenuItems.Count == 0)
                operatorAnalogMenuItems.Add(new MenuItem("None found", null));
            if (operatorDigitalMenuItems.Count == 0)
                operatorDigitalMenuItems.Add(new MenuItem("None found", null));

            operatorAnalogMenuItems.Add(new MenuItem("Fetch from dropbox", UICallbacks.FetchDecodersFromDropbox));
            operatorAnalogMenuItems.Add(new MenuItem("Refresh", UICallbacks.ReloadDecoders));
            operatorDigitalMenuItems.Add(new MenuItem("Fetch from dropbox", UICallbacks.FetchDecodersFromDropbox));
            operatorDigitalMenuItems.Add(new MenuItem("Refresh", UICallbacks.ReloadDecoders));

            if (graphType == GraphType.Analog)
                return operatorAnalogMenuItems;
            else
                return operatorDigitalMenuItems;
        }

        private List<MenuItem> ProbesMenu()
        {
            List<MenuItem> probesMenu = new List<MenuItem>();
            foreach (Probe p in knownProbes)
            {
                List<MenuItem> probeSubMenu = new List<MenuItem>();
                probeSubMenu.Add(new MenuItem("Edit settings", UICallbacks.ShowEditExistingProbeForm, p));
                probeSubMenu.Add(new MenuItem("Remove", UICallbacks.RemoveExistingProbe, p));
                probesMenu.Add(new MenuItem(p.Name + " [" + p.Unit + "]", probeSubMenu, ExpansionMode.Sidemenu));
            }
            probesMenu.Add(new MenuItem("Define new probe", new List<MenuItem>() {
                new Drawables.MenuItem("Attenuation probe (eg 20:1)", UICallbacks.ShowAddNewProbeFormSimplest,null, null, ExpansionMode.None, false, false, false, null, false),
                new Drawables.MenuItem("Relative probe (eg 100mV/A)", UICallbacks.ShowAddNewProbeFormRelative,null, null, ExpansionMode.None, false, false, false, null, false),
                new Drawables.MenuItem("Linear probe (eg 15PSI@10V)", UICallbacks.ShowAddNewProbeFormLinear,null, null, ExpansionMode.None, false, false, false, null, false),
                new Drawables.MenuItem("Offset-based probe", UICallbacks.ShowAddNewProbeFormOffset,null, null, ExpansionMode.None, false, false, false, null, false)
            }, ExpansionMode.Sidemenu));
            return probesMenu;
        }

        MenuItem AwgMenu()
        {
            if (waveGenerator == null)
                return null;

            //store reference, so we can flash it during AWG upload

            return new MenuItem("Generator", null, null,
                new MenuItem[] {
                    new MenuItemCheckbox("Analog", UICallbacks.GeneratorEnableAnalogOut, null, new MenuItem[] {
                        new MenuItem("Shape", new MenuItem[] {
                            new MenuItem("Square",     UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.SQUARE, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.SQUARE, false),
                            new MenuItem("Sine",       UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.SINE, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.SINE, false),
                            new MenuItem("Triangle",   UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.TRIANGLE, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.TRIANGLE, false),
                            new MenuItem("Sawtooth",   UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.SAWTOOTH, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.SAWTOOTH, false),
                            new MenuItem("Saw + sine", UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.SAWTOOTH_SINE, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.SAWTOOTH_SINE, false),
                            new MenuItem("Multisine",  UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.MULTISINE, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.MULTISINE, false),
    #if DEBUG
                            new MenuItem("HB-HU",      UICallbacks.GeneratorSetAnalogWaveform, AnalogWaveForm.HALF_BIG_HALF_UGLY, UICallbacks.GeneratorAnalogWaveform == AnalogWaveForm.HALF_BIG_HALF_UGLY, false),
    #endif
                        }.ToList(), ExpansionMode.Sidemenu, false, true),
                        new MenuItemSlider(UICallbacks.GeneratorSetAnalogAmplitude, null, "amplitude", 0f, 3.3f, 1e-3, UICallbacks.GeneratorAmplitudeScaler, "V") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad }, //mV precision
                        new MenuItemSlider(UICallbacks.GeneratorSetAnalogOffset, null, "offset", -3.3f, 3.3f, 1e-3, UICallbacks.GeneratorAnalogOffset, "V") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad }, //mV precision
                        new MenuItemSlider(UICallbacks.GeneratorSetAnalogFrequency, null, "freq", waveGenerator.GeneratorFrequencyMin, waveGenerator.GeneratorFrequencyMax, 1e-3, UICallbacks.GeneratorAnalogFrequency, "Hz", true) { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad }, //mHz precision
                        new MenuItem("Upload", UICallbacks.GeneratorUploadAnalogWaveform),
                    }.ToList(), ExpansionMode.Sidemenu, Settings.Current.awgEnabled.Value, false, false, null),
                    new MenuItemCheckbox("Digital", UICallbacks.GeneratorEnableDigitalOut, null, new MenuItem[] {
                        new MenuItem("Function", new MenuItem[] {
                            new MenuItem("Counter",     UICallbacks.GeneratorSetDigitalWaveform, DigitalWaveForm.Counter, UICallbacks.GeneratorDigitalWaveform == DigitalWaveForm.Counter, false),
                            new MenuItem("One hot",     UICallbacks.GeneratorSetDigitalWaveform, DigitalWaveForm.OneHot, UICallbacks.GeneratorDigitalWaveform == DigitalWaveForm.OneHot, false),
                            new MenuItem("Marquee",     UICallbacks.GeneratorSetDigitalWaveform, DigitalWaveForm.Marquee, UICallbacks.GeneratorDigitalWaveform == DigitalWaveForm.Marquee, false),
                            new MenuItemValue("Pulse", UICallbacks.GeneratorSetDigitalWaveform, DigitalWaveForm.Pulse, 0, 100, 0.1, 20f, "%", false, UICallbacks.GeneratorDigitalWaveform == DigitalWaveForm.Pulse)
                                { OnValueTapCallback = (DrawableCallback)UICallbacks.MenuValueShowNumPad},
                        }.ToList(), ExpansionMode.Sidemenu, false, true),
                        new MenuItemSlider(UICallbacks.GeneratorSetDigitalSamplePeriod, null, "freq", waveGenerator.GeneratorSamplePeriodMin, waveGenerator.GeneratorSamplePeriodMax, timePrecision, UICallbacks.GeneratorDigitalSamplePeriod, "s", true) { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
                        new MenuItem("Voltage", new MenuItem[] {
                            new MenuItem("3.0V",     UICallbacks.GeneratorSetDigitalVoltage, SmartScope.DigitalOutputVoltage.V3_0, Settings.Current.digitalOutputVoltage == SmartScope.DigitalOutputVoltage.V3_0, false),
                            new MenuItem("5.0V",     UICallbacks.GeneratorSetDigitalVoltage, SmartScope.DigitalOutputVoltage.V5_0, Settings.Current.digitalOutputVoltage == SmartScope.DigitalOutputVoltage.V5_0, false),
                        }.ToList(), ExpansionMode.Sidemenu, false, true),
                        new MenuItem("Upload", UICallbacks.GeneratorUploadDigitalWaveform),
                    }.ToList(), ExpansionMode.Sidemenu, waveGenerator.GeneratorToDigitalEnabled, false, false, null),
                    new MenuItem("Upload from Dropbox", UICallbacks.GUI_UploadAwgWaveformFromDropbox),
                    new MenuItem("Upload from local file", UICallbacks.GeneratorUploadWaveformFromLocalPath),
                }.ToList(), ExpansionMode.Harmonica);
        }
        MenuItem DigitalOutputMenu()
        {
            if (!(scope is SmartScope))
                return null;

            return new MenuItem("Digital output", null, null,
                new MenuItem[] {
                    new MenuItem("Bit 0", UICallbacks.ToggleSmartScopeOutputBit, 0, false),
                    new MenuItem("Bit 1", UICallbacks.ToggleSmartScopeOutputBit, 1, false),
                    new MenuItem("Bit 2", UICallbacks.ToggleSmartScopeOutputBit, 2, false),
                    new MenuItem("Bit 3", UICallbacks.ToggleSmartScopeOutputBit, 3, false),
                }.ToList(), ExpansionMode.Sidemenu, false, false, false);
        }
        MenuItem DummyScopeMenu()
        {
            if (!(scope is DummyScope))
                return null;

            return new MenuItem("Data source", new MenuItem[] {
#if ANDROID
					new MenuItem("AudioJack/Mic", UICallbacks.SetDummyScopeWaveSource, DummyInterface.Audio, (scope is DummyScope) && ((DummyScope)scope).isAudio),
#endif                    
                    new MenuItem("Recorded .mat file", UICallbacks.SetDummyScopeFileSource, null, (scope is DummyScope) && ((DummyScope)scope).isFile, false),
                    new MenuItem("Generator", UICallbacks.SetDummyScopeWaveSource, new DummyInterfaceGenerator(), (scope is DummyScope) && ((DummyScope)scope).isGenerator, false),
                    new MenuItem("Generator config", new MenuItem[] {
                        new MenuItem("Channel A", DummyScopeChannelMenu(AnalogChannel.ChA), ExpansionMode.Sidemenu),
                        new MenuItem("Channel B", DummyScopeChannelMenu(AnalogChannel.ChB), ExpansionMode.Sidemenu),
                    }.ToList(), ExpansionMode.Sidemenu, false, true),                    
                }.ToList(), ExpansionMode.Harmonica);
        }
        List<MenuItem> DummyScopeChannelMenu(AnalogChannel channel)
        {
            if ((scope as DummyScope) == null) return null;
            DummyScopeChannelConfig config = (scope as DummyScope).ChannelConfig[channel];
            return new MenuItem[] {
                new MenuItem("Shape", new MenuItem[] {
                    new MenuItem("Square", UICallbacks.SetDummyScopeWaveform, new object[] {channel, AnalogWaveForm.SQUARE}, config.waveform == AnalogWaveForm.SQUARE, false),
                    new MenuItem("Sine", UICallbacks.SetDummyScopeWaveform, new object[] {channel, AnalogWaveForm.SINE}, config.waveform == AnalogWaveForm.SINE, false),
                    new MenuItem("Triangle", UICallbacks.SetDummyScopeWaveform, new object[] {channel, AnalogWaveForm.TRIANGLE}, config.waveform == AnalogWaveForm.TRIANGLE, false),
                    new MenuItem("Sawtooth", UICallbacks.SetDummyScopeWaveform, new object[] {channel, AnalogWaveForm.SAWTOOTH}, config.waveform == AnalogWaveForm.SAWTOOTH, false),
                    new MenuItem("Saw + sine", UICallbacks.SetDummyScopeWaveform, new object[] {channel, AnalogWaveForm.SAWTOOTH_SINE}, config.waveform == AnalogWaveForm.SAWTOOTH_SINE, false),
                }.ToList(), ExpansionMode.Sidemenu, false, true),
                new MenuItemSlider(UICallbacks.SetDummyAmplitude, channel, "amplitude", 0f, 5f, 1e-3, (float)config.amplitude, "V") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
                new MenuItemSlider(UICallbacks.SetDummyDcOffset, channel, "offset", -3f, 3f, 1e-3, (float)config.dcOffset, "V") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
                new MenuItemSlider(UICallbacks.SetDummyFrequency, channel, "freq", 0.01f, 45e6f, 1e-3, (float)config.frequency, "Hz", true) { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
                new MenuItemSlider(UICallbacks.SetDummyPhase, channel, "phase", -180f, 180f, 0.5, (float)(config.phase / Math.PI * 180.0), "°") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
#if DEBUG
                new MenuItemSlider(UICallbacks.SetDummyDutyCycle, channel, "phase", 5f, 95f, 1, (float)(config.dutyCycle * 100.0), "%") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
#endif
                new MenuItemSlider(UICallbacks.SetDummyNoise, channel, "noise", 0f, .5f, 1e-3, (float)config.noise, "V") { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
            }.ToList();
        }
        List<MenuItem> DigitalChannelsSelectionMenu(DrawableCallbackDelegate checkboxValueChangedCallback, object callbackArgument = null, DrawableCallbackDelegate multipleCheckboxesChangedCallback = null)
        {
            List<MenuItem> l = new List<MenuItem>();
            foreach (DigitalChannel ch in DigitalChannel.List)
            {
                object arg = null;
                if (callbackArgument != null)
                    arg = new object[] { callbackArgument, ch };
                else
                    arg = ch;
                l.Add(new MenuItemCheckbox(ch.Name, checkboxValueChangedCallback, arg, true, multipleCheckboxesChangedCallback));
            }
            return l;
        }
        MenuItem DebugMenu()
        {
#if DEBUG
            return new MenuItem("Debug", new MenuItem[] {
                new MenuItem("Show log", UICallbacks.ToggleLog, null),
                new MenuItem("UI", new MenuItem[] {
                    new MenuItem("Show clickable areas", UICallbacks.DrawInteractiveAreas),
                    new MenuItem("Dialog Test", UICallbacks.TestDialog),
                }.ToList(), ExpansionMode.Sidemenu),
                new MenuItem("Time", new MenuItem[] {
                    new MenuItem("SincTriggering OFF", UICallbacks.EnableSincTriggering, false),
                    new MenuItem("SincTriggering ON", UICallbacks.EnableSincTriggering, true),
                    new MenuItem("EquivalentSampling OFF", UICallbacks.EnableEquivalentSampling, false),
                    new MenuItem("EquivalentSampling ON", UICallbacks.EnableEquivalentSampling, true),
                    new MenuItem("TimeSmoothing OFF", UICallbacks.EnableTimeSmoothing, false),
                    new MenuItem("TimeSmoothing ON", UICallbacks.EnableTimeSmoothing, true),
                    new MenuItem("ETSTimeSmoothing OFF", UICallbacks.EnableETSTimeSmoothing, false),
                    new MenuItem("ETSTimeSmoothing ON", UICallbacks.EnableETSTimeSmoothing, true),
                }.ToList(), ExpansionMode.Sidemenu),
                new MenuItem("Scope", new MenuItem[] {
                    new MenuItemSlider("PW min", UICallbacks.SetTriggerPulseWidthMin, null, "freq" , 10e-9, 20e-3, 10e-9, TriggerPulseWidthMin, "s", true) { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },
                    new MenuItemSlider("PW max", UICallbacks.SetTriggerPulseWidthMax, null, "freq", 10e-9, 20e-3, 10e-9, TriggerPulseWidthMax, "s", true) { DoubleTapCallback = (DrawableCallback)UICallbacks.SliderShowNumPad },

                }.ToList(), ExpansionMode.Sidemenu),
            }.ToList(), ExpansionMode.Harmonica);
#else
            return null;
#endif

        }

        internal bool CollapseAllSideMenus()
        {
            return leftMenu.CollapseAllSideMenus() || measurementMenu.CollapseAllSideMenus();
        }

        internal void ShowScreenshotSavedToast(string filename)
        {
            ShowToast("ScreenshotSaved", screenshotButton, null, Color.White, "Screenshot saved as " + filename, Location.Top, Location.Right, Location.Left, 5000);
        }

        internal bool CloseMenusOnGraphArea()
        {
            bool anyMenuClosed = false;
            //We have to do this in several statement since otherwise if the first one returns
            //true the other close commands won't be called
            anyMenuClosed |= HideContextMenu();
            anyMenuClosed |= triggerDropDown.Collapse();
            anyMenuClosed |= fileTypeDropDown.Collapse();
            anyMenuClosed |= CollapseAllSideMenus();
            anyMenuClosed |= HideNumPad();
            return anyMenuClosed;
        }
        internal void ToggleMainMenu()
        {
            Settings.Current.MainMenuVisible = !mainMenuVisible;
            ShowSideMenu(!mainMenuVisible, true);
        }
        internal void ToggleMeasurementMenu()
        {
            ShowMeasurementMenu(!Settings.Current.MeasurementMenuVisible.Value, true);
        }

        internal void ToggleWifiMenu()
        {
            ShowWifiMenu(!Settings.Current.WifiMenuVisible.Value, true);
        }

        private sealed class DecoderOutputEventRecordMapper : CsvClassMap<DecoderOutputEvent>
        {
            public DecoderOutputEventRecordMapper()
            {
                Map(x => x.StartIndex).Name("start").Index(0);
                Map(x => x.EndIndex).Name("end").Index(1);
                Map(x => x.Text).Name("event").Index(2);
            }
        }
        private sealed class DecoderOutputValueRecordMapper : CsvClassMap<DecoderOutputValueNumeric>
        {
            public DecoderOutputValueRecordMapper()
            {
                Map(x => x.StartIndex).Name("start").Index(0);
                Map(x => x.EndIndex).Name("end").Index(1);
                Map(x => x.Text).Name("event").Index(2);
                Map(x => x.Value).Name("value").Index(3);
            }
        }

        internal void ShowMenuChannel(Channel channel, EDrawable originatingSender = null)
        {
            //small protection when the wave might be hidden during an operation on its own context menu
            if (!Waveform.Waveforms.ContainsKey(channel))
                return;

            Indicator indicator = Waveform.Waveforms[channel].OffsetIndicator;
            //check whether context menu was is already being displayed. if so, we need to close it
            if (indicator.contextMenuShown)
            {
                ContextMenu.Visible = false;
                indicator.contextMenuShown = false;
                return;
            }

            List<EContextMenuItem> items = new List<EContextMenuItem>();
            //Build menu items depending on who is asking for one

            if (Waveform.EnabledWaveformsVisible.Keys.Contains(channel))
            {
                if (channel is MathChannel)
                {
                    Dictionary<EContextMenuItemButton, bool> mathOperationItems = new Dictionary<EContextMenuItemButton, bool>();
                    foreach (DataProcessorMath.Operation i in Enum.GetValues(typeof(DataProcessorMath.Operation)))
                    {
                        mathOperationItems.Add(
                            new EContextMenuItemButton(
                                "icon-" + i.ToString("G").Replace('_', '-').ToLower(),
                                new DrawableCallback(UICallbacks.SetMathOperation, new object[] { channel, i })
                            ),
                            ((MathChannel)channel).processor.operation == i
                        );
                    }
                    items.Add(new EContextMenuDropdown(gm, mathOperationItems));

                    items.Add(new EContextMenuItemButton("icon-remove", new DrawableCallback(
                            UICallbacks.DestroyChannel, channel)));

                }
                else if (channel is ReferenceChannel)
                {
                    items.Add(new EContextMenuItemButton("icon-remove", new DrawableCallback(
                            UICallbacks.DestroyChannel, channel)));
                }
                else if (channel is AnalogChannel)
                {
                    AnalogChannel ch = channel as AnalogChannel;

                    /* Channel coupling */
                    Dictionary<EContextMenuItemButton, bool> couplingItems = new Dictionary<EContextMenuItemButton, bool>();
                    foreach (Coupling i in Enum.GetValues(typeof(Coupling)))
                    {
                        couplingItems.Add(
                            new EContextMenuItemButton("icon-" + i.ToString("G"), new DrawableCallback(UICallbacks.SetChannelCoupling, new object[] { channel, i })),
                            scope.GetCoupling(ch) == i
                        );
                    }
                    items.Add(new EContextMenuDropdown(gm, couplingItems));

                    /* Trigger direction */
                    TriggerEdge currentDir = AnalogTriggerChannel == channel ? TriggerEdge : TriggerEdge.RISING;
                    Dictionary<EContextMenuItemButton, bool> directionItems = new Dictionary<EContextMenuItemButton, bool>();
                    foreach (TriggerEdge i in Enum.GetValues(typeof(TriggerEdge)))
                    {
                        TriggerValue tv = new TriggerValue(triggerValue)
                        {
                            edge = i,
                            mode = analogTriggerMode,
                            channel = ch
                        };
                        directionItems.Add(new EContextMenuItemButton(
                        "icon-" + i.ToString() + "-trigger",
                        new DrawableCallback(UICallbacks.SetTrigger,
                            new object[] {
                                tv,
                                new DrawableCallback(UICallbacks.OffsetIndicatorClicked, channel)
                                })
                        ),
                        currentDir == i);
                    }
                    items.Add(new EContextMenuDropdown(gm, directionItems));

                    /* Probe selection */
                    Probe currentProbe = ch.Probe;
                    Dictionary<EContextMenuItemButton, bool> divisionItems = new Dictionary<EContextMenuItemButton, bool>();
                    foreach (Probe i in knownProbes)
                    {
                        List<ButtonTextDefinition> textDefs = new List<ButtonTextDefinition>();
                        textDefs.Add(new ButtonTextDefinition(i.Name, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Medium));
                        textDefs.Add(new ButtonTextDefinition("[" + i.Unit + "]", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Medium));

                        divisionItems.Add(
                            new EContextMenuItemButton(null, textDefs, null, new DrawableCallback(UICallbacks.SetProbeDivision, new object[] { channel, i })),
                            currentProbe.Unit == i.Unit && currentProbe.Name == i.Name
                        );
                    }
                    items.Add(new EContextMenuDropdown(gm, divisionItems));

                    /* Invert probe */
                    Dictionary<EContextMenuItemButton, bool> invertItems = new Dictionary<EContextMenuItemButton, bool>();
                    invertItems.Add(new EContextMenuItemButton("icon-invert-on", new DrawableCallback(UICallbacks.SetChannelInvert, new object[] { ch, true })), ch.Inverted);
                    invertItems.Add(new EContextMenuItemButton("icon-invert-off", new DrawableCallback(UICallbacks.SetChannelInvert, new object[] { ch, false })), !ch.Inverted);

                    items.Add(new EContextMenuDropdown(gm, invertItems));

                    /* Reference wave */
                    List<ButtonTextDefinition> refwaveStrings = new List<ButtonTextDefinition>();
                    refwaveStrings.Add(new ButtonTextDefinition("Ref", VerticalTextPosition.Center, MappedColor.Font, ContextMenuTextSizes.Large));
                    items.Add(new EContextMenuItemButton(null, refwaveStrings, "", new DrawableCallback(UICallbacks.AddRefChannel, channel)));

                    /* Hide show */
                    if (Waveform.EnabledWaveformsVisible.ContainsKey(channel))
                        items.Add(new EContextMenuItemButton("icon-hide", new DrawableCallback(UICallbacks.ShowChannel, new object[] { channel, false })));
                    else
                        items.Add(new EContextMenuItemButton("icon-hide", new DrawableCallback(UICallbacks.ShowChannel, new object[] { channel, true })));
                }
                else if (channel is ProcessorChannel)
                {
                    //grab internalDecoder
                    DataProcessorDecoder decoderProcessor = (channel as ProcessorChannel).decoder;

                    /* Source channels */
                    //for each of the sourcewaves, build dropdown menu and add to sourceMenuItems
                    Dictionary<string, EContextMenuItem> sourceMenuItems = new Dictionary<string, EContextMenuItem>();
                    foreach (KeyValuePair<string, Type> sourceKvp in decoderProcessor.ChannelTypes)
                    {
                        //find all enabled channels with matching type
                        List<Channel> matchingChannels = Utils.GetEnabledChannelsOfRequestedType(sourceKvp.Value);

                        if (sourceKvp.Value == typeof(bool))
                            matchingChannels.AddRange(Utils.GetEnabledChannelsOfRequestedType(typeof(float)));

                        //list all matching waves in the dropdown menu
                        Dictionary<EContextMenuItemButton, bool> directionItems = new Dictionary<EContextMenuItemButton, bool>();
                        foreach (KeyValuePair<Channel, Waveform> visibleWaveKvp in Waveform.EnabledWaveforms)
                        {
                            if (matchingChannels.Contains(visibleWaveKvp.Key))
                            {
                                //but not the decoder wave itself
                                if (visibleWaveKvp.Key != channel)
                                {
                                    string sourceName = "";
                                    if (visibleWaveKvp.Key is AnalogChannel)
                                        sourceName = "Ch";
                                    else if (visibleWaveKvp.Key is DigitalChannel)
                                        sourceName = "B";
                                    sourceName += visibleWaveKvp.Key.Name;

                                    //define strings to be printed on button
                                    List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                                    if (channel is ProtocolDecoderChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(sourceKvp.Key, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                                        buttonStrings.Add(new ButtonTextDefinition(sourceName, VerticalTextPosition.Below, visibleWaveKvp.Value.GraphColor, ContextMenuTextSizes.Medium));
                                    }
                                    else if (channel is OperatorAnalogChannel || channel is OperatorDigitalChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(sourceName, VerticalTextPosition.Center, visibleWaveKvp.Value.GraphColor, ContextMenuTextSizes.Medium));
                                    }

                                    directionItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.SetDecoderSourceChannel, new object[] { decoderProcessor, sourceKvp.Key, visibleWaveKvp.Key })), visibleWaveKvp.Key == decoderProcessor.SourceChannelMapCopy[sourceKvp.Key]);
                                }
                            }
                        }

                        //in case this input is a nullable type: add possibility to disable input
                        if (decoderProcessor.InputIsNullable[sourceKvp.Key])
                        {
                            //define strings to be printed on button
                            List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                            buttonStrings.Add(new ButtonTextDefinition(sourceKvp.Key, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                            buttonStrings.Add(new ButtonTextDefinition("'0'", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Medium));

                            directionItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.SetDecoderSourceChannel, new object[] { decoderProcessor, sourceKvp.Key, null })), null == decoderProcessor.SourceChannelMapCopy[sourceKvp.Key]);
                        }

                        sourceMenuItems.Add(sourceKvp.Key, new EContextMenuDropdown(gm, directionItems));
                    }

                    /* Parameters */
                    //for each of the parameters, build dropdown menu and add to parameterMenuItems
                    Dictionary<string, EContextMenuItem> parameterMenuItems = new Dictionary<string, EContextMenuItem>();
                    if (decoderProcessor.Processor.Description.Parameters != null)
                    {
                        foreach (DecoderParameter p in decoderProcessor.Processor.Description.Parameters)
                        {
                            //for each param: add new context menu item
                            Dictionary<EContextMenuItemButton, bool> parameterItems = new Dictionary<EContextMenuItemButton, bool>();

                            if (p is DecoderParameterInts)
                            {
                                DecoderParameterInts paramInts = (DecoderParameterInts)p;
                                foreach (int i in paramInts.PossibleValues)
                                {
                                    List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                                    if (channel is ProtocolDecoderChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(p.ShortName, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                                        buttonStrings.Add(new ButtonTextDefinition(i.ToString(), VerticalTextPosition.Below, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                    }
                                    else if (channel is OperatorAnalogChannel || channel is OperatorDigitalChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(i.ToString(), VerticalTextPosition.Center, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                    }

                                    parameterItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.SetDecoderParameter, new object[] { decoderProcessor, p.ShortName, i })), i == (int)decoderProcessor.ParameterValuesCopy[p.ShortName]);
                                }
                            }
                            else if (p is DecoderParameterStrings)
                            {
                                DecoderParameterStrings paramStrings = (DecoderParameterStrings)p;
                                foreach (string s in paramStrings.PossibleValues)
                                {
                                    List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                                    if (channel is ProtocolDecoderChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(p.ShortName, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                                        buttonStrings.Add(new ButtonTextDefinition(s, VerticalTextPosition.Below, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                    }
                                    else if (channel is OperatorAnalogChannel || channel is OperatorDigitalChannel)
                                    {
                                        buttonStrings.Add(new ButtonTextDefinition(s, VerticalTextPosition.Center, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                    }

                                    parameterItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.SetDecoderParameter, new object[] { decoderProcessor, p.ShortName, s })), s == (string)decoderProcessor.ParameterValuesCopy[p.ShortName]);
                                }
                            }
                            else if (p is DecoderParameterNumpadFloat)
                            {
                                DecoderParameterNumpadFloat paramFloatRange = (DecoderParameterNumpadFloat)p;

                                List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                                if (channel is ProtocolDecoderChannel)
                                {
                                    buttonStrings.Add(new ButtonTextDefinition(p.ShortName, VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                                    buttonStrings.Add(new ButtonTextDefinition(decoderProcessor.ParameterValuesCopy[p.ShortName].ToString(), VerticalTextPosition.Below, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                }
                                else if (channel is OperatorAnalogChannel || channel is OperatorDigitalChannel)
                                {
                                    buttonStrings.Add(new ButtonTextDefinition(decoderProcessor.ParameterValuesCopy[p.ShortName].ToString(), VerticalTextPosition.Center, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));
                                }

                                Point position = new Point();
                                if (originatingSender != null)
                                    position = originatingSender.Boundaries.Center;

                                object o = decoderProcessor.ParameterValuesCopy[p.ShortName];
                                Type t = o.GetType();
                                float f = float.Parse(o.ToString());
                                parameterItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.ParameterShowNumpad, new object[] { decoderProcessor, paramFloatRange, f, position })), true);

                            }
                            parameterMenuItems.Add(p.ShortName, new EContextMenuDropdown(gm, parameterItems));
                        }
                    }

                    /* Throw mixture of sourceChans and Params in predefined order*/
                    //order prefefined
                    if (decoderProcessor.ProcessorContextMenuOrder != null)
                    {
                        foreach (string s in decoderProcessor.ProcessorContextMenuOrder)
                        {
                            if (sourceMenuItems.ContainsKey(s))
                            {
                                items.Add(sourceMenuItems[s]);
                                sourceMenuItems.Remove(s);
                            }
                            else if (parameterMenuItems.ContainsKey(s))
                            {
                                items.Add(parameterMenuItems[s]);
                                parameterMenuItems.Remove(s);
                            }
                        }
                    }

                    //add remaining items of which the order was not predefined
                    items.AddRange(sourceMenuItems.Values.ToList());
                    items.AddRange(parameterMenuItems.Values.ToList());

                    /* Radix */
                    if (channel is ProtocolDecoderChannel)
                    {
                        Dictionary<EContextMenuItemButton, bool> radixItems = new Dictionary<EContextMenuItemButton, bool>();
                        foreach (RadixType radix in Enum.GetValues(typeof(RadixType)))
                        {
                            //define strings to be printed on button
                            List<ButtonTextDefinition> buttonStrings = new List<ButtonTextDefinition>();
                            buttonStrings.Add(new ButtonTextDefinition(RadixPrinter.Print(15, 4, radix), VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Large));
                            buttonStrings.Add(new ButtonTextDefinition(radix.ToString().ToUpper(), VerticalTextPosition.Below, MappedColor.FontSubtle, ContextMenuTextSizes.Medium));

                            radixItems.Add(new EContextMenuItemButton(null, buttonStrings, "", new DrawableCallback(UICallbacks.SetDecoderRadix, new object[] { decoderProcessor, radix })), radix == (channel as ProtocolDecoderChannel).RadixType);
                        }
                        items.Add(new EContextMenuDropdown(gm, radixItems));
                    }

                    /* Remove */
                    items.Add(new EContextMenuItemButton("icon-remove", new DrawableCallback(UICallbacks.DestroyChannel, channel)));

                    if (channel is ProtocolDecoderChannel)
                    {
                        items.Add(new EContextMenuItemButton("icon-save", new DrawableCallback(
                            (EDrawable sender, object argument) =>
                            {
                                if (scope.Running)
                                {
                                    ShowDialog("Can't store decoder data while scope is running");
                                    return;
                                }
                                ChannelData decoderData = lastScopeData.GetData(ChannelDataSourceScope.Acquisition, channel);
                                if (lastScopeData.FullAcquisitionFetchProgress < 1f || decoderData == null)
                                {
                                    ShowDialog("Wait for the entire acquisition to come in\nbefore storing the decoder data");
                                    return;
                                }

                                string filename = LabNation.Common.Utils.GetTempFileName(".csv");
                                StreamWriter textWriter = File.CreateText(filename);
                                CsvWriter csvFileWriter = new CsvWriter(textWriter);

                                csvFileWriter.Configuration.RegisterClassMap<DecoderOutputEventRecordMapper>();
                                csvFileWriter.Configuration.RegisterClassMap<DecoderOutputValueRecordMapper>();

                                csvFileWriter.Configuration.HasExcelSeparator = true;
                                csvFileWriter.WriteRecords(decoderData.array);
                                textWriter.Close();

                                StorageFile f = new StorageFile() { info = new FileInfo(filename), format = StorageFileFormat.CSV };
                                f.proposedPath = "/Recordings/SmartScopeDecoder" + channel.Name + f.format.GetFileExtension();
                                if (StoreToDropbox)
                                    QueueCallback(UICallbacks.StoreFileDropbox, f);
                                else
                                    QueueCallback(UICallbacks.StoreFileLocal, f);
                            }, channel)));
                    }
                }
                else if (channel is DigitalChannel)
                {
                    /* Hide show */
                    if (Waveform.EnabledWaveformsVisible.ContainsKey(channel))
                        items.Add(new EContextMenuItemButton("icon-hide", new DrawableCallback(UICallbacks.ShowChannel, new object[] { channel, false })));
                    else
                        items.Add(new EContextMenuItemButton("icon-hide", new DrawableCallback(UICallbacks.ShowChannel, new object[] { channel, true })));
                }
            }
            else
            {
                items.Add(new EContextMenuItemButton("icon-show", new DrawableCallback(UICallbacks.ShowChannel, new object[] { channel, true })));
            }

            /* Rename */
            Dictionary<EContextMenuItemButton, bool> renameItems = new Dictionary<EContextMenuItemButton, bool>();
            List<ButtonTextDefinition> renameButtonStrings = new List<ButtonTextDefinition>();
            renameButtonStrings.Add(new ButtonTextDefinition("Name", VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Medium));
            renameButtonStrings.Add(new ButtonTextDefinition(Waveform.Waveforms[channel].OffsetIndicator.CenterText, VerticalTextPosition.Below, Waveform.Waveforms[channel].GraphColor, ContextMenuTextSizes.Medium));
            renameItems.Add(new EContextMenuItemButton(null, renameButtonStrings, "", new DrawableCallback(UICallbacks.SetWaveformName, new object[] { channel, Waveform.Waveforms })),true);
            List<ButtonTextDefinition> resetNameButtonStrings = new List<ButtonTextDefinition>();
            resetNameButtonStrings.Add(new ButtonTextDefinition("Reset", VerticalTextPosition.Center, MappedColor.Font, ContextMenuTextSizes.Medium));
            renameItems.Add(new EContextMenuItemButton(null, resetNameButtonStrings, "", new DrawableCallback(UICallbacks.ResetWaveformName, new object[] { channel, Waveform.Waveforms })), false);
            items.Add(new EContextMenuDropdown(gm, renameItems));

            ContextMenu.Show(indicator, indicator.Center, items, new DrawableCallback(SetIndicatorHighContrast, new object[] { indicator, false }));
            indicator.contextMenuShown = true;
        }
        internal void SetDigitalWaveIntervalDisplay(DigitalChannel ch, IntervalsVisibility visibility)
        {
            intervalsVisibility[ch] = visibility;
            HideContextMenu();
        }

        internal void UpdateAllIntervalVisibilities()
        {
            UpdateAllIntervalVisibilities(new Point());
        }
        internal void UpdateAllIntervalVisibilities(Point position)
        {
            foreach (KeyValuePair<Channel, IntervalsVisibility> kvp in intervalsVisibility)
            {
                if (Waveform.EnabledWaveforms.ContainsKey(kvp.Key))
                {
                    if (kvp.Value == IntervalsVisibility.Always)
                        ((WaveformDigital)Waveform.EnabledWaveforms[kvp.Key]).ShowIntervals = true;
                    else if (kvp.Value == IntervalsVisibility.Never)
                        ((WaveformDigital)Waveform.EnabledWaveforms[kvp.Key]).ShowIntervals = false;
                    else if (kvp.Value == IntervalsVisibility.WhenActive)
                        ((WaveformDigital)Waveform.EnabledWaveforms[kvp.Key]).ShowIntervals = selectedChannel == kvp.Key;

                    //mouse-over
                    Rectangle boundaries = ((WaveformDigital)Waveform.EnabledWaveforms[kvp.Key]).BackgroundRectangle;
                    if (boundaries.Contains(position))
                        ((WaveformDigital)Waveform.EnabledWaveforms[kvp.Key]).ShowIntervals = true;
                }
            }


        }
        internal void TriggerIndicatorDropped(Indicator sender)
        {

        }
        internal void TriggerLevelIndicatorClicked(Indicator sender)
        {
            if (triggerMode == TriggerMode.Digital)
            {
                CloseMenusOnGraphArea();
                triggerMode = analogTriggerMode;
                SetTrigger();
                return;
            }
            ToggleTriggerContextMenu(sender);
        }

        internal void ToggleTriggerContextMenu(Indicator sender)
        {
            if (sender.contextMenuShown)
            {
                CloseMenusOnGraphArea();
            }
            else
            {
                CloseMenusOnGraphArea();
                ShowTriggerContextMenu(sender);
            }
        }
        internal void HideTriggerContextMenu(Indicator sender)
        {
            ContextMenu.Visible = false;
            sender.contextMenuShown = false;
            return;
        }
        internal void ShowTriggerContextMenu(Indicator sender)
        {
            List<EContextMenuItem> items = new List<EContextMenuItem>();
            //Build menu items depending on who is asking for one

            DrawableCallback reopenTriggerMenuCallback = new DrawableCallback(UICallbacks.ShowMenuTrigger, sender);
            DrawableCallback resetTriggerCallback;

            if (sender == gm.SelectorTriggerVerPos)
                resetTriggerCallback = new DrawableCallback(UICallbacks.ResetTriggerVertically, reopenTriggerMenuCallback);
            else
                resetTriggerCallback = new DrawableCallback(UICallbacks.ResetTriggerHorizontally, reopenTriggerMenuCallback);

            int nextTriggerChannelOrder = (AnalogTriggerChannel.Order + 1) % (AnalogChannel.List.Count);

            TriggerValue tv;
            if (triggerMode != TriggerMode.Digital)
            {
                /* Trigger channel */
                Dictionary<EContextMenuItemButton, bool> channelItems = new Dictionary<EContextMenuItemButton, bool>();
                foreach (AnalogChannel ch in AnalogChannel.List)
                {
                    tv = new TriggerValue(triggerValue)
                    {
                        source = TriggerSource.Channel,
                        channel = ch,
                        mode = analogTriggerMode,
                    };
                    channelItems.Add(new EContextMenuItemButton(
                    "icon-channel-" + ch.Name,
                    new DrawableCallback(UICallbacks.SetTrigger,
                        new object[] {
                                tv,
                                reopenTriggerMenuCallback
                                })
                    ),
                    triggerSource == TriggerSource.Channel && AnalogTriggerChannel == ch);
                }
                tv = new TriggerValue(triggerValue)
                {
                    mode = analogTriggerMode,
                    source = TriggerSource.External
                };
                channelItems.Add(new EContextMenuItemButton(null,
                    new List<ButtonTextDefinition>() {
                        new ButtonTextDefinition("EXT", VerticalTextPosition.Center, MappedColor.ContextMenuText, ContextMenuTextSizes.Medium)
                    }, null,
                    new DrawableCallback(UICallbacks.SetTrigger,
                        new object[] {
                                tv,
                                reopenTriggerMenuCallback
                                })
                    ),
                    triggerSource == TriggerSource.External);
                items.Add(new EContextMenuDropdown(gm, channelItems));


                /* Analog trigger direction */
                Dictionary<EContextMenuItemButton, bool> directionItems = new Dictionary<EContextMenuItemButton, bool>();
                foreach (TriggerEdge i in Enum.GetValues(typeof(TriggerEdge)))
                {
                    tv = new TriggerValue(triggerValue)
                    {
                        mode = analogTriggerMode,
                        edge = i,
                    };
                    directionItems.Add(new EContextMenuItemButton(
                    "icon-" + i.ToString(),
                    new DrawableCallback(UICallbacks.SetTrigger,
                        new object[] {
                                tv,
                                reopenTriggerMenuCallback
                                })
                    ),
                    TriggerEdge == i);
                }
                items.Add(new EContextMenuDropdown(gm, directionItems));

                /* Analog trigger type (edge/pulse/timeout) */
                Dictionary<EContextMenuItemButton, bool> typeItems = new Dictionary<EContextMenuItemButton, bool>();
                foreach (TriggerMode i in Enum.GetValues(typeof(TriggerMode)))
                {
                    if (i == TriggerMode.Digital) continue;
                    tv = new TriggerValue(triggerValue)
                    {
                        mode = i,
                    };
                    typeItems.Add(new EContextMenuItemButton(
                    "icon-" + i.ToString(),
                    new DrawableCallback(UICallbacks.SetTrigger,
                        new object[] {
                                tv,
                                reopenTriggerMenuCallback
                                })
                    ),
                    analogTriggerMode == i);
                }
                items.Add(new EContextMenuDropdown(gm, typeItems));

                /* Add pulse/timeout min/max/length setting */

                DrawableCallback ContextMenuNumpadTap = new DrawableCallback((EDrawable btn, object arg) =>
                {
                    object[] args = (object[])arg;
                    EKeyboardNumeric np = (EKeyboardNumeric)args[0];
                    bool showNumpad = (bool)args[1];
                    HideNumPad();
                    if (showNumpad)
                    {
                        np.Value = ((EContextMenuItemButtonNumpad)btn).Value;
                        np.DragCallback = new DrawableCallback(UICallbacks.DragFloater, null);
                        np.DropCallback = new DrawableCallback(UICallbacks.DropFloater, null);
                        np.keyboardHandler = numericKeyHandler;
                        AddKeyboard(np,
                            new Point(
                                btn.Boundaries.Right - np.Size.Value.X,
                                btn.Boundaries.Bottom
                                )
                            );
                        EDrawable.focusedDrawable = np;
                    }
                });
                if (triggerMode == TriggerMode.Pulse || triggerMode == TriggerMode.Timeout)
                {
                    EContextMenuItemButtonNumpad pw_min = new EContextMenuItemButtonNumpad(
                        "icon-" + (triggerMode == TriggerMode.Pulse ? "pulse-min" : "timeout-length"),
                        TriggerPulseWidthMin, 0, 100, timePrecision, "s",
                        ShowKeyboardNumericalSi, HideNumPad, new DrawableCallback(UICallbacks.SetTriggerPulseWidthMin)
                        );
                    items.Add(pw_min);
                    if (triggerMode == TriggerMode.Pulse)
                    {
                        EContextMenuItemButtonNumpad pw_max = new EContextMenuItemButtonNumpad(
                        "icon-pulse-max",
                        TriggerPulseWidthMax, 0, 100, timePrecision, "s",
                        ShowKeyboardNumericalSi, HideNumPad, new DrawableCallback(UICallbacks.SetTriggerPulseWidthMax)
                        );
                        items.Add(pw_max);
                    }
                }
            }

            items.Add(new EContextMenuItemButton("icon-reset", resetTriggerCallback));

            ContextMenu.Show(sender, sender.Center, items, new DrawableCallback(SetIndicatorHighContrast, new object[] { sender, false }));
            sender.contextMenuShown = true;
        }
        void SetIndicatorHighContrast(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            IndicatorInteractive i = (IndicatorInteractive)args[0];
            i.contextMenuShown = (bool)args[1];
        }

        #endregion
    }
}
