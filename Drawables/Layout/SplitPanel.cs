using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.Common;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using ESuite.Measurements;

namespace ESuite.Drawables
{
    internal delegate void OnUpdateCompleteDelegate();
    internal enum SizeType { Inches, Relative }
    internal class SplitPanel:EDrawable
    {
        public Orientation orientation { get; private set; }
        public Direction direction = Direction.Forward;
        public float[] TargetSizes;
        internal EDrawable[] Panels { get; private set; }
        public OnUpdateCompleteDelegate OnUpdateComplete;
        protected float?[] panelSizes;
        public float?[] PanelSizes 
        { 
            get { return panelSizes; }
            set { panelSizes = value; /*OnMatrixChangedInternal();*/ }
        }
        private int?[] ComputedSizeInPixels;
        protected LerpFloat[] PanelSizeAnimations;
        protected SizeType sizeType;
        bool shouldUpdate = false;

        public SplitPanel(Orientation orientation, int panels, SizeType sizeType)
            : base()
        {
            this.sizeType = sizeType;
            this.orientation = orientation;
            this.Panels = new Drawables.EDrawable[panels];
            this.PanelSizes = new float?[panels]; //this can contain either absolute or relative values; depending on the SizeType
            this.TargetSizes = new float[panels];

            if (sizeType == SizeType.Relative)
                for (int i = 0; i < panels; i++)
                    panelSizes[i] = 0;

            this.PanelSizeAnimations = new LerpFloat[panels];

            LoadContent();
            this.MustRecalcNextUpdateCycle = true;
        }

        internal virtual void RedefineNumberOfPanels(int nrNewPanels)
        {
            EDrawable[] oldPanels = Panels; 

            int previousNrPanels = Panels.Length;
            ClearAllChildren();

            Drawables.EDrawable[] newPanels = new Drawables.EDrawable[nrNewPanels]; 
            float?[] newPanelSizes = new float?[nrNewPanels];
            LerpFloat[] newPanelSizeAnimations = new LerpFloat[nrNewPanels];
            float[] newTargets = new float[nrNewPanels];
            for (int i = 0; i < newPanelSizes.Length && i < Panels.Length; i++)
            {
                newPanelSizes[i] = PanelSizes[i];
                newPanelSizeAnimations[i] = PanelSizeAnimations[i];
                newTargets[i] = TargetSizes[i];
                newPanels[i] = Panels[i];
            }

            this.Panels = newPanels;
            this.PanelSizes = newPanelSizes;
            this.PanelSizeAnimations = newPanelSizeAnimations;
            this.TargetSizes = newTargets;

            for (int i = previousNrPanels; i < nrNewPanels; i++)
            {
                SetPanel(i, new Empty());          //dummy
                if (i >= previousNrPanels)
                    SetPanelSize(i, 0);
            }

            //easiest, and perhaps cleanest way to make sure there's a perfect correlation between number of panels and children
            ClearAllChildren();
            foreach (var item in Panels)
                if (item != null)
                    AddChild(item);

            shouldUpdate = true;
        }

        internal void RemovePanel(int panelNumber)
        {
            if (panelNumber > Panels.Length-1)
                return;

            //remove all children
            ClearAllChildren();

            //move all following panels
            for (int i = panelNumber; i < Panels.Length-1; i++)
            {
                PanelSizes[i] = PanelSizes[i+1];
                PanelSizeAnimations[i] = PanelSizeAnimations[i+1];
                TargetSizes[i] = TargetSizes[i+1];
                Panels[i] = Panels[i+1];
                AddChild(Panels[i]);
            }

            RedefineNumberOfPanels(Panels.Length - 1);

            shouldUpdate = true;
        }

        //returns the Ycoord, in pixels, of the requested panel when all animimations will have finished
        public int TargetYCoord(EDrawable panel)
        {
            if (!children.Contains(panel))
                throw new Exception("TargetYCoord index out of bounds");

            int yCoord = Boundaries.Y;
            for (int i = 0; i < Panels.Length; i++)
            {
                if (Panels[i] != panel)
                    yCoord += Scaler.InchesToPixels(TargetSizes[i]);
                else
                    break;
            }
            return yCoord;
        }

