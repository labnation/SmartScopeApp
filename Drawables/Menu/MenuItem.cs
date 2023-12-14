using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using ESuite;

namespace ESuite.Drawables
{
    internal enum ExpansionMode
    {
        None,
        Harmonica,
        Sidemenu,
        Carousel
    }    

    internal class MenuItem : EDrawable
    {
        private MenuPanel menuPanel;
        internal int MenuLevel { get; private set; } //value indicating the PANEL of this item. Main entries of panel 0 are level 0. Their harmonica items are level 0, but their sidemenu items are level 1 (as they are on panel 1). Simple.
        internal List<MenuItem> ChildMenuItems { get; private set; }
        private List<MenuItem> childRadioGroupMembers;
        protected SpriteFont menuFont;
        internal bool IsPartOfRadioGroup { get; private set; }
        internal MenuItem ParentMenuItem { get; private set; }
        internal int MaxItemsOnChildPanel { get; private set; }
        internal int? RadioGroupID { get; private set; }
        private bool left_nRight_aligned;
        private Location tapSide;
        private Color textColor;
        private int largestHarmonicaChild = 0;
        public int LargestHarmonicaChild { get { return largestHarmonicaChild; } }
        private bool selectable = true;

        public virtual string text { get; protected set; }
        internal DrawableCallback action { get; private set; }
        internal DrawableCallback TapCallback { get; set; }
        internal DrawableCallback DoubleTapCallback { get; set; }
        internal ExpansionMode ExpansionMode { get; private set; }
        
        public override Point? Size { get { return Scaler.MenuItemSize; } }
        protected Point Margin { get { return Scaler.MenuItemMargin; } }
        protected Texture2D arrowTexture;
        protected Vector2 rightArrowTopLeft;
        protected Vector2 leftArrowTopLeft;

        protected SpriteFont topFont;
        protected SpriteFont subFont;

        protected Vector2 textLocation = new Vector2();
        protected SpriteFont usedFont;
        protected Color bgColor;
        protected Color fgColor;

        Rectangle radioArea;
        private Texture2D radioSelectedTexture;
        private Texture2D radioUnselectedTexture;

        //Selected: when member of radiogroup is selected, or when checkbox is selected, or when item is highlighted
        //Active: when its childMenu is open

        private bool selected;
        internal bool Selected
        {
            get
            {
                return selected;
            }
            set
            {
                if (!selectable)
                    return;

                //call this first, as otherwise this item might open a sidemenu only to be closed afterwards by groupmember
                if (value && ParentMenuItem != null && this.RadioGroupID.HasValue)
                    ParentMenuItem.RadioGroupSelect(this);
                
                selected = value;

                //if this gets selected or deselected, immediately show/hide submenus
                SubMenuActive = value;   
            }
        }
        public bool AutoChangeOnTap { get; set; }

