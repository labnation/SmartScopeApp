using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using ESuite.DataProcessors;
using LabNation.DeviceInterface.Devices;
using LabNation.Interfaces;
using LabNation.DeviceInterface.DataSources;

namespace ESuite.Drawables
{
    internal enum RadixType { Binary, Decimal, Hex, ASCII}
    internal static class RadixPrinter
    {
        internal static SpriteFont ReferenceFont;
        internal static string Print(DecoderOutputValueNumeric decoderOutput, RadixType rad)
        {
            return Print((uint)decoderOutput.Value, decoderOutput.ValueBitSize, rad);
        }

        internal static string Print(uint number, int bitSize, RadixType rad)
        {
            uint bitmask = UInt32.MaxValue;
            int shiftLeft = sizeof(UInt32)  * 8 - bitSize;
            bitmask = bitmask >> shiftLeft;
            number &= bitmask;

            if (rad == RadixType.Hex)
                return "0x" + number.ToString("X").PadLeft(bitSize / 4, '0');
            else if (rad == RadixType.Decimal)
                return number.ToString();
            else if (rad == RadixType.Binary)
                return Convert.ToString(number, 2).PadLeft(bitSize, '0') + 'b';
            else if (rad == RadixType.ASCII)
                //FIXME: use sizeof to parse larger words, dwords,...
            {
                byte byteval = (byte)number;
                if (ReferenceFont == null) return "///";
                string result = System.Text.Encoding.ASCII.GetString(new byte[] { byteval });
                if (ReferenceFont.Characters.Contains(result.ToCharArray()[0]))
                    return result;
                else
                    if (AsciiLookup.ContainsKey(byteval))
                        return AsciiLookup[byteval];
                    else
                        return "[" + number.ToString() + "]";
            }

            return "RAD_ERR";
        }
        internal static Dictionary<byte, string> AsciiLookup = new Dictionary<byte, string>() { 
            {0,"NULL"},
            {1,"SOH"},
            {2,"STX"},
            {3,"ETX"},
            {4,"EOT"},
            {5,"ENQ"},
            {6,"ACK"},
            {7,"BEL"},
            {8,"BS"},
            {9,"HT"},
            {10,"LF"},
            {11,"VT"},
            {12,"FF"},
            {13,"CR"},
            {14,"SO"},
            {15,"SI"},
            {16,"DLE"},
            {17,"DC1"},
            {18,"DC2"},
            {19,"DC3"},
            {20,"DC4"},
            {21,"NAK"},
            {22,"SYN"},
            {23,"ETB"},
            {24,"CAN"},
            {25,"EM"},
            {26,"SUB"},
            {27,"ESC"},
            {28,"FS"},
            {29,"GS"},
            {30,"RS"},
            {31,"US"},
            {127,"DEL"},
            {32," "}
        };
    }

    internal struct DecodedDrawItem
    {
        public float startTime;
        public float finishTime;
        public DecoderOutput decoderOutput;
        public DecodedDrawItem(float startTime, float finishTime, DecoderOutput d)
        {
            this.decoderOutput = d;
            this.startTime = startTime;
            this.finishTime = finishTime;
        }
        
    }

    internal class WaveformDecoded : Waveform
    {
        new static public Dictionary<Channel, WaveformDecoded> Waveforms
        {
            get
            {
                return Waveform.Waveforms.
                    Where(x => x.Value is WaveformDecoded).
                    ToDictionary(x => x.Key, x => (WaveformDecoded)x.Value);
            }
        }
        
        private SpriteFont font;
        public override double TimeOffset
        {
            get { return base.TimeOffset; }
            set
            {
                double oldValue = base.TimeOffset;
                base.TimeOffset = value;
                if(oldValue != value)
                    FilterDrawList();
            }
        }

        List<DecodedDrawItem> decoderOutputDrawList = new List<DecodedDrawItem>();
        List<DecodedDrawItem> decoderOutputDrawListFiltered = new List<DecodedDrawItem>(); //init, so app doesn't crash when decoder is added when no data is available (require trigger)

