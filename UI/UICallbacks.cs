using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using ESuite.DataProcessors;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Memories;
using ESuite.Drawables;
using LabNation.Interfaces;
using ESuite.DataStorage;
#if ANDROID
using Android.Content;
#endif


#if !__IOS__

#endif

#if LABDEVICES
using LabDevices;
using LabDevices.DG4000;
#endif

using DropNet.Exceptions;
using ESuite.Measurements;
using LabNation.DeviceInterface.DataSources;
using DropNet.Models;
using System.IO;
using System.Diagnostics;
using LabNation.Common;
using LabNation.DeviceInterface.Hardware;


namespace ESuite
{
    /// <summary>
    /// This class is full of EDrawableCallbacks
    /// The sole function of these methods is to parse
    /// the arguments and call into uiHandler methods
    /// </summary>
    static internal partial class UICallbacks
    {
        internal static UIHandler uiHandler;        

        //FIXME: this shouldn't be exposed by uiHandler
        static IScope scope { get { return uiHandler.scope; } }

        #region Dummy scope
        public static void SetDummyScopeWaveform(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            AnalogWaveForm waveform = (AnalogWaveForm)args[1];
            (scope as DummyScope).SetDummyWaveForm(channel, waveform);
        }
        public static void SetDummyScopeWaveSource(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
			DummyInterface waveSource = (DummyInterface)arg;
			(scope as DummyScope).hardwareInterface = waveSource;
            uiHandler.AutoEverything(AnalogChannel.List.First(), true);

			if (waveSource.Serial == DummyInterface.Audio) {
				uiHandler.ChangeTimebaseLimits (UIHandler.MIN_TIME_PER_DIVISION_AUDIOJACK, UIHandler.MAX_TIME_PER_DIVISION);
				uiHandler.SetTDivAbsolute (0.001); //reset Tdiv for audioscope to default of 1ms
				uiHandler.SetChannelDivVertical(0.1f, AnalogChannel.ChA);
			}
			else
				uiHandler.ChangeTimebaseLimits (UIHandler.MIN_TIME_PER_DIVISION_SMARTSCOPE, UIHandler.MAX_TIME_PER_DIVISION);
        }
        public static void SetDummyScopeFileSource(EDrawable sender, object arg)
        {
            SelectFileFromDirectory(LabNation.Common.Utils.StoragePath, "*.mat", SourceFileSelected, new DrawableCallback(HideDialog));            
        }
        public static void SourceFileSelected(EDrawable sender, object arg)
        {
            uiHandler.HideDialog();
            uiHandler.CloseMenusOnGraphArea();

            if (arg is Exception)
            {
                ShowDialog(null, "Failed to load .mat file\n" + ((Exception)arg).Message);
                return;
            }

            try
            {
                string fileName = (string)arg;
                DummyInterfaceFromFile waveSource = new DummyInterfaceFromFile(fileName);

                (scope as DummyScope).hardwareInterface = waveSource;

                uiHandler.ChangeTimebaseLimits(UIHandler.MIN_TIME_PER_DIVISION_SMARTSCOPE, UIHandler.MAX_TIME_PER_DIVISION);
                uiHandler.SetAcquisitionLength(waveSource.AcquisitionLenght, false);
                uiHandler.SetViewportCenterAndTimespan(0, waveSource.AcquisitionLenght);

                uiHandler.SetAcquisitionRunning(true);
            }
            catch (Exception e)
            {
                ShowDialog(null, "Failed to read .mat file\n" + e.Message);
            }            
        }
        public static void SetDummyAmplitude(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1];
            (scope as DummyScope).SetDummyWaveAmplitude(channel, value);
        }
        public static void SetDummyFrequency(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1];
            (scope as DummyScope).SetDummyWaveFrequency(channel, value);
        }
        public static void SetDummyPhase(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1] * Math.PI / 180.0;
            (scope as DummyScope).SetDummyWavePhase(channel, value);
        }
        public static void SetDummyDutyCycle(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1] / 100.0;
            (scope as DummyScope).SetDummyWaveDutyCycle(channel, value);
        }
        public static void SetDummyNoise(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1];
            (scope as DummyScope).SetNoiseAmplitude(channel, value);
        }
        public static void SetDummyDcOffset(EDrawable sender, object arg)
        {
            if (!(scope is DummyScope)) return;
            object[] args = arg as object[];
            AnalogChannel channel = (AnalogChannel)args[0];
            double value = (double)args[1];
            (scope as DummyScope).SetDummyWaveDcOffset(channel, value);
        }
        #endregion

        #region Measurements, cursors
        public static void SetMeasurementBoxVisibility(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            MeasurementBox box = (MeasurementBox)args[0];
            bool visible = (bool)args[1];

            uiHandler.ShowMeasurementBox(visible);
        }

        public static void SetMeasurementBoxMode(EDrawable sender, MeasurementBoxMode mode)
        {
            uiHandler.SetMeasurementBoxMode(mode);
        }

        public static bool AddCursor(EDrawable sender, Grid grid, Vector2 relativeLocation, GestureSample gesture)
        {
            if (!uiHandler.InteractionAllowed) return false;

            uiHandler.CloseMenusOnGraphArea();
            return uiHandler.AddCursor(sender, grid, relativeLocation, gesture);
        }
        public static void RemoveCursor(Cursor cursor)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.RemoveCursor(cursor);
        }

        public static void CursorTapped(Cursor cursor)
        {
            if (cursor is CursorVertical)
                uiHandler.VerticalCursorTapped((CursorVertical)cursor);
            else if (cursor is CursorHorizontal)
                uiHandler.HorizontalCursorTapped((CursorHorizontal)cursor);
        }
        #endregion

        #region Math channels
        public static void AddMathChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.AddMathChannel();
        }
        public static void AddFFTChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.AddFFTChannel(arg as AnalogChannel);
        }
        public static void AddRefChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.AddRefChannel(arg as AnalogChannel);
        }
        public static void DestroyChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.DestroyChannel(arg as ChannelDestructable);

            if (arg is ProcessorChannel)
                uiHandler.SaveProcessorSettings();
        }
        public static void SetMathOperation(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.UpdateMathChannelOperation((MathChannel)args[0], (DataProcessorMath.Operation)args[1]);
        }
        #endregion

        #region Protocol decoders-
        public static void SetDecoderSourceChannel(EDrawable sender, object arg)
        {
            object[] argArr = (object[])arg;
            DataProcessorDecoder decoderProcessor = (DataProcessorDecoder)argArr[0];
            string sourceName = (string)argArr[1];
            Channel ch = (Channel)argArr[2];

            //make change
            uiHandler.UpdateDecoderSourceChannel(decoderProcessor, sourceName, ch);

            //finishing touch: redraw context menu
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.ShowMenuChannel(decoderProcessor.dataProcessorChannel);

            //and save processor config to file
            uiHandler.SaveProcessorSettings();
        }

        public static void SetDecoderParameter(EDrawable sender, object arg)
        {
            object[] argArr = (object[])arg;
            DataProcessorDecoder decoderProcessor = (DataProcessorDecoder)argArr[0];
            string parameterName = (string)argArr[1];
            object parameterValue = (object)argArr[2];

            uiHandler.UpdateDecoderParameter(decoderProcessor, parameterName, parameterValue);
            
            //finishing touch: redraw context menu
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.ShowMenuChannel(decoderProcessor.dataProcessorChannel);

            //and save processor config to file
            uiHandler.SaveProcessorSettings();
        }

        public static void ResetWaveformName(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            Dictionary<Channel, Waveform> waveforms = (Dictionary<Channel, Waveform>)args[1];

            uiHandler.RemoveCustomWaveName(ch);
            uiHandler.UpdateOffsetIndicatorNames();
            uiHandler.HideContextMenu();
        }

        public static void SetWaveformName(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            Dictionary<Channel, Waveform> waveforms = (Dictionary<Channel, Waveform>)args[1];

            uiHandler.ShowKeyboardAlfaNumeric(waveforms[ch].OffsetIndicator.CenterText, new Point(sender.Boundaries.X, sender.Boundaries.Y), 5, new DrawableCallback(SetWaveformNameAfterKeyboard, new object[] { ch, waveforms }));
        }

        public static void SetWaveformNameAfterKeyboard(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            Dictionary<Channel, Waveform> waveforms = (Dictionary<Channel, Waveform>)args[1];
            string newName = (string)args[2];

            uiHandler.SetCustomWaveName(ch, newName);
            uiHandler.RemoveFloater(sender);
            uiHandler.UpdateOffsetIndicatorNames();
            uiHandler.HideContextMenu();
        }

        public static void SetDecoderRadix(EDrawable sender, object arg)
        {
            object[] argArr = (object[])arg;
            DataProcessorDecoder decoderProcessor = (DataProcessorDecoder)argArr[0];
            RadixType radixType = (RadixType)argArr[1];
            ProtocolDecoderChannel ch = (ProtocolDecoderChannel)decoderProcessor.dataProcessorChannel;
            
            //make change
            ch.RadixType = radixType;

            //finishing touch
            uiHandler.CloseMenusOnGraphArea();

            //and save processor config to file
            uiHandler.SaveProcessorSettings();
        }

        public static void SetDigitalWaveIntervalDisplay(EDrawable sender, object arg)
        {
            object[] argArr = (object[])arg;
            DigitalChannel digiChannel = (DigitalChannel)argArr[0];
            IntervalsVisibility visibility = (IntervalsVisibility)argArr[1];

            uiHandler.SetDigitalWaveIntervalDisplay(digiChannel, visibility);
        }

        public static void AddProcessor(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;

            object[] argArr = (object[])arg;
            IProcessor proc = (IProcessor)argArr[0];
            GraphType graphType = (GraphType)argArr[1];
            IProcessor newInstance = (IProcessor)Activator.CreateInstance(proc.GetType());

            uiHandler.AddProcessor(newInstance, graphType, true);
        }

        public static void ChangeFFTWindowFunction(EDrawable sender, object arg)
        {
            uiHandler.ChangeFFTWindowFunction((FFTWindow)arg);
        }

        #endregion

        public static void CloseContextMenu(EDrawable sender, object arg)
        {
            uiHandler.HideContextMenu();
        }
        
        #region Indicator click/drag
        public static void OffsetIndicatorClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Channel ch;
            if (arg is object[])
            {
                object[] args = (object[])arg;
                ch = (Channel)args[0];
            }
            else
                ch = (Channel)arg;
            uiHandler.SelectChannel(ch);
            uiHandler.ShowMenuChannel(ch, sender);
        }
        public static void OffsetIndicatorDoubleClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Channel ch;
            if (arg is object[])
            {
                object[] args = (object[])arg;
                ch = (Channel)args[0];
            }
            else
                ch = (Channel)arg;
            uiHandler.SelectChannel(ch);
            if (!(ch is DigitalChannel))
            {
                //close context menu if displayed
                IndicatorInteractive indicator = (IndicatorInteractive)sender;
                if (indicator.contextMenuShown)
                    uiHandler.ShowMenuChannel(ch);

                //jump to 0 offset
                uiHandler.SetYOffset(ch, 0);
            }
        }
        public static void OffsetIndicatorMoved(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;

            uiHandler.CloseMenusOnGraphArea();
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            float relativePosition = (float)args[1];
            uiHandler.OffsetIndicatorMoved(ch, relativePosition);
        }
        public static void OffsetIndicatorDropped(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;

            uiHandler.CloseMenusOnGraphArea();
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            float relativePosition = (float)args[1];
            uiHandler.OffsetIndicatorDropped(ch, relativePosition);
        }
        #endregion

        #region Channel selection, visibility
        public static void ShowChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();
            Channel ch = (Channel)args[0];
            bool visible = (bool)args[1];
            uiHandler.ShowChannel(ch, visible);
        }
        public static void EnableChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();
            Channel ch = (Channel)args[0];
            bool enable = (bool)args[1];
            uiHandler.EnableChannel(ch, enable);
            uiHandler.AutoSpaceDigiWaves(null);
        }

        public static void SelectNextChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.SelectNextChannel(Waveform.EnabledWaveformsVisible.Keys.ToList(), 1);
        }
        public static void SelectPreviousChannel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.SelectNextChannel(Waveform.EnabledWaveformsVisible.Keys.ToList(), -1);
        }
        #endregion

        #region Acquisition mode
        public static void SetAcquisitionMode(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.SetAcquisitionMode((AcquisitionMode)arg, true);
        }
        public static void SelectRollingMode(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.EnableRollingByUser();
        }
        #endregion

        #region File storage
        public static void StoreFileLocal(EDrawable sender, object arg)
        {
            StorageFile localFile = (StorageFile)arg;
            localFile = uiHandler.MoveRecordedFileToStoragePath(localFile);
            uiHandler.ShowDialog("File stored at " + localFile.info.FullName);
        }

        public static void StoreFileDropbox(EDrawable sender, object arg)
        {
            if(!DropboxAuthenticated(sender, new DrawableCallback(StoreFileDropbox, arg)))
                return;

            StorageFile localFile = (StorageFile)arg;

            EDialogProgress progressDialog = uiHandler.ShowProgressDialog(
                "Gimme a minute while I upload your recording (" +
                LabNation.Common.Utils.siPrint(localFile.info.Length, 0.01, 3, "B") +
                ") to dropbox...", 0f);
			uiHandler.dropboxStorage.Store(localFile, StoreFileDropboxCallback, (f) => progressDialog.Progress = f);
        }
        public static void StoreFileDropboxCallback(EDrawable sender, object arg)
        {
            object[] args = arg as object[];
            StorageFile localFile = (StorageFile)args[0];
            DropNet.Models.MetaData metadata = args[1] as DropNet.Models.MetaData;
            Exception exception = args[1] as Exception;

            if (exception != null)
            {
                localFile = uiHandler.MoveRecordedFileToStoragePath(localFile);
                string message;
                List<ButtonInfo> buttons = new List<ButtonInfo>() {
                    new ButtonInfo("OK", ShowDialog, "Recording stored at " + localFile.info.FullName, ButtonType.Confirm.Keys())
                };
                if (exception is DropboxRestException)
                {
                    DropboxRestException dbe = exception as DropboxRestException;
                    if (
                        dbe.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        dbe.StatusCode == System.Net.HttpStatusCode.Forbidden
                        )
                    {
                        message = "I don't seem to have permission to store in your dropbox";
                        buttons.Insert(0,
                            new ButtonInfo(
                                "Authenticate",
                                DropboxAuthenticateStep1,
                                new DrawableCallback(StoreFileDropbox, localFile))
                        );
                    }
                    else
                    {
                        message = "Something went wrong storing the file to dropbox";
                        buttons.Insert(0, new ButtonInfo("Retry", StoreFileDropbox, localFile));
                    }
                }
                else
                {
                    message = exception.Message;
                    buttons.Insert(0, new ButtonInfo("Retry", StoreFileDropbox, localFile));
                }

                uiHandler.ShowDialog(message, buttons);
                return;
            }

            uiHandler.ShowDialog("Hooray, file uploaded to dropbox. Find it at:\n" + metadata.Path,
                    new List<ButtonInfo>() { new ButtonInfo("Yey", HideDialog, null, ButtonType.ConfirmOrCancel.Keys()) });
        }
        #endregion

        #region Dropbox

        private static bool DropboxAuthenticated(EDrawable sender, DrawableCallback callback)
        {
            //Check if we're already authenticated, if not initiate authentication
            //The authentication will call the callback when completed
            if (uiHandler.dropboxStorage.authenticated)
                return true;

            uiHandler.QueueCallback(
                DropboxAuthenticateStep4,
                new object[] { 
                    callback,
                    Settings.CurrentRuntime.dropboxLogin
                }
            );
            return false;
        }

        public static void DropboxAuthenticateStep1(EDrawable sender, object arg)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>();

            if (arg is DrawableCallback)
            {
                DrawableCallback cb = (DrawableCallback)arg;
                if (cb.DelegateMethodName == "StoreFileDropbox" && cb.argument is StorageFile)
                {
                    StorageFile f = uiHandler.MoveRecordedFileToStoragePath((StorageFile)cb.argument);
                    buttons.Add(new ButtonInfo("Store locally", ShowDialog, "Recording stored at " + f.info.FullName, ButtonType.Cancel.Keys()));

                    cb.argument = f;
                    arg = cb;
                }
                else
                    buttons.Add(new ButtonInfo("Nevermind", HideDialog, ButtonType.Cancel.Keys()));
            }
            else
                buttons.Add(new ButtonInfo("Nevermind", HideDialog, ButtonType.Cancel.Keys()));
            buttons.Insert(0, new ButtonInfo("OK, off we go!", DropboxAuthenticateStep2, arg, ButtonType.Confirm.Keys()));

            uiHandler.ShowDialog(
                "I'm gonna send you off to the dropbox site so you can allow this app to store and read data there.\n" +
                "After approving this app, come back here and let me know",
                buttons);
        }
