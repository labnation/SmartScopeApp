#region Using Statements
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using LabNation.DeviceInterface.Devices;
using System.Collections.Generic;
#if IOS
using MonoTouch;
using UIKit;
using Foundation;
using System.Runtime.InteropServices;
using ObjCRuntime;
#endif

#endregion

namespace ESuite
{
    /// <summary>
    /// Default Project Template
    /// </summary>
    public class SmartScopeGui : Game
    {

        #region Fields

        GraphicsDeviceManager graphics;
        ScopeApp smartScopeController;

        MouseState lastMouseState;
        Microsoft.Xna.Framework.Input.KeyboardState lastKeyboardState;
        const int DOUBLECLICK_TIMEOUT = 500;
        const int KEYBOARD_DELAY_MS = 250;
        const int KEYBOARD_REPEAT_MS = 30;
        Dictionary<Keys, int> keyTimers = new Dictionary<Keys, int>();
        DateTime previousUpdateTime = DateTime.Now;

        DateTime lastLeftClickTime = DateTime.MinValue;
        bool quitting = false;
        bool exitCalled = false;
        Thread inputThread;
        int requestedWidth = 0; /* Default window size now defined in Settings.windowPositionAndSize! */
        int requestedHeight = 0;
        const int minWidth = 500;
        const int minHeight = 500;
        bool windowResizeRequested = false;

#if ANDROID
        Android.Content.Context context;
#endif
#if WINDOWS && DEBUG
        LabNation.DeviceInterface.Devices.DeviceConnectHandler connectHandler;
#endif

        #endregion

        #region Initialization

        public SmartScopeGui(
#if ANDROID
            Android.Content.Context context
#endif
#if WINDOWS && DEBUG
            LabNation.DeviceInterface.Devices.DeviceConnectHandler connectHandler
#endif
        )
        {
#if ANDROID
            this.context = context;
#endif
#if WINDOWS && DEBUG
            this.connectHandler = connectHandler;
#endif

            graphics = new GraphicsDeviceManager(this);
#if IOS
			Directory.SetCurrentDirectory(Foundation.NSBundle.MainBundle.BundlePath);
#elif LINUX
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
#endif

#if ANDROID
            Content.RootDirectory = "";
#else
            Content.RootDirectory = "Content";
#endif

            //set PPI in Scaler
            smartScopeController = new ScopeApp();
            lastMouseState = Mouse.GetState();
            lastKeyboardState = Keyboard.GetState();

            //load intersessions settings from file
            Settings.Load(Settings.IntersessionSettingsId, null);

            this.Exiting += Cleanup;
        }

        private void InitializeWindow()
        {
#if !ANDROID && !IOS
            //act in case of low screen resolotion
            graphics.IsFullScreen = false;
            this.Window.AllowUserResizing = true;
            this.Window.ClientSizeChanged += new EventHandler<EventArgs>(Window_ClientSizeChanged);
            IsMouseVisible = true;


            //fetch last store window location and size
            requestedWidth = smartScopeController.WindowPositionAndSize.Width;
            requestedHeight = smartScopeController.WindowPositionAndSize.Height;

            Point requestedPosition = new Point(smartScopeController.WindowPositionAndSize.Left, smartScopeController.WindowPositionAndSize.Top);
            //make sure window doesn't start out of screen (in case resolution or screen is changed between sessions)
            //in such case: make sure top-right corner is visible
            if (requestedPosition.X + requestedWidth > GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width)
                requestedPosition.X = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width - requestedWidth;
            if (requestedPosition.Y + requestedHeight > GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height)
                requestedPosition.Y = 10;
            if (requestedPosition.X < 0)
                requestedPosition.X = 0;
            if (requestedPosition.Y < 0)
                requestedPosition.Y = 0;
#if !MONOMAC
            //commit
            Window.Position = requestedPosition;
#endif
            ResizeBackBuffer(requestedWidth, requestedHeight);

#else
            graphics.IsFullScreen = true;
			IsMouseVisible = false;
			graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
			graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
			graphics.SupportedOrientations = DisplayOrientation.LandscapeRight | DisplayOrientation.LandscapeLeft;
			graphics.ApplyChanges();
#endif
        }

        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
#if !__IOS__ && !ANDROID
            Microsoft.Xna.Framework.GameWindow window = (Microsoft.Xna.Framework.GameWindow)sender;

