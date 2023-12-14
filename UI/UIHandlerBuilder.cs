using ESuite.Drawables;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.Common;
using ESuite.DataStorage;

namespace ESuite
{
    internal partial class UIHandler
    {
        EDropDown triggerDropDown;
        EDropDownItem triggerDropDownRollingModeItem;
        EButtonImageAndTextSelectable recordButton, forceTriggerButton, startButton, screenshotButton;
        List<Spacer> BottomBarButtonSpacers = new List<Spacer>();
        EDropDown fileTypeDropDown;

        Panel bottomBarLeft, bottomBarRight;
        SplitPanel bottomBar;
        Stack leftPanelStack;
        SplitPanel sidemenuGraphareaSplitter;
        EToggleButtonTextInImage connectedIndicator;

        MenuPanel leftMenuPanel;
        MenuPanel rightMenuPanel;
        MenuPanel wifiMenuPanel;
        Stack toplevel;
        GraphManager gm;
        Stack guiStack;
        EButtonImageAndTextSelectable helpButton;
        EButtonImageAndTextSelectable etsButton;
        EContextMenu ContextMenu;
        ELogBox logBox;
        List<EDrawable> numpads = new List<EDrawable>();
        private PanoramaSplitter panoramaSplitter;
        private Stack menuAndGraphStack;

        GraphManager graphManager { get { return this.gm; } }
        public bool Pinching { get { return gm.Graphs[GraphType.Analog].Grid.Pinching; } }

