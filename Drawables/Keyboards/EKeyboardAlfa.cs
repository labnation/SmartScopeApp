using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class EKeyboardAlfa : EKeyboard<string>
    {
        protected override string ConvertValueToString(string value)
        {
            return value;
        }

        protected override bool IsValid(object value)
        {
            return true;
        }

        public override string Value
        {
            get
            {
                return inputFieldValue; //MUSTCHECK : need to strip from pre-suffix?
            }
            set
            {
                inputFieldValue = value;
            }
        }

        internal EKeyboardAlfa(string prefix, string suffix, DrawableCallbackDelegate selfDestructCallback, int maxNbrChars) : base("", prefix, suffix, selfDestructCallback)
        {
            this.MAX_NBR_CHARS = maxNbrChars;
        }

        protected override KeyDef[,] DefineKeys()
        {
            return new KeyDef[,] {
                { new KeyDef(NumPadKey.ClearAll,  "Clr"),    new KeyDef(NumPadKey.Clear, "Del"),   new KeyDef(NumPadKey.Confirm,   "OK"),   new KeyDef(NumPadKey.Esc,   "Esc"),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "), new KeyDef(NumPadKey.Void,   " ") },
                { new KeyDef(NumPadKey.Q,  "q"),    new KeyDef(NumPadKey.W, "w"),   new KeyDef(NumPadKey.E,   "e"),   new KeyDef(NumPadKey.R,   "r"),   new KeyDef(NumPadKey.T,   "t"),   new KeyDef(NumPadKey.Y,   "y"),   new KeyDef(NumPadKey.U,   "u"),   new KeyDef(NumPadKey.I,   "i"),   new KeyDef(NumPadKey.O,   "o"),   new KeyDef(NumPadKey.P,   "p"), new KeyDef(NumPadKey.Void,   "x") },
                { new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.A,     "a"),    new KeyDef(NumPadKey.S,        "s"),   new KeyDef(NumPadKey.D,        "d"),   new KeyDef(NumPadKey.F,   "f"),   new KeyDef(NumPadKey.G,   "g"),   new KeyDef(NumPadKey.H,   "h"),   new KeyDef(NumPadKey.J,   "j"),   new KeyDef(NumPadKey.K,   "k"),   new KeyDef(NumPadKey.L,   "l"), new KeyDef(NumPadKey.HalfSpacer,   "x") },
                { new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Z,     "z"),    new KeyDef(NumPadKey.X,        "x"),   new KeyDef(NumPadKey.C,        "c") ,   new KeyDef(NumPadKey.V,   "v"),   new KeyDef(NumPadKey.B,   "b"),   new KeyDef(NumPadKey.N,   "n"),   new KeyDef(NumPadKey.M,   "m"),   new KeyDef(NumPadKey.Spacer,   "x"),   new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Void,   "x")},
            };
        }
    }
}