            //only update when there's a change
#if WINDOWS
            if (window.Position.X != -32000 && window.Position.Y != -32000)
#endif
            {
                if ((requestedWidth != window.ClientBounds.Width) || (requestedHeight != window.ClientBounds.Height))
                {
                    requestedWidth = window.ClientBounds.Width;
                    requestedHeight = window.ClientBounds.Height;
                    this.windowResizeRequested = true;
                }
            }
#endif
        }

        private void ResizeBackBuffer(int newWidth, int newHeight)
        {
            //clip in case the window is made too small
            if (newWidth < minWidth) newWidth = minWidth;
            if (newHeight < minHeight) newHeight = minHeight;

            //preferred fixed screensize
            graphics.PreferredBackBufferWidth = newWidth;
            graphics.PreferredBackBufferHeight = newHeight;
            graphics.ApplyChanges();
        }

        /// <summary>
        /// Overridden from the base Game.Initialize. Once the GraphicsDevice is setup,
        /// we'll use the viewport to initialize some values.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            InitializeWindow();
#if DEBUG
            LabNation.Common.LogLevel level = LabNation.Common.LogLevel.DEBUG;
#else
            LabNation.Common.LogLevel level = LabNation.Common.LogLevel.WARN;
#endif
            int dpi = GetDpi();
            smartScopeController.Initialize(
#if ANDROID
                context,
#endif
                GraphicsDevice, Content,
#if WINDOWS && DEBUG
                connectHandler,
#endif
                level, dpi
            );
#if !ANDROID && !IOS
            inputThread = new Thread(handleInputThread);
            inputThread.Name = "SmartScope input handling thread";
            inputThread.Start();
#endif
        }

        private int GetDpi()
        {
            int dpi = 96;
#if WINDOWS && DIRECTX
			dpi = this.PixelsPerInch;
#elif ANDROID
			dpi = (int)context.Resources.DisplayMetrics.DensityDpi;
#elif IOS
			string iDeviceVersion = iOSHardware.iDeviceVersion;
			LabNation.Common.Logger.Info("Detected iDevice named " + iDeviceVersion);
			if (iOSHardware.iDeviceDpi.ContainsKey(iDeviceVersion))
				dpi = iOSHardware.iDeviceDpi[iDeviceVersion];
#else
            dpi = 96;
#endif
            LabNation.Common.Logger.Info("Got DPI " + dpi);
            return dpi;
        }

        public void Pause()
        {
            //Make scope go in low power mode
            smartScopeController.Pause();
        }

        public void Resume()
        {
            //Restore scope
            smartScopeController.Resume();
        }

        /// <summary>
        /// Load your graphics content.
        /// </summary>
        protected override void LoadContent()
        {
        }

        #endregion

        #region Update and Draw

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (smartScopeController.Quitting)
            {
                //menu->settings->quit->ssc.Quitting=true->Cleanup->terminateScopeThreads + quitting=true->see below:Exit()
                smartScopeController.Quitting = false;
                this.Cleanup(null, null);
            }

            if (quitting)
            {
                if (!exitCalled)
                {
                    exitCalled = true;
#if IOS
					throw new Exception("iOS doesn't allow programmatic closing, so we throw an exception");
#else
                    Exit();
#endif
#if ANDROID
                    System.Environment.Exit(0); //added for cases where the above doesn't work reliably (android)
#endif
                }
                base.Update(gameTime);
                return;
            }
            //backbuffer must be updated during the Update routine. Hence this construction
            if (windowResizeRequested)
            {
                bool tryRestoreUiScale = true;
                Point position =
#if WINDOWS
					Window.Position;
#else
                    new Point();
#endif

#if WINDOWS
                this.ResizeBackBuffer(requestedWidth, requestedHeight);
#endif
                if (position.X == -32000 && position.Y == -32000) //Minimized
                    tryRestoreUiScale = false;
                else //not minimized - store position
                    smartScopeController.WindowPositionAndSize = new Rectangle(position.X, position.Y, Window.ClientBounds.Width, Window.ClientBounds.Height);

                if (smartScopeController != null)
                    smartScopeController.OnResize(tryRestoreUiScale);
                windowResizeRequested = false;
            }
#if !ANDROID && !IOS
            handleKeyboardInput();
