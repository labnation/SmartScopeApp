using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using ESuite.Measurements;
using ESuite.Drawables;
using LabNation.DeviceInterface;
using LabNation.DeviceInterface.Devices;
using System.Diagnostics;
using LabNation.Common;
using System.Collections.Concurrent;
#if ANDROID
using Android.Content;
#endif

namespace ESuite {
    public enum RenderMode { Immediate, Deferred }
	public class ScopeApp {
        internal static EDrawable GestureClaimer = null;
		//The drawable that claimed the gesture
		internal static bool GestureMustBeReleased = false;
		//Set to true when drawable doesn't need to hold the gesture anymore
#if DEBUG
		public static List<string> DebugTextList = new List<string> ();
        public static List<string> DebugPermanentTextList = new List<string>();
#endif

        private ContentManager Content;
		private SpriteFont measurementTitleFont;
		private SpriteFont measurementValueFont;
        FileLogger filelog;

        private bool initialised = false;
        public bool Quitting { get; internal set; }
		internal event EventHandler OnUpdate;

		private GraphicsDevice GraphicsDevice { get; set; }

		private SpriteBatch SpriteBatch { get; set; }

		private BasicEffect Effect { get; set; }

		private ConcurrentQueue<GestureSample> windowsGestures = new ConcurrentQueue<GestureSample> ();

        private EDrawable Gui;

        private UIHandler UIHandler { get; set; }
		//Framerate throttling
		private const int frameRateTarget = 30;

		public ScopeApp() { }

		public void Initialize (
            #if ANDROID
            Android.Content.Context context,
            #endif
            GraphicsDevice graphicsDevice, ContentManager content, 
            #if WINDOWS && DEBUG
            DeviceConnectHandler connectHandler, 
            #endif
            LogLevel logLevel, int pixelsPerInch)
		{
			this.Quitting = false;
			this.GraphicsDevice = graphicsDevice;
			this.Content = content;

            if(pixelsPerInch < 1) {
                string msg = "Aborting because ppi smaller than 1";
                Logger.Error(msg);
                throw new Exception(msg);
            }

            //Find nearest DPI
            var dpis = (Scaler.PixelDensity[])Enum.GetValues(typeof(Scaler.PixelDensity));
            var ordered = dpis.Select(x => new { dpi = x, difference = Math.Abs((int)x - pixelsPerInch) }).
                OrderBy(x => x.difference).ToList();
            
            Scaler.CurrentDpi = ordered.First().dpi;

            #if !ANDROID && !IOS
            try {
                filelog = new FileLogger("smartscope.log", logLevel);
            } catch (Exception) {} //Don't fail on locked file
            #endif
            #if IOS
            ConsoleLogger cl = new ConsoleLogger(LogLevel.DEBUG);
            #endif

			measurementTitleFont = Content.Load<SpriteFont> (Scaler.GetFontDialog());
            measurementValueFont = Content.Load<SpriteFont>(Scaler.GetFontDialog());

			Effect = new BasicEffect (graphicsDevice);
			Effect.VertexColorEnabled = true;
			SpriteBatch = new SpriteBatch (graphicsDevice);
			EDrawable.Initialize (graphicsDevice, Effect, SpriteBatch, Content);

			
			UIHandler = new UIHandler(this
            #if WINDOWS && DEBUG
            , connectHandler
            #endif
            );

            Gui = UIHandler.Initialize(graphicsDevice
                #if ANDROID
                , context
                #endif
                );
            //Try setting scale. In case it's too large, we'll revert to the next best thing   
            UIHandler.SetGuiScale(null, Scale.Normal, true); //First try scaling so that the "NORMAL" setting fits, using a DPI before Scale strategy
            ApplySettings(Settings.Current); //Then set the user's desired scale.
            Settings.ObserveMe(this.ApplySettings);

            TouchPanel.EnabledGestures = GestureType.Tap | GestureType.Pinch | GestureType.PinchComplete | GestureType.FreeDrag | GestureType.DragComplete | GestureType.Hold | GestureType.DoubleTap;
            initialised = true;
        }

        internal void ApplySettings(Settings settings)
        {
            UIHandler.SetGuiScale(null, settings.GuiScale.Value); //Then set the user's desired scale.
        }