#if ANDROID
        public static void AskForPlayStoreReview(EDrawable sender, object arg)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>();

            buttons.Insert(0, new ButtonInfo("Sure, my pleasure!", AskForPlayStoreReview2, ButtonType.Confirm.Keys()));
            buttons.Add(new ButtonInfo("No, I hate this app", HideDialog, ButtonType.Cancel.Keys()));

            uiHandler.ShowDialog(
                "We're going to bother you with this only once.\n" +
                "\n" +
                "But since you seem to like this app, you could support our work by helping our app get a little bit closer to a 5 star rating. \n" +
                "Would you like to help us getting there?",
                buttons);
        }
        public static void AskForPlayStoreReview2(EDrawable sender, object arg)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>();

            buttons.Insert(0, new ButtonInfo("OK, got it!", AskForPlayStoreReview3, ButtonType.Confirm.Keys()));

            uiHandler.ShowDialog(
                "Awesome!\n" +
                "On the next page, navigate to where you see our current rating. Just above this, give your rating and hit the Submit button!",
                buttons);
        }
        public static void AskForPlayStoreReview3(EDrawable sender, object arg)
        {
            uiHandler.HideDialog();
            uiHandler.RateApp();
        }
#endif
        public static void DropboxAuthenticateStep2(EDrawable sender, object arg)
        {
            uiHandler.ShowDialog("Browser opening any second now");
            uiHandler.dropboxStorage.OpenAuthenticationUrl(DropboxAuthenticateStep3, arg);
        }
        public static void DropboxAuthenticateStep3(EDrawable sender, object arg)
        {
            object[] args = arg as object[];
            object genericArgument = args[0];
            DropNet.Models.MetaData metadata = args[1] as DropNet.Models.MetaData;
            DropNet.Exceptions.DropboxException exception = args[1] as DropNet.Exceptions.DropboxException;

            if (exception != null)
            {
                ShowDialog(sender, new object[] {
                        "Something went wrong:\n" + exception.Message,
                        new List<ButtonInfo>() { new ButtonInfo("Sad, but OK", HideDialog, ButtonType.ConfirmOrCancel.Keys()) }
                    });
                return;
            }

            List<ButtonInfo> buttons = new List<ButtonInfo>();

            if (genericArgument is DrawableCallback)
            {
                DrawableCallback cb = (DrawableCallback)genericArgument;
                if (cb.DelegateMethodName == "StoreFileDropbox" && cb.argument is StorageFile)
                {
                    StorageFile f = uiHandler.MoveRecordedFileToStoragePath((StorageFile)cb.argument);
                    buttons.Add(new ButtonInfo("No", ShowDialog, "Recording stored at " + f.info.FullName, ButtonType.Cancel.Keys()));

                    cb.argument = f;
                    genericArgument = cb;
                }
                else
                    buttons.Add(new ButtonInfo("No", HideDialog, null, ButtonType.Cancel.Keys()));
            }
            else
                buttons.Add(new ButtonInfo("No", HideDialog, null, ButtonType.Cancel.Keys()));
            buttons.Insert(0, new ButtonInfo("Yep", DropboxAuthenticateStep4, genericArgument, ButtonType.Confirm.Keys()));

            uiHandler.ShowDialog("So you've approved this app on the dropbox site?", buttons);
        }
        public static void DropboxAuthenticateStep4(EDrawable sender, object arg)
        {
            object[] args = arg as object[];
            object genericArg;
            DropNet.Models.UserLogin login = null;

            if (args != null)
            {
                genericArg = args[0];
                login = args[1] as DropNet.Models.UserLogin;
            }
            else
            {
                genericArg = arg;
            }


			EDialogProgress progressDialog = uiHandler.ShowProgressDialog ("Authenticating with dropbox...", 0f);
			uiHandler.dropboxStorage.Authenticate (
				() => uiHandler.QueueCallback(DropboxAuthenticateStep1, genericArg),
				new DrawableCallback(DropboxAuthenticateStep5, genericArg), 
				(f) => progressDialog.Progress = f, login);
        }
        public static void DropboxAuthenticateStep5(EDrawable sender, object arg)
        {
            object[] args = arg as object[];
            object genericArg = args[0];
            DropNet.Models.MetaData metadata = args[1] as DropNet.Models.MetaData;
            Exception exception = args[1] as Exception;

            if (metadata == null)
            {
                List<ButtonInfo> buttons = new List<ButtonInfo>();

                if (genericArg is DrawableCallback)
                {
                    DrawableCallback cb = (DrawableCallback)genericArg;
                    if (cb.DelegateMethodName == "StoreFileDropbox" && cb.argument is StorageFile)
                    {
                        StorageFile f = uiHandler.MoveRecordedFileToStoragePath((StorageFile)cb.argument);
                        buttons.Add(new ButtonInfo("OK", ShowDialog, "Recording stored at " + f.info.FullName));

                        cb.argument = f;
                        genericArg = cb;
                    }
                    else
                        buttons.Add(new ButtonInfo("OK", HideDialog));
                }
                else
                    buttons.Add(new ButtonInfo("OK", HideDialog));

                if (exception is DropNet.Exceptions.DropboxRestException)
                {
                    DropboxRestException dbre = exception as DropNet.Exceptions.DropboxRestException;

                    if (dbre.Response.ErrorException != null)
                        buttons.Insert(0, new ButtonInfo("Retry",
                            DropboxAuthenticateStep4,
                            new object[] { genericArg, Settings.CurrentRuntime.dropboxLogin }));
                    else if (dbre.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        buttons.Insert(0, new ButtonInfo("Reauthenticate", DropboxAuthenticateStep1, genericArg));
                }
                else
                {
                    buttons.Insert(0, new ButtonInfo("Retry",
                        DropboxAuthenticateStep4,
                        new object[] { genericArg, Settings.CurrentRuntime.dropboxLogin }));
                }

                ShowDialog(sender, new object[] {
                        "Something went wrong authenticating with dropbox:\n" + exception.Message,
                        buttons
                    });
                return;
            }
            uiHandler.HideDialog();
            if (genericArg is DrawableCallback)
            {
                DrawableCallback cb = (DrawableCallback)genericArg;
                cb.Call(sender);
            }
        }
        public static void SelectFileFromDropboxFilelist(List<MetaData> files, DrawableCallbackDelegate del, DrawableCallback cancelCallback)
        {
            try
            {
                List<EDialog.Button> fileButtons = files.Select(x => new EDialog.Button()
                {
                    text = x.Name,
                    border = EDialog.Border.None,
                    callback = new DrawableCallback(del, x.Path),
                    textAlignment = EDialog.Alignment.Left
                }).ToList();
                uiHandler.ShowFileSelector(3, 15, 0, fileButtons, cancelCallback);
            }
            catch (Exception e)
            {
                uiHandler.QueueCallback(del, e);
            }
        }

#endregion

#region local file select

        public static void SelectFileFromDirectory(string path, string pattern, DrawableCallbackDelegate del, DrawableCallback cancelCallback)
        {
            try
            {
                if (!Directory.Exists(path))
                    throw new Exception("Folder '" + path + "' does not exist");
             
                List<FileInfo> files = LabNation.Common.Utils.GetFiles(path, pattern);
                if (files.Count == 0)
                    throw new Exception("No files found in path '" + path + "'");

                List<EDialog.Button> fileButtons = files.Select(x => new EDialog.Button()
                {
                    text = x.Name,
                    border = EDialog.Border.None,
                    callback = new DrawableCallback(del, x.FullName),
                    textAlignment = EDialog.Alignment.Left
                }).ToList();
                uiHandler.ShowFileSelector(3, 15, 0, fileButtons, cancelCallback);
            }
            catch (Exception e)
            {
                uiHandler.QueueCallback(del, e);
            }
        }

#endregion

#region Grid generals
        public static void SetChannelDivVertically(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            float divVertical = (float)args[1];

            uiHandler.SetChannelDivVertical(divVertical, ch);
            uiHandler.PanZoomEnd(ch);
        }

        public static void ZoomChannelVertically(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Channel ch = (Channel)args[0];
            bool zoomIn = (bool)args[1];

            if (zoomIn)
                uiHandler.PanZoomGridVertical(new Vector2(1f, 0.5f), new Vector2(), true, ch);
            else
                uiHandler.PanZoomGridVertical(new Vector2(1f, 2f), new Vector2(), true, ch);

            uiHandler.PanZoomEnd(ch);
        }

        public static void SetTDivAbsolute(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            float tdiv = (float)args[1];
            uiHandler.SetTDivAbsolute((double)tdiv);
        }

        public static void PanAndZoomGrid (EDrawable sender, object arg)
		{
			if (!uiHandler.InteractionAllowed)
				return;
			object[] args = (object[])arg;
			Vector2 ratioChange = (Vector2)args [0];
			Vector2 pinchCenter = (Vector2)args [1];
			Vector2 offsetDelta = (Vector2)args [2];
			bool verifyWaveIsUnderCursor = (bool)args [3];
			Rectangle? gestureRectangle = null;
            bool wasMouseScroll = false;
			if (args.Length > 4) {
				if(args[4] is Point) {
					Point gestureLocation = (Point)args[4];
					gestureRectangle = new Rectangle(gestureLocation.X, gestureLocation.Y, 1, 1);
				}
				if(args[4] is Rectangle)
					gestureRectangle = (Rectangle)args [4];                
			}
            if (args.Length > 5)
            {
                if (args[5] is bool)
                    wasMouseScroll = (bool)args[5];
            }

            uiHandler.CloseMenusOnGraphArea();

            if (sender is GridFrequency)
                uiHandler.PanZoomFreqGrid(ratioChange, pinchCenter, offsetDelta, verifyWaveIsUnderCursor, wasMouseScroll, gestureRectangle);
            else
                uiHandler.PanZoomGrid(ratioChange, pinchCenter, offsetDelta, verifyWaveIsUnderCursor, wasMouseScroll, gestureRectangle);
        }

        public static void PinchGridBegin(EDrawable sender, object arg)
        {
            uiHandler.PinchBegin();
        }
        public static void PanAndZoomGridEnd(EDrawable sender, object arg)
        {
        	uiHandler.PanZoomEnd();
        }

        public static void WaveDragEnd(EDrawable sender, object arg)
        {
            uiHandler.WaveDragEnd();
        }

        public static void SetFrequencyModeTimeScale(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.SetTimeScale((LinLog)arg);
        }
#endregion

#region Scope running 
        public static void ToggleRunning(EDrawable sender, object arg)
        {
            EButtonImageAndTextSelectable b = sender as EButtonImageAndTextSelectable;
            if (b != null)
                uiHandler.SetAcquisitionRunning(!b.Selected);
            else
                uiHandler.ToggleAcquisitionRunning();
        }
        public static void DoubleTapGrid(EDrawable sender, object arg)
        {
            Vector2 location = (Vector2)arg;
            uiHandler.DoubleTapGrid(location);
        }
        public static void SetRunning(EDrawable sender, object arg)
        {
            bool running = (bool)arg;
            uiHandler.SetAcquisitionRunning(running);
        }
#if WINDOWS
        public static void InstallDriver(EDrawable sender, object arg)
        {
            uiHandler.InstallDriver();
        }
#endif
#endregion

#region Recording
        public static void ToggleRecord(EDrawable sender, object arg)
        {
            uiHandler.ToggleRecording();            
        }
        public static void SetStorageFileFormat(EDrawable sender, object arg)
        {
            Settings.Current.StorageFormat = (StorageFileFormat)arg;
        }
        #endregion

        #region Configurations
        public static void ShowSaveConfigForm(EDrawable sender, object arg)
        {
            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = "", MaxNbrChars = 10 });

            uiHandler.ActivateLeftForm("Save configuration", "Save", formEntries, new DrawableCallback(SaveConfigFromForm));
        }

        public static void ResetWifiBridge(EDrawable sender, object arg)
        {
            uiHandler.ResetWifiBridge();
        }

        public static void ShowConnectToAPForm(EDrawable sender, object arg)
        {
            AccessPointInfo apInfo = (AccessPointInfo)arg;

            //guess encryptionType based on AP info retreived from OpenWRT
            string encryptionType;
            if (apInfo.CCMP)
                encryptionType = "WPA2-PSK";
            else if (apInfo.Authentication.ToLower().IndexOf("psk") > -1)
                encryptionType = "WPA-PSK";
            else if (apInfo.TKIP)
                encryptionType = "WPA-PSK";
            else if (apInfo.Authentication.ToLower().IndexOf("wep") > -1)
                encryptionType = "WEP";
            else
            {
                if (apInfo.capabilities.HasFlag(ApCapabilities.PRIVACY))
                    encryptionType = "WEP";
                else
                    encryptionType = "Open";
            }
                

            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();            
            formEntries.Add(new FormEntryDefinitionPassword() { Name = "Pass", Value = "...", MaxNbrChars = 40 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Auth", Value = encryptionType, MaxNbrChars = 40 });

            uiHandler.ShowSideMenu(true, true);
            uiHandler.ActivateLeftForm(apInfo.SSID, "Connect", formEntries, new DrawableCallback(ShowSwitchWifiDialog, arg));
        }

        public static void ShowSwitchWifiDialog(EDrawable sender, object arg)
        {
            List<ButtonInfo> buttons = new List<ButtonInfo>();
            buttons.Add(new ButtonInfo("Cancel", HideDialog, null));
            buttons.Add(new ButtonInfo("Jump!", ConnectToAP, arg));

            string message = "When you proceed, the wifi bridge will disconnect from its current network. Next, it will try to connect to the wifi AP you just selected, using the credentials you just entered. This can take up to 30 seconds.\nIn the meantime, YOU also have to connect to the same target wifi AP, as both you and the bridge need to be on that same new network. As soon as that's been done, this app will reconnect to the bridge.\n If this all sounds too scary, just hit the cancel button and you'll be safe.";

            uiHandler.ShowDialog(message, buttons);
        }

        public static void ConnectToAP(EDrawable sender, object arg)
        {
            HideDialog(null, null);

            object[] args = (object[])arg;
            AccessPointInfo apInfo = (AccessPointInfo)args[0];
            object[] formArgs = (object[])args[1];
            Dictionary<string, string> mapper = (Dictionary<string, string>)formArgs[0];
            string pass = mapper["Pass"];
            string encryptionAnswer = mapper["Auth"];

            //convert user input to values supported by OpenWRT. Compatible with our own guessed type; and allows user to define himself.
            string enc;
            if (encryptionAnswer.ToLower().IndexOf("psk") > -1)
            {
                if (encryptionAnswer.IndexOf("2") > -1)
                    enc = "psk2";
                else
                    enc = "psk";
            }
            else if (encryptionAnswer.ToLower().IndexOf("wep") > -1)
            {
                if (encryptionAnswer.ToLower().IndexOf("shared") > -1)
                    enc = "wep+shared";
                else if (encryptionAnswer.ToLower().IndexOf("mixed") > -1)
                    enc = "wep+mixed";
                else
                    enc = "wep";
            }
            else
            {
                enc = "open";
            }

            uiHandler.SwitchWifiBridgeToAP(apInfo, enc, pass);
        }

        public static void SaveConfigFromForm(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];

            string configName = (string)mapper["Name"];

            if (Settings.SaveExists(configName))
            {
                uiHandler.ShowSimpleToast("FAILED saving current config as " + configName + " -- file exists already", 5000);
                return;
            }

            if (Settings.SaveCurrent(configName, uiHandler.scope))
                uiHandler.ShowSimpleToast("Saved current config as " + configName, 5000);
            else
                uiHandler.ShowSimpleToast("FAILED saving current config as " + configName, 5000);

            uiHandler.RebuildSideMenu();
        }

        public static void RemoveKeyboard(EDrawable sender, object arg)
        {
            uiHandler.RemoveFloater(sender);
        }

        public static void LoadConfig(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            string id = (string)args[0];
            Type attributeToLoad = (Type)args[1];
            uiHandler.LoadConfig(id, attributeToLoad);
        }
#endregion

        #region Channel settings
        public static void SetChannelCoupling(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();

            AnalogChannel ch = (AnalogChannel)args[0];
            uiHandler.SetChannelCoupling(ch, (Coupling)args[1]);
            uiHandler.ShowMenuChannel(ch);
        }
        public static void SetChannelInvert(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            AnalogChannel ch = (AnalogChannel)args[0];
            bool invert = (bool)args[1];

            uiHandler.SetChannelInvert(ch, invert);
        }
        public static void SetProbeDivision(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();
            AnalogChannel ch = (AnalogChannel)args[0];
            uiHandler.SetProbeDivision(ch, (Probe)args[1]);
            uiHandler.ShowMenuChannel(ch);
        }
        public static void DoMagicAnalog(EDrawable sender, object args)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.DoMagic();
        }
        public static void HWAutoArrange(EDrawable sender, object arg)
        {
            uiHandler.HWAutoArrangeStart();
        }
        public static void SetFFTVoltageAxis(EDrawable sender, object arg)
        {
            uiHandler.SetFFTVoltageAxis((LinLog)arg);
        }
        public static void SetFFTFrequencyAxis(EDrawable sender, object arg)
        {
            uiHandler.SetFFTFrequencyAxis((LinLog)arg);
        }
