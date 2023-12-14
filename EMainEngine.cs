using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface;
using ESuite.Measurements;
using ESuite.DataProcessors;
using ESuite.DataStorage;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace ESuite
{
    internal class EMainEngine
    {
        public ScopeDataCollection ScopeData { get; private set; }
        public ScopeDataCollection newScopeData = null;
        public DateTime lastProcessTimestamp = DateTime.Now;
        
        public List<IDataProcessor> dataProcessors;

        private object scopeDataPackageLock = new Object();
        private bool initFinished = false;

        // Recording variables
        public RecordingScope Recording { get; private set; }
        private DateTime RecordingLastAcquisitionTimestamp;
        private int RecordingAcquisitionsThisInterval;

        public bool RecordingBusy
        {
            get
            {
                if (Recording == null) return false;
                return Recording.Busy;
            }
        }

        public int AcquisitionsRecorded { get { return Recording.AcquisitionsRecorded; } }
        
        public EMainEngine()
        {
            ScopeData = null;
            this.dataProcessors = new List<IDataProcessor>();
        }

        #region process and measure

        public void UpdateNewestDataPointer(DataPackageScope scopeData, DataSource dataSource)
        {
            if (scopeData == null)
                return;
            if (!initFinished)
                return;
            if (lastProcessTimestamp == scopeData.LastDataUpdate)
                return;

            lastProcessTimestamp = scopeData.LastDataUpdate;
            
            ScopeDataCollection dataCollection = new ScopeDataCollection(scopeData);

            /* Processors */
            /* Need to run this in this thread, as you want the processed channels to be stored to recordings as well */

            //The order here is important in some cases
            //First, the FFTs are taken because processors further down
            //might change the length of the data
            foreach (DataProcessorFFT dataProcessor in FFTChannel.List.Select(x => x.processor))
                dataProcessor.Process(dataCollection);
            //Next we do all user-enabled (or UIHandler enabled) processors which
            //will make the data look different (i.e. averaging, freqcomp,...)
            foreach (IDataProcessor dataProcessor in dataProcessors)
                dataProcessor.Process(dataCollection);

            /* PluginProcessors */
            //set all finished flags to false
            foreach (DataProcessorDecoder dataProcessor in ProcessorChannel.List.Select(x => x.decoder))
                dataProcessor.ProcessingCompletedSuccessfully = false;

            // Run Process on all PluginProcessors until there is no progress anymore
            // Enables inter-dependencies, yet avoids deadlocks from loops
            int needFinishingPrev = 0;
            int needFinishingCurr = -1;
            while (needFinishingCurr != needFinishingPrev)
            {
                needFinishingPrev = needFinishingCurr;
                needFinishingCurr = 0;
                foreach (DataProcessorDecoder dataProcessor in ProcessorChannel.List.Select(x => x.decoder))
                {
                    dataProcessor.Process(dataCollection);
                    if (!dataProcessor.ProcessingCompletedSuccessfully)
                        needFinishingCurr++;
                }
            }

            //if a recording is active, save all processed data into recording as well
            if(RecordingBusy)
                Record(dataCollection);
            
            //lock here, so other thread is guaranteed to take fully processed package
            lock (scopeDataPackageLock)
            {
                newScopeData = dataCollection;
            }
        }

        public void Update()
        {
            initFinished = true;
            if (newScopeData == null)
                return;

            lock (scopeDataPackageLock)
            {
                this.ScopeData = newScopeData;
            }
        }

        public void ResetTimeVariantProcessors()
        {
            foreach (IDataProcessor processor in dataProcessors.Where(x => x.IsTimeVariant))
                processor.Reset();
        }

        #endregion

        #region recording

        public bool StartRecording(bool scopeIsRolling)
        {
            if (Recording != null)
            {
                Logger.Warn("Can't start recording since a previous recording still exists");
                return false;
            }

            this.RecordingLastAcquisitionTimestamp = DateTime.Now;
            this.RecordingAcquisitionsThisInterval = 0;

            Recording = new RecordingScope(scopeIsRolling);

            return true;
        }

        private void Record(ScopeDataCollection dataPackage)
        {
            //Only do the whole acquisitions per interval checking if the interval
            //and acqs per interval is greater than zero
            if (Settings.Current.RecordingInterval > TimeSpan.Zero && Settings.Current.RecordingAcquisitionsPerInterval.Value > 0)
            {
                DateTime now = DateTime.Now;
                if (now.Subtract(RecordingLastAcquisitionTimestamp) > Settings.Current.RecordingInterval)
                {
                    this.RecordingAcquisitionsThisInterval = 0;
                    this.RecordingLastAcquisitionTimestamp = now;
                }

                //exit in case enough acquisitions have already been stored this interval
                if (++this.RecordingAcquisitionsThisInterval > Settings.Current.RecordingAcquisitionsPerInterval.Value)
                    return;
            }
            Recording.Record(dataPackage);
        }

        public bool StopRecording()
        {
            if (Recording == null)
            {
                Logger.Warn("Can't stop recording since no recording exists");
                return false;
            }
            if (!Recording.Busy)
            {
                Logger.Info("Recording stop requested but was already stopped");
                return false;
            }

            Recording.Busy = false;
            if (Recording.acqInfo.Count == 0)
            {
                Recording.Dispose();
                Recording = null;
                return false;
            }
            return true;
        }

        public void DestroyRecording()
        {
            if (Recording != null)
            {
                StopRecording();
                Recording.Dispose();
                Recording = null;
            }
        }

        #endregion
    }
}
