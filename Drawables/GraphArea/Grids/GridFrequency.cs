using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class GridFrequency : Grid
    {
        public GridFrequency(GraphManager gm)
            : base(GraphType.Frequency, gm, "")
        {
            this.ShowCross = false;
            this.ShowCenterSpoke = false;
            this.ShowVerticalGridLinesMajor = true;
            this.ShowVerticalGridLinesMinor = true;
            this.ShowHorizontalGridLines = true;
            this.ShowHorizontalGridLineLabelsLeft = false;
            this.ShowHorizontalGridLineLabelsRight = false;
            this.ShowVerticalGridLineLabelsBottom = false;
            this.SupportsLogarithmicScale = true;

            ApplySettings(Settings.Current);
            Settings.ObserveMe(this.ApplySettings);
        }

        public void ApplySettings(Settings settings)
        {
            this.TimeScale = Settings.Current.fftFrequencyScale.Value;
            this.VoltageScale = Settings.Current.fftVoltageScale.Value;
        }
    }
}
