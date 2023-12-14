using System;
using System.Collections.Generic;
using System.Linq;

namespace ESuite
{
    public enum Scale { Tiny = 0, Small = 1, Normal = 2, Large = 3, Humongous = 4 }
    static internal partial class Scaler
    {
        public const string IMAGE_PATH = "Images";

        //define scaling factors for predefined GuiScales, in percentage
        public static Dictionary<Scale, double> guiScalers = new Dictionary<Scale, double>()
        {
            {Scale.Tiny, .50 },
            {Scale.Small, .75 },
            {Scale.Normal, 1.00 },
            {Scale.Large, 1.25 },
            {Scale.Humongous, 1.50 },
        };

        private static int[] fontSizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
        public enum PixelDensity
        {
            DPI_72 = 72,
            DPI_96 = 96,
            DPI_120 = 120,
            DPI_140 = 140,
            DPI_160 = 160,
            DPI_240 = 240,
            DPI_320 = 320,
        };
        public const float PRESCALE = 50f / 300f;
        public const PixelDensity BASE_DPI = PixelDensity.DPI_72;

        public static string ImageFileName(string file, PixelDensity dpi, Scale scale)
        {
            int promille = (int)Math.Round((double)dpi / (double)BASE_DPI * guiScalers[scale] * 1000.0);
            return (file.ToLower() + "-" + (int)promille + ".png").ToLower();
        }

        public static HashSet<string> imageNames = new HashSet<string>()
        {
            //ImageGroup.ButtonBar,
            "button-force-trigger-empty",
            "button-force-trigger-full",
            "menu-btn",
            "menu-labnation",
            "menu-ruler",
            "button-screenshot",
            "button-play",
            "button-stop",
            "button-record-empty",
            "button-record-full",
            "button-measurement-box-button-off",
            "button-measurement-box-button-on",
            "indicator-usb",
            "indicator-wifi",
            //ImageGroup.ScreenOverlayInformation,
            "cuecard",
            //ImageGroup.ContextMenu,
            "icon-a-divided-by-b",
            "icon-a-minus-b",
            "icon-a-plus-b",
            "icon-a-times-b",
            "icon-ac",
            "icon-b-divided-by-a",
            "icon-b-minus-a",
            "icon-channel-a",
            "icon-channel-b",
            "icon-dc",
            "icon-digital",
            "icon-edge",
            "icon-pulse-max",
            "icon-pulse-min",
            "icon-pulse",
            "icon-timeout",
            "icon-timeout-length",
            "icon-falling-trigger",
            "icon-falling",
            "icon-hide",
            "icon-remove",
            "icon-invert-on",
            "icon-invert-off",
            "icon-reset",
            "icon-rising-trigger",
            "icon-rising",
            "icon-any",
            "icon-any-trigger",
            "icon-show",
            "icon-save",
            "icon-x1-probe",
            "icon-x10-probe",
            "icon-x100-probe",
            //ImageGroup.Indicators,
            "widget-indicator-full",
            "widget-indicator-empty",
            "widget-indicator-hilite",
            "widget-cursor-full",
            "widget-cursor-hilite",
            "widget-cursor-empty",
            "widget-graph",
            "widget-graph-intense",
            //ImageGroup.SideMenuIcons,
            "slider-icon-amplitude",
            "slider-icon-burst",
            "slider-icon-freq",
            "slider-icon-noise",
            "slider-icon-offset",
            "slider-icon-phase",
            "widget-list",
            "widget-slider-knob",
            "widget-checkbox-off",
            "widget-checkbox-on-1",
            "widget-checkbox-on-2",
            "widget-radio-on",
            "widget-radio-off",
            "widget-arrow-up",
            "widget-arrow-right",
        };
    }
}