        private EDrawable BuildUI()
        {
            /* Grid */
            panoramaSplitter = new Drawables.PanoramaSplitter();
            gm = new GraphManager(MIN_TIME_PER_DIVISION_SMARTSCOPE, MAX_TIME_PER_DIVISION, panoramaSplitter.Panorama);
            panoramaSplitter.Init(gm);

            //Set up GUI
            intervalsVisibility = new Dictionary<Channel, IntervalsVisibility>();
            Waveform.Waveforms.Clear();

            SetHighBandwidthMode(Settings.Current.HighBandwidthMode.Value);
            foreach (AnalogChannel ch in AnalogChannel.List)
            {
                float probeGain = (float)Math.Abs(ch.Probe.Gain);
                gm.AddWaveform(ch, MIN_VOLTAGE_PER_DIVISION * probeGain, MAX_VOLTAGE_PER_DIVISION * probeGain);
            }
            foreach (DigitalChannel ch in DigitalChannel.List)
            {                
                gm.AddWaveform(ch, 0, 0);
                intervalsVisibility.Add(ch, IntervalsVisibility.WhenActive);
            }

            /* Bottom bar and its buttons */
            float margin = Scaler.ButtonMargin;

            float bottomBarHeight = 50.ToInchesBaseDpi();
            connectedIndicator = new EToggleButtonTextInImage(null,
                "indicator-usb", "indicator-usb",
                MappedColor.Selected, MappedColor.Record, 
                MappedColor.Font, MappedColor.Font, false, false);
            connectedIndicator.TapCallback = new DrawableCallback(UICallbacks.ToggleLog);

            EButtonImage mainMenuButton = new EButtonImage("menu-labnation", Location.Top, Location.Left, MappedColor.MenuButtonForeground, MappedColor.MenuButtonBackground);
            mainMenuButton.TapCallback = new DrawableCallback(UICallbacks.ToggleGlobalMenu);

            triggerDropDownRollingModeItem = new EDropDownItem(
                "rolling mode", 
                new DrawableCallback(UICallbacks.SelectRollingMode, null), "roll", true, "roll");
            List<EDropDownItem> triggerItemDefinitionList = new List<EDropDownItem>() {
                triggerDropDownRollingModeItem,
                new EDropDownItem("auto triggering", new DrawableCallback(UICallbacks.SetAcquisitionMode, AcquisitionMode.AUTO), "auto", false, AcquisitionMode.AUTO),
                new EDropDownItem("require trigger", new DrawableCallback(UICallbacks.SetAcquisitionMode, AcquisitionMode.NORMAL), "normal", false, AcquisitionMode.NORMAL),
                new EDropDownItem("single trigger", new DrawableCallback(UICallbacks.SetAcquisitionMode, AcquisitionMode.SINGLE), "single", false, AcquisitionMode.SINGLE)
            };
            triggerDropDown = new EDropDown(triggerItemDefinitionList, 0);

            forceTriggerButton = new EButtonImageAndTextSelectable(
                "button-force-trigger-empty", MappedColor.ButtonBarForeground,
                "button-force-trigger-full", MappedColor.ButtonBarForeground,
                "trigger", MappedColor.ButtonBarForeground,
                "trigger", MappedColor.ButtonBarForeground,
                MappedColor.Highlight,
                MappedColor.ButtonBarForeground);
            BottomBarButtonSpacers.Add(new Spacer(Orientation.Horizontal, 0f));
            startButton = new EButtonImageAndTextSelectable(
                "button-play",
                MappedColor.Selected,
                "button-stop", MappedColor.ButtonBarForeground,
                "start", MappedColor.ButtonBarForeground,
                "stop", MappedColor.ButtonBarForeground);
            BottomBarButtonSpacers.Add(new Spacer(Orientation.Horizontal, 0f));
            recordButton = new EButtonImageAndTextSelectable(
                "button-record-empty", MappedColor.Record,
                "button-record-full", MappedColor.Record,
                "record", MappedColor.ButtonBarForeground,
                "record", MappedColor.ButtonBarForeground);
            BottomBarButtonSpacers.Add(new Spacer(Orientation.Horizontal, 0f));
            screenshotButton = new EButtonImageAndTextSelectable(
                "button-screenshot", MappedColor.ButtonBarForeground,
                "button-screenshot", MappedColor.ButtonBarForeground,
                "shot", MappedColor.ButtonBarForeground,
                "shot", MappedColor.ButtonBarForeground);

            List<EDropDownItem> fileTypeDefinitionList = new List<EDropDownItem>();
            StorageFileFormat[] fileTypeValues = new StorageFileFormat[] { StorageFileFormat.MATLAB, StorageFileFormat.CSV };
            foreach (StorageFileFormat f in fileTypeValues)
            {
                fileTypeDefinitionList.Add(
                    new EDropDownItem(
                        EnumExtensions.GetFileExtension(f),
                        new DrawableCallback(UICallbacks.SetStorageFileFormat, f), null, false, f)
                        );
            }

            fileTypeDropDown = new EDropDown(
                fileTypeDefinitionList, 0);

            bottomBarLeft = new Panel() {
                background = MappedColor.ButtonBarBackground,
                margin = margin,
                direction = Direction.Forward,
            };
            bottomBarLeft.AddItem(mainMenuButton);
            bottomBarLeft.AddItem(triggerDropDown);
            bottomBarLeft.AddItem(forceTriggerButton);
            bottomBarLeft.AddItem(BottomBarButtonSpacers[0]);
            bottomBarLeft.AddItem(startButton);
            bottomBarLeft.AddItem(BottomBarButtonSpacers[1]);
            bottomBarLeft.AddItem(recordButton);
            bottomBarLeft.AddItem(BottomBarButtonSpacers[2]);
            bottomBarLeft.AddItem(screenshotButton);
            bottomBarLeft.AddItem(fileTypeDropDown);            

            EButtonImage measurementMenuButton = new EButtonImage("menu-ruler", Location.Top, Location.Left, MappedColor.MenuButtonForeground, MappedColor.MenuButtonBackground);
            measurementMenuButton.TapCallback = new DrawableCallback(UICallbacks.ToggleMeasurementMenu);            

            bottomBarRight = new Panel() {
                background = MappedColor.ButtonBarBackground,
                direction = Direction.Backward,
                margin = margin,
            };
            
            bottomBarRight.AddItem(measurementMenuButton);
            bottomBarRight.AddItem(new Spacer(Orientation.Horizontal, margin));
            bottomBarRight.AddItem(connectedIndicator);

            bottomBar = new SplitPanel(Orientation.Horizontal, 2, SizeType.Inches);
            bottomBar.SetPanel(0, bottomBarLeft);
            bottomBar.SetPanel(1, bottomBarRight);

            guiStack = new Stack();
            guiStack.AddItem(panoramaSplitter);

            Panel helpBtnPanel1 = new Panel()
            {
                orientation = Orientation.Horizontal,
                direction = Direction.Backward
            };
            helpBtnPanel1.AddItem(new Spacer(Orientation.Horizontal, 4.ToInchesBaseDpi()));
            helpButton = new EButtonImageAndTextSelectable(
                "widget-cursor-empty", MappedColor.HelpButton,
                "widget-cursor-full", MappedColor.HelpButton,
                "?", MappedColor.HelpButton,
                "?", MappedColor.GridBorder, false, false, Location.Top, Location.Center, null, null, Location.Right) { 
                TightBoundaries = true, FontSize = 14f
            };
            helpBtnPanel1.AddItem(helpButton);

            Panel helpBtnPanel = new Panel()
            {
                orientation = Orientation.Vertical
            };
            helpBtnPanel.AddItem(new Spacer(Orientation.Vertical, 4.ToInchesBaseDpi()));
            helpBtnPanel.AddItem(helpBtnPanel1);
            guiStack.AddItem(helpBtnPanel);

            //ETS Button
            Panel etsBtnPanel1 = new Panel()
            {
                orientation = Orientation.Horizontal,
                direction = Direction.Forward
            };
            etsBtnPanel1.AddItem(new Spacer(Orientation.Horizontal, 4.ToInchesBaseDpi()));
            List<ButtonTextDefinition> etsOffText = new List<ButtonTextDefinition>();
            etsOffText.Add(new ButtonTextDefinition("ETS", VerticalTextPosition.Center, MappedColor.HelpButton, ContextMenuTextSizes.Medium));
            List<ButtonTextDefinition> etsOnText = new List<ButtonTextDefinition>();
            etsOnText.Add(new ButtonTextDefinition("2GS/s", VerticalTextPosition.Above, MappedColor.Font, ContextMenuTextSizes.Tiny));
            etsOnText.Add(new ButtonTextDefinition("ETS", VerticalTextPosition.Below, MappedColor.Font, ContextMenuTextSizes.Small));
            
            etsButton = new EButtonImageAndTextSelectable(
                "widget-cursor-empty", MappedColor.HelpButton, etsOnText,
                "widget-cursor-full", MappedColor.HelpButton, etsOffText)
                {
                    TightBoundaries = true,
                    FontSize = 14f
                };
            etsBtnPanel1.AddItem(etsButton);

            Panel etsBtnPanel = new Panel()
            {
                orientation = Orientation.Vertical
            };
            etsBtnPanel.AddItem(new Spacer(Orientation.Vertical, 4.ToInchesBaseDpi()));
            etsBtnPanel.AddItem(etsBtnPanel1);
            guiStack.AddItem(etsBtnPanel);

            //create new MenuPanel for left menu
            leftMenuPanel = new Drawables.MenuPanel(true);
            rightMenuPanel = new MenuPanel(false);
            wifiMenuPanel = new Drawables.MenuPanel(false);

            //extra level, which has only 1 element which can therefore easily be swapped between mainMenu and Form
            leftPanelStack = new Stack();
            leftPanelStack.AddItem(leftMenuPanel);

            sidemenuGraphareaSplitter = new SplitPanel(Orientation.Horizontal, 4, SizeType.Inches);
            sidemenuGraphareaSplitter.SetPanel(0, leftPanelStack);
            sidemenuGraphareaSplitter.SetPanel(1, guiStack);
            sidemenuGraphareaSplitter.SetPanel(2, rightMenuPanel);
            sidemenuGraphareaSplitter.SetPanel(3, wifiMenuPanel);
            sidemenuGraphareaSplitter.PanelSizes = new float?[] { Scaler.SideMenuWidth, null, 0, 0};
            
            //gently reshuffly drawing order so menu panels cover all elements of guiStack, as they might be too large (such as waveforms)
            sidemenuGraphareaSplitter.SetHighestDrawPriority(leftPanelStack);
            sidemenuGraphareaSplitter.SetHighestDrawPriority(rightMenuPanel);
            sidemenuGraphareaSplitter.DrawOrder = Direction.Backward;

            /* Paste it all together into splitcontainers */
            SplitPanel splitter1 = new SplitPanel(Orientation.Vertical, 2, SizeType.Inches);
            splitter1.SetPanel(0, sidemenuGraphareaSplitter);
            splitter1.SetPanel(1, bottomBar);
            splitter1.PanelSizes = new float?[] { null, bottomBarHeight };

            ContextMenu = new EContextMenu(gm);

            HelpImage = new EButtonImage("cuecard", Location.Center, Location.Center);

            logBox = new ELogBox(LabNation.Common.LogLevel.DEBUG);
            logBox.Visible = false;

            toplevel = new Stack();
            toplevel.AddItem(splitter1);
            toplevel.AddItem(ContextMenu);
            toplevel.AddItem(HelpImage);
            toplevel.AddItem(logBox);

            panoramaSplitter.MeasurementBox.OnMeasurementBoxPositionChanged += MeasurementBoxMovedHandler;

            return toplevel;
        }