		#region windows mouse event translators

#if !__IOS__
        public void LeftMouseClick(int clickX, int clickY)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.Tap, new TimeSpan(0,0,0,0,0),  new Vector2(clickX, clickY), new Vector2(), new Vector2(), new Vector2()));
        }

        public void RightMouseClick(int clickX, int clickY)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.Hold, new TimeSpan(0, 0, 0, 0, 0), new Vector2(clickX, clickY), new Vector2(), new Vector2(), new Vector2()));
        }

        public void LeftMouseDoubleClick(int clickX, int clickY)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.DoubleTap, new TimeSpan(0, 0, 0, 0, 0), new Vector2(clickX, clickY), new Vector2(), new Vector2(), new Vector2()));
        }
        
        public void ZoomOutHorizontal(Point centerPos)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.MouseScroll, new TimeSpan(0, 0, 0, 0, 0), new Vector2(centerPos.X, centerPos.Y), new Vector2(), new Vector2(0, -1), new Vector2(0, 0))); //-1 on Y means down
        }

        public void ZoomInHorizontal(Point centerPos)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.MouseScroll, new TimeSpan(0, 0, 0, 0, 0), new Vector2(centerPos.X, centerPos.Y), new Vector2(), new Vector2(0, 1), new Vector2(0, 0))); //+1 on Y means up
        }

        public void ZoomOutVertical(Point centerPos)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.MouseScroll, new TimeSpan(0, 0, 0, 0, 0), new Vector2(centerPos.X, centerPos.Y), new Vector2(), new Vector2(1, 0), new Vector2(0, 0))); //+1 on X means right
        }

        public void ZoomInVertical(Point centerPos)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.MouseScroll, new TimeSpan(0, 0, 0, 0, 0), new Vector2(centerPos.X, centerPos.Y), new Vector2(), new Vector2(-1, 0), new Vector2(0, 0))); //-1 on X means left        
        }

        public void AddFreeDragRegular(Point startingMouseLocation, Point previousMouseLocation, Point currentMouseLocation)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.FreeDrag, new TimeSpan(0, 0, 0, 0, 0), new Vector2(currentMouseLocation.X, currentMouseLocation.Y), new Vector2(), new Vector2(currentMouseLocation.X - previousMouseLocation.X, currentMouseLocation.Y - previousMouseLocation.Y), new Vector2())); //FIXME: this 1,1 is another dirty hack, to indicate it's a left drag. default drag behaviour on iPad is move:rightClickDrag
        }

        public void AddFreeDragModified(Point startingMouseLocation, Point previousMouseLocation, Point currentMouseLocation)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.FreeDrag, new TimeSpan(0, 0, 0, 0, 0), new Vector2(currentMouseLocation.X, currentMouseLocation.Y), new Vector2(), new Vector2(currentMouseLocation.X - previousMouseLocation.X, currentMouseLocation.Y - previousMouseLocation.Y), new Vector2(1, 1)));
        }        

        public void FinishFreeDragRegular(Point startingMouseLocation, Point previousMouseLocation, Point currentMouseLocation)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.DragComplete, new TimeSpan(0, 0, 0, 0, 0), new Vector2(currentMouseLocation.X, currentMouseLocation.Y), new Vector2(), new Vector2(currentMouseLocation.X - previousMouseLocation.X, currentMouseLocation.Y - previousMouseLocation.Y), new Vector2())); //this 1,1 is another dirty hack, to indicate it's a left drag. default drag behaviour on iPad is move:rightClickDrag
        }

        public void FinishFreeDragModified(Point startingMouseLocation, Point previousMouseLocation, Point currentMouseLocation)
        {
            windowsGestures.Enqueue(new GestureSample(GestureType.DragComplete, new TimeSpan(0, 0, 0, 0, 0), new Vector2(currentMouseLocation.X, currentMouseLocation.Y), new Vector2(), new Vector2(currentMouseLocation.X - previousMouseLocation.X, currentMouseLocation.Y - previousMouseLocation.Y), new Vector2(1, 1)));
        }

        public void HandleKey(List<Keys> pressedKeys, List<Keys> keyDown, List<Keys> keyUp, Point mousePos)
        {
            UIHandler.QueueCallback(new DrawableCallback(delegate(EDrawable d, object c) { UIHandler.HandleKey(pressedKeys, keyDown, keyUp, mousePos); }));
        }

#endif

