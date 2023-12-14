using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;

namespace ESuite.Drawables
{
    internal class Graph: EDrawable
    {
        private EGraphicImageBackground background;
        public Grid Grid { get; private set; }
        private Panorama panorama;
        GraphManager gm;
        BoundaryDefiner offsetBoundaryDefiner;
        BoundaryDefiner triggerBoundaryDefiner;
        internal List<GridDivisionLabel> GridDivisionLabels { get { return voltageDivisionLabels.Values.ToList(); } }
        internal GridDivisionLabel SDivLabel { get { return sDivLabel; } }
        public Dictionary<Channel, Waveform> Waveforms { get; private set; }
        internal override bool Visible
        {
            get
            {
                return base.Visible;
            }
            set
            {
                base.Visible = value;
                if (sDivLabel != null)
                    sDivLabel.Visible = value;
                this.Grid.Visible = value;
            }
        }
        public Dictionary<Channel, Waveform> EnabledWaveforms
        {
            get
            {
                return Waveforms.Where(x => x.Value.Enabled).ToDictionary(x => x.Key, x => x.Value);
            }
        }
        public Dictionary<Channel, Waveform> EnabledWaveformsVisible
        {
            get
            {
                return Waveforms.Where(x => x.Value.Enabled && x.Value.Visible).ToDictionary(x => x.Key, x => x.Value);
            }
        }
        private Dictionary<Channel, GridDivisionLabel> voltageDivisionLabels = new Dictionary<Channel, GridDivisionLabel>();
        private GridDivisionLabel sDivLabel;

        public Graph(Grid grid, GraphManager gm, Panorama panorama, float minTimeDiv, float maxTimeDiv)
            : base()
        {
            this.panorama = panorama;
            this.gm = gm;
            this.Waveforms = new Dictionary<Channel, Waveform>();
            this.offsetBoundaryDefiner = new BoundaryDefiner(Boundaries.Top, Boundaries.Bottom);
            this.triggerBoundaryDefiner = new BoundaryDefiner(Boundaries.Top, Boundaries.Bottom);

            background = new EGraphicImageBackground("white", MappedColor.MainAreaBackground, true);
            AddChild(background);

            this.Grid = grid;
            AddChild(grid);

            sDivLabel = new GridDivisionLabel(MappedColor.TimeScaleFont, minTimeDiv, maxTimeDiv, "s", ColorMapper.timePrecision, null);
            sDivLabel.BindScrollUpCallback(new DrawableCallback(UICallbacks.PanAndZoomGrid, new object[] { new Vector2(2f, 1f), new Vector2(), new Vector2(), false }));
            sDivLabel.BindScrollDownCallback(new DrawableCallback(UICallbacks.PanAndZoomGrid, new object[] { new Vector2(0.5f, 1f), new Vector2(), new Vector2(), false }));
            sDivLabel.BindSlideCallback(new DrawableCallback(UICallbacks.SetTDivAbsolute, null));

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
        }

        protected override void DrawInternal(GameTime time)
        {
        }

        protected override void OnBoundariesChangedInternal()
        {
            if (Grid is GridXY && (Grid as GridXY).Squared)
            {
                Rectangle squaredBoundaries = this.Boundaries;
                int difference = squaredBoundaries.Width - squaredBoundaries.Height;
                squaredBoundaries.Width = squaredBoundaries.Height;
                squaredBoundaries.X += difference / 2;
                Boundaries = squaredBoundaries;
            }

            this.Grid.SetBoundaries(this.Boundaries);
            this.background.SetBoundaries(this.Boundaries);

            foreach (Waveform w in children.Where(x => x is Waveform))
                w.SetBoundaries(this.Boundaries);

            offsetBoundaryDefiner.UpdateLimits(Boundaries.Top, Boundaries.Bottom);
            triggerBoundaryDefiner.UpdateLimits(Boundaries.Top, Boundaries.Bottom);

            UpdateGridDivLabelPositions();
        }

        internal void UpdateChannelDivisionLabel(Channel channel, double value)
        {
            if (voltageDivisionLabels.ContainsKey(channel))
                voltageDivisionLabels[channel].Value = value;
        }

