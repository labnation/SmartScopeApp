using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;

namespace ESuite.DataProcessors
{
    internal interface IDataProcessor
    {
        void Process(ScopeDataCollection scopeDataCollection);
        bool IsTimeVariant { get; }
        void Reset();
    }
}
