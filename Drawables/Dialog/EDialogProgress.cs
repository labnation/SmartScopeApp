using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace ESuite.Drawables
{
    internal class EDialogProgress:EDialog
    {
        private float progress;
        public float Progress { get { return this.progress; }
            set
            {
                if (this.progress != value)
                    redrawRequest = true;
                this.progress = value;
            }
        }
        private Color progressColor = Color.White;
        private int progressHeight { get { return defaultFont.LineSpacing * 2; } }

        public EDialogProgress(string message, float progress)
            : base(message)
        {
            this.Progress = progress;
        }

        protected override void OnBoundariesChangedInternal()
        {            
            base.OnBoundariesChangedInternal();
            
            contentBoundaries.Height += progressHeight;
            CenterContentBox();
        }

        protected override void UpdateInternal(GameTime now)
        {
            drawBatchRectangles.Clear();

            Rectangle progressBar = new Rectangle(
                (int)Math.Round(contentBoundaries.X + marginOld.X),
                (int)Math.Round(contentBoundaries.Bottom - marginOld.Y - progressHeight),
                (int)Math.Round((contentBoundaries.Width - 2*marginOld.X)*Progress),
                progressHeight);


            drawBatchRectangles.Add(new DrawBatchItem() {
                color = progressColor,
                rectangle = progressBar
            });            
        }
    }
}
