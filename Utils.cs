using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.DataSources;
using System.IO;
using LabNation.DeviceInterface.Devices;
using LabNation.DeviceInterface.Hardware;

namespace ESuite
{
    public class GridDivision {
        public GridDivision(double divisions, double divisionRange, double offset = 0)
        {
            this.Divisions = divisions;
            this.DivisionRange = divisionRange;
            this.Offset = offset;
        }
        public double Divisions { get; private set; }
        public double DivisionRange {get; private set; }
        public double FullRange { get { return Divisions * DivisionRange; } }
        public double Offset { get; internal set; }
    }

    static class Utils
    {
        public static float TimeOffset = 0;
        public static float VolageOffset = 0;
        public static float SampleFreq = 100000000;
        public static float FirstSampleTime = 0;

        public static Matrix RectangleToMatrix(Rectangle rectangle, GraphicsDevice device, Matrix world, Matrix view, Matrix projection)
        {
            return RectangleToMatrix(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, device, world, view, projection);
        }
        public static Matrix RectangleToMatrix(int topLeftX, int topLeftY, int width, int height, GraphicsDevice device, Matrix world, Matrix view, Matrix projection)
        {
            Rectangle boundaries = ScreenBoundaries(device, world, view, projection);
            Vector3 unprojected = device.Viewport.Unproject(new Vector3(topLeftX, topLeftY, 1), projection, view, world);
            if(float.IsNaN(unprojected.Length()) || boundaries.Width == 0 || boundaries.Height == 0)
                return new Matrix();
            Matrix matrix = Matrix.CreateScale((float)width / (float)boundaries.Width, (float)height / (float)boundaries.Height, 1) * Matrix.CreateTranslation(unprojected);
            return matrix;
        }