        public float? TransitionProgress(int panelID)
        {
            if (PanelSizeAnimations.Length - 1 < panelID) return null;
            if (PanelSizeAnimations[panelID] == null) return null;
            return PanelSizeAnimations[panelID].Progress;
        }

        public void SetPanel(int panel, EDrawable content)
        {
            //if panel was already used: discard child
            if (Panels[panel] != null)
                RemoveChild(Panels[panel]);

            if (content != null) //might be null in case calling code wants to clear that panel (so it can be used as spacer)
            {
                //if panel was already filled: de-child panel so it is no longer drawn/updated
                if (Panels[panel] != null)
                    RemoveChild(Panels[panel]);

                Panels[panel] = content;
                AddChild(content);
            }

            shouldUpdate = true;
        }
        public void SetPanelSize(int panel, float size, float animationTime = 0f)
        {
            if (this.sizeType == SizeType.Relative)
                if (size < 0 || size > 1)
                    throw new Exception("Error: SplitPanel: panel " + panel.ToString() + " set to size " + size.ToString());

            if (float.IsNaN(size))
                throw new Exception("Error: SplitPanel: panel " + panel.ToString() + " set to size NaN");

            if (panel > Panels.Length - 1)
                throw new Exception("Panel beyond number of panels");

            TargetSizes[panel] = size;

            if (animationTime == 0f)
            {
                this.panelSizes[panel] = size;
                PanelSizeAnimations[panel] = null; //terminate any animation if there was any
                //OnMatrixChangedInternal(); don't do this here. simply wait for the next Update cycle
                shouldUpdate = true;
            }
            else
            {
                if (!PanelSizes[panel].HasValue)
                    throw new Exception("Can't animate this panel since it's of an unspecified size");

                if (PanelSizeAnimations[panel] != null)
                {
                    PanelSizeAnimations[panel] = new LerpFloat(PanelSizeAnimations[panel].CurrentValue, size, animationTime);
                }
                else
                {
                    PanelSizeAnimations[panel] = new LerpFloat(PanelSizes[panel].Value, size, animationTime);
                }
            }
        }

        protected override void LoadContentInternal() { }

        protected override void UpdateInternal(GameTime now) 
        {
            foreach (LerpFloat f in PanelSizeAnimations)
            {
                if (f == null) continue;
                f.Update(now);
                shouldUpdate = true;
            }
            if(shouldUpdate)
                OnBoundariesChanged();
        }

        protected override void DrawInternal(GameTime time) {}

