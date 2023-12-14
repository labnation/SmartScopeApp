using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables
{
    internal class CursorHorizontal : Cursor
    {
        public WaveformAnalog waveform { get; private set; }

        public CursorHorizontal(Grid grid, WaveformAnalog waveform, Vector2 location, double precision)
            : base(grid, location, precision, waveform.GraphColor)
        {
            this.waveform = waveform;
            //Re-call SetLocation here (already done in base constructor) because waveform is only now set
            this.SetLocation(location);

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
        }

        //FIXME: get voltage precision from some constant
        protected override string sideText { get { return LabNation.Common.Utils.siPrint(Value, Precision, ColorMapper.NumberDisplaySignificance, (waveform.Channel as LabNation.DeviceInterface.Devices.AnalogChannel).Probe.Unit); } }
        protected override string centerText { get { return LabNation.Common.Utils.siScale(Value, Precision, ColorMapper.NumberDisplaySignificance); } }
        protected override string bottomText { get { return LabNation.Common.Utils.siPrefix(Value, Precision, (waveform.Channel as LabNation.DeviceInterface.Devices.AnalogChannel).Probe.Unit); } }
        protected override string topText { get { return ""; } }

        protected override Vector2 sideTextPosition
        {
            get
            {
                float margin = 3;
                Vector2 stringSize = sideFont.MeasureString(sideText);
                float Y = touchCenter.Y - stringSize.Y;
                if(Y < Boundaries.Top)
                    Y = touchCenter.Y + margin;
                return new Vector2(Boundaries.Right - stringSize.X, Y);
            }
        }

        protected override List<VertexPositionColor> vertexList
        {
            get
            {
                List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
                Color c = graphColor.C();
                Vector3 topleft = device.Viewport.Unproject(new Vector3(indicatorRectangle.Left, indicatorRectangle.Top, 1), this.Projection, this.View, this.localWorld);
                Vector3 botright = device.Viewport.Unproject(new Vector3(indicatorRectangle.Right, indicatorRectangle.Bottom, 1), this.Projection, this.View, this.localWorld);
                vertexList.Add(new VertexPositionColor(new Vector3(0, 0.5f - Location.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(topleft.X, 0.5f - Location.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(botright.X, 0.5f - Location.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(1, 0.5f - Location.Y, 0.0f), c));
                return vertexList;
            }
        }

        public void RecomputePosition()
        {
            Vector2 newLocation = this.Location;
            newLocation.Y = (float)((Value + waveform.VoltageOffset) / waveform.VoltageRange);
            SetLocation(newLocation);
        }
        public override void SetLocation(Vector2 location)
        {
            //FIXME: this check shouldn't be necessary - it's there because the base class calls this before waveform is set
            if(waveform != null)
                Value = location.Y * waveform.VoltageRange - waveform.VoltageOffset;
            base.SetLocation(location);
        }
    }
}
