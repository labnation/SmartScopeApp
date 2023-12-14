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
using ESuite.Measurements;

namespace ESuite.Drawables
{
    internal enum GraphType { Analog, Digital, Frequency, XY, Measurements }
    internal enum AnalogGraphCombo { Analog, AnalogXY, AnalogFFT, AnalogMeasurements }

    internal class GraphManager : EDrawable
    {
        private const float intergraphBorderSpacing = 0.05f;

        private Dictionary<GraphType, Graph> graphs = new Dictionary<GraphType, Graph>();
        private List<Graph> activeGraphs = new List<Graph>();
        internal ClippingMask GraphBlocker { get; private set; }
        internal SplitPanel graphSplitPanel;
        internal Rectangle InnerSectionRectangle { get; private set; }        
        internal static int BorderSizePx { get; private set; }
        internal Dictionary<GraphType, Graph> Graphs { get { return graphs; } }
        internal List<Graph> ActiveGraphs { get { return activeGraphs; } }
        internal IndicatorInteractive SelectorTriggerHorPos { get; private set; }
        internal IndicatorInteractive SelectorTriggerVerPos { get; private set; }
        internal List<Cursor> Cursors { get { return this.graphSplitPanel.children.Where(x => x is Cursor).Cast<Cursor>().ToList<Cursor>(); } }
        internal GridLabelPrinter LabelPrinter { get; private set; }        
        private Panel parkedIndicatorPanel;
        public SplitPanel MeasurementGraphPanel { get; private set; }
        private Panorama panorama;

        public GraphManager(float minTimeDiv, float maxTimeDiv, Panorama panorama)
            : base()
        {
            this.panorama = panorama;

            Graph analogGraph = new Graph(new GridAnalog(this), this, panorama, minTimeDiv, maxTimeDiv);
            graphs.Add(GraphType.Analog, analogGraph);
            
            Graph digitalGraph = new Graph(new GridDigital(this), this, panorama, minTimeDiv, maxTimeDiv);
            graphs.Add(GraphType.Digital, digitalGraph);
            
            //add freqGraph and XY at this point, because
            // - its contents is clipped within its Boundaries
            // - it needs to be drawn over cursor spokes
            Graph frequencyGraph = new Graph(new GridFrequency(this), this, panorama, 0, 0);
            graphs.Add(GraphType.Frequency, frequencyGraph);
            
            Graph xyGraph = new Graph(new GridXY(this), this, panorama, 0, 0);
            graphs.Add(GraphType.XY, xyGraph);

            MeasurementGraphPanel = new Drawables.SplitPanel(Orientation.Horizontal, 0, SizeType.Relative);
            
            graphSplitPanel = new SplitPanel(Orientation.Vertical, 9, SizeType.Relative);
            graphSplitPanel.SetPanel((int)GraphType.Analog*2, analogGraph);
            graphSplitPanel.SetPanel((int)GraphType.Digital * 2, digitalGraph);
            graphSplitPanel.SetPanel((int)GraphType.Frequency * 2, frequencyGraph);
            graphSplitPanel.SetPanel((int)GraphType.XY * 2, xyGraph);
            graphSplitPanel.SetPanel((int)GraphType.Measurements * 2, MeasurementGraphPanel);
            AddChild(graphSplitPanel);

            GraphBlocker = new ClippingMask(this, panorama, MeasurementGraphPanel);
            AddChild(GraphBlocker);

            graphSplitPanel.OnUpdateComplete += GraphBlocker.OnBoundariesChanged;

            SelectorTriggerHorPos = new IndicatorInteractive(analogGraph.Grid, 
                "widget-indicator-empty", "widget-indicator-empty", "widget-indicator-hilite", 
                MappedColor.Neutral, MappedColor.Neutral, Location.Top);
            AddChild(SelectorTriggerHorPos);

            SelectorTriggerVerPos = new IndicatorInteractive(analogGraph.Grid, 
                "widget-indicator-empty", "widget-indicator-empty", 
                "widget-indicator-hilite", MappedColor.Neutral, MappedColor.Neutral, 
                Location.Right);
            AddChild(SelectorTriggerVerPos);            

            parkedIndicatorPanel = new Panel()
            {
                background = MappedColor.Transparent,
                margin = Scaler.ButtonMargin,
                direction = Direction.Backward,
            };
            AddChild(parkedIndicatorPanel);                        

            LabelPrinter = new Drawables.GridLabelPrinter();
            AddChild(LabelPrinter);

            //do this way as they need to be added as very last items (to be drawn on top of all other items)
            foreach (var kvp in graphs)
                AddChild(kvp.Value.SDivLabel);

            LoadContent();
        }

