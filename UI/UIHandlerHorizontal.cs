using System;




#if ANDROID
using Android.Content;
#endif


using ESuite.Drawables;
using LabNation.DeviceInterface.Hardware;
using LabNation.DeviceInterface.Devices;

namespace ESuite
{
    partial class UIHandler
    {
        const double MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING = 0.3; //in seconds
        double TriggerHoldoff
        {
            get { return Settings.Current.TriggerHoldoff.Value; }
            set
            {
                if (double.IsNaN(value))
                    Settings.Current.TriggerHoldoff = 0; //WARNING: if a breakpoint was hit here, examine it as you've hit a very rare bug!
                else
                    Settings.Current.TriggerHoldoff = value;
            }
        }
        double TriggerHoldoffForIndicator
        {
            get
            {
                if (gridAnchor == GridAnchor.AcquisitionBuffer)
                    return TriggerHoldoff;
                else if (gridAnchor == GridAnchor.Viewport)
                    return TriggerHoldoff - ViewportCenter;
                else
                    throw new Exception("Dunno what to do");
            }
        }
        double ViewportOffset
        {
            get { return Settings.Current.ViewportOffset.Value; }
            set
            {
                if (double.IsNaN(value))
                    Settings.Current.ViewportOffset = 0; //WARNING: if a breakpoint was hit here, examine it as you've hit a very rare bug!
                else
                    Settings.Current.ViewportOffset = value;
            }
        }
        double ViewportCenter
        {
            get { return ViewportOffset + ViewportTimespan / 2.0 - AcquisitionLength / 2.0; }
        }

        /// <summary>
        /// This constant defines when we tell the scope to attempt at making
        /// partial dumps rather than completing the acquisition and only dumping
        /// then. We do this in order to get a better UI experience where the
        /// graph is updated while data is coming in.
        /// </summary>
        const double VIEWPORT_TIMESPAN_PREFER_PARTIAL = 500e-3;

        double minimalViewportTimespan
        {
            get { return scope.SamplePeriod * 10; }
        }
        double ViewportTimespan
        {
            get { return Settings.Current.ViewportTimespan.Value; }
            set { Settings.Current.ViewportTimespan = value; }
        }

        bool PanoramaVisible { get { return panoramaSplitter.PanoramaShown; } }
        bool PanoramaEnabledPreference
        {
            get { return Settings.Current.PanoramaVisible.Value; }
            set { Settings.Current.PanoramaVisible = value; }
        }
        double AcquisitionLength { get { return Settings.Current.acquisitionLength.Value; } set { Settings.Current.acquisitionLength = value; } }

        public bool MinimizeAcquisitionLengthPending { get; private set; }
        public const float MIN_TIME_PER_DIVISION_SMARTSCOPE = 20e-9f;
        public const float MIN_TIME_PER_DIVISION_AUDIOJACK = 20e-6f;
        public const float MAX_TIME_PER_DIVISION = 200e-3f;

