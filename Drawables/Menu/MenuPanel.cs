using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ESuite.Drawables
{
    internal class MenuPanel : SplitPanel
    {
        private SplitPanel[] verSplitPanels;
        private int menuLevels;
        private Dictionary<int, Dictionary<MenuItem, int>> harmonicaMapper;
        private Dictionary<int, Dictionary<MenuItem, int>> carouselMapper;
        internal int MaxItemsOnAnyPanel = 0;
        private Color bgColor;
        private bool left_nRight_align;

        //THIS IS a horizontal panel, which CONTAINS vertical panels to store all MenuItems. One vertical panel for each Menu level!
        //These vertical panels also contain 1 vertical panel for each embedded harmonica submenu. harmonicaMapper keeps track of which sub-verticalpanel belongs to which header
        internal MenuPanel(bool left_nRight_align) : base(Orientation.Horizontal, 0, SizeType.Inches)
        {
            //main panel must be drawn on top of subpanels
            this.DrawOrder = Direction.Backward;
            this.left_nRight_align = left_nRight_align;
        }
        
        override internal void RedefineNumberOfPanels(int menuLevels)
        {
            base.RedefineNumberOfPanels(menuLevels+1); //1 additional; to fill the space

            this.menuLevels = menuLevels;

            this.verSplitPanels = new Drawables.SplitPanel[menuLevels];
            this.harmonicaMapper = new Dictionary<int, Dictionary<MenuItem, int>>();
            this.carouselMapper = new Dictionary<int, Dictionary<MenuItem, int>>();

            for (int i = 0; i < menuLevels; i++)
            {
                harmonicaMapper.Add(i, new Dictionary<MenuItem, int>());
                carouselMapper.Add(i, new Dictionary<MenuItem, int>());
                this.verSplitPanels[i] = new Drawables.SplitPanel(Orientation.Vertical, 0, SizeType.Inches);
                this.SetPanel(i, verSplitPanels[i]);
            }

            //set Panel0 size, which is never changed after this
            this.SetPanelSize(0, Scaler.SideMenuWidth);

            //at this moment all newly created panels have 0 width and height.
            //calling OnBoundariesChangedInternal of this object, which is a SplitPanel, causes all panels to receive their correct size; allowing the calling code to immediately perform correct calculations.
            base.OnBoundariesChangedInternal();
        }

        //this class must be provided with Boundaries which cover the level0 rectangle. From this rectangle it uses only the Right coordinate.
        protected override void OnBoundariesChangedInternal()
        {
            //for bool left_nRight_align: the 0th level menu has to start from Scaler.SideMenuWidth left of the screen
            Rectangle newBoundaries = Boundaries;
            if(left_nRight_align)
                newBoundaries.X = newBoundaries.Right - Scaler.InchesToPixels(Scaler.SideMenuWidth);
            newBoundaries.Width = Scaler.InchesToPixels(Scaler.SideMenuWidth) * menuLevels;

            //now this is something dirty. We cannot simply override Boundaries, because otherwise each call of this method the previous line would shift Boundaries. So we temporarily override Boundaries, call the SplitPanel magic, and set the original Boundaries back.
            Rectangle origBoundaries = Boundaries;
            this.Boundaries = newBoundaries;
            base.OnBoundariesChangedInternal();
            this.Boundaries = origBoundaries;

            this.bgColor = MappedColor.MenuSolidBackgroundVoid.C();
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            spriteBatch.Draw(whiteTexture, Boundaries, bgColor);
            spriteBatch.End();
        }

        //level -1 means out of Boundaries
        internal void ShowLevel(int level, bool animated)
        {
            for (int i = 1; i < menuLevels; i++) //Panel0 keeps its size
            {
                float targetSize = i - 1 < level ? Scaler.SideMenuWidth : 0;
                this.SetPanelSize(i, targetSize, animated ? ColorMapper.AnimationTime : 0);
            }
        }

        //level0 is main menu
        internal void Repopulate(int level, int verticalOffsetInPixels, List<MenuItem> mainItems)
        {
            harmonicaMapper[level] = new Dictionary<Drawables.MenuItem, int>();
            carouselMapper[level] = new Dictionary<Drawables.MenuItem, int>();

            int entriesToCreate = mainItems.Count + 1 + 1; //adding a first element to position the submenu vertically, and a last element to fill the bottom par;
            foreach (var item in mainItems)
                if (item.ExpansionMode == ExpansionMode.Harmonica)
                    entriesToCreate++;  //for each harmonica header, we will need to add an additional Stack

            //create brand new vertical SplitPanel
            this.verSplitPanels[level].RedefineNumberOfPanels(entriesToCreate);// = new SplitPanel(Orientation.Vertical, itemCount + 1, SizeType.Inches); //+1: adding a last element to fill the bottom part            

            //make sure to clean the last item, as we need this as filler but it might still contain an item from a previous (larger) menu
            this.verSplitPanels[level].SetPanel(entriesToCreate - 1, null);

            //make sure sidemenu will not be positioned too low
            int sideMenuHeightInPixels = mainItems.Count * mainItems[0].Height;
            if (mainItems[0].ParentMenuItem != null)
                sideMenuHeightInPixels += mainItems[0].ParentMenuItem.LargestHarmonicaChild * mainItems[0].Height;
            if (verSplitPanels[level].Boundaries.Height < verticalOffsetInPixels + sideMenuHeightInPixels)
                verticalOffsetInPixels = verSplitPanels[level].Boundaries.Height - sideMenuHeightInPixels;

            //in case there are too many items to fit on one page: make sure the top item stays at the top of the panel
            verticalOffsetInPixels = (int)Math.Max(verticalOffsetInPixels, 0);

            //in case the new panel was hidden: set vertical offset immediately. Otherwise animate it.
            verSplitPanels[level].SetPanelSize(0, Scaler.PixelsToInches(verticalOffsetInPixels), verSplitPanels[level].Boundaries.Width == 0 ? 0 : ColorMapper.AnimationTime);

            //then populate the newly created vertical panel
            int indexer = 1; //start at 1, because item0 is just for vertical spacing            
            foreach (MenuItem thisItem in mainItems)
            {
                float menuItemHeight = Scaler.PixelsToInches(thisItem.Height);
                float menuItemWidth = Scaler.PixelsToInches(thisItem.Width);

                //add mainItem
                verSplitPanels[level].SetPanel(indexer, thisItem);
                verSplitPanels[level].SetPanelSize(indexer++, menuItemHeight);
                
                if (thisItem.ExpansionMode == ExpansionMode.Harmonica)
                {
                    //in case of harmonica header: add SplitPanel to contain all harmonica subitems
                    MenuItem harmonicaHeader = thisItem;
                    int n = harmonicaHeader.ChildMenuItems == null ? 0 : harmonicaHeader.ChildMenuItems.Count;
                    //add all subitems to new splitpanel
                    SplitPanel harmonicaPanel = new SplitPanel(Orientation.Vertical, n, SizeType.Inches);
                    for (int i = 0; i < n; i++)
                    {
                        harmonicaPanel.SetPanel(i, thisItem.ChildMenuItems[i]);
                        harmonicaPanel.SetPanelSize(i, menuItemHeight);
                    }

                    //add new splitpanel to main splitpanel
                    verSplitPanels[level].SetPanel(indexer, harmonicaPanel);
                    verSplitPanels[level].SetPanelSize(indexer++, harmonicaHeader.SubMenuActive ? Scaler.PixelsToInches(harmonicaHeader.Height) * n : 0);

                    //add to dictionary, allowing faster lookup later
                    harmonicaMapper[level].Add(thisItem, indexer - 1);
                }
                else if (thisItem.ExpansionMode == ExpansionMode.Carousel)
                {
                    //in case of carousel header: add horizontal SplitPanel with 2 vertical panels
                    MenuItem carouselHeader = thisItem;

                    SplitPanel carouselPanel = new SplitPanel(Orientation.Horizontal, 2, SizeType.Inches);
                    verSplitPanels[level].SetPanel(indexer, carouselPanel);
                    verSplitPanels[level].SetPanelSize(indexer++, 2.0f*Scaler.PixelsToInches(carouselHeader.Height));

                    //add 2 vertical panels to horizontal carousel Panel
                    carouselPanel.SetPanel(0, new SplitPanel(Orientation.Vertical, 0, SizeType.Inches));
                    carouselPanel.SetPanel(1, new SplitPanel(Orientation.Vertical, 0, SizeType.Inches));

                    //add to dictionary, allowing faster lookup later
                    carouselMapper[level].Add(thisItem, indexer - 1);
                }
            }
        }

        internal void ShowHarmonicaItems(int level, MenuItem harmonicaHeader, bool animate)
        {
            float totalHeightOfHarmonicaMenu = (float)harmonicaHeader.ChildMenuItems.Count * Scaler.PixelsToInches(harmonicaHeader.Height);
            verSplitPanels[level].SetPanelSize(harmonicaMapper[level][harmonicaHeader], totalHeightOfHarmonicaMenu, animate ? ColorMapper.AnimationTime : 0);
        }

        internal void HideHarmonicaItems(int level, MenuItem harmonicaHeader, bool animate)
        {
            verSplitPanels[level].SetPanelSize(harmonicaMapper[level][harmonicaHeader], 0, animate ? ColorMapper.AnimationTime : 0);
        }

        internal void SlideCarousel(int level, MenuItem carouselHeader, MenuItem carouselElement, bool toRight, bool animate)
        {
            float menuItemWidth = Scaler.PixelsToInches(Scaler.MenuItemSize.X);
            float menuItemHeight = Scaler.PixelsToInches(Scaler.MenuItemSize.Y);
            int verSplitPanelID = carouselMapper[level][carouselHeader];
            SplitPanel carouselHorPanel = verSplitPanels[level].Panels[verSplitPanelID] as SplitPanel;

            //now populate 1 of the 2 vertical panels of the carousel
            //in case of toRight: set size to [0, all], move old items to shown right panel, put new items on hidden left panel, and transition to [all, 0]
            //in case of !toRight: set size to [all, 0], move old items to shown left panel, put new items on hidden right panel, and transition to [0, all]
            int panelIdOfOrigItems = carouselHorPanel.TargetSizes[0]<menuItemWidth/2 ? 1 : 0; //if panel 0 was hidden, the original items are on panel 1
            int targetPanelIdForOldItems = toRight ? 1 : 0;
            int targetPanelIdForNewItems = toRight ? 0 : 1;

            //set sizes (immediate)
            carouselHorPanel.SetPanelSize(0, toRight ? 0 : menuItemWidth);
            carouselHorPanel.SetPanelSize(1, toRight ? menuItemWidth : 0);

            //move old items
            if (panelIdOfOrigItems != targetPanelIdForOldItems)
            {
                SplitPanel panelWithOrigItems = carouselHorPanel.Panels[panelIdOfOrigItems] as SplitPanel;
                SplitPanel targetPanelForOldtems = carouselHorPanel.Panels[targetPanelIdForOldItems] as SplitPanel;
                int itemsToTransfer = panelWithOrigItems.Panels.Length;
                targetPanelForOldtems.RedefineNumberOfPanels(itemsToTransfer);
                for (int i = 0; i < itemsToTransfer; i++)
                {
                    targetPanelForOldtems.SetPanel(i, panelWithOrigItems.Panels[i]);
                    targetPanelForOldtems.SetPanelSize(i, menuItemHeight);
                }
                panelWithOrigItems.RedefineNumberOfPanels(0);
                panelWithOrigItems.RedefineNumberOfPanels(1);
            }

            //put new items on correct panel
            int nrNewElements = carouselElement.ChildMenuItems.Count;
            SplitPanel verticalPanelForNewItems = carouselHorPanel.Panels[targetPanelIdForNewItems] as SplitPanel;
            verticalPanelForNewItems.RedefineNumberOfPanels(nrNewElements + 1); //extra one to fill the space at the bottom! (will be an instance of Empty)
            for (int i = 0; i < nrNewElements; i++)
            {
                verticalPanelForNewItems.SetPanel(i, carouselElement.ChildMenuItems[i]);
                verticalPanelForNewItems.SetPanelSize(i, Scaler.PixelsToInches(Scaler.MenuItemSize.Y));
            }

            //initiate transition
            carouselHorPanel.SetPanelSize(toRight? 0:1, menuItemWidth , ColorMapper.AnimationTime);
            carouselHorPanel.SetPanelSize(toRight ? 1 : 0,  0, ColorMapper.AnimationTime);
            verSplitPanels[level].SetPanelSize(verSplitPanelID, (float)nrNewElements * Scaler.PixelsToInches(Scaler.MenuItemSize.Y));
        }

        internal void Destroy()
        {
            ClearAllChildren();
        }

        internal void Scroll(int offsetInPixels, bool immediate)
        {
            //calc new target size
            float newTargetSize = verSplitPanels[0].TargetSizes[0] + Scaler.PixelsToInches(offsetInPixels);            

            //prevent scrolling up too much
            float totalSize = verSplitPanels[0].TargetSizes.Sum() - verSplitPanels[0].TargetSizes[0] + newTargetSize - Scaler.PixelsToInches(Boundaries.Height);
            if (totalSize < 0) newTargetSize -= totalSize;

            //prevent scrolling down too much
            if (newTargetSize > 0) newTargetSize = 0;

            //commit
            verSplitPanels[0].SetPanelSize(0, newTargetSize, immediate ? 0 : ColorMapper.AnimationTime);
        }
    }
}
