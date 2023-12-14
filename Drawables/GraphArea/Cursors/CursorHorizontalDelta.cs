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
    internal class CursorHorizontalDelta : Cursor
    {
        public Cursor ParentCursor1 { get; private set; }
        public Cursor ParentCursor2 { get; private set; }
        public WaveformAnalog waveform { get; private set; }

        public CursorHorizontalDelta(Grid grid, WaveformAnalog waveform, Vector2 location, double precision, Cursor cursor1, Cursor cursor2)
            : base(grid, location, precision, waveform.GraphColor)
        {
            this.unit = unit;
            this.ParentCursor1 = cursor1;
            this.ParentCursor2 = cursor2;
            this.waveform = waveform;

            ParentCursor1.OnPositionChanged += this.ParentPositionChanged;
            ParentCursor2.OnPositionChanged += this.ParentPositionChanged;

            //init to correct value
            ParentPositionChanged();

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
        }

        public void Unlink()
        {
            ParentCursor1.OnPositionChanged -= this.ParentPositionChanged;
            ParentCursor2.OnPositionChanged -= this.ParentPositionChanged;
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
                float Y = indicatorRectangle.Center.Y - stringSize.Y / 2;
                if (Y < Boundaries.Top)
                    Y = touchCenter.Y + margin;
                return new Vector2(Boundaries.Right - stringSize.X, Y);
            }
        }

        protected override List<VertexPositionColor> vertexList
        {
            get
            {
                if (ParentCursor1 == null) return new List<VertexPositionColor>();

                List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
                Color c = graphColor.C();
                Vector3 topleft = device.Viewport.Unproject(new Vector3(indicatorRectangle.Left, indicatorRectangle.Top, 1), this.Projection, this.View, this.localWorld);
                Vector3 botright = device.Viewport.Unproject(new Vector3(indicatorRectangle.Right, indicatorRectangle.Bottom, 1), this.Projection, this.View, this.localWorld);
                float halfDiameterX = (float)(Math.Abs((botright.X - topleft.X) / 2f));
                float halfDiameterY = (float)(Math.Abs((botright.Y - topleft.Y) / 2f));

                //determine which parent is up and which is down
                Cursor downParent = ParentCursor1;
                Cursor upParent = ParentCursor2;
                if (downParent.Location.Y < upParent.Location.Y)
                {
                    downParent = ParentCursor2;
                    upParent = ParentCursor1;
                }

                //circle around left parent indicator
                float spaceFromTopCenter = (float)Math.Abs(Location.X - downParent.Location.X);
                float marginTop = 0;
                if (spaceFromTopCenter < halfDiameterX)
                {
                    float angle = (float)Math.Acos(spaceFromTopCenter / halfDiameterX);
                    marginTop = (float)Math.Sin(angle) * halfDiameterY;
                }

                //circle around right parent indicator
                float spaceFromDownCenter = (float)Math.Abs(Location.X - upParent.Location.X);
                float marginBottom = 0;
                if (spaceFromDownCenter < halfDiameterX)
                {
                    float angle = (float)Math.Acos(spaceFromDownCenter / halfDiameterX);
                    marginBottom = (float)Math.Sin(angle) * halfDiameterY;
                }

                //very dirty yet mathematically correct hack to remove visual artifact in case only 1 line should be drawn
                if (downParent.Location.Y - Location.Y < halfDiameterY)
                    marginTop = downParent.Location.Y - Location.Y - halfDiameterY;
                if (Location.Y - upParent.Location.Y < halfDiameterY)
                    marginBottom = Location.Y - halfDiameterY - upParent.Location.Y;
                
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, 0.5f - downParent.Location.Y + marginTop, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, 0.5f - Location.Y - halfDiameterY, 0.0f), c));
                
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, 0.5f - Location.Y + halfDiameterY, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, 0.5f - upParent.Location.Y - marginBottom, 0.0f), c));
                
                return vertexList;
            }
        }

        public void ParentPositionChanged()
        {
            float newY = (ParentCursor1.Location.Y + ParentCursor2.Location.Y) / 2f;
            this.SetLocation(new Vector2(Location.X, newY));
            this.Value = Math.Abs(ParentCursor1.Value - ParentCursor2.Value);
        }

        public void Recompute()
        {
            SetLocation(this.Location);
        }

        public override void SetLocation(Vector2 location)
        {
            /* restrict to stay within parent Boundaries */
            if (ParentCursor1 != null)
            {
                Cursor downParent = ParentCursor1;
                Cursor upParent = ParentCursor2;
                if (downParent.Location.Y < upParent.Location.Y)
                {
                    downParent = ParentCursor2;
                    upParent = ParentCursor1;
                }

                if (location.Y > downParent.Location.Y)
                    location.Y = downParent.Location.Y;
                if (location.Y < upParent.Location.Y)
                    location.Y = upParent.Location.Y;                
            }

            base.SetLocation(location);
        }



        protected override void OnDrag(GestureSample gesture)
        {
            Vector2 oldLocation = this.Location;
            base.OnDrag(gesture);
            Vector2 deltaY = new Vector2(0, Location.Y - oldLocation.Y);

            ParentCursor1.SetLocation(ParentCursor1.Location + deltaY);
            ParentCursor2.SetLocation(ParentCursor2.Location + deltaY);
        }
    }
}
