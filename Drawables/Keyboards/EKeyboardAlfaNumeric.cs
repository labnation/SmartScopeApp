using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal class EKeyboardAlfaNumeric : EKeyboardAlfa
    {
        private enum KeyboardView
        {
            Caps,
            Lower,
            Special
        }
        private KeyboardView currentView = KeyboardView.Lower;

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

        internal EKeyboardAlfaNumeric(string prefix, string suffix, int maxNbrChars, DrawableCallbackDelegate selfDestructCallback) : base(prefix, suffix, selfDestructCallback, maxNbrChars)
        {
            this.MAX_NBR_CHARS = maxNbrChars;
        }

        private KeyDef[,] lowerKeys = new KeyDef[,] {
                { new KeyDef(NumPadKey.ClearAll,  "Clr"),    new KeyDef(NumPadKey.Clear, "Del"),   new KeyDef(NumPadKey.Confirm,   "OK"),   new KeyDef(NumPadKey.Esc,   "Esc"),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "), new KeyDef(NumPadKey.Caps,   "CAPS"),   new KeyDef(NumPadKey.Lower,   "lower"), new KeyDef(NumPadKey.Special,   "-+#"), new KeyDef(NumPadKey.Void,   " "), new KeyDef(NumPadKey.N7,   "7"), new KeyDef(NumPadKey.N8,   "8"), new KeyDef(NumPadKey.N9,   "9") },
                { new KeyDef(NumPadKey.Q,  "q"),    new KeyDef(NumPadKey.W, "w"),   new KeyDef(NumPadKey.E,   "e"),   new KeyDef(NumPadKey.R,   "r"),   new KeyDef(NumPadKey.T,   "t"),   new KeyDef(NumPadKey.Y,   "y"),   new KeyDef(NumPadKey.U,   "u"),   new KeyDef(NumPadKey.I,   "i"),   new KeyDef(NumPadKey.O,   "o"),   new KeyDef(NumPadKey.P,   "p"), new KeyDef(NumPadKey.Void,   "x"), new KeyDef(NumPadKey.N4,   "4") , new KeyDef(NumPadKey.N5,   "5"), new KeyDef(NumPadKey.N6,   "6") },
                { new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.A,     "a"),    new KeyDef(NumPadKey.S,        "s"),   new KeyDef(NumPadKey.D,        "d"),   new KeyDef(NumPadKey.F,   "f"),   new KeyDef(NumPadKey.G,   "g"),   new KeyDef(NumPadKey.H,   "h"),   new KeyDef(NumPadKey.J,   "j"),   new KeyDef(NumPadKey.K,   "k"),   new KeyDef(NumPadKey.L,   "l"), new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.N1,   "1")  , new KeyDef(NumPadKey.N2,   "2"), new KeyDef(NumPadKey.N3,   "3")},
                { new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Z,     "z"),    new KeyDef(NumPadKey.X,        "x"),   new KeyDef(NumPadKey.C,        "c") ,   new KeyDef(NumPadKey.V,   "v"),   new KeyDef(NumPadKey.B,   "b"),   new KeyDef(NumPadKey.N,   "n"),   new KeyDef(NumPadKey.M,   "m"),   new KeyDef(NumPadKey.Spacer,   "x"),   new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Void,   "x"), new KeyDef(NumPadKey.N0,   "0"), new KeyDef(NumPadKey.N0,   " "), new KeyDef(NumPadKey.Confirm,   "OK") },
            };

        private KeyDef[,] capsKeys = new KeyDef[,] {
                    { new KeyDef(NumPadKey.ClearAll,  "Clr"),    new KeyDef(NumPadKey.Clear, "Del"),   new KeyDef(NumPadKey.Confirm,   "OK"),   new KeyDef(NumPadKey.Esc,   "Esc"),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "), new KeyDef(NumPadKey.Caps,   "CAPS"),   new KeyDef(NumPadKey.Lower,   "lower"), new KeyDef(NumPadKey.Special,   "-+#"), new KeyDef(NumPadKey.Void,   " "), new KeyDef(NumPadKey.N7,   "7"), new KeyDef(NumPadKey.N8,   "8"), new KeyDef(NumPadKey.N9,   "9") },
                    { new KeyDef(NumPadKey.Q,  "Q"),    new KeyDef(NumPadKey.W, "W"),   new KeyDef(NumPadKey.E,   "E"),   new KeyDef(NumPadKey.R,   "R"),   new KeyDef(NumPadKey.T,   "T"),   new KeyDef(NumPadKey.Y,   "Y"),   new KeyDef(NumPadKey.U,   "U"),   new KeyDef(NumPadKey.I,   "I"),   new KeyDef(NumPadKey.O,   "O"),   new KeyDef(NumPadKey.P,   "P"), new KeyDef(NumPadKey.Void,   "X"), new KeyDef(NumPadKey.N4,   "4") , new KeyDef(NumPadKey.N5,   "5"), new KeyDef(NumPadKey.N6,   "6") },
                    { new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.A,     "A"),    new KeyDef(NumPadKey.S,        "S"),   new KeyDef(NumPadKey.D,        "D"),   new KeyDef(NumPadKey.F,   "F"),   new KeyDef(NumPadKey.G,   "G"),   new KeyDef(NumPadKey.H,   "H"),   new KeyDef(NumPadKey.J,   "J"),   new KeyDef(NumPadKey.K,   "K"),   new KeyDef(NumPadKey.L,   "L"), new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.N1,   "1")  , new KeyDef(NumPadKey.N2,   "2"), new KeyDef(NumPadKey.N3,   "3")},
                    { new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Z,     "Z"),    new KeyDef(NumPadKey.X,        "X"),   new KeyDef(NumPadKey.C,        "C") ,   new KeyDef(NumPadKey.V,   "V"),   new KeyDef(NumPadKey.B,   "B"),   new KeyDef(NumPadKey.N,   "N"),   new KeyDef(NumPadKey.M,   "M"),   new KeyDef(NumPadKey.Spacer,   "x"),   new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Void,   "x"), new KeyDef(NumPadKey.N0,   "0"), new KeyDef(NumPadKey.N0,   " "), new KeyDef(NumPadKey.Confirm,   "OK") },
                };

        private KeyDef[,] specialKeys = new KeyDef[,] {
                    { new KeyDef(NumPadKey.ClearAll,  "Clr"),    new KeyDef(NumPadKey.Clear, "Del"),   new KeyDef(NumPadKey.Confirm,   "OK"),   new KeyDef(NumPadKey.Esc,   "Esc"),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "),   new KeyDef(NumPadKey.X,   " "), new KeyDef(NumPadKey.Caps,   "CAPS"),   new KeyDef(NumPadKey.Lower,   "lower"), new KeyDef(NumPadKey.Special,   "-+#"), new KeyDef(NumPadKey.Void,   " "), new KeyDef(NumPadKey.N7,   "7"), new KeyDef(NumPadKey.N8,   "8"), new KeyDef(NumPadKey.N9,   "9") },
                    { new KeyDef(NumPadKey.Backslash,  "\\"),    new KeyDef(NumPadKey.CloseBrackets, ")"),   new KeyDef(NumPadKey.OpenBrackets,   "("),   new KeyDef(NumPadKey.Pipe,   "|"),   new KeyDef(NumPadKey.Plus,   "+"),   new KeyDef(NumPadKey.Minus,   "-"),   new KeyDef(NumPadKey.Quotes,   "'"),   new KeyDef(NumPadKey.Semicolon,   ";"),   new KeyDef(NumPadKey.Tilde,   "~"),   new KeyDef(NumPadKey.At,   "@"), new KeyDef(NumPadKey.Void,   "X"), new KeyDef(NumPadKey.N4,   "4") , new KeyDef(NumPadKey.N5,   "5"), new KeyDef(NumPadKey.N6,   "6") },
                    { new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.Hat,     "^"),    new KeyDef(NumPadKey.Exclamation,        "!"),   new KeyDef(NumPadKey.Pound,        "#"),   new KeyDef(NumPadKey.Dollar,   "$"),   new KeyDef(NumPadKey.Ampersand,   "&"),   new KeyDef(NumPadKey.Star,   "*"),   new KeyDef(NumPadKey.Slash,   "/"),   new KeyDef(NumPadKey.Percentage,   "%"),   new KeyDef(NumPadKey.Question,   "?"), new KeyDef(NumPadKey.HalfSpacer,   "x"), new KeyDef(NumPadKey.N1,   "1")  , new KeyDef(NumPadKey.N2,   "2"), new KeyDef(NumPadKey.N3,   "3")},
                    { new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Spacebar,     " "),    new KeyDef(NumPadKey.Spacebar,        " "),   new KeyDef(NumPadKey.Spacebar,        " ") ,   new KeyDef(NumPadKey.Spacebar,   " "),   new KeyDef(NumPadKey.Spacebar,   " "),   new KeyDef(NumPadKey.Spacebar,   " "),   new KeyDef(NumPadKey.Period,   "."),   new KeyDef(NumPadKey.Spacer,   "x"),   new KeyDef(NumPadKey.Spacer,   "x"), new KeyDef(NumPadKey.Void,   "x"), new KeyDef(NumPadKey.N0,   "0"), new KeyDef(NumPadKey.N0,   " "), new KeyDef(NumPadKey.Confirm,   "OK") },
                };

        protected override KeyDef[,] DefineKeys()
        {
            return lowerKeys;
        }

        public override void OnKeyPressed(NumPadKey k)
        {
            //on first input: clear value instead of appending to existing value
            if (firstInput)
            {
                inputFieldValue = clearValue;
                firstInput = false;
            }

            if (k == NumPadKey.Caps)
            {
                this.KeyDefinitions = capsKeys;
                this.Init();
                this.firstInput = false;
                currentView = KeyboardView.Caps;
            }
            else if (k == NumPadKey.Lower)
            {
                this.KeyDefinitions = lowerKeys;
                this.Init();
                this.firstInput = false;
                currentView = KeyboardView.Lower;
            }
            else if (k == NumPadKey.Special)
            {
                this.KeyDefinitions = specialKeys;
                this.Init();
                this.firstInput = false;
                currentView = KeyboardView.Lower;
            }
            else if (k.isLetter())
            {
                if (inputFieldValue.Length >= MAX_NBR_CHARS) return;
                if (currentView == KeyboardView.Caps)
                    inputFieldValue += k.ToString().ToUpper();
                else
                    inputFieldValue += k.ToString().ToLower();
            }
            else if (k.isSpecial())
            {
                if (inputFieldValue.Length >= MAX_NBR_CHARS) return;
                if (k == NumPadKey.Backslash) inputFieldValue += "\\";
                else if (k == NumPadKey.CloseBrackets) inputFieldValue += ")";
                else if (k == NumPadKey.OpenBrackets) inputFieldValue += "(";
                else if (k == NumPadKey.Plus) inputFieldValue += "+";
                else if (k == NumPadKey.Minus) inputFieldValue += "-";
                else if (k == NumPadKey.Pipe) inputFieldValue += "|";
                else if (k == NumPadKey.Question) inputFieldValue += "?";
                else if (k == NumPadKey.Quotes) inputFieldValue += "'";
                else if (k == NumPadKey.Semicolon) inputFieldValue += ";";
                else if (k == NumPadKey.Tilde) inputFieldValue += "~";
                else if (k == NumPadKey.Comma) inputFieldValue += ",";
                else if (k == NumPadKey.Period) inputFieldValue += ".";
                else if (k == NumPadKey.Exclamation) inputFieldValue += "!";
                else if (k == NumPadKey.Pound) inputFieldValue += "#";
                else if (k == NumPadKey.Dollar) inputFieldValue += "$";
                else if (k == NumPadKey.Ampersand) inputFieldValue += "&";
                else if (k == NumPadKey.Star) inputFieldValue += "*";
                else if (k == NumPadKey.Slash) inputFieldValue += "/";
                else if (k == NumPadKey.Percentage) inputFieldValue += "%";
                else if (k == NumPadKey.At) inputFieldValue += "@";
                else if (k == NumPadKey.Hat) inputFieldValue += "^";
                else if (k == NumPadKey.Spacebar) inputFieldValue += " ";
            }
            else
            {
                base.OnKeyPressed(k);
            }
        }
    }
}
