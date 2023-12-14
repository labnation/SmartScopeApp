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
    internal class CursorVerticalDelta : Cursor
    {
        public Cursor ParentCursor1 { get; private set; }
        public Cursor ParentCursor2 { get; private set; }
        private bool time_nFreq = true;

		protected override string unit { get { return time_nFreq ? "s" : "Hz"; } }
        private double displayValue { get { return time_nFreq ? Value : 1 / Value; } }

        public CursorVerticalDelta(Grid grid, Vector2 location, Cursor cursor1, Cursor cursor2, double precision)
            : base(grid, location, precision, MappedColor.VerticalCursor)
        {
            this.ParentCursor1 = cursor1;
            this.ParentCursor2 = cursor2;

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

        protected override string sideText
        {
            get
            {
                if (double.IsNaN(displayValue) || double.IsInfinity(displayValue)) return "---";
                return LabNation.Common.Utils.siPrint(displayValue, Precision, ColorMapper.NumberDisplaySignificance, unit);
            }
        }

        protected override string centerText
        {
            get
            {
                if (double.IsNaN(displayValue) || double.IsInfinity(displayValue)) return "---";
                return LabNation.Common.Utils.siScale(displayValue, Precision, ColorMapper.NumberDisplaySignificance);
            }
        }
        protected override string bottomText
        {
            get
            {
                if (double.IsNaN(displayValue) || double.IsInfinity(displayValue)) return "";
                return LabNation.Common.Utils.siPrefix(displayValue, Precision, unit);
            }
        }
        protected override string topText { get { return ""; } }

        protected override Vector2 sideTextPosition
        {
            get
            {
                float margin = 3;
                Vector2 stringSize = sideFont.MeasureString(sideText);
                float X = indicatorRectangle.Center.X - stringSize.X / 2;
                if (X + stringSize.X > Boundaries.Right)
                    X = touchCenter.X - stringSize.X - margin;
                return new Vector2(X, Boundaries.Top);
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

                //determine which parent is left and which is right
                Cursor leftParent = ParentCursor1;
                Cursor rightParent = ParentCursor2;
                if (leftParent.Location.X > rightParent.Location.X)
                {
                    leftParent = ParentCursor2;
                    rightParent = ParentCursor1;
                }

                //circle around left parent indicator
                float heightFromLeftCenter = (float)Math.Abs(Location.Y - leftParent.Location.Y);
                float marginLeft = 0;
                if (heightFromLeftCenter < halfDiameterY)
                {
                    float angle = (float)Math.Acos(heightFromLeftCenter / halfDiameterY);
                    marginLeft = (float)Math.Sin(angle) * halfDiameterX;
                }

                //circle around right parent indicator
                float heightFromRightCenter = (float)Math.Abs(Location.Y - rightParent.Location.Y);
                float marginRight = 0;
                if (heightFromRightCenter < halfDiameterY)
                {
                    float angle = (float)Math.Acos(heightFromRightCenter / halfDiameterY);
                    marginRight = (float)Math.Sin(angle) * halfDiameterX;
                }

                //very dirty yet mathematically correct hack to remove visual artifact in case only 1 line should be drawn
                if (Location.X - leftParent.Location.X < halfDiameterX)
                    marginLeft = -leftParent.Location.X + Location.X - halfDiameterX;
                if (rightParent.Location.X - Location.X < halfDiameterX)
                    marginRight = - Location.X - halfDiameterX + rightParent.Location.X;

                vertexList.Add(new VertexPositionColor(new Vector3(leftParent.Location.X + 0.5f + marginLeft, 0.5f - Location.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f - halfDiameterX, 0.5f - Location.Y, 0.0f), c));
                
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f + halfDiameterX, 0.5f - Location.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(rightParent.Location.X + 0.5f - marginRight, 0.5f - Location.Y, 0.0f), c));
                
                return vertexList;
            }
        }

        public void ParentPositionChanged()
        {
            float newX = (ParentCursor1.Location.X + ParentCursor2.Location.X) / 2f;
            this.SetLocation(new Vector2(newX, Location.Y));
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
                Cursor leftParent = ParentCursor1;
                Cursor rightParent = ParentCursor2;
                if (leftParent.Location.X > rightParent.Location.X)
                {
                    leftParent = ParentCursor2;
                    rightParent = ParentCursor1;
                }

                if (location.X < leftParent.Location.X)
                    location.X = leftParent.Location.X;
                if (location.X > rightParent.Location.X)
                    location.X = rightParent.Location.X;                
            }

            base.SetLocation(location);
        }

        protected override void OnTap(GestureSample gesture)
        {
            this.time_nFreq = !this.time_nFreq;

            //above call will cause texts to change, so need to recenter them
            SetLocation(Location);

            base.OnTap(gesture);
        }

        protected override void OnDrag(GestureSample gesture)
        {
            Vector2 oldLocation = this.Location;
            base.OnDrag(gesture);
            Vector2 deltaX = new Vector2(Location.X - oldLocation.X,0);

            ParentCursor1.SetLocation(ParentCursor1.Location + deltaX);
            ParentCursor2.SetLocation(ParentCursor2.Location + deltaX);
        }
    }
}
