using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DropNet;
using DropNet.Models;
using System.IO;
using LabNation.DeviceInterface;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DropNet.Exceptions;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using ESuite.Drawables;
using ICSharpCode.SharpZipLib.Zip;
#if ANDROID
using Android.Content;
#endif

namespace ESuite.DataStorage
{
    class DropboxStorage
    {
        string appKey = "6pgrfimet2ht9zu";
        string appSecret = "j0pn06ro3trbd8g";

        DropNetClient client = null;
        bool sandbox = true;
        UIHandler uiHandler;
        int timeout = 8000;

        public bool authenticated { get; private set; }

        public DropboxStorage(UIHandler uihandler)
        {
            this.uiHandler = uihandler;
            init();
        }

        private void init()
        {
            Logger.Debug("initialising dropbox storage");
            authenticated = false;

            client = new DropNetClient(appKey, appSecret);
            client.UseSandbox = sandbox;
        }

        internal void OpenAuthenticationUrl(DrawableCallbackDelegate callback, object argument)
        {
            init();
            Thread t = new Thread(GetToken);
            t.Start(new object[] {callback, argument});
        }

        private void GetToken(object arg)
        {
            Logger.Debug("getting authentication token");
            object[] args = arg as object[];
            DrawableCallbackDelegate callback = args[0] as DrawableCallbackDelegate;
            object callbackArgument = args[1];
            client.GetTokenAsync(
                success =>
                {
                    Logger.Debug("opening browser URL");
                    string url = client.BuildAuthorizeUrl(success);
                    #if ANDROID
                    var uri = Android.Net.Uri.Parse (url);
                    var intent = new Intent (Intent.ActionView, uri); 
                    intent.AddFlags(ActivityFlags.NewTask);
                    uiHandler.context.StartActivity(intent); 
                    #else
                    Process.Start(url);
                    #endif
                    //Give the browser some time to open before showing the
                    //return message
                    System.Threading.Thread.Sleep(2000);
                    uiHandler.QueueCallback(
                        callback,
                        new object[] {callbackArgument, success});
                },
                failure =>
                {
                    Logger.Debug("failed to get token: " + failure.Message);
                    uiHandler.QueueCallback(callback, new object[] {callbackArgument, failure});
                });
        }

		internal void Authenticate(Action getTokenCallback, DrawableCallback callback, Action<float> progressreport = null, DropNet.Models.UserLogin login = null)
        {
            authenticated = false;
            Thread AuthThread = new Thread(AuthenticateAsync);
            AuthThread.Start(new object[] { getTokenCallback, callback, login, progressreport });
        }
        private void AuthenticateAsync(object arg)
        {
            Logger.Debug("starting dropbox authentication");
            object[] args = arg as object[];
			Action getTokenCallback = args [0] as Action;
            DrawableCallback callback = args[1] as DrawableCallback;
            UserLogin login = args[2] as UserLogin;
			Action<float> progressReport = args[3] as Action<float>;

            if(login == null) {
                try
                {
                    Logger.Debug("trying to get access token");
                    login = client.GetAccessToken();
                }
                catch(DropNet.Exceptions.DropboxException e)
                {
                    Logger.Debug("Failed to get access token: " + e.Message);
					getTokenCallback ();
                    return;
                }
            }
            Settings.CurrentRuntime.dropboxLogin = login;
            client.UserLogin = login;
            Logger.Debug("reading root contents to verify if we're authenticated");
            Task<MetaData> t = client.GetMetaDataTask("/");
            DateTime startTime = DateTime.Now;
            double timeSpent = 0;
            while (timeSpent < timeout)
            {
                switch (t.Status)
                {
                    case TaskStatus.WaitingForActivation:
                    case TaskStatus.Running:
                        break;
                    case TaskStatus.RanToCompletion:
                        Settings.CurrentRuntime.dropboxAuthenticated = true;
                        authenticated = true;
						uiHandler.QueueCallback(callback.AddArgument(t.Result));
                        return;
                    case TaskStatus.Faulted:
                        client.UserLogin = null;
						uiHandler.QueueCallback(callback.AddArgument(t.Exception.InnerException));
                        return;
                    default:
                        client.UserLogin = null;
						uiHandler.QueueCallback(callback.AddArgument(new Exception("Something went terribly wrong")));
                        return;
                }
                timeSpent = (DateTime.Now - startTime).TotalMilliseconds;
				progressReport((float)(timeSpent / timeout));
                Thread.Sleep(timeout / 200);
            }
			uiHandler.QueueCallback(callback.AddArgument(new Exception("Dropbox request timed out")));
        }

        internal List<MetaData> List(string path)
        {
            try
            {
                MetaData metaData = client.GetMetaData(path);
                return metaData.Contents;
            }
            catch (DropboxException)
            {
                Logger.Error("Failed to read dropbox path at [" + path + "]");
                return null;
            }
        }