        protected override void OnBoundariesChangedInternal() 
        {
            if (Panels.Length == 0) return;

            this.shouldUpdate = false;
            int pixelsToAllocate = orientation == Orientation.Horizontal ? Boundaries.Width : Boundaries.Height;
            float totalSizeAllocated = 0;
            int pixelsAllocated = 0;
            this.ComputedSizeInPixels = new int?[Panels.Length];

            // Set size for panels with specified height
            for (int i = 0; i < Panels.Length; i++)
            {
                int index = direction == Direction.Backward ? Panels.Length - i - 1: i;
                if (PanelSizes[index].HasValue)
                {
                    if (pixelsToAllocate < PanelSizes[index].Value)
                        ComputedSizeInPixels[index] = pixelsToAllocate;
                    else
                    {
                        if (index < PanelSizeAnimations.Length && PanelSizeAnimations[index] != null)
                        {
                            //we just need to convert PanelSize to pixels, but keep track of total so any rounding error is solved eventually
                            ComputedSizeInPixels[index] = ComputeSizeInPixels(totalSizeAllocated + PanelSizeAnimations[index].CurrentValue) - pixelsAllocated;
                            totalSizeAllocated += PanelSizeAnimations[index].CurrentValue;
                        }
                        else
                        {
                            //we just need to convert PanelSize to pixels, but keep track of total so any rounding error is solved eventually
                            ComputedSizeInPixels[index] = ComputeSizeInPixels(totalSizeAllocated + PanelSizes[index].Value) - pixelsAllocated;
                            totalSizeAllocated += PanelSizes[index].Value;
                        }
                    }

                    pixelsAllocated += ComputedSizeInPixels[index].Value;
                    pixelsToAllocate -= ComputedSizeInPixels[index].Value;
                }
            }
            // Divide leftover space
            List<int> nullIndices = ComputedSizeInPixels.Select((val, i) => new { val = val, i = i }).Where(x => !x.val.HasValue).Select(x => x.i).ToList();
            
            //First try to use the EDrawable.Size() to fill the void
            for (int i = 0; i < Panels.Length; i++)
            {
                if (Panels[i] == null) continue;

                int index = direction == Direction.Backward ? Panels.Length - i - 1 : i;
                if (nullIndices.Contains(index) && Panels[index].Size.HasValue)
                {
                    if (orientation == Orientation.Horizontal)
                        ComputedSizeInPixels[index] = Math.Min(pixelsToAllocate, Panels[index].Size.Value.X);
                    else
                        ComputedSizeInPixels[index] = Math.Min(pixelsToAllocate, Panels[index].Size.Value.Y);
                    pixelsToAllocate -= ComputedSizeInPixels[index].Value;
                    nullIndices.Remove(index);
                }
            }

            //Finally just divide whatever space is left
            if (direction == Direction.Backward)
                nullIndices.Reverse();
            while(nullIndices.Count > 0)
            {
                int leftoverSpacePerPanel = Math.Max(0, pixelsToAllocate / nullIndices.Count);
                int index = nullIndices.First();
                if (Panels.Length > index && Panels[index] != null && Panels[index].Size.HasValue)
                {
                    
                } else
                    ComputedSizeInPixels[index] = leftoverSpacePerPanel;
                pixelsToAllocate -= ComputedSizeInPixels[index].Value;
                nullIndices.Remove(index);
            }
            if (pixelsToAllocate > 0)
                ComputedSizeInPixels[Panels.Length - 1] += pixelsToAllocate;

            int offset = 0;
            for (int i = 0; i < Panels.Length; i++)
            {
                if (Panels[i] != null)
                {
                    Rectangle r;
                    if(orientation == Orientation.Horizontal)
                        r = new Rectangle(Boundaries.X + offset, Boundaries.Y, ComputedSizeInPixels[i].Value, Boundaries.Height);
                    else //Vertical
                        r = new Rectangle(Boundaries.X, Boundaries.Y + offset, Boundaries.Width, ComputedSizeInPixels[i].Value);                    
                    Panels[i].SetBoundaries(r);
                    Panels[i].Visible = ComputedSizeInPixels[i] > 0;
                }
                offset += ComputedSizeInPixels[i].Value;
            }

            //detect finished animations and delete them (otherwise this method gets being called!)
            for (int i = 0; i < PanelSizeAnimations.Length; i++)
                if (PanelSizeAnimations[i] != null)
                    if (PanelSizeAnimations[i].done)
                    {
                        panelSizes[i] = PanelSizeAnimations[i].CurrentValue; //need to store this value, as next call to SetPanelSize needs to know this value!
                        PanelSizeAnimations[i] = null;
                    }

            //event allowing other elements to be called only once, and not after the OnBoundaryChanged of all of the elements
            if (OnUpdateComplete != null)
                OnUpdateComplete();
        }

        private int ComputeSizeInPixels(float size)
        {
            if (this.sizeType == SizeType.Inches)
                return Scaler.InchesToPixels(size);
            else
            {
                int pixelsToAllocate = orientation == Orientation.Horizontal ? Boundaries.Width : Boundaries.Height;
                return (int)((float)pixelsToAllocate * size);
            }
        }

        public bool panelContentsTooLarge { get 
            { 
                for(int i = 0; i < Panels.Length; i++)
                {
                    var panel = Panels[i];
                    if(panel.Size.HasValue)
                    {
                        if(
                            (orientation == Orientation.Horizontal && panel.Size.Value.X > ComputedSizeInPixels[i]) //horiztonally too big 
                            || 
                            (orientation == Orientation.Vertical && panel.Size.Value.Y > ComputedSizeInPixels[i]) //horiztonally too big
                            )
                            return true;
                    }
                }
                return false;
            } 
        }
    }
}
