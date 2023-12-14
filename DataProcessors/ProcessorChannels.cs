using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    abstract class ChannelDestructable : Channel
    {
        public override bool Destructable { get { return true; } }
        public ChannelDestructable(string name, int value, Type dataType) : base(name, value, dataType) { }
        public virtual void Destroy() { Channel.Destroy(this); }
    }

    abstract class ProcessorChannel : ChannelDestructable
    {
        public DataProcessorDecoder decoder { get; protected set; }
        private static HashSet<ProcessorChannel> list = new HashSet<ProcessorChannel>();
        new public static IList<ProcessorChannel> List { get { return list.ToList().AsReadOnly(); } }
        public ProcessorChannel(string name, int value, Type dataType)
            : base(name, value, dataType)
        {
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }

    class ProtocolDecoderChannel : ProcessorChannel
    {
        public Drawables.RadixType RadixType;
        private static HashSet<ProtocolDecoderChannel> list = new HashSet<ProtocolDecoderChannel>();
        new public static IList<ProtocolDecoderChannel> List { get { return list.ToList().AsReadOnly(); } }

        public ProtocolDecoderChannel(string name, int value, Type dataType, DataProcessorDecoder processor)
            : base(name, value, dataType) 
        {
            this.RadixType = Drawables.RadixType.Hex;
            this.decoder = processor;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class MathChannel : ChannelDestructable
    {
        private static HashSet<MathChannel> list = new HashSet<MathChannel>();
        new public static IList<MathChannel> List { get { return list.ToList().AsReadOnly(); } }
        public DataProcessorMath processor { get; private set; }

        public MathChannel(string name, int value, DataProcessorMath processor)
            : base(name, value, typeof(float))
        {
            this.processor = processor;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class OperatorAnalogChannel : ProcessorChannel
    {
        private static HashSet<OperatorAnalogChannel> list = new HashSet<OperatorAnalogChannel>();
        new public static IList<OperatorAnalogChannel> List { get { return list.ToList().AsReadOnly(); } }

        public OperatorAnalogChannel(string name, int value, DataProcessorDecoder processor)
            : base(name, value, typeof(float))
        {
            this.decoder = processor;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class ReferenceChannel : ChannelDestructable
    {
        private static HashSet<ReferenceChannel> list = new HashSet<ReferenceChannel>();
        new public static IList<ReferenceChannel> List { get { return list.ToList().AsReadOnly(); } }

        public ReferenceChannel(string name, int value)
            : base(name, value, typeof(float))
        {
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class OperatorDigitalChannel : ProcessorChannel
    {
        private static HashSet<OperatorDigitalChannel> list = new HashSet<OperatorDigitalChannel>();
        new public static IList<OperatorDigitalChannel> List { get { return list.ToList().AsReadOnly(); } }

        public OperatorDigitalChannel(string name, int value, DataProcessorDecoder processor)
            : base(name, value, typeof(bool))
        {
            this.decoder = processor;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class FFTChannel : ChannelDestructable
    {
        private static HashSet<FFTChannel> list = new HashSet<FFTChannel>();
        new public static IList<FFTChannel> List { get { return list.ToList().AsReadOnly(); } }
        public DataProcessorFFT processor { get; private set; }
        public AnalogChannel analogChannel { get; private set; }
        public FFTChannel(AnalogChannel ch, DataProcessorFFT processor)
            : base("FFT"+ch.Name, ch.Value, typeof(float))
        {
            this.analogChannel = ch;
            this.processor = processor;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class XYChannel : ChannelDestructable
    {
        private static HashSet<XYChannel> list = new HashSet<XYChannel>();
        new public static IList<XYChannel> List { get { return list.ToList().AsReadOnly(); } }
        public AnalogChannel analogChannelX { get; private set; }
        public AnalogChannel analogChannelY { get; private set; }
        public XYChannel(AnalogChannel chX, AnalogChannel chY)
            : base("XY" + chX.Name, chX.Value, typeof(float))
        {
            this.analogChannelX = chX;
            this.analogChannelY = chY;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
    class MeasurementChannel : ChannelDestructable
    {
        private static HashSet<MeasurementChannel> list = new HashSet<MeasurementChannel>();
        new public static IList<MeasurementChannel> List { get { return list.ToList().AsReadOnly(); } }
        public Measurements.Measurement Measurement { get; private set; }
        public MappedColor Color { get; private set; }
        public MeasurementChannel(Measurements.Measurement measurement, int value)
            : base("MEAS", value, typeof(float))
        {
            this.Measurement = measurement;
            list.Add(this);

            if (measurement is Measurements.ChannelMeasurement)
                this.Color = (measurement as Measurements.ChannelMeasurement).Channel.ToManagedColor();
            else
                this.Color = MappedColor.System;
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }

    class DebugChannel : ChannelDestructable
    {
        private static HashSet<DebugChannel> list = new HashSet<DebugChannel>();
        new public static IList<DebugChannel> List { get { return list.ToList().AsReadOnly(); } }
        public AnalogChannel analogChannel { get; private set; }
        public DebugChannel(AnalogChannel ch, string name, int value)
            : base(name, value, typeof(float))
        {
            this.analogChannel = ch;
            list.Add(this);
        }
        override public void Destroy() { list.Remove(this); base.Destroy(); }
    }
}
