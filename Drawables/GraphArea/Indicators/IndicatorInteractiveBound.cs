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
    internal class IndicatorInteractiveBound : IndicatorInteractive, IBoundaryDefined
    {
        private BoundaryDefiner boundaryDefiner;

        public IndicatorInteractiveBound(Grid grid, 
            string imageNotSelected, string imageSelected, string imageHighContrast, 
            MappedColor color, MappedColor textColor, 
            Location selectorSide, BoundaryDefiner boundaryDefiner, string centerText = null, string bottomText = null, string contentIconImageName = null)
            : base(grid, imageNotSelected, imageSelected, imageHighContrast, color, textColor, selectorSide, centerText, bottomText, contentIconImageName)
        {
            this.boundaryDefiner = boundaryDefiner;
            boundaryDefiner.AddElement(this, 0);
        }

        internal override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                if (value && !Visible) //changed from false to true
                {
                    if (boundaryDefiner != null)
                    {
                        this.boundaryDefiner.AddElement(this, 0);
                        RecomputePosition();
                    }
                }
                else if (!value && Visible) //changed from true to false
                    if (boundaryDefiner != null)
                        this.boundaryDefiner.RemoveElement(this);

                base.Visible = value;
            }
        }

        public override void RecomputePosition()
        {
            if (boundaryDefiner == null) return;

            base.RecomputePosition();

            if (!Visible)
                return;

            Rectangle intRect = this.interactiveAreas[0];
            if ((selectorSide == Location.Left) || (selectorSide == Location.Right))
                boundaryDefiner.UpdateElement(this, screenCenterPosition.Y);
            else
                boundaryDefiner.UpdateElement(this, screenCenterPosition.X);
        }

        public void UpdateBoundaries(float lowerBoundary, float upperBoundary)
        {
            Rectangle intRect = this.interactiveAreas[0];
            if ((selectorSide == Location.Left) || (selectorSide == Location.Right))
            {
                int preferredLower = (int)(screenCenterPosition.Y - Scaler.MinimalTouchDimension / 2f);
                if (preferredLower < lowerBoundary)
                    preferredLower = (int)lowerBoundary;
                int preferredHigher = (int)(screenCenterPosition.Y + Scaler.MinimalTouchDimension / 2f);
                if (preferredHigher > upperBoundary)
                    preferredHigher = (int)upperBoundary;

                this.interactiveAreas[0] = new Rectangle(intRect.Left, (int)preferredLower, intRect.Width, (int)Math.Abs(preferredHigher - preferredLower));
            }
            else
            {
                int preferredLower = (int)(screenCenterPosition.X - Scaler.MinimalTouchDimension / 2f);
                if (preferredLower < lowerBoundary)
                    preferredLower = (int)lowerBoundary;
                int preferredHigher = (int)(screenCenterPosition.X + Scaler.MinimalTouchDimension / 2f);
                if (preferredHigher > upperBoundary)
                    preferredHigher = (int)upperBoundary;

                this.interactiveAreas[0] = new Rectangle((int)preferredLower, intRect.Top, (int)Math.Abs(preferredHigher - preferredLower), intRect.Height);
            }
        }
    }
}
