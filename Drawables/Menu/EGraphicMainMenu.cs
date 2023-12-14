using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using ECore.Devices;
using ECore.DataPackages;

namespace ESuite
{
    public class EGraphicMainMenu:EGraphicSubMenu
    {
        //private EGraphicMenuButton menuButton;
        private EMainEngine mainEngine;
        private EGraphicSubMenu subAddHorizontalCursor;
        private EGraphicSubMenu subAddVerticalCursor;

        public EGraphicMainMenu(EDrawable parent, EXNAController xnaController, EGraphicSubMenu parentMenuItem)
            : base(parent, xnaController, "MainMenu", parentMenuItem)
        {
            EGraphicSubMenu math = new EGraphicSubMenu(this, xnaController, "Math", this);
            math.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Ch A + Ch B", SetMathSum, math));
            math.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Ch A - Ch B", SetMathSubtract, math));
            math.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Ch A * Ch B", SetMathMultiply, math));
            math.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Ch A / Ch B", SetMathDivide, math));
            AddMenuItem(math);

            EGraphicSubMenu sub2 = new EGraphicSubMenu(this, xnaController, "Actions", this);
                EGraphicSubMenu subAddAction = new EGraphicSubMenu(this, xnaController, "Add action", sub2);
                    EGraphicSubMenu subAddActionSaveToDisk = new EGraphicSubMenu(this, xnaController, "Save to file on disk", subAddAction);
                        subAddActionSaveToDisk.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Save to Excel file", null, subAddActionSaveToDisk));
                        subAddActionSaveToDisk.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Save to Matlab file", null, subAddActionSaveToDisk));
                    subAddAction.AddMenuItem(subAddActionSaveToDisk);
			subAddAction.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Flash FPGA FW", ButtonActionFlashFPGAFW, subAddAction));
                    subAddAction.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Auto-configure", null, subAddAction));
                    subAddAction.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Auto-space waveforms", ButtonActionAutoArrangeWaveforms, subAddAction));
                sub2.AddMenuItem(subAddAction);
                EGraphicSubMenu subRemoveAction = new EGraphicSubMenu(this, xnaController, "Remove action", sub2);
                    subRemoveAction.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Save to Excel file", null, subRemoveAction));
                    subRemoveAction.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Save to Matlab file", null, subRemoveAction));
                sub2.AddMenuItem(subRemoveAction);
            AddMenuItem(sub2);

            EGraphicSubMenu subMeasurements = new EGraphicSubMenu(this, xnaController, "Measurements", this);
            AddMenuItem(subMeasurements);

            EGraphicSubMenu subWaveforms = new EGraphicSubMenu(this, xnaController, "Waveforms", this);
                EGraphicSubMenu subWaveformsAdd = new EGraphicSubMenu(this, xnaController, "Add waveforms", subWaveforms);
                    subWaveformsAdd.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Analog channel A", ButtonActionAddChannelA, subWaveformsAdd));
                    subWaveformsAdd.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Analog channel B", ButtonActionAddChannelB, subWaveformsAdd));
                    subWaveformsAdd.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Digital channels 0-7", ButtonActionAddAllDigitalChannels, subWaveformsAdd));
                    subWaveformsAdd.AddMenuItem(new EGraphicMenuButton(this, xnaController, "Math", ButtonActionAddMath, subWaveformsAdd));
                    subWaveformsAdd.AddMenuItem(new EGraphicMenuButton(this, xnaController, "I2C", ButtonActionAddI2C, subWaveformsAdd));
                subWaveforms.AddMenuItem(subWaveformsAdd);
                EGraphicSubMenu subAddCursor = new EGraphicSubMenu(this, xnaController, "Add cursor", subWaveforms);
                    subAddHorizontalCursor = new EGraphicSubMenu(this, xnaController, "Add horizontal cursor", subAddCursor);
                    subAddCursor.AddMenuItem(subAddHorizontalCursor);
                    subAddVerticalCursor = new EGraphicSubMenu(this, xnaController, "Add vertical cursor", subAddCursor);
                    subAddCursor.AddMenuItem(subAddVerticalCursor);                    
                subWaveforms.AddMenuItem(subAddCursor);
            AddMenuItem(subWaveforms);

            EGraphicSubMenu subTriggering = new EGraphicSubMenu(this, xnaController, "Triggering", this);
            AddMenuItem(subTriggering);
        }

