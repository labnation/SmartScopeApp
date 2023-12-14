using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class GridDigital : Grid
    {
        public GridDigital(GraphManager gm)
            : base(GraphType.Digital, gm, "")
        {
            this.ShowCross = false;
            this.ShowCenterSpoke = true;
            this.ShowVerticalGridLinesMajor = true;
            this.ShowVerticalGridLinesMinor = true;
            this.ShowHorizontalGridLines = false;
            this.ShowHorizontalGridLineLabelsLeft = false;
            this.ShowHorizontalGridLineLabelsRight = false;
            this.ShowVerticalGridLineLabelsBottom = false;
            this.TimeScale = LinLog.Linear;
            this.VoltageScale = LinLog.Linear;
        }
    }
}
