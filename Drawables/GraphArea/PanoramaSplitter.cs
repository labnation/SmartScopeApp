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
    public enum MeasurementBoxMode { Floating, DockedRight, DockedBottom }

    internal class PanoramaSplitter : EDrawable
    {
        private SplitPanel verSplitPanel;
        private SplitPanel panoHorSplitPanel;
        private SplitPanel mainHorSplitPanel;
        private GraphManager gm;
        private Panorama panorama;
        public Panorama Panorama { get { return this.panorama; } }
        public PanoramaShading PanoramaShading { get; private set; }
        internal MeasurementBox MeasurementBox { get; private set; }
        private MeasurementBoxMode measurementBoxMode = MeasurementBoxMode.Floating;

        public bool PanoramaShown { get; private set; }

        public PanoramaSplitter() : base()
        {            
            panorama = new Drawables.Panorama();

            LoadContent();
        }

        public void Init(GraphManager gm)
        {
            this.gm = gm;

            panoHorSplitPanel = new SplitPanel(Orientation.Horizontal, 3, SizeType.Inches);
            panoHorSplitPanel.SetPanel(1, panorama);            

            verSplitPanel = new Drawables.SplitPanel(Orientation.Vertical, 2, SizeType.Relative);
            verSplitPanel.SetPanel(0, panoHorSplitPanel);
            verSplitPanel.SetPanel(1, gm);
            verSplitPanel.SetHighestDrawPriority(panoHorSplitPanel); //do this way; as cannot use DrawOrder.BackWard because verSplitPanel also contains bottom MBox which would be clipped by GM's ClippingPanel
            verSplitPanel.OnUpdateComplete += gm.GraphBlocker.OnBoundariesChanged;

            mainHorSplitPanel = new Drawables.SplitPanel(Orientation.Horizontal, 1, SizeType.Inches);
            mainHorSplitPanel.SetPanel(0, verSplitPanel);
            AddChild(mainHorSplitPanel);

            PanoramaShading = new PanoramaShading(panorama, verSplitPanel);
            AddChild(PanoramaShading);
            gm.Graphs[GraphType.Analog].OnBoundariesChangedDelegate += PanoramaShading.SetBoundaries;
            gm.MeasurementGraphPanel.OnBoundariesChangedDelegate += delegate (Rectangle r) { gm.GraphBlocker.SetBoundaries(this.Boundaries); };

            MeasurementBox = new Drawables.MeasurementBox(this);
            AddChild(MeasurementBox);
        }

        public void ShowPanorama(bool show)
        {
            if (this.PanoramaShown == show) return;            

            this.PanoramaShown = show;
            this.PanoramaShading.Opening = show;

            CalcVerSplitterSpacing(ColorMapper.AnimationTime);
        }

        private void CalcVerSplitterSpacing(float animTime)
        {
            if (Boundaries.Height == 0) //when not yet fully initialized
                return;

            float remSpacing = 1;
            float boxHeight = 0;
            float panoHeight = 0;

            if (measurementBoxMode == MeasurementBoxMode.DockedBottom)
            {
                if (Boundaries.Height != 0)
                {
                    boxHeight = (float)MeasurementBox.Height / (float)Boundaries.Height;
                    remSpacing -= boxHeight;
                    verSplitPanel.SetPanelSize(2, boxHeight);
                }
            }

            if (PanoramaShown)
            {
                //calc pano height
                int fullGraphHeightPixels = gm.InnerSectionRectangle.Height;
                if (fullGraphHeightPixels < 0) fullGraphHeightPixels = 0; //when not yet fully initialized
                float singleDivisionHeight = (float)fullGraphHeightPixels / (float)Grid.DivisionsVerticalMax;
                panoHeight = singleDivisionHeight / (float)Boundaries.Height;                
                remSpacing -= panoHeight;
            }

            if (!(float.IsNaN(panoHeight) || float.IsNegativeInfinity(panoHeight) || float.IsPositiveInfinity(panoHeight) || (panoHeight < 0)))
                verSplitPanel.SetPanelSize(0, panoHeight, animTime);
            if (!(float.IsNaN(remSpacing) || float.IsNegativeInfinity(remSpacing) || float.IsPositiveInfinity(remSpacing) || (remSpacing < 0)))
                verSplitPanel.SetPanelSize(1, remSpacing, animTime);            
        }

        public void OnMeasurementBoxHeightUpdated()
        {
            CalcVerSplitterSpacing(0);
        }

        public void OnMeasurementBoxWidthUpdated()
        {
            if (measurementBoxMode == MeasurementBoxMode.DockedRight)
                mainHorSplitPanel.SetPanelSize(1, Scaler.PixelsToInches(MeasurementBox.Width));
        }

        public void SetMeasurementBoxMode(MeasurementBoxMode mode)
        {
            if (this.measurementBoxMode == mode)
                return;
            this.measurementBoxMode = mode;

            if (mode == Drawables.MeasurementBoxMode.Floating)
            {
                verSplitPanel.RedefineNumberOfPanels(2);
                mainHorSplitPanel.RedefineNumberOfPanels(1);
                this.AddChild(MeasurementBox);
            }
            else if (mode == Drawables.MeasurementBoxMode.DockedBottom)
            {
                this.RemoveChild(MeasurementBox);
                verSplitPanel.RedefineNumberOfPanels(3);
                verSplitPanel.SetPanel(2, MeasurementBox);
            }
            else if (mode == Drawables.MeasurementBoxMode.DockedRight)
            {
                this.RemoveChild(MeasurementBox);
                mainHorSplitPanel.RedefineNumberOfPanels(2);
                mainHorSplitPanel.SetPanel(1, MeasurementBox);
            }

            OnBoundariesChangedInternal(); //to correctly define the boundaries of the mbox
            MeasurementBox.SetMode(mode);                

            CalcVerSplitterSpacing(0);
            OnMeasurementBoxWidthUpdated();
        }

        protected override void LoadContentInternal()
        {
        }

        protected override void DrawInternal(GameTime time)
        {
        }

        protected override void OnBoundariesChangedInternal()
        {
            panoHorSplitPanel.SetPanelSize(0, Scaler.PixelsToInches(GraphManager.BorderSizePx));
            panoHorSplitPanel.SetPanelSize(2, Scaler.PixelsToInches(GraphManager.BorderSizePx));
            mainHorSplitPanel.SetBoundaries(this.Boundaries);

            gm.GraphBlocker.SetBoundaries(this.Boundaries);

            if (measurementBoxMode == MeasurementBoxMode.Floating)
                MeasurementBox.SetBoundaries(this.Boundaries); //this is not the position of the mbox; only the boundaries where the mbox should stay within

            CalcVerSplitterSpacing(0); //needed, as measurmentbox height is in absolute pixels, while vertSplitPanel is in %
        }
    }
}