#endregion

#region General UI, menus, dialogs

        public static void SwitchGraphMode(EDrawable sender, object arg)
        {
            MenuItem i = (MenuItem)sender;
            MainModes mainMode = (MainModes)arg;
            uiHandler.SwitchMainMode(mainMode, sender);
        }

        public static void DragFloater(EDrawable sender, object arg)
        {
            GestureSample g = (GestureSample)arg;
            uiHandler.MoveFloaterBy((EFloater)sender, g.Delta.ToPoint());
        }

        public static void DropFloater(EDrawable sender, object arg)
        {
            GestureSample g = (GestureSample)arg;
            uiHandler.RemoveFloaterIfOffscreen(sender);
        }

        public static void SliderShowNumPad(EDrawable sender, object arg)
        {
            MenuItemSlider s = (MenuItemSlider)sender;
            GestureSample g = (GestureSample)arg;

            uiHandler.ShowKeyboardNumericalSi(s.Minimum, s.Maximum, s.Precision, s.Unit, s.Value, g.Position.ToPoint(), new DrawableCallback(UpdateSliderValueFromNumpad, s));
        }

        public static void UpdateSliderValueFromNumpad(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            MenuItemSlider s = (MenuItemSlider)args[0];
            double value = (double)args[1];
            s.Value = value;
            uiHandler.RemoveFloater(sender);
        }

        public static void ParameterShowNumpad(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            DataProcessorDecoder proc = (DataProcessorDecoder)args[0];
            DecoderParameterNumpad param = (DecoderParameterNumpad)args[1];
            float currentValue = (float)args[2];
            Point position = (Point)args[3];

            //because they're object, MinValue and MaxValue can't be cast to doubles straight away
            //first they would need to be converted to their base type, depending on type of parameter
            //here we're converting to string and to double, which works for all cases
            uiHandler.ShowKeyboardNumericalSi(double.Parse(param.MinValue.ToString()), double.Parse(param.MaxValue.ToString()), 0.01, param.Unit, currentValue, position, new DrawableCallback(UpdateParameterValueFromNumpad, new object[] { proc, param }));
        }

        public static void UpdateParameterValueFromNumpad(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            DataProcessorDecoder proc = (DataProcessorDecoder)args[0];
            DecoderParameterNumpad param = (DecoderParameterNumpad)args[1];
            double value = (double)args[2];

            if (param is DecoderParameterNumpadFloat)
                SetDecoderParameter(null, new object[] { proc, param.ShortName, (float)value });
            else if (param is DecoderParameterNumpadInt)
                SetDecoderParameter(null, new object[] { proc, param.ShortName, (int)value });
            else
                throw new Exception("numpad entry not supported by parameter type");
            uiHandler.RemoveFloater(sender);
        }

        public static void MenuValueShowNumPad(EDrawable sender, object arg)
        {
            MenuItemValue v = (MenuItemValue)sender;
            if (v.UseSiScaling)
                uiHandler.ShowKeyboardNumericalSi(v.Minimum, v.Maximum, v.Precision, v.Unit, v.Value, new Point(v.Boundaries.Right, v.Boundaries.Top), new DrawableCallback(UICallbacks.UpdateMenuValueFromNumpad, v));
            else
                uiHandler.ShowKeyboardNumerical(v.Minimum, v.Maximum, v.Precision, v.Unit, v.Value, new Point(v.Boundaries.Right, v.Boundaries.Top), new DrawableCallback(UICallbacks.UpdateMenuValueFromNumpad, v));
        }
        public static void SetAcquisitionDepthUserMaximum(EDrawable sender, object arg)
        {
            uiHandler.SetAcquisitionDepthUserMaximum((uint)arg);
        }

        public static void GUI_SetRecordingAcquisitionsPerInterval(EDrawable sender, object arg)
        {
            uiHandler.SetRecordingAcquisitionsPerInterval((int)(double)arg);
        }
        public static int GUI_GetRecordingAcquisitionsPerInterval
        {
            get { return uiHandler.GetRecordingAcquisitionsPerInterval(); }
        }
        public static void GUI_SetRecordingInterval(EDrawable sender, object seconds)
        {
            uiHandler.SetRecordingInterval(TimeSpan.FromSeconds((double)seconds));
        }
        public static void GUI_SetMeasurementsTimespan(EDrawable sender, object minutes)
        {
            uiHandler.SetMeasurementsTimespan(TimeSpan.FromMinutes((double)minutes));
        }
        public static int GUI_GetMeasurementsTimespan
        {
            get { return (int)(uiHandler.GetMeasurementsTimespan().TotalMinutes); }
        }        
        public static int GUI_GetRecordingInterval
        {
            get { return (int)(uiHandler.GetRecordingInterval().TotalSeconds); }
        }

        public static void UpdateMenuValueFromNumpad(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            MenuItemValue s = (MenuItemValue)args[0];
            s.Selected = false;

            double value = (double)args[1];
            s.Value = value;
            uiHandler.RemoveFloater(sender);
        }

        public static void RemoveExistingProbe(EDrawable sender, object arg)
        {
            Probe probe = (Probe)arg;
            uiHandler.RemoveProbe(probe);
        }

        public static void ShowEditExistingProbeForm(EDrawable sender, object arg)
        {
            Probe probe = (Probe)arg;

            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = probe.Name, MaxNbrChars = 5 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Factor", Prefix = "x", Value = probe.Gain, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Offset", Suffix = "V", Value = probe.Offset, MinValue = -1000, MaxValue = 1000 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Unit", Value = probe.Unit, MaxNbrChars = 3 });

            uiHandler.ActivateLeftForm("Edit existing probe", "Save changes", formEntries, new DrawableCallback(EditExistingProbe), probe);
        }

        public static void EditExistingProbe(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];
            Probe originalProbe = (Probe)args[1];

            string name = (string)mapper["Name"];
            double gain = double.Parse(mapper["Factor"]);
            double offset = double.Parse(mapper["Offset"]);
            string unit = (string)mapper["Unit"];

            uiHandler.UpdateProbe(originalProbe, name, gain, offset, unit);
        }

        ////////////////////////////
        // Relative probe
        public static void ShowAddNewProbeFormRelative(EDrawable sender, object arg)
        {
            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = "", MaxNbrChars = 5 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Unit", Value = "A", MaxNbrChars = 3, ValueChangedCallback = new Drawables.DrawableCallback(NewProbeFormRelativeUnitChanged) });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Relation", Suffix = "V/A", Value = 0.1, MinValue = -10000, MaxValue = 10000 });            

            uiHandler.ActivateLeftForm("New probe definition", "Save new probe", formEntries, new DrawableCallback(AddNewProbeRelative));
        }

        public static void NewProbeFormRelativeUnitChanged(EDrawable sender, object arg)
        {
            EForm form = (EForm)sender;
            string newValue = (string)arg;

            form.ChangeSuffix("Relation", "V/" + newValue);
        }

        public static void AddNewProbeRelative(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];

            string name = (string)mapper["Name"];
            double relation = double.Parse(mapper["Relation"]);
            string unit = (string)mapper["Unit"];

            Probe newProbe = new Probe(name, unit, (float)(1.0/relation), 0, false);
            uiHandler.AddProbe(newProbe);
        }
        ////////////////////////////

        ////////////////////////////
        // Linear probe
        public static void ShowAddNewProbeFormLinear(EDrawable sender, object arg)
        {
            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = "", MaxNbrChars = 5 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Unit", Value = "PSI", MaxNbrChars = 3, ValueChangedCallback = new Drawables.DrawableCallback(NewProbeFormLinearUnitChanged) });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Value", Suffix = "PSI", Value = 100, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Voltage@Value", Suffix = "V", Value = 15, MinValue = -10000, MaxValue = 10000 });

            uiHandler.ActivateLeftForm("New probe definition", "Save new probe", formEntries, new DrawableCallback(AddNewProbeLinear));
        }

        public static void NewProbeFormLinearUnitChanged(EDrawable sender, object arg)
        {
            EForm form = (EForm)sender;
            string newValue = (string)arg;

            form.ChangeSuffix("Spec'ed value", newValue);
        }

        public static void AddNewProbeLinear(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];

            string name = (string)mapper["Name"];
            double y = double.Parse(mapper["Spec'ed value"]);
            double x = double.Parse(mapper["Corresponding voltage"]);
            string unit = (string)mapper["Unit"];

            Probe newProbe = new Probe(name, unit, (float)(y/x), 0, false);
            uiHandler.AddProbe(newProbe);
        }
        ////////////////////////////

        ////////////////////////////
        // Offset probe
        public static void ShowAddNewProbeFormOffset(EDrawable sender, object arg)
        {
            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = "", MaxNbrChars = 5 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Unit", Value = "PSI", MaxNbrChars = 3, ValueChangedCallback = new Drawables.DrawableCallback(NewProbeFormOffsetUnitChanged) });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Value1", Suffix = "PSI", Value = 0, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "V @Value1", Suffix = "V", Value = 0.5, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Value2", Suffix = "PSI", Value = 500, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "V @Value2", Suffix = "V", Value = 4.5, MinValue = -10000, MaxValue = 10000 });

            uiHandler.ActivateLeftForm("New probe definition", "Save new probe", formEntries, new DrawableCallback(AddNewProbeOffset));
        }

        public static void NewProbeFormOffsetUnitChanged(EDrawable sender, object arg)
        {
            EForm form = (EForm)sender;
            string newValue = (string)arg;

            form.ChangeSuffix("Value1", newValue);
            form.ChangeSuffix("Value2", newValue);
        }

        public static void AddNewProbeOffset(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];

            string name = (string)mapper["Name"];
            string unit = (string)mapper["Unit"];
            double y1 = double.Parse(mapper["Value1"]);
            double x1 = double.Parse(mapper["V @Value1"]);
            double y2 = double.Parse(mapper["Value2"]);
            double x2 = double.Parse(mapper["V @Value2"]);

            double gain = (y2 - y1) / (x2 - x1);
            double offset = y1 - gain * x1;
            double off = y2 - gain * x2;

            Probe newProbe = new Probe(name, unit, (float)gain, (float)offset, false);
            uiHandler.AddProbe(newProbe);
        }
        ////////////////////////////

        ////////////////////////////
        // Simplest probe
        public static void ShowAddNewProbeFormSimplest(EDrawable sender, object arg)
        {
            List<FormEntryDefinition> formEntries = new List<FormEntryDefinition>();
            formEntries.Add(new FormEntryDefinitionString() { Name = "Name", Value = "", MaxNbrChars = 5 });
            formEntries.Add(new FormEntryDefinitionDouble() { Name = "Factor", Prefix = "x", Value = 1, MinValue = -10000, MaxValue = 10000 });
            formEntries.Add(new FormEntryDefinitionString() { Name = "Unit", Value = "V", MaxNbrChars = 3 });
            
            uiHandler.ActivateLeftForm("New probe definition", "Save new probe", formEntries, new DrawableCallback(AddNewProbeSimplest));
        }

        public static void AddNewProbeSimplest(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Dictionary<string, string> mapper = (Dictionary<string, string>)args[0];

            string name = (string)mapper["Name"];
            double gain = double.Parse(mapper["Factor"]);
            string unit = (string)mapper["Unit"];

            Probe newProbe = new Probe(name, unit, (float)gain, 0, false);
            uiHandler.AddProbe(newProbe);
        }
        ////////////////////////////

        public static void ShowNumpadFormFieldDouble(EDrawable sender, object arg)
        {
            FormEntryDefinitionDouble entryDouble = (FormEntryDefinitionDouble)arg;

            Point numpadLoc = new Point(uiHandler.Form.Boundaries.Right, uiHandler.Form.Boundaries.Bottom);
            double value = double.Parse(uiHandler.Form.GetValue(entryDouble.Name));
            uiHandler.ShowKeyboardNumerical(entryDouble.MinValue, entryDouble.MaxValue, 0.001, entryDouble.Suffix != null ? entryDouble.Suffix.ToString() : "", value, numpadLoc, new DrawableCallback(FormUpdateField, entryDouble.Name));
        }

        public static void ShowNumpadFormFieldStringAlfa(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            string entryName = (string)args[0];
            int maxNbrChars = (int)args[1];

            Point numpadLoc = new Point(uiHandler.Form.Boundaries.Right, uiHandler.Form.Boundaries.Bottom);
            string value = uiHandler.Form.GetValue(entryName);
            uiHandler.ShowKeyboardAlfa(value, numpadLoc, maxNbrChars, new DrawableCallback(FormUpdateField, entryName));
        }

        public static void ShowNumpadFormFieldStringAlfaNumeric(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            string entryName = (string)args[0];
            int maxNbrChars = (int)args[1];

            Point numpadLoc = new Point(uiHandler.Form.Boundaries.Right, uiHandler.Form.Boundaries.Bottom);
            string value = uiHandler.Form.GetValue(entryName);
            uiHandler.ShowKeyboardAlfaNumeric(value, numpadLoc, maxNbrChars, new DrawableCallback(FormUpdateField, entryName));
        }

        public static void HideForm(EDrawable sender, object arg)
        {
            uiHandler.DeactivateLeftForm();
        }

        //retrieves value from Keyboard and sends it to correct Form entry
        public static void FormUpdateField(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            string entryName = (string)args[0];
            object value = args[1];

            uiHandler.Form.SetValue(entryName, value);

            uiHandler.RemoveFloater(sender);
        }

        public static void ToggleGlobalMenu(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.ToggleMainMenu();
        }

        public static void ScreenCapture(EDrawable sender, object arg)
        {
            ScopeApp.ScreenshotRequested = true;
        }

        public static void ToggleMeasurementMenu(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.ToggleMeasurementMenu();
        }
        public static void TogglePanoramaByUser(EDrawable sender, object arg)
        {
            uiHandler.TogglePanoramaByUser();
        }

        public static void PanZoomViewportFromPanorama(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] zoomArgArray = (object[])arg;
            float zoom = (float)zoomArgArray[0];
            float offset = (float)zoomArgArray[1];
            float center = (float)zoomArgArray[2];
            bool wasMouseScroll = false;
            if (zoomArgArray.Length > 3)
                wasMouseScroll = (bool)zoomArgArray[3];
            uiHandler.PanZoomViewportFromPanorama(zoom, offset, center, wasMouseScroll);
        }

		public static void PanZoomPanoramaFromPanorama(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            object[] zoomArgArray = (object[])arg;
            float zoom = (float)zoomArgArray[0];
            float pan = (float)zoomArgArray[1];
            float center = (float)zoomArgArray[2];
            bool wasMouseScroll = false;
            if (zoomArgArray.Length > 3)
                wasMouseScroll = (bool)zoomArgArray[3];
            uiHandler.PanZoomPanoramaFromPanorama(zoom, pan, center, wasMouseScroll);
        }

        public static void ToggleDropDownMenu(EDrawable sender, object arg)
        {
            EDropDown triggerDropDown = (EDropDown)sender;
            bool expanded = (bool)arg;
            if (expanded)
            {
                triggerDropDown.Collapse();
            }
            else
            {
                uiHandler.CloseMenusOnGraphArea();
                triggerDropDown.Expand();
            }
        }
        public static void GridClicked(EDrawable sender, object gestureLocation)
        {
            if (!uiHandler.InteractionAllowed) return;
            if (uiHandler.CloseMenusOnGraphArea())
                return;
            uiHandler.SelectNextChannel(Waveform.ChannelsAt((Point)gestureLocation), 1);
        }
        public static void ShowDialog(EDrawable sender, object arg)
        {
            object[] args = arg as object[];

            string message;
            if (args != null)
                message = (string)args[0];
            else
                message = (string)arg;

            List<ButtonInfo> buttons = null;
            if (args != null)
                buttons = args[1] as List<ButtonInfo>;

            uiHandler.ShowDialog(message, buttons);
        }
        public static void HideDialog(EDrawable button, object arg)
        {
            uiHandler.HideDialog();
        }
        public static void OpenUrl (EDrawable sender, object arg)
		{
			string url;
			object[] args = null;
			if (arg is Array) {
				args = (object[])arg;
				url = (string)args [0];
			} else
				url = (string)arg;
#if ANDROID
            var uri = Android.Net.Uri.Parse (url);
            var intent = new Intent (Intent.ActionView, uri); 
            intent.AddFlags(ActivityFlags.NewTask);
            uiHandler.context.StartActivity(intent); 
#else
            Process.Start(url);
#endif
			if (args != null)
            	((DrawableCallback)args[1]).Call(sender);
        }
        public static void ShowFileSelector(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            uiHandler.ShowFileSelector((int)args[0], (int)args[1], (int)args[2], (List<EDialog.Button>)args[3], (DrawableCallback)args[4]);
        }