        public void MeasurementBoxMovedHandler(Rectangle newBoundaries)
        {
            Settings.CurrentRuntime.MeasurementBoxPosition = new Vector2(newBoundaries.X, newBoundaries.Y);
        }

        bool mainMenuVisible = true;
        internal void ShowSideMenu(bool show, bool animate)
        {
            DeactivateLeftForm();
            mainMenuVisible = show;
            sidemenuGraphareaSplitter.SetPanelSize(0, show ? Scaler.SideMenuWidth : 0f, animate ? ColorMapper.AnimationTime : 0);

            leftMenu.SubMenuActive = show;
        }
        void ShowMeasurementMenu(bool show, bool animate)
        {
            Settings.Current.MeasurementMenuVisible = show;
            sidemenuGraphareaSplitter.SetPanelSize(2, show ? Scaler.SideMenuWidth : 0f, animate ? ColorMapper.AnimationTime : 0);

            if (show)
            {
                ShowWifiMenu(false, animate);
                ShowMeasurementBox(true);
            }
        }
        public void ShowWifiMenu(bool show, bool animate)
        {
            //in case no bridge detected: close menu anyhow
            SmartScope sscope = scope as SmartScope;
            if (sscope == null)
                show = false;
            else if (sscope.WifiBridge == null) show = false;            

            Settings.Current.WifiMenuVisible = show;
            sidemenuGraphareaSplitter.SetPanelSize(3, show ? Scaler.SideMenuWidth : 0f, animate ? ColorMapper.AnimationTime : 0);

            if (show)
            {
                ShowMeasurementMenu(false, animate);

                //clean AP list and ask for new one
                LabNation.DeviceInterface.Devices.IWifiBridge wifiBridge = sscope.WifiBridge;
                RebuildWifiMenu(false, null);
                System.Threading.Thread apFetchThread = new System.Threading.Thread(FetchWifiBridgeAccessPoints);
                apFetchThread.Start();
            }
        }