        private bool subMenuActive;
        internal bool SubMenuActive
        {
            get { return subMenuActive; }
            set
            {
                if (value == subMenuActive)
                {
                    ComputeScales(); //first call this, as SubMenuActive can be called from Selected
                    return;
                }

                subMenuActive = value;

                ComputeScales();

                if (ChildMenuItems != null)
                {
                    //when deactivated: propagate to children
                    //call this first, so ShowPanel of this level overrides any potential ShowLevel calls called by children
                    if (!subMenuActive)
                    {
                        if (ExpansionMode == ExpansionMode.Harmonica && selected && !ParentMenuItem.SubMenuActive) //makes sure that the currently selected harmonica isn't closed when the main menu is closed
                            subMenuActive = true;
                        else if (ExpansionMode == ExpansionMode.Sidemenu && !subMenuActive) //sidemenu have no memory: when deactivated, also unselect them. But they should be Selectable and Deselectable, so they can be opened and closed when clicked upon.
                            selected = false;

                        if (ExpansionMode != ExpansionMode.Carousel)
                            foreach (var item in ChildMenuItems)
                                item.SubMenuActive = false;
                    }

                    if (ExpansionMode == ExpansionMode.Harmonica)
                    {
                        if (subMenuActive)
                            menuPanel.ShowHarmonicaItems(MenuLevel, this, true);
                        else
                            menuPanel.HideHarmonicaItems(MenuLevel, this, true);
                    }
                    else if (ExpansionMode == ExpansionMode.Carousel)
                    {
                        //first find currently active child
                        int currentlyActiveChild = 0;
                        for (int i = 0; i < ChildMenuItems.Count; i++)
                            if (ChildMenuItems[i].SubMenuActive)
                                currentlyActiveChild = i;
                        ChildMenuItems[currentlyActiveChild].subMenuActive = false;

                        //select next child
                        int childToActivateID = (currentlyActiveChild + 1) % ChildMenuItems.Count;
                        MenuItem childToActivate = ChildMenuItems[childToActivateID];                        
                        childToActivate.subMenuActive = true;

                        //as a last dirty patch: take over text and color from child
                        this.text = childToActivate.text;
                        this.textColor = childToActivate.textColor;

                        menuPanel.SlideCarousel(MenuLevel, this, childToActivate, tapSide == Location.Right, true);
                    }
                    else if (ExpansionMode == ExpansionMode.Sidemenu)
                    {
                        if (subMenuActive)
                        {
                            //now this is a bit though. This item needs to know at which Y coordinate it will be AFTER its submenu is shown. Though, becuase this item might be moving!
                            int expectedYcoord = 0;
                            if (parent != null)
                                expectedYcoord = (parent as SplitPanel).TargetYCoord(this);

                            menuPanel.Repopulate(MenuLevel+1, expectedYcoord, this.ChildMenuItems);
                            menuPanel.ShowLevel(MenuLevel + 1, true);
                        }
                        else
                        {
                            menuPanel.ShowLevel(MenuLevel, true);
                        }
                    }                   
                }
            }
        }
        internal int Width { get { return Scaler.MenuItemSize.X; } }
        internal int Height { get { return Scaler.MenuItemSize.Y; } }

        internal MenuItem(string text, DrawableCallbackDelegate action, object delegateArgument = null, bool selected = false, bool autoChangeOnTap = true)
            : this(text, action, delegateArgument, null, ExpansionMode.None, selected, false, true, -1, autoChangeOnTap) { }
        internal MenuItem(string text, List<MenuItem> childMenuItems, ExpansionMode expansionMode, bool active = false, bool selectable = true, Color? textColor = null)
            : this(text, null, null, childMenuItems, expansionMode, false, active, selectable, -1, true, textColor) { }

        //selected means
        //    for item: with green overlay
        //    for header: submenu shown
        // 'active' probably has no meaning and could be removed
        //radioGroupID: use null if the menuitem is not part of a radiogroup (to allow multiple items to be selected simultaneously), otherwise give it a number so selecting this item will unselect all other items within the same radiogroup. basically menuitems in the entire program can all be the same value, except when you want multiple radiogroups in 1 submenu. By default they will all be set to -1.
        internal MenuItem(string text, DrawableCallbackDelegate action, object delegateArgument, List<MenuItem> childMenuItems, ExpansionMode expansionMode, bool startSelected = false, bool active = false, bool selectable = true, int? radioGroupID = -1, bool autoChangeOnTap = true, Color? textColor = null)
            : base()
        {
            AutoChangeOnTap = autoChangeOnTap;
            this.RadioGroupID = radioGroupID;

            this.text = text;
            this.textColor = textColor ?? Color.Black; //needed as we cannot have Color as optional argument
            if (action != null)
                this.action = new DrawableCallback(action, delegateArgument);
            this.subMenuActive = active;
            this.selected = startSelected;
            this.ExpansionMode = expansionMode;
            this.selectable = selectable;

            //store childList, but only non-null items
            if (childMenuItems != null)
            {
                this.ChildMenuItems = new List<MenuItem>();
                foreach (var item in childMenuItems)
                    if (item != null)
                        this.ChildMenuItems.Add(item);

                if (this.ChildMenuItems.Count == 0)
                    this.ChildMenuItems = null;
            }

            //if non-null children were found, see if they contain a radiogroup and store them in childRadioGroupMembers
            if (this.ChildMenuItems != null)
            {
                this.childRadioGroupMembers = new List<MenuItem>();
                foreach (var item in this.ChildMenuItems)
                    if (item.IsPartOfRadioGroup)
                        this.childRadioGroupMembers.Add(item);

                //if children do not contain radiogroup -> set childRadioGroupMembers to null
                if (this.childRadioGroupMembers.Count == 0)
                    this.childRadioGroupMembers = null;
            }

            this.supportedGestures = GestureType.DoubleTap | GestureType.DragComplete | GestureType.Flick | GestureType.FreeDrag | GestureType.Hold | GestureType.HorizontalDrag | GestureType.None | GestureType.Pinch | GestureType.PinchComplete | GestureType.Tap | GestureType.VerticalDrag | GestureType.MouseScroll;
            this.interactiveAreas = new List<Rectangle> { new Rectangle() };

            LoadContent();

            MustRecalcNextUpdateCycle = true;
        }

