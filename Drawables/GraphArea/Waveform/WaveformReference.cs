using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.DataSources;

namespace ESuite.Drawables
{
    internal class WaveformReference : WaveformAnalog
    {
        private double origTimeRange = -1;
        private double origTimeOffset = -1;
        private double origVoltageRange = -1;

        public WaveformReference(Graph graph, MappedColor graphColor, Channel channel)
            : base(graph, graphColor, channel, null, null)
        {
        }

        public void CopyWave(WaveformAnalog origWave)
        {
            this.origTimeOffset = origWave.TimeOffset;
            this.origTimeRange = origWave.TimeRange;
            this.origVoltageRange = origWave.VoltageRange;
            TimeOffset = origWave.TimeOffset;
            TimeRange = origWave.TimeRange;
            VoltageRange = origWave.VoltageRange;
        }

        public override double VoltageRange
        {
            get
            {
                return base.VoltageRange;
            }
            set
            {
                base.VoltageRange = origVoltageRange;
            }
        }

        public override double TimeOffset
        {
            get
            {
                return base.TimeOffset;
            }
            set
            {
                base.TimeOffset = origTimeOffset;
            }
        }

        public override double TimeRange
        {
            get
            {
                return base.TimeRange;
            }
            set
            {
                base.TimeRange = origTimeRange;
            }
        }
    }
}
