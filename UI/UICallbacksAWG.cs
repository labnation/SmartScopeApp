using LabNation.DeviceInterface.Devices;
using ESuite.Drawables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DropNet.Models;
using DropNet.Exceptions;

namespace ESuite
{
    static partial class UICallbacks
    {
        public static AnalogWaveForm GeneratorAnalogWaveform = AnalogWaveForm.SINE;
        internal static double GeneratorAmplitudeScaler = 3.0;
        static uint GeneratorMultisineHarmonic = 2;
        internal static double GeneratorAnalogOffset = 0.1;
        internal static double GeneratorAnalogFrequency = 50e3;

        #region dropbox

        public static void AwgLoadFileDropboxStep1(EDrawable sender, object arg)
        {
            if (!DropboxAuthenticated(sender, new DrawableCallback(AwgLoadFileDropboxStep1, arg)))
                return;
            
            uiHandler.dropboxStorage.GetFilesInDirectory("AWG", new DrawableCallbackDelegate(AwgLoadFileDropboxStep2));
        }
        public static void AwgLoadFileDropboxStep2(EDrawable sender, object arg)
        {
            if (arg is List<MetaData>)
            {
                List<MetaData> directoryContents = (arg as List<MetaData>).Where(x => x.Extension.ToUpper() == ".CSV").ToList();
                if (directoryContents.Count == 0)
                {
                    ShowDialog(null, "The AWG folder seems to contain no CSV files");
                    return;
                }
                SelectFileFromDropboxFilelist(directoryContents, UploadAwgWaveformDropboxPath, new DrawableCallback(GUI_UploadAwgWaveformFromDropboxCancel));
            }
            else if (arg is Exception)
            {
                string message;
                List<ButtonInfo> buttons = new List<ButtonInfo>() {
                    new ButtonInfo("OK", HideDialog)
                };
                if (arg is DropboxRestException)
                {
                    DropboxRestException dbe = arg as DropboxRestException;
                    if (
                        dbe.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        dbe.StatusCode == System.Net.HttpStatusCode.Forbidden
                        )
                    {
                        message = "I don't seem to have permission to access your dropbox";
                        buttons.Insert(0,
                            new ButtonInfo(
                                "Authenticate",
                                DropboxAuthenticateStep1,
                                new DrawableCallback(AwgLoadFileDropboxStep1))
                        );
                    }
                    else
                    {
                        message = "Something went wrong storing the file to dropbox";
                        buttons.Insert(0, new ButtonInfo("Retry", AwgLoadFileDropboxStep1));
                    }

                }
                else
                {
                    message = (arg as Exception).Message;
                    buttons.Insert(0, new ButtonInfo("Retry", AwgLoadFileDropboxStep1));
                }
                uiHandler.ShowDialog(message, buttons);
                return;
            }
            else
            {
                uiHandler.ShowDialog("Unknown error while accessing dropbox");
            }
        }

        public static void UploadAwgWaveformDropboxPath(EDrawable sender, object arg)
        {
            try
            {
                byte[] data = uiHandler.dropboxStorage.GetFile((string)arg);
                GeneratorUploadWaveformFromCsvBytes(data);
            }
            catch (Exception e)
            {
                ShowDialog(null, "Failed to fetch AWG file\n" + e.Message);
            }
        }

        public static void GUI_UploadAwgWaveformFromDropbox(EDrawable sender, object arg)
        {
            if (!uiHandler.GotWaveGenerator)
            {
                uiHandler.ShowDialog("No waveform generator connected, can't upload waveform");
                return;
            }
            AwgLoadFileDropboxStep1(sender, null);
        }

        public static void GUI_UploadAwgWaveformFromDropboxCancel(EDrawable sender, object arg)
        {
            if (arg is string)
            {
                uiHandler.ShowDialog("Coulnd't proceed uploading an AWG file from dropbox\n" + (string)arg);
            }
            else
            {
                uiHandler.HideDialog();
            }
        }

        #endregion

        #region local file select

