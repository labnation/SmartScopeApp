using System;

namespace ESuite
{
	public static class CrashLogger
	{
#if ANDROID
		public static Android.Content.Context Context;
#endif
		public static void GlobalUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = default(Exception);
			ex = (Exception)e.ExceptionObject;

			DumpExceptionDetails (ex);
		}

        private static Version currentVersion
        {
            get
            {
                return
#if ANDROID
				new Version(Context.PackageManager.GetPackageInfo (Context.PackageName, 0).VersionName);
#elif IOS
            	new Version(Foundation.NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString").ToString());
#else
 System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
#endif
            }
        }

        private static String OSVersion
        {
            get
            {
                return
#if ANDROID
				"Android";
#elif IOS
            	"iOS";
#elif WINDOWS
                "Windows " + System.Environment.OSVersion.VersionString;
#elif LINUX
                "Linux";
#else
                "UNKNOWN";
#endif
            }
        }

		public static void DumpExceptionDetails(Exception ex)
		{
			DateTime now = DateTime.Now;
			string filename = "LabNation_CrashReport_" + now.Year.ToString("0000") + now.Month.ToString("00") + now.Day.ToString("00") + now.Hour.ToString("00") + now.Minute.ToString("00") + now.Second.ToString("00") + now.Millisecond.ToString("000") + ".txt";
			string fullPath = System.IO.Path.Combine(LabNation.Common.Utils.StoragePath, filename);
            try
            {

                System.IO.StreamWriter writer = new System.IO.StreamWriter(new System.IO.FileStream(fullPath, System.IO.FileMode.Append));

                writer.WriteLine("-----------------------------------------------------------------------------------------");
                writer.WriteLine("------------------------------         CRASH REPORT         ------------------------------");
                writer.WriteLine("Timestamp: " + now.ToLocalTime());
                while (ex != null)
                {
                    try
                    {
                        writer.WriteLine("SmartScopeApp version: " + currentVersion.ToString());
                        writer.WriteLine("OS: " + OSVersion);
                        writer.WriteLine("Error message: " + ex.Message);
                        writer.WriteLine("Source: " + ex.Source);
                        writer.WriteLine("TargetSite: " + ex.TargetSite.Name);
                        writer.WriteLine("StackTrace: " + ex.StackTrace);
                        writer.WriteLine("-----------------------------------------------------------------------------------------");
                    }
                    catch
                    { }

                    ex = ex.InnerException;
                }

                writer.Flush();
                writer.Close();
            }
            catch { }

			LabNation.Common.Logger.Error(": !!! CRASH !!! Details saved to " + fullPath + ", please send that file to info@lab-nation.com so we can fix it");
			LabNation.Common.FileLogger.StopAll();
		}
	}
}

