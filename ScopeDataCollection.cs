using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using ESuite.Measurements;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;
using LabNation.Interfaces;
using System.IO;
using MatlabFileIO;
using ESuite.DataStorage;

namespace ESuite
{
    public sealed class ChannelDataSourceProcessor : ChannelDataSource
    {
        private static HashSet<ChannelDataSourceProcessor> list = new HashSet<ChannelDataSourceProcessor>();
        new public static IList<ChannelDataSourceProcessor> List { get { return list.ToList().AsReadOnly(); } }
        private ChannelDataSourceProcessor(string name, int value)
            : base(name, value)
        {
            list.Add(this);
        }
        public static readonly ChannelDataSourceProcessor ETSVoltages = new ChannelDataSourceProcessor("ETSVoltages", 0);
        public static readonly ChannelDataSourceProcessor ETSTimestamps = new ChannelDataSourceProcessor("ETSTimestamps", 1);
    }

    class ScopeDataCollection
    {
        readonly object dataLock = new object();
        Dictionary<ChannelDataSource, Dictionary<Channel, ChannelData>> data;
        public ScopeDataCollection(DataPackageScope scopeData)
        {
            this.Data = scopeData;

            data = new Dictionary<ChannelDataSource, Dictionary<Channel, ChannelData>>();
            foreach (ChannelDataSource t in ChannelDataSource.List)
                data[t] = new Dictionary<Channel, ChannelData>();

        }
        public DataPackageScope Data { get; private set; }
        public double TriggerAdjustment { get; set; }
        public Type ScopeType { get { return Data.ScopeType; } }

        public ChannelData GetUnprocessedData(ChannelDataSourceScope t, Channel ch)
        {
            lock (dataLock)
            {
                if (t == ChannelDataSourceScope.Acquisition && Data.FullAcquisitionFetchProgress < 1f)
                    return null;
                return Data.GetData(t, ch);
            }
        }

        /// <summary>
        /// Returns data of specified source type and channel.
        /// WARNING: If previously SetData was called for this type and channel,
        /// that data will be returned. To ensure getting the original, unprocessed
        /// data (as it came from the IScope), use GetUnprocessedData.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="ch"></param>
        /// <returns></returns>
        public ChannelData GetData(ChannelDataSource t, Channel ch)
        {
            lock (dataLock)
            {
                if (data[t].ContainsKey(ch))
                    return data[t][ch];
                if(t is ChannelDataSourceScope)
                    return GetUnprocessedData(t as ChannelDataSourceScope, ch);
                return null;
            }
        }
        public ChannelData GetBestData(Channel ch)
        {
            ChannelData d = null;
            if (this.FullAcquisitionFetchProgress >= 1)
                d = GetData(ChannelDataSourceScope.Acquisition, ch);
            if (d == null)
                d = GetData(ChannelDataSourceScope.Viewport, ch);
            return d;
            //Don't bother checking for overview if viewport is not found since it's should be sent only after the viewport is received
        }
        public void SetData(ChannelDataSource t, Channel ch, Array d)
        {
            lock (dataLock)
            {
                if (t is ChannelDataSourceScope && !data[t].ContainsKey(ch) && !ch.Destructable)
                    throw new Exception("Can't set data for channel " + ch + " since it's not a channel for derived data");
                data[t][ch] = new ChannelData(t, ch, d, false, Data.samplePeriod[t], Data.offset[t]);
            }
        }

        /// <summary>
        /// Overrides the data. Only use this if you want future calls of GetData for this ChannelData to return
        /// the new array. Note that a new ChannelData object will be created, i.e. GetData() will return a different
        /// object than the one passed into this method.
        /// 
        /// Use SetData when possible! 
        /// 
        /// Original data will always be availble through GetUnprocessedData
        /// </summary>
        /// <param name="d"></param>
        /// <param name="arr"></param>
        public void OverrideData(ChannelData d, Array arr)
        {
            data[d.source][d.channel] = new ChannelData(d.source, d.channel, arr, d.partial, Data.samplePeriod[d.source], Data.offset[d.source]);
        }

        public int Identifier { get { return Data.Identifier; } }
        public double AcquisitionLength { get { return Data.AcquisitionLength; } }
        public uint AcquisitionSamples { get { return Data.AcquisitionSamples; } }
        private double? holdoff;
        public double Holdoff { 
            get { return holdoff.HasValue ? holdoff.Value : Data.Holdoff; }
            set { holdoff = value; }
        }
        public double HoldoffCenter { get { return Holdoff - AcquisitionLength / 2.0; } }
        public Int64 HoldoffSamples { get { return Data.HoldoffSamples; } }
        public bool Rolling { get { return Data.Rolling; } }
        public double ViewportExcess { get { return Data.ViewportExcess; } }

        public float FullAcquisitionFetchProgress { get { return Data.FullAcquisitionFetchProgress; } }
        public Dictionary<string, double> Settings { get { return Data.Settings; }}
        public Dictionary<AnalogChannel, float> resolution { get { return Data.Resolution; } }

        internal StorageFile SaveAs(StorageFileFormat format, Action<float> progress)
        {
            //create Recording
            RecordingScope Recording = new RecordingScope(false); //not rolling, as this SaveAs method is only called when the acquisition is 
            ChannelDataSource src = ChannelDataSourceScope.Viewport;
            int samples = 0;
            foreach (Channel ch in Channel.List)
            {
                ChannelData channelData =  GetBestData(ch);
                if (channelData != null) {
                    src = channelData.source;
                    samples = channelData.array.Length;
                    Recording.Record(ch, channelData.array, channelData.array.Length);
                }
            }
            Recording.acqInfo.Add(
                    new RecordingScope.AcquisitionInfo()
                    {
                        firstSampleTime = (ulong)(DateTime.Now.TimeOfDay.TotalMilliseconds * 1000000.0),
                        samples = samples,
                        samplePeriod = this.Data.samplePeriod[src]
                    });
            Recording.Busy = false;

            return RecordingHandler.FinishRecording(Recording, format, progress);
        }
    }
}