        protected override void LoadContentInternal()
        {
            if (ExpansionMode == ExpansionMode.Sidemenu || ExpansionMode == ExpansionMode.Carousel)
                this.arrowTexture = LoadPng(Scaler.ScaledImageName("widget-arrow-right"));

            this.topFont = content.Load<SpriteFont>(Scaler.GetFontSideMenu() + "Bold");
            this.subFont = content.Load<SpriteFont>(Scaler.GetFontSideMenu());
            this.radioSelectedTexture = LoadPng(Scaler.ScaledImageName("widget-radio-on"));
            this.radioUnselectedTexture = LoadPng(Scaler.ScaledImageName("widget-radio-off"));

            this.menuFont = content.Load<SpriteFont>(Scaler.GetFontSideMenu());

            //since a lot of MenuItems have no parents: propage LoadContent this way. 
            //FIXME: this will cause menuitems currently shown to load their content twice.
            if (ChildMenuItems != null)
                foreach (var item in ChildMenuItems)
                    item.LoadContentInternal();
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(whiteTexture, Boundaries, bgColor);
            if (ExpansionMode == ExpansionMode.Sidemenu)
                spriteBatch.Draw(arrowTexture, rightArrowTopLeft, null, fgColor);
            if (ExpansionMode == ExpansionMode.Carousel)
            {
                spriteBatch.Draw(arrowTexture, rightArrowTopLeft, null, fgColor);
                spriteBatch.Draw(arrowTexture, leftArrowTopLeft, null, null, null, 0, null, fgColor, SpriteEffects.FlipHorizontally, 0);
            }
            if (IsPartOfRadioGroup && ExpansionMode != ExpansionMode.Sidemenu)
            {
                if (SubMenuActive)
                    spriteBatch.Draw(this.radioSelectedTexture, radioArea, fgColor);
                else
                    spriteBatch.Draw(this.radioUnselectedTexture, radioArea, fgColor);
            }
            spriteBatch.End();

            if (usedFont != null)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
                spriteBatch.DrawString(usedFont, this.text, this.textLocation, fgColor);
                spriteBatch.End();
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            this.interactiveAreas[0] = Boundaries;

            //override boundaries so menuItems seem to slide in from the side, instead of 'grow'
            int width = Scaler.InchesToPixels(Scaler.SideMenuWidth);  
            if (ParentMenuItem != null && ParentMenuItem.ParentMenuItem != null && ParentMenuItem.ParentMenuItem.ExpansionMode != ExpansionMode.Carousel) //basically should simply detect whether this is a left- or right-sliding menu
                Boundaries = new Rectangle(Boundaries.Right - width, Boundaries.Y, width, Boundaries.Height);
            else
                Boundaries = new Rectangle(Boundaries.Left, Boundaries.Y, width, Boundaries.Height); 

            ComputeScales();
        }