#endif
        }

        public void Cleanup(object sender, EventArgs e)
        {
            //2 origins:
            // 1. app killed -> Exiting -> Cleanup
            // 2. menu->settings->quit->ssc.Quitting=true->Cleanup
            //what Cleanup does: terminateScopeThreads + quitting=true->see below:Exit()

            quitting = true;
            smartScopeController.Stop();    //might get called twice. Different on different OSes
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            smartScopeController.Update(gameTime);
            graphics.GraphicsDevice.Clear(Color.Black);

            base.Draw(gameTime);
            smartScopeController.Draw(gameTime);
        }

        #endregion


        #region mouse and keyboard
#if !IOS && !ANDROID
        private void handleInputThread()
        {
            while (!quitting)
            {
                handleMouseInput();
                Thread.Sleep(1);
            }
        }

        private void handleMouseInput()
        {
            try
            {
                MouseState s = Mouse.GetState();

                if (s.Position != lastMouseState.Position)
                    mouseMove(s, lastKeyboardState);
                if (
                    (s.LeftButton == ButtonState.Released && s.LeftButton != lastMouseState.LeftButton)
                    ||
                    (s.RightButton == ButtonState.Released && s.RightButton != lastMouseState.RightButton))
                    mouseUp(s.Position, s.RightButton == ButtonState.Released && s.RightButton != lastMouseState.RightButton);
                int scrollDelta = lastMouseState.ScrollWheelValue - s.ScrollWheelValue;
                if (scrollDelta != 0)
                    mouseScroll(s, scrollDelta);

                lastMouseState = s;
            }
            catch (InvalidOperationException) { }
        }

        private void handleKeyboardInput()
        {

            try
            {
                Microsoft.Xna.Framework.Input.KeyboardState k = Keyboard.GetState();
                Keys[] pressedKeys = k.GetPressedKeys();

                int msecsSincePreviousUpdate = (int)DateTime.Now.Subtract(previousUpdateTime).TotalMilliseconds;
                previousUpdateTime = DateTime.Now;
                AdvanceKeyboardTimers(pressedKeys, msecsSincePreviousUpdate);

                if (pressedKeys.Length > 0)
                {
                    List<Keys> keyDown = new List<Keys>();
                    List<Keys> keyUp = new List<Keys>();
                    foreach (Keys key in pressedKeys)
                    {
                        if (KeyDown(k, key))
                        {
                            keyDown.Add(key);
                            LabNation.Common.Logger.Debug("key " + key.ToString("G") + " Pressed");
                        }
                        if (KeyUp(k, key))
                            keyUp.Add(key);
                    }

                    Point mousePos = new Point();
                    if (lastMouseState != null)
                        mousePos = lastMouseState.Position;
                    smartScopeController.HandleKey(pressedKeys.ToList(), keyDown, keyUp, mousePos);
                }

                lastKeyboardState = k;
            }
            catch (InvalidOperationException) { }
        }

        bool KeyPressed(Microsoft.Xna.Framework.Input.KeyboardState k, Keys key)
        {
            return k.GetPressedKeys().Contains(key);
        }

        void AdvanceKeyboardTimers(Keys[] pressedKeys, int msecs)
        {
            keyTimers = keyTimers.Where(k => pressedKeys.Contains(k.Key))
                .ToDictionary(p => p.Key, p => p.Value - msecs);
        }

        bool KeyDown(Microsoft.Xna.Framework.Input.KeyboardState k, Keys key)
        {
            bool currentlyPressed = KeyPressed(k, key);

            if (currentlyPressed)
            {
                if (keyTimers.ContainsKey(key))
                {
                    if (keyTimers[key] <= 0)
                    {
                        keyTimers[key] = KEYBOARD_REPEAT_MS;
                        return true;//repeat timer underflow
                    }
                    else
                        return false; //wait for delay or repeat
                }
                else
                {
                    keyTimers.Add(key, KEYBOARD_DELAY_MS);
                    return true; //just now pressed
                }
            }
            else
            {
                if (keyTimers.ContainsKey(key))
                    keyTimers.Remove(key);

                return false;//not pressed
            }
        }
        bool KeyUp(Microsoft.Xna.Framework.Input.KeyboardState k, Keys key)
        {
            return KeyPressed(lastKeyboardState, key) && !KeyPressed(k, key);
        }

        private enum DRAGTYPE
        {
            REGULAR,
            MODIFIED,
            UNDEFINED
        }

        //mouse control
        private DRAGTYPE dragtype = DRAGTYPE.UNDEFINED;
        private DateTime lastClickMoment = DateTime.Now;
        private Point lastMouseLocation;
        private Point startingMouseLocation;

        private void mouseMove(MouseState s, Microsoft.Xna.Framework.Input.KeyboardState k)
        {
            //in case this window does not have focus
            if (!this.IsActive) return;
            if (smartScopeController == null) return;
            if (s == null) return;
            if (k == null) return;
            if (s.Position == null) return;
            if (s.LeftButton == null) return;       //cannot be false actually..
            if (s.RightButton == null) return;      //cannot be false actually..
            if (dragtype == null) return;           //cannot be false actually..
                                                    //s is not null, otherwise there would have been a crash in the calling code already!

            s = mouseMove1(s);
            s = mouseMove2(s, k);

            if (dragtype == DRAGTYPE.UNDEFINED)
                return;
            s = mouseMove3(s);
            mouseMove4(s);
        }

        private void mouseMove4(MouseState s)
        {
            lastMouseLocation = s.Position;
        }

        private MouseState mouseMove3(MouseState s)
        {
            switch (dragtype)
            {
                case DRAGTYPE.MODIFIED:
                    smartScopeController.AddFreeDragModified(startingMouseLocation, lastMouseLocation, s.Position);
                    break;
                case DRAGTYPE.REGULAR:
                    smartScopeController.AddFreeDragRegular(startingMouseLocation, lastMouseLocation, s.Position);
                    break;
            }

            return s;
        }

        private MouseState mouseMove2(MouseState s, Microsoft.Xna.Framework.Input.KeyboardState k)
        {
            //Start of drag
            if (dragtype == DRAGTYPE.UNDEFINED && (s.LeftButton == ButtonState.Pressed || s.RightButton == ButtonState.Pressed))
            {
                bool shiftDown = KeyPressed(k, Keys.LeftShift) || KeyPressed(k, Keys.RightShift);
                bool ctrlDown = KeyPressed(k, Keys.LeftControl) || KeyPressed(k, Keys.RightControl);

                startingMouseLocation = lastMouseLocation = s.Position;
                if (
                    (s.LeftButton == ButtonState.Pressed && shiftDown) ||
                    (s.RightButton == ButtonState.Pressed))
                    dragtype = DRAGTYPE.MODIFIED;
                else
                    dragtype = DRAGTYPE.REGULAR;
            }

            return s;
        }

        private MouseState mouseMove1(MouseState s)
        {
            //to enable intervals
            smartScopeController.MouseHover(s.Position);
            return s;
        }

        private void mouseUp(Point p, bool rightButton)
        {
            //in case this window does not have focus
            if (!this.IsActive) return;

            if (dragtype != DRAGTYPE.UNDEFINED)
            {
                switch (dragtype)
                {
                    case DRAGTYPE.MODIFIED:
                        smartScopeController.FinishFreeDragModified(startingMouseLocation, lastMouseLocation, p);
                        break;
                    case DRAGTYPE.REGULAR:
                        smartScopeController.FinishFreeDragRegular(startingMouseLocation, lastMouseLocation, p);
                        break;
                }
                dragtype = DRAGTYPE.UNDEFINED;
            }
            //Left click
            else if (!rightButton)
            {
                if ((DateTime.Now - lastLeftClickTime).TotalMilliseconds < DOUBLECLICK_TIMEOUT)
                {
                    smartScopeController.LeftMouseDoubleClick(p.X, p.Y);
                    lastLeftClickTime = DateTime.MinValue;
                }
                else
                {
                    smartScopeController.LeftMouseClick(p.X, p.Y);
                    lastLeftClickTime = DateTime.Now;
                }
            }
            //Right click
            else
            {
                lastLeftClickTime = DateTime.MinValue;
                smartScopeController.RightMouseClick(p.X, p.Y);
            }
        }

