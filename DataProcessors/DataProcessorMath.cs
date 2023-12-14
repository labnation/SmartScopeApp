using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;

namespace ESuite.DataProcessors
{
    internal class DataProcessorMath: IDataProcessor
    {
        private ChannelData[] lastOperands = new ChannelData[2] {
            new ChannelData(ChannelDataSourceScope.Viewport, AnalogChannel.ChA, null, false, 0),
            new ChannelData(ChannelDataSourceScope.Viewport, AnalogChannel.ChB, null, false, 0)
        };

        internal List<AnalogChannel> inputChannels = new List<AnalogChannel>() {
            AnalogChannel.ChA,
            AnalogChannel.ChB,
        };

        public enum Operation 
        {
                A_PLUS_B,
                A_MINUS_B,
                A_TIMES_B,
                A_DIVIDED_BY_B,
        }

        public Dictionary<Operation, Func<float, float, float>> Operations = new Dictionary<Operation, Func<float, float, float>>()
        {
            { Operation.A_PLUS_B,       new Func<float, float, float> ((a, b) => (a + b))},
            { Operation.A_MINUS_B,      new Func<float, float, float> ((a, b) => (a - b))},
            { Operation.A_TIMES_B,      new Func<float, float, float> ((a, b) => (a * b))},
            { Operation.A_DIVIDED_BY_B, new Func<float, float, float> ((a, b) => (a / b))},
        };

        public Operation operation;
        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }
        public MathChannel channel { get; private set; }
        public DataProcessorMath(Operation operation = Operation.A_PLUS_B) {
            this.operation = operation;
            channel = new MathChannel("Math", 0, this);
        }
        
        public void Process(ScopeDataCollection scopeDataCollection)
        {
            ChannelData operand0 = scopeDataCollection.GetBestData(AnalogChannel.ChA);
            ChannelData operand1 = scopeDataCollection.GetBestData(AnalogChannel.ChB);
            
            if(operand0 == null || operand1 == null)
                return;
            if (lastOperands[0].Equals(operand0) && lastOperands[1].Equals(operand1))
                return;
            lastOperands[0] = operand0;
            lastOperands[1] = operand1;
            float[] result = LabNation.Common.Utils.CombineArrays<float, float, float>((float[])operand0.array, (float[])operand1.array, Operations[this.operation]);
            
            scopeDataCollection.SetData(ChannelDataSourceScope.Viewport, channel, result);
        }
    }
}