        protected virtual void ComputeScales()
        {
            bgColor = (MenuLevel == 0) 
                ? MappedColor.MenuSolidBackground.C() 
                : (SubMenuActive || Selected)
                    ? MappedColor.Selected.C() 
                    : MappedColor.MenuTransparantBackground.C();
            fgColor = (MenuLevel == 0) 
                ? textColor
                : (MenuLevel == 1) 
                    ? (SubMenuActive || Selected)
                        ? Color.White 
                        : new Color(70, 70, 70) 
                    : (SubMenuActive || Selected)
                        ? Color.White 
                        : textColor;
            usedFont = (ExpansionMode == ExpansionMode.Harmonica || ExpansionMode == ExpansionMode.Carousel || SubMenuActive || Selected) ? topFont : subFont;

            //calc text position
            float extraMarginX = 0f;

            if (this.ParentMenuItem != null && this.ParentMenuItem.ExpansionMode == ExpansionMode.Harmonica)
                extraMarginX = Margin.X / 2f;

            Vector2 textSize = usedFont.MeasureString(this.text);
            if (ExpansionMode == ExpansionMode.Carousel)
                this.textLocation = new Vector2((int)(this.Boundaries.Center.X - textSize.X/2), (int)(this.Boundaries.Center.Y - textSize.Y / 2f)); //text in center of item
            else
                this.textLocation = new Vector2((int)(this.Boundaries.X + Margin.X + extraMarginX), (int)(this.Boundaries.Center.Y - textSize.Y / 2f));

            if (ExpansionMode == ExpansionMode.Sidemenu || ExpansionMode == ExpansionMode.Carousel)
            {
                rightArrowTopLeft = new Vector2(Boundaries.Right - arrowTexture.Width - Margin.X, Boundaries.Center.Y - arrowTexture.Height / 2f);
                leftArrowTopLeft = new Vector2(Boundaries.Left + Margin.X, Boundaries.Center.Y - arrowTexture.Height / 2f);
            }
            if (IsPartOfRadioGroup)
            {
                Vector2 radioTopLeft = new Vector2(Boundaries.Right - radioUnselectedTexture.Width - Margin.X, Boundaries.Center.Y - (int)(radioUnselectedTexture.Height / 2));
                radioArea = new Rectangle((int)radioTopLeft.X, (int)radioTopLeft.Y, radioUnselectedTexture.Width, radioUnselectedTexture.Height);
            }
        }        

        protected virtual void DoTap (Point location, object argument = null)
		{
            UICallbacks.CloseContextMenu(this, null);

            if (location.X < Boundaries.Center.X)
                tapSide = Location.Left;
            else
                tapSide = Location.Right;

            //AutoChangeOnTap==false 
            //whenever item is pushed, it becomes selected (eg: Harmonica headers eg "Analog Mode")
            //if it wasn't selected before, it should become active (show childMenu).
            //if it was already selected, then it should toggle the childMenu. 

            //AutoChangeOnTap==true: whenever item is pushed, Selected is toggled  (eg: SideMenu headers eg "Digital pins")

            if (AutoChangeOnTap)
            {
                Selected = !Selected && (!subMenuActive);
            }
            else
            {
                if (Selected)
                    SubMenuActive = !SubMenuActive;
                else
                    Selected = true; //which automatically toggles Active
            }

            if (this.action != null)
            {
                this.action.Call(this, argument);
            }
        }

        internal override void HandleGesture(GestureSample gesture)
        {
            if (ShouldHandleGesture(gesture) && !ScopeApp.GestureMustBeReleased)
                this.HandleGestureInternal(gesture);
        }


        override protected void HandleGestureInternal(GestureSample gesture)
        {
            switch (gesture.GestureType)
            {
				case GestureType.DragComplete:
					if(Boundaries.Contains(Utils.VectorToPoint(gesture.Position)))
						DoTap(new Point((int)gesture.Position.X, (int)gesture.Position.Y));
					ReleaseGestureControl();
					break;
                case GestureType.DoubleTap:
                case GestureType.Tap:
                    DoTap(new Point((int)gesture.Position.X, (int)gesture.Position.Y));
                    ReleaseGestureControl();
                    break;
                case GestureType.Hold:
				case GestureType.FreeDrag:
                    //ReleaseGestureControl must not be called here! Or DragComplete will not cause a Tap
                    menuPanel.Scroll((int)gesture.Delta.Y, true);
                    break;
                case GestureType.MouseScroll:                    
                    if (gesture.Delta == new Vector2(0, 1)) //mouseScroll UP 
                        menuPanel.Scroll(this.Boundaries.Height, false);
                    else if (gesture.Delta == new Vector2(0, -1)) //mouseScroll DOWN
                        menuPanel.Scroll(-this.Boundaries.Height, false);
                    ReleaseGestureControl();
                    break;
                default:
                    ReleaseGestureControl();
                    break;
            }
        }