        internal void ShowPanorama(bool show, bool minimizeAcquisition = true)
        {
            PanoramaEnabledPreference = show;
            panoramaSplitter.ShowPanorama(show);
            if (show && scope.Rolling && scope.Running)
                return;
            scope.SendOverviewBuffer = show;
            if (PanoramaVisible != show)
            {
                if (!show && minimizeAcquisition)
                {
                    if (scope.Running)
                        MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
                    else
                        MinimizeAcquisitionLengthPending = true;
                }
                else
                    MinimizeAcquisitionLengthPending = false;
            }
        }
        internal void MinimizeAcquisitionLengthToFitViewport(double viewportTimespanTarget)
        {
            MinimizeAcquisitionLengthPending = false;

            if (scope.Rolling)
            {
                //put viewport at far right of acqBuffer, in attempt to make rolling mode respond instantaneously
                //FIXME: rolling mode still respond instantaneously, while viewport is on right side of acqBuffer (hit space to verify). seems like data isnt' coming in?
                double viewportCenterToTriggerHoldoff = ViewportCenter - 0;
                double newViewportCenter = scope.AcquisitionLength / 2 - viewportTimespanTarget / 2;
                double newTriggerHoldoff = newViewportCenter - viewportCenterToTriggerHoldoff;
                SetViewportCenterAndTimespan(newViewportCenter, viewportTimespanTarget);
                SetTriggerHoldoff(newTriggerHoldoff);
            }
            else if (!PanoramaVisible) //When panorama is hidden and not rolling, adjust acquisitionlength to make acquisition as fast as possible
            {
                double viewportCenterToTriggerHoldoff = ViewportCenter - TriggerHoldoff;
                double newViewportCenter = 0;
                double newTriggerHoldoff = newViewportCenter - viewportCenterToTriggerHoldoff;

                double acquisitionLengthTarget = Math.Max(viewportTimespanTarget * (scope.Rolling ? 1.0 : 2.0), scope.Rolling ? 0 : newTriggerHoldoff * 2.0);

                SetAcquisitionLength(acquisitionLengthTarget, scope.Rolling); //Maintain rolling state
                SetViewportCenterAndTimespan(newViewportCenter, viewportTimespanTarget);
                SetTriggerHoldoff(newTriggerHoldoff);
            }
        }
        internal void SetTimeScale(LinLog scale)
        {
            throw new NotImplementedException();
            /*
            if (scopeMode != ScopeMode.ScopeFrequency)
                return;

            foreach (Waveform w in Waveform.EnabledWaveforms.Values)
            {
                (w as WaveformAnalog).scale = scale;
            }
            frequencyModeTimeScale = scale;
            UpdateUiRanges();*/
        }

        internal void PanZoomGridHorizontal(float ratio, float offset, float center, bool invertBehaviour = false)
        {
            //in case of loading from file, zooming can only be done on the Viewport
            if (scope is DummyScope && (scope as DummyScope).isFile)
            {
                PanZoomViewportFromGridPinch(ratio, offset, center);
            }
            else
            {
                if ((!invertBehaviour && !scope.Running) || (invertBehaviour && scope.Running))
                    PanZoomViewportFromGridPinch(ratio, offset, center);
                else
                    PanZoomPanoramaFromGridPinch(1 / ratio, offset, center);
            }
        }

        internal void PanZoomViewportFromPanorama(float zoom, float pan, float center, bool wasMouseScroll)
        {
            if (!PanoramaVisible)
            {
                ShowSimpleToast("Can't zoom the viewport when panorama is hidden", 2000, null);
                return;
            }

            if (wasMouseScroll) zoom = 1f / zoom;

            PanoramaEnabledPreference = PanoramaVisible;

            double newAcquisitionLength = AcquisitionLength;
            double newViewportTimespan = ViewportTimespan * zoom;
            double newTriggerHoldoff = TriggerHoldoff;

            //when the scope is stopped, you still want to be able to zoom out the panorama. But when stopped, you want to limit zooming in to the level where the Acquisition buffer covers the entire panorama.
            if (!scope.Running && (newViewportTimespan < newAcquisitionLength))
                if (lastScopeData != null)
                    newAcquisitionLength = Math.Max(lastScopeData.AcquisitionLength, newViewportTimespan);

            if (float.IsNaN(center))
                center = (float)(ViewportCenter / AcquisitionLength);
            //<Distance pinchCenter to holdOff> = center * AcquisitionLength - TriggerHoldoff
            double pinchCenterToViewportCenter = center * AcquisitionLength - ViewportCenter;

            if (wasMouseScroll) pinchCenterToViewportCenter = 0;

            double newViewportCenter = (center + pan) * newAcquisitionLength - pinchCenterToViewportCenter;

            SetAcquisitionLength(newAcquisitionLength, false); //don't roll since we're using panorama to zoom
            SetTriggerHoldoff(newTriggerHoldoff);
            SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);
        }

