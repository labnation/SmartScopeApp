using System.Collections.Generic;
using Microsoft.Xna.Framework;
using LabNation.Interfaces;

namespace ESuite
{
    public class ColorMapperDark
    {
        public static readonly Dictionary<MappedColor, Color> colorDictionary = new Dictionary<MappedColor,Color>() {
            { MappedColor.Font, Color.Black },
            { MappedColor.FontSubtle, new Color(100,100,100) },
            { MappedColor.FontDecoder, Color.White },
            { MappedColor.DisabledFont, Color.LightGray},
            { MappedColor.ButtonBarBackground, Color.White },
            { MappedColor.ButtonBarForeground, Color.Black },
            { MappedColor.ButtonImageOverlay, new Color(220, 220, 220)},
            { MappedColor.ClippingMaskOverlay, Color.Gray},
            { MappedColor.MainAreaBackground, new Color(20, 20, 20, 255)},
            { MappedColor.GridMajor, new Color(80, 80, 80)},
            { MappedColor.GridMinor, new Color(45, 45, 45)},
            { MappedColor.GridHilite, new Color(110, 110, 110)},
            { MappedColor.MeasurementBoxBackground, new Color(0, 0, 0, 180)},
            { MappedColor.MultimeterPatchBackground, Color.Black},
            { MappedColor.GridDivisionLabelBackground, Color.Black *0.65f},
            { MappedColor.GridDivisionLabelTabs, Color.White },
            { MappedColor.GridDivisionWheelCenter, new Color(70, 70, 70, 255) },
            { MappedColor.GridDivisionWheelBorder, new Color(10, 10, 10, 50) },
            { MappedColor.MeasurementFont, new Color(230,230,230) },
            { MappedColor.CursorOverlay, new Color(130, 130, 130, 255)},
            { MappedColor.TimeScaleFont, Color.White},
            { MappedColor.MenuTransparantBackground, new Color(220, 220, 220, 230)}, //same as buttonbar, but transparent
            { MappedColor.MenuSolidBackground, new Color(220, 220, 220)}, //same as buttonbar
            { MappedColor.MenuSolidBackgroundVoid, new Color(212, 212, 212, 255)}, //246*220/255=212
            { MappedColor.DebugColor, Color.Red },
            { MappedColor.SliderKnob, Color.Red },
            { MappedColor.PanoramaTriggerIndicator, Color.Red },
            { MappedColor.PanoramaShading, new Color(0,0,0,120) },
			{ MappedColor.PanoramaBackground, new Color(20, 20, 20, 255) },
            { MappedColor.VerticalCursor, new Color(100, 100, 100)},
            { MappedColor.Selected     , new Color(154, 203, 0)},
            { MappedColor.Neutral      , Color.White},
            { MappedColor.Record       , Color.Red},
            { MappedColor.Disabled     , Color.LightGray},
            { MappedColor.Highlight    , Color.Red},
            { MappedColor.HelpButton   , new Color(255,255,255,200)},
            { MappedColor.MenuButtonBackground, new Color(246, 246, 246, 255) },
            { MappedColor.MenuButtonForeground, new Color(75, 75, 75, 255) },
            { MappedColor.GridBorder   , Color.Black }, 
            { MappedColor.System       , Color.Gray},            
            { MappedColor.InternalArrow,      Color.White},
            { MappedColor.GridLabel,        Color.White},
            { MappedColor.AcquisitionFetchProgress, Color.FromNonPremultiplied(0, 100, 0, 80) },
            { MappedColor.Transparent, Color.Transparent                      },

            { MappedColor.NumPadNumberBackground,     Color.LightGray                  },
            { MappedColor.NumPadNumberForeground,     Color.Black                      },
            { MappedColor.NumPadScalerBackground,     Color.Orange                     },
            { MappedColor.NumPadScalerForeground,     Color.Black                      },
            { MappedColor.NumPadInputBackground,      Color.Black                      },
            { MappedColor.NumPadInputBackgroundError, Color.Red                        },
            { MappedColor.NumPadInputForeground,      new Color(242, 242, 242, 255)    },
            { MappedColor.NumPadInputForegroundError, Color.Black                      },

            { MappedColor.ContextMenuBackground, new Color(255, 255, 255, 230) },
            { MappedColor.ContextMenuText, Color.Black },
        };