        public Graph AddMeasurementGraph(StochasticMeasurement m)
        {
            //create new graph
            Graph newGraph = new Graph(new GridMeasurement(this, m.Unit), this, panorama, 0, 0);

            //disable, as this could lead to some undesired behaviour of removing multiple graphs in one movement
            /*if (m is ChannelMeasurement)
            {
                ChannelMeasurement cm = m as ChannelMeasurement;
                newGraph.Grid.PinchDragCallback = new DrawableCallback(UICallbacks.ShowChannelMeasurementInGraph, new object[] { cm.GetType(), cm.Channel, false});
            } */           

            //add to measurementPanel
            //measurementPanel contains elements in this order:             0       1       2       3       4
            //                                                              Graph   Border  Graph   Border  Graph
            int nrGraphs = (MeasurementGraphPanel.Panels.Length+1)/2;
            MeasurementGraphPanel.RedefineNumberOfPanels((nrGraphs+1)*2-1);
            MeasurementGraphPanel.SetPanel((nrGraphs * 2), newGraph);

            ResizeAllMeasurementGraphs();

            return newGraph;
        }

        private void ResizeAllMeasurementGraphs()
        {
            float measurementBorderSize = intergraphBorderSpacing / 2f;

            //resize all measurement graphs in measurementPanel
            int nrGraphs = (MeasurementGraphPanel.Panels.Length + 1) / 2;
            float eachPanelSize = (1f - (float)(nrGraphs - 1f) * measurementBorderSize) / (float)(nrGraphs);
            for (int i = 0; i < nrGraphs; i++)
            {
                MeasurementGraphPanel.SetPanelSize(i * 2, eachPanelSize, ColorMapper.AnimationTime);
                if (i * 2 + 1 < nrGraphs * 2 - 1)
                    MeasurementGraphPanel.SetPanelSize(i * 2 + 1, measurementBorderSize, ColorMapper.AnimationTime);

                //make sure number of horizontal divisions is decreased according to number of horizontal graphs
                (MeasurementGraphPanel.Panels[i * 2] as Graph).Grid.GraphsSharingHorizontalSpace = nrGraphs;
            }
        }

        public void RemoveMeasurementGraph(Graph graph)
        {
            LabelPrinter.RemoveGridLabelDefinitions(graph.Grid);
            int panelNumber = Array.FindIndex(MeasurementGraphPanel.Panels, w => w == graph);

            //remove graph and its boundary
            MeasurementGraphPanel.RemovePanel(panelNumber);
            MeasurementGraphPanel.RemovePanel(panelNumber);

            ResizeAllMeasurementGraphs();
        }

        public void AddIndicator(Indicator ind)
        {
            if (!children.Contains(ind))
                AddChild(ind);
        }

        public void RemoveIndicator(Indicator ind)
        {
            if (children.Contains(ind))
                RemoveChild(ind);
        }

        public void AddDivLabel(GridDivisionLabel l)
        {
            //griddivs are children of this GM, so they're drawn on top of anything inside the Graphs
            if (!children.Contains(l))
                AddChild(l); //add at very end, so even drawn on top of clippingMask
        }

        public void RemoveDivLabel(GridDivisionLabel l)
        {
            //griddivs are children of this GM, so they're drawn on top of anything inside the Graphs
            if (children.Contains(l))
                RemoveChild(l);
        }

