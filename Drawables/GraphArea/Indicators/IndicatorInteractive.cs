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
    internal class IndicatorInteractive : Indicator
    {        
        private DrawableCallback tapCallback;
        private DrawableCallback doubleTapCallback;
        private DrawableCallback holdCallback;
        protected DrawableCallback slideCallback;
        private DrawableCallback dropCallback;
        public float stickyInterval;
        public float stickyOffset;
        private float stickyRegion = 0.01f;

        public IndicatorInteractive(Grid grid, 
            string imageNotSelected, string imageSelected, string imageHighContrast, 
            MappedColor color, MappedColor textColor, 
            Location selectorSide, string centerText = null, string bottomText = null, string contentIconImageName = null)
            : base(grid, imageNotSelected, imageSelected, imageHighContrast, color, textColor, selectorSide, centerText, bottomText, contentIconImageName)
        {
            Selected = false;
        }


        public void BindTapCallback(DrawableCallbackDelegate del)
        { BindTapCallback(new DrawableCallback(del, null)); }

        public void BindTapCallback(DrawableCallback callback)
        {
            if (callback != null)
            {
                this.supportedGestures |= GestureType.Tap;
                this.supportedGestures |= GestureType.DoubleTap;
            }
            this.tapCallback = callback;
            if (this.doubleTapCallback == null)
                this.doubleTapCallback = callback;
        }

        public void BindDoubleTapCallback(DrawableCallbackDelegate del)
        { BindDoubleTapCallback(new DrawableCallback(del, null)); }

        public void BindDoubleTapCallback(DrawableCallback callback)
        {
            if (callback != null)
                this.supportedGestures |= GestureType.DoubleTap;
            this.doubleTapCallback = callback;
        }

        public void BindHoldCallback(DrawableCallbackDelegate del)
        { BindHoldCallback(new DrawableCallback(del, null)); }

        public void BindHoldCallback(DrawableCallback callback)
        {
            if(callback != null)
                this.supportedGestures |= GestureType.Hold;
            this.holdCallback = callback;
        }

        public void BindDropCallback(DrawableCallbackDelegate del)
        { BindDropCallback(new DrawableCallback(del, null)); }
        public void BindDropCallback(DrawableCallback callback)
        {
            this.dropCallback = callback;
        }

        public void BindSlideCallback(DrawableCallbackDelegate del)
        { BindSlideCallback(new DrawableCallback(del, null)); }
        public void BindSlideCallback(DrawableCallback callback)
        {
            if(callback != null)
                this.supportedGestures |= GestureType.FreeDrag | GestureType.DragComplete;
            this.slideCallback = callback;
        }

        private void OnTap(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            if(tapCallback != null)
                tapCallback.Call(this, RelativeLocationFromGesture(gesture));
        }
        private void OnDoubleTap(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            if (doubleTapCallback != null)
                doubleTapCallback.Call(this, RelativeLocationFromGesture(gesture));
            else
                OnTap(gesture);
        }
        private void OnHold(GestureSample gesture)
        {
            if (holdCallback != null)
                holdCallback.Call(this, RelativeLocationFromGesture(gesture));
        }
        private void OnDrag(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            if (slideCallback != null)
                slideCallback.Call(this, RelativeLocationFromGesture(gesture));
        }

        private void OnDrop(GestureSample gesture)//[0.5=bottom,0.5=top]
        {
            if(dropCallback != null)
                dropCallback.Call(this, RelativeLocationFromGesture(gesture));
        }


        private float RelativeLocationFromGesture(GestureSample gesture)
        {
            float location = 0f;
            if ((selectorSide == Location.Left) || (selectorSide == Location.Right))
            {
                float maxY = Boundaries.Y;
                float minY = Boundaries.Bottom;
                location = (gesture.Position.Y - minY) / (maxY - minY) - 0.5f;
            }
            else if ((selectorSide == Location.Top) || (selectorSide == Location.Bottom))
            {
                float maxX = Boundaries.Right;
                float minX = Boundaries.X;
                location = (gesture.Position.X - minX) / (maxX - minX) - 0.5f;
            }
            //Don't stick when not sticky or when location is out of grid
            if (stickyInterval == 0f || Math.Abs(location) > 0.5f)
                return location;
            float offsetLocation = location - stickyOffset;
            float distanceToStickySpot = Math.Min(Math.Abs((offsetLocation % stickyInterval) - stickyInterval), Math.Abs(offsetLocation) % stickyInterval);
            if (distanceToStickySpot < stickyRegion)
                return (float)(stickyInterval * Math.Round(offsetLocation / stickyInterval) + stickyOffset);
            return location;
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.Tap:
                    ReleaseGestureControl();
                    OnTap(gesture);
                    break;
                case GestureType.DoubleTap:
                    ReleaseGestureControl();
                    OnDoubleTap(gesture);
                    break;
                case GestureType.Hold:
                    ReleaseGestureControl();
                    OnHold(gesture);
                    break;
                case GestureType.FreeDrag:
                    OnDrag(gesture);
                    break;
                case GestureType.DragComplete:
                    OnDrop(gesture);
                    ReleaseGestureControl();
                    break;
            }
        }

        override public void RecomputePosition()
        {
            base.RecomputePosition();

            if(slideCallback != null) 
            {
                Rectangle intRect = this.interactiveAreas[0];
                if ((selectorSide == Location.Left) || (selectorSide == Location.Right)) {
                    int growSize = Math.Max(intRect.Height, Scaler.MinimalTouchDimension) - intRect.Height;
                    intRect.Height += growSize;
                    intRect.Y -= growSize / 2;                }
                else {
                    int growSize = Math.Max(intRect.Width, Scaler.MinimalTouchDimension) - intRect.Width;
                    intRect.Width += growSize;
                    intRect.X -= growSize / 2;
                }
                this.interactiveAreas[0] = intRect;
            }
        }
    }
}
