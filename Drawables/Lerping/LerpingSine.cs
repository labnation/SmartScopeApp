using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal static class LerpingSine
    {
        public static float SmoothStopStartSine(float time)
        {
            float sineValue= (float)(Math.Sin(time*Math.PI+6f/4f*Math.PI));
            return sineValue / 2f + 0.5f;
            
        }

        public static float SmoothStartStopPower(float time)
        {
            float tBetweenM1P1 = (time - 0.5f) * 2f;
            if (tBetweenM1P1 >= 0)
                return (float)((1 - Math.Pow(1 - tBetweenM1P1, 4)) / 2f + 0.5f);
            else
                return (float)(Math.Pow(1-Math.Abs(tBetweenM1P1),4)/2f);
        }

        public static float SmoothStopPower(float time)
        {
            return (float)((1 - Math.Pow(1 - time, 4)));
        }

        public static float SmoothStartPower(float time)
        {
            return (float)(Math.Pow(Math.Abs(time), 4));
        }

    }
}
