using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace ESuite.Drawables
{
    internal static class Extensions
    {
        public static bool isScaler(this NumPadKey k) { return k >= NumPadKey.Pico && k <= NumPadKey.Tera; }
        public static bool isNumber(this NumPadKey k) { return k >= NumPadKey.N0 && k <= NumPadKey.N9; }
        public static bool isLetter(this NumPadKey k) { return (k >= NumPadKey.A && k <= NumPadKey.Z); }
        public static bool isOperator(this NumPadKey k) { return k >= NumPadKey.Comma && k <= NumPadKey.Esc; }
        public static bool isSpecial(this NumPadKey k) { return k >= NumPadKey.CloseBrackets && k <= NumPadKey.Minus; }
        public static bool isSpacer(this NumPadKey k) { return k >= NumPadKey.Spacer && k <= NumPadKey.Void; }
    }

    public enum NumPadKey   //numbered, so checks above can easily figure out type of entered key
    {
        Unused = -1,

        N0 = 0,
        N1 = 1,
        N2 = 2,
        N3 = 3,
        N4 = 4,
        N5 = 5,
        N6 = 6,
        N7 = 7,
        N8 = 8,
        N9 = 9,

        Comma = 50,
        Period = 51,
        Clear = 52,
        ClearAll = 53,
        PlusMinus = 54,
        Confirm = 55,
        Esc = 56,        

        CloseBrackets = 57,
        OpenBrackets = 58,
        Pipe = 59,
        Plus = 60,
        Question = 61,
        Quotes = 62,
        Semicolon = 63,
        Tilde = 64,        
        Exclamation = 65,
        Pound = 66,
        Dollar = 67,
        Ampersand = 68,
        Star = 69,
        Slash = 70,
        Percentage = 71,
        At = 72,
        Hat = 73,
        Spacebar = 74,
        Backslash = 75,
        Minus = 76,

        Pico = 100 - 4,
        Nano = 100 - 3,
        Micro = 100 - 2,
        Milli = 100 - 1,
        Unit = 100,
        Kilo = 100 + 1,
        Mega = 100 + 2,
        Giga = 100 + 3,
        Tera = 100 + 4,

        A = 200,
        B = 201,
        C = 202,
        D = 203,
        E = 204,
        F = 205,
        G = 206,
        H = 207,
        I = 208,
        J = 209,
        K = 210,
        L = 211,
        M = 212,
        N = 213,
        O = 214,
        P = 215,
        Q = 216,
        R = 217,
        S = 218,
        T = 219,
        U = 220,
        V = 221,
        W = 222,
        X = 223,
        Y = 224,
        Z = 225,

        Spacer = 500,
        HalfSpacer = 501,
        Void = 502, //takes zero space, needed because entryMatrix needs to be 2D
        Caps = 503,
        Lower = 504,
        Special = 505
    }

    internal class EFloater : EDrawable //wrapper method to take the non-generic functionality out of the EKeyboard. Otherwise the correct type had to be specified when moving the keyboard, which is not known when the keyboard is passed as simple EDrawable
    {
        private Rectangle _numpadRectangle;
        private int width;
        protected int Width
        {
            set
            {
                this.width = value;
                OnBoundariesChanged();
            }
        }
        private int height;
        protected int Height
        {
            set
            {
                this.height = value;
                OnBoundariesChanged();
            }
        }

        public Rectangle floaterRectangle
        {
            get { return _numpadRectangle; }
            set
            {
                //verify box is not outside of window
                _numpadRectangle = value;
                if (_numpadRectangle.Right > Boundaries.Right)
                    _numpadRectangle.X = Boundaries.Right - _numpadRectangle.Width;
                if (_numpadRectangle.Left < Boundaries.Left)
                    _numpadRectangle.X = Boundaries.Left;
                if (_numpadRectangle.Bottom > Boundaries.Bottom)
                    _numpadRectangle.Y = Boundaries.Bottom - _numpadRectangle.Height;
                if (_numpadRectangle.Top < Boundaries.Top)
                    _numpadRectangle.Y = Boundaries.Top;
            }
        }

        internal EFloater()
        {
        }

        protected override void LoadContentInternal()
        {
        }

        protected override void OnBoundariesChangedInternal()
        {
            floaterRectangle = new Rectangle(floaterRectangle.X, floaterRectangle.Y, width, height);  //auto ensures numpad stays within boundaries

            foreach (EDrawable child in children)
                child.SetBoundaries(floaterRectangle);
        }

        protected override void DrawInternal(GameTime time)
        {
        }
    }

    //required to get around generic type casting
    internal interface IKeyboard
    {
        void OnKeyPressed(NumPadKey k);
    }

    internal abstract class EKeyboard<T> : EFloater, IKeyboard
    {
        private string COMMA = "?";
        private const float fontSize = 14f;
        protected bool firstInput = true;
        protected string prefix = "";
        protected string suffix = "";
        protected string clearValue = "";

        public class KeyDef
        {
            public NumPadKey key;
            public string text;
            public KeyDef(NumPadKey key, string text)
            {
                this.key = key;
                this.text = text;
            }
        }

        protected KeyDef[,] KeyDefinitions;

        public DrawableCallback DragCallback, DropCallback;
        public DrawableCallback OnValueEntered;
        Rectangle numpadRect;
        public override Point? Size
        {
            get
            {
                return new Point(numpadRect.Width, numpadRect.Height);
            }
        }
        int rows { get { return KeyDefinitions.GetLength(0) + 1; } }
        int cols {
            get {
                int cols = KeyDefinitions.GetLength(1);
                return cols;//MUSTCHECK useSiScalers ? cols : cols - 1;
            }
        }

        public abstract T Value { get; set; }
        protected abstract bool IsValid(object value);
        protected abstract KeyDef[,] DefineKeys();
        private DrawableCallbackDelegate selfDestructDelegate;

        float btnSize = .7f; //1 inch btns
        float totalWidth = 0;
        Stack numpadStack;

        protected int MAX_NBR_CHARS = Scaler.DefaultMaxNbrChars;
        
        private string _inputNumber;
        protected string inputFieldValue
        {
            get { return _inputNumber; }
            set
            {
                _inputNumber = value;
                UpdateInputField();
            }
        }        

        EButtonImageAndTextSelectable inputField;

        public EKeyboard(string clearValue, string prefix, string suffix, DrawableCallbackDelegate selfDestructDelegate) : base()
        {
            /*
             * ,-----------------. ------
             * |     123,456 kHz |  
             * |-----------------| ------
             * | 1 | 2 | 3 | kHz |
             * |-----------------|
             * | 4 | 5 | 6 | MHz |
             * |-----------------|
             * | 7 | 8 | 9 | GHz |
             * |-----------------|
             * |   0   | , | THz |
             * `-----------------'  -----
             */
            this.selfDestructDelegate = selfDestructDelegate;
            this.KeyDefinitions = DefineKeys();
            this.clearValue = clearValue;
            this._inputNumber = clearValue;
            this.prefix = prefix;
            this.suffix = suffix;
            this.COMMA = Convert.ToString(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            Init();
            LoadContent();
        }

        protected void Init()
        {
            if (children.Contains(numpadStack))
                RemoveChild(numpadStack);

            GenerateKeypad();
            UpdateInputField();
            this.AddChild(numpadStack);
            this.firstInput = true;
        }

        protected override void OnBoundariesChangedInternal()
        {
            while (Boundaries.Width > 0 && totalWidth.InchesToPixels() >= Boundaries.Width)
            {
                btnSize /= 1.5f;
                Init();
            }

            base.OnBoundariesChangedInternal();           
        }

        protected void GenerateKeypad()
        {
            SplitPanel numpad = new SplitPanel(Orientation.Vertical, rows, SizeType.Inches);

            //first figure out total width. depends on whether keyboard contains Spacer keys
            totalWidth = 0;
            for (int x = 0; x < cols; x++)
            {
                KeyDef def = KeyDefinitions[0, x];

                float btnWidth = btnSize;
                if (def.key == NumPadKey.HalfSpacer) btnWidth = btnSize / 2f;
                if (def.key == NumPadKey.Void) btnWidth = 0;

                totalWidth += btnWidth;
            }

            //small trick to ensure fullWidth corresponds to even number of pixels; so we know 2 halfSpacers equal 1 fullWidth
            int pixelsEven = (btnSize.InchesToPixels() >> 1) << 1;
            btnSize = pixelsEven.PixelsToInches();

            //update size of floater; needed when it is dragged around
            int btnSizePx = btnSize.InchesToPixels()/2*2;
            this.Width = totalWidth.InchesToPixels(); ;
            this.Height = rows * btnSizePx;

            //inputfield on top
            SplitPanel inputLine = new SplitPanel(Orientation.Horizontal, 2, SizeType.Inches);
            inputLine.SetPanelSize(0, totalWidth);
            inputField = new EButtonImageAndTextSelectable(
                        null, MappedColor.NumPadInputBackground,
                        null, MappedColor.NumPadInputBackgroundError,
                        "", MappedColor.NumPadInputForeground,
                        null, MappedColor.NumPadInputForegroundError,
                        false, true,
                        Location.Center, Location.Right, null, null, Location.Center)
                        {
                            StretchTexture = true,
                            DragCallback = (DrawableCallback)this.OnButtonDrag,
                            DropCallback = (DrawableCallback)this.OnButtonDrop,
                            FontSize = fontSize,
                            TextPosition = Location.Right
                        };
            inputLine.SetPanel(0, inputField);
            numpad.SetPanel(0, inputLine);

            //all keys as defined in KeyDefinitions
            for (int y = 0; y < rows - 1; y++)
            {
                SplitPanel s = new SplitPanel(Orientation.Horizontal, cols, SizeType.Inches);
                for (int x = 0; x < cols; x++)
                {
                    KeyDef def = KeyDefinitions[y, x];
                    bool isNumberOrOperator = def.key.isNumber() || def.key.isOperator();
                    if (def == null) continue;

                    float btnWidth = btnSize;
                    if (def.key == NumPadKey.HalfSpacer) btnWidth = btnSize / 2f;
                    if (def.key == NumPadKey.Void) btnWidth = 0;

                    s.SetPanelSize(x, btnWidth);
                    if (!def.key.isSpacer())
                    {
                        s.SetPanel(x, new EButtonImageAndTextSelectable(
                            null, isNumberOrOperator ? MappedColor.NumPadNumberBackground : MappedColor.NumPadScalerBackground,
                            null, isNumberOrOperator ? MappedColor.NumPadNumberBackground : MappedColor.NumPadScalerBackground,
                            def.text, isNumberOrOperator ? MappedColor.NumPadNumberForeground : MappedColor.NumPadScalerForeground,
                            def.text, isNumberOrOperator ? MappedColor.NumPadNumberForeground : MappedColor.NumPadScalerForeground,
                            false, true,
                            Location.Center, Location.Center, null, null, Location.Center)
                        {
                            StretchTexture = true,
                            TapCallback = new DrawableCallback(this.OnButtonTap, def.key),
                            DoubleTapCallback = new DrawableCallback(this.OnButtonDoubleTap, def.key),
                            Margin = new Vector4(1.PixelsToInches(), 0f, 0f, x > 0 ? 1.PixelsToInches() : 0f),
                            FontSize = fontSize
                        });
                    }
                }
                numpad.SetPanelSize(y + 1, btnSize);
                numpad.SetPanel(y + 1, s);
            }
            numpadStack = new Stack();
            numpadStack.AddItem(new EButtonImageAndText(null, MappedColor.NumPadInputBackground, "background", MappedColor.NumPadInputBackground)
            {
                StretchTexture = true
            });
            numpadStack.AddItem(numpad);            
        }

        private void OnButtonTap(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            OnKeyPressed((NumPadKey)args[0]);
        }

        private void OnButtonDoubleTap(EDrawable sender, object arg)
        {
            object[] args = (object[])arg;
            NumPadKey k = (NumPadKey)args[0];
            
            if (k == NumPadKey.Clear)
            {
                this.inputFieldValue = clearValue;
                return;
            }

            if (k.isScaler())   // k >= NumPadKey.Pico && k <= NumPadKey.Tera;
            {
                if (OnValueEntered != null)
                    OnValueEntered.Call(this, this.Value);
                return;
            }
            
            OnKeyPressed(k);
        }

        public virtual void OnKeyPressed(NumPadKey k)  //called when key is pressed on keyboard, or when tapped in numpad
        {
            //on first input: clear value instead of appending to existing value
            if (firstInput)
            {
                inputFieldValue = clearValue;
                firstInput = false;
            }

            string initialInput = this.inputFieldValue;
            if (k == NumPadKey.Clear)
            {
                if (initialInput.Length == 1)
                {
                    inputFieldValue = clearValue;
                    return;
                }
                if (inputFieldValue.Length > 0)
                    inputFieldValue = initialInput.Substring(0, initialInput.Length - 1);
                else
                    inputFieldValue = clearValue;
            }
            if (k == NumPadKey.ClearAll)
            {
                inputFieldValue = clearValue;
                return;
            }
            if (k.isNumber())
            {
                if (initialInput.Length >= MAX_NBR_CHARS) return;
                if (initialInput == clearValue) initialInput = "";
                inputFieldValue = initialInput + ((int)k).ToString();
                return;
            }
            if (k.isLetter())
            {
                if (inputFieldValue.Length >= MAX_NBR_CHARS) return;
                inputFieldValue += k.ToString();
            }
            if (k == NumPadKey.Confirm)
            {
                if (OnValueEntered != null && IsValid(Value))
                    OnValueEntered.Call(this, this.Value);
                return;
            }
            if (k == NumPadKey.Comma || k == NumPadKey.Period)
            {
                if (initialInput.Contains(COMMA)) return;
                if (initialInput.Length >= MAX_NBR_CHARS) return;
                inputFieldValue = initialInput + COMMA;
                return;
            }
            if (k == NumPadKey.Esc)
            {
                this.selfDestructDelegate.Invoke(this, null);
            }
        }

        protected abstract string ConvertValueToString(T value);

        protected void UpdateInputField()
        {
            //set text of input field
            inputField.Text = ConvertValueToString(Value);

            //set color to red in case value is out of limits
            inputField.Selected = !IsValid(Value);
        }        

        private void OnButtonDrag(EDrawable sender, object argument)
        {
            if (DragCallback != null)
                DragCallback.Call(this, argument);
        }

        private void OnButtonDrop(EDrawable sender, object argument)
        {
            if (DropCallback != null)
                DropCallback.Call(this, argument);
        }
    }        
}