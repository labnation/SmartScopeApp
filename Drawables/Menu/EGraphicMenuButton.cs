using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace ESuite
{
    public enum ButtonStatus { Deactivated, Activating, Active, Deactivating };

    public class EGraphicMenuButton:EGraphicMenuItem
    {
        List<VertexPositionColor> controlVertices = new List<VertexPositionColor>();        
        Texture2D texture;
        Rectangle boundaries;
        ButtonDelegate buttonDelegate;
        SpriteFont font;
        string buttonText;
        Vector2 stringSize;
        Vector2 stringPosition;
        ButtonStatus currentStatus = ButtonStatus.Deactivated;
        //float transitionTime = 0;
        string temp = "";
        float topYPos = 0;

        Color multiplyColor = Utils.MainColorBlue;
        LerpMatrix lerpMatrix = new LerpMatrix(Matrix.CreateTranslation(-1f,0,0)*Matrix.Identity, 0.8f);

        public ButtonStatus CurrentStatus { get { return currentStatus; } }

        public EGraphicMenuButton(EDrawable parent, EXNAController xnaController, string buttonText, ButtonDelegate buttonDelegate, EGraphicSubMenu parentMenuItem)
            : base(parent, xnaController, parentMenuItem)
        {
            this.buttonDelegate = buttonDelegate;
            this.buttonText = buttonText;
            //this.Activate();
        }

        protected override void LoadContentInternal(ContentManager Content)
        {
            texture = Content.Load<Texture2D>("white");
            font = Content.Load<SpriteFont>("sampleFont");
            stringSize = font.MeasureString(buttonText);

            ReactOnNewWVP();//need to do, because this class can be instantiated dynamically
        }

        protected override void DrawInternal()
        {
            if (currentStatus == ButtonStatus.Deactivated) return;

            //first render semi-black rectangle and text
            spriteBatch.Begin();
            spriteBatch.Draw(texture, boundaries, new Color(new Vector4(0, 0, 0.1f, 0.5f)));
            spriteBatch.DrawString(font, buttonText, stringPosition, Color.White);
            //spriteBatch.DrawString(font, temp, new Vector2(0,50), Color.White);
            spriteBatch.End();

            

            //then draw white box on top
            effect.World = lerpMatrix.CurrentValue*parentWorld;
            effect.View = this.view;
            effect.Projection = this.projection;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                effect.CurrentTechnique.Passes[0].Apply();
                device.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.LineStrip, controlVertices.ToArray(), 0, controlVertices.Count-1);
            }

#if DRAWUIRECTANGLE
            if (this.gestureClaimer)
                Utils.DrawRectangle(device, effect, boundaries, Color.Red);
            else
                Utils.DrawRectangle(device, effect, boundaries, Color.White);
#endif

        }

        public void SetCallBackMethod(ButtonDelegate del)
        {
            this.buttonDelegate = del;
        }

        override public void SetTopYPos(float topYPos)
        {
            this.topYPos = topYPos;
        }

        public override void Deactivate(float delaySeconds)
        {
            //top condition will be true in case menu is deactivated by repressing the Settings button -> call to mainMenu.Deactivate()
            if (currentStatus == ButtonStatus.Deactivated) return;

            currentStatus = ButtonStatus.Deactivating;
            lerpMatrix.SetTarget(Matrix.CreateTranslation(1f, 0, 0), delaySeconds);            
        }
        
        protected override void ReactOnNewWVP()
        {            
            Vector2 topLeftLocation = new Vector2(0.25f, topYPos);
            Vector2 buttonSize = new Vector2(0.5f, 0.15f);
            Vector2 botRightLocation = topLeftLocation + buttonSize;           

            //define screen coord rectange to render image
            Vector3 topLeftLocationScreenPos = device.Viewport.Project(new Vector3(topLeftLocation, 0), projection, view, lerpMatrix.CurrentValue * parentWorld);
            Vector3 botRightScreenCoords = device.Viewport.Project(new Vector3(botRightLocation, 0), projection, view, lerpMatrix.CurrentValue * parentWorld);
            boundaries = new Rectangle((int)topLeftLocationScreenPos.X, (int)topLeftLocationScreenPos.Y, (int)(botRightScreenCoords.X-topLeftLocationScreenPos.X), (int)(botRightScreenCoords.Y-topLeftLocationScreenPos.Y));

            //define relative positions for boundary box
            controlVertices.Clear();
            controlVertices.Add(new VertexPositionColor(new Vector3(topLeftLocation, 0), Color.White));
            controlVertices.Add(new VertexPositionColor(new Vector3(botRightLocation.X, topLeftLocation.Y, 0), Color.White));
            controlVertices.Add(new VertexPositionColor(new Vector3(botRightLocation, 0), Color.White));
            controlVertices.Add(new VertexPositionColor(new Vector3(topLeftLocation.X, botRightLocation.Y, 0), Color.White));
            controlVertices.Add(new VertexPositionColor(new Vector3(topLeftLocation, 0), Color.White));            

            //calculate center position
            Vector3 centerLocationScreenCoords = (topLeftLocationScreenPos + botRightScreenCoords) / 2;
            stringPosition = new Vector2(centerLocationScreenCoords.X, centerLocationScreenCoords.Y)-stringSize / 2;

            temp = lerpMatrix.CurrentTransitionTime.ToString();
        }

        //public void Activate() { Activate(0); }
        override public void Activate(float timeDelay)
        {
            currentStatus = ButtonStatus.Activating;
            lerpMatrix.SetTarget(Matrix.Identity, timeDelay);            
        }

        private void OnClick(DateTime now)
        {
            ReleaseGestureControl();

            //set MenuShown to false. If the delegate is invoking a submenu, then that delegate has to reset to true
            EXNAController.MenuShown = false;
            if (buttonDelegate != null)
                buttonDelegate();      

            parentMenuItem.DeactivateChildren();
        }

        override protected void UpdateInternal(DateTime now, List<GestureSample> gestureList)
        {
            lerpMatrix.Update(now);

            if (currentStatus == ButtonStatus.Deactivated) return;            

            if (lerpMatrix.CurrentTransitionTime >= 1)
            {
                switch (currentStatus)
                {
                    case ButtonStatus.Deactivated:
                        return;
                    case ButtonStatus.Activating:
                        currentStatus = ButtonStatus.Active;
                        //lerpMatrix.LerpingFunction = LerpingSine.SmoothStartPower;
                        break;
                    case ButtonStatus.Active:
                        break;
                    case ButtonStatus.Deactivating:
                        currentStatus = ButtonStatus.Deactivated;
                        //at this point, the button has finished deactivating, so it's matrix should be moved quietly to the left
                        lerpMatrix.SetTargetImmediately(Matrix.CreateTranslation(-1f, 0, 0));
                        //lerpMatrix.LerpingFunction = LerpingSine.SmoothStopPower;
                        break;
                }
            }

            ReactOnNewWVP();

            //process input
            if (gestureList != null && gestureList.Count == 0) return;

            //boundaries contains the exact coordinates of the button image only
            if (CheckGestureInteractionFail(gestureList, boundaries, GestureType.Tap | GestureType.FreeDrag | GestureType.DragComplete))
                return;            

            foreach (GestureSample gesture in gestureList)
            {
                switch (gesture.GestureType)
                {
                    case GestureType.Tap:                    
                        //button pressed
                        OnClick(now);
                        ReleaseGestureControl();
                        break;
                    default:
                        ReleaseGestureControl();
                        break;
                }
            }
        }

    }
}