        List<LabNation.DeviceInterface.Devices.AccessPointInfo> latestAccessPoints;
        void FetchWifiBridgeAccessPoints()
        {
            SmartScope sscope = scope as SmartScope;
            if (sscope == null) return;

            if (sscope.WifiBridge == null) return;
            LabNation.DeviceInterface.Devices.IWifiBridge wifiBridge = sscope.WifiBridge;

            latestAccessPoints = wifiBridge.GetAccessPoints();
            QueueCallback(delegate (EDrawable d, object c)
            {
                RebuildWifiMenu(false, latestAccessPoints);
            });
        }

        /* Screen overlay things */
        internal EButtonImage HelpImage { get; private set; }
        private EToast toast;
        private string toastId;
        private EDialog dialog;

        public EDialogProgress ShowProgressDialog(string message, float progress)
        {
            toplevel.RemoveItem(dialog);
            dialog = new EDialogProgress(message, progress);
            toplevel.AddItem(dialog);
            return dialog as EDialogProgress;
        }

        public void ShowToast(string toastId, EDrawable anchor, string icon, Color iconColor, string message, Location position, Location alignment, Location textAlignment, int hideTimeout = -1)
        {
            if (toast != null)
            {
                //FIXME: we're only supporting 1 simultaneous toast atm (so we hide it)
                HideToast();
            }
            this.toastId = toastId;
            toast = new EToast(anchor, icon, iconColor, message, hideTimeout, position, alignment, textAlignment);
            toast.OnBoundariesChanged();
            toplevel.AddItem(toast);
            toast.Show();
        }

