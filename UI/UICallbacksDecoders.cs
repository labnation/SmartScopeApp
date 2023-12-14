using ESuite.Drawables;
using System;
using System.Collections.Generic;
using System.Linq;
using DropNet.Models;
using DropNet.Exceptions;

namespace ESuite
{
    static partial class UICallbacks
    {
        #region dropbox

        public static void ReloadDecoders(EDrawable sender, object arg)
        {
            uiHandler.LoadDecoders();
            uiHandler.RebuildSideMenu();
        }

        public static void FetchDecodersFromDropbox(EDrawable sender, object arg)
        {
            if (!DropboxAuthenticated(sender, new DrawableCallback(FetchDecodersFromDropbox, arg)))
                return;
            
            uiHandler.dropboxStorage.GetFilesInDirectory("Plugins", new DrawableCallbackDelegate(FetchDecodersFromDropboxStep2));
        }
		public static void FetchDecodersFromDropboxStep2(EDrawable sender, object arg)
        {
			if (arg is List<MetaData>) {
				List<MetaData> directoryContents = (arg as List<MetaData>).Where (x => x.Extension.ToUpper () == ".DLL").ToList ();
				if (directoryContents.Count == 0) {
					ShowDialog (null, "The Plugins folder seems to contain no DLL files");
					return;
				}
				int bytes = directoryContents.Aggregate (0, (a, b) => a + (int)b.Bytes);
				EDialogProgress progressDialog = uiHandler.ShowProgressDialog (
					                                 String.Format ("Downloading dropbox decoders ({0})", LabNation.Common.Utils.siPrint(bytes, 1, 4, "B")),
					                                 0f);
        		
				uiHandler.dropboxStorage.DownloadToFolderAsync (
					directoryContents, 
					LabNation.Common.Utils.PluginPathDropbox, 
					(progress) => progressDialog.Progress = progress,
					(exception) => uiHandler.QueueCallback ((s, e) => {
						HideDialog (s, e);
						uiHandler.LoadDecoders ();
						uiHandler.RebuildSideMenu ();
						if (e is Exception) {
							ShowDialog (s, "Downloading dropbox decoders failed\n" + (e as Exception).Message);
						} else {
							ShowDialog (s, "Downloading dropbox decoders done");
						}
					}, exception)
				);
			} else if (arg is Exception) {
				if (arg is DropboxRestException) {
					string message;
					List<ButtonInfo> buttons = new List<ButtonInfo> () {
						new ButtonInfo ("OK", HideDialog)
					};
					var dbe = arg as DropboxRestException;
					if (
						dbe.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
						dbe.StatusCode == System.Net.HttpStatusCode.Forbidden)
						{
							message = "I don't seem to have permission to access your dropbox";
							buttons.Insert(0,
								new ButtonInfo(
									"Authenticate",
									DropboxAuthenticateStep4,
								new DrawableCallback(FetchDecodersFromDropbox))
							);
						}
						else
						{
							message = "Something went wrong trying to reach dropbox";
							buttons.Insert(0, new ButtonInfo("Retry", FetchDecodersFromDropbox));
						}
					uiHandler.ShowDialog(message, buttons);
				} else {
					ShowDialog (sender, "Downloading dropbox decoders failed\n\n" + (arg as Exception).Message);
				}
			}
        }


        #endregion
    }
}
