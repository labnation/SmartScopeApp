using System.Collections.Generic;
using Microsoft.Xna.Framework;
using LabNation.Interfaces;

namespace ESuite
{
    public static class ColorMapperLight
    {
        private static Color LabNationGreen = new Color(153, 204, 0);
        private static Color LabNationBlue = new Color(0, 153, 255);

        public static readonly Dictionary<MappedColor, Color> colorDictionary = new Dictionary<MappedColor, Color>() {
            { MappedColor.Font, Color.Black },
            { MappedColor.FontSubtle, new Color(100,100,100) },
            { MappedColor.FontDecoder, Color.White },
            { MappedColor.DisabledFont, Color.LightGray },
            { MappedColor.ButtonBarBackground, Color.White },
            { MappedColor.ButtonBarForeground, Color.Black },
            { MappedColor.ButtonImageOverlay, Color.White },
            { MappedColor.ClippingMaskOverlay, new Color(220, 220, 220, 255) },
            { MappedColor.MainAreaBackground, new Color(242, 242, 242, 255) },
            { MappedColor.GridMajor, new Color(165, 165, 165) },
            { MappedColor.GridMinor, new Color(220, 220, 220) },
            { MappedColor.GridHilite, new Color(100, 100, 100) },
            { MappedColor.MeasurementBoxBackground, Color.FromNonPremultiplied(255, 255, 255, 220) },
            { MappedColor.MultimeterPatchBackground, Color.White},
            { MappedColor.GridDivisionLabelBackground, Color.White*0.85f },
            { MappedColor.GridDivisionLabelTabs, Color.Black },
            { MappedColor.GridDivisionWheelCenter, new Color(255, 255, 255, 255) },
            { MappedColor.GridDivisionWheelBorder, new Color(255, 255, 255, 20) },
            { MappedColor.MeasurementFont, new Color(20, 20, 20)  },
            { MappedColor.TimeScaleFont, Color.Black },
            { MappedColor.CursorOverlay, Color.FromNonPremultiplied(178, 178, 178, 255)},
            { MappedColor.MenuTransparantBackground, Color.FromNonPremultiplied(255, 255, 255, 220) },
            { MappedColor.MenuSolidBackground, Color.White },
            { MappedColor.MenuSolidBackgroundVoid, new Color(246, 246, 246, 255) },
            { MappedColor.DebugColor, Color.Red },
            { MappedColor.SliderKnob, Color.Red },
            { MappedColor.PanoramaTriggerIndicator, Color.Red },
            { MappedColor.PanoramaShading, new Color(0,0,0,50) },
            { MappedColor.PanoramaBackground, new Color(242, 242, 242, 255) },
            { MappedColor.VerticalCursor, new Color(100, 100, 100)},
            { MappedColor.Selected     , new Color(154, 203, 0)},
            { MappedColor.Neutral      , Color.White},
            { MappedColor.Record       , Color.Red},
            { MappedColor.Disabled     , Color.LightGray},
            { MappedColor.Highlight    , Color.Red},
            { MappedColor.HelpButton   , new Color(255,255,255,200) },
            { MappedColor.MenuButtonBackground, new Color(246, 246, 246, 255) },
            { MappedColor.MenuButtonForeground, new Color(75, 75, 75, 255) },
            { MappedColor.GridBorder   , Color.DarkGray },
            { MappedColor.System       , Color.Gray},            
            { MappedColor.InternalArrow,            Color.Black},
            { MappedColor.GridLabel,                Color.Black},
            { MappedColor.AcquisitionFetchProgress, Color.FromNonPremultiplied(LabNationGreen.R, LabNationGreen.G, LabNationGreen.B, 80) },
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
            { MappedColor.MathChannel, Color.Red    },
            { MappedColor.FFTChannel, Color.Red},
            { MappedColor.XYChannel, Color.Red},
            { MappedColor.ProtocolDecoderChannel, Color.DarkBlue},
            { MappedColor.ReferenceChannel, Color.Gray},
            { MappedColor.OperatorAnalogChannel, Color.DarkBlue},
            { MappedColor.OperatorDigitalChannel, Color.DarkBlue},
            { MappedColor.AnalogChannelA,         LabNationGreen},
            { MappedColor.AnalogChannelB,         LabNationBlue},
            { MappedColor.DigitalChannel0,       Color.SaddleBrown     },
            { MappedColor.DigitalChannel1,       Color.Red             },
            { MappedColor.DigitalChannel2,       Color.Orange          },
            { MappedColor.DigitalChannel3,       Color.DarkGreen       },
            { MappedColor.DigitalChannel4,       new Color(200,200,0,255)      }, //dark yellow
            { MappedColor.DigitalChannel5,       Color.DodgerBlue      },
            { MappedColor.DigitalChannel6,       Color.Fuchsia         },
            { MappedColor.DigitalChannel7,       Color.Gray            },
            { MappedColor.DigitalBusChannel,      Color.White},
        };

        public static readonly Dictionary<MappedColor, Color> contrastTextColors = new Dictionary<MappedColor, Color>() {
            { MappedColor.AnalogChannelA,         Color.White},
            { MappedColor.AnalogChannelB,         Color.White},
            { MappedColor.ReferenceChannel,       Color.White},
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
            { DecoderOutputColor.DarkPurple, Color.FromNonPremultiplied(90,0,90,255) },
            { DecoderOutputColor.DarkRed, Color.DarkRed },
            { DecoderOutputColor.Green, Color.Green },
            { DecoderOutputColor.Orange, Color.Orange },
            { DecoderOutputColor.Purple, Color.FromNonPremultiplied(150,0,180,255) },
            { DecoderOutputColor.Red, Color.Red },
            { DecoderOutputColor.Yellow, Color.Yellow }
        };
    }
}