#endregion

        #region drawing

        private void RenderTextAtLocation (string text, Vector2 location, SpriteFont font, Color color)
		{
			SpriteBatch.DrawString (font, text, location, color);            
		}

		public void Update (GameTime gametime)
		{
            if (!initialised) return;
            EDrawable.FullRedrawRequired = false;
#if DEBUG
            //clean debugtexts
            DebugTextList.Clear ();
			if (GestureClaimer != null)
				DebugTextList.Add ("GestureClaimer: " + GestureClaimer.GetType ().Name);
#endif
            //Merge mouse and touch input into 1 gestureList
            List<GestureSample> gestureList = HandleInputsFromAllDevices();

            //at the end of a pinch, remove any spurious drag gestures as they cause small unintended offset movements
            if (gestureList.Where(x => x.GestureType == GestureType.PinchComplete).ToList().Count > 0)
            {
                for (int i = gestureList.Count - 1; i >= 0; i--)
                {
                    if ((gestureList[i].GestureType == GestureType.FreeDrag) || (gestureList[i].GestureType == GestureType.DragComplete) || (gestureList[i].GestureType == GestureType.VerticalDrag) || (gestureList[i].GestureType == GestureType.HorizontalDrag))
                        gestureList.RemoveAt(i);
                }
            }

			foreach (GestureSample gesture in gestureList) {
				Gui.HandleGesture (gesture);
				if (ScopeApp.GestureMustBeReleased) {
					ScopeApp.GestureClaimer = null;
					ScopeApp.GestureMustBeReleased = false;
				}
			}

            //Do whatever is requested to do before actually drawing
            if (OnUpdate != null)
                OnUpdate(this, new EventArgs());

            //here gestureList contains all gestures, coming from XNA or hacked input
            //main Update hook to all components -> needs to be called whether there are gestures or not            
			Gui.Update (gametime, gestureList);
		}

        /// <summary>
        /// This method combines the user inputs from touchpanels and mouse.
        /// The tricky part is, that Windows copies all touches as mouse inputs. 
        /// This would result in all operations done twice: drags would be executed at double speed eg.
        /// The solution is to only use the touch input when touches are available
        /// </summary>
        private List<GestureSample> HandleInputsFromAllDevices()
        {
            List<GestureSample> gestureList = new List<GestureSample>();

            //first process any touch input
            while (TouchPanel.IsGestureAvailable)
            {
                GestureSample gesture = TouchPanel.ReadGesture();
                gestureList.Add(gesture);
            }

            //in case there was no touch input: check for mouse input
            bool useMouseGestures = gestureList.Count == 0;
            
            //during pinching, the mouse is continuously sending scrollwheel events.
            //normally this causes useMouseGestures to be true, but not in the case when the Touch and Mouse events are received in a different update cycle!
            //therefore, while pinching all mouse input is disabled here
            if (UIHandler.Pinching) useMouseGestures = false;

            GestureSample g;
            while (windowsGestures.TryDequeue(out g))
            {
                if(useMouseGestures)
                    gestureList.Add(g);
            }
            
            return gestureList;
        }

        internal struct CachedImage
        {
            internal RenderTarget2D renderTarget;
            internal List<EDrawable> drawablesContained;
        }

        private List<CachedImage> freeCachedImages;
        private List<CachedImage> usedCachedImages;
        const int MAX_CACHED_IMAGES = 10;
        SpriteBatch spriteBatch;

        private void AllocateRenderTargets()
        {
            if (GraphicsDevice == null)
                return;

            //first dispose of any existing rendertarget
            if (freeCachedImages != null)
                for (int i = 0; i < freeCachedImages.Count; i++)
                    freeCachedImages[i].renderTarget.Dispose();
            if (usedCachedImages != null)
                for (int i = 0; i < usedCachedImages.Count; i++)
                    usedCachedImages[i].renderTarget.Dispose();

            //allocate new
            freeCachedImages = new List<ESuite.ScopeApp.CachedImage>();
            usedCachedImages = new List<ESuite.ScopeApp.CachedImage>();
            for (int i = 0; i < 2 * MAX_CACHED_IMAGES; i++)
            {
                RenderTarget2D rt = new RenderTarget2D(GraphicsDevice, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);
                CachedImage ci = new CachedImage();
                ci.renderTarget = rt;
                freeCachedImages.Add(ci);
            }
        }

        private void DisposeRenderTargets()
        {
            //first dispose of any existing rendertarget
            if (freeCachedImages != null)
                for (int i = 0; i < freeCachedImages.Count; i++)
                    freeCachedImages[i].renderTarget.Dispose();
            if (usedCachedImages != null)
                for (int i = 0; i < usedCachedImages.Count; i++)
                    usedCachedImages[i].renderTarget.Dispose();

            freeCachedImages.Clear();
            freeCachedImages = null;
            usedCachedImages.Clear();
            freeCachedImages = null;
        }

        public static bool ScreenshotRequested = false;
        public void Draw (GameTime gametime)
		{
            bool processingScreenshot = false;

            if (!initialised) return;

            //in case screenshot has been requested: init render target, activate and clear
            RenderTarget2D screenshotRenderTarget = null;
            if (ScreenshotRequested)
            {
                ScreenshotRequested = false;
                processingScreenshot = true;

                screenshotRenderTarget = new RenderTarget2D(GraphicsDevice, GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);

                GraphicsDevice.SetRenderTarget(screenshotRenderTarget);
                GraphicsDevice.Clear(Color.Black);
            }

            ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            if (Settings.CurrentRuntime.RenderMode == RenderMode.Deferred && (!ScreenshotRequested))
            {
                if (freeCachedImages == null || spriteBatch == null) //initialization
                {
                    spriteBatch = new SpriteBatch(GraphicsDevice);
                    AllocateRenderTargets();
                }

                List<List<EDrawable>> drawList = new List<List<EDrawable>>();
                drawList.Add(new List<EDrawable>());
                drawList = Gui.CreateDrawList(drawList);

                if ((drawList.Count > MAX_CACHED_IMAGES) || EDrawable.FullRedrawRequired) //too many changed sections -> just render as usual
                {
                    foreach (var drawablesSection in drawList)
                        foreach (var d in drawablesSection)
                            d.Draw(gametime);

                    //and make sure all rendertargets are reset
                    foreach (CachedImage ci in usedCachedImages)
                        freeCachedImages.Add(ci);
                    usedCachedImages.Clear();
                }
                else
                {//not so many different sections -> use optimized method
                 //browse through sections
                 //- see if we can re-use stored section from previous frame
                 //- otherwise draw anew
                 //- store new sections into cache
                    List<CachedImage> thisFrameCachedImages = new List<CachedImage>();
                    foreach (var drawablesSection in drawList)
                    {
                        bool identicalImageFound = false;

                        //if there's only 1 drawable, just draw it in a separate buffer
                        if (drawablesSection.Count > 1)
                        {
                            foreach (CachedImage ci in usedCachedImages)
                            {
                                //detect if cached image contains same EDrawables
                                if (ci.drawablesContained.Count == drawablesSection.Count)
                                {
                                    bool allDrawablesContained = true;
                                    foreach (EDrawable d in drawablesSection)
                                    {
                                        if (!ci.drawablesContained.Contains(d))
                                        {
                                            allDrawablesContained = false;
                                            break;
                                        }
                                    }

                                    //if identical --> add to textures to draw
                                    if (allDrawablesContained)
                                    {
                                        identicalImageFound = true;
                                        thisFrameCachedImages.Add(ci);
                                    }
                                }
                            }
                        }

                        //if no identical image was found: need to draw it from scratch into rendertarget
                        if (!identicalImageFound)
                        {
                            CachedImage ci = freeCachedImages.Last();
                            freeCachedImages.Remove(ci);

                            ci.drawablesContained = drawablesSection;
                            thisFrameCachedImages.Add(ci);

                            GraphicsDevice.SetRenderTarget(ci.renderTarget);
                            GraphicsDevice.Clear(Color.Transparent);

                            for (int i = 0; i < drawablesSection.Count; i++)
                                drawablesSection[i].Draw(gametime);
                        }
                    }

                    //at this point, all sections have either been re-used from the previous frame, or have been drawn anew
                    //so draw them all after each other to the screen
                    GraphicsDevice.SetRenderTarget(null);
                    GraphicsDevice.Clear(Color.Black);

                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                    if (false) //debugging purpose
                    {
                        for (int i = 0; i < thisFrameCachedImages.Count; i++)
                            spriteBatch.Draw(thisFrameCachedImages[i].renderTarget, new Rectangle(300 * (i % 3), 300 * (i / 3), 300, 300), Color.LightGreen);

                        for (int i = 0; i < thisFrameCachedImages.Count; i++)
                            spriteBatch.Draw(thisFrameCachedImages[i].renderTarget, new Rectangle(0, 600, 400, 400), Color.White);
                    }
                    else
                    {
                        for (int i = 0; i < thisFrameCachedImages.Count; i++)
                            spriteBatch.Draw(thisFrameCachedImages[i].renderTarget, new Vector2(), new Color(1f,1f,1,1));
                    }
                    spriteBatch.End();

                    //prep for next frame
                    foreach (CachedImage ci in usedCachedImages)
                        if (!thisFrameCachedImages.Contains(ci))
                            freeCachedImages.Add(ci);
                    usedCachedImages = thisFrameCachedImages;
                }

#if DEBUG
                foreach (List<EDrawable> list in drawList)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var v in list)
                        sb.Append(v.ToString() + " ");
                    //DebugTextList.Add(list.Count.ToString("000 : ") + sb.ToString());
                }
