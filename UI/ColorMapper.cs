using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using LabNation.DeviceInterface.Devices;
using LabNation.Interfaces;

namespace ESuite
{
    public static class MyExtensions
    {
        public static MappedColor ToManagedColor(this Channel ch)
        {
            MappedColor m = MappedColor.Undefined;
            Enum.TryParse(ch.GetType().Name + ch.Name, out m);
            if (m == MappedColor.Undefined)
                Enum.TryParse(ch.GetType().Name, out m);
            if (m == MappedColor.Undefined)
            {
                throw new Exception("MappedColor does not exist for either " + ch.GetType().Name + ch.Name + " nor " + ch.GetType().Name);
            }
            return m;
        }
        public static Color C(this MappedColor m)
        {
            return ColorMapper.ColorDictionary(m);
        }
        public static Color CC(this MappedColor m)
        {
            return ColorMapper.ConstrastTextColorDictionary[m];
        }
        public static Color ContrastRatio(Color col1, Color col2, float wantedContrastRatio)
        {
            float rel1 = 0.2126f * (float)col1.R + 0.7152f * (float)col1.G + 0.0722f * (float)col1.B;
            float rel2 = 0.2126f * (float)col2.R + 0.7152f * (float)col2.G + 0.0722f * (float)col2.B;

            if (rel1 < rel2)
            {
                float actualContrastRatio = (rel2 / 255f + 0.05f) / (rel1 / 255f + 0.05f);
                if (actualContrastRatio < wantedContrastRatio)
                {
                    float missingRatio = wantedContrastRatio / actualContrastRatio;
                    col1 = new Color((int)((float)col1.R / missingRatio), (int)((float)col1.G / missingRatio), (int)((float)col1.B / missingRatio));
                }
            }
            else
            {
                float actualContrastRatio = (rel1 / 255f + 0.05f) / (rel2 / 255f + 0.05f);
                if (actualContrastRatio < wantedContrastRatio)
                {
                    float missingRatio = wantedContrastRatio / actualContrastRatio;
                    col1 = new Color((int)((float)col1.R * missingRatio), (int)((float)col1.G * missingRatio), (int)((float)col1.B * missingRatio));
                }
            }

            return col1;
        }

        public static List<KeyCombo> Keys(this ButtonType b)
        {
            return ColorMapper.buttonKeys[b];
        }
    }  

    public enum MappedColor {
        Undefined,
        Font,
        FontSubtle,
        FontDecoder,
        DisabledFont,
        ButtonBarBackground,
        ButtonBarForeground,
        ButtonImageOverlay,
        ClippingMaskOverlay,
        MainAreaBackground,
        GridMajor,
        GridMinor,
        GridHilite,
        MeasurementBoxBackground,
        MultimeterPatchBackground,
        GridDivisionLabelBackground,
        GridDivisionLabelTabs,
        GridDivisionWheelCenter,
        GridDivisionWheelBorder,
        MeasurementFont,
        TimeScaleFont,
        MenuTransparantBackground,
        MenuSolidBackground,
        MenuSolidBackgroundVoid,
        DebugColor,
        SliderKnob,
        PanoramaTriggerIndicator,
        PanoramaBackground,
        PanoramaShading,
        AcquisitionFetchProgress,
        ContextMenuBackground,
        ContextMenuText,
        CursorOverlay,
        Record,
        Neutral,
        Selected,
        VerticalCursor,
        Disabled,
        Highlight,
        HelpButton,
        MenuButtonBackground,
        MenuButtonForeground,
        GridBorder,
        System,
        InternalArrow,
        GridLabel,

        NumPadNumberBackground,
        NumPadNumberForeground,
        NumPadScalerBackground,
        NumPadScalerForeground,
        NumPadInputBackground,
        NumPadInputBackgroundError,
        NumPadInputForeground,
        NumPadInputForegroundError,
        Transparent,

