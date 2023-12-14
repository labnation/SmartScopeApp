using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;



using LabNation.Common;
using Newtonsoft.Json;

#if ANDROID
using System.IO;
using Android.Content;
#endif


using ESuite.Drawables;


namespace ESuite
{
    internal partial class UIHandler
    {
        #region Check for update

		#if WINDOWS || MONOMAC || LINUX || ANDROID

        class Package
        {
            public int major = 0;
            public int minor = 0;
            public int build = 0;
            public int revision = 0;
            public string url = "";
			public string md5 = "";
            public Version GetVersion()
            {
                return new Version(major, minor, build, revision);
            }
        }

        internal void SetAutoUpdate(bool autoUpdate)
        {
            Settings.Current.AutoUpdate = autoUpdate;

            if (AutoUpdateTimer == null)
                AutoUpdateTimer = new Timer(new TimerCallback(checkForUpdatesSilently), null, System.Threading.Timeout.Infinite, 0);
            AutoUpdateTimer.Change(autoUpdate ? 0 : System.Threading.Timeout.Infinite, AUTO_UPDATE_INTERVAL);
        }

        void checkForUpdatesSilently(object arg = null)
        {
            Logger.Debug("Checking for update...");
            CheckForUpdates(true);
        }

		WebClient webclient = new WebClient();
        internal void CheckForUpdates(bool silent)
        {
            if (!silent)
                ShowScreenWideDialog("Checking...");

#if ANDROID
			string packageInstaller = context.PackageManager.GetInstallerPackageName(context.PackageName);
			Logger.Info("Package installer = " + packageInstaller);
			if(packageInstaller == "com.android.vending")
			{
				Logger.Info("App installed through play store - not checking for updates");
				if(!silent)
					QueueCallback(UICallbacks.ShowDialog, "Check for updates through the Google Play store");
				return;
			} 
			else if(packageInstaller != null && packageInstaller != "")
			{
				Logger.Warn("Unknown package installer");
				if(!silent)
					QueueCallback(UICallbacks.ShowDialog, "In-app checking for updates not supported");
				return;
			}
#endif
			while (webclient.IsBusy)
			{
				webclient.CancelAsync();
				Thread.Sleep(10);
			}
			
            if (silent)
                webclient.DownloadStringCompleted += CheckForUpdatesStep1Silent;
            else
                webclient.DownloadStringCompleted += CheckForUpdatesStep1;
            try
            {
#if WINDOWS || MONOMAC || LINUX || ANDROID
				string updateUrl = "https://" + SITE_URL + "/package/SmartScope/" +
#if MONOMAC
		     "MacOS"
#elif WINDOWS 
    #if DIRECTX
            "Windows"
    #else
			"WindowsGL"
    #endif
#elif LINUX
			"Linux"
#elif ANDROID
			"Android"
#endif
		     + "/latest" + (currentVersion.Major >= 2000 ? "_unstable" : "");
#endif
                if (updateUrl == null)
                    throw new Exception("No update checking URL defined for your platform (" + Environment.OSVersion.Platform.ToString("G") + ")");
                Uri url = new Uri(updateUrl);
				Logger.Info("Update url = " + updateUrl);
				webclient.DownloadStringAsync(url);
            }
            catch (Exception e)
            {
                if (!silent)
                    ShowDialog("Something went wrong checking for updates\n" + e.Message);
            }
        }
        void CheckForUpdatesStep1(object sender, System.Net.DownloadStringCompletedEventArgs e)
        {
            ((WebClient)sender).DownloadStringCompleted -= CheckForUpdatesStep1;
            if (e.Error == null)
                QueueCallback(CheckForUpdatesStep2, new object[] { e.Result, false });
            else
            	QueueCallback(UICallbacks.ShowDialog, "Something went wrong checking for updates\n" + e.Error.Message);
        }
        void CheckForUpdatesStep1Silent(object sender, System.Net.DownloadStringCompletedEventArgs e)
        {
            ((WebClient)sender).DownloadStringCompleted -= CheckForUpdatesStep1Silent;
            if (e.Error == null)
                QueueCallback(CheckForUpdatesStep2, new object[] { e.Result, true });
        }
        void CheckForUpdatesStep2(EDrawable sender, object arg)
        {
#if !__IOS__
            object[] args = (object[])arg;
            string json = (string)args[0];
            bool silent = (bool)args[1];
            try
            {
                Package lastPackage = JsonConvert.DeserializeObject<Package>(json);
            
                if (currentVersion < lastPackage.GetVersion())
                {
                    ShowScreenWideDialog("New version available. Download now?", new List<ButtonInfo>
                    {
                        new ButtonInfo("No, thanks", UICallbacks.HideDialog),
						new ButtonInfo("Sure!",
                        #if ANDROID
						this.DownloadWithProgressReport, lastPackage.url
                        #else
                        UICallbacks.OpenUrl, new object[] {lastPackage.url, new DrawableCallback(UICallbacks.HideDialog) }
                       	#endif
						)
                    });
                }
                else if (!silent)
                {
                    ShowDialog("All up to date!");
                }
            }
            catch (Exception e)
            {
                if (!silent)
                    ShowDialog("Something went wrong checking for updates\n" + e.Message);
            }
#endif
        }

		#if ANDROID
		public void DownloadWithProgressReport(EDrawable sender, object updateUrl)
		{
			EDialogProgress progressDialog = ShowProgressDialog("Downloading update", 0f);

			string filename = Path.Combine(LabNation.Common.Utils.StoragePath, "smartscope-update.apk");
			WebClient webclient = new WebClient();
            webclient.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e) 
            {
          		progressDialog.Progress = (float)e.ProgressPercentage / 100f;
            };
            webclient.DownloadFileCompleted += delegate(object s, System.ComponentModel.AsyncCompletedEventArgs e) {
            	this.QueueCallback(InstallApk, filename);
            };
             
            try
            {
                Uri url = new Uri((string)updateUrl);
                webclient.DownloadFileAsync(url, filename);
            }
            catch (Exception e)
            {
                ShowDialog("Something went wrong downloading the update\n" + e.Message);
            }
		}

		public void InstallApk(EDrawable sender, object filename)
		{
			Intent intent = new Intent(Intent.ActionView);
			Android.Net.Uri uri = Android.Net.Uri.FromFile(new Java.IO.File((string)filename));
			intent.SetDataAndType(uri, "application/vnd.android.package-archive");
			intent.SetFlags(ActivityFlags.NewTask);
			context.StartActivity(intent);
        }
        #endif

		#endif

		internal void ResetSettingsAndQuit()
		{
			saveSettingsOnExit = false;
			Settings.Current.Reset();
			Settings.SaveCurrent(Settings.IntersessionSettingsId, this.scope);
			UICallbacks.Quit(null, null);
		}

        #endregion
    }
}
