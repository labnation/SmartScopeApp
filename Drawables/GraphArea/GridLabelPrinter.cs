using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface;

namespace ESuite.Drawables
{
    internal class GridLabelPrinter : EDrawable
    {
        private SpriteFont labelFont;
        private Dictionary<Grid, List<LabelDefinition>> labelDefinitions = new Dictionary<Grid, List<LabelDefinition>>();

        public GridLabelPrinter()
            : base()
        {
            this.supportedGestures = GestureType.None;

            LoadContent();
        }

        protected override void LoadContentInternal()
        {
            labelFont = content.Load<SpriteFont>(Scaler.GetFontInternalArrows());
        }

        public Vector2 MeasureString(string str)
        {
            return labelFont.MeasureString(str);
        }

        public void AddGridLabelDefinition(Grid grid, LabelDefinition labDef)
        {
            if (!labelDefinitions.ContainsKey(grid))
                labelDefinitions.Add(grid, new List<Drawables.LabelDefinition>());

            labelDefinitions[grid].Add(labDef);
        }

        public void RemoveGridLabelDefinitions(Grid grid)
        {
            if (labelDefinitions.ContainsKey(grid))
                labelDefinitions.Remove(grid);
        }

        protected override void DrawInternal(GameTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, textureBlendState);
            foreach (var kvp in labelDefinitions)
                foreach (LabelDefinition ld in kvp.Value)
                    spriteBatch.DrawString(labelFont, ld.text, ld.topLeftScreenCoord, ld.color);
            spriteBatch.End();
        }

        protected override void OnBoundariesChangedInternal()
        {            
        }        

        override protected void HandleGestureInternal(GestureSample gesture)
        {
        }
    }
}