        private void FilterDrawList()
        {
            decoderOutputDrawListFiltered = decoderOutputDrawList.Where(x => x.finishTime > TimeOffset && x.startTime < TimeOffset + TimeRange).ToList();
        }
        private List<Vector2> screenCenterPositions = new List<Vector2>();
        private int maxBytesPerLine = 200;
        float fontHeightInPixels;

        public WaveformDecoded(Graph graph, MappedColor graphColor, Channel channel)
            : base(graph, graphColor, PrimitiveType.TriangleStrip, 100, channel) //100 decoderOutputs is more than enough to fill 1 screen -- can't display that already
        {
            this.Minimum = 0;
            this.VoltageRange = 8f; //must be multiple of number of vertical divisions

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            font = content.Load<SpriteFont>(Scaler.GetFontSideMenu() + "Bold");
            
            //store fontsize, as wave drawing depends on it
            Vector2 fontSize = font.MeasureString("0xFF");
            this.fontHeightInPixels = fontSize.Y;            
        }

        override protected VertexPositionColor[] CreateVertices(WaveDataBuffer[] dataBuffers, double visibleStartTime, double visibleEndTime)
        {
            if (dataBuffers == null) return null;
            if (dataBuffers.Length < 1) return null;
            if (dataBuffers[0] == null) return null;
            WaveDataBuffer dataBuffer = dataBuffers[0];

            if (!contentLoaded) LoadContent();
            DecoderOutput[] decoderOutputList = (DecoderOutput[])dataBuffer.SubsampledData;
            
            //next: find indices in buffer, corresponding to visibleStartTime and visibleEndTime
            int pointer = 0;
            int bufferDrawStartIndex = 0;
            int bufferDrawEndIndex = -1;

            if (decoderOutputList.Length > 0)
            {
                while (dataBuffer.RawStartTime + (double)decoderOutputList[pointer].StartIndex * dataBuffer.RawSamplePeriod < visibleStartTime && pointer < decoderOutputList.Length - 2)
                    pointer++;
                bufferDrawStartIndex = (int)Math.Max(0, pointer - 1); //-1 needed as otherwise first block is cut off
                //visibleEndTime
                while (dataBuffer.RawStartTime + (double)decoderOutputList[pointer].StartIndex * dataBuffer.RawSamplePeriod < visibleEndTime && pointer < decoderOutputList.Length - 1)
                    pointer++;
                bufferDrawEndIndex = pointer;
            }
            
            float gridHeightInPixels = parent.Boundaries.Height;
            float halfBoxHeight = fontHeightInPixels / gridHeightInPixels * (float)VoltageRange; //voltageRange represents full vertical size
            this.ActiveRange = halfBoxHeight*2.2f;
            float onePixelHeight = 1.0f / gridHeightInPixels * (float)VoltageRange; //voltageRange represents full vertical size
            float minBoxWidth = (float)TimeRange / samplesToDrawOnScreen;

            float startTime = (float)dataBuffer.SubsampledStartTime;
            float samplingPeriod = (float)dataBuffer.RawSamplePeriod;
            
            
            List<VertexPositionColor> vertexList = new List<VertexPositionColor>();
            decoderOutputDrawList.Clear();

            Vector3 screenLeftScreenPos = new Vector3(parent.Boundaries.Left,0,0);
            Vector3 screenLeftTimePos = device.Viewport.Unproject(screenLeftScreenPos, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            Vector3 screenRightScreenPos = new Vector3(parent.Boundaries.Right,0,0);
            Vector3 screenRightTimePos = device.Viewport.Unproject(screenRightScreenPos, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);

            float screenLeftTime = screenLeftTimePos.X;
            float screenRightTime = screenRightTimePos.X;

            //Starting points (see h-g below)
            Color color = GraphColor.C();
            vertexList.Add(new VertexPositionColor(new Vector3(startTime, -onePixelHeight, 0.0f), color));
            vertexList.Add(new VertexPositionColor(new Vector3(startTime,  onePixelHeight, 0.0f), color));
            
            //byte boxes
            for (int i = bufferDrawStartIndex; i <= bufferDrawEndIndex; i++)
            {
                float leftBoxPos = startTime + samplingPeriod * (float)decoderOutputList[i].StartIndex;
                float rightBoxPos = startTime + samplingPeriod * (float)decoderOutputList[i].EndIndex;

                //in case WaveDataBuffer content is highly decimated, the datapoints will be very thin and a few pixels apart -> this will render the decoder as gaps separated by thin lines
                if (rightBoxPos - leftBoxPos < minBoxWidth)
                    rightBoxPos = leftBoxPos + minBoxWidth;

                //save timestamps of Boundaries, so they can be converted to screenpos whenever needed
                decoderOutputDrawList.Add(new DecodedDrawItem(leftBoxPos, rightBoxPos, decoderOutputList[i]));
                
                //fetch actual colors
                Color boxColor = ColorMapper.DecoderEventColorDictionary[decoderOutputList[i].Color];

                //make slightly transparant, for increased fun
                boxColor.A = 220;

                //     d------f
                //    /        \
                //   b          h-------------------------------------b
                //   a          g-------------------------------------a
                //    \        /
                //     c------e

                //calc angle of edge, maxed by boxlength
                float tilt = (float)(halfBoxHeight/voltageRange*TimeRange/2.0f)*0;
                float boxLength = rightBoxPos - leftBoxPos;
                if (tilt > boxLength / 8.0f) tilt = boxLength / 8.0f;

                //connection line
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos, -onePixelHeight, 0.0f), color));               //a
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos, onePixelHeight, 0.0f), color));                //b

                //colored box
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos, -onePixelHeight, 0.0f), boxColor));                  //a
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos,  onePixelHeight, 0.0f), boxColor));                  //b
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos+tilt, -halfBoxHeight, 0.0f), boxColor));              //c
                vertexList.Add(new VertexPositionColor(new Vector3(leftBoxPos+tilt, halfBoxHeight, 0.0f), boxColor));               //d
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos - tilt, -halfBoxHeight, 0.0f), boxColor));           //e
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos - tilt,  halfBoxHeight, 0.0f), boxColor));           //f
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos, -onePixelHeight, 0.0f), boxColor));                 //g
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos,  onePixelHeight, 0.0f), boxColor));                 //h

                //connection line
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos, -onePixelHeight, 0.0f), color));                 //g
                vertexList.Add(new VertexPositionColor(new Vector3(rightBoxPos, onePixelHeight, 0.0f), color));                  //h                                
            }
            
            //Ending points (see a-b above)
            //Tough case to define the location of where this should end, because it might be that the raw waves are longer than the last output of the decoder
            float maxRightTime = screenRightTime;
            if (decoderOutputList.Length > 0)
                if (maxRightTime < startTime + samplingPeriod * (float)decoderOutputList.Last().EndIndex)
                    maxRightTime = startTime + samplingPeriod * (float)decoderOutputList.Last().EndIndex;

            vertexList.Add(new VertexPositionColor(new Vector3(maxRightTime, -onePixelHeight, 0.0f), color));
            vertexList.Add(new VertexPositionColor(new Vector3(maxRightTime, onePixelHeight, 0.0f), color));           

            FilterDrawList();

            return vertexList.ToArray();
        }

        protected override void CalculateBoundaries()
        {
            if (parent == null) return;

            float boundaryHeight = fontHeightInPixels * 3.0f;
            Vector3 worldOrigin = new Vector3();
            Vector3 screenOrigin = device.Viewport.Project(worldOrigin, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            backgroundRectangle = new Rectangle(parent.Boundaries.Left, (int)(screenOrigin.Y - boundaryHeight / 2.0f), parent.Boundaries.Width, (int)(boundaryHeight));
        }

        protected override void DrawInternal(GameTime time)
        {
            base.DrawInternal(time);

			spriteBatch.Begin(SpriteSortMode.Deferred, fontBlendState);
            float screenStartTime = (float)this.TimeOffset;
            float screenEndTime = (float)(this.TimeOffset + this.timeRange);
            Vector3 worldStartLocMin = new Vector3((float)this.TimeOffset, 0, 0);
            Vector3 screenStartLocMin = device.Viewport.Project(worldStartLocMin, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            Vector3 worldFinishLocMax = new Vector3((float)(this.TimeOffset+this.timeRange), 0, 0);
            Vector3 screenFinishLocMax = device.Viewport.Project(worldFinishLocMax, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
            foreach (DecodedDrawItem drawItem in decoderOutputDrawListFiltered)
            {
                DecoderOutput decoderOutput = drawItem.decoderOutput;

                //calc screenpos of text, needed as fixed screenpositions wouldn't change when wave (array) is moved
                Vector3 worldStartLoc = new Vector3(drawItem.startTime, 0, 0);
                Vector3 screenStartLoc = device.Viewport.Project(worldStartLoc, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
                Vector3 worldFinishLoc = new Vector3(drawItem.finishTime, 0, 0);
                Vector3 screenFinishLoc = device.Viewport.Project(worldFinishLoc, Matrix.Identity, View, scaleOffsetLerpMatrix.CurrentValue);
                screenStartLoc = screenStartLoc.X < screenStartLocMin.X ? screenStartLocMin : screenStartLoc;
                screenFinishLoc = screenFinishLoc.X > screenFinishLocMax.X ? screenFinishLocMax : screenFinishLoc;
                float boxWidth = screenFinishLoc.X - screenStartLoc.X;
                
                //determine actual text to be printed. must be done here, so it can reflect change in radix selection
                string screenText = "MAIN_ERR";
                if (decoderOutput is DecoderOutputEvent)
                {
                    screenText = decoderOutput.Text;
                }
                else
                {
                    if (decoderOutput is DecoderOutputValueNumeric)
                    {
                        DecoderOutputValueNumeric dov = (DecoderOutputValueNumeric)decoderOutput;

                        ProtocolDecoderChannel pdChannel = (ProtocolDecoderChannel)this.channel;

                        //watch out: if Text is "" the Substring(0,1) will cause a crash!
                        string ParseValue = RadixPrinter.Print(dov, pdChannel.RadixType);

                        if (decoderOutput.Text != null && decoderOutput.Text.Length > 0)
                        {
                            screenText = decoderOutput.Text + ": " + ParseValue;

                            //if full message is too large: truncate
                            if (font.MeasureString(screenText).X > boxWidth)
                                screenText = decoderOutput.Text.Substring(0, 1) + ":" + ParseValue;

                            //if still too large: show value only
                            if (font.MeasureString(screenText).X > boxWidth)
                                screenText = ParseValue;
                        }
                        else
                            screenText = ParseValue;
                    }
                    else if (decoderOutput is DecoderOutputValue<string>)
                    {
                        DecoderOutputValue<string> dov = (DecoderOutputValue<string>)decoderOutput;

                        ProtocolDecoderChannel pdChannel = (ProtocolDecoderChannel)this.channel;

                        //watch out: if Text is "" the Substring(0,1) will cause a crash!
                        string ParseValue = dov.Value;

                        if (decoderOutput.Text != null && decoderOutput.Text.Length > 0)
                        {
                            screenText = decoderOutput.Text + ": " + ParseValue;

                            //if full message is too large: truncate
                            if (font.MeasureString(screenText).X > boxWidth)
                                screenText = decoderOutput.Text.Substring(0, 1) + ":" + ParseValue;

                            //if still too large: show value only
                            if (font.MeasureString(screenText).X > boxWidth)
                                screenText = ParseValue;
                        }
                        else
                            screenText = ParseValue;
                    }
                    else
                    {
                        screenText = "TYPE_ERR";
                    }
                }                                

                //only draw text if it fits inside box
                Vector2 wordSize = font.MeasureString(screenText);
                if (wordSize.X < boxWidth)
                {
                    Vector3 byteCenterPosition = (screenStartLoc + screenFinishLoc) / 2.0f;
                    Vector2 textPosition = new Vector2((int)(byteCenterPosition.X - wordSize.X / 2.0f), (int)(byteCenterPosition.Y - wordSize.Y / 2.0f));
                    spriteBatch.DrawString(font, screenText, textPosition, MappedColor.FontDecoder.C());
                }
            }
            
            spriteBatch.End();
        }        
    }
}