#if WINDOWS || MONOMAC || LINUX || ANDROID
        public static void SetAutoUpdate(EDrawable sender, object arg)
        {
            uiHandler.SetAutoUpdate((bool)arg);
        }
#endif

        public static void SetHighBandwidthMode(EDrawable sender, object arg)
        {
            uiHandler.SetHighBandwidthMode((bool)arg);
        }
        public static void SetStoreToDropbox(EDrawable sender, object arg)
        {
            uiHandler.SetStoreToDropbox((bool)arg);
        }
        public static void ResetAndQuit(EDrawable sender, object arg)
        {
            uiHandler.ResetSettingsAndQuit();
        }


        public static void ShowToast(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            uiHandler.ShowSimpleToast((string)args[0], (int)args[1]);
        }

        public static void ShowAbout(EDrawable sender, object arg)
        {
            uiHandler.ShowAbout();
        }

		public static void ToggleHelp(EDrawable sender, object arg)
        {
        	uiHandler.CloseMenusOnGraphArea();
            uiHandler.ToggleHelp();
        }

        public static void EnableETS(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.EnableETS((bool)args[1]);
        }
			
		public static void Quit(EDrawable sender, object arg)
        {
        	uiHandler.scopeApp.Quitting = true;
        	uiHandler.CloseMenusOnGraphArea();
        }

		//User requested GUI size change
        public static void ChangeGuiSize(EDrawable sender, object arg)
        {
            Scale newScale = (Scale)arg;
            uiHandler.SetGuiScale(sender, newScale);
			//Here we store whichever is the result of the scaling as default
			uiHandler.PreferredUiScale = Scaler.CurrentScale;
        }

        //User requested GUI size change
        public static void ChangeWaveformThickness(EDrawable sender, object arg)
        {
            int adj = (int)arg;
            float newThickness = Waveform.Thickness + adj;
            uiHandler.ChangeWaveformThickness((int)newThickness);
        }

        public static void EnabledFFT(EDrawable sender, object arg)
        {
            Settings.Current.analogGraphCombo = uiHandler.EnableFFT((bool)arg);
            uiHandler.UpdateGraphCheckboxesTicks();
        }

        public static void ShowChannelMeasurementInBox(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Type type = (Type)args[0];            
            AnalogChannel ch = (AnalogChannel)args[1];
            bool show = (bool)args[2];

            uiHandler.ShowChannelMeasurementInBox(type, show, ch);
        }

        public static void ShowChannelMeasurementInGraph(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            Type type = (Type)args[0];
            AnalogChannel ch = (AnalogChannel)args[1];
            bool show = (bool)args[2];

            uiHandler.ShowChannelMeasurementInGraph(type, show, ch, true);
        }

        public static void RefreshAccessPoints(EDrawable sender, object arg)
        {
            uiHandler.ShowWifiMenu(true, false);
        }

        public static void ShowSystemMeasurementInGraph(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            SystemMeasurementType type = (SystemMeasurementType)args[0];
            bool show = (bool)args[1];

            uiHandler.ShowSystemMeasurementInGraph(type, show, true);
        }

        public static void ShowSystemMeasurementInBox(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            SystemMeasurementType type = (SystemMeasurementType)args[0];
            bool show = (bool)args[1];

            uiHandler.ShowSystemMeasurementInBox(type, show);
        }

        public static void EnabledXY(EDrawable sender, object arg)
        {
            Settings.Current.analogGraphCombo = uiHandler.EnableXY((bool)arg);
            uiHandler.UpdateGraphCheckboxesTicks();
        }

        public static void EnabledMeasurementMode(EDrawable sender, object arg)
        {
            Settings.Current.analogGraphCombo = uiHandler.EnableMeasurementMode((bool)arg);
            uiHandler.UpdateGraphCheckboxesTicks();
        }

        public static void SquareXY(EDrawable sender, object arg)
        {
            Settings.Current.xySquared = uiHandler.SquareXY((bool)arg);
        }

        public static void InvertXY(EDrawable sender, object arg)
        {
            Settings.Current.xyInverted = uiHandler.InvertXY((bool)arg);
        }

        public static void ChangeColorMode(EDrawable sender, object arg)
        {
            ColorMapper.CurrentMode = (ColorMapper.Mode)arg;
            uiHandler.scopeApp.OnResize(true);
        }
        public static void SwitchRenderMode(EDrawable sender, object arg)
        {
            Settings.Current.RenderMode = Settings.Current.RenderMode.Value == RenderMode.Deferred ? RenderMode.Immediate : RenderMode.Deferred;
        }
        public static void ChangeStickyTicks(EDrawable sender, object arg)
        {
            uiHandler.SetTickStickyNess((TickStickyNess)arg);
        }        
        public static void ChangeAutoRolling(EDrawable sender, object arg)
        {
            Settings.Current.SwitchAutomaticallyToRollingMode = (bool)arg;
        }
        public static void ChangeGridAnchor(EDrawable sender, object arg)
        {
            uiHandler.SetGridAnchor((GridAnchor)arg);
        }
        public static void ChangeCursorReference(EDrawable sender, object arg)
        {
            uiHandler.ChangeCursorReference((bool)arg);
        }

