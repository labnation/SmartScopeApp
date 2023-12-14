using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Content;
using LabNation.DeviceInterface.Devices;

namespace ESuite.Drawables
{
    internal class Menu: EDrawable
    {
        static internal bool MenuCurrentlyDisplayed = false;
        List<EDrawable> ItemDrawOrderList;
        bool DrawOrderListStale = true;

        internal Rectangle displayBoundaries
        {
            get
            {
                if (this.parentItem == null)
                {
                    //A bit dirty, but hey, plenty of hardcoded shit in this menu already
                    Rectangle screenboundaries = Utils.ScreenBoundaries(device, Matrix.Identity, View, Projection);
                    return new Rectangle(0, 0, screenboundaries.Width, boundaries.Height);
                }
                else
                {
                    return parentItem.menu.displayBoundaries;
                }
            }
        }

        internal ExpansionMode ExpansionMode { get; private set; }
        new public bool Visible
        {
            get { return base.Visible; }
            set
            {
                foreach (EDrawable i in children)
                    i.Visible = value;
                base.Visible = value;
            }
        }
        internal LerpMatrix WorldAnimation = new LerpMatrix(Matrix.Identity, Matrix.Identity, ColorMapper.AnimationTime);

        internal SpriteFont font { get; private set; }

        internal MenuItem parentItem { get { return parent as MenuItem; } }
        internal Menu root
        {
            get
            {
                if (parentItem == null) return this;
                return parentItem.menu.root;
            }
        }
        internal List<MenuItem> Items { get { return children.Where(x => x is MenuItem).Select(x => x as MenuItem).ToList(); } }

        internal SelectableMode selectableMode = SelectableMode.None;
        internal List<MenuItem> selectedItems = new List<MenuItem>();

        internal int Width { get { return Items.Count() > 0 ? Items.Select(x => x.Width).Max() : 0; } }
        internal int Height { get { return children.Select(x => (x as MenuItem).Height).Sum(); } }
        internal Point ItemSize { get { return Scaler.MenuItemSize; } }
        public override Point? Size { get { return new Point(Width, Height); } }
        public int MaxHeight
        {
            get
            {
                int nrOfItems = Items.Count;
                if(Items.Where(x => x.expansionMode== Drawables.ExpansionMode.Harmonica).Count() > 0)
                    nrOfItems += Items.Where(x => x.expansionMode == Drawables.ExpansionMode.Harmonica).Select(x => x.childMenu.Items.Count).Max();
                int maxHeightSubItems = 0;
                if (Items.Where(x => x.expansionMode == Drawables.ExpansionMode.Sidemenu).Count() > 0)
                    maxHeightSubItems = Items.Where(x => x.expansionMode == Drawables.ExpansionMode.Sidemenu).Select(x => x.childMenu.MaxHeight).Max();
                return Math.Max(nrOfItems * ItemSize.Y, maxHeightSubItems);
            }
        }
        
        internal Menu(ExpansionMode expansionMode, SelectableMode selectableMode)
            : base()
        {
            //Support all gestures so that drags don't fall through the sidemenu
            font = content.Load<SpriteFont>(Scaler.GetFontSideMenu());

            this.ExpansionMode = expansionMode;
            this.selectableMode = selectableMode;
            this.supportedGestures = GestureType.Tap | GestureType.FreeDrag | GestureType.DragComplete;
        }

        protected override void LoadContentInternal()
        {
        }

        internal void SetItems(List<MenuItem> items)
        {
            this.ClearChildren();
            List<MenuItem> itemsNotNull = items.Where(x => x != null).ToList();
            foreach (MenuItem item in itemsNotNull)
            {
                //Update menu selected items before 
                bool selected = item.Selected;
                this.AddChild(item);
                this.Select(item, selected);
            }
           
            if (selectableMode == SelectableMode.None && selectedItems.Count > 0)
                throw new Exception("Can't set items selected when selectabe mode is None");
            if (selectableMode == SelectableMode.Single && selectedItems.Count > 1)
                throw new Exception("Can't set multiple items selected when selectabe mode is Single");
            root.DrawOrderListStale = true;
        }

        internal void AddItem(MenuItem newItem)
        {
            AddChild(newItem);
            //FIXME: calling this here because otherwise it takes 2 draw calls for the submenus to be updated
            //Might rethink this, i.e. by making menu AND menuitems on same level or so, instead of menu being child of menuItem
            RecalcMenuImmediate();
            root.DrawOrderListStale = true;
        }

        internal void RemoveItem(MenuItem item)
        {
            RemoveChild(item);
            RecalcMenuImmediate();
        }

        private void RecalcMenuImmediate()
        {
            if (this.parentItem != null)
                this.parentItem.ComputeChildMenuMatrix(false);
            this.World = WorldAnimation.CurrentValue;
            this.ComputeMenuItemMatrices(false);
            OnMatrixChangedPropagating();
        }

        internal void ComputeMenuItemMatrices(bool animate = true)
        {
            int yOffset = boundaries.Y;
            foreach(MenuItem item in children)
            {
                Matrix m = Utils.RectangleToMatrix(boundaries.X, yOffset, item.Width, item.Height, device, WorldAbsolute, View, Projection);
                if (item.WorldAnimation.target == Matrix.Identity || !animate)
                {
                    item.WorldAnimation.SetTargetImmediately(m); ;
                    item.World = m;
                }
                else
                    item.WorldAnimation.UpdateTarget(m);
                yOffset += item.Height;
                if (item.expansionMode == ExpansionMode.Harmonica && item.Active)
                    yOffset += item.childMenu.Height;
            }
        }

