using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class GridAnalog : Grid
    {
        public GridAnalog(GraphManager gm)
            : base(GraphType.Analog, gm, "V")
        {
            this.ShowCross = true;
            this.ShowCenterSpoke = true;
            this.ShowVerticalGridLinesMajor = true;
            this.ShowVerticalGridLinesMinor = true;
            this.ShowHorizontalGridLines = true;
            this.ShowHorizontalGridLineLabelsLeft = true;
            this.ShowHorizontalGridLineLabelsRight = false;
            this.ShowVerticalGridLineLabelsBottom = false;
            this.TimeScale = LinLog.Linear;
            this.VoltageScale = LinLog.Linear;
        }
    }
}
