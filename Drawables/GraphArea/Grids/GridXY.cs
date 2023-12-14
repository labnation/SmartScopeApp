using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class GridXY : Grid
    {
        public bool Squared = false;
        public bool InvertAxes = false;

        public GridXY(GraphManager gm)
            : base(GraphType.XY, gm, "V")
        {
            this.ShowCross = true;
            this.ShowCenterSpoke = true;
            this.ShowVerticalGridLinesMajor = true;
            this.ShowVerticalGridLinesMinor = true;
            this.ShowHorizontalGridLines = true;
            this.ShowHorizontalGridLineLabelsLeft = false;
            this.ShowHorizontalGridLineLabelsRight = false;
            this.ShowVerticalGridLineLabelsBottom = false;
            this.TimeScale = LinLog.Linear;
            this.VoltageScale = LinLog.Linear;
            this.DivisionHorizontal = new GridDivision(DivisionsVerticalMax, 1, 0); //override, because now we have 2 voltage axis
        }
    }
}
