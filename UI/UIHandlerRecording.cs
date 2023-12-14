using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if ANDROID
using Android.Content;
#endif

using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Drawables;
using ESuite.DataStorage;


namespace ESuite
{
    internal partial class UIHandler
    {
        #region Recording

        private bool StoreToDropbox
        {
            get { return Settings.Current.StoreToDropbox.Value; }
        }

		internal void SetStoreToDropbox(bool storeToDropbox)
		{
			Settings.Current.StoreToDropbox = storeToDropbox;
		}

        internal void SetRecordingAcquisitionsPerInterval(int acqs)
        {
            if (scope == null)
                return;
            if (acqs < 1)
                return;

            Settings.Current.RecordingAcquisitionsPerInterval = acqs;
        }

        internal int GetRecordingAcquisitionsPerInterval()
        {
            return Settings.Current.RecordingAcquisitionsPerInterval.Value;
        }

        internal void SetRecordingInterval(TimeSpan interval)
        {
            if (scope == null || scope.DataSourceScope == null)
                return;
            if (interval.TotalMilliseconds < 0)
                return;

            Settings.Current.RecordingInterval = interval;
        }

        internal void SetMeasurementsTimespan(TimeSpan timespan)
        {
            measurementManager.LLTimespan = timespan;
            Settings.Current.MeasurementAcquisitionTimespan = measurementManager.LLTimespan;
        }

        internal TimeSpan GetMeasurementsTimespan()
        {
            return measurementManager.LLTimespan;
        }

        internal TimeSpan GetRecordingInterval()
        {
            return Settings.Current.RecordingInterval.Value;
        }

        internal void ToggleRecording()
        {
            if (!engine.RecordingBusy)
            {
                if (!scope.Running)
                {
                    ShowDialog("Scope not running, store last received data?", new List<ButtonInfo>() {
                        new ButtonInfo("OK", (EDrawable sender, object arg) => {
                            EDialogProgress dialogProgress = ShowProgressDialog("Storing...", 0f);
                            StorageFile f = ((ScopeDataCollection)arg).SaveAs(
                                Settings.Current.StorageFormat.Value, 
                                (progress) =>
                                {
                                    dialogProgress.Progress = progress;
                                });
                            f.proposedPath = "/Recordings/SmartScopeDump" + f.format.GetFileExtension();
                            if(StoreToDropbox)
                                QueueCallback(UICallbacks.StoreFileDropbox, f);
                            else
                                QueueCallback(UICallbacks.StoreFileLocal, f);

                        }, lastScopeData),
                        new ButtonInfo("Cancel", UICallbacks.HideDialog)
                    });
                }
                else
                    StartRecording();
            }
            else
                StopRecording(null);
        }
        internal void StopRecording(DrawableCallbackDelegate recordingHandler)
        {
            if (engine.StopRecording())
            {
                FinishRecording(recordingHandler);
            }
            else
                ShowScreenWideDialog(
                    "No data to store, probably no data came in during the recording session",
                    new List<ButtonInfo>() { new ButtonInfo("OK", UICallbacks.HideDialog) }
                    );

            (measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.StorageFileSize] as Measurements.MeasurementStorageMemorySize).Reset();
            measurementManager.ShowSystemMeasurmentInBox(Measurements.SystemMeasurementType.StorageFileSize, false);
        }
        internal bool StartRecording()
        {
            if (scope is SmartScope && ((SmartScope)scope).ChunkyAcquisitions)
            {
                UICallbacks.ShowDialog(null, "Can't record when zoomed out this far\nWe're working on making that possible");
                return false;
            }
            /*if (scope.Rolling)
            {
                UICallbacks.ShowDialog(null, "Can't record in rolling mode\nWe're working on that.");
                //return false;
            }*/

            if (!scope.Ready)
            {
                UICallbacks.ShowDialog(null, "No scope found to record from\nMake sure it's connected");
                return false;
            }

            //show storage file size measurement
            (measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.StorageFileSize] as Measurements.MeasurementStorageMemorySize).Reset();
            measurementManager.ShowSystemMeasurmentInBox(Measurements.SystemMeasurementType.StorageFileSize, true);
            ShowMeasurementBox(true);

            if (!engine.StartRecording(scope.Rolling))
            {
                ShowScreenWideDialog("Can't start recording since a previous recording is still ongoing");
                return false;
            }
            HideDialog();
            recordingStartTime = DateTime.Now;
            return true;
        }
        public bool IsRecording { get { return engine.RecordingBusy; } }

        internal void FinishRecording(DrawableCallbackDelegate recordingHandler)
        {
            HideDialog();
            recordButton.Selected = false;

            EDialogProgress dialogProgress = ShowProgressDialog("Storing...", 0f);
#if DEBUG
            if (recordingHandler != null)
            {
                engine.StopRecording();
                recordingHandler(null, engine.Recording);
            }
            else
            {
#endif
                RecordingHandler.FinishRecordingAsync(
                    engine.Recording,
                    Settings.Current.StorageFormat.Value,
                    (progress) =>
                    {
                        dialogProgress.Progress = progress;
                    },
                    (storageFile) =>
                    {
                        engine.DestroyRecording();
                        storageFile.proposedPath = "/Recordings/SmartScopeDump" + storageFile.format.GetFileExtension();
                        if(StoreToDropbox)
                            QueueCallback(UICallbacks.StoreFileDropbox, storageFile);
                        else
                            QueueCallback(UICallbacks.StoreFileLocal, storageFile);
                    },
                    (failure) =>
                    {
                        engine.DestroyRecording();
                        QueueCallback(
                            UICallbacks.ShowDialog,
                            new object[] {
                                "No data was stored, probably nothing came in while recording\n" + failure.Message, 
                                new List<ButtonInfo>() { new ButtonInfo("OK", UICallbacks.HideDialog) }
                            }
                        );
                    }
                );
#if DEBUG
            }
#endif
        }
        internal StorageFile MoveRecordedFileToStoragePath(StorageFile file)
        {
            if (!(file.info.Directory == new FileInfo(LabNation.Common.Utils.StoragePath).Directory))
            {
                //Move file to local storage folder in case dropbox storing failed
                List<string> directoryContents = Directory.EnumerateFiles(LabNation.Common.Utils.StoragePath).ToList();
                string filename = Utils.GenerateUniqueNumberedFilename(
                    Path.Combine(LabNation.Common.Utils.StoragePath, "Recording" + file.format.GetFileExtension()),
                    directoryContents);
                filename = Path.Combine(LabNation.Common.Utils.StoragePath, filename);
                try
                {
                    File.Move(file.info.FullName, filename);
                }
                catch (Exception e)
                {
                    ShowSimpleToast("Could not move recording data to " + filename + "!. Error: " + e.Message, 5000);
                }
                file.info = new FileInfo(filename);
                #if ANDROID
                Android.Media.MediaScannerConnection.ScanFile(this.context, new string[] {filename}, null, null);
                #endif
            }
            return file;
        }

        #endregion
    }
}