        //override because this is the top menu
        public override void Activate(float delaySeconds)
        {
            if (EXNAController.MenuShown) return;

            EXNAController.MenuShown = true;
            this.ActivateChildren();
        }

        public void UpdateActiveWaves(Dictionary<ScopeChannel, EGraphicsWaveform> activeWaves)
        {
            //clean menus depending on this
            this.subAddHorizontalCursor.RemoveAllChildren();
            this.subAddVerticalCursor.RemoveAllChildren();

            foreach (KeyValuePair<ScopeChannel, EGraphicsWaveform> kvp in activeWaves)
            {
                //horizontal cursors: only analog waves matter
                if (kvp.Value is EGraphicsAnalogWave)
                {
                    Color color = kvp.Value.GraphColor;
                    ButtonDelegate del = new ButtonDelegate(kvp.Value.AddCursor);
                    EGraphicMenuButton newButton = new EGraphicMenuButton(this, xnaController, kvp.Key.Name, del, subAddHorizontalCursor);
                    //need to call this, so it is propagated towards new child menu buttons
                    newButton.SetParentWVP(device, this.WorldAbsolute, view, projection);
                    
                    if (content != null) newButton.LoadContent(this.content); //always need to load content separately when loading EDrawables dynamically
                    this.subAddHorizontalCursor.AddMenuItem(newButton);
                }
            }

            //need to call this, so it is propagated towards new child menu buttons
            //SetParentWVP(device, world, view, projection);
        }

        public override void Deactivate(float delaySeconds)
        {
            if (!EXNAController.MenuShown) return;

            EXNAController.MenuShown = false;
            this.DeactivateChildren();
        }

        protected override void LoadContentInternal(ContentManager Content)
        {
        }

        protected override void DrawInternal()
        {
        }

        protected override void ReactOnNewWVP()
        {
        }

        override protected void UpdateInternal(DateTime now, List<GestureSample> gestureList)
        {
        }

        public void HookMainEngine(EMainEngine mainEngine)
        {
            this.mainEngine = mainEngine;
        }

        private void ButtonActionAddAllDigitalChannels()
        {
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi0);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi1);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi2);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi3);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi4);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi5);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi6);
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Digi7);

            this.ButtonActionAutoArrangeWaveforms();
        }

        private void ButtonActionAddMath()
        {
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.Math);
        }
        
        private void ButtonActionAddI2C()
        {
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.I2c);
        }

        private void ButtonActionAddChannelA()
        {
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.ChA);
        }

        private void ButtonActionAddChannelB()
        {
            mainEngine.XNAController.UIHandler.GUI_ShowWaveform(ScopeChannels.ChB);
        }

        private void ButtonActionAutoArrangeWaveforms()
        {
            mainEngine.XNAController.UIHandler.GUI_AutoArrangeWaveforms();
        }

		private void ButtonActionFlashFPGAFW()
		{            
			if (mainEngine == null) return;
			ECore.Devices.ScopeV2 device = (ECore.Devices.ScopeV2)mainEngine.Scope;
			device.FlashHW ();
		}

        private void ButtonActionAddSaveToExcel()
        {
            //mainEngine.XNAController.MainGui.ButtonBar.DemoAddSaveToExcel();
        }

        /* MATH CHANNEL FUNCTIONS */
        private void SetMathSum() { MathChannelOperationSetterDelegate(new Func<float, float, float>((a, b) => (a + b))); }
        private void SetMathSubtract() { MathChannelOperationSetterDelegate(new Func<float, float, float>((a, b) => (a - b))); }
        private void SetMathMultiply() { MathChannelOperationSetterDelegate(new Func<float, float, float>((a, b) => (a * b))); }
        private void SetMathDivide() { MathChannelOperationSetterDelegate(new Func<float, float, float>((a, b) => (a / b))); }
        public delegate void MathChannelOperationSetter(Func<float, float, float> operation);
        public MathChannelOperationSetter MathChannelOperationSetterDelegate;
    }
}

