using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;
using LabNation.Common;

namespace ESuite.Drawables
{
    internal class PanoramaShading : EDrawableVertices
    {        
        private VertexPositionColor[] coverVertices;
        private Panorama panorama;
        private float triggerLocation = 0f;
        public bool TriggerVisible { get; set; }
        private Rectangle triggerRectangle;
        private Rectangle viewportRectangle;
        private bool draggingTrigger, draggingViewport;
        public bool Opening = false;

		public DrawableCallback OnDoubleTap;
        public DrawableCallback OnScaleViewport;
		public DrawableCallback OnScalePanorama;

        private bool pinchingViewport = false;
		private bool pinchingAcquisitionBuffer = false;
        private Vector2 pinchPreviousPosition1;
        private Vector2 pinchPreviousPosition2;

        private float _coverLeftOffset = 0f;
        internal float CoverLeftOffset { 
            get { return _coverLeftOffset; } 
            set { if (_coverLeftOffset != value) redrawRequest = true; _coverLeftOffset = value; if (this.Boundaries.Height > 0) { DefineShadingVertices(); } } 
        }
        private float _coverRightOffset;
        internal float CoverRightOffset { 
            get { return _coverRightOffset; }
            set { if (_coverRightOffset != value) redrawRequest = true; _coverRightOffset = value; if (this.OpeningProgress > 0) { DefineShadingVertices(); } } 
        }

        private float _acquistionFetchProgress;
        internal float AcquisitionFetchProgress {
            get { return _acquistionFetchProgress; }
            set {
                if (_acquistionFetchProgress != value)
                    redrawRequest = true;
                _acquistionFetchProgress = value;
                DefineShadingVertices();
            }
        }
        internal float OpeningProgress { get; set; }
        private SplitPanel splitPanel;

        public PanoramaShading(Panorama panorama, SplitPanel splitPanel)
            : base()
        {
            this.panorama = panorama;
            this.splitPanel = splitPanel;

            CoverLeftOffset = 0f;
            CoverRightOffset = 1f;

            this.supportedGestures = GestureType.FreeDrag | GestureType.DragComplete | GestureType.Pinch | GestureType.PinchComplete | GestureType.DoubleTap | GestureType.Tap | GestureType.MouseScroll;
            this.interactiveAreas = new List<Rectangle>() { new Rectangle(), new Rectangle() };

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw border
            effect.World = localWorld;
            effect.View = this.View;
            effect.Projection = this.Projection;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, coverVertices, 0, coverVertices.Length/3);
            }

            if (TriggerVisible)
            {
                spriteBatch.Begin();
                spriteBatch.Draw(whiteTexture, triggerRectangle, MappedColor.PanoramaTriggerIndicator.C());
                spriteBatch.End();
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            //Boundaries have to be the boundaries of the analog graph!
            DefineShadingVertices();
        }

