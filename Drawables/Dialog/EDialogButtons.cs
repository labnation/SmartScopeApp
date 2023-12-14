using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace ESuite.Drawables
{
    internal class EDialogButtons:EDialog
    {
        private Color buttonColor = Color.White;
        private int buttonHeight { get { return defaultFont.LineSpacing * 2; } }
        private List<EButton> buttons { get { return children.Where(x => x is EButton).Select(x => x as EButton).ToList(); } }

        public EDialogButtons(string message, List<ButtonInfo> options)
            : base(message)
        {
            if (options != null)
            {
                foreach (ButtonInfo bi in options)
                {
                    EButton b = new EButton(bi, buttonColor, 1f, Color.Black);
                    AddChild(b);
                    if(bi.listenKeys != null && bi.listenKeys.Count != 0)
                        this.keysToCallbackMap.Add(bi.listenKeys, bi.callback);
                }
            }
        }

        protected override void OnBoundariesChangedInternal()
        {            
            base.OnBoundariesChangedInternal();
            
            contentBoundaries.Height += buttonHeight;
            CenterContentBox();

            int buttonWidth = (int)Math.Round((contentBoundaries.Width - (buttons.Count + 1) * marginOld.X) / buttons.Count);
            float centerX = contentBoundaries.X + contentBoundaries.Width / 2f;
            float buttonAreaWidth = buttonWidth * buttons.Count + (buttons.Count - 1) * marginOld.X;
            Vector2 topLeft = new Vector2(centerX - buttonAreaWidth / 2f, contentBoundaries.Bottom - marginOld.Y - buttonHeight);

            foreach (EButton button in buttons)
            {
                Rectangle butRect = new Rectangle((int)topLeft.X, (int)topLeft.Y, buttonWidth, buttonHeight);
                button.SetBoundaries(butRect);
                topLeft.X += butRect.Width + marginOld.X;
            }

        }
    }
}