        internal void PanZoomViewportFromGridPinch(float zoom, float pan, float center)
        {
            /*
			 * Keep trigger holdoff where it is, and ensure the pinch-center to holdoff
			 * distance is constant
			 */
            PanoramaEnabledPreference = PanoramaVisible;

            double newAcquisitionLength = AcquisitionLength;
            double newViewportTimespan = ViewportTimespan * zoom;
            if (newViewportTimespan > newAcquisitionLength)
                newAcquisitionLength = newViewportTimespan;

            //when the scope is stopped, you still want to be able to zoom out the panorama. But when stopped, you want to limit zooming in to the level where the Acquisition buffer covers the entire panorama.
            if (!scope.Running && (newViewportTimespan < newAcquisitionLength))
                if (lastScopeData != null)
                    newAcquisitionLength = Math.Max(lastScopeData.AcquisitionLength, newViewportTimespan);

            if (float.IsNaN(center))
                center = (float)(ViewportCenter / AcquisitionLength);
            double pinchCenterToTriggerAbsolute = ViewportCenter + center * ViewportTimespan - TriggerHoldoff;

            bool shouldRollInAutomaticSetting = (scope.Rolling && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING) || (ViewportTimespan <= MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING);
            SetAcquisitionLength(newAcquisitionLength, Settings.Current.SwitchAutomaticallyToRollingMode.Value ? shouldRollInAutomaticSetting : scope.Rolling);
            SetTriggerHoldoff(TriggerHoldoff);
            double newViewportCenter = pinchCenterToTriggerAbsolute + TriggerHoldoff - (center + pan) * newViewportTimespan;

            SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);

            MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
        }

        internal void SetTDivAbsolute(double newTDiv)
        {
            double newViewportTimespan = newTDiv * Grid.DivisionsHorizontalMax;

            double zoom = ViewportTimespan / newViewportTimespan;
            double newAcquisitionLength = AcquisitionLength / zoom;
            double newViewportCenter = ViewportCenter / AcquisitionLength * newAcquisitionLength;


            float center = (float)(ViewportCenter / AcquisitionLength);
            double pinchCenterToTriggerAbsolute = ViewportCenter + center * ViewportTimespan - TriggerHoldoff;
            double newTriggerHoldoff = newViewportCenter + (center + 0) * newViewportTimespan - pinchCenterToTriggerAbsolute;

            if (newTriggerHoldoff > newAcquisitionLength / 2.0)
            {
                if (PanoramaVisible)
                {
                    ShowTriggerClippedToast();
                    return;
                }
                else
                {
                    newAcquisitionLength = newTriggerHoldoff * 2.0;
                }
            }

            bool shouldRollInAutomaticSetting = (scope.Rolling && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING) || (ViewportTimespan <= MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING);
            SetAcquisitionLength(newAcquisitionLength, Settings.Current.SwitchAutomaticallyToRollingMode.Value ? shouldRollInAutomaticSetting : scope.Rolling);
            SetTriggerHoldoff(newTriggerHoldoff);
            SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);

            MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
        }

        internal void PanZoomPanoramaFromGridPinch(float zoom, float pan, float center)
        {
            /*
             * Pan Zoom Hell - Explained
             *  
             * Key to computing the new view is
             * - Understanding what is the boundary condition
             * - First compute locally, then call methods
             * 
             * In the case of a horizontal (time) pinch on the grid, you expect
             * the center of the pinch gesture to stick to the shap of the wave
             * beneath. This translates to keeping the time between that center
             * point and the trigger holdoff (the shape-time-origin of the waveform)
             * constant.
             * 
             * So what we do here, is start by computing the knowns:
             * - AcquisitionLength and ViewportTimespan just scale
             * - We want the viewport to retain it's relative position on the panorama
             * 
             * From here on we compute the new TriggerHoldoff, from the boundary condition
             * 
             * <Distance pinchCenter to holdOff> = center * ViewportTimespan + ViewportCenter - TriggerHoldoff
             * 
             */

            double newAcquisitionLength = AcquisitionLength / zoom;
            double newViewportTimespan = ViewportTimespan / zoom;
            double newViewportCenter = ViewportCenter / AcquisitionLength * newAcquisitionLength;

            if (float.IsNaN(center))
                center = (float)(ViewportCenter / AcquisitionLength);
            double pinchCenterToTriggerAbsolute = ViewportCenter + center * ViewportTimespan - TriggerHoldoff;
            double newTriggerHoldoff = newViewportCenter + (center + pan) * newViewportTimespan - pinchCenterToTriggerAbsolute;

            if (newTriggerHoldoff > newAcquisitionLength / 2.0)
            {
                if (PanoramaVisible)
                {
                    ShowTriggerClippedToast();
                    return;
                }
                else
                {
                    newAcquisitionLength = newTriggerHoldoff * 2.0;
                }
            }

            bool shouldRollInAutomaticSetting = (scope.Rolling && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING) || (ViewportTimespan <= MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING && newViewportTimespan > MIN_VIEWPORT_TIMESPAN_BEFORE_ROLLING);
            SetAcquisitionLength(newAcquisitionLength, Settings.Current.SwitchAutomaticallyToRollingMode.Value ? shouldRollInAutomaticSetting : scope.Rolling);
            SetTriggerHoldoff(newTriggerHoldoff);
            SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);

            MinimizeAcquisitionLengthToFitViewport(ViewportTimespan);
        }

        internal void PanZoomPanoramaFromPanorama(float zoom, float pan, float center, bool wasMouseScroll)
        {
            /* 
             * When zooming the panorama, keep the viewfinder's center
             * and the trigger holdoff fixed on the panorama. This is
             * done by storing their relative positions.
             * Of course, when there's a pan offset, we add this to the
             * relative positions
             */
            if (!PanoramaVisible)
            {
                ShowSimpleToast("Can't zoom the panorama when it's hidden", 2000, null);
                return;
            }

            double newAcquisitionLength = AcquisitionLength / zoom;
            double newViewportTimespan = ViewportTimespan / zoom;

            if (float.IsNaN(center))
                center = (float)(TriggerHoldoff / AcquisitionLength);
            double triggerHoldoffToPinchCenter = center * AcquisitionLength - TriggerHoldoff;
            double newTriggerHoldoff = (center + pan) * newAcquisitionLength - triggerHoldoffToPinchCenter;

            if (newTriggerHoldoff > newAcquisitionLength / 2.0)
            {
                ShowTriggerClippedToast();
                newTriggerHoldoff = newAcquisitionLength / 2.0;
            }

            double newViewportCenter = ViewportCenter / AcquisitionLength * newAcquisitionLength;

            SetAcquisitionLength(newAcquisitionLength, false);
            SetTriggerHoldoff(newTriggerHoldoff);
            SetViewportCenterAndTimespan(newViewportCenter, newViewportTimespan);
        }

        internal void EnableRollingByUser()
        {
            EnableRolling(true);
        }
        internal void EnableRolling(bool enable)
        {
            SetAcquisitionLength(AcquisitionLength, enable);
        }

        internal void ChangeTimebaseLimits(float min, float max)
        {
            //update picker wheels
            gm.Graphs[GraphType.Analog].RepopulateTDivWheelItems(min, max);
            gm.Graphs[GraphType.Digital].RepopulateTDivWheelItems(min, max);

            //in case current setting is outside of new bounds: correct
            if (ViewportTimespan / Grid.DivisionsHorizontalMax > max)
                ViewportTimespan = max * Grid.DivisionsHorizontalMax;
            if (ViewportTimespan / Grid.DivisionsHorizontalMax < min)
                ViewportTimespan = min * Grid.DivisionsHorizontalMax;
        }

        internal void SetAcquisitionLength(double time, bool requestRoll)
        {
            if (time > scope.AcquisitionLength && ViewportTimespan == AcquisitionLength && scope.SubSampleRate == scope.InputDecimationMax && scope.AcquisitionDepth == scope.AcquisitionDepthUserMaximum && scope.AcquisitionDepthUserMaximum < scope.AcquisitionDepthMax)
                ShowSimpleToast("Max limit reached. Enlarge the RAM depth through Menu -> System -> Acquisition Depth", 2000);

            if (scope is LabNation.DeviceInterface.Devices.DummyScope && time > scope.AcquisitionLengthMax)
            {
                time = scope.AcquisitionLengthMax;
                if (ViewportTimespan == AcquisitionLength)
                {
                    LabNation.DeviceInterface.Devices.DummyScope dummy = scope as LabNation.DeviceInterface.Devices.DummyScope;
                    if (dummy.isAudio)
                        ShowSimpleToast("A SmartScope is needed to zoom out till 2s/div", 3000);
                }
            }

            if (time < minimalViewportTimespan)
            {
                time = minimalViewportTimespan;
            }
            bool couldRoll = scope.CanRoll;
            bool wasRolling = scope.Rolling;

            scope.PreferPartial = time >= VIEWPORT_TIMESPAN_PREFER_PARTIAL;
            scope.AcquisitionLength = time;
            AcquisitionLength = Math.Min(time, scope.AcquisitionLength);

            if (scope.CanRoll && requestRoll)
            {
                //do only when changing from false to true
                if (!scope.Rolling)
                    EnableFFT(false);

                scope.Rolling = true;
                triggerDropDown.SelectItemByTag("roll");
                if (scope.Running)
                    ShowPanorama(false, false);
            }
            else
            {
                //do only when changing from true to false
                if (scope.Rolling)
                {
                    if (Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogFFT) EnableFFT(true);
                    if (Settings.Current.analogGraphCombo == AnalogGraphCombo.AnalogXY) EnableXY(true);
                }

                scope.Rolling = false;
                scope.AcquisitionMode = AcquisitionMode;
                triggerDropDown.SelectItemByTag(AcquisitionMode);
                triggerDropDownRollingModeItem.Disabled = !scope.CanRoll;
                ShowPanorama(PanoramaEnabledPreference);
            }

            measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.AcquisitionLength].UpdateValueInternal(AcquisitionLength);
            measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.AcquisitionDepth].UpdateValueInternal(scope.AcquisitionDepth);
        }
        public void SetViewportCenterAndTimespan(double center, double timespan)
        {
            double offset = (center + AcquisitionLength / 2.0) - timespan / 2;
            if (timespan < minimalViewportTimespan)
            {
                ShowSimpleToast("Viewfinder reached maximum zoom", 2000);
                if (scope is LabNation.DeviceInterface.Devices.DummyScope)
                {
                    LabNation.DeviceInterface.Devices.DummyScope dummy = scope as LabNation.DeviceInterface.Devices.DummyScope;
                    if (dummy.isAudio)
                    {
                        ShowSimpleToast("A SmartScope is needed to zoom till 10ns/div", 3000);
                    }
                }

                double timespanDifference = scope.SamplePeriod * 10 - timespan;
                timespan += timespanDifference;
                offset -= timespanDifference / 2;
            }

            if (offset < 0)
                offset = 0;

            if (timespan > AcquisitionLength)
            {
                timespan = AcquisitionLength;
                offset = 0;
            }
            if (offset + timespan > AcquisitionLength)
                offset = AcquisitionLength - timespan;

            double offsetConvertor = 0;
            if (!scope.Running && currentDataCollection != null)
            {
                offsetConvertor = currentDataCollection.HoldoffCenter - TriggerHoldoff;
                offsetConvertor += (currentDataCollection.AcquisitionLength - AcquisitionLength) / 2.0;
            }
            else
            {
                offsetConvertor = (scope.AcquisitionLength - AcquisitionLength) / 2.0;
            }

            scope.SetViewPort(offset + offsetConvertor, timespan);

            double scopeTimeRange = scope.ViewPortTimeSpan;

            if (ViewportTimespan != timespan)
                etsProcessor.RequestETSReset();

            ViewportTimespan = timespan;
            ViewportOffset = offset;

            measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.ViewportLength].UpdateValueInternal(ViewportTimespan);
            measurementManager.SystemMeasurements[Measurements.SystemMeasurementType.ViewportOffset].UpdateValueInternal(ViewportOffset);

            SetTriggerHoldoff(TriggerHoldoff);
            UpdateUiRanges(graphManager.Graphs[GraphType.Analog].Grid);
        }
    }
}
