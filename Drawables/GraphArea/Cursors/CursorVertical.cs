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
    internal delegate double ValueFromLocation(float location);

    internal class CursorVertical : Cursor
    {
        ValueFromLocation convertor;
        public static bool TriggerAttachedToScreen { get; private set; } 
        private double triggerHoldoff;

        public CursorVertical(Grid grid, Vector2 location, string unit, ValueFromLocation convertor, double precision, double triggerHoldoff)
            : base(grid, location, precision, MappedColor.VerticalCursor)
        {
            this.unit = unit;
            this.convertor = convertor;
            this.triggerHoldoff = triggerHoldoff;

            //Re-call SetLocation here (already done in base constructor). otherwise InteractionRectangle would be invalid.
            this.SetLocation(location);
        }

        protected override void LoadContentInternal()
        {
            base.LoadContentInternal();
        }

        protected override string sideText { 
            get {
                return LabNation.Common.Utils.siPrint(Value, Precision, ColorMapper.NumberDisplaySignificance, unit);
            } 
        }

        protected override string centerText { get { return LabNation.Common.Utils.siScale(Value, Precision, ColorMapper.NumberDisplaySignificance); } }
        protected override string bottomText { get { return LabNation.Common.Utils.siPrefix(Value, Precision, "s"); } }
        protected override string topText { get { return ""; } }

        protected override Vector2 sideTextPosition
        {
            get
            {
                float margin = 3;
                Vector2 stringSize = sideFont.MeasureString(sideText);
                float X = touchCenter.X + margin;
                if(X + stringSize.X > Boundaries.Right)
                    X = touchCenter.X - stringSize.X - margin;
                return new Vector2(X, Boundaries.Top);
            }
        }
        protected override List<VertexPositionColor> vertexList
        {
            get
            {
                List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
                Color c = graphColor.C();
                
                Vector3 indTopleft = device.Viewport.Unproject(new Vector3(indicatorRectangle.Left, indicatorRectangle.Top, 1), this.Projection, this.View, this.localWorld);
                Vector3 indBotright = device.Viewport.Unproject(new Vector3(indicatorRectangle.Right, indicatorRectangle.Bottom, 1), this.Projection, this.View, this.localWorld);

                Rectangle fullGraphAreaRectangle = new Rectangle();
                if (parent != null) fullGraphAreaRectangle = (parent.parent as GraphManager).InnerSectionRectangle;
                Vector3 screenTopleft = device.Viewport.Unproject(new Vector3(fullGraphAreaRectangle.Left, fullGraphAreaRectangle.Top, 1), this.Projection, this.View, this.localWorld);
                Vector3 screenBotright = device.Viewport.Unproject(new Vector3(fullGraphAreaRectangle.Right, fullGraphAreaRectangle.Bottom, 1), this.Projection, this.View, this.localWorld);

                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, screenTopleft.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, indTopleft.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, indBotright.Y, 0.0f), c));
                vertexList.Add(new VertexPositionColor(new Vector3(Location.X + 0.5f, screenBotright.Y, 0.0f), c));
                return vertexList;
            }
        }
        
        public void Recompute(double triggerHoldoff, double viewfinderCenter, double range)
        {
            this.triggerHoldoff = triggerHoldoff;

            Vector2 newLocation = this.Location;

            if (!TriggerAttachedToScreen)
                newLocation.X = (float)((triggerHoldoff + Value + viewfinderCenter) / range); 

            SetLocation(newLocation);
        }

        public void ChangeReference(bool waveReferenced_nScreenReferenced)
        {
            TriggerAttachedToScreen = !waveReferenced_nScreenReferenced;
            SetLocation(this.Location);
        }

        public override void SetLocation(Vector2 location)
        {
            if(convertor != null)
                Value = convertor(location.X);

            if (!TriggerAttachedToScreen)
                Value -= triggerHoldoff;
       
            base.SetLocation(location);
        }
    }
}
