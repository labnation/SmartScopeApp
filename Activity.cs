using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using System.Threading;
using LabNation.Common;
using Android.Content.PM;

namespace ESuite
{
    [Activity(Label = "SmartScope",
        MainLauncher = true,
        Icon = "@drawable/icon",
        Theme = "@style/Theme.Splash",
        AlwaysRetainTaskState = true,
        LaunchMode = Android.Content.PM.LaunchMode.SingleInstance,
        ScreenOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape,
        ConfigurationChanges = Android.Content.PM.ConfigChanges.Orientation |
        Android.Content.PM.ConfigChanges.KeyboardHidden |
        Android.Content.PM.ConfigChanges.Keyboard)]
    [IntentFilter(new[] { "android.hardware.usb.action.USB_DEVICE_ATTACHED" })]
    [MetaData("android.hardware.usb.action.USB_DEVICE_ATTACHED", Resource = "@xml/device_filter")]
    public class Activity : AndroidGameActivity
    {
        private ConcurrentQueue<LabNation.Common.LogMessage> logQueue;
        private SmartScopeGui g;
        private const string TAG = "applog";
        private bool isDestroyed = false;
        private bool isPaused = false;
        private Thread logThread;

        protected override void OnCreate(Bundle bundle)
        {
            AndroidEnvironment.UnhandledExceptionRaiser += HandleAndroidException;

            logQueue = new ConcurrentQueue<LabNation.Common.LogMessage>();
            logThread = new Thread(AndroidLoggerDequeuer);
            logThread.Name = "Android log";
            logThread.Start();
            LabNation.Common.Logger.AddQueue(logQueue);

            base.OnCreate(bundle);

            // Create our OpenGL view, and display it
            g = new SmartScopeGui(this.BaseContext);
            SetContentView((View)g.Services.GetService(typeof(View)));
            g.Run();
            this.Window.SetFlags(WindowManagerFlags.KeepScreenOn, WindowManagerFlags.KeepScreenOn);

            string[] perms =
            {
                "android.permission.INTERNET",
                "android.permission.WRITE_EXTERNAL_STORAGE",
                "android.permission.RECORD_AUDIO",
                "android.permission.READ_EXTERNAL_STORAGE",
            };

            if (Android.OS.Build.VERSION.SdkInt >= BuildVersionCodes.M)
                RequestPermissions(perms, 0);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            foreach (var p in grantResults)
            {
                if (p == Permission.Denied)
                {
                    Logger.Error("Cannot live without all permissions");
                    Finish();
                }
            }
        }


        void HandleAndroidException(object sender, RaiseThrowableEventArgs e)
        {
            CrashLogger.Context = this.BaseContext;
            CrashLogger.DumpExceptionDetails(e.Exception);
        }

        protected override void OnPause()
        {
            isPaused = true;
            base.OnPause();
            g.Pause();
        }

        protected override void OnResume()
        {
            isPaused = false;
            g.Resume();
            base.OnResume();
        }

        protected override void OnDestroy()
        {
            this.isPaused = true;
            this.isDestroyed = true;
            if (!logThread.Join(1000))
                Android.Util.Log.Debug(TAG, "Failed to join logger thread");
            base.OnDestroy();
        }

        private void AndroidLoggerDequeuer()
        {
            LogMessage l;
            while (!isDestroyed)
            {
                System.Threading.Thread.Sleep(100);
                if (isPaused)
                {
                    System.Threading.Thread.Sleep(500);
                    continue;
                }

                while (logQueue.TryDequeue(out l))
                {
                    switch (l.level)
                    {
                        case LogLevel.DEBUG:
                            Android.Util.Log.Debug(TAG, l.message);
                            break;
                        case LogLevel.ERROR:
                            Android.Util.Log.Error(TAG, l.message);
                            break;
                        case LogLevel.WARN:
                            Android.Util.Log.Warn(TAG, l.message);
                            break;
                        case LogLevel.INFO:
                            Android.Util.Log.Info(TAG, l.message);
                            break;
                        default:
                            Android.Util.Log.Wtf(TAG, l.message);
                            break;
                    }
                }
            }
        }
    }
}