        private void DefineShadingVertices()
        {
            //funky stuff to get panorama Y coords expressed in the grid's matix system
            Rectangle panoramaRectangle = panorama.Boundaries;
            Vector3 panoramaTopLeftScreenPos = new Vector3(panoramaRectangle.Left, panoramaRectangle.Top, 1);
            Vector3 panoramaTopLeftPos = device.Viewport.Unproject(panoramaTopLeftScreenPos, Projection, View, localWorld);
            Vector3 panoramaBottomRightScreenPos = new Vector3(panoramaRectangle.Right, panoramaRectangle.Bottom, 1);
            Vector3 panoramaBottomRightPos = device.Viewport.Unproject(panoramaBottomRightScreenPos, Projection, View, localWorld);
            float? transitionProgress = splitPanel.TransitionProgress(0);
            if (transitionProgress != null)
            {
                if (Opening)
                    this.OpeningProgress = transitionProgress.Value;
                else
                    this.OpeningProgress = 1f-transitionProgress.Value;
            }

            List<VertexPositionColor> coverVertexList = new List<VertexPositionColor>();
            Color barShadingColor = MappedColor.PanoramaShading.C();

            /*
             * ,-------------------------------.
             * |10000000|6666666666667|32222222|
             * |11110000|6666666777777|33332222|
             * |11111110|6777777777777|33333332|
             * `-------------------------------`
             *          44444444444445
             *       444444445555555555555
             *    4444555555555555555555555555
             * 455555555555555555555555555555555
             * ---------------------------------
             * (0,0,0)                   (1,0,0)
             */
            //left side
            //Triangle 0
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaBottomRightPos.Y, 0), barShadingColor));
            //Triangle 1
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaBottomRightPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaBottomRightPos.Y, 0), barShadingColor));

            //right side
            //Triangle 2
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(1, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(1, panoramaBottomRightPos.Y, 0), barShadingColor));
            //Triangle 3
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaTopLeftPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(1, panoramaBottomRightPos.Y, 0), barShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaBottomRightPos.Y, 0), barShadingColor));

            //zoom shades
            Color zoomShadingColor = Color.FromNonPremultiplied(255, 255, 255, 50)*OpeningProgress;

            //Triangle 4
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));
            //Triangle 5
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), zoomShadingColor));
            //6
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaTopLeftPos.Y, 0), zoomShadingColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaTopLeftPos.Y, 0), zoomShadingColor));
            //7
			coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaTopLeftPos.Y, 0), zoomShadingColor));
			coverVertexList.Add(new VertexPositionColor(new Vector3(CoverRightOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));
			coverVertexList.Add(new VertexPositionColor(new Vector3(CoverLeftOffset, panoramaBottomRightPos.Y, 0), zoomShadingColor));

            /*
             * ,-------------------------------.
             * |1222222222222222222222222222222|
             * |1111111111111111222222222222222|
             * |1111111111111111111111111111112|
             * `-------------------------------`
             *          44444444444445
             *       444444445555555555555
             *    4444555555555555555555555555
             * 455555555555555555555555555555555
             * ---------------------------------
             * (0,0,0)                   (1,0,0)
             */

            Color progressColor = MappedColor.AcquisitionFetchProgress.C();
            //Triangle 1
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaTopLeftPos.Y, 0), progressColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(AcquisitionFetchProgress, panoramaBottomRightPos.Y, 0), progressColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaBottomRightPos.Y, 0), progressColor));
            //Triangle 2
            coverVertexList.Add(new VertexPositionColor(new Vector3(AcquisitionFetchProgress, panoramaBottomRightPos.Y, 0), progressColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(0, panoramaTopLeftPos.Y, 0), progressColor));
            coverVertexList.Add(new VertexPositionColor(new Vector3(AcquisitionFetchProgress, panoramaTopLeftPos.Y, 0), progressColor));
            
            coverVertices = coverVertexList.ToArray();

            UpdateTrigger(this.triggerLocation);

            if (this.interactiveAreas == null) return;
            this.interactiveAreas[0] = panoramaRectangle;

            Vector3 ViewportTopLeft = new Vector3(CoverLeftOffset, panoramaTopLeftPos.Y, 0);
            Vector3 ViewportBottomRight = new Vector3(CoverRightOffset, panoramaBottomRightPos.Y, 0);
            Vector3 ViewportTopLeftScreen = device.Viewport.Project(ViewportTopLeft, Projection, View, localWorld);
            Vector3 ViewportBottomRightScreen = device.Viewport.Project(ViewportBottomRight, Projection, View, localWorld);
            Vector3 ViewportSize = ViewportBottomRightScreen - ViewportTopLeftScreen;

            if (ViewportSize.X < Scaler.MinimalTouchDimension)
            {
                ViewportTopLeftScreen.X -= (Scaler.MinimalTouchDimension / 2f - ViewportSize.X / 2f);
                ViewportSize.X = Scaler.MinimalTouchDimension;
            }
            viewportRectangle = new Rectangle((int)ViewportTopLeftScreen.X, (int)ViewportTopLeftScreen.Y, (int)ViewportSize.X, (int)ViewportSize.Y);

            if (viewportRectangle.Right > panoramaRectangle.Right)
                viewportRectangle.X -= (viewportRectangle.Right - panoramaRectangle.Right);
            if (viewportRectangle.Left < panoramaRectangle.Left)
                viewportRectangle.X -= (viewportRectangle.Left - panoramaRectangle.Left);

            //Grow it just a bit so we can grab it more easily
            viewportRectangle.X -= Scaler.MinimalTouchDimension / 2;
            viewportRectangle.Width += Scaler.MinimalTouchDimension;
            
            interactiveAreas[1] = viewportRectangle;
        }        

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.MouseScroll:
                    float xRatio = 0;
                    if (gesture.Delta.Y > 0) //mouseScroll UP
                        xRatio = 2f;
                    else //mouseScroll DOWN
                        xRatio = 0.5f;
                    Vector3 positionWSC = device.Viewport.Unproject(new Vector3(gesture.Position, 1), Matrix.Identity, View, localWorld);
                    object[] zoomArgArray = new object[4];
                    zoomArgArray[0] = xRatio;
                    zoomArgArray[1] = 0f;
                    zoomArgArray[2] = positionWSC.X - 0.5f;			    
                    zoomArgArray[3] = true;
                	OnScaleViewport.Call(this, zoomArgArray);

                    ReleaseGestureControl();
                    break;
            	case GestureType.DoubleTap:
            		OnDoubleTap.Call(this);
                    ReleaseGestureControl();
            		break;
                case GestureType.FreeDrag:
                    Vector3 pinchCenterWSC = device.Viewport.Unproject(new Vector3(gesture.Position, 1), Matrix.Identity, View, localWorld);
                    Vector3 deltaWorldSpaceCoords = device.Viewport.Unproject(new Vector3(gesture.Delta, 1), Matrix.Identity, View, Utils.RemoveTranslation(localWorld));
                    if ((viewportRectangle.Contains(gesture.Position) || draggingViewport) && !draggingTrigger)
                    {
                        draggingViewport = true;
                        OnScaleViewport.Call(this, new object[] { 1f, deltaWorldSpaceCoords.X, pinchCenterWSC.X - 0.5f, false } );
                    }
                    else
                    {
                        draggingTrigger = true;
                        OnScalePanorama.Call(this, new object[] { 1f, deltaWorldSpaceCoords.X, pinchCenterWSC.X - 0.5f, false });
                    }
                    break;
                case GestureType.DragComplete:
                    draggingTrigger = false;
                    draggingViewport = false;
                    ReleaseGestureControl();
                    break;
                case GestureType.Pinch:
                    OnPinch(gesture);
                    break;
                case GestureType.PinchComplete:
                    pinchingViewport = false;
                    pinchingAcquisitionBuffer = false;
                    ReleaseGestureControl();
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }       

        private void OnPinch(GestureSample gesture)
        {
            if (!pinchingViewport && !pinchingAcquisitionBuffer) //start of pinch gesture
            {
				if(
					viewportRectangle.Contains(Utils.VectorToPoint(gesture.Position)) && 
					viewportRectangle.Contains(Utils.VectorToPoint(gesture.Position2))
				)
					pinchingViewport = true;
				else
					pinchingAcquisitionBuffer = true;

                pinchPreviousPosition1 = gesture.Position;
                pinchPreviousPosition2 = gesture.Position2;
            }
            else
            {
                Vector2 previousDistance = pinchPreviousPosition1 - pinchPreviousPosition2;
                Vector2 currentDistance = gesture.Position - gesture.Position2;

                //protection against going too small, which would result in infinite values
                float minimalDistance = Scaler.MinimalPinchSize;
                if (Math.Abs(currentDistance.X) < minimalDistance)
                    currentDistance.X = 1;
                
                //when too small: set previous to current, so ratios are 1 AND in case of smallstart ratio doesn't blow up (as would happen when just set to 1)
                if (Math.Abs(previousDistance.X) < minimalDistance)
                    previousDistance.X = currentDistance.X;
                
                float ratioX = 1f;                
                if (Math.Abs(currentDistance.X) >= minimalDistance)
                    ratioX = currentDistance.X/previousDistance.X;

                //protection against flipping the waveforms
                bool pinchedThroughMirror = false;
                if (ratioX < 0) pinchedThroughMirror = true;

                if (pinchedThroughMirror)
                    return;

				Vector2 previousPinchCenter = (pinchPreviousPosition1 + pinchPreviousPosition2) / 2;
				Vector3 previousPinchCenterWSC = device.Viewport.Unproject(new Vector3(previousPinchCenter, 1), Matrix.Identity, View, localWorld);
                Vector2 pinchCenter = (gesture.Position + gesture.Position2) / 2;
                Vector3 pinchCenterWSC = device.Viewport.Unproject(new Vector3(pinchCenter, 1), Matrix.Identity, View, localWorld);
				
				Vector3 translationWSC = pinchCenterWSC - previousPinchCenterWSC;

                bool wasMouseScroll = false;

                object[] zoomArgArray = new object[4];
                zoomArgArray[0] = ratioX;
                zoomArgArray[1] = translationWSC.X;
				zoomArgArray[2] = pinchCenterWSC.X - 0.5f;
                zoomArgArray[3] = wasMouseScroll;

                //if(pinchingViewport)
                	OnScaleViewport.Call(this, zoomArgArray);
               // else //pinchingAcquisitionBuffer
					//OnScalePanorama.Call(this, zoomArgArray);

                //prep for next cycle
                pinchPreviousPosition1 = gesture.Position;
                pinchPreviousPosition2 = gesture.Position2;
            }
        }

        public void UpdateTrigger(float relativeLocation)
        {
            triggerLocation = relativeLocation;
            Rectangle panoramaRectangle = panorama.Boundaries;
            Vector3 triggerOffset = device.Viewport.Project(new Vector3(relativeLocation, 0, 0), Projection, View, localWorld);
            triggerRectangle = new Rectangle((int)triggerOffset.X, panoramaRectangle.Y, 1, panoramaRectangle.Height - 1);
        }
    }
}