        public static readonly Dictionary<MappedColor, Color> DefaultWaveColors = new Dictionary<MappedColor, Color>()
        {
            { MappedColor.AnalogChannelA,         new Color(153, 204, 0)},
            { MappedColor.AnalogChannelB,         new Color(0, 153, 255)},
            { MappedColor.DigitalChannel0,       Color.SaddleBrown     },
            { MappedColor.DigitalChannel1,       Color.Red             },
            { MappedColor.DigitalChannel2,       Color.Orange          },
            { MappedColor.DigitalChannel3,       Color.Green       },
            { MappedColor.DigitalChannel4,       Color.Yellow          },
            { MappedColor.DigitalChannel5,       Color.DodgerBlue      },
            { MappedColor.DigitalChannel6,       Color.Fuchsia         },
            { MappedColor.DigitalChannel7,       Color.LightGray            },
            { MappedColor.MathChannel, Color.Red},
            { MappedColor.FFTChannel, Color.Red},
            { MappedColor.XYChannel, Color.Red},
            { MappedColor.ProtocolDecoderChannel, Color.DarkBlue},
            { MappedColor.ReferenceChannel, Color.Gray},
            { MappedColor.OperatorAnalogChannel, Color.Gold},
            { MappedColor.OperatorDigitalChannel, Color.Gold},
            { MappedColor.DigitalBusChannel,      Color.White},
        };

        public static readonly Dictionary<MappedColor, Color> contrastTextColors = new Dictionary<MappedColor, Color>() {
            { MappedColor.AnalogChannelA,         Color.White},
            { MappedColor.AnalogChannelB,         Color.White},
            { MappedColor.ReferenceChannel,         Color.White},
            { MappedColor.DigitalChannel0,       Color.White},
            { MappedColor.DigitalChannel1,       Color.White},
            { MappedColor.DigitalChannel2,       Color.Black},
            { MappedColor.DigitalChannel3,       Color.Black},
            { MappedColor.DigitalChannel4,       Color.Black},
            { MappedColor.DigitalChannel5,       Color.White},
            { MappedColor.DigitalChannel6,       Color.White},
            { MappedColor.DigitalChannel7,       Color.White},
            { MappedColor.VerticalCursor,        Color.White},
            { MappedColor.MathChannel,           Color.White},
            { MappedColor.FFTChannel,             Color.White},
            { MappedColor.ProtocolDecoderChannel, Color.White},
            { MappedColor.OperatorAnalogChannel, Color.White},
            { MappedColor.OperatorDigitalChannel, Color.White},
            { MappedColor.DigitalBusChannel,      Color.Black},
            { MappedColor.Neutral,                Color.Black},
        };

        public static readonly Dictionary<DecoderOutputColor, Color> decoderEventColorMapper = new Dictionary<DecoderOutputColor, Color>() {
            { DecoderOutputColor.Black, Color.Black },
            { DecoderOutputColor.Blue, Color.Blue },
            { DecoderOutputColor.DarkBlue, Color.DarkBlue },
            { DecoderOutputColor.DarkPurple,  Color.FromNonPremultiplied(90,0,90,255) },
            { DecoderOutputColor.DarkRed, Color.DarkRed },
            { DecoderOutputColor.Green, Color.Green },
            { DecoderOutputColor.Orange, Color.Orange },
            { DecoderOutputColor.Purple, Color.FromNonPremultiplied(150,0,180,255) },
            { DecoderOutputColor.Red, Color.Red },
            { DecoderOutputColor.Yellow, Color.Yellow }
        };
    }
}
