using System;
using System.Linq;

namespace ESuite
{
    static internal partial class Scaler
    {
        //define scaling factors for predefined GuiScales, in percentage
		public static Scale CurrentScale;
        public static PixelDensity CurrentDpi = BASE_DPI;

        private static int PixelsPerInch { get { return (int)CurrentDpi; } }
        
        //Define nominal values of fonts
        static public string GetFontButtonBar() { return FontBuilder(11f); }
        static public string GetFontTextInButton() { return FontBuilder(9f); }
        static public string GetFontSideMenu() { return FontBuilder(10f); }
        static public string GetFontContextMenuLarge() { return FontBuilder(12f); }
        static public string GetFontContextMenuMedium() { return FontBuilder(10f); }
        static public string GetFontContextMenuSmall() { return FontBuilder(9f); }
        static public string GetFontContextMenuTiny() { return FontBuilder(8f); }
        static public string GetFontContextMenuLargeBold() { return GetFontContextMenuLarge() + "Bold"; }
        static public string GetFontContextMenuMediumBold() { return GetFontContextMenuMedium() + "Bold"; }
        static public string GetFontContextMenuSmallBold() { return GetFontContextMenuSmall() + "Bold"; }
        
        static public string GetFontDialog() { return FontBuilder(10f); }
        static public string GetFontInternalArrows() { return FontBuilder(7f); }

        static public string GetFontCursorSide() { return FontBuilder(9f); }
        static public string GetFontCursorCenter() { return GetFontIndicatorCenter(); }
        static public string GetFontCursorBottom() { return GetFontIndicatorBottom(); }
        
        static public string GetFontIndicatorCenter() { return FontBuilder(10f); }
        static public string GetFontIndicatorBottom() { return FontBuilder(8f); }

        static public string GetFontLegacyBarRegular() { return FontBuilder(8f); }
        static public string GetFontLegacyBarBold() { return GetFontLegacyBarRegular() + "Bold"; }
        
        static public string GetFontLogBox() { return FontBuilder(7f); }
        static public string GetFontMeasurementTitle() { return FontBuilder(14f); }
        static public string GetFontMeasurementValue() { return FontBuilder(10f); }
        static public string GetFontMeasurementValueBold() { return FontBuilder(10f) + "Bold"; }
        static public string GetFontMeasurementGraphTitle() { return FontBuilder(10f); }
        static public string GetFontMeasurementMultimeter() { return FontBuilder(32f); }
        static public string GetFontMeasurementMultimeterBold() { return FontBuilder(32f) + "Bold"; }
        static public string GetFontSliderValue() { return FontBuilder(7f); }

        //This is how we assume the fonts (and their filenames) are made, with 96 pixels per point
        public static int InchesToPixels(this float sizeInches) { return (int)Math.Round(sizeInches * PixelsPerInch * guiScalers[CurrentScale]); }
        private static float pointsAt96ToPoints(float sizePointsBaseDPI) { return (float)(sizePointsBaseDPI * PixelsPerInch / (double)BASE_DPI * guiScalers[CurrentScale]); }
        public static float PixelsToInches(this int sizePx) { return (float)(sizePx / (double)PixelsPerInch / guiScalers[CurrentScale]); }
        public static float ToInchesBaseDpi(this int sizePx) { return sizePx / (float)BASE_DPI; }

        //other size definitions
        public static int DefaultMaxNbrChars { get { return 8; } }

        static public int MinimalPinchSize 
        {
            get
            {
                return InchesToPixels(0.3f);
            }
        }

        static public int MinimalTouchDimension
        {
            get
            {
                return InchesToPixels(1f);
            }
        }

        //FIXME: to be moved to UI building functions
        static public float GraphLabelMargin = 10.ToInchesBaseDpi();
        static public float GraphLabelWidth = 60.ToInchesBaseDpi();
        static public float GraphLabelHeight = 30.ToInchesBaseDpi();
        static public float ContextMenuSize = 42.ToInchesBaseDpi();
        static public float SideMenuWidth = 160.ToInchesBaseDpi();
        static public float SideMenuHeight = 34.ToInchesBaseDpi();
        static public float ButtonMargin = 6.ToInchesBaseDpi();
        static public float LegacyBarMargin = 10.ToInchesBaseDpi();
        static public float GridLabelMargin = 2.ToInchesBaseDpi();
        static public float MeasurementGraphTitleMargin = 4.ToInchesBaseDpi();

        static public Microsoft.Xna.Framework.Point MenuItemSize
        {
            get
            {
                float sizeX = InchesToPixels(SideMenuWidth);
                float sizeY = InchesToPixels(SideMenuHeight);
                return new Microsoft.Xna.Framework.Point((int)sizeX, (int)sizeY);
            }
        }

        static public Microsoft.Xna.Framework.Point MenuItemMargin
        {
            get
            {
                int margin = InchesToPixels(10.ToInchesBaseDpi());
                return new Microsoft.Xna.Framework.Point(margin, margin);
            }
        }
        
        /// <summary>
        /// method which looks up in which container an image belongs, and returns the correct assetFileName
        /// </summary>
        /// <param name="imageName">Filename. Lower-case only please</param>
        /// <returns></returns>
        static public string ScaledImageName(string imageName)
        {
            return System.IO.Path.Combine("Images", ImageFileName(imageName, CurrentDpi, CurrentScale));
        }

        ////////////////////////////////////////////////////////////////////////////////////
        //helper methods
        static public string FontBuilder(float sizePointsAtBaseDpi)
        {
            float sizePt = pointsAt96ToPoints(sizePointsAtBaseDpi);
            var goodSizes = fontSizes.Where(x => x <= sizePt);
            
            int size = fontSizes.Min();
            if (goodSizes.Count() != 0)
                size = goodSizes.Max();
            
            return "Fonts/Abel" + size.ToString();
        }
    }
}
