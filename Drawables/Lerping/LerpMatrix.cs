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
    internal class LerpMatrix : Lerp<Matrix>
    {
        public LerpMatrix(Matrix start, Matrix end, float speed) : base
            (start, end, speed,
            (Matrix m_start, Matrix m_end, float progress) => Matrix.Lerp(m_start, m_end, LerpingSine.SmoothStopPower(progress))
            )
        {
        }

    }
}
