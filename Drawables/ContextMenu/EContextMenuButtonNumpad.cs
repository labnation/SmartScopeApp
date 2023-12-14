using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite.Drawables
{
    internal class EContextMenuItemButtonNumpad : EContextMenuItemButton
    {
        private double minimum, maximum, precision;
        private double _value;
        public double Value
        {
            get { return _value; }
            set
            {
                if (this._value != value)
                {
                    this._value = value;
                    this.buttonStrings = new List<ButtonTextDefinition>() {
                        new ButtonTextDefinition(
                            LabNation.Common.Utils.siPrint(this._value, precision, ColorMapper.NumberDisplaySignificance, "s"),
                            VerticalTextPosition.Bottom, MappedColor.ContextMenuText, ContextMenuTextSizes.Tiny
                        )};
                }
            }
        }
        private string unit;
        private UIHandler.ShowNumPadDelegate showNumPadDelegate;
        private UIHandler.HideNumPadDelegate hideNumPadDelegate;
        private DrawableCallback onValueEntered;

        public EContextMenuItemButtonNumpad(
            string icon, 
            double value, double minimum, double maximum, double precision, string unit,
            UIHandler.ShowNumPadDelegate showNumPadDelegate, UIHandler.HideNumPadDelegate hideNumPadDelegate, DrawableCallback onValueEntered)
            : base(icon, null)
        {
            this.minimum = minimum;
            this.maximum = maximum;
            this.precision = precision;
            this.unit = unit;
            this.Value = value;
            this.showNumPadDelegate = showNumPadDelegate;
            this.hideNumPadDelegate = hideNumPadDelegate;
            this.onValueEntered = onValueEntered;
        }

        protected override void OnTap()
        {
            showNumPadDelegate(minimum, maximum, precision, unit, Value, new Point(Boundaries.X, Boundaries.Y), new Drawables.DrawableCallback(NumpadValueEntered));
        }

        private void NumpadValueEntered(EDrawable sender, object arg)
        {
            double value = (double)arg;
            Value = value;

            onValueEntered.Call(this, value);
            hideNumPadDelegate();

            this.Collapse();
        }

        public void Collapse()
        {
        }
    }
}
