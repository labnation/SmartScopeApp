#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Memories;
using LabNation.DeviceInterface.Hardware;
using LabNation.Common;
using System.Collections.Concurrent;
using System.Threading;

namespace ESuite
{
    partial class FormMain : Form
    {
        private SmartScopeGui smartScopeApp;
        private Thread appThread;
        private IDevice device;
        private FormLog logForm;

        public FormMain()
        {
            appThread = new Thread(new ThreadStart(runApp));
            appThread.Name = "SmartScope app thread";

            System.Threading.Thread.CurrentThread.Name = "FormMainThread";
            
            InitializeComponent();  //must be done before calling InitApp()

            logForm = new FormLog();
            
            this.WindowState = FormWindowState.Normal;
            appThread.Start();
            
            Logger.Info("Application initialized");
        }

        void runApp()
        {
            smartScopeApp = new SmartScopeGui(onDeviceConnect);
            smartScopeApp.Exiting += (s, e) => {
                try
                {
                    if (this.InvokeRequired)
                        this.Invoke((MethodInvoker)delegate() { this.Close(); });
                    else
                        this.Close();
                }
                catch { }
            };
            smartScopeApp.Run();
        }

        private void btnJasper_click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope scope = (SmartScope)device;
            bool running = scope.DataSourceScope.IsRunning;
            scope.DataSourceScope.Stop();
            List<SmartScope.GainCalibration> calibA = new List<SmartScope.GainCalibration>() {
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[0], multiplier = SmartScope.validMultipliers[0], coefficients = new double[] {0.0051, -0.0090,  0.3112} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[0], multiplier = SmartScope.validMultipliers[1], coefficients = new double[] {0.0027, -0.0048,  0.1630} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[0], multiplier = SmartScope.validMultipliers[2], coefficients = new double[] {0.0018, -0.0031,  0.1044} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[1], multiplier = SmartScope.validMultipliers[0], coefficients = new double[] {0.0320, -0.0559,  1.9276} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[1], multiplier = SmartScope.validMultipliers[1], coefficients = new double[] {0.0169, -0.0296,  1.0094} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[1], multiplier = SmartScope.validMultipliers[2], coefficients = new double[] {0.0109, -0.0190,  0.6446} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[2], multiplier = SmartScope.validMultipliers[0], coefficients = new double[] {0.1876, -0.3280, 11.2537} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[2], multiplier = SmartScope.validMultipliers[1], coefficients = new double[] {0.0995, -0.1739,  5.8836} },
                new SmartScope.GainCalibration() { channel = AnalogChannel.ChA, divider = SmartScope.validDividers[2], multiplier = SmartScope.validMultipliers[2], coefficients = new double[] {0.0636, -0.1111,  3.7572} },
            };
            List<SmartScope.GainCalibration> calibB = calibA.Select(x => new SmartScope.GainCalibration() { channel = AnalogChannel.ChB, multiplier=x.multiplier,divider=x.divider, coefficients= x.coefficients }).ToList();

