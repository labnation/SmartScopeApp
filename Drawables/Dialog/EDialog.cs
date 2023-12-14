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

namespace ESuite.Drawables
{
    internal class EDialog:EDrawable
    {
        protected DialogItem contents;

        protected Rectangle contentBoundaries;
        protected Vector2 marginOld = new Vector2(10f, 10f);
        private Color titleColor = Color.White;
        private Color messageColor = Color.White;
        
        private float textScale = 1f;
        private float messageScaler = 1f;
        private string message;

        protected struct ClickableArea
        {
            public Rectangle area;
            public DrawableCallback callback;
        }
        protected List<ClickableArea> clickableAreas;

        static readonly Point DEFAULT_MARGIN = new Point(10, 10);
        static readonly Point DEFAULT_MARGIN_LABEL = new Point(4, 4);

        [Flags]
        public enum Border
        {
            None    = 0,
            Top     = 1 << 0,
            Right   = 1 << 1,
            Bottom  = 1 << 2,
            Left    = 1 << 3,
            All     = Top | Right | Bottom | Left,
        }
        public enum Alignment
        {
            Left,
            Right,
            Center
        }
        public enum FontSize
        {
            Normal,
            Big
        }
        public abstract class DialogItem
        {
            public Color background = Color.Transparent;
            public Border border = Border.None;
            public Color borderColor = Color.Yellow;
            public Point margin = DEFAULT_MARGIN;
        }

        public class Column:DialogItem
        {
            public float width;
            public List<DialogItem> contents;

            public Column(float width = 1f, List<DialogItem> contents = null) : this(width, Border.None, contents) { }
            public Column(float width, Border border, List<DialogItem> contents = null) { 
                this.width = width; 
                this.contents = contents;
                this.border = border;
            }
        }
        public class Row:DialogItem
        {
            public float height = 1f;
            public List<DialogItem> contents;
            public Row(float height = 1f, List<DialogItem> contents = null) : this(height, Border.None, contents) { }
            public Row(float height, Border border, List<DialogItem> contents = null) { 
                this.height = height; 
                this.contents = contents;
                this.border = border;
            }
        }
        public class Label : DialogItem
        {
            public bool fillVertically = false;
            public string text;
            public Color textColor = Color.White;
            public Alignment textAlignment = Alignment.Left;
            public FontSize fontSize = FontSize.Normal;
            public Label() { this.margin = DEFAULT_MARGIN_LABEL; }

        }
        public class Button : Label
        {
            public List<KeyCombo> listenKeys = new List<KeyCombo>();
            public DrawableCallback callback;
            public Button() : base() { 
                border = Border.All;
                textAlignment = Alignment.Center;
            }
            public Button(string text, DrawableCallback callback)
                : this()
            {
                this.text = text;
                this.callback = callback;
            }
        }

        protected struct DrawBatchStringItem
        {
            public string text;
            public Vector2 offset;
            public Color color;
            public float scale;
            public SpriteFont font;
        }
        protected struct DrawBatchItem
        {
            public Rectangle rectangle;
            public Color color;
        }
        protected List<DrawBatchStringItem> drawBatchStrings;
        protected List<DrawBatchItem> drawBatchRectangles = new List<DrawBatchItem>();
        protected Dictionary<List<KeyCombo>, DrawableCallback> keysToCallbackMap = new Dictionary<List<KeyCombo>,DrawableCallback>();

        public EDialog(string message)
            : base()
        {
            this.keyboardHandler = keyHandler;
            this.message = message;

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };
        }

        public EDialog(EDialog.DialogItem contents) : base()
        {            
            this.contents = contents;
            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            defaultFont = content.Load<SpriteFont>(Scaler.GetFontDialog());
        }

        protected override void DrawInternal(GameTime time)
        {
            //simply draw the image to span the entire screen
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            Texture2D pixel = new Texture2D(device, 1, 1, true, SurfaceFormat.Color);
            Color c = Color.White;
            pixel.SetData(new Color[] {c});
            spriteBatch.Draw(pixel, Boundaries, new Color(10,10,10, 190));

            Vector2 topLeftContent = new Vector2(contentBoundaries.X, contentBoundaries.Y);
            if (drawBatchStrings != null)
                foreach (DrawBatchStringItem d in drawBatchStrings)
                    spriteBatch.DrawString(d.font != null ? d.font : defaultFont, d.text, topLeftContent + d.offset, d.color, 0f, Vector2.Zero, d.scale, SpriteEffects.None, 0f);

            if (drawBatchRectangles != null)
                foreach (DrawBatchItem d in drawBatchRectangles)
                    spriteBatch.Draw(pixel, d.rectangle, d.color);
            spriteBatch.End();
        }