        //the range above 511 is looked up as a wave color: stored inside the settings
        FFTChannel                  = 512,
        XYChannel                   = 513,
        MathChannel                 = 514,
        OperatorAnalogChannel       = 515,
        ReferenceChannel            = 516,
        OperatorDigitalChannel      = 517,
        ProtocolDecoderChannel      = 518,
        DigitalBusChannel           = 519,
        AnalogChannelA              = 520,
        AnalogChannelB              = 521,
        DigitalChannel0             = 522,
        DigitalChannel1             = 523,
        DigitalChannel2             = 524,
        DigitalChannel3             = 525,
        DigitalChannel4             = 526,
        DigitalChannel5             = 527,
        DigitalChannel6             = 528,
        DigitalChannel7             = 529,
    }

    public enum ButtonType {
        Confirm,
        Cancel,
        ConfirmOrCancel,

    }

    public static class ColorMapper
    {
        public static Dictionary<ButtonType, List<KeyCombo>> buttonKeys = new Dictionary<ButtonType, List<KeyCombo>>()
        {
            { ButtonType.Confirm, new List<KeyCombo>() { Keys.Enter, Keys.Space } },
            { ButtonType.Cancel, new List<KeyCombo>() { Keys.Escape } },
			{ ButtonType.ConfirmOrCancel, new List<KeyCombo>() { Keys.Enter, Keys.Escape, Keys.Space } },
        };

        public enum Mode
        {
            NORMAL,
            DARK
        }

        public static int NumberDisplaySignificance = 3;
        public static float AnimationTime = 0.3f;
        public static int ToastFadeTime = 250; //ms

        public const double timePrecision = 10e-9; //ns time precision
        public const double cursorTimePrecision = 0.1e-9; //0.1ns time precision
        public const double voltagePrecision = 1e-3; //mV voltage precision
        public const double averagedVoltagePrecision = voltagePrecision / 2000;
        

        public static Mode CurrentMode { get { return Settings.CurrentRuntime.GuiColor.Value; } set { Settings.CurrentRuntime.GuiColor = value; } }

        public static Color ColorDictionary(MappedColor mappedColor)
        {
            switch (CurrentMode)
            {
                case Mode.NORMAL:
                    if (Convert.ToInt32(mappedColor) < 512)
                        return ColorMapperLight.colorDictionary[mappedColor];
                    else //lookup as waveColor
                        return Settings.Current.WaveColorsNormal[mappedColor];
                case Mode.DARK:
                    if (Convert.ToInt32(mappedColor) < 512)
                        return ColorMapperDark.colorDictionary[mappedColor];
                    else //lookup as waveColor
                        return Settings.Current.WaveColorsDark[mappedColor];
                default:
                    return Color.Pink;
            }
        }

        public static Color EnhanceContrast(Color orig)
        {
            switch (CurrentMode)
            {
                case Mode.NORMAL:
                    Color newColor = orig;
                    newColor.R = (byte)((float)newColor.R * 0.6f);
                    newColor.G = (byte)((float)newColor.G * 0.6f);
                    newColor.B = (byte)((float)newColor.B * 0.6f);
                    return newColor;
                case Mode.DARK:
                    return orig;
                default:
                    return orig;
            }
        }

        public static Dictionary<MappedColor, Color> ConstrastTextColorDictionary
        {
            get
            {
                switch (CurrentMode)
                {
                    case Mode.NORMAL:
                        return ColorMapperLight.contrastTextColors;
                    case Mode.DARK:
                        return ColorMapperDark.contrastTextColors;
                    default:
                        return ColorMapperLight.contrastTextColors;
                }
            }
        }

        public static Dictionary<DecoderOutputColor, Color> DecoderEventColorDictionary
        {
            get
            {
                switch (CurrentMode)
                {
                    case Mode.NORMAL:
                        return ColorMapperLight.decoderEventColorMapper;
                    case Mode.DARK:
                        return ColorMapperDark.decoderEventColorMapper;
                    default:
                        return ColorMapperLight.decoderEventColorMapper;
                }
            }
        }
    }
}