        internal void UpdateParkedIndicators()
        {
            /* figure out which waves are hidden */
            Dictionary<Channel, EButtonImageAndTextSelectable> parkedIndicators = new Dictionary<Channel, EButtonImageAndTextSelectable>();
            foreach (var kvp in Waveform.EnabledWaveforms)
            {
                if (!Waveform.EnabledWaveformsVisible.Contains(kvp))
                {
                    Channel ch = kvp.Key;
                    EButtonImageAndTextSelectable parkedIndicator = new EButtonImageAndTextSelectable(
                        "widget-indicator-empty", ch.ToManagedColor(),
                        "widget-indicator-empty", ch.ToManagedColor(),
                        ch.Name, MappedColor.HelpButton,
                        ch.Name, MappedColor.HelpButton, false, false, Location.Top, Location.Center, null, null, Location.Right)
                        {
                            Rotation = -MathHelper.PiOver4,
                            TapCallback = new DrawableCallback(UICallbacks.ShowChannel, new object[] { ch, true }),
                        };

                    parkedIndicators.Add(ch, parkedIndicator);
                }
            }

            /* clear indicator panel, sort and re-add */
            parkedIndicatorPanel.Clear();
            foreach (var kvp in parkedIndicators.OrderByDescending(x => x.Key.Name))
            {
                if (kvp.Key is AnalogChannel)
                    if (ActiveGraphs.Contains(graphs[GraphType.Analog]))
                        parkedIndicatorPanel.AddItem(kvp.Value);
                if (kvp.Key is DigitalChannel)
                    if (ActiveGraphs.Contains(graphs[GraphType.Digital]))
                        parkedIndicatorPanel.AddItem(kvp.Value);
            }
            parkedIndicatorPanel.OnBoundariesChanged();
        }

        public void AddCursor(Cursor cur)
        {
            graphSplitPanel.AddChildBefore(graphs[GraphType.Frequency], cur);
        }

        public void RemoveCursor(Cursor cur)
        {
            graphSplitPanel.RemoveChild(cur);
        }

        private float[] Normalize(float[] arr, float valueToNormalizeTo = 1)
        {
            float sum = arr.Sum();
            for (int i = 0; i < arr.Length; i++)
                arr[i] = arr[i] / sum * valueToNormalizeTo;
            return arr;
        }

        public void ShowGraphs(List<GraphType> graphtypesToActivate, float[] spacing = null, bool immediate = false)
        {            
            const float minGraphSize = 0.2f;

            if (spacing == null || spacing.Length != graphtypesToActivate.Count)
            {
                //if spacing array is ill-defined: evenly distribute all active grids
                spacing = new float[graphtypesToActivate.Count];
                for (int i = 0; i < spacing.Length; i++)
                    spacing[i] = 1f / (float)spacing.Length;
            }

            //size limit
            for (int i = 0; i < spacing.Length; i++)
                if (spacing[i] < minGraphSize)
                    spacing[i] = minGraphSize;

            //make space for borders
            spacing = Normalize(spacing, 1-(spacing.Length-1)*intergraphBorderSpacing);

            //add to dictionary for easier lookup
            Dictionary<GraphType, float> spacingDict = new Dictionary<Drawables.GraphType, float>(spacing.Length);
            for (int i = 0; i < spacing.Length; i++)
                spacingDict.Add(graphtypesToActivate[i], spacing[i]);

            //clear activeGraphs and fill it, make active graphs visible and set all graph+border sizes
            activeGraphs.Clear();
            var allGraphTypes = Enum.GetValues(typeof(GraphType)).Cast<GraphType>();
            int nrAddedGraphs = 0;
            foreach (GraphType gType in allGraphTypes)
            {
                if (gType == GraphType.Measurements)
                {
                    MeasurementGraphPanel.Visible = graphtypesToActivate.Contains(gType);
                }
                else
                {
                    //for all non-measurement graphs
                    if (graphtypesToActivate.Contains(gType))
                    {
                        activeGraphs.Add(graphs[gType]);
                        graphs[gType].Visible = true;                        
                    }
                    else
                    {
                        graphs[gType].Visible = false;                        
                    }
                }

                //common part for normal and measurement graphs: sets size of graphs and borders
                if (graphtypesToActivate.Contains(gType))
                {
                    nrAddedGraphs++;

                    graphSplitPanel.SetPanelSize((int)gType * 2, spacingDict[gType], immediate ? 0 : ColorMapper.AnimationTime);

                    if (nrAddedGraphs < graphtypesToActivate.Count)
                        graphSplitPanel.SetPanelSize((int)gType * 2 + 1, intergraphBorderSpacing, immediate ? 0 : ColorMapper.AnimationTime); //if another active graph is following: add border
                    else if (gType != allGraphTypes.Last()) //if this is the last active graph: make sure its border is removed (if possible: the last GraphType doesn't have a border!)
                        graphSplitPanel.SetPanelSize((int)gType * 2 + 1, 0, immediate ? 0 : ColorMapper.AnimationTime);
                }
                else
                {
                    graphSplitPanel.SetPanelSize((int)gType * 2, 0, immediate ? 0 : ColorMapper.AnimationTime);
                    if (gType != allGraphTypes.Last())
                        graphSplitPanel.SetPanelSize((int)gType * 2 + 1, 0, immediate ? 0 : ColorMapper.AnimationTime);
                }
            }

            //show/hide cursors according to whether their graphs are shown
            foreach (Cursor cur in Cursors)
            {
                bool visible = false;
                foreach (Graph graph in activeGraphs)
                    if (graph.Grid == cur.Grid)
                        visible = true;
                cur.Visible = visible;
            }

            //hide voltage labels when analog grid is not shown
            if (!graphtypesToActivate.Contains(GraphType.Analog))
                LabelPrinter.RemoveGridLabelDefinitions(Graphs[GraphType.Analog].Grid);

            UpdateParkedIndicators();
        }

