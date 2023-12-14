using System;
using System.Collections.Generic;
using System.Linq;


using Microsoft.Xna.Framework;



#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.Devices;

using ESuite.Drawables;


namespace ESuite
{
    internal partial class UIHandler
    {
        #region Dialogs and conversion helpers

        internal void ShowDialog(string message, List<ButtonInfo> buttons = null)
        {
            if (buttons == null)
                buttons = new List<ButtonInfo>() { new ButtonInfo("OK", UICallbacks.HideDialog, null, ButtonType.ConfirmOrCancel.Keys()) };

            ShowScreenWideDialog(message, buttons);
        }

        internal void ShowFileSelector(int columns, int itemsPerColum, int startingColumn, List<EDialog.Button> fileButtons, DrawableCallback cancelCallback)
        {
            if (fileButtons.Count == 0)
            {
                cancelCallback.Call(null, "No files found");
            }
            float buttonWidth = .1f;
            float fileColumnWidth = (1f - 2 * buttonWidth) / columns;
            int absoluteOffset = startingColumn * itemsPerColum;

            EDialog.Button backButton = null;
            if (startingColumn > 0)
                backButton = new EDialog.Button("<", new DrawableCallback(UICallbacks.ShowFileSelector, new object[] { columns, itemsPerColum, startingColumn - 1, fileButtons, cancelCallback }))
                {
                    border = EDialog.Border.None
                };
            EDialog.Button nextButton = null;
            if (fileButtons.Count > (absoluteOffset + itemsPerColum * columns))
                nextButton = new EDialog.Button(">", new DrawableCallback(UICallbacks.ShowFileSelector, new object[] { columns, itemsPerColum, startingColumn + 1, fileButtons, cancelCallback }))
                {
                    border = EDialog.Border.None
                };


            List<EDialog.DialogItem> dialogColumns = new List<EDialog.DialogItem>();
            dialogColumns.Add(new EDialog.Column(buttonWidth, new List<EDialog.DialogItem>() { backButton }));

            for (int i = 0; i < columns; i++)
            {
                dialogColumns.Add(
                    new EDialog.Column(fileColumnWidth,
                        fileButtons.Skip(absoluteOffset + itemsPerColum * i).
                        Take(itemsPerColum).
                        Select(x => x as EDialog.DialogItem).
                        ToList())
                    );
            }
            dialogColumns.Add(new EDialog.Column(buttonWidth, new List<EDialog.DialogItem>() { nextButton }));

            EDialog.Button closeButton = new EDialog.Button("", cancelCallback)
            {
                border = EDialog.Border.None,
                textAlignment = EDialog.Alignment.Right,
                textColor = Color.Yellow,
                fontSize = EDialog.FontSize.Big,
                margin = new Point(0, 0),
                fillVertically = true
            };

            EDialog.Row wrapper = new EDialog.Row(1f, new List<EDialog.DialogItem>()
            {
                new EDialog.Column(.2f, new List<EDialog.DialogItem>() { closeButton }) { margin = Point.Zero },
                new EDialog.Column(.6f, new List<EDialog.DialogItem>() {
                    new EDialog.Row(.2f, new List<EDialog.DialogItem>() { closeButton }) { margin = Point.Zero },
                    new EDialog.Row(.6f, EDialog.Border.All, dialogColumns),
                    new EDialog.Row(.2f, new List<EDialog.DialogItem>() { closeButton }) { margin = Point.Zero },
                }) { margin = Point.Zero },
                new EDialog.Column(.2f, new List<EDialog.DialogItem>() { closeButton }) { margin = Point.Zero }
            }) { margin = Point.Zero };

            ShowDialog(wrapper);
        }

        internal void LoadConfig(string id, Type attributeToLoad)
        {
            HideAllMeasurementGraphs(); //needed to do here, as Settings.MeasGraphs is first overridden by Settings.Load, and afterwards used to hide all current measGraphs
            Settings.Load(id, attributeToLoad);
        }

        internal void EnableETS(bool enable)
        {
            Settings.Current.EnableETS = enable;
            DataProcessors.DataProcessorETS.Disable = !enable;
            etsButton.Selected = enable;
        }
		internal void ToggleHelp ()
		{
            HelpImage.Visible = !HelpImage.Visible;
            helpButton.Selected = HelpImage.Visible;
		}
        internal void ShowSimpleToast(string message, int timeout, EDrawable anchor = null)
        {
            QueueCallback(delegate
            {
                ShowToast("SIMPLE_TOAST", anchor, null, Color.White, message, Location.Center, Location.Center, Location.Center, timeout);
            });            
        }

        internal void ShowAbout()
        {
            SmartScope ss = scope as SmartScope;
            DummyScope ds = scope as DummyScope;
            
            string message = "";
            if (scope.Ready)
            {
                message += "Scope type: " + scope.GetType().Name + "\n";
                message += "Serial: " + scope.Serial + "\n";
                if (ss != null)
                {
                    message += "USB Controller: " + string.Join(".", ss.GetPicFirmwareVersion()) + "\n";
                    message += "Scope Controller: " + ss.GetFpgaFirmwareVersion().ToString("X") + "\n";
                    if (ss.WifiBridge != null)
                        message += String.Format("Wifi Bridge: {0:s}\n", ss.WifiBridge.Info);
                }
                message += "Mono version: ";
                Type type = Type.GetType("Mono.Runtime");
                if (type != null)
                {
                    System.Reflection.MethodInfo displayName = type.GetMethod("GetDisplayName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (displayName != null)
                    {
                        string name = (string)displayName.Invoke(null, null);
                        Version v = new Version(name.Split(' ')[0]);
                        message += v + "\n";
                    }
                    else
                        message += "Unknown\n";
                }
                else
                    message += "Unknown\n";
            }
            else
            {
                message += "No scope connected\n";
            }
            message += "Software version: " + currentVersion + "\n";

            ShowDialog(message, new List<ButtonInfo>() {
                new ButtonInfo("OK", UICallbacks.HideDialog),
				#if WINDOWS || MONOMAC || LINUX || ANDROID
                new ButtonInfo("Check for updates", UICallbacks.CheckForUpdates)
				#endif
            });

        }

        public double CursorLocationToTime(float location)
        {
            if (gridAnchor == GridAnchor.AcquisitionBuffer)
                return ViewportCenter + (location) * ViewportTimespan;
            else if (gridAnchor == GridAnchor.Viewport)
                return (location) * ViewportTimespan;
            else
                throw new Exception("Don't know what to do");
        }
        /* FIXME: this is broken */
        public double TimeRangeToFrequency(double timeRange)
        {
            return 0;
            //double maxTimeRange = scope.GetSamplePeriod() * scope.GetNumberOfSamples();
            //return (timeRange / maxTimeRange) / scope.GetSamplePeriod();;
        }
        /* FIXME: this is broken */
        public double CursorLocationToFrequency(float location)
        {
            return 0;
            /*
            double FrequencyRightEdge = TimeRangeToFrequency(ViewportTimespan);
            double FrequencyRightEdgeLog = Math.Log10(FrequencyRightEdge);
            if (frequencyModeTimeScale == LinLog.Logarithmic)
            {
                return Math.Pow(10.0, (location + 0.5) * FrequencyRightEdgeLog);
            }
            else
                return (location + 0.5) * FrequencyRightEdge;
             */
        }

        #endregion
    }
}
