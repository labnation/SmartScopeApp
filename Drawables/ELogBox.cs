using ESuite.Measurements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using LabNation.Common;
using System.Collections.Concurrent;

namespace ESuite.Drawables
{
    internal class ELogBox : EDrawable
    {
        private SpriteFont font;
        private ConcurrentQueue<LogMessage> logQueue = new ConcurrentQueue<LogMessage>();
        private List<String> logEntriesStrings = new List<string>();
        LogLevel logLevel;

        public ELogBox(LogLevel logLevel)
            : base()
        {
            this.logLevel = logLevel;
            Logger.AddQueue(logQueue);

            LoadContent();        
        }

        protected override void LoadContentInternal()
        {
			font = content.Load<SpriteFont>(Scaler.GetFontLogBox()+"Bold");
        }

        protected override void DrawInternal(GameTime time)
        {
            //simply draw the image to span the entire screen
            spriteBatch.Begin(SpriteSortMode.FrontToBack, BlendState.NonPremultiplied);
            Texture2D pixel = new Texture2D(device, 1, 1, true, SurfaceFormat.Color);
            Color c = Color.Black;
            c.A  = 190;
            pixel.SetData(new Color[] {c});
            spriteBatch.Draw(pixel, Boundaries, new Color(10,10,10));

            while (logQueue.Count > 0)
            {
                LogMessage entry;
                if (logQueue.TryDequeue(out entry))
                {
                    if (entry.level > logLevel) continue;
                    logEntriesStrings.Add("<" + entry.level.ToString()[0] + "> " + entry.message);
                }
            }
            //Display last N items, depending on height
            int itemHeight = (int)font.MeasureString("dummyString").Y;
            int itemsToShow = Boundaries.Height / itemHeight;
            string[] display = logEntriesStrings.Skip(logEntriesStrings.Count - itemsToShow).ToArray();
            for (int i = display.Length - 1; i >= 0 ; i--)
            {
				spriteBatch.DrawString(font, display[i], new Vector2(Boundaries.X, Boundaries.Bottom - itemHeight * (display.Length - i)), Color.White, 0f, new Vector2(), 1, SpriteEffects.None, 1f);
            }
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
        }
    }
}
