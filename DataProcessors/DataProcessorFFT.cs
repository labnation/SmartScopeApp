using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface.DataSources;
using LabNation.DeviceInterface.Devices;
using System.Numerics;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace ESuite.DataProcessors
{
    enum FFTWindow
    {
        BlackmanHarris,
        Hamming,
        Hann,
        FlatTop,
        KaiserBessel,
        Uniform
    }

    class DataProcessorFFT: IDataProcessor
    {
        public static int MaxDb = -(int)(20*Math.Log10(1f/128f)); //defined by resolution of 8bit scope with 1bit of noise
        private ChannelData lastProcessedChannel;
        public AnalogChannel analogChannel { get; private set; }
        public FFTChannel channel { get; private set; }
        public void Reset() { }
        public bool IsTimeVariant { get { return false; } }

        public DataProcessorFFT(AnalogChannel channel)
        {
            this.channel = new FFTChannel(channel, this);
            this.analogChannel = channel;
            lastProcessedChannel = new ChannelData(ChannelDataSourceScope.Viewport, channel, null, false, 0);
        }
        public void Process(ScopeDataCollection scopeDataCollection)
        {
            if (scopeDataCollection.Rolling) return;
            ChannelData input = scopeDataCollection.GetBestData(analogChannel);
            if (input == null) return;
            if (lastProcessedChannel.Equals(input)) return;
            lastProcessedChannel = input;

            //in case of saturation
            if (scopeDataCollection.Data.SaturationHighValue[analogChannel] == ((float[])input.array).Max() || scopeDataCollection.Data.SaturationLowValue[analogChannel] == ((float[])input.array).Min())
            {
                scopeDataCollection.SetData(ChannelDataSourceScope.Viewport, this.channel, new float[0]);
                return;
            }
            
            //FIXME: this really shouldn't be recalculated each time ...
            //Convert voltages to complex numbers for FFT function
            double[] window;
            switch (Settings.CurrentRuntime.fftWindowType)
            {
                case FFTWindow.BlackmanHarris:
                    window = Window.BlackmanHarris(input.array.Length);
                    break;
                case FFTWindow.FlatTop:
                    window = Window.FlatTop(input.array.Length);
                    break;
                case FFTWindow.Hamming:
                    window = Window.Hamming(input.array.Length);
                    break;
                case FFTWindow.Hann:
                    window = Window.Hann(input.array.Length);
                    break;
                case FFTWindow.Uniform:
                    window = new double[input.array.Length];
                    for (int i = 0; i < window.Length; i++)
                        window[i] = 1;
                    break;
                default:
                    window = Window.BlackmanHarris(input.array.Length);
                    break;
            }

            float windowPower = 0;
            for (int i = 0; i < window.Length; i++)
                windowPower += (float)window[i];
            windowPower /= (float)window.Length;

            Func<float, Complex> floatToComplex = o => (Complex)o;
            Complex[] voltages = new Complex[input.array.Length]; //LabNation.Common.Utils.TransformArray(input.array, floatToComplex);
            float[] inVoltages = (float[])input.array;
            for (int i = 0; i < voltages.Length; i++)
               voltages[i] = new Complex((float)inVoltages[i]*window[i] , 0);
            MathNet.Numerics.IntegralTransforms.Fourier.Forward(voltages, FourierOptions.Matlab);
            
            //Notice here how we only use half of the complex FFT as input (since it's mirrored in magnitude)
            float[] magnitudes = new float[voltages.Length / 2];
            for (int i = 0; i < magnitudes.Length; i++)
                magnitudes[i] = (float)voltages[i].Magnitude;

            //scale lineary between 0 and 1, with 1 being max grid amplitude
            float maxWaveAmplitude = (float)Drawables.Waveform.Waveforms.Single(x => x.Key == analogChannel).Value.VoltageRange;
            float maxMagnitude = maxWaveAmplitude * magnitudes.Length / 2f * windowPower;
            for (int i = 0; i < magnitudes.Length; i++)
                magnitudes[i] = magnitudes[i] /maxMagnitude;

            //log
            if (Settings.CurrentRuntime.fftVoltageScale.Value == LinLog.Logarithmic)
            {
                for (int i = 0; i < magnitudes.Length; i++)
                {
                    magnitudes[i] = 20f * (float)Math.Log10(magnitudes[i]) / (float)MaxDb + 1;
                    if (magnitudes[i] < 0)
                        magnitudes[i] = 0;
                }
            }

            //clamp within 0-1 limits
            for (int i = 0; i < magnitudes.Length; i++)
            {
                if (magnitudes[i] < 0) magnitudes[i] = 0;
                if (magnitudes[i] > 1) magnitudes[i] = 1;
            }

            float maxAmp = magnitudes.Max();
            scopeDataCollection.SetData(ChannelDataSourceScope.Viewport, this.channel, magnitudes);
        }
    }
}
