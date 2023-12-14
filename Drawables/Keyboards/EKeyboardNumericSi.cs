using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class EKeyboardNumericSi : EKeyboardNumeric
    {
        private Dictionary<NumPadKey, string> PowerPrefixMap = new Dictionary<NumPadKey, string>()
        {
            { NumPadKey.Pico,  "p" },
            { NumPadKey.Nano,  "n" },
            { NumPadKey.Micro, "µ" },
            { NumPadKey.Milli, "m" },
            { NumPadKey.Unit,   "" },
            { NumPadKey.Kilo,  "k" },
            { NumPadKey.Mega,  "M" },
            { NumPadKey.Giga,  "G" },
            { NumPadKey.Tera,  "T" },
        };

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

                return LabNation.Common.Utils.precisionRound(inputNumberParse * Math.Pow(10, inputPowerOfTen), this.precision);
            }
            set
            {
                sign = value == 0 ? 1 : Math.Sign(value);
                double input = Math.Abs(LabNation.Common.Utils.precisionRound(value, this.precision));

                int scale = 0;
                //Multiply by 10 until there's 3 digits before the comma
                while (input * 1e3 < 1e3 && input != 0.0)
                {
                    scale -= 3;
                    input *= 1e3;
                }
                //Divide by 10 until we have at most 3 places before the comma
                while (input > 1e3 && !double.IsInfinity(input))
                {
                    scale += 3;
                    input /= 1e3;
                }

                inputFieldValue = input.ToString();
                inputPowerOfTen = scale;
            }
        }

        private NumPadKey minPowerNumPadKey, maxPowerNumPadKey;
        private int _inputPowerOfTen = 0;
        private int inputPowerOfTen
        {
            get { return _inputPowerOfTen; }
            set
            {
                _inputPowerOfTen = value;
                UpdateInputField();
            }
        }

        internal EKeyboardNumericSi(double minimum, double maximum, double precision, string prefix, string suffix, DrawableCallbackDelegate selfDestructCallback) : base(minimum, maximum, precision, prefix, suffix, selfDestructCallback)
        {
        }

        protected override string ConvertValueToString(double value)
        {
            return ((sign < 0) ? "-" : "") + inputFieldValue + " " + LabNation.Common.Utils.siPrefix(Math.Pow(10, inputPowerOfTen), 0, this.suffix);
        }

        protected override KeyDef[,] DefineKeys()
        {
            KeyDef[,] keyDefs = new KeyDef[,] {
                { new KeyDef(NumPadKey.Clear,  "c"),    new KeyDef(NumPadKey.PlusMinus, "±"),   new KeyDef(NumPadKey.Comma,     "."), new KeyDef(NumPadKey.Confirm,   "dummy") },
                { new KeyDef(NumPadKey.N7,     "7"),    new KeyDef(NumPadKey.N8,        "8"),   new KeyDef(NumPadKey.N9,        "9"), new KeyDef(NumPadKey.Confirm,   "dummy") },
                { new KeyDef(NumPadKey.N4,     "4"),    new KeyDef(NumPadKey.N5,        "5"),   new KeyDef(NumPadKey.N6,        "6"), new KeyDef(NumPadKey.Confirm,   "dummy") },
                { new KeyDef(NumPadKey.N1,     "1"),    new KeyDef(NumPadKey.N2,        "2"),   new KeyDef(NumPadKey.N3,        "3"), new KeyDef(NumPadKey.Confirm,   "dummy") },
                { new KeyDef(NumPadKey.Esc,   "Esc"),    new KeyDef(NumPadKey.N0,        "0"),  new KeyDef(NumPadKey.Confirm,   "OK"), new KeyDef(NumPadKey.Confirm,   "dummy") },
            };

            //Find range of SI scalers
            int minPower = (int)NumPadKey.Unit + (int)Math.Floor(Math.Log10(this.minimum) / 3.0);
            int minPowerPrecision = (int)NumPadKey.Unit + (int)Math.Floor(Math.Log10(this.precision) / 3.0);
            if (minPowerPrecision > minPower) minPower = minPowerPrecision;
            int maxPower = (int)NumPadKey.Unit + (int)Math.Floor(Math.Log10(this.maximum) / 3.0);

            if (minPower < (int)NumPadKey.Pico)
                minPowerNumPadKey = NumPadKey.Pico;
            else if (minPower > (int)NumPadKey.Tera)
                minPowerNumPadKey = NumPadKey.Tera;
            else
                minPowerNumPadKey = (NumPadKey)minPower;

            if (maxPower < (int)NumPadKey.Pico)
                maxPowerNumPadKey = NumPadKey.Pico;
            else if (maxPower > (int)NumPadKey.Tera)
                maxPowerNumPadKey = NumPadKey.Tera;
            else
                maxPowerNumPadKey = (NumPadKey)maxPower;

            NumPadKey currentPower = minPowerNumPadKey;
            for (int y = 0; y < keyDefs.GetLength(0); y++)
            {
                KeyDef def = keyDefs[y, keyDefs.GetLength(1) - 1];
                if (def == null) continue;
                if (currentPower > maxPowerNumPadKey)
                {
                    def.key = NumPadKey.Unused;
                    def.text = "";
                    continue;
                }
                def.key = currentPower;
                def.text = PowerPrefixMap[currentPower] + this.suffix;
                currentPower++;
            }

            return keyDefs;
        }

        public override void OnKeyPressed(NumPadKey k)  //called when key is pressed on keyboard, or when tapped in numpad
        {
            base.OnKeyPressed(k);

            if (k.isScaler())       // k >= NumPadKey.Pico && k <= NumPadKey.Tera;
            {
                if (k < minPowerNumPadKey) return;
                if (k > maxPowerNumPadKey) return;
                inputPowerOfTen = (((int)k) - 100) * 3;
                return;
            }
        }
    }
}
