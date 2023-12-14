using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class GridMeasurement : Grid
    {
        public GridMeasurement(GraphManager gm, string gridLabelUnit)
            : base(GraphType.Measurements, gm, gridLabelUnit)
        {
            this.ShowCross = false;
            this.ShowCenterSpoke = false;
            this.ShowVerticalGridLinesMajor = true;
            this.ShowVerticalGridLinesMinor = false;
            this.ShowHorizontalGridLines = true;
            this.ShowHorizontalGridLineLabelsLeft = false;
            this.ShowHorizontalGridLineLabelsRight = true;
            this.ShowVerticalGridLineLabelsBottom = true;
            this.TimeScale = LinLog.Linear;
            this.VoltageScale = LinLog.Linear;
        }
    }
}
