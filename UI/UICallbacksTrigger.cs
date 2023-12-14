using LabNation.DeviceInterface.Devices;
using ESuite.Drawables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DropNet.Models;
using DropNet.Exceptions;

namespace ESuite
{
    static partial class UICallbacks
    {
        public static void TriggerLevelIndicatorClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Indicator ind = (Indicator)sender;
            uiHandler.TriggerLevelIndicatorClicked(ind);
        }
        public static void TriggerHoldoffIndicatorClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Indicator ind = (Indicator)sender;
            uiHandler.ToggleTriggerContextMenu(ind);
        }

        public static void TriggerIndicatorDropped(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Indicator ind = (Indicator)sender;
            uiHandler.TriggerIndicatorDropped(ind);
        }
        public static void HorTriggerDoubleClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Indicator ind = (Indicator)sender;

            //close context menu if shown
            if (ind.contextMenuShown)
                uiHandler.ToggleTriggerContextMenu(ind);

            //set time offset to 0
            uiHandler.ZeroTriggerHoldoff();
        }

        public static void VerTriggerDoubleClicked(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            Indicator ind = (Indicator)sender;

            //close context menu if shown
            if (ind.contextMenuShown)
                uiHandler.ToggleTriggerContextMenu(ind);

            //set time offset to 0
            uiHandler.SetTriggerAnalogLevelRelative(0);
        }
        
        public static void ChangeTriggerLevel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.SetTriggerAnalogLevelRelative((float)arg);
        }
        public static void ChangeTriggerHoldoffRelativeToViewport(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.SetTriggerHoldoffRelativeToViewport((float)arg);
        }

        public static void ChangeTriggerHoldoffRelativeToAcquisitionBuffer(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            uiHandler.ChangeTriggerHoldoffRelativeToAcquisitionBuffer((float)arg);
        }

        public static void ResetTriggerVertically(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();

            uiHandler.SetTriggerAnalogLevel(0f);
            if (arg is DrawableCallback)
                ((DrawableCallback)arg).Call(sender);
        }
        public static void ResetTriggerHorizontally(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();

            uiHandler.ZeroTriggerHoldoff();
            if (arg is DrawableCallback)
                ((DrawableCallback)arg).Call(sender);
        }

        public static void SetTrigger(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.CloseMenusOnGraphArea();
            object[] args = (object[])arg;

            TriggerValue trigger = (TriggerValue)args[0];
            uiHandler.SetTrigger(trigger);
            if (args.Length == 2)
            {
                ((DrawableCallback)args[1]).Call(sender);
            }
        }

        public static void SetTriggerPulseWidthMin(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.SetTriggerPulseWidthMin((double)arg);
        }
        public static void SetTriggerPulseWidthMax(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.SetTriggerPulseWidthMax((double)arg);
        }

        public static void ShowMenuTrigger(EDrawable sender, object arg)
        {
            uiHandler.ShowTriggerContextMenu((Indicator)arg);
        }

        public static void ForceTrigger(EDrawable sender, object arg)
        {
            uiHandler.ForceTrigger();
        }

        public static void DigitalTriggerIndicatorTapped(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            DigitalChannel ch = (DigitalChannel)(((object[])arg)[0]);
            uiHandler.DigitalTriggerIndicatorTapped(ch);
        }

        public static void MoveTriggerLevel(EDrawable sender, object arg)
        {
            if (!uiHandler.InteractionAllowed) return;
            uiHandler.MoveTriggerAnalogLevelRelative((float)arg);
        }
    }
}