        public void HideToast(string id = null)
        {
            if (toastId == id || id == null)
            {
                toast.Visible = false;
                toplevel.RemoveItem(toast);
            }
        }

        public void ShowDialog(EDialog.DialogItem contents)
        {
            ShowDialog(new EDialog(contents));
        }

        public void ShowScreenWideDialog(string message, List<ButtonInfo> options = null)
        {
            ShowDialog(new EDialogButtons(message, options));
        }

        public void ShowDialog(EDialog dialog)
        {
            //Move to toplevel
            toplevel.RemoveItem(this.dialog);
            this.dialog = dialog;
            toplevel.AddItem(dialog);
            EDrawable.focusedDrawable = dialog;
        }

        public void HideDialog()
        {
            toplevel.RemoveItem(dialog);
        }

        public bool HideContextMenu()
        {
            ContextMenu.Collapse();
            if (!ContextMenu.Visible) return false;
            ContextMenu.Visible = false;
            return true;
        }
        
		internal Scale PreferredUiScale { 
			get { return Settings.Current.GuiScale.Value; } 
			set { Settings.Current.GuiScale = value; }
		}

        internal void ChangeWaveformThickness(int newThickness)
        {
            if (newThickness < 0) newThickness = 0;
            if (newThickness > 10) newThickness = 10;
            Settings.Current.WaveformThickness = newThickness;
            Waveform.Thickness = newThickness;
            foreach (Waveform w in Waveform.EnabledWaveforms.Values)
                w.RebuildVertexBuffer();
        }

		public void SetGuiScale(EDrawable sender, Scale newScale, bool preferLoweringDpiToLoweringScale = false)
        {
            Scaler.CurrentScale = newScale;
            bottomBarScale = Scale.Normal;
			rebuildUI ();
			while (!uiFits())
            {
                if (bottomBarScale == Scale.Tiny)
                {
					//Choose strategy of rescaling:
					//When starting the UI, we prefer setting the scale so that the user's
					//preference (which is "normal" at first start) fits, so if it doesn't
					//we first force a lower DPI
					if (preferLoweringDpiToLoweringScale) {
						if (Scaler.CurrentDpi != Scaler.PixelDensity.DPI_72) {
							//First try lowering DPI, if possible
							Scaler.CurrentDpi = Enum.GetValues (typeof(Scaler.PixelDensity)).Cast<Scaler.PixelDensity> ().Last (e => (int)e < (int)Scaler.CurrentDpi);
							bottomBarScale = Scale.Normal;
							Scaler.CurrentScale = newScale;
							Logger.Info ("Lowering DPI down to [" + Scaler.CurrentDpi + "] to make UI scale [" + Scaler.CurrentScale + "] fit");
						} else {
							//Only then try lowering UI scale
							if (Scaler.CurrentScale == Scale.Tiny)
								break; //Stop trying when also that's not possible anymore
							Scaler.CurrentScale--;
							bottomBarScale = Scale.Normal;
						}
					} else {
						if (Scaler.CurrentScale != Scale.Tiny) {
							//First try lowering UI scale, if still possible
							Scaler.CurrentScale--;
							bottomBarScale = Scale.Normal;
						} else {
							//If lowering UI scale isn't possible, lower DPI and start trying with requested UI scale
							if (Scaler.CurrentDpi == Scaler.PixelDensity.DPI_72)
								break; //Stop trying when even lowering DPI is not possible anymore
							Scaler.CurrentDpi = Enum.GetValues (typeof(Scaler.PixelDensity)).Cast<Scaler.PixelDensity> ().Last (e => (int)e < (int)Scaler.CurrentDpi);
							bottomBarScale = Scale.Normal;
							Scaler.CurrentScale = newScale;
						}
					}
                }
                else
                    bottomBarScale--;
				rebuildUI ();
            }
            if(Scaler.CurrentScale != newScale)
                ShowToast("UI_NOFIT", sender, null, Color.White, newScale.ToString("G") + " is too large for a UI size", Location.Center, Location.Center, Location.Center, 2000);
        }

