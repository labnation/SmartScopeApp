using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    internal class DataProcessorETS : IDataProcessor
    {
        static public bool Disable = false;
        static public bool ETSTimeSmoothing = true;
        private List<AnalogChannel> enabledAnalogChannels;
        private DataProcessorDifferenceDetector differenceProcessor;
        public int EquivalentSamplingBoost { get; private set; }
        public double EquivalentSamplingRate { get; private set; }
        private double viewportLength = 0;
        public bool ETSEffective { get; private set; }
        public bool ETSCandidate { get; private set; } //indicates whether the timing of GUI and datapackage should be processed for ETS
        
        private bool resetNeeded = false;

        public DataProcessorETS(DataProcessorDifferenceDetector differenceProcessor)
        {
            this.differenceProcessor = differenceProcessor;

            //call ChannelDataSourceProcessor.List, to init ChannelDataSourceProcessor class. needed to have ETSVoltages and ETSTimestamps added to List
            var dummyInit = ChannelDataSourceProcessor.List;
        }

        public void RequestETSReset()
        {
            this.resetNeeded = true;
        }

        public void UpdateGUIValues(List<AnalogChannel> enabledAnalogChannels, double viewportLength)
        {
            this.enabledAnalogChannels = enabledAnalogChannels;
            this.viewportLength = viewportLength;
        }

        Dictionary<AnalogChannel, Queue<ScopeDataCollection>> masterList = new Dictionary<AnalogChannel, Queue<ScopeDataCollection>>();
        public void Process(ScopeDataCollection dataCollection)
        {
            const int minNumberOfSamplesInViewport = 400;
            double samplesInViewport = this.viewportLength / dataCollection.Data.samplePeriod[ChannelDataSourceScope.Overview];
            EquivalentSamplingBoost = (int)Math.Round(minNumberOfSamplesInViewport / samplesInViewport);
            EquivalentSamplingRate = (double)EquivalentSamplingBoost / dataCollection.Data.samplePeriod[ChannelDataSourceScope.Overview];
            bool localETSEffective = false; //needs to be local, as this is done in another thread than GUI. Otherwise, if GUI samples this value during calc, this value will be false.
			double samplePeriodOverview = dataCollection.Data.samplePeriod[ChannelDataSourceScope.Overview];
			double samplePeriodAcquisition = dataCollection.Data.samplePeriod [ChannelDataSourceScope.Acquisition];

            if (
                (samplePeriodOverview != samplePeriodAcquisition) ||
                (EquivalentSamplingBoost < 2) ||
                (Disable)
                )
            {
                ETSCandidate = false;
                ETSEffective = false;
                return;
            }
            else
            {
                ETSCandidate = true;
            }

            /* Masterlist maintenance */
            List<AnalogChannel> addList = enabledAnalogChannels.Where(x => !masterList.ContainsKey(x)).ToList();
            foreach (var ch in addList)
                masterList.Add(ch, new Queue<ScopeDataCollection>());
            for (int i = 0; i < masterList.Count; i++)
                if (!enabledAnalogChannels.Contains(masterList.ElementAt(masterList.Count -1 - i).Key))
                    masterList.Remove(masterList.ElementAt(masterList.Count -1 - i).Key);
            if (resetNeeded)
                foreach (var kvp in masterList)
                    kvp.Value.Clear();

            /* Process ETS queue for each enabled analog channel */
            foreach (var kvp in masterList)
            {
                RunETS(kvp.Key, kvp.Value, dataCollection);
                if (kvp.Value.Count > 1)
                    localETSEffective = true;
            }

            resetNeeded = false;

            ETSEffective = localETSEffective; //thread-safe update
        }

        internal void RunETS(AnalogChannel ch, Queue<ScopeDataCollection> queue, ScopeDataCollection newDataCollection)
        {
            ChannelData d = newDataCollection.GetBestData(ch);

            //in case no data is available for channel; eg when switching to digital mdoe
            if (d == null)
                return;

            //timebase change
            if (queue.Count > 0)
                if (newDataCollection.AcquisitionLength != queue.First().AcquisitionLength)
                    queue.Clear();

            /* delete queue when incoming data is radically different */
            if (queue.Count > 0)
            {
                //check for differences between acquisition parameters
                if (!CheckSimilarity(queue.First(), newDataCollection, ch))
                    queue.Clear();
                //check for differences in input wave
                if (differenceProcessor.DifferenceDetected.ContainsKey(ch))
                    if (differenceProcessor.DifferenceDetected[ch])
                        queue.Clear();
                //reset queue when there was trouble locating trigger position, indicated in triggerAdjust left to 0
                if (newDataCollection.TriggerAdjustment == 0)
                    queue.Clear();
            }

            /* add new data package to queue */
            queue.Enqueue(newDataCollection);

            //remove data which is too much
            int toRemove = queue.Count - EquivalentSamplingBoost;
            for (int i = 0; i < toRemove; i++)
                queue.Dequeue();
            int queueLength = queue.Count;

            /*
             * Approach:
             *  1) Determine which timerange is shared by all arrays in queue
             *  2) For each array: determine which section corresponds to this timerange
             *  3) Copy-paste these sections into 1 large array, ordered by their sinc-time!
             *  4) Create time-array, taking sinc-timings into account
            */

            /* 1) Determine which timerange is shared by all arrays in queue */
            //first ordering pass: order by trigger position
            List<ScopeDataCollection> orderedList = queue.OrderBy(x => x.HoldoffCenter).ToList();

            //  . . . .|. . . . . .|
            //      . .|. . . . . .|.
            //      . .|. . . . . .|.
            //        .|. . . . . .|. .
            //    . . .|. . . . . .|
            //      . .|. . . . . .|.
            //         |. . . . . .|. . .
            //      . .|. . . . . .|.

            //figure out which timeranges are shared by all arrays
            Int64 sharedStartTiming = orderedList.Last().HoldoffSamples;
            Int64 sharedEndTiming = orderedList.First().HoldoffSamples + orderedList.First().GetBestData(ch).array.Length;
            Int64 earliestStart = orderedList.First().HoldoffSamples;

            /* 2) For each array: determine which section corresponds to this timerange */
            Int64 runlength = sharedEndTiming - sharedStartTiming;

            //should never be the case, but small protection anyway
            if (runlength < 2)
            {
                LabNation.Common.Logger.Error("ETS runlength of " + runlength.ToString() + ". This is a prime sign of evilness.");
                return;
            }

            //need to re-order now according to sincAdjust, as these indices are needed when copying data, which needs to be done ordered to sinctime!
            orderedList = queue.OrderBy(x => -x.TriggerAdjustment).ToList();

            int[] startIndices = new int[queueLength];
            for (int i = 0; i < queueLength; i++)
                startIndices[i] = (int)(orderedList.ElementAt(i).HoldoffSamples - earliestStart);

            /* 3) Copy-paste these sections into 1 large array, ordered by their sinc-time! */
            //create easy pointers to source arrays            
            float[][] sourceArrays = new float[queueLength][];
            for (int i = 0; i < queueLength; i++)
                sourceArrays[i] = (float[])orderedList.ElementAt(i).GetBestData(ch).array;

            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //20160616: Factoring out this method related to crashreport "Index was outside the bounds of the array" in RunETS, goal is to get more fine-grained crashreport
            //20161026: "Index was outside the bounds of the array" originates from UberArray method
            // ways to crash:
            // -- 1/ sourceArrays contains less than q elements 
            // -- 2/ sourceArrays contains null element
            // -- 3/ startIndices contains less than q elements --> impossible as this is an int array inited to queueLength elements
            // -- 4/ startIndices contains a negative number
            // -- 5/ one sourceArray contains less elements than [its startIndex + runLength]

            //crashdetection 1:
            if (sourceArrays.Length < queueLength)
            {
                LabNation.Common.Logger.Error("Crash prevented, please send this line to bughunt@lab-nation.com: sourceArrays.Length < queueLength: " + sourceArrays.Length.ToString() +" <  " + queueLength.ToString());
                return;
            }                        
            for (int q = 0; q < queueLength; q++)
            {
                //crashdetection 2:
                if (sourceArrays[q] == null)
                {
                    LabNation.Common.Logger.Error("Crash prevented, please send this line to bughunt@lab-nation.com: sourceArrays[q] == null, " + q.ToString());
                    return;
                }
                //crashdetection 4:
                if (startIndices[q] < 0)
                {
                    LabNation.Common.Logger.Error("Crash prevented, please send this line to bughunt@lab-nation.com: startIndices[q] < 0 " + q.ToString() + " ,  " + startIndices[q].ToString());
                    return;
                }
                //crashdetection 5:
                if (startIndices[q] + runlength > sourceArrays[q].Length)
                {
                    LabNation.Common.Logger.Error("Crash prevented, please send this line to bughunt@lab-nation.com: startIndices[q] + runlength > sourceArrays[q].Length: " + startIndices[q].ToString() + " + " + runlength.ToString() + " > " + sourceArrays[q].Length.ToString());
                    return;
                }
            }
            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            //pump over data
            float[] uberArray = UberArray(queueLength, runlength, startIndices, sourceArrays);            

            /* 4) Create time-array, taking sinc-timings into account */

            //create source values once
            //20160616: Factoring out this method related to crashreport "Index was outside the bounds of the array" in RunETS, goal is to get more fine-grained crashreport
            double[] sourceTimes = SourceTimes(ch, newDataCollection, d, queueLength, orderedList, earliestStart);

            // create full axis
            //20160616: Factoring out this method related to crashreport "Index was outside the bounds of the array" in RunETS, goal is to get more fine-grained crashreport
            float[] timeArray = CreateFullAxis(d, queueLength, runlength, sourceTimes);

            //20160616: Factoring out this method related to crashreport "Index was outside the bounds of the array" in RunETS, goal is to get more fine-grained crashreport
            TimeSmoothing(ref uberArray, ref timeArray);

            newDataCollection.Data.samplePeriod[ChannelDataSourceProcessor.ETSVoltages] = 0;
            newDataCollection.Data.offset[ChannelDataSourceProcessor.ETSVoltages] = 0;
            newDataCollection.SetData(ChannelDataSourceProcessor.ETSVoltages, ch, uberArray);
            newDataCollection.Data.samplePeriod[ChannelDataSourceProcessor.ETSTimestamps] = 0;
            newDataCollection.Data.offset[ChannelDataSourceProcessor.ETSTimestamps] = 0;
            newDataCollection.SetData(ChannelDataSourceProcessor.ETSTimestamps, ch, timeArray);
        }

        private static float[] CreateFullAxis(ChannelData d, int queueLength, Int64 runlength, double[] sourceTimes)
        {
            float[] timeArray = new float[runlength * queueLength];
            int counter2 = 0;
            for (int s = 0; s < runlength; s++)
                for (int q = 0; q < queueLength; q++)
                    timeArray[counter2++] = (float)(sourceTimes[q] + d.samplePeriod * (double)s);
            return timeArray;
        }

        private static void TimeSmoothing(ref float[] uberArray, ref float[] timeArray)
        {
            /* Timesmoothing frenzy */
            if (ETSTimeSmoothing)
            {
                //allocating sufficient memory, in order not to keep on growing a list
                float[] filteredVoltages = new float[uberArray.Length];
                float[] filteredTimes = new float[timeArray.Length];

                int index = 0;
                int patchStartIndex = 0;
                float cumulV = uberArray[0];
                float cumulT = timeArray[0];
                for (int i = 1; i < uberArray.Length - 1; i++)
                {
                    cumulV += uberArray[i];
                    cumulT += timeArray[i];

                    //FIXME: max time difference should adjust itself, even though 1ns works remarkably well here
                    if (timeArray[i] - timeArray[patchStartIndex] > 1e-9)
                    {
                        float patchLength = i - patchStartIndex + 1;
                        filteredVoltages[index] = cumulV / patchLength;
                        filteredTimes[index] = cumulT / patchLength;
                        patchStartIndex = i;
                        index++;
                        cumulV = uberArray[i];
                        cumulT = timeArray[i];
                    }
                }

                uberArray = filteredVoltages.Take(index).ToArray();
                timeArray = filteredTimes.Take(index).ToArray();
            }
        }

        private static double[] SourceTimes(AnalogChannel ch, ScopeDataCollection newDataCollection, ChannelData d, int queueLength, List<ScopeDataCollection> orderedList, Int64 earliestStart)
        {
            double[] sourceTimes = new double[queueLength];
            for (int i = 0; i < queueLength; i++)
            {
                sourceTimes[i] = orderedList.ElementAt(i).GetBestData(ch).timeOffset - orderedList.ElementAt(i).TriggerAdjustment;

                //in case when trigger is moved to right, the left samples of current array are dropped, so we need to increase the timestamp of the first actual sample
                if (newDataCollection.HoldoffSamples - earliestStart > 0)
                    sourceTimes[i] += (double)(newDataCollection.HoldoffSamples - earliestStart) * d.samplePeriod;
            }
            return sourceTimes;
        }

        private static float[] UberArray(int queueLength, Int64 runlength, int[] startIndices, float[][] sourceArrays)
        {
            float[] uberArray = new float[runlength * queueLength];
            int counter = 0;
            for (int s = 0; s < runlength; s++)
                for (int q = 0; q < queueLength; q++)
                    uberArray[counter++] = sourceArrays[q][startIndices[q] + s];
            return uberArray;
        }

        private bool CheckSimilarity(ScopeDataCollection coll1, ScopeDataCollection coll2, AnalogChannel ch)
        {
            //prep data
            ChannelData d1 = coll1.GetBestData(ch);
            ChannelData d2 = coll2.GetBestData(ch);
            float resolution1 = coll1.resolution[ch];
            float resolution2 = coll2.resolution[ch];
            float[] arr1 = (float[])d1.array;
            float[] arr2 = (float[])d2.array;

            //general code safety
            if (arr1.Length != arr2.Length)
                return false;

            //voltagebase change (recommended as different gain has different amplitude which might affect voltages
            if (resolution1 != resolution2)
                return false;

            //timebase change
            if (d1.timeOffset != d2.timeOffset)
                return false;

            //ensure there's at least some overlap
            if (Math.Abs(coll1.HoldoffSamples - coll2.HoldoffSamples) > (coll2.AcquisitionSamples - 2))
                return false;

            return true;
        }

        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }
    }
}
