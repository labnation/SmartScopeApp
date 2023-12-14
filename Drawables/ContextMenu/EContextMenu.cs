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
    internal class EContextMenu : EContextMenuItem
    {
        public enum Orientation
        {
            Horizontal,
            Vertical
        };
        private Orientation orientation;
        public EDrawable Owner { get; private set; }
        private Point location = new Point();

        private Texture2D backgroundTexture;
        private DrawableCallback closeCallback;
        private GraphManager gm;

        //When making invisible, call the onClose callback
        new internal bool Visible {
            get { return base.Visible; }
            set {
                if(base.Visible && value == false && closeCallback != null) {
                    closeCallback.Call(this);
                }
                base.Visible = value;
            }
        }

        public EContextMenu(GraphManager gm, Orientation orientation = Orientation.Horizontal)
            : base()
        {
            this.gm = gm;
            this.orientation = orientation;
            this.Visible = false;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            this.backgroundTexture = whiteTexture;
        }

        protected override void DrawInternal(GameTime time)
        {
            //draw transparent background
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(backgroundTexture, Boundaries, new Color(255, 255, 255, 230));
            spriteBatch.End();

            //now re-draw the owner on top of the context menu!
            //FIXME: this has the potential of drawing a LOT of contextMenus... since they're stacked
            Owner.Draw(time);
        }

        public virtual void Show(EDrawable owner, Point location, List<EContextMenuItem> itemList, DrawableCallback closeCallback = null)
        {
            this.location = location;
            this.Owner = owner;
            //Hide the menu in case it's open, including all its children
            this.Visible = false;
            this.closeCallback = closeCallback;

            int childSize = Scaler.ContextMenuSize.InchesToPixels();

            //first find out whether items need to be positioned to left or right of touch
            Point offset = Point.Zero;
            Point childOffset = Point.Zero;
            Rectangle graphBoundaries = gm.Boundaries;

            switch (orientation)
            {
                case Orientation.Horizontal:
                    if (location.X + itemList.Count * childSize > graphBoundaries.Right)
                        offset.X -= (itemList.Count + 1) * childSize;
                    childOffset.X = childSize;
                    break;
                case Orientation.Vertical:
                    if (location.Y + itemList.Count * childSize > graphBoundaries.Bottom)
                        offset.Y -= (itemList.Count + 1) * childSize;
                    if (location.Y + offset.Y - childSize / 2f < graphBoundaries.Top)
                        offset.Y = (int)(graphBoundaries.Top - location.Y - childSize / 2f);
                    childOffset.Y = childSize;
                    break;
            }

            //position all child items to the right of the touch
            ClearChildren();
            for (int i = 0; i < itemList.Count; i++)
            {
                EContextMenuItem item = itemList[i];
                Rectangle childRectangle = new Rectangle(0, 0, childSize, childSize);
                childRectangle.Location += offset + location + new Point(-childSize / 2, -childSize / 2);
                childRectangle.Location += new Point(i + 1, i + 1) * childOffset;
                item.SetBoundaries(childRectangle);
                item.Visible = true;
                AddChild(item);
            }
            
            this.Visible = true;

            OnBoundariesChanged();
        }

        public void Collapse()
        {
            foreach (EDrawable d in children)
            {
                if (d is EContextMenuDropdown)
                    ((EContextMenuDropdown)d).Collapse();
                if (d is EContextMenuItemButtonNumpad)
                    ((EContextMenuItemButtonNumpad)d).Collapse();
            }
        }
        protected override void OnBoundariesChangedInternal()
        {
            int childSize = Scaler.ContextMenuSize.InchesToPixels();
            this.Boundaries = new Rectangle(location.X - childSize / 2, location.Y - childSize / 2, childSize, childSize);
        }

        protected override void HandleGestureInternal(GestureSample gesture)
        {        
        }
    }
}
