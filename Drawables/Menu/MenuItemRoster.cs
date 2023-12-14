using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.Devices;

namespace ESuite.Drawables
{
    internal class MenuItemRoster : MenuItem
    {
		int headerHeight;
		float headerTextScale;
		float radioMarginPercentage = 0.1f;
		//Percentage of radio button width to use as margin
		float headerMarginPercentage = 0.1f;
		Rectangle headerArea;
		Vector2 [] headerTextTopLeft;
		Rectangle [] textArea;
		Rectangle [][] radioArea;
		private List<object> options;
        private Dictionary<object, Color> colorMapper;
        bool colorPerLine;
        internal Dictionary<object, object> selections = new Dictionary<object, object>();
		private Texture2D radioSelected;
		private Texture2D radioUnselected;

        internal MenuItemRoster(int headerHeight, string text, DrawableCallbackDelegate onValueChanged, object delegateArgument, List<object> options, Dictionary<object, object> selections, Dictionary<object, Color> colorMapper, bool colorPerLine) :
            base (text, onValueChanged, delegateArgument, null, ExpansionMode.None, false)
		{
			this.options = options;
			this.selections = selections;
			this.headerHeight = headerHeight;
            this.colorMapper = colorMapper;
            this.colorPerLine = colorPerLine;
			radioSelected = LoadPng ("checkbox-selected");
			radioUnselected = LoadPng ("checkbox-unselected");
		}

        protected override void DrawInternal(GameTime time)
		{
			spriteBatch.Begin ();
			//Background
			spriteBatch.Draw (whiteTexture, Boundaries, MappedColor.MenuTransparantBackground.C());

			for (int j = 0; j < options.Count (); j++)
				spriteBatch.DrawString (menuFont, options[j].ToString(), headerTextTopLeft [j], colorPerLine ? fgColor : colorMapper[options [j]], 0f, Vector2.Zero, headerTextScale, SpriteEffects.None, 0f);


			for (int i = 0; i < selections.Count; i++) {
                Color c = colorPerLine ? colorMapper[selections.Keys.ElementAt(i)] : fgColor;
				spriteBatch.DrawString (menuFont, selections.Keys.ElementAt(i).ToString(), new Vector2 (textArea [i].X, textArea [i].Y), colorPerLine ? c : fgColor);
				for (int j = 0; j < options.Count (); j++) {
					bool selected = selections.ElementAt(i).Value.Equals(options [j]);
                    spriteBatch.Draw(selected ? radioSelected : radioUnselected, radioArea[i][j], colorPerLine ? c : colorMapper[options[j]]);
				}
			}
			spriteBatch.End ();
		}

		protected override void ComputeScales ()
		{
            base.ComputeScales();
			//Header
			headerArea = new Rectangle (Boundaries.X, Boundaries.Y, Boundaries.Width, headerHeight);

			//radio button matrix
			float textWidth = 0.2f; //20% for text...
			int textAreaWidth = (int) Math.Round (Boundaries.Width * textWidth);
			int lineSize = (int) Math.Round ((Boundaries.Height - headerHeight) / (float) selections.Count ());
			int radioSizeWithMargin = (int) Math.Min (Math.Round ((Boundaries.Width - textAreaWidth) / (float) options.Count ()), lineSize * 2f / 3f);
			int radioMargin = (int) (radioSizeWithMargin * radioMarginPercentage);
			textArea = new Rectangle[selections.Count ()];
			radioArea = new Rectangle[selections.Count ()][];
			for (int i = 0; i < selections.Count; i++) {
				textArea [i] = new Rectangle ((int)(Boundaries.X + Margin.X), (int)(headerArea.Bottom + i * lineSize + Margin.Y), (int)(textAreaWidth - 2 * Margin.X), (int)(lineSize - 2 * Margin.Y));

				radioArea [i] = new Rectangle[options.Count ()];
				for (int j = 0; j < options.Count (); j++) {
					int left = Boundaries.Right - (options.Count () - j) * radioSizeWithMargin;
					int middle = (int) Math.Round (headerArea.Bottom + (i + 0.5f) * lineSize);
					int radioSize = (int)(radioSizeWithMargin - Margin.X);
					radioArea [i] [j] = new Rectangle (left, middle - radioSize / 2, radioSize, radioSize);
				}
			}

			int headerMarginTop = (int) Math.Round (headerHeight * headerMarginPercentage);
			headerTextScale = (headerHeight - headerMarginTop) / (float) menuFont.LineSpacing;
			headerTextTopLeft = new Vector2[options.Count ()];
			for (int j = 0; j < options.Count (); j++) {
				float headerTextWidth = menuFont.MeasureString (options [j].ToString()).Y * headerTextScale;
				headerTextTopLeft [j] = new Vector2 ((int) Math.Round (radioArea [0] [j].X + radioSizeWithMargin / 2f - headerTextWidth / 2f), headerArea.Bottom - (int) (menuFont.LineSpacing * headerTextScale));
			}
		}

		new private void DoTap (Point position)
		{
			object selector = null;
			object value = null;
			for (int i = 0; i < selections.Count; i++) {
				for (int j = 0; j < options.Count (); j++) {
					if (radioArea [i] [j].Contains (position)) {
						selector = selections.Keys.ElementAt(i);
						value = options[j];
					}
				}
			}
			if (value != null && action != null) {
				action.Call (this, new object[] {
					selector,
					value
				});
			}
		}

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.Tap:
                    DoTap(Utils.VectorToPoint(gesture.Position));
                    ReleaseGestureControl();
                    break;
                case GestureType.Hold:
                case GestureType.DragComplete:
                    ReleaseGestureControl();
                    break;
                default:
                    break;
            }
        }
    }
}
