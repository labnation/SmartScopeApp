using System;
#if WINDOWS && DEBUG
using System.Windows.Forms;
#elif MONOMAC
using AppKit;
using Foundation;
#elif IOS
using Foundation;
using UIKit;
#endif

namespace ESuite
{
#if MONOMAC
    class Program
    {
        static void Main(string[] args)
        {
            NSApplication.Init();
            NSApplication.SharedApplication.Delegate = new AppDelegate();
            NSApplication.Main(args);
        }
    }

    class AppDelegate : NSApplicationDelegate
    {
        private SmartScopeGui game;
        public override void DidFinishLaunching(NSNotification notification)
        {
            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            // Handler for unhandled exceptions.
            currentDomain.UnhandledException += CrashLogger.GlobalUnhandledExceptionHandler;
            game = new SmartScopeGui();
            game.Run();
            game.Dispose();
        }
        public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
        {
            return true;
        }
    }
#elif IOS
    class Program
    {
        static void Main (string [] args)
        {
            UIApplication.Main (args,null,"AppDelegate");
        }
    }

	[Register ("AppDelegate")]
	class AppDelegate : UIApplicationDelegate 
	{
		private SmartScopeGui game;

		public override void FinishedLaunching (UIApplication app)
		{
			// Fun begins..
			app.IdleTimerDisabled = true;
			game = new SmartScopeGui();
			game.Run();
		}

		public override void DidEnterBackground(UIApplication application)
		{
			game.Pause();
		}

		public override void OnActivated(UIApplication application)
		{
			game.Resume();
		}
	}
#elif !WINDOWS_PHONE
    /// <summary>
    /// The main class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            AppDomain currentDomain = default(AppDomain);
            currentDomain = AppDomain.CurrentDomain;
            // Handler for unhandled exceptions.
            currentDomain.UnhandledException += CrashLogger.GlobalUnhandledExceptionHandler;

#if WINDOWS && DEBUG
            //Winer case: on Windows the Crashlogger gets the right StoragePath, but afterwards the Storagepath is wrong; perhaps because this crashlogger is running in a differen domain. So in this new approach this sets the StoragePath.
            Type type = typeof(DummyClassToGetStoragePathCorrect);
            currentDomain.CreateInstanceAndUnwrap(type.Assembly.FullName, type.FullName);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new FormMain());
#else
				using (var game = new SmartScopeGui())
					game.Run();
#endif
        }
    }

#if WINDOWS
    //Winer case: on Windows the Crashlogger gets the right StoragePath, but afterwards the Storagepath is wrong; perhaps because this crashlogger is running in a differen domain. So in this new approach this sets the StoragePath.
    public class DummyClassToGetStoragePathCorrect
    {
        public DummyClassToGetStoragePathCorrect()
        {
            string dummy = LabNation.Common.Utils.StoragePath;
        }
    }
#endif
#endif
}