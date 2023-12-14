using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LabNation.DeviceInterface;

namespace ESuite.Measurements
{
    internal class FrameRateCounter
    {
        private LinkedList<double> frameIntervals = new LinkedList<double>();
        private const int listLength = 15;
        private const long maximumAge = 1500; //Time after which we consider the stream stopped (ms) and GetFrameRate will return 0
        private object frameTimesLock = new object();
        private DateTime time;

        public FrameRateCounter() 
        {
            this.time = DateTime.Now;
        }

        public void IncrementCounter(object sender, EventArgs e)
        {
            //add to linkedList
            DateTime now = DateTime.Now;
            lock (frameTimesLock)
            {
                frameIntervals.AddLast((now - time).TotalMilliseconds);
                this.time = now;
                //Since we only add to the list in this block, it's
                //fine not to loop, but to remove just one element
                if (frameIntervals.Count > listLength)
                    frameIntervals.RemoveFirst();
            }
        }

        public double FrameRate { get { 
            double frameRate = 0.0; 
            lock (frameTimesLock)
            {
                //Start with a very small number that won't influence the count but will avoid division by zero
                double average = 0;
                int i = 0;
                //Iterate backwards through all frameTimes
                LinkedListNode<double> frameInterval = frameIntervals.Last;
                double ago = (DateTime.Now - time).TotalMilliseconds;
                while(frameInterval != null)
                {
                    //Ignore all further frameTimes if we encounter a too old one
                    //or if the stopwatch is still running and more time than the max
                    //age has elapsed
                    if (ago > maximumAge)
                    {
                        break;
                    }
                    i++;
                    average += frameInterval.Value;
                    frameInterval = frameInterval.Previous;
                }

                frameRate = average == 0 ? 0f : (double)i * 1e3 / average;
            }
            return frameRate;
		} }
    }
}