        protected override void UpdateInternal(GameTime now)
        {
        }

        //method which basically sets the level of this menu, allowing to add itself to the correct MenuPanel
        //also calculates the max depth of the menu
        //and how many items there can be on one panel. This is calculated as ChildMenuItems.Count + nrItemsOfLargestHarmonicaChild
        internal void Init(MenuPanel menuPanel, int menuLevel, MenuItem parentMenuItem, bool left_nRight_aligned, ref int maxDepthOfEntirePanel, ref int maxItemsForAnyChildPanel)
        {
            int highestLevel = menuLevel;        

            this.left_nRight_aligned = left_nRight_aligned;
            this.ParentMenuItem = parentMenuItem;
            this.MenuLevel = menuLevel;
            this.menuPanel = menuPanel;

            if (ChildMenuItems != null)
            {
                foreach (MenuItem child in ChildMenuItems)
                {
                    int childLevel = menuLevel + 1;
                    if (ExpansionMode == ExpansionMode.Harmonica || ExpansionMode == ExpansionMode.Carousel)
                        childLevel = menuLevel;

                    if (child.ExpansionMode == ExpansionMode.Harmonica || child.ExpansionMode == ExpansionMode.Carousel)
                        if (child.ChildMenuItems != null)
                            if (child.ChildMenuItems.Count > largestHarmonicaChild)
                                largestHarmonicaChild = child.ChildMenuItems.Count;

                    int maxItemsForChildsChildPanels = 0;
                    child.Init(menuPanel, childLevel, this, left_nRight_aligned, ref maxDepthOfEntirePanel, ref maxItemsForChildsChildPanels);
                    highestLevel = (int)Math.Max(highestLevel, maxDepthOfEntirePanel);
                    maxItemsForAnyChildPanel = (int)Math.Max(maxItemsForAnyChildPanel, maxItemsForChildsChildPanels);
                }
                MaxItemsOnChildPanel = ChildMenuItems.Count + largestHarmonicaChild;
                maxItemsForAnyChildPanel = (int)Math.Max(maxItemsForAnyChildPanel, MaxItemsOnChildPanel);
            }
            else
            {
                maxItemsForAnyChildPanel = 1;
            }

            maxDepthOfEntirePanel = highestLevel;
        }

        //adds all MenuItems which should be drawn on this item's panel. typically this item itself, and any Harmonica children. each with a bool indicating whether it should be shown or hidden (for harmonica items)
        internal Dictionary<MenuItem, bool> ParentalItems(Dictionary<MenuItem, bool> menuPanelItems)
        {
            menuPanelItems.Add(this, true);
            if (ExpansionMode == ExpansionMode.Harmonica)
                if (ChildMenuItems != null)
                    foreach (var harmonicaChild in ChildMenuItems)
                        menuPanelItems.Add(harmonicaChild, subMenuActive); //if the item is active: immediately show the submenu

            return menuPanelItems;
        }

        //this method gets called from a child MenuItem, which is part of a radiogroup, when it gets selected. This method informs all other members of the radiogroup that they're deactived.
        internal void RadioGroupSelect(MenuItem selectedMember)
        {
            if (!selectedMember.RadioGroupID.HasValue)
                return;

            foreach (var item in ChildMenuItems)
                if (item != selectedMember)
                    if (item.RadioGroupID.HasValue)
                        if (item.RadioGroupID.Value == selectedMember.RadioGroupID.Value)
                            item.Selected = false;
        }

        internal bool CollapseAllSideMenus()
        {
            bool wasOpen = false;
            foreach (var child in ChildMenuItems)
            {
                if (child.ExpansionMode == ExpansionMode.Sidemenu)
                {
                    wasOpen |= child.Selected;
                    child.Selected = false;
                }
                else if (child.ExpansionMode == ExpansionMode.Harmonica)
                {
                    wasOpen |= child.CollapseAllSideMenus();
                }
            }
            return wasOpen;
        }
    }
}