        public static void GeneratorUploadWaveformFromLocalPath(EDrawable sender, object arg)
        {
            SelectFileFromDirectory(Path.Combine(LabNation.Common.Utils.StoragePath, "AWG"), "*.csv", GeneratorUploadWaveformLocal, new DrawableCallback(HideDialog));
        }

        public static void GeneratorEnableAnalogOut(EDrawable sender, object arg)
        {
            uiHandler.EnableGeneratorAnalogOutput((bool)arg, sender);
        }
        public static void GeneratorEnableDigitalOut(EDrawable sender, object arg)
        {
            uiHandler.EnableGeneratorDigitalOutput((bool)arg);
        }

        public static void GeneratorUploadWaveformLocal(EDrawable sender, object arg)
        {
            if (arg is Exception)
            {
                ShowDialog(null, "Failed to load AWG file\n" + ((Exception)arg).Message);
                return;
            }

            try
            {
                byte[] data = File.ReadAllBytes((string)arg);
                GeneratorUploadWaveformFromCsvBytes(data);
            }
            catch (Exception e)
            {
                ShowDialog(null, "Failed to fetch AWG file\n" + e.Message);
            }
        }


        #endregion

        public static void GeneratorSetAnalogWaveform(EDrawable sender, object arg)
        {
            GeneratorAnalogWaveform = (AnalogWaveForm)arg;
        }

        public static void GeneratorSetAnalogFrequency(EDrawable sender, object arg)
        {
            if (!uiHandler.GotWaveGenerator)
                return;

            GeneratorAnalogFrequency = (double)arg;
        }

        public static void GeneratorSetAnalogAmplitude(EDrawable sender, object arg)
        {
            GeneratorAmplitudeScaler = (double)arg;
        }
        public static void GeneratorSetAnalogOffset(EDrawable sender, object arg)
        {
            GeneratorAnalogOffset = (double)arg;
        }

        public static void GeneratorUploadAnalogWaveform(EDrawable sender, object arg)
        {
            uiHandler.GeneratorUploadAnalogWaveform((MenuItem)sender, GeneratorAnalogFrequency, GeneratorAnalogWaveform, 0, GeneratorAmplitudeScaler / 2.0, GeneratorAnalogOffset, 0, 2);
        }


        # region CSV parsing

        class awgCsvRecord
        {
            public double Value { get; set; }
            public string Name { get; set; }
        }

        sealed class AwgCsvRecordMap : CsvHelper.Configuration.CsvClassMap<awgCsvRecord>
        {
            public AwgCsvRecordMap()
            {
                Map(m => m.Value).Index(0);
                Map(m => m.Name).Index(1);
            }
        }