#if MONOMAC
        DateTime lastScrollEvent = DateTime.MinValue;
#endif

        private void mouseScroll(MouseState s, int delta)
        {
            //in case this window does not have focus
            if (!this.IsActive) return;

#if MONOMAC
            //On mac, only allow 5 scroll events per second
            if ((DateTime.Now - lastScrollEvent).TotalMilliseconds < 200)
                return;
            lastScrollEvent = DateTime.Now;
#endif
            bool scrollUp = delta < 0;
            if (lastKeyboardState.GetPressedKeys().Contains(Keys.LeftShift) || lastKeyboardState.GetPressedKeys().Contains(Keys.RightShift))
            {
                if (scrollUp)
                    smartScopeController.ZoomInVertical(s.Position);
                else
                    smartScopeController.ZoomOutVertical(s.Position);
            }
            else
            {
                if (scrollUp)
                    smartScopeController.ZoomInHorizontal(s.Position);
                else
                    smartScopeController.ZoomOutHorizontal(s.Position);
            }
        }
#endif
        #endregion
    }

#if IOS
	static class iOSHardware
	{
		const int DPI_APPLE_TV = 72;
		const int DPI_APPLE_WATCH = 326;
		const int DPI_IPHONE_1 = 163;
		const int DPI_IPHONE_4_5_6 = 326;
		const int DPI_IPHONE_6_PLUS = 401;
        const int DPI_IPOD_1 = 163;
        const int DPI_IPOD_4 = 326;
        const int DPI_IPAD_1_2 = 132;
        const int DPI_IPAD_3_4 = 264;
        const int DPI_IPAD_AIR = 264;
        const int DPI_IPAD_PRO = 264;
        const int DPI_IPAD_MINI_1 = 163;
        const int DPI_IPAD_MINI_2_3_4 = 326;
        const int DPI_SIMULATOR = 264;


		public static Dictionary<string, int> iDeviceDpi = new Dictionary<string, int>()
		{
			// Apple TV
			{"AppleTV2,1", DPI_APPLE_TV }, // Apple TV 2G
			{"AppleTV3,1", DPI_APPLE_TV }, // Apple TV 3G
			{"AppleTV3,2", DPI_APPLE_TV }, // Apple TV 3G
			{"AppleTV5,3", DPI_APPLE_TV }, // Apple TV 4G

			// Apple Watch
			{"Watch1,1", DPI_APPLE_WATCH }, // Apple Watch
			{"Watch1,2", DPI_APPLE_WATCH }, // Apple Watch

			// iPhone
			{"iPhone1,1", DPI_IPHONE_1 }, // iPhone
			{"iPhone1,2", DPI_IPHONE_1 }, // iPhone 3G
			{"iPhone2,1", DPI_IPHONE_1 }, // iPhone 3GS
			{"iPhone3,1", DPI_IPHONE_1 }, // iPhone 4
			{"iPhone3,2", DPI_IPHONE_1 }, // iPhone 4
			{"iPhone3,3", DPI_IPHONE_1 }, // iPhone 4 (CDMA)
			{"iPhone4,1", DPI_IPHONE_4_5_6 }, // iPhone 4S
			{"iPhone5,1", DPI_IPHONE_4_5_6 }, // iPhone 5 (GSM)
			{"iPhone5,2", DPI_IPHONE_4_5_6 }, // iPhone 5 (GSM+CDMA)
			{"iPhone5,3", DPI_IPHONE_4_5_6 }, // iPhone 5C (GSM)
			{"iPhone5,4", DPI_IPHONE_4_5_6 }, // iPhone 5C (GSM+CDMA)
			{"iPhone6,1", DPI_IPHONE_4_5_6 }, // iPhone 5S (GSM)
			{"iPhone6,2", DPI_IPHONE_4_5_6 }, // iPhone 5S (GSM+CDMA)
			{"iPhone7,1", DPI_IPHONE_6_PLUS }, // iPhone 6 Plus
			{"iPhone7,2", DPI_IPHONE_4_5_6 }, // iPhone 6
			{"iPhone8,1", DPI_IPHONE_4_5_6 }, // iPhone 6s
			{"iPhone8,2", DPI_IPHONE_6_PLUS }, // iPhone 6s Plus

			// iPod
			{"iPod1,1", DPI_IPOD_1 }, // iPod Touch
			{"iPod2,1", DPI_IPOD_1 }, // iPod Touch 2G
			{"iPod3,1", DPI_IPOD_1 }, // iPod Touch 3G
			{"iPod4,1", DPI_IPOD_4 }, // iPod Touch 4G
			{"iPod5,1", DPI_IPOD_4 }, // iPod Touch 5G
			{"iPod7,1", DPI_IPOD_4 }, // iPod Touch 6G

			// iPad
			{"iPad1,1", DPI_IPAD_1_2 }, // iPad
			{"iPad2,1", DPI_IPAD_1_2 }, // iPad 2 (WiFi)
			{"iPad2,2", DPI_IPAD_1_2 }, // iPad 2 (GSM)
			{"iPad2,3", DPI_IPAD_1_2 }, // iPad 2 (CDMA)
			{"iPad2,4", DPI_IPAD_1_2 }, // iPad 2 (WiFi)
			{"iPad3,1", DPI_IPAD_3_4 }, // iPad 3 (WiFi)
			{"iPad3,2", DPI_IPAD_3_4 }, // iPad 3 (GSM+CDMA)
			{"iPad3,3", DPI_IPAD_3_4 }, // iPad 3 (GSM)
			{"iPad3,4", DPI_IPAD_3_4 }, // iPad 4 (WiFi)
			{"iPad3,5", DPI_IPAD_3_4 }, // iPad 4 (GSM)
			{"iPad3,6", DPI_IPAD_3_4}, // iPad 4 (GSM+CDMA)
			{"iPad4,1", DPI_IPAD_AIR }, // iPad Air (WiFi)
			{"iPad4,2", DPI_IPAD_AIR }, // iPad Air (Cellular)
			{"iPad4,3", DPI_IPAD_AIR }, // iPad Air
			{"iPad5,3", DPI_IPAD_AIR }, // iPad Air 2 (WiFi)
			{"iPad5,4", DPI_IPAD_AIR }, // iPad Air 2 (Cellular)
			{"iPad6,7", DPI_IPAD_PRO }, // iPad Pro (WiFi)
			{"iPad6,8", DPI_IPAD_PRO }, // iPad Pro (Cellular)

			// iPad Mini
			{"iPad2,5", DPI_IPAD_MINI_1 }, // iPad Mini (WiFi)
			{"iPad2,6", DPI_IPAD_MINI_1 }, // iPad Mini (GSM)
			{"iPad2,7", DPI_IPAD_MINI_1 }, // iPad Mini (GSM+CDMA)
			{"iPad4,4", DPI_IPAD_MINI_2_3_4 }, // iPad Mini 2 (WiFi)
			{"iPad4,5", DPI_IPAD_MINI_2_3_4 }, // iPad Mini 2 (Cellular)
			{"iPad4,6", DPI_IPAD_MINI_2_3_4 }, // iPad Mini 2
			{"iPad4,7", DPI_IPAD_MINI_2_3_4 }, // iPad mini 3 (WiFi)
			{"iPad4,8", DPI_IPAD_MINI_2_3_4 }, // iPad mini 3 (Cellular)
			{"iPad4,9", DPI_IPAD_MINI_2_3_4 }, // iPad mini 3 (China Model)
			{"iPad5,1", DPI_IPAD_MINI_2_3_4 }, // iPad mini 4 (WiFi)
			{"iPad5,2", DPI_IPAD_MINI_2_3_4 }, // iPad mini 4 (Cellular)

			// Simulator
			{"i386",   DPI_SIMULATOR}, // Simulator
		  	{"x86_64", DPI_SIMULATOR }, // Simulator
		};

		[DllImport(Constants.SystemLibrary)]
		internal static extern int sysctlbyname([MarshalAs(UnmanagedType.LPStr)] string property, // name of the property
													  IntPtr output, // output
													  IntPtr oldLen, // IntPtr.Zero
													  IntPtr newp, // IntPtr.Zero
													  uint newlen // 0
			);

		public static string iDeviceVersion
		{
			get
			{
				// get the length of the string that will be returned
				var pLen = Marshal.AllocHGlobal(sizeof(int));
				sysctlbyname("hw.machine", IntPtr.Zero, pLen, IntPtr.Zero, 0);

				var length = Marshal.ReadInt32(pLen);

				// check to see if we got a length
				if (length == 0)
				{
					Marshal.FreeHGlobal(pLen);
					return null;
				}


				// get the hardware string
				var pStr = Marshal.AllocHGlobal(length);
				sysctlbyname("hw.machine", pStr, pLen, IntPtr.Zero, 0);

				// convert the native string into a C# string
				var hardwareStr = Marshal.PtrToStringAnsi(pStr);
				string ret = "" + hardwareStr;

				// cleanup
				Marshal.FreeHGlobal(pLen);
				Marshal.FreeHGlobal(pStr);

				return ret;
			}
		}
	}
#endif

}