        protected override void LoadContentInternal()
        {
        }

        protected override void DrawInternal(GameTime time)
        {
        }        

        protected override void UpdateInternal(GameTime now)
        {
            base.UpdateInternal(now);
        }

        protected override void OnBoundariesChangedInternal()
        {            
            BorderSizePx = SelectorTriggerHorPos.Size.Value.X;

            int interBorderPixels = BorderSizePx / 2; //the border between different graphs
            int graphWidthPixels = Boundaries.Width - 2 * BorderSizePx;
            int graphHeightPixels = Boundaries.Height - 2 * BorderSizePx;

            //the inner section is the area inside the grey borders. this depends on whether the panorama is shown or not
            this.InnerSectionRectangle = new Rectangle(Boundaries.Left + BorderSizePx, Boundaries.Top + BorderSizePx, graphWidthPixels, graphHeightPixels);
            
            graphSplitPanel.SetBoundaries(InnerSectionRectangle);            

            //position panel which holds parked indicators
            Rectangle parkedPanelRect = new Rectangle(InnerSectionRectangle.X, InnerSectionRectangle.Bottom, InnerSectionRectangle.Width, Boundaries.Bottom - InnerSectionRectangle.Bottom);
            parkedIndicatorPanel.SetBoundaries(parkedPanelRect);
        }        
        
        #region waveforms

        public void AddWaveform(Channel ch, float minVDiv, float maxVDiv)
        {
            if (ch is AnalogChannel || ch is AnalogChannelRaw || ch is DebugChannel || ch is MathChannel || ch is ProtocolDecoderChannel || ch is OperatorAnalogChannel || ch is ReferenceChannel)
                Graphs[GraphType.Analog].AddWaveform(ch, minVDiv, maxVDiv);
            else if (ch is DigitalChannel || ch is ProtocolDecoderChannel || ch is OperatorDigitalChannel)
                Graphs[GraphType.Digital].AddWaveform(ch, minVDiv, maxVDiv);
            else if (ch is FFTChannel)
                Graphs[GraphType.Frequency].AddWaveform(ch, minVDiv, maxVDiv);
            else
                throw new NotImplementedException();
        }

        public void RemoveWaveform(Channel ch)
        {
            //FIXME: should search exactly which graph contains channel
            if (ch is AnalogChannel || ch is AnalogChannelRaw || ch is DebugChannel || ch is MathChannel || ch is ProtocolDecoderChannel || ch is OperatorAnalogChannel || ch is ReferenceChannel)
                Graphs[GraphType.Analog].RemoveWaveform(ch);
            else if (ch is DigitalChannel || ch is ProtocolDecoderChannel || ch is OperatorDigitalChannel)
                Graphs[GraphType.Digital].RemoveWaveform(ch);
            else
                throw new NotImplementedException();
        }

        public void BringWaveformToFront(Channel ch)
        {
            //find graph containing waveform of this channel, and forward request
            foreach (var kvp in Graphs)
                if (kvp.Value.Waveforms.ContainsKey(ch))
                    kvp.Value.BringWaveformToFront(ch);
        }        

        #endregion
    }
}