        private static void GeneratorUploadWaveformFromCsvBytes(byte[] bytes)
        {
            if (!uiHandler.GotWaveGenerator)
                return;

            bytes = bytes.Select(x => x == 44 ? (byte)46 : x).ToArray();
            MemoryStream s = new MemoryStream(bytes);
            TextReader t = new StreamReader(s);
            int nSamples = 0;
            int sampleStretch = 0;

            double[] data;
            bool castDataToBytes = false;

            try
            {
                var csvConfig = new CsvHelper.Configuration.CsvConfiguration();
                csvConfig.Delimiter = ";";
                csvConfig.CultureInfo = System.Globalization.CultureInfo.InvariantCulture;
                csvConfig.HasHeaderRecord = false;
                CsvHelper.CsvReader csv = new CsvHelper.CsvReader(t, csvConfig);
                csv.Configuration.RegisterClassMap<AwgCsvRecordMap>();

                //to make CSV work with and without header line; in a way which doesn't upload bogus data in neither cases
                IEnumerable<awgCsvRecord> a = null;
                try
                {
                    a = csv.GetRecords<awgCsvRecord>().ToList();
                }
                catch
                {
                    //need to reset the datastream to the beginning
                    s.Seek(0, SeekOrigin.Begin);
                    //also need to create a new csv object as it apparently keeps an internal pointer
                    csvConfig.HasHeaderRecord = true;
                    csv = new CsvHelper.CsvReader(t, csvConfig);
                    a = csv.GetRecords<awgCsvRecord>().ToList();
                }

                try
                {
                    var dataType = a.Single(x => x.Name == "DataIsBytes");
                    if (dataType.Value == 1)
                        castDataToBytes = true;
                }
                catch (Exception e)
                {
                    LabNation.Common.Logger.Warn("No 'DataIsBytes' field found in CSV - interpreting as voltages");
                }

                try
                {
                    nSamples = (int)a.Single(x => x.Name == "Samples").Value;
                }
                catch
                {
                    uiHandler.ShowDialog("Failed to parse the CSV file\nCannot find value for Samples field\nSee the LabNation wiki for example CSV files");
                    return;
                }

                try
                {
                    sampleStretch = (int)a.Single(x => x.Name == "SampleStretch").Value;
                }
                catch
                {
                    uiHandler.ShowDialog("Failed to parse the CSV file\nCannot find value for SampleStretch field\nSee the LabNation wiki for example CSV files");
                    return;
                }

                int dataIndex = 0;
                try
                {
                    dataIndex = (int)a.Select((x, i) => new { r = x, index = i }).
                                    Where(x => x.r.Name == "BeginData").First().index;
                }
                catch
                {
                    uiHandler.ShowDialog("Failed to parse the CSV file\nCannot find value for BeginData field\nSee the LabNation wiki for example CSV files");
                    return;
                }
                
                data = a.Skip(dataIndex).Select(x => x.Value).ToArray();
            }
            catch (Exception e)
            {
                uiHandler.ShowDialog("Failed to parse the CSV file\n" + e.Message);
                return;
            }
            if (data.Length < nSamples)
            {
                uiHandler.ShowDialog(String.Format("Mismatch in sample count: {0} samples were specified, but only {1} found\n", nSamples, data.Length));
                return;
            }
            else if (data.Length > nSamples)
            {
                data = data.Take(nSamples).ToArray();
            }
            try
            {
                Array res = data;
                if(castDataToBytes)
                    res = data.Select(x => (byte)x).ToArray();
                uiHandler.SetGeneratorData(null, res, sampleStretch);
            }
            catch (Exception e)
            {
                uiHandler.ShowDialog("Failed to upload data to scope\n" + e.Message);
                return;
            }
            uiHandler.ShowDialog("AWG upload OK");
        }

        #endregion

        #region Generator digital

		internal static double GeneratorPulseDutyCycle = 0.2;
        internal static double GeneratorDigitalSamplePeriod = 0.1; //seconds
        public static DigitalWaveForm GeneratorDigitalWaveform = DigitalWaveForm.Counter;

        public static void GeneratorSetDigitalSamplePeriod(EDrawable sender, object arg)
        {
            if (!uiHandler.GotWaveGenerator)
                return;

            GeneratorDigitalSamplePeriod = (double)arg;
        }

        public static void GeneratorSetDigitalVoltage(EDrawable sender, object arg)
        {
            SmartScope.DigitalOutputVoltage voltage = (SmartScope.DigitalOutputVoltage)arg;
            uiHandler.GeneratorSetDigitalVoltage(voltage);
        }

        public static void GeneratorSetDigitalWaveform(EDrawable sender, object arg)
        {
			if (arg is object[]) {
				object[] args = (object[])arg;
				GeneratorDigitalWaveform = (DigitalWaveForm)args[0];
				GeneratorPulseDutyCycle = (double)args[1];
				return;
			}
            GeneratorDigitalWaveform = (DigitalWaveForm)arg;
        }

        public static void GeneratorUploadDigitalWaveform(EDrawable sender, object arg)
        {
			uiHandler.GeneratorUploadDigitalWaveform((MenuItem)sender, GeneratorDigitalSamplePeriod, GeneratorDigitalWaveform, GeneratorPulseDutyCycle);
        }

        #endregion
    }
}