		internal void Store(StorageFile file, DrawableCallbackDelegate callback, Action<float> progressDialog)
        {
            string fullPath = Path.GetDirectoryName(file.proposedPath);

            Logger.Debug("About to store " + file.info.Name + " to dropbox");
            Task<MetaData> t = client.GetMetaDataTask(fullPath);
            while (!t.IsCompleted)
                Thread.Sleep(20);

            MetaData folderInfo;
            List<string> filenames = null;
            if (t.IsFaulted)
            {
                if ((t.Exception.InnerException as DropboxRestException).StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Logger.Info("Creating folder in dropbox" + fullPath);
                    folderInfo = client.CreateFolder(fullPath.Replace('\\', '/'));
                    filenames = new List<string>();
                }
                else
                {
                    Logger.Error("Fail: " + t.Exception.InnerException.Message);
                    uiHandler.QueueCallback(callback,
                        new object[] {
                            file,
                            t.Exception.InnerException
                        }
                    );
                    return;
                }
            }
            else
            {
                folderInfo = t.Result;
            }

            if (!folderInfo.Is_Dir)
            {
                Logger.Warn("Can't store to dropbox because a file is in the way");
                uiHandler.QueueCallback(
                    callback,
                    new object[] {
                        file,
                        new Exception("Can't store file to dropbox folder " + fullPath + " because a file with that name is in the way")
                    });
                return;
            }

            if(filenames == null)
                filenames = t.Result.Contents.Select(x => x.Name).ToList();

            string filename = Utils.GenerateUniqueNumberedFilename(Path.GetFileName(file.proposedPath), filenames);
            Logger.Debug("Going to store the file as " + filename);

            if(file.format == StorageFileFormat.CSV)
            {
                FileStream zipFileStream = File.Create(LabNation.Common.Utils.GetTempFileName(".zip"));
                ZipOutputStream zipStream = new ZipOutputStream(zipFileStream);
                zipStream.SetLevel(9);
                ZipEntry newEntry = new ZipEntry(filename);
                newEntry.Size = file.info.Length;

                zipStream.PutNextEntry(newEntry);

                // the "using" will close the stream even if an exception occurs
                using (FileStream streamReader = File.OpenRead(file.info.FullName)) {
                    streamReader.CopyTo(zipStream);
                }
                zipStream.CloseEntry();
                zipStream.Close();

                File.Delete(file.info.FullName);

                //Update StorageFile
                filename = Utils.GenerateUniqueNumberedFilename(Path.GetFileName(file.proposedPath) + ".zip", filenames, ".csv.zip");
                file = new StorageFile();
                file.format = StorageFileFormat.CSV_ZIP;
                file.info = new FileInfo(zipFileStream.Name);
                
                file.proposedPath = filename;
            }

            FileStream stream = new FileStream(file.info.FullName, FileMode.Open);
            int chunkSize = 1024 * 1024;
            client.UploadChunkedFileAsync(
                (offset) => {
                    Logger.Debug("New chunck requested at offset " + offset);
                    byte[] contents = new byte[(int)Math.Min(chunkSize, stream.Length - stream.Position)];
                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.Read(contents, 0, contents.Length);
                    return contents;
                },
                Path.Combine(fullPath, filename),
                (metaData) => {
                    Logger.Debug("Done uploading file [" + metaData.Name + "]");
                    stream.Close();
                    File.Delete(file.info.FullName);
                    uiHandler.QueueCallback(callback,
                        new object[] {
                            file,
                            metaData
                        });
                },
                (failure) => {
                    Logger.Error("Failed to store file: " + failure.Message);
                    stream.Close();
                    uiHandler.QueueCallback(callback,
                        new object[] {
                            file,
                            failure
                        });
                },
                (progress) => {
					progressDialog(progress.BytesSaved / (float)stream.Length);
                },
                false, null, stream.Length, 5);
        }

		internal void DownloadToFolderAsync(List<MetaData> files, string Path, Action<float> progress, Action<Exception> complete)
        {
        	Task.Factory.StartNew( () => {
				progress(0f);
				int bytesToFetch = files.Aggregate(0, (a, b) => a + (int)b.Bytes);
				int bytesFetched = 0;
				try {
					foreach(var file in files) 
					{
	            		System.IO.Directory.CreateDirectory(Path);
	            		byte[] fileContents = uiHandler.dropboxStorage.GetFile(file.Path);
	            		bytesFetched += fileContents.Length;
	            		progress((float)bytesFetched / (float)bytesToFetch);
						FileStream fs = new System.IO.FileStream(
							System.IO.Path.Combine(Path, file.Name), 
							System.IO.FileMode.Create,
	                      	System.IO.FileAccess.Write);
	                    fs.Write(fileContents, 0, fileContents.Length);
						fs.Close();
					}
					complete(null);
            	}
            	catch(Exception e)
            	{
            		complete(e);
            	}
    		});
        }

        internal byte[] GetFile(string path)
        {
            return client.GetFile(path);
        }

        internal List<MetaData> GetFilesInDirectory (string directory, DrawableCallbackDelegate callback)
		{
			Task<MetaData> t = client.GetMetaDataTask (directory);
			while (!t.IsCompleted)
				Thread.Sleep (20);

			MetaData folderInfo;
			if (t.IsFaulted) {
				if ((t.Exception.InnerException as DropboxRestException).StatusCode == System.Net.HttpStatusCode.NotFound) {
					Logger.Info ("Creating folder in dropbox" + directory);
					folderInfo = client.CreateFolder (directory.Replace ('\\', '/'));
				} else {
					Logger.Error ("Fail: " + t.Exception.InnerException.Message);
					uiHandler.QueueCallback (callback, t.Exception.InnerException);
					return null;
				}
			} else {
				folderInfo = t.Result;
			}

			if (!folderInfo.Is_Dir) {
				Logger.Warn ("Can't get files because the path is not a directory");
				uiHandler.QueueCallback (
					callback,
					new object[] {
						new Exception ("Can't get the files in dropbox folder " + directory + " because a file with that name exists")
					});
				return null;
			}
			//This seems to happen when the folder is just created: just try re-reading it
			if (folderInfo.Contents == null) {
				return GetFilesInDirectory(directory, callback);
			}
            uiHandler.QueueCallback(callback, folderInfo.Contents);
            return folderInfo.Contents;
        }
    }
}
