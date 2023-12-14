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
    internal delegate void PositionChangedDelegate();

    internal abstract class Cursor : EDrawableVertices
    {
        protected VertexBuffer vertexBuffer;
        public event PositionChangedDelegate OnPositionChanged;

        protected MappedColor graphColor;
        protected MappedColor overlayColor;

        protected SpriteFont sideFont;
        protected SpriteFont centerFont;
        protected SpriteFont bottomFont;
        protected abstract string sideText { get; }
        protected abstract string centerText { get; }
        protected abstract string bottomText { get; }
        protected abstract string topText { get; }        
        protected abstract List<VertexPositionColor> vertexList { get; }
        protected abstract Vector2 sideTextPosition { get; }
        private Vector2 centerTextPosition;
        private Vector2 bottomTextPosition;
        private Vector2 topTextPosition;
        public Point Center { get { return indicatorRectangle.Center; } }

        protected Texture2D cursorTexture;
        protected Vector2 touchCenter = new Vector2();
        protected Rectangle indicatorRectangle = new Rectangle();
        protected Rectangle touchSensitiveRectangle = new Rectangle();

        public double Value { get; protected set; }
		protected virtual string unit { get; set; }
        public double Precision { get; protected set; }

        private bool dragging = false;
        public Vector2 Location { get; protected set; }
        internal Grid Grid { get; private set; }        

        public Cursor(Grid grid, Vector2 location, double precision, MappedColor graphColor)
            : base()
        {
            this.Grid = grid;
            this.graphColor = graphColor;
            this.Precision = precision;
            this.Location = location;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
            this.supportedGestures = GestureType.FreeDrag | GestureType.DragComplete | GestureType.Tap | GestureType.DoubleTap;
            this.overlayColor = MappedColor.CursorOverlay;

            grid.OnBoundariesChangedDelegate += this.SetBoundaries;

            this.Boundaries = grid.Boundaries;
            MustRecalcNextUpdateCycle = true;

            //FIXME: this requires the content to be loaded immediately, as it is required to calc the interactionRectangle.
            //in case the interactionRectangle is not set from the beginning, the drag operation is ended
            //therefore, this is now calling LoadContent from its constructor.
            LoadContent();
        }
        
        protected override void LoadContentInternal()
        {
            cursorTexture = LoadPng(Scaler.ScaledImageName("widget-cursor-full"));
            sideFont = content.Load<SpriteFont>(Scaler.GetFontCursorSide());

            centerFont = content.Load<SpriteFont>(Scaler.GetFontCursorCenter());
            bottomFont = content.Load<SpriteFont>(Scaler.GetFontCursorBottom());
        }

        protected override void DrawInternal(GameTime time)
        {
            //now draw the line
            if (vertexBuffer == null) return;

            effect.World = localWorld;
            effect.View = this.View;
            effect.Projection = this.Projection;

            device.RasterizerState = RasterizerState.CullNone;
            device.SetVertexBuffer(vertexBuffer);

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawPrimitives(PrimitiveType.LineList, 0, 2);                
            }

            //draw the indicator
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            Color indicatorColor = graphColor.C();
            indicatorColor.A = 255;
            indicatorColor.R = (byte)(indicatorColor.R * overlayColor.C().R/255);
            indicatorColor.G = (byte)(indicatorColor.G * overlayColor.C().G/255);
            indicatorColor.B = (byte)(indicatorColor.B * overlayColor.C().B/255);
            spriteBatch.Draw(cursorTexture, indicatorRectangle, indicatorColor);            

			spriteBatch.End(); 
			spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            if (dragging)
                spriteBatch.DrawString(sideFont, sideText, sideTextPosition, indicatorColor);
            else
            {
                spriteBatch.DrawString(centerFont, centerText, centerTextPosition, Color.White);
                spriteBatch.DrawString(bottomFont, bottomText, bottomTextPosition, Color.White);
                spriteBatch.DrawString(bottomFont, topText, topTextPosition, Color.White); 
            }
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            SetLocation(this.Location);
        }

        public virtual void SetLocation(Vector2 location)
        {
            if (sideFont == null) return;
            this.Location = location;

            //find center of touchrectangle = crossing point with cursor
            touchCenter.X = Boundaries.X + (float)Boundaries.Width * (location.X + 0.5f);
            touchCenter.Y = Boundaries.Bottom - (float)Boundaries.Height * (location.Y + 0.5f);

            //define drawable touch rectangle
            int touchRectangleSize = cursorTexture.Width;
            indicatorRectangle = new Rectangle((int)(touchCenter.X - touchRectangleSize / 2), (int)(touchCenter.Y - touchRectangleSize / 2), touchRectangleSize, touchRectangleSize);

            int touchSensitiveSize = (int)((float)touchRectangleSize * 1.8f);
            this.touchSensitiveRectangle = new Rectangle((int)(touchCenter.X - touchSensitiveSize / 2), (int)(touchCenter.Y - touchSensitiveSize / 2), touchSensitiveSize, touchSensitiveSize);
            this.interactiveAreas[0] = touchSensitiveRectangle;

            if (vertexBuffer == null)
                vertexBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, 4, BufferUsage.WriteOnly);
            var l = vertexList.ToArray();
			if(l != null && l.Count() > 0)
            	vertexBuffer.SetData<VertexPositionColor>(l, 0, l.Length);

            //Calculate positions for accopagnying text and value
            Vector2 centerTextSize = centerFont.MeasureString(centerText);
            Vector2 bottomTextSize = bottomFont.MeasureString(bottomText);
            Vector2 topTextSize = bottomFont.MeasureString(topText);

            float bottomTextBaseline = indicatorRectangle.Height * 7 / 8;
            float topTextBaseline = indicatorRectangle.Height * 5 / 16;
            centerTextPosition = new Vector2((int)Math.Round(indicatorRectangle.Center.X - centerTextSize.X / 2), (int)Math.Round(indicatorRectangle.Center.Y - centerTextSize.Y / 2));
            if (bottomText != null && bottomText != "")
                centerTextPosition.Y = (int)Math.Round(centerTextPosition.Y - bottomTextSize.Y / 4);
            bottomTextPosition = new Vector2((int)Math.Round(indicatorRectangle.Center.X - bottomTextSize.X / 2), (int)Math.Round(indicatorRectangle.Y + bottomTextBaseline - bottomTextSize.Y));
            topTextPosition = new Vector2((int)Math.Round(indicatorRectangle.Center.X - topTextSize.X / 2), (int)Math.Round(indicatorRectangle.Y + topTextBaseline - bottomTextSize.Y));

            if (OnPositionChanged != null)
                OnPositionChanged();
        }

        /// <summary>
        /// method called whenever the cursorIndicator is dragged.Causes line and indicator to follow touch location. Removes cursor when indicator is moved out of the grid area.
        /// </summary>
        /// <param name="gesture">Gesture, containing touch location</param>
        protected virtual void OnDrag(GestureSample gesture)
        {
            //transform from screen -> local/relative coordinates
            float maxY = Boundaries.Y;
            float minY = Boundaries.Bottom;
            float minX = Boundaries.X;
            float maxX = Boundaries.Right;
            Vector2 location = new Vector2((gesture.Position.X - minX) / (maxX - minX) - 0.5f, (gesture.Position.Y - minY) / (maxY - minY) - 0.5f);

            //remove cursor when out of bounds
            bool outOfBounds = false;
            if (location.X < -0.5f) outOfBounds = true;
            if (location.Y < -0.5f) outOfBounds = true;
            if (location.X > 0.5f) outOfBounds = true;
            if (location.Y > 0.5f) outOfBounds = true;
            if (outOfBounds)
            {
                UICallbacks.RemoveCursor(this);
                this.ReleaseGestureControl();
            }

            this.SetLocation(location);

            UICallbacks.CloseContextMenu(this, false);
        }

        protected virtual void OnTap(GestureSample gesture)
        {
            UICallbacks.CursorTapped(this);
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    OnTap(gesture);
                    ReleaseGestureControl();
                    break;                
                case GestureType.FreeDrag:
                    dragging = true;
                    OnDrag(gesture);
                    break;
                case GestureType.DragComplete:
                    dragging = false;
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