        private bool uiFits()
        {
            bool uiFits = true;
            uiFits &= !bottomBar.panelContentsTooLarge;
            uiFits &= leftMenu.MaxItemsOnChildPanel*Scaler.MenuItemSize.Y <= sidemenuGraphareaSplitter.Boundaries.Height;
            return uiFits;
        }

        private Scale _bottomBarScale = Scale.Normal;
        private Scale bottomBarScale {
            set
            {
                if (_bottomBarScale == value)
                    return;

                switch (value)
                {
                    case Scale.Tiny:
                        triggerDropDown.useShortText = true;
                        startButton.TextVisible = false;
                        recordButton.TextVisible = false;
                        forceTriggerButton.TextVisible = false;
                        BottomBarButtonSpacers.ForEach(x => x.spacing = Scaler.ButtonMargin);
                        break;
                    case Scale.Small:
                        startButton.TextVisible = true;
                        recordButton.TextVisible = true;
                        forceTriggerButton.TextVisible = true;
                        triggerDropDown.useShortText = true;
                        BottomBarButtonSpacers.ForEach(x => x.spacing = 0f);
                        break;
                    case Scale.Normal:
                        startButton.TextVisible = true;
                        recordButton.TextVisible = true;
                        forceTriggerButton.TextVisible = true;
                        triggerDropDown.useShortText = false;
                        BottomBarButtonSpacers.ForEach(x => x.spacing = 0f);
                        break;
                    default:
                        throw new Exception("Scale unhandled for bottombar");
                }
                _bottomBarScale = value;
            }
            get { return _bottomBarScale; }

        }

        private void rebuildUI()
        {
            toplevel.LoadContentPropagating();
            toplevel.SetBoundaries(Utils.ScreenBoundaries(EDrawable.device, Matrix.Identity, toplevel.View, toplevel.Projection));
            
            RebuildSideMenu();
            RebuildMeasurementMenu();
            RebuildWifiMenu(false, latestAccessPoints);

            //FIXME: this second call is necessary because some elements change size
            //independent of the parent rectangle. This could be fixed by adding a
            //EDrawable.Measure() phase or something.
            toplevel.OnBoundariesChanged();
        }

        internal void ToggleLog()
        {
            logBox.Visible = !logBox.Visible;
        }