        internal bool PassGestureControlToCheckbox(MenuItemCheckbox sender, GestureSample g)
        {
            foreach (MenuItemCheckbox i in Items.Where(x => x is MenuItemCheckbox && x != sender))
            {
                if (PassGestureControl(i, g))
                    return true;
            }
            return false;
        }

        internal void ResetCheckboxChangedByDrag()
        {
            foreach (MenuItemCheckbox i in Items.Where(x => x is MenuItemCheckbox))
                i.ResetChangedByDrag();
        }
        internal void RebuildItemsCheckboxesOrdered()
        {
            //FIXME: the actual function of this method is vague - it's written to serve one purpose. Perhaps it should be moved
            //into uihandler
            List<MenuItemCheckbox> enabledCheckboxes = Items.Where(x => x is MenuItemCheckbox).Select(x => x as MenuItemCheckbox).Where(x => x.Checked).ToList();
            List<MenuItemCheckbox> disabledCheckboxes = Items.Where(x => x is MenuItemCheckbox).Select(x => x as MenuItemCheckbox).Where(x => !x.Checked).ToList();
            List<MenuItem> notCheckbox = Items.Where(x => !(x is MenuItemCheckbox)).ToList();

            enabledCheckboxes.Sort((x, y) => string.Compare(x.text, y.text));
            disabledCheckboxes.Sort((x, y) => string.Compare(x.text, y.text));

            ClearChildren();
            foreach (MenuItemCheckbox i in enabledCheckboxes)
                AddChild(i as EDrawable);
            foreach (MenuItemCheckbox i in disabledCheckboxes)
                AddChild(i as EDrawable);
            foreach (MenuItem i in notCheckbox)
                AddChild(i);
            OnMatrixChangedPropagating();
        }

        protected override void OnMatrixChangedInternal()
        {
            boundaries = Utils.ScreenBoundaries(device, WorldAbsolute, View, Projection);
            interactiveAreas = new List<Rectangle>();
            interactiveAreas.Add(boundaries);
            ComputeMenuItemMatrices(true);
        }

        protected override void UpdateInternal(GameTime now)
        {
            if(!WorldAnimation.done && WorldAnimation.Update(now))
                this.World = WorldAnimation.CurrentValue;
        }

        internal void BuildMenuDrawOrderList()
        {
            ItemDrawOrderList = new List<EDrawable>();
            for (int i = 10; i > 0; i--)
                this.GetMenuToDrawAtLevel(ItemDrawOrderList, i, 0);

        }

        internal void GetMenuToDrawAtLevel(List<EDrawable> drawList, int level, int currentLevel)
        {
            if (level == currentLevel)
            {
                drawList.Add(this);
                return;
            }
            for (int i = Items.Count - 1; i >= 0; i--)
                if(Items[i].childMenu != null)
                    Items[i].childMenu.GetMenuToDrawAtLevel(drawList, level, currentLevel + 1);

        }

        protected override void DrawInternal(GameTime time)
        {
            if (!Visible) return;

            //draw main gray background for mostleft menu
            spriteBatch.Begin();
            spriteBatch.Draw(whiteTexture, boundaries, MappedColor.MenuSolidBackgroundVoid.C());
            spriteBatch.End();
            if (parentItem == null)
            {
                if (DrawOrderListStale)
                {
                    DrawOrderListStale = false;
                    BuildMenuDrawOrderList();
                }
                foreach (EDrawable e in ItemDrawOrderList)
                    e.Draw(time);
            }

            foreach (EDrawable e in children)
                e.Draw(time);
        }

        internal override void HandleGesture(GestureSample gesture)
        {
            for (int i = 0; i < 10; i++)
                this.HandleGestureAtLevel(i, 0, gesture);

            if (!ScopeApp.GestureMustBeReleased && ShouldHandleGesture(gesture))
                this.HandleGestureInternal(gesture);
        }

        internal void HandleGestureAtLevel(int level, int currentLevel, GestureSample gesture)
        {
            if (level == currentLevel)
            {
                for(int i = 0; i < children.Count; i++)
                    children[i].HandleGesture(gesture);
                return;
            }
            foreach (MenuItem i in Items)
                if (i.childMenu != null)
                    i.childMenu.HandleGestureAtLevel(level, currentLevel + 1, gesture);
        }

        internal bool Select(object tag, bool select)
        {
            MenuItem m;
            if (!(tag is MenuItem))
                m = (MenuItem)children.Single(x => tag.Equals(x.Tag));
            else
                m = (MenuItem)tag;
            return Select(m, select);
        }

        internal bool Select(MenuItem menuItem, bool select)
        {
            if (!children.Contains(menuItem))
                throw new Exception("Can't select this item since it's not a child");

            if(selectableMode == SelectableMode.None) {
                    selectedItems.Clear();
                    return false;
            }

            if (selectableMode == SelectableMode.Single && select)
                selectedItems.Clear();

            if(select)
                selectedItems.Add(menuItem);
            else
                selectedItems.Remove(menuItem);

            OnMatrixChangedPropagating();
            return select;
        }

        protected void DoTap(Point location)
        {
            UICallbacks.ToggleGlobalMenu(this, null);
        }

        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
                case GestureType.DragComplete:
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    DoTap(new Point((int)gesture.Position.X, (int)gesture.Position.Y));
                    ReleaseGestureControl();
                    break;
                case GestureType.Hold:
                case GestureType.FreeDrag:
                    //ReleaseGestureControl must not be called here! Or DragComplete will not cause a Tap
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }
    }
}
