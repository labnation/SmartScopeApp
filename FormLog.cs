#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LabNation.Common;
using System.Collections.Concurrent;

namespace ESuite
{
    public partial class FormLog : Form
    {
        private ConcurrentQueue<LogMessage> logQueue;

        public FormLog()
        {
            InitializeComponent();
            logQueue = new ConcurrentQueue<LogMessage>();
        }

        private void invalidateLog()
        {
            this.Invalidate();
        }

        private void HideForm(object sender, FormClosingEventArgs e)
        {
            if (InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { HideForm(sender, e); });
                return;
            }
            e.Cancel = true;
            this.Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            while (logQueue.Count > 0)
            {
                LogMessage entry;
                if (logQueue.TryDequeue(out entry))
                {
                    lstLog.Items.Add(entry.timestamp.ToString().PadRight(22) + entry.level.ToString().PadRight(15) + entry.message);
                    lstLog.SelectedIndex = lstLog.Items.Count - 1;
                    lstLog.SetSelected(lstLog.Items.Count - 1, false);
                }
            }

            base.OnPaint(e);
        }
    }
}
#endif