        protected static void generateBorders(Rectangle Boundaries, Border borders, Color borderColor, List<DrawBatchItem> drawBatch)
        {
            if (borderColor == Color.Transparent)
                return;

            if (borders.HasFlag(Border.Top))
                drawBatch.Add(new DrawBatchItem() { 
                    color = borderColor,
                    rectangle = new Rectangle(Boundaries.X, Boundaries.Y, Boundaries.Width, 1)
                });
            if (borders.HasFlag(Border.Bottom))
                drawBatch.Add(new DrawBatchItem()
                {
                    color = borderColor,
                    rectangle = new Rectangle(Boundaries.X, Boundaries.Bottom - 1, Boundaries.Width, 1)
                });
            if (borders.HasFlag(Border.Left))
                drawBatch.Add(new DrawBatchItem()
                {
                    color = borderColor,
                    rectangle = new Rectangle(Boundaries.X, Boundaries.Y, 1, Boundaries.Height)
                });
            if (borders.HasFlag(Border.Right))
                drawBatch.Add(new DrawBatchItem()
                {
                    color = borderColor,
                    rectangle = new Rectangle(Boundaries.Right - 1, Boundaries.Y, 1, Boundaries.Height)
                });
        }

        /// <summary>
        /// Returns space taken by contents
        /// </summary>
        /// <param name="content"></param>
        /// <param name="Boundaries"></param>
        /// <param name="topLeft"></param>
        /// <param name="drawBatchRectangles"></param>
        /// <param name="drawBatchStrings"></param>
        /// <param name="clickableAreas"></param>
        /// <returns></returns>
        protected Rectangle parseContent(
            DialogItem content, Rectangle Boundaries, Point topLeft, 
            List<DrawBatchItem> drawBatchRectangles, List<DrawBatchStringItem> drawBatchStrings, List<ClickableArea> clickableAreas)
        {
            if (content == null) return Rectangle.Empty;
            if (content is Row)
            {
                Row r = content as Row;
                Rectangle rowBoundaries = new Rectangle(
                    topLeft.X, topLeft.Y, Boundaries.Width, (int)Math.Round(Boundaries.Height * r.height));
                Rectangle contentBoundaries = new Rectangle(
                    rowBoundaries.X + r.margin.X, rowBoundaries.Y + r.margin.Y, rowBoundaries.Width - 2*r.margin.X, rowBoundaries.Height - 2*r.margin.Y);
                if(r.background != Color.Transparent)
                    drawBatchRectangles.Add(new DrawBatchItem() { color = r.background, rectangle = rowBoundaries });
                generateBorders(rowBoundaries, r.border, r.borderColor, drawBatchRectangles);
                Point topLeftContents = new Point(contentBoundaries.X, contentBoundaries.Y);
                if (r.contents != null)
                {
                    foreach (DialogItem i in r.contents)
                    {
                        Rectangle spaceTaken = parseContent(i, contentBoundaries, topLeftContents, drawBatchRectangles, drawBatchStrings, clickableAreas);
                        topLeftContents.X += spaceTaken.Width;
                    }
                }
                return rowBoundaries;
            }
            else if (content is Column)
            {
                Column c = content as Column;
                Rectangle colBoundaries = new Rectangle(
                    topLeft.X, topLeft.Y, (int)Math.Round(Boundaries.Width * c.width), Boundaries.Height);
                Rectangle contentBoundaries = new Rectangle(
                    colBoundaries.X + c.margin.X, colBoundaries.Y + c.margin.Y, colBoundaries.Width - 2 * c.margin.X, colBoundaries.Height - 2 * c.margin.Y);
                if (c.background != Color.Transparent)
                    drawBatchRectangles.Add(new DrawBatchItem() { color = c.background, rectangle = colBoundaries });
                generateBorders(colBoundaries, c.border, c.borderColor, drawBatchRectangles);
                Point topLeftContents = new Point(contentBoundaries.X, contentBoundaries.Y);
                if (c.contents != null)
                {
                    foreach (DialogItem i in c.contents)
                    {
                        Rectangle spaceTaken = parseContent(i, contentBoundaries, topLeftContents, drawBatchRectangles, drawBatchStrings, clickableAreas);
                        topLeftContents.Y += spaceTaken.Height;
                    }
                }
                return colBoundaries;
            }
            else if (content is Label)
            {
                Label l = content as Label;

                SpriteFont font = l.fontSize == FontSize.Big ? bigFont : defaultFont;

                Point textDimensions = Utils.VectorToPoint(font.MeasureString(l.text));
                Rectangle labelBoundaries;
                if(l.fillVertically)
                    labelBoundaries = new Rectangle(
                    topLeft.X, topLeft.Y, Boundaries.Width - (topLeft.X - Boundaries.X), Boundaries.Height - (topLeft.Y - Boundaries.Y));
                else
                    labelBoundaries = new Rectangle(
                    topLeft.X, topLeft.Y, Boundaries.Width - (topLeft.X - Boundaries.X), textDimensions.Y + 2* l.margin.Y);

                
                if (l.background != Color.Transparent)
                    drawBatchRectangles.Add(new DrawBatchItem() { color = l.background, rectangle = labelBoundaries });

                Point tempPoint = new Point(labelBoundaries.Location.X + l.margin.X, labelBoundaries.Location.Y + l.margin.Y);
                Vector2 textLocation = Utils.PointToVector(tempPoint);
                switch(l.textAlignment) {
                    case Alignment.Left:
                        break;
                    case Alignment.Center:
                        textLocation.X = labelBoundaries.Center.X - textDimensions.X / 2;
                        break;
                    case Alignment.Right:
                        textLocation.X = labelBoundaries.Right - textDimensions.X - l.margin.X;
                        break;
                }
                drawBatchStrings.Add(new DrawBatchStringItem() { color = l.textColor, offset = textLocation, scale = 1f, text = l.text, font = font });

                generateBorders(labelBoundaries, l.border, l.borderColor, drawBatchRectangles);

                if (content is Button)
                {
                    clickableAreas.Add(new ClickableArea() { area = labelBoundaries, callback = ((Button)content).callback });
                    Button b = (Button)content;
                    if (b.listenKeys != null && b.listenKeys.Count != 0)
                    {
                        keysToCallbackMap.Add(b.listenKeys, b.callback);
                    }
                }
                return labelBoundaries;
            }
            else
            {
                throw new Exception("Unknown content item");
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            this.interactiveAreas[0] = Boundaries;
            clickableAreas = new List<ClickableArea>();
            drawBatchStrings = new List<DrawBatchStringItem>();

            if (contents != null)
            {
                //Recurse through items
                drawBatchRectangles = new List<DrawBatchItem>();
                
                parseContent(contents, Boundaries, Boundaries.Location, drawBatchRectangles, drawBatchStrings, clickableAreas);
            }
            else
            {
                contentBoundaries = Boundaries;
                contentBoundaries.Width = (int)Math.Round(Boundaries.Width * 0.4f);
                ComputeContentSize();

                CenterContentBox();
            }
        }

        protected void CenterContentBox()
        {
            //Center content box
            contentBoundaries = new Rectangle(
                (int)Math.Round(Boundaries.X + Boundaries.Width / 2f - contentBoundaries.Width / 2f),
                (int)Math.Round(Boundaries.Y + Boundaries.Height / 2f - contentBoundaries.Height / 2f),
                contentBoundaries.Width,
                contentBoundaries.Height
            );
        }

        private void ComputeContentSize()
        {
            Vector2 offset = marginOld;

            int textAreaWidth = (int)(contentBoundaries.Width - offset.X - marginOld.X);
            if (textAreaWidth <= 0) {
                LabNation.Common.Logger.Error("Dialog text area <= 0");
                return;
            }

            foreach (string s in message.Split('\n'))
            {
                int strpos = 0;
                while (strpos < s.Length)
                {
                    if (s[strpos] == ' ') { strpos++; continue; }
                    //Add words so it fits
                    int length = 0;
                    int nextWordOffset;
                    do
                    {
                        nextWordOffset = s.IndexOf(" ", strpos + length + 1);
                        if (nextWordOffset < 0)
                            nextWordOffset = s.Length;

                        if (defaultFont.MeasureString(s.Substring(strpos, nextWordOffset - strpos)).X * textScale * messageScaler > textAreaWidth)
                            break;

                        length = nextWordOffset - strpos;
                    } while (nextWordOffset < s.Length);
                    //If splitting by word wasn't possible and we got a too long string, rewind per character
                    if (length == 0)
                        length = s.Length - strpos;

                    while (defaultFont.MeasureString(s.Substring(strpos, length)).X * textScale * messageScaler > textAreaWidth)
                        length--;

                    string msgline = s.Substring(strpos, length);
                    strpos += length;

                    drawBatchStrings.Add(new DrawBatchStringItem() { text = msgline, offset = offset, color = titleColor, scale = textScale * messageScaler });
                    offset += new Vector2(0f, defaultFont.LineSpacing * textScale * messageScaler + marginOld.Y);
                }
                offset += new Vector2(0f, defaultFont.LineSpacing * textScale * messageScaler + marginOld.Y);

                contentBoundaries.Height = (int)Math.Round(offset.Y + marginOld.Y);
            }
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    try
                    {
                        clickableAreas.Where(x => x.area.Contains(Utils.VectorToPoint(gesture.Position))).First().callback.Call(this);
                    }
                    catch (InvalidOperationException) { }
                    ReleaseGestureControl();
                    break;
                case GestureType.FreeDrag:
                case GestureType.Pinch:
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }

        public bool keyHandler(EDrawable dialog, KeyboardState ks)
        {
            //Walk through elements and check for listening keys
            foreach (var keyAndCallback in keysToCallbackMap)
                foreach (KeyCombo kc in keyAndCallback.Key)
                    if (ks.HasCombo(kc))
                    {
                        keyAndCallback.Value.Call();
                        return true;
                    }

            //Since it's a dialog, we catch all keys
            return true;
        }
    }
}