            scope.rom.clearCalibration();
            foreach (SmartScope.GainCalibration c in calibA.Concat(calibB))
                scope.rom.setCalibration(c);
            scope.rom.Upload();
            if (running)
                scope.DataSourceScope.Start();
        }

        private void onDeviceConnect(IDevice scope, bool connected)
        {
            //Only add memories here, since the scope device is only initialized after the initialization of
            //the xnaControl
            if (connected)
            {
                this.device = scope;
                this.AddMemories(panelMemories);
            }
            else
            {
                this.device = null;
                this.ClearMemories(panelMemories);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            string RomContents = "plug count: " + s.rom.plugCount + "\n\n";
            RomContents += "ADC timing: " + s.rom.AdcTimingValue + "\n\n";
            foreach(LabNation.DeviceInterface.Devices.SmartScope.GainCalibration c in s.rom.gainCalibration){
                RomContents += String.Format("Calib [ch {0}:div {1:#0.0}:mul {2:0.0}] = [{3:0.0000}:{4:0.0000}:{5:0.0000}]\n", c.channel.Value, c.divider, c.multiplier, c.coefficients[0], c.coefficients[1], c.coefficients[2]);
            }
            MessageBox.Show(RomContents);
        }

        private void btnReadRom_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            ISmartScopeInterface h = s.HardwareInterface as ISmartScopeInterface;
            byte[] result;
            uint bytesPerRead = 8;
            for(uint i = 0; i < 256; i+=bytesPerRead)
            {
                h.GetControllerRegister(ScopeController.ROM, i, bytesPerRead, out result);
                Logger.Info(String.Format("@{0:X4} : {1}", i, String.Join(",", result.Select(x => x.ToString("X2")))));
            }
        }

        private void btnReadFlash_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            ISmartScopeInterface h = s.HardwareInterface as ISmartScopeInterface;
            byte[] result;
            uint bytesPerRead = 8;
            for (uint i = 0; i < 0x100; i += bytesPerRead)
            {
                h.GetControllerRegister(ScopeController.FLASH, i, bytesPerRead, out result);
                if (result == null)
                {
                    Logger.Error(String.Format("Read nothing @{0:X4}", i));
                    continue;
                }
                Logger.Info(String.Format("@{0:X4} : {1}", i, String.Join(",", result.Select(x => x.ToString("X2")))));
            }
        }

        private void btnWriteFlash_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            ISmartScopeInterface h = s.HardwareInterface as ISmartScopeInterface;

            uint bytesPerWrite = 11;
            for (uint i = 0x0; i < 0x1000; i += bytesPerWrite)
            {
                byte[] data = new byte[bytesPerWrite];
                for (int j = 0; j < data.Length; j++)
                {
                    data[j] = (byte)(i + j);
                }
                h.SetControllerRegister(ScopeController.FLASH, i, data);
            }

        }


        private void btnWriteRom_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            ISmartScopeInterface h = s.HardwareInterface as ISmartScopeInterface;
            byte[] result = new byte[10] { 10,11,12,13,14,15,16,17,18,19};
            h.SetControllerRegister(ScopeController.ROM, 10, result);
        }

        private void btnBootloader_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            s.LoadBootLoader();
        }
        private void btnReset_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            s.Reset();
        }

        private void btnFpgaFwVersion_Click(object sender, EventArgs e)
        {
            if (!(device is SmartScope)) return;
            SmartScope s = (SmartScope)device;
            MessageBox.Show(String.Format("FPGA FW version: 0x{0:X08}", s.GetFpgaFirmwareVersion()));
        }

        private void btnAwgTest_Click(object sender, EventArgs e)
        {
            if (!(device is IWaveGenerator)) return;
            IWaveGenerator s = (IWaveGenerator)device;
            double[] awgData = new double[2048];
            s.GeneratorDataDouble = awgData;

            for (int i = 0; i < awgData.Length; i++)
                awgData[i] = (double)i / awgData.Length * 3.3;

            s.GeneratorDataDouble = awgData;
        }

        private void txtBoxSerialClosetLocation_TextChanged(object sender, EventArgs e)
        {
            string serialPartBase36 = ((TextBox)sender).Text;
            int serialPartBase10 = (int)Base36.Decode(serialPartBase36);
            int boxesPerPigeonHole = 4;
            int rows = 5;
            int cols = 5;

            int boxNr = serialPartBase10 % boxesPerPigeonHole;
            int rowNr = (serialPartBase10 / boxesPerPigeonHole) % rows;
            int colNr = (serialPartBase10 / (boxesPerPigeonHole * rows)) % cols;

            string location = String.Format("Row {0} - Col {1} - Box {2}", rowNr + 1, colNr + 1, boxNr + 1);
            labelClosetLocation.Text = location;
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                smartScopeApp.Exit();
            }
            catch { }
        }

        private void btnShowLog_Click(object sender, EventArgs e)
        {
            logForm.Show();
        }

        private void fullscreenify()
        {
            var s = Screen.AllScreens;
            if (s.Count() > 1)
            {
                this.Location = s[0].WorkingArea.Location;
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void btnReposition_Click(object sender, EventArgs e)
        {
            fullscreenify();
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            fullscreenify();
        }

        private void UpdateAdcTiming(object sender, EventArgs e)
        {
            int DataTermination = comboDataTermination.SelectedIndex;
            int DclkTermination = comboDclkTermination.SelectedIndex;
            int DataDelay = (int)numericDataTime.Value;
            int DclkDelay = (int)numericDclkTime.Value;

            byte timingRegister = (byte)((Math.Sign(DclkDelay) >= 0 ? 0 : 1) << 5);
            timingRegister += (byte)((Math.Abs(DclkDelay) & 0x03) << 3);
            timingRegister += (byte)((Math.Sign(DataDelay) >= 0 ? 0 : 1) << 2);
            timingRegister += (byte)(Math.Abs(DataDelay) & 0x03);

            byte terminationRegister = (byte)((DclkTermination & 0x07) << 3);
            terminationRegister += (byte)((DataTermination & 0x07));

            if (device is SmartScope)
            {
                SmartScope ss = device as SmartScope;
                while(ss.AdcMemory[MAX19506.DATA_CLK_TIMING].Read().GetByte() != timingRegister)
                    ss.AdcMemory[MAX19506.DATA_CLK_TIMING].WriteImmediate(timingRegister);
                while (ss.AdcMemory[MAX19506.CHA_TERMINATION].Read().GetByte() != terminationRegister)
                    ss.AdcMemory[MAX19506.CHA_TERMINATION].WriteImmediate(terminationRegister);
            }
        }
    }
}
#endif