#if DEBUG
namespace ESuite
{
    partial class FormMain
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelMemories = new System.Windows.Forms.Panel();
            this.rightTabControl = new System.Windows.Forms.TabControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.numericDataTime = new System.Windows.Forms.NumericUpDown();
            this.numericDclkTime = new System.Windows.Forms.NumericUpDown();
            this.comboDataTermination = new System.Windows.Forms.ComboBox();
            this.comboDclkTermination = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.btnShowLog = new System.Windows.Forms.Button();
            this.txtBoxSerialClosetLocation = new System.Windows.Forms.TextBox();
            this.labelClosetLocation = new System.Windows.Forms.Label();
            this.btnAwgTest = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnBootloader = new System.Windows.Forms.Button();
            this.btnFlashWrite = new System.Windows.Forms.Button();
            this.btnFlashRead = new System.Windows.Forms.Button();
            this.btnEepromWrite = new System.Windows.Forms.Button();
            this.btnEepromRead = new System.Windows.Forms.Button();
            this.btnFpgaFwVersion = new System.Windows.Forms.Button();
            this.btnShowRom = new System.Windows.Forms.Button();
            this.btnSetDefaultCalib = new System.Windows.Forms.Button();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.rightTabControl.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericDataTime)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericDclkTime)).BeginInit();
            this.tabPage1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelMemories
            // 
            this.panelMemories.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMemories.Location = new System.Drawing.Point(2, 2);
            this.panelMemories.Name = "panelMemories";
            this.panelMemories.Size = new System.Drawing.Size(1331, 640);
            this.panelMemories.TabIndex = 3;
            // 
            // rightTabControl
            // 
            this.rightTabControl.Controls.Add(this.tabPage2);
            this.rightTabControl.Controls.Add(this.tabPage1);
            this.rightTabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rightTabControl.Location = new System.Drawing.Point(0, 0);
            this.rightTabControl.Margin = new System.Windows.Forms.Padding(2);
            this.rightTabControl.Name = "rightTabControl";
            this.rightTabControl.SelectedIndex = 0;
            this.rightTabControl.Size = new System.Drawing.Size(1343, 670);
            this.rightTabControl.TabIndex = 4;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.numericDataTime);
            this.tabPage2.Controls.Add(this.numericDclkTime);
            this.tabPage2.Controls.Add(this.comboDataTermination);
            this.tabPage2.Controls.Add(this.comboDclkTermination);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.label2);
            this.tabPage2.Controls.Add(this.label1);
            this.tabPage2.Controls.Add(this.btnShowLog);
            this.tabPage2.Controls.Add(this.txtBoxSerialClosetLocation);
            this.tabPage2.Controls.Add(this.labelClosetLocation);
            this.tabPage2.Controls.Add(this.btnAwgTest);
            this.tabPage2.Controls.Add(this.btnReset);
            this.tabPage2.Controls.Add(this.btnBootloader);
            this.tabPage2.Controls.Add(this.btnFlashWrite);
            this.tabPage2.Controls.Add(this.btnFlashRead);
            this.tabPage2.Controls.Add(this.btnEepromWrite);
            this.tabPage2.Controls.Add(this.btnEepromRead);
            this.tabPage2.Controls.Add(this.btnFpgaFwVersion);
            this.tabPage2.Controls.Add(this.btnShowRom);
            this.tabPage2.Controls.Add(this.btnSetDefaultCalib);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(2);
            this.tabPage2.Size = new System.Drawing.Size(1335, 644);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Danger zone";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // numericDataTime
            // 
            this.numericDataTime.Location = new System.Drawing.Point(465, 3);
            this.numericDataTime.Maximum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.numericDataTime.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            -2147483648});
            this.numericDataTime.Name = "numericDataTime";
            this.numericDataTime.Size = new System.Drawing.Size(73, 20);
            this.numericDataTime.TabIndex = 21;
            this.numericDataTime.ValueChanged += new System.EventHandler(this.UpdateAdcTiming);
            // 
            // numericDclkTime
            // 
            this.numericDclkTime.Location = new System.Drawing.Point(321, 6);
            this.numericDclkTime.Maximum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.numericDclkTime.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            -2147483648});
            this.numericDclkTime.Name = "numericDclkTime";
            this.numericDclkTime.Size = new System.Drawing.Size(73, 20);
            this.numericDclkTime.TabIndex = 20;
            this.numericDclkTime.ValueChanged += new System.EventHandler(this.UpdateAdcTiming);
            // 
            // comboDataTermination
            // 
            this.comboDataTermination.FormattingEnabled = true;
            this.comboDataTermination.Items.AddRange(new object[] {
            "50 Ohm",
            "75 Ohm",
            "100 Ohm",
            "150 Ohm",
            "300 Ohm"});
            this.comboDataTermination.Location = new System.Drawing.Point(465, 31);
            this.comboDataTermination.Name = "comboDataTermination";
            this.comboDataTermination.Size = new System.Drawing.Size(73, 21);
            this.comboDataTermination.TabIndex = 19;
            this.comboDataTermination.SelectedIndex = 0;
            this.comboDataTermination.SelectedIndexChanged += new System.EventHandler(this.UpdateAdcTiming);
            // 
            // comboDclkTermination
            // 
            this.comboDclkTermination.FormattingEnabled = true;
            this.comboDclkTermination.Items.AddRange(new object[] {
            "50 Ohm",
            "75 Ohm",
            "100 Ohm",
            "150 Ohm",
            "300 Ohm"});
            this.comboDclkTermination.Location = new System.Drawing.Point(321, 31);
            this.comboDclkTermination.Name = "comboDclkTermination";
            this.comboDclkTermination.Size = new System.Drawing.Size(73, 21);
            this.comboDclkTermination.TabIndex = 18;
            this.comboDclkTermination.SelectedIndex = 0;
            this.comboDclkTermination.SelectedIndexChanged += new System.EventHandler(this.UpdateAdcTiming);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(400, 31);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(59, 13);
            this.label3.TabIndex = 17;
            this.label3.Text = "DATA term";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(401, 5);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(58, 13);
            this.label4.TabIndex = 16;
            this.label4.Text = "DATA time";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(256, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(58, 13);
            this.label2.TabIndex = 15;
            this.label2.Text = "DCLK term";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(256, 5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 13);
            this.label1.TabIndex = 14;
            this.label1.Text = "DCLK time";
            // 
            // btnShowLog
            // 
            this.btnShowLog.Location = new System.Drawing.Point(3, 239);
            this.btnShowLog.Name = "btnShowLog";
            this.btnShowLog.Size = new System.Drawing.Size(120, 39);
            this.btnShowLog.TabIndex = 13;
            this.btnShowLog.Text = "AWG test";
            this.btnShowLog.UseVisualStyleBackColor = true;
            this.btnShowLog.Click += new System.EventHandler(this.btnShowLog_Click);
            // 
            // txtBoxSerialClosetLocation
            // 
            this.txtBoxSerialClosetLocation.Location = new System.Drawing.Point(6, 200);
            this.txtBoxSerialClosetLocation.Name = "txtBoxSerialClosetLocation";
            this.txtBoxSerialClosetLocation.Size = new System.Drawing.Size(244, 20);
            this.txtBoxSerialClosetLocation.TabIndex = 12;
            this.txtBoxSerialClosetLocation.TextChanged += new System.EventHandler(this.txtBoxSerialClosetLocation_TextChanged);
            // 
            // labelClosetLocation
            // 
            this.labelClosetLocation.AutoSize = true;
            this.labelClosetLocation.Location = new System.Drawing.Point(5, 223);
            this.labelClosetLocation.Name = "labelClosetLocation";
            this.labelClosetLocation.Size = new System.Drawing.Size(34, 13);
            this.labelClosetLocation.TabIndex = 11;
            this.labelClosetLocation.Text = "Ahum";
            // 
            // btnAwgTest
            // 
            this.btnAwgTest.Location = new System.Drawing.Point(131, 154);
            this.btnAwgTest.Name = "btnAwgTest";
            this.btnAwgTest.Size = new System.Drawing.Size(120, 39);
            this.btnAwgTest.TabIndex = 10;
            this.btnAwgTest.Text = "AWG test";
            this.btnAwgTest.UseVisualStyleBackColor = true;
            this.btnAwgTest.Click += new System.EventHandler(this.btnAwgTest_Click);
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(4, 119);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(120, 29);
            this.btnReset.TabIndex = 9;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnBootloader
            // 
            this.btnBootloader.Location = new System.Drawing.Point(130, 119);
            this.btnBootloader.Name = "btnBootloader";
            this.btnBootloader.Size = new System.Drawing.Size(120, 29);
            this.btnBootloader.TabIndex = 9;
            this.btnBootloader.Text = "Bootloader";
            this.btnBootloader.UseVisualStyleBackColor = true;
            this.btnBootloader.Click += new System.EventHandler(this.btnBootloader_Click);
            // 
            // btnFlashWrite
            // 
            this.btnFlashWrite.Location = new System.Drawing.Point(4, 84);
            this.btnFlashWrite.Name = "btnFlashWrite";
            this.btnFlashWrite.Size = new System.Drawing.Size(120, 29);
            this.btnFlashWrite.TabIndex = 8;
            this.btnFlashWrite.Text = "FLASH write";
            this.btnFlashWrite.UseVisualStyleBackColor = true;
            this.btnFlashWrite.Click += new System.EventHandler(this.btnWriteFlash_Click);
            // 
            // btnFlashRead
            // 
            this.btnFlashRead.Location = new System.Drawing.Point(4, 51);
            this.btnFlashRead.Name = "btnFlashRead";
            this.btnFlashRead.Size = new System.Drawing.Size(120, 29);
            this.btnFlashRead.TabIndex = 8;
            this.btnFlashRead.Text = "FLASH read";
            this.btnFlashRead.UseVisualStyleBackColor = true;
            this.btnFlashRead.Click += new System.EventHandler(this.btnReadFlash_Click);
            // 
            // btnEepromWrite
            // 
            this.btnEepromWrite.Location = new System.Drawing.Point(130, 84);
            this.btnEepromWrite.Name = "btnEepromWrite";
            this.btnEepromWrite.Size = new System.Drawing.Size(120, 29);
            this.btnEepromWrite.TabIndex = 8;
            this.btnEepromWrite.Text = "EEPROM write";
            this.btnEepromWrite.UseVisualStyleBackColor = true;
            this.btnEepromWrite.Click += new System.EventHandler(this.btnWriteRom_Click);
            // 
            // btnEepromRead
            // 
            this.btnEepromRead.Location = new System.Drawing.Point(130, 50);
            this.btnEepromRead.Name = "btnEepromRead";
            this.btnEepromRead.Size = new System.Drawing.Size(120, 29);
            this.btnEepromRead.TabIndex = 8;
            this.btnEepromRead.Text = "EEPROM read";
            this.btnEepromRead.UseVisualStyleBackColor = true;
            this.btnEepromRead.Click += new System.EventHandler(this.btnReadRom_Click);
            // 
            // btnFpgaFwVersion
            // 
            this.btnFpgaFwVersion.Location = new System.Drawing.Point(5, 154);
            this.btnFpgaFwVersion.Name = "btnFpgaFwVersion";
            this.btnFpgaFwVersion.Size = new System.Drawing.Size(120, 39);
            this.btnFpgaFwVersion.TabIndex = 7;
            this.btnFpgaFwVersion.Text = "FPGA FW?";
            this.btnFpgaFwVersion.UseVisualStyleBackColor = true;
            this.btnFpgaFwVersion.Click += new System.EventHandler(this.btnFpgaFwVersion_Click);
            // 
            // btnShowRom
            // 
            this.btnShowRom.Location = new System.Drawing.Point(130, 5);
            this.btnShowRom.Name = "btnShowRom";
            this.btnShowRom.Size = new System.Drawing.Size(120, 39);
            this.btnShowRom.TabIndex = 7;
            this.btnShowRom.Text = "Show ROM";
            this.btnShowRom.UseVisualStyleBackColor = true;
            this.btnShowRom.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnSetDefaultCalib
            // 
            this.btnSetDefaultCalib.Location = new System.Drawing.Point(3, 5);
            this.btnSetDefaultCalib.Name = "btnSetDefaultCalib";
            this.btnSetDefaultCalib.Size = new System.Drawing.Size(121, 39);
            this.btnSetDefaultCalib.TabIndex = 1;
            this.btnSetDefaultCalib.Text = "Set calib";
            this.btnSetDefaultCalib.UseVisualStyleBackColor = true;
            this.btnSetDefaultCalib.Click += new System.EventHandler(this.btnJasper_click);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.panelMemories);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(2);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(2);
            this.tabPage1.Size = new System.Drawing.Size(1335, 644);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Memory overview";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1343, 670);
            this.Controls.Add(this.rightTabControl);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FormMain";
            this.Text = "ESuite";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.rightTabControl.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericDataTime)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericDclkTime)).EndInit();
            this.tabPage1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelMemories;
        private System.Windows.Forms.TabControl rightTabControl;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button btnSetDefaultCalib;
        private System.Windows.Forms.Button btnShowRom;
        private System.Windows.Forms.Button btnEepromWrite;
        private System.Windows.Forms.Button btnEepromRead;
        private System.Windows.Forms.Button btnBootloader;
        private System.Windows.Forms.Button btnFlashWrite;
        private System.Windows.Forms.Button btnFlashRead;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnFpgaFwVersion;
        private System.Windows.Forms.Button btnAwgTest;
        private System.Windows.Forms.TextBox txtBoxSerialClosetLocation;
        private System.Windows.Forms.Label labelClosetLocation;
        private System.Windows.Forms.Button btnShowLog;
        private System.Windows.Forms.NumericUpDown numericDataTime;
        private System.Windows.Forms.NumericUpDown numericDclkTime;
        private System.Windows.Forms.ComboBox comboDataTermination;
        private System.Windows.Forms.ComboBox comboDclkTermination;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
    }
}

#endif