#if WINDOWS || MONOMAC || LINUX || ANDROID
        public static void CheckForUpdates(EDrawable sender, object arg)
        {
            bool silent = false;
            if (arg != null)
                silent = (bool)arg;

            uiHandler.CheckForUpdates(silent);
        }
#endif
        public static void HideControl(EDrawable sender, object arg)
        {
            sender.Visible = false;
        }
        internal static void AdjustMultigraphHeight(EDrawable sender, object argument)
        {
            uiHandler.AdjustMultigraphHeight((float)argument);
        }
        internal static void ToggleContextDropdown(EDrawable sender, object argument)
        {
            EContextMenuDropdown d = (EContextMenuDropdown)argument;
            d.Toggle();
        }
#endregion

#region Debug

		public static void ToggleLog(EDrawable sender, object arg)
        {
            uiHandler.ToggleLog(sender);
            uiHandler.ToggleWifiMenu();
        }

        public static void ToggleSmartScopeOutputBit(EDrawable sender, object arg)
        {
            uiHandler.ToggleSmartScopeOutputBit((MenuItem)sender, (int)arg);
        }

#if DEBUG
        public static void DrawInteractiveAreas(EDrawable sender, object arg)
        {
            EDrawable.drawInteractiveAreas = !EDrawable.drawInteractiveAreas;
        }
        public static void EnableTimeSmoothing(EDrawable sender, object arg)
        {
            bool enable = (bool)arg;
            DataProcessorSmartScope.TimeSmoothingEnabled = enable;
        }
        public static void EnableETSTimeSmoothing(EDrawable sender, object arg)
        {
            bool enable = (bool)arg;
            DataProcessorETS.ETSTimeSmoothing = enable;
        }
        public static void EnableEquivalentSampling(EDrawable sender, object arg)
        {
            bool enable = (bool)arg;
            DataProcessorETS.Disable = !enable;
        }
        public static void EnableSincTriggering(EDrawable sender, object arg)
        {
            bool enable = (bool)arg;
            DataProcessorSincTriggering.EnableSincInterpolation = enable;
        }
        public static void DebugMemoryWrite(EDrawable sender, object arg)
        {
            object[] args = arg as object[];
            DeviceMemory m = args[0] as DeviceMemory;
            uint register = (uint)args[1];
            object value = args[2];
            m[register].Set(value).WriteImmediate();
        }

        public static void TestDialog(EDrawable sender, object arg)
        {
            EDialog.Row textrow = new EDialog.Row(.8f,
                new List<EDialog.DialogItem>() {
                    new EDialog.Column(.33f, EDialog.Border.Right, 
                        new List<EDialog.DialogItem>() {
                            new EDialog.Label() { text = "There was" },
                            new EDialog.Label() { text = "A Boy" },
                            new EDialog.Label() { text = "A very strang" },
                            new EDialog.Label() { text = "And odd boy" }
                        }),
                    new EDialog.Column(.33f, EDialog.Border.Right,
                        new List<EDialog.DialogItem>() {
                            new EDialog.Label() { text = "But then" },
                            new EDialog.Label() { text = "one day" },
                            new EDialog.Label() { text = "A wanderer" }
                        }),
                    new EDialog.Column(.33f, EDialog.Border.Right,
                        new List<EDialog.DialogItem>() {
                            new EDialog.Label() { text = "Asked" },
                            new EDialog.Label() { text = "the boy" },
                            new EDialog.Label() { text = "what to do" },
                            new EDialog.Label() { text = "oh, oh, oh," },
                            new EDialog.Label() { text = "oh, what to do" }
                        }),
                });
            EDialog.Row buttonrow = new EDialog.Row(.2f, new List<EDialog.DialogItem>() {
                    new EDialog.Column(.33f),
                    new EDialog.Column(.33f, new List<EDialog.DialogItem>() {
                        new EDialog.Button() {
                            text = "close",
                            callback = new DrawableCallback(HideDialog),
                        }
                    }),
                    new EDialog.Column(.33f),
            });

            EDialog.Column wrapper = new EDialog.Column()
            {
                background = new Color(0, 0, 0, 100),
                contents = new List<EDialog.DialogItem>() { textrow, buttonrow }
            };

            uiHandler.ShowDialog(wrapper);
        }