        internal delegate void ShowNumPadDelegate(double minimum, double maximum, double precision, string unit, double value, Point location, DrawableCallback onEnter);
        internal void ShowKeyboardNumerical(double minimum, double maximum, double precision, string unit, double value, Point location, DrawableCallback onEnter)
        {
            EKeyboardNumeric numpad = new EKeyboardNumeric(minimum, maximum, precision, "", unit, new DrawableCallbackDelegate(UICallbacks.RemoveKeyboard)) {
                Value = value,
                OnValueEntered = onEnter,
                DragCallback = new DrawableCallback(UICallbacks.DragFloater, null),
                DropCallback = new DrawableCallback(UICallbacks.DropFloater, null),
            };
            numpad.keyboardHandler = numericKeyHandler;

            HideNumPad();
            AddKeyboard(numpad, location);
            EDrawable.focusedDrawable = numpad;
        }
        internal void ShowKeyboardNumericalSi(double minimum, double maximum, double precision, string unit, double value, Point location, DrawableCallback onEnter)
        {
            EKeyboardNumeric numpad = new EKeyboardNumericSi(minimum, maximum, precision, "", unit, new DrawableCallbackDelegate(UICallbacks.RemoveKeyboard))
            {
                Value = value,
                OnValueEntered = onEnter,
                DragCallback = new DrawableCallback(UICallbacks.DragFloater, null),
                DropCallback = new DrawableCallback(UICallbacks.DropFloater, null),
            };
            numpad.keyboardHandler = numericKeyHandler;

            HideNumPad();
            AddKeyboard(numpad, location);
            EDrawable.focusedDrawable = numpad;
        }
        internal void ShowKeyboardAlfa(string value, Point location, int maxNbrChars, DrawableCallback onEnter)
        {
            EKeyboardAlfa numpad = new EKeyboardAlfa("", "", new DrawableCallbackDelegate(UICallbacks.RemoveKeyboard), maxNbrChars)
            {
                Value = value,
                OnValueEntered = onEnter,
                DragCallback = new DrawableCallback(UICallbacks.DragFloater, null),
                DropCallback = new DrawableCallback(UICallbacks.DropFloater, null),
            };
            numpad.keyboardHandler = alphaKeyHandler;

            HideNumPad();
            AddKeyboard(numpad, location);
            EDrawable.focusedDrawable = numpad;
        }
        internal void ShowKeyboardAlfaNumeric(string value, Point location, int maxNbrChars, DrawableCallback onEnter)
        {
            EKeyboardAlfaNumeric numpad = new EKeyboardAlfaNumeric("", "", maxNbrChars, new DrawableCallbackDelegate(UICallbacks.RemoveKeyboard))
            {
                Value = value,
                OnValueEntered = onEnter,
                DragCallback = new DrawableCallback(UICallbacks.DragFloater, null),
                DropCallback = new DrawableCallback(UICallbacks.DropFloater, null),
            };
            numpad.keyboardHandler = alphaKeyHandler;

            HideNumPad();
            AddKeyboard(numpad, location);
            EDrawable.focusedDrawable = numpad;
        }


        internal delegate bool HideNumPadDelegate();
        internal bool HideNumPad()
        {
            EDrawable numpad = numpads.FirstOrDefault(x => x is EKeyboardNumeric);
            if(numpad == null)
                return false;
            RemoveFloater(numpad);
            return true;
        }

        internal void AddKeyboard(EFloater keyboard, Point position)
        {
            if (numpads.Contains(keyboard))
                throw new Exception("Floater already present!");
            numpads.Add(keyboard);
            toplevel.AddItem(keyboard);
            keyboard.SetBoundaries(toplevel.Boundaries);
            MoveFloaterTo(keyboard, position);
            keyboard.Visible = true;
        }

        internal void RemoveFloater(EDrawable floater)
        {
            if (floater == null) return;
            toplevel.RemoveItem(floater);
            numpads.Remove(floater);
            floater.Visible = false;
        }

        internal void MoveFloaterBy(EFloater floater, Point offset)
        {
            MoveFloaterTo(floater, new Point(floater.floaterRectangle.X + offset.X, floater.floaterRectangle.Y + offset.Y));
        }
        internal void MoveFloaterTo(EFloater floater, Point position)
        {
            if(!(numpads.Contains(floater)))
                throw new Exception("This ain't a numpad, can't move it!");
            Rectangle newRectangle = new Rectangle(
                position.X, position.Y,
                floater.floaterRectangle.Width, floater.floaterRectangle.Height);

            floater.floaterRectangle = newRectangle;
            floater.OnBoundariesChanged();
        }

        internal void RemoveFloaterIfOffscreen(EDrawable floater)
        {
            if (Utils.rectangleIntersectionBelowMinimalTouchSize(toplevel.Boundaries, floater.Boundaries))
                RemoveFloater(floater);
        }
    }
}
