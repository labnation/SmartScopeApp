using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface;

namespace ESuite.Drawables
{
    internal class ClippingMask : EDrawable
    {
        private Texture2D backgroundTexture;
        private Texture2D alphaOverlay;
        Rectangle innerAreaBoundaries, panoAreaBoundaries;
        Rectangle rectangleTop;
        List<Rectangle> patchRectangles = new List<Rectangle>();
        List<Rectangle> alphaCoordRectangles = new List<Rectangle>();
        Dictionary<Graph, Rectangle> graphRectangles = new Dictionary<Graph, Rectangle>();
        Matrix centerAreaWorldMatrix = Matrix.Identity;
        Matrix topAreaWorldMatrix = Matrix.Identity;
        bool draggingBarBetweenGraphs = false;
        private GraphManager gm;
        private Panorama panorama;
        private SplitPanel measurementGraphPanel;

        public ClippingMask(GraphManager gm, Panorama panorama, SplitPanel measurementGraphPanel)
            : base()
        {
            this.supportedGestures = GestureType.FreeDrag | GestureType.DragComplete | GestureType.Tap | GestureType.DoubleTap;

            this.gm = gm;
            this.panorama = panorama;
            this.measurementGraphPanel = measurementGraphPanel;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            backgroundTexture = EDrawable.LoadPng("background-texture");
            alphaOverlay = EDrawable.LoadPng("alpha");
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw covering images, because the graph is sticking out of its render area
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.LinearWrap, DepthStencilState.Default, RasterizerState.CullNone);

            //use the same screen coordinates also as sampling locations inside the image, so no borders are visible on the borders between the different patches!
            foreach (Rectangle rect in patchRectangles)
                spriteBatch.Draw(backgroundTexture, rect, rect, MappedColor.ClippingMaskOverlay.C());

            spriteBatch.End();

            //need to rebegin spritebatch, as otherwise the texture wrapping would give undesired results
            spriteBatch.Begin();
            for (int i = 0; i < patchRectangles.Count; i++)
                spriteBatch.Draw(alphaOverlay, patchRectangles[i], alphaCoordRectangles[i], new Color(1, 1, 1, 0.3f));
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            panoAreaBoundaries = panorama.Boundaries;
            innerAreaBoundaries = gm.InnerSectionRectangle;

            if (((GraphManager)parent).ActiveGraphs.Count == 0) return;

            List<Graph> graphs = ((GraphManager)parent).ActiveGraphs;
            graphRectangles.Clear();
            foreach (Graph graph in graphs)
                this.graphRectangles.Add(graph, graph.Boundaries);

            List<Graph> measurementGraphs = measurementGraphPanel.Panels.Where(x => x is Graph).Cast<Graph>().ToList();

            patchRectangles.Clear();
            //left border
            patchRectangles.Add(new Rectangle(Boundaries.X, Boundaries.Top, innerAreaBoundaries.Left - Boundaries.Left, Boundaries.Height));
            //right border
            patchRectangles.Add(new Rectangle(innerAreaBoundaries.Right, Boundaries.Top, Boundaries.Right - innerAreaBoundaries.Right, Boundaries.Height));
            //top border, needed for detecting double-taps for opening panorama
            rectangleTop = new Rectangle(innerAreaBoundaries.Left, panoAreaBoundaries.Bottom, innerAreaBoundaries.Width, innerAreaBoundaries.Y - panoAreaBoundaries.Height);
            patchRectangles.Add(rectangleTop);
            //bottom border
            patchRectangles.Add(new Rectangle(innerAreaBoundaries.Left, innerAreaBoundaries.Bottom, innerAreaBoundaries.Width, Boundaries.Bottom - innerAreaBoundaries.Bottom));

            //patches between grids            
            for (int i = 1; i < graphs.Count; i++)
            {
                Rectangle grid1Rect = graphs.ElementAt(i - 1).Boundaries;
                Rectangle grid2Rect = graphs.ElementAt(i).Boundaries;
                patchRectangles.Add(new Rectangle(grid1Rect.Left, grid1Rect.Bottom, grid1Rect.Width, grid2Rect.Top - grid1Rect.Bottom));
            }