#endif
            }
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            else //immediate render mode
            {
                if (freeCachedImages != null)
                    DisposeRenderTargets();

                Gui.DrawPropagating(gametime);
            }

            if (processingScreenshot)
            {
                //find unique filename
                List<string> directoryContents = System.IO.Directory.EnumerateFiles(LabNation.Common.Utils.StoragePath).ToList();
                string screenshotFilename = Utils.GenerateUniqueNumberedFilename(
                    System.IO.Path.Combine(LabNation.Common.Utils.StoragePath, "LabNation_Screenshot.png"),
                    directoryContents);

                //save screenshot to file
                try
                {
                    using (System.IO.FileStream fs = new System.IO.FileStream(screenshotFilename, System.IO.FileMode.Create))
                    {
                        MonoGame.Utilities.Png.PngWriter writer = new MonoGame.Utilities.Png.PngWriter();
                        writer.Write(screenshotRenderTarget, fs);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("Could not write screenshot to " + screenshotFilename+ "!. Error: "+e.Message);
                }

                //re-activate backbuffer and draw image
                GraphicsDevice.SetRenderTarget(null);
                GraphicsDevice.Clear(Color.Black);
                using (SpriteBatch sBatch = new SpriteBatch(GraphicsDevice))
                {
                    sBatch.Begin(SpriteSortMode.Deferred);
                    sBatch.Draw(screenshotRenderTarget, new Vector2());
                    sBatch.End();
                }

                screenshotRenderTarget.Dispose();

                UIHandler.ShowScreenshotSavedToast(screenshotFilename);
            }

#if DEBUG
            //prepare device
            Effect.VertexColorEnabled = true;
			GraphicsDevice.RasterizerState = RasterizerState.CullNone;

            SpriteBatch.Begin ();            

            for (int i = 0; i < DebugTextList.Count; i++)
				SpriteBatch.DrawString (measurementTitleFont, DebugTextList [i], new Vector2 (200, 90 + i * 20), Color.Blue);
            for (int i = 0; i < DebugPermanentTextList.Count; i++)
                SpriteBatch.DrawString(measurementTitleFont, DebugPermanentTextList[i], new Vector2(600, 90 + i * 20), Color.DarkBlue);

            SpriteBatch.End ();
#endif
        }               

        public void OnResize(bool checkUiScale)
        {
            if (!initialised) return;
            AllocateRenderTargets();
            if(checkUiScale)
				UIHandler.SetGuiScale(null, UIHandler.PreferredUiScale);
        }

        public void MouseHover(Point location)
        {
            UIHandler.UpdateAllIntervalVisibilities(location);
        }

#endregion

#region App lifecycle

        public void Pause()
        {
            if(!initialised)
                return;
            UIHandler.PauseScope();
			Settings.SaveCurrent(Settings.IntersessionSettingsId, UIHandler.scope);
        }

        public void Resume()
        {
            if(!initialised)
                return;
            UIHandler.ResumeScope();
        }

        //FIXME: should be handled by dispose() of this class
		public void Stop ()
		{
			this.UIHandler.Cleanup ();
            if(this.filelog != null)
                this.filelog.Stop();
		}

        public Rectangle WindowPositionAndSize
        {
            get { return Settings.CurrentRuntime.windowPositionAndSize; }
            set { Settings.CurrentRuntime.windowPositionAndSize = value; }
        }
#endregion
    }
}
