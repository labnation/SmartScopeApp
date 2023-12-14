using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class EKeyboardNumeric : EKeyboard<double>
    {
        protected double minimum = double.MinValue;
        protected double maximum = double.MaxValue;
        protected double precision = double.MinValue;

        private int _sign = 1;
        protected int sign
        {
            get { return _sign; }
            set
            {
                _sign = value > 0 ? 1 : -1;
                UpdateInputField();
            }
        }

        public override double Value
        {
            get
            {
                double inputNumberParse;

                bool success = double.TryParse(inputFieldValue, out inputNumberParse);
                if (!success)
                    success = double.TryParse(inputFieldValue.Replace('.', ','), out inputNumberParse);
                if (!success)
                    throw new Exception("failure trying tot parse numpad input to doubleType");

                inputNumberParse *= sign;

                return LabNation.Common.Utils.precisionRound(inputNumberParse, this.precision);
            }
            set
            {
                sign = value == 0 ? 1 : Math.Sign(value);
                double input = Math.Abs(LabNation.Common.Utils.precisionRound(value, this.precision));

                inputFieldValue = input.ToString();
            }
        }

        protected override string ConvertValueToString(double value)
        {
            return ((sign < 0) ? "-" : "") + inputFieldValue + " " + this.suffix;
        }

        protected override bool IsValid(object value)
        {
            return ((double)value <= maximum && (double)value >= minimum);
        }

        internal EKeyboardNumeric(double minimum, double maximum, double precision, string prefix, string suffix, DrawableCallbackDelegate selfDestructCallback) : base("0", prefix, suffix, selfDestructCallback)
        {
            this.minimum = minimum;
            this.maximum = maximum;
            this.precision = precision;
        }

        protected override KeyDef[,] DefineKeys()
        {
            return new KeyDef[,] {
                { new KeyDef(NumPadKey.Clear,  "c"),    new KeyDef(NumPadKey.PlusMinus, "±"),   new KeyDef(NumPadKey.Comma,     ".") },
                { new KeyDef(NumPadKey.N7,     "7"),    new KeyDef(NumPadKey.N8,        "8"),   new KeyDef(NumPadKey.N9,        "9") },
                { new KeyDef(NumPadKey.N4,     "4"),    new KeyDef(NumPadKey.N5,        "5"),   new KeyDef(NumPadKey.N6,        "6") },
                { new KeyDef(NumPadKey.N1,     "1"),    new KeyDef(NumPadKey.N2,        "2"),   new KeyDef(NumPadKey.N3,        "3") },
                { new KeyDef(NumPadKey.Esc,    "Esc"),    new KeyDef(NumPadKey.N0,        "0"),   new KeyDef(NumPadKey.Confirm,   "OK") },
            };
        }

        public override void OnKeyPressed(NumPadKey k)  //called when key is pressed on keyboard, or when tapped in numpad
        {
            base.OnKeyPressed(k);

            if (k == NumPadKey.PlusMinus)
            {
                sign *= -1;
                return;
            }
            if (k == NumPadKey.Clear)
            {
                this.sign = 1;
                return;
            }
        }
    }
}