        public void RepopulateVDivWheelItems(Channel ch, float minVDiv, float maxVDiv)
        {
            if (!voltageDivisionLabels.ContainsKey(ch)) return;
            voltageDivisionLabels[ch].RepopulateItems(minVDiv, maxVDiv);
        }

        public void RepopulateTDivWheelItems(float minVDiv, float maxVDiv)
        {
            if (sDivLabel != null)
                sDivLabel.RepopulateItems(minVDiv, maxVDiv);
        }

        public void AddWaveform(Channel ch, float minVDiv, float maxVDiv)
        {
            //Add waveform to grid
            Waveform waveform = null;
            MappedColor color;

            if (ch is MeasurementChannel)
                color = (ch as MeasurementChannel).Color;
            else
                color = ch.ToManagedColor();

            //needs to be instantiated here, because caller (such as the Translator) could not have link to device
            if (ch is AnalogChannel || ch is MathChannel || ch is OperatorAnalogChannel
#if DEBUG
 || ch is DebugChannel
#endif
)
            {
                string unit = "V";
                if (ch is AnalogChannel) unit = (ch as AnalogChannel).Probe.Unit;
                GridDivisionLabel l = new GridDivisionLabel(ch.ToManagedColor(), minVDiv, maxVDiv, unit, 1e-6, ch);
                l.LoadContent();
                l.BindScrollUpCallback(new DrawableCallback(UICallbacks.ZoomChannelVertically, new object[] { ch, false }));
                l.BindScrollDownCallback(new DrawableCallback(UICallbacks.ZoomChannelVertically, new object[] { ch, true }));
                l.BindSlideCallback(new DrawableCallback(UICallbacks.SetChannelDivVertically, null));
                voltageDivisionLabels.Add(ch, l);
                gm.AddDivLabel(l);

                waveform = new WaveformAnalog(this, color, ch, l);
            }
            else if (ch is ReferenceChannel)
                waveform = new WaveformReference(this, color, ch);
            else if (ch is FFTChannel)
                waveform = new WaveformFreq(this, (ch as FFTChannel).processor.analogChannel.ToManagedColor(), ch);
            else if (ch is DigitalChannel || ch is OperatorDigitalChannel)
                waveform = new WaveformDigital(this, color, ch);
            else if (ch is ProtocolDecoderChannel)
                waveform = new WaveformDecoded(this, color, ch);
            else if (ch is XYChannel)
                waveform = new WaveformXY(this, color, ch, Grid as GridXY);
            else if (ch is MeasurementChannel)
                waveform = new WaveformMeasurement(this, ch as MeasurementChannel);

            this.Waveforms.Add(ch, waveform);

            if (ch is AnalogChannel || ch is DigitalChannel)
                this.panorama.AddScopeChannel(waveform);

            if (!(ch is FFTChannel) && !(ch is XYChannel) && !(ch is MeasurementChannel))
            {
                IndicatorInteractive offsetIndicator = new IndicatorInteractiveBound(this.Grid,
                    "widget-indicator-empty", "widget-indicator-hilite", "widget-indicator-hilite",
                    ch.ToManagedColor(), MappedColor.Neutral,
                    Location.Left, offsetBoundaryDefiner, ch.Name, "");
                offsetIndicator.BindSlideCallback(new DrawableCallback(UICallbacks.OffsetIndicatorMoved, ch));
                offsetIndicator.BindDropCallback(new DrawableCallback(UICallbacks.OffsetIndicatorDropped, ch));
                offsetIndicator.BindTapCallback(new DrawableCallback(UICallbacks.OffsetIndicatorClicked, ch));
                offsetIndicator.BindDoubleTapCallback(new DrawableCallback(UICallbacks.OffsetIndicatorDoubleClicked, ch));
                offsetIndicator.BindHoldCallback(new DrawableCallback(UICallbacks.OffsetIndicatorClicked, ch));
                gm.AddIndicator(offsetIndicator); //need to do this way, as indicator needs to be drawn on top of blocker, which is drawn on top of this Graph
                waveform.OffsetIndicator = offsetIndicator;
            }

            BringWaveformToFront(ch);

            if (ch is DigitalChannel)
            {
                IndicatorInteractive digitalTriggerIndicator = new IndicatorInteractiveBound(this.Grid,
                    "widget-indicator-empty", "widget-indicator-hilite", "widget-indicator-hilite",
                    ch.ToManagedColor(), MappedColor.Neutral,
                    Location.Right, triggerBoundaryDefiner);
                digitalTriggerIndicator.BindTapCallback(new DrawableCallback(UICallbacks.DigitalTriggerIndicatorTapped, ch));
                gm.AddIndicator(digitalTriggerIndicator); //need to do this way, as indicator needs to be drawn on top of blocker, which is drawn on top of this Graph
                (waveform as WaveformDigital).TriggerIndicator = digitalTriggerIndicator;
            }
        }
        public void RemoveWaveform(Channel ch)
        {
            Waveform w = Waveform.Waveforms[ch];
            gm.RemoveIndicator(w.OffsetIndicator);

            if (w.PanoramaWaveParent != null)
                this.panorama.RemoveScopeChannel(w);

            if (ch is DigitalChannel)
                gm.RemoveIndicator(WaveformDigital.Waveforms[ch].TriggerIndicator);

            Waveform waveform = Waveform.Waveforms[ch];
            RemoveChild(waveform);

            if (voltageDivisionLabels.ContainsKey(ch))
            {
                gm.RemoveDivLabel(voltageDivisionLabels[ch]);
                voltageDivisionLabels.Remove(ch);
            }

            w.Destroy();
            OnBoundariesChanged();

            this.Waveforms.Remove(ch);
        }

