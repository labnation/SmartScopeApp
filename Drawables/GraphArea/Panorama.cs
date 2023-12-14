using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using LabNation.DeviceInterface.Devices;
using ESuite.DataProcessors;
using LabNation.Common;

namespace ESuite.Drawables
{
    internal class Panorama : EDrawableVertices
    {        
        private Texture2D texture;
        private VertexPositionColor[] gridVertices;
        private List<Waveform> panoWaves = new List<Waveform>();

        public Panorama() : base()
        {
            DefineBorderVertices();

            LoadContent();
        }

        private void DefineBorderVertices()
        {
            List<VertexPositionColor> gridVertexList = new List<VertexPositionColor>();
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 0, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(1, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 1, 0), MappedColor.GridHilite.C()));
            gridVertexList.Add(new VertexPositionColor(new Vector3(0, 0, 0), MappedColor.GridHilite.C()));

            gridVertices = gridVertexList.ToArray();
        }

        protected override void LoadContentInternal()
        {
            this.texture = whiteTexture;
        }

        protected override void DrawInternal(GameTime time)
        {
            //simply draw the image to span the entire screen
            spriteBatch.Begin();
            spriteBatch.Draw(texture, Boundaries, MappedColor.PanoramaBackground.C());
            spriteBatch.End();

            //draw border
            effect.World = localWorld;
            effect.View = this.View;
            effect.Projection = this.Projection;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineStrip, gridVertices, 0, gridVertices.Length-1);
            }
        }

        protected override void OnBoundariesChangedInternal()
        {
            foreach (var panoWave in panoWaves)
                panoWave.SetBoundaries(this.Boundaries);            
        }

        public Waveform AddScopeChannel(Waveform parentWave)
        {
            Waveform waveform = null;
            //needs to be instantiated here, because caller (such as the Translator) could not have link to device
            if (parentWave is WaveformAnalog)
                waveform = new WaveformAnalog(null, parentWave.GraphColor, parentWave.Channel, null, (WaveformAnalog)parentWave);

            if (parentWave is WaveformDigital)
                waveform = new WaveformDigital(null, parentWave.GraphColor, parentWave.Channel, (WaveformDigital)parentWave);

            if (waveform != null)
                AddChild(waveform);

            panoWaves.Add(waveform);

            return waveform;
        }

        public void RemoveScopeChannel(Waveform parentWave)
        {
            panoWaves.Remove(panoWaves.Single(x => x.PanoramaWaveParent == parentWave));
        }
    }
}