            //patch above measurement graphs
            Rectangle grid1RectBis = graphs.Last().Boundaries;
            Rectangle grid2RectBis = measurementGraphPanel.Boundaries;
            patchRectangles.Add(new Rectangle(grid1RectBis.Left, grid1RectBis.Bottom, grid1RectBis.Width, grid2RectBis.Top - grid1RectBis.Bottom));

            //patches between measurement graphs
            for (int i = 1; i < measurementGraphs.Count; i++)
            {
                Rectangle grid1RectTris = measurementGraphs.ElementAt(i - 1).Boundaries;
                Rectangle grid2RectTris = measurementGraphs.ElementAt(i).Boundaries;
                patchRectangles.Add(new Rectangle(grid1RectTris.Right, grid1RectTris.Top, grid2RectTris.Left - grid1RectTris.Right, grid1RectTris.Height));
            }

            //patches next to squared XY graph
            Graph xyGraph = graphs.Where(x => x.Grid is GridXY).SingleOrDefault();
            if (xyGraph != null)
            {
                Rectangle gridRect = xyGraph.Boundaries;
                patchRectangles.Add(new Rectangle(innerAreaBoundaries.Left, gridRect.Y, gridRect.X - innerAreaBoundaries.Left, gridRect.Height));
                patchRectangles.Add(new Rectangle(gridRect.Right, gridRect.Y, innerAreaBoundaries.Right - gridRect.Right, gridRect.Height));
            }

            alphaCoordRectangles.Clear();
            foreach (Rectangle rect in patchRectangles)
                alphaCoordRectangles.Add(new Rectangle((int)((float)rect.X / (float)Boundaries.Width * (float)alphaOverlay.Width), (int)((float)rect.Y / (float)Boundaries.Height * (float)alphaOverlay.Height), (int)((float)rect.Width / (float)Boundaries.Width * (float)alphaOverlay.Width), (int)((float)rect.Height / (float)Boundaries.Height * (float)alphaOverlay.Height)));

            this.interactiveAreas = patchRectangles;
        }

        private void OnDrag(GestureSample gesture)
        {
            //check whether bar between 2 graphs is being dragged!
            //first 4 rectangles are the regular sides, all rectangles afterwards are the patches between graphs
            for (int i = 4; i < patchRectangles.Count; i++)
            {
                Rectangle rect = patchRectangles[i];
                if (draggingBarBetweenGraphs || rect.Contains(Utils.VectorToPoint(gesture.Position)) || rect.Contains(Utils.VectorToPoint(gesture.Position - gesture.Delta)))
                {
                    float relVertPosition = (gesture.Position.Y - (float)innerAreaBoundaries.Top) / (float)innerAreaBoundaries.Height;
                    UICallbacks.AdjustMultigraphHeight(this, relVertPosition);
                    draggingBarBetweenGraphs = true;
                    return;
                }
            }

            //foreach graph: check whether gesture was moved into graph. if so: add cursor
            foreach (var kvp in graphRectangles)
            {
                Rectangle rect = kvp.Value;
                if (rect.Contains(Utils.VectorToPoint(gesture.Position)) && !rect.Contains(Utils.VectorToPoint(gesture.Position - gesture.Delta)))
                {
                    Vector2 relativeGesturePosition = new Vector2((gesture.Position.X - rect.Left) / rect.Width - .5f, .5f - (gesture.Position.Y - rect.Top) / rect.Height);
                    UICallbacks.AddCursor(this, kvp.Key.Grid, relativeGesturePosition, gesture);
                    break; //need to break out of foreach loop here, as AddCursor has potential of creating a Toast, which causes graphRectangles to be recreated
                }
            }
        }
        private void OnDoubleTap(GestureSample gesture)
        {
            if (rectangleTop.Contains(Utils.VectorToPoint(gesture.Position)))
                UICallbacks.TogglePanoramaByUser(this, null);
        }

        private void OnTap(GestureSample gesture)
        {
            UICallbacks.CloseContextMenu(this, null);
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DoubleTap:
                    draggingBarBetweenGraphs = false;
                    OnDoubleTap(gesture);
                    ReleaseGestureControl();
                    break;
                case GestureType.FreeDrag:
                    OnDrag(gesture);
                    break;
                case GestureType.DragComplete:
                    draggingBarBetweenGraphs = false;
                    ReleaseGestureControl();
                    break;
                case GestureType.Tap:
                    draggingBarBetweenGraphs = false;
                    OnTap(gesture);
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
