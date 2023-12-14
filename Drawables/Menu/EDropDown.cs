using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables
{
    internal class EDropDown : EDrawable
    {
        private Rectangle arrowBoundaries;
        private Vector2 textLocation;
        
        private Texture2D arrowTexture;
        private SpriteFont font;
        
        private List<EDropDownItem> items;
        internal bool useShortText = false;
        private EDropDownItem currentlySelectedItem;
        private bool currentlyExpanded = false;
        public override Point? Size { get { return new Point(Boundaries.Width, Boundaries.Height); } }

        public EDropDownItem CurrentlySelectedItem { get { return currentlySelectedItem; } }

        public EDropDown(List<EDropDownItem> items, int defaultIndex)
            : base()
        {               
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };           

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag;

            this.items = items;
            foreach (EDropDownItem i in items)
            {
                AddChild(i);
                i.Visible = false;
            }

            this.currentlySelectedItem = items[defaultIndex];

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            this.arrowTexture = LoadPng(Scaler.ScaledImageName("widget-arrow-up"));
            this.font = content.Load<SpriteFont>(Scaler.GetFontButtonBar());
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(arrowTexture, arrowBoundaries, MappedColor.Selected.C());
			spriteBatch.End(); spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            spriteBatch.DrawString(font, currentlySelectedItem.Text, textLocation, MappedColor.Font.C());
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {
            arrowBoundaries = new Rectangle(Boundaries.Left + Scaler.MenuItemMargin.X, Boundaries.Center.Y - arrowTexture.Height / 2, arrowTexture.Width, arrowTexture.Height);

            //update Boundaries based on actual font size
            float longestItem = 0;
            foreach(var item in items) {
                float curItemLength = font.MeasureString(item.Text).X;
                if (curItemLength > longestItem)
                    longestItem = curItemLength; 
            }
            Rectangle newBoundaries = Boundaries;
            newBoundaries.Width = (int)(arrowBoundaries.Right + 2*Scaler.MenuItemMargin.X + longestItem - Boundaries.Left);
            this.Boundaries = newBoundaries;
            this.interactiveAreas = new List<Rectangle>() { Boundaries };

            var currentItemSize = font.MeasureString(currentlySelectedItem.Text);
            for (int i = 0; i < items.Count; i++)
            {
                EDropDownItem item = items[items.Count-1-i]; //needed if you want the first-defined to be displayed on top
                int itemHeight = (int)currentItemSize.Y * 2;
                Rectangle childLocation = new Rectangle(Boundaries.Left, Boundaries.Top - itemHeight * (i + 1), Boundaries.Width, itemHeight);
                item.SetBoundaries(childLocation);
            }
            textLocation = new Vector2((int)(arrowBoundaries.Right + (Boundaries.Width - arrowBoundaries.Width - Scaler.MenuItemMargin.X - currentItemSize.X) / 2), arrowBoundaries.Center.Y - (int)(font.MeasureString(currentlySelectedItem.Text).Y / 2f));
        }

        public bool Collapse()
        {
            bool anythingNeededToBeHidden = false;
            foreach (EDropDownItem item in items)
            {
                anythingNeededToBeHidden |= item.Visible;
                item.Visible = false;
            }
            anythingNeededToBeHidden |= currentlyExpanded;
            currentlyExpanded = false;
            return anythingNeededToBeHidden;
        }

        public void Expand()
        {
            foreach (EDropDownItem item in items)
                item.Visible = true;
            currentlyExpanded = true;
        }

        public void SelectItemByTag(object value)
        {
            EDropDownItem i = (EDropDownItem)children.Single(x => x.Tag.Equals(value));
            SelectItem(i);
        }

        public void SelectItem(EDropDownItem selectedItem)
        {
            if (!children.Contains(selectedItem))
                throw new Exception("Can't select dropdown item since item is not child of dropdown");
            currentlySelectedItem = selectedItem;
            Collapse();
            OnBoundariesChanged();
        }

        protected void OnTap()
        {
            FullRedrawRequired = true;
            UICallbacks.ToggleDropDownMenu(this, currentlyExpanded);
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DragComplete:
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    OnTap();
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
    }
}