#endif
#endregion

#region Labdevices
#if LABDEVICES
        /* AWG stuff */
        WaveForm Dg4000WaveForm = WaveForm.SINE;
        double Dg4000Frequency = 1e2;
        double Dg4000AmplitudeScaler = 1.0;
        uint Dg4000Points = 16384;
        uint Dg4000MultisineHarmonic = 2;

        public static void GUI_UploadDg4000Waveform(EDrawable sender, object arg)
        {
            DG4000 d;
            try
            {
                d = new DG4000();
            }
            catch (Exception)
            {
                GUI_ShowDialog(null, "No DG4000 AWG connected");
                return;
            }
            double timeOffset = 0;
            double amplitude = 0x3FFF / 2 * Dg4000AmplitudeScaler;
            double phase = 0;
            double awgSamplePeriod = 1.0 / Dg4000Points;
            double frequency = 1;

            float[] wave;
            switch (Dg4000WaveForm)
            {
                case WaveForm.SINE:
                    wave = DummyScope.WaveSine(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.SQUARE:
                    wave = DummyScope.WaveSquare(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.SAWTOOTH:
                    wave = DummyScope.WaveSawTooth(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.TRIANGLE:
                    wave = DummyScope.WaveTriangle(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.SAWTOOTH_SINE:
                    wave = DummyScope.WaveSawtoothSine(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                case WaveForm.MULTISINE:
                    float[] wave1 = DummyScope.WaveSine(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude / 2.0, phase);
                    float[] wave2 = DummyScope.WaveSine(Dg4000Points, awgSamplePeriod, timeOffset, frequency * Dg4000MultisineHarmonic, amplitude / 2.0, phase);
                    wave = wave1.Select((x, i) => x + wave2[i]).ToArray();
                    break;
                case WaveForm.HALF_BIG_HALF_UGLY:
                    wave = DummyScope.WaveHalfBigHalfUgly(Dg4000Points, awgSamplePeriod, timeOffset, frequency, amplitude, phase);
                    break;
                default:
                    throw new NotImplementedException();
            }

            short[] waveInt16 = wave.Select(x => (Int16)(x + amplitude)).ToArray();
            d.UploadArbitraryWaveform(AWGChannel.Channel1, waveInt16);
        }

        public static void GUI_SetDg4000Waveform(EDrawable sender, object arg)
        {
            Dg4000WaveForm = (WaveForm)arg;
        }

        public static void GUI_SetDg4000MultisineHarmonic(EDrawable sender, object arg)
        {
            Dg4000MultisineHarmonic = (uint)Math.Round((float)arg);
            ((MenuItemSlider)sender).Value = Dg4000MultisineHarmonic;
        }

        public static void GUI_SetDg4000Frequency(EDrawable sender, object arg)
        {
            DG4000 d;
            try { d = new DG4000(); }
            catch (Exception) { GUI_ShowDialog(null, "No DG4000 Connected"); return;  }
            d.SetFrequency(AWGChannel.Channel1, (float)arg);

            ((MenuItemSlider)sender).Value = d.GetFrequency(AWGChannel.Channel1);
        }

        public static void GUI_SetDg4000Amplitude(EDrawable sender, object arg)
        {
            DG4000 d;
            try { d = new DG4000(); }
            catch (Exception) { GUI_ShowDialog(null, "No DG4000 Connected"); return; } 
            d.SetAmplitude(AWGChannel.Channel1, (float)arg);
            ((MenuItemSlider)sender).Value = d.GetAmplitude(AWGChannel.Channel1);
        }
        public static void GUI_SetDg4000Offset(EDrawable sender, object arg)
        {
            DG4000 d;
            try { d = new DG4000(); }
            catch (Exception) { GUI_ShowDialog(null, "No DG4000 Connected"); return; }
            d.SetOffset(AWGChannel.Channel1, (float)arg);
            ((MenuItemSlider)sender).Value = d.GetOffset(AWGChannel.Channel1);
        }

#endif
#endregion
    }
}