        public void BringWaveformToFront(Channel ch)
        {
            RemoveChild(Waveform.Waveforms[ch]);
            AddChild(Waveform.Waveforms[ch]);
        }

        public void UpdateGridDivLabelPositions()
        {
            //position all V/div labels
            int margin = Scaler.GraphLabelMargin.InchesToPixels();
            int width = Scaler.GraphLabelWidth.InchesToPixels();
            int height = Scaler.GraphLabelHeight.InchesToPixels();

            Rectangle rectSDiv = new Rectangle(Boundaries.Right - margin - width, Boundaries.Bottom - height - 2, width, height); //no clue why this -2 is needed...
            SDivLabel.SetBoundaries(rectSDiv);
            SDivLabel.OnBoundariesChanged();
            rectSDiv = SDivLabel.Boundaries;
            Rectangle lastLabelRectangle = rectSDiv;

            //add from right to left
            List<GridDivisionLabel> divLabelsOrdered = new List<GridDivisionLabel>();
            //timebase
            divLabelsOrdered.AddRange(GridDivisionLabels.Where(x => x.channel == null));
            //analog channels
            divLabelsOrdered.AddRange(GridDivisionLabels.Where(x => x.channel != null && x.channel is AnalogChannel && Waveform.EnabledWaveformsVisible.ContainsKey(x.channel)).OrderBy(x => x.channel.Name).Reverse());
            //all the rest
            divLabelsOrdered.AddRange(GridDivisionLabels.Where(x => x.channel != null && !divLabelsOrdered.Contains(x) && Waveform.EnabledWaveformsVisible.ContainsKey(x.channel)).OrderBy(x => x.channel.Name).Reverse());

            foreach (GridDivisionLabel l in divLabelsOrdered)
            {
                Rectangle labelRectangle = new Rectangle(lastLabelRectangle.Left - margin - width, lastLabelRectangle.Top, width, lastLabelRectangle.Height);
                l.SetBoundaries(labelRectangle);
                l.OnBoundariesChanged();
                lastLabelRectangle = l.Boundaries;
            }

            //finally, set visibility
            foreach (GridDivisionLabel l in GridDivisionLabels)
            {
                if (l.channel == null)
                    l.Visible = true;
                else
                    l.Visible = Waveform.EnabledWaveformsVisible.ContainsKey(l.channel);
            }
        }
    }
}