        public static bool IsFirstRun()
        {
            if (Settings.Current.firstRun.Value)
            {
                Settings.Current.firstRun = false;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static Matrix RemoveTranslation(Matrix inMat)
        {
            inMat.M41 = 0;
            inMat.M42 = 0;
            inMat.M43 = 0;
            return inMat;
        }

        public static Rectangle ScreenBoundaries(GraphicsDevice device, Matrix world, Matrix view, Matrix projection)
        {
            Vector3 topLeft = device.Viewport.Project(new Vector3(0f, 0f, 0), projection, view, world);
            Vector3 botRight = device.Viewport.Project(new Vector3(1f, 1f, 0), projection, view, world);

            return new Rectangle((int)(Math.Round(topLeft.X)), (int)(Math.Round(topLeft.Y)), (int)(Math.Round(botRight.X - topLeft.X)), (int)(Math.Round(botRight.Y - topLeft.Y)));
        }

        public static void DrawRectangle(GraphicsDevice device, BasicEffect effect, Rectangle rect, Color color)
        {
            Vector3 pcp1 = device.Viewport.Unproject(new Vector3(rect.X, rect.Y, 1), Matrix.Identity, Matrix.Identity, Matrix.Identity);
            Vector3 pcp2 = device.Viewport.Unproject(new Vector3(rect.Right, rect.Bottom, 1), Matrix.Identity, Matrix.Identity, Matrix.Identity);

            List<VertexPositionColor> rectVertList = new List<VertexPositionColor>();

            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp1.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp2.X, pcp1.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp2.X, pcp2.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp2.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp1.X, pcp1.Y, 0), color));

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.Identity;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineStrip, rectVertList.ToArray(), 0, rectVertList.Count - 1);
            }
        }

        public static bool rectangleIntersectionBelowMinimalTouchSize(Rectangle a, Rectangle b)
        {
            Rectangle r = Rectangle.Intersect(a, b);
            return r.Width < Scaler.MinimalTouchDimension || r.Height < Scaler.MinimalTouchDimension;
        }

        public static void DrawCross(GraphicsDevice device, BasicEffect effect, Vector2 screenPos, Color color)
        {
            Vector3 pcp = device.Viewport.Unproject(new Vector3(screenPos, 1), Matrix.Identity, Matrix.Identity, Matrix.Identity);

            List<VertexPositionColor> rectVertList = new List<VertexPositionColor>();

            rectVertList.Add(new VertexPositionColor(new Vector3(pcp.X - 0.2f, pcp.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp.X + 0.2f, pcp.Y, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp.X, pcp.Y - 0.2f, 0), color));
            rectVertList.Add(new VertexPositionColor(new Vector3(pcp.X, pcp.Y + 0.2f, 0), color));

            effect.World = Matrix.Identity;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.Identity;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineList, rectVertList.ToArray(), 0, rectVertList.Count - 1);
            }
        }

        static public IHardwareInterface PreferredDevice(List<IHardwareInterface> connectedList, IHardwareInterface currentlyActiveInterface)
        {
            //start with dummyscope as preference
            IHardwareInterface preferredSourceType;
            //if audioscope is available: switch to audioscope as preference
            preferredSourceType = connectedList.Where(x => x is SmartScopeInterfaceUsb).FirstOrDefault();
            if(preferredSourceType == null)
                preferredSourceType = connectedList.Where(x => x is SmartScopeInterfaceEthernet).FirstOrDefault();
            if (preferredSourceType == null)
                if (connectedList.Where(x => x.Serial == DummyInterface.Audio).Count() > 0)
                    preferredSourceType = connectedList.Where(x => x.Serial == DummyInterface.Audio).First();

            //if currently active device is of prefered type -> keep it
            if (connectedList.Contains(currentlyActiveInterface) && preferredSourceType.GetType() == currentlyActiveInterface.GetType())
                return currentlyActiveInterface;

            if (preferredSourceType == null)
                return connectedList.FirstOrDefault();

            //else, return serial of interface which should be connected to
            return preferredSourceType;
        }

        public static Point VectorToPoint(Vector2 v)
        {
            return new Point((int)v.X, (int)v.Y);
        }

        public static Vector2 PointToVector(Point p)
        {
            return new Vector2(p.X, p.Y);
        }

        public static byte byteFromBitOrder(byte input, int[] busBitOrder)
        {
            int output = 0;
            for (int i = 0; i < busBitOrder.Length; i++)
            {
                byte mask = (byte)(0x01 << i);

                output += (input >> (busBitOrder[i] - i)) & (0x01 << i);
            }
            return (byte)output;
        }

        public static string GenerateFilename(string filename, string extension)
        {
            // Generate filename if none is given
            int i = 0;
            string fileName;
            do
            {
                fileName = Path.Combine(LabNation.Common.Utils.StoragePath, filename + i.ToString() + extension);
                ++i;
            } while (File.Exists(fileName));
            return fileName;
        }

        public static string GenerateUniqueNumberedFilename(string pattern, List<string> existing, string extension = null)
        {
            if(extension == null)
                extension = Path.GetExtension(pattern);
            string filenameStripped = pattern.Substring(0, pattern.Length - extension.Length);
            List<string> filtered = existing.Where(x => x.EndsWith(extension)).Select(x => x.Substring(0, x.Length - extension.Length)).ToList();

            string generatedName = GenerateUniqueNumberedString(filenameStripped, filtered);
            return generatedName + extension;
        }

        public static string GenerateUniqueNumberedString(string prefix, List<string> existing)
        {
            string result;
            int i = 0;
            do
            {
                result = prefix + i.ToString();
                i++;
            } while (existing.Contains(result));
            return result;
        }

        /// <summary>
        /// Finds the per-division magnitude, adjusting the by-ref arguments to
        /// what was found
        /// </summary>
        /// <param name="fullRange">full range of the screen</param>
        /// <param name="divisionsMax">the maximum number of divisions on the screen</param>
        /// <returns></returns>
        static public GridDivision divisionRangeFinder(double fullRange, double divisionsMax)
        {
            double divRange = getRoundDivisionRange(fullRange / divisionsMax, RoundDirection.Up);
            return new GridDivision(fullRange / divRange, divRange);
        }

        static public double roundFullRangeFinder(double fullRange, double divisions)
        {
            double divisionRange = getRoundDivisionRange(fullRange / divisions, RoundDirection.Up);
            return divisions * divisionRange;
        }

        static public double roundToNextMultiple(double value, double multiple)
        {
            return multiple * Math.Ceiling(Math.Abs(value) / multiple)* (double)SignNaNSafe(value);
        }

        static public double roundToPreviousMultiple(double value, double multiple)
        {
            return multiple * Math.Floor(Math.Abs(value) / multiple) * (double)SignNaNSafe(value);
        }

        static public int SignNaNSafe(double value)
        {
            if (value < 0)
                return -1;
            else
                return 1;
        }

        static public double MinNanSafe(IEnumerable<double> values)
        {
            double min = double.MaxValue;
            foreach (double val in values)
                if (!double.IsNaN(val))
                    if (val < min)
                        min = val;

            if (min == double.MaxValue)
                min = 0;

            return min;
        }

        static public double MaxNanSafe(IEnumerable<double> values)
        {
            double max = double.MinValue;
            foreach (double val in values)
                if (!double.IsNaN(val))
                    if (val > max)
                        max = val;

            if (max == double.MinValue)
                max = 0;

            return max;
        }

        public enum RoundDirection { Up, Down };

        static public double getRoundDivisionRange(double divisionMinimalRange, RoundDirection roundDirection)
        {
            //Get most significant decimal of the minimal vertical range
            // i.e. : 200, 20 and 2 all become 2
            if (divisionMinimalRange == 1.0)
                return divisionMinimalRange;
            double mostSignificantDecimal = Math.Ceiling(divisionMinimalRange / Math.Pow(10.0, Math.Floor(Math.Log10(divisionMinimalRange))));

            //Figure out if the division's base is going to be 2, 5 or 10
            //Start with 10
            double divisionBase = 10;
            //If the most significant decimal is smaller than half of the current division base,
            //divide that division base.
            //We pass from 10 -> 5 -> 2 
            while (
                (roundDirection == RoundDirection.Up && mostSignificantDecimal <= Math.Floor(divisionBase / 2.0))
                ||
                (roundDirection == RoundDirection.Down && (mostSignificantDecimal <= divisionBase))
                )
            {
                divisionBase = Math.Floor(divisionBase / 2.0); //becomes 10,5,2
            }

            //Now we find out if, for e.g. 2, if we're having a 2, 20 or 200, the so-called "tens"
            double orderOfThousand = Math.Floor(Math.Log(divisionMinimalRange, 1000.0));
            int tens = (int)Math.Floor(Math.Log10(divisionMinimalRange / Math.Pow(1000, orderOfThousand)));

            //Now we just put it all together
            double divisionRange = divisionBase * Math.Pow(10, tens + 3 * orderOfThousand);
            return divisionRange;
        }

        internal static float SincReconstruct(float t, float T_s, float[] p)
        {
            int N = p.Length;
            float result = 0;
            for (int i = 0; i < N; i++)
            {
                float z = (MathHelper.Pi * (t / T_s - i));
                result += (float)(p[i] * Math.Sin(z) / z);
            }
            return result;
        }
        /// <summary>
        /// Extends an object as follows:
        /// - If argument is object[], adds newElement to that object[]
        /// - If argument is an object, returns an object[] = { argument, newElement }
        /// - If argument is null, returns newElement
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="newElement"></param>
        /// <returns></returns>
        internal static object extendArgument(object argument, object newElement)
        {
            if (argument == null)
                return newElement;
            if (newElement == null)
                return argument;
            if (argument is object[])
            {
                List<object> r = ((object[])argument).ToList();
                if (newElement is object[])
                {
                    List<object> a = ((object[])newElement).ToList();
                    r.AddRange(a);
                }
                else
                    r.Add(newElement);
                return r.ToArray();
            }
            else
                return new object[] { argument, newElement };
        }

        internal static Dictionary<LabNation.DeviceInterface.Devices.Channel, long> GetActivityOfAllDigitizableChannels(ScopeDataCollection scopeDataCollection)
        {
            if (scopeDataCollection == null) return null;
            Dictionary<LabNation.DeviceInterface.Devices.Channel, long> output = new Dictionary<LabNation.DeviceInterface.Devices.Channel, long>();

            foreach (var kvp in Drawables.Waveform.EnabledWaveforms)
            {
                ChannelData channelData = scopeDataCollection.GetBestData(kvp.Key);
                if (channelData == null) continue;
                bool[] boolArr = null;
                if (GetChannelType(kvp.Key) == typeof(bool))
                    boolArr = (bool[])channelData.array;
                else if (GetChannelType(kvp.Key) == typeof(float))
                    boolArr = LabNation.Common.Utils.Schmitt((float[])channelData.array);

                //skip to next wave in case this wave cannot be converted into booleans
                if (boolArr == null) continue;

                //count transitions
                long transitions = 0;
                for (int i = 1; i < boolArr.Length; i++)
                    if (boolArr[i] != boolArr[i - 1])
                        transitions++;

                output.Add(kvp.Key, transitions);
            }

            return output;
        }

        internal static List<LabNation.DeviceInterface.Devices.Channel> GetEnabledChannelsOfRequestedType(Type requestedType)
        {
            List<LabNation.DeviceInterface.Devices.Channel> channels = new List<LabNation.DeviceInterface.Devices.Channel>();
            foreach (var ch in Drawables.Waveform.EnabledWaveforms)
                if (GetChannelType(ch.Key) == requestedType && !(ch.Key is DataProcessors.FFTChannel))
                    channels.Add(ch.Key);

            return channels;
        }

        //FIXME: datatype of each channel should be stored as property within channel, not through a dirty method like this
        internal static Type GetChannelType(LabNation.DeviceInterface.Devices.Channel ch)
        {
            if (ch is DataProcessors.ProtocolDecoderChannel)
                return typeof(LabNation.Interfaces.DecoderOutput);
            else if (ch is LabNation.DeviceInterface.Devices.DigitalChannel)
                return typeof(bool);
            else if (ch is ESuite.DataProcessors.OperatorDigitalChannel)
                return typeof(bool);

            return typeof(float);
        }

        internal static Channel Next(this List<Channel> availableChannels, Channel selectedChannel, int jump)
        {
            if (availableChannels.Count == 0)
                return null;
            availableChannels.Sort(Channel.CompareByOrder);
            //FIXME: cover for jumps that are larger in absval than available channels.count
            int nextChannelIndex = (availableChannels.IndexOf(selectedChannel) + availableChannels.Count + jump) % availableChannels.Count;
            return availableChannels[nextChannelIndex];
        }
    }    
}
