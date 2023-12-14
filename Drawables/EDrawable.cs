using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Reflection;
using System.Collections.ObjectModel;
using System.IO;

namespace ESuite.Drawables
{
    internal enum Location { Left, Top, Center, Right, Bottom }
    internal enum Orientation { Horizontal, Vertical }
    internal enum Direction { Forward, Backward }

    internal delegate void DrawableCallbackDelegate(EDrawable sender, object argument);
    internal delegate bool KeyboardHandler(EDrawable focusedDrawable, KeyboardState keyboardState);

    internal class DrawableCallback
    {
        static public implicit operator DrawableCallback(DrawableCallbackDelegate del)
        {
            return new DrawableCallback(del);
        }

        private DrawableCallbackDelegate del;
        public string DelegateMethodName { get { return this.del.Method.Name; } }
        public object argument;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="del"></param>
        /// <param name="argument"></param>
        /// <param name="delay">Delay in ms before which the callback shan't be called</param>
        public DrawableCallback(DrawableCallbackDelegate del, object argument = null)
        {
            if (del == null)
                throw new Exception("Can't make a callback with a NULL delegate");
            this.del = del;
            this.argument = argument;
        }
        public DrawableCallback AddArgument(object newArg)
        {
            argument = Utils.extendArgument(argument, newArg);
            return this;
        }
        public void Call() { Call(null); }
        public void Call(EDrawable sender, object extraArg = null)
        {
            this.del(sender, Utils.extendArgument(argument, extraArg));
        }
    };

    internal abstract class EDrawableVertices : EDrawable
    {
        protected Matrix localWorld = Matrix.Identity; //this one to be used only when eDrawable uses vertices

        public override void OnBoundariesChanged()
        {
            localWorld = Utils.RectangleToMatrix(Boundaries, device, Matrix.Identity, View, Projection);
            base.OnBoundariesChanged();
        }

        internal EDrawableVertices() : base()
        {
        }
    }

    internal abstract class EDrawable
    {
        #region static

        protected static BlendState textureBlendState = BlendState.AlphaBlend;
        protected static BlendState fontBlendState = BlendState.AlphaBlend;
        private static Dictionary<string, Texture2D> loadedTextures;
#if DEBUG
        public string DummyTag = "";
#endif
        private static bool initialized = false;
        private static EDrawable _focusedDrawable = null;
        public static EDrawable focusedDrawable
        {
            get
            {
                return _focusedDrawable == null ? null : _focusedDrawable.Visible ? _focusedDrawable : null;
            }
            set
            {
                _focusedDrawable = value;
            }
        }
        public KeyboardHandler keyboardHandler = null;

        internal static GraphicsDevice device { get; private set; }
        protected static BasicEffect effect { get; private set; }
        protected static SpriteBatch spriteBatch { get; private set; }
        protected static ContentManager content { get; private set; }

        internal static Texture2D LoadPng(string source)
        {
            //init singleton
            if (loadedTextures == null) loadedTextures = new Dictionary<string, Texture2D>();

            if (loadedTextures.ContainsKey(source))
                return loadedTextures[source];
            else
            {
                string path = source;
                if (!path.EndsWith(".png"))
                    path += ".png";
#if ANDROID
                Stream fileStream = Android.App.Application.Context.Assets.Open(path);
#else
            FileStream fileStream = new FileStream("Content/" + path, FileMode.Open, FileAccess.Read);
#endif
                Texture2D texture = Texture2D.FromStream(device, fileStream);
                fileStream.Dispose();
                loadedTextures.Add(source, texture);
                return texture;
            }
        }
        public static void Initialize(GraphicsDevice device, BasicEffect effect, SpriteBatch spriteBatch, ContentManager content)
        {
            EDrawable.device = device;
            EDrawable.effect = effect;
            EDrawable.spriteBatch = spriteBatch;
            if (content == null)
                throw new Exception("Cannot initialize with null content");
            EDrawable.content = content;
            whiteTexture = content.Load<Texture2D>("white");
            defaultFont = content.Load<SpriteFont>(Scaler.GetFontDialog());
            bigFont = content.Load<SpriteFont>(Scaler.GetFontDialog());
            initialized = true;
        }

        public object Tag;

        public delegate void BoundariesChangedDelegate(Rectangle newBoundaries);
        public BoundariesChangedDelegate OnBoundariesChangedDelegate;
        public Rectangle Boundaries { get; protected set; }
        public virtual void SetBoundaries(Rectangle boundaries)
        {
            this.Boundaries = boundaries;
            OnBoundariesChanged();
        }

        public virtual Point? Size { get { return null; } }
        internal static bool drawInteractiveAreas = false;
        protected static Texture2D whiteTexture;
        //FIXME: these fonts should be removed, all EDrawables should be able to do its calculations self-contained
        protected static SpriteFont defaultFont, bigFont;

        protected bool MustRecalcNextUpdateCycle = false; //flag introduced as alternative to calling OnBoundariesUpdated multiple times each frame.

        #endregion

        private bool _visible = true;
        internal virtual bool Visible
        {
            get { return _visible; }
            set
            {
                if (_visible != value)
                    redrawRequest = true;

                _visible = value;

                //don't propagate Visible to children. Some objects have many children and it's the parent's choise to show them, not the grandparent's
                //foreach (EDrawable c in children)
                //c.Visible = value;
            }
        }

        private List<EDrawable> _children = new List<EDrawable>();
        internal ReadOnlyCollection<EDrawable> children { get { return _children.AsReadOnly(); } }
        protected void AddChild(EDrawable child)
        {
            _children.Add(child);
            child.parent = this;
            MustRecalcNextUpdateCycle = true;
        }
        public void AddChildBefore(EDrawable nextChild, EDrawable child)
        {
            RemoveChild(child);
            int index = _children.IndexOf(nextChild);
            _children.Insert(index, child);
            child.parent = this;
            MustRecalcNextUpdateCycle = true;
        }
        public void RemoveChild(EDrawable child)
        {
            _children.Remove(child);
            if (focusedDrawable == child)
                focusedDrawable = null;
            child.parent = null;
            MustRecalcNextUpdateCycle = true;
        }
        public void ClearAllChildren()
        {
            _children.Clear();
        }
        protected void InsertChild(int index, EDrawable child) { _children.Insert(index, child); child.parent = this; }
        protected void SetChildAt(int index, EDrawable child)
        {
            if (children[index] != null) children[index].parent = null;
            _children[index] = child;
            child.parent = this;
        }
        protected void ClearChildren() { foreach (var child in _children) child.parent = null; _children.Clear(); }

        public EDrawable parent { get; private set; }

        protected List<Rectangle> interactiveAreas;
        protected GestureType supportedGestures;

        protected bool contentLoaded = false;

        static private Matrix view = Matrix.CreateTranslation(-0.5f, -0.5f, 0) * Matrix.CreateScale(new Vector3(2f, -2f, 1f));
        public Matrix View { get { return view; } }
        public Matrix Projection { get { return Matrix.Identity; } }
        public Direction DrawOrder = Direction.Forward;
        protected bool redrawRequest = false;
        public static bool FullRedrawRequired = false;

        protected EDrawable()
        {
            if (!initialized)
                throw new Exception("EDrawable.Initialize() must be called before creating EDrawables");
        }

        #region OnWorldChanged, Update, Draw

        public void LoadContent()
        {
            RadixPrinter.ReferenceFont = content.Load<SpriteFont>(Scaler.GetFontButtonBar());

            LoadContentInternal();
            contentLoaded = true;
        }

        public void LoadContentPropagating()
        {
            LoadContent();
            contentLoaded = true;

            foreach (EDrawable child in children)
                child.LoadContentPropagating();
        }

        //this method executes only locally
        public virtual void OnBoundariesChanged()
        {
            this.redrawRequest = true;
            if (!contentLoaded) LoadContent();

            OnBoundariesChangedInternal();
            MustRecalcNextUpdateCycle = false;

            if (OnBoundariesChangedDelegate != null)
                OnBoundariesChangedDelegate(this.Boundaries);
        }

        //in this method:
        // - all new drawing locations must be calculated 
        // - SetBoundaries of all children must be called. They will NOT be called automatically.
        // - local vertices should NOT be recalculated, as they are relative to the localMatrix, which is recalculated upon call to SetBoundaries by parent which triggered call to this OMCI
        // - this method IS allowed to override this.Boundaries (OnBoundariesChangedDelegate is called after call this this method)
        abstract protected void OnBoundariesChangedInternal();
        abstract protected void LoadContentInternal();

        public void Update(GameTime now, List<GestureSample> gestureList)
        {
            //next: pass on to all children. Last to first, because last drawn is on top
            for (int i = 0; i < children.Count; i++)
                children[children.Count - 1 - i].Update(now, gestureList);

            //finally: call Update of this specific implementation at the end, because it will handle stuff that is drawn below all children
            this.UpdateInternal(now);

            //check this here, as UpdateInternal might already have called OnBoundariesChanged
            if (MustRecalcNextUpdateCycle)
                this.OnBoundariesChanged();
        }
        virtual protected void UpdateInternal(GameTime now) { }

        //for each section of non-changed drawables, the drawlist contains these non-changed drawables 
        public List<List<EDrawable>> CreateDrawList(List<List<EDrawable>> drawList)
        {
            if (!Visible) return drawList;

            //first item in the list should be drawn first. So first add this item itself, and afterwards its children.

            //try to add most complexity to the 'true' case, as this will most of the times evaluate to false
            if (this.redrawRequest)
            {
                //see whether a newlist must be started
                if (drawList.Last().Count > 0)
                    drawList.Add(new List<Drawables.EDrawable>());

                //add this drawable to the new singleton list
                drawList.Last().Add(this);

                //end anyway with a newlist
                drawList.Add(new List<Drawables.EDrawable>());
            }
            else
            {
                drawList.Last().Add(this);
            }

            //First draw all children on top of this drawable, not in prio list
            for (int i = 0; i < children.Count; i++)
            {
                int index = DrawOrder == Direction.Forward ? i : children.Count - i - 1;
                if (!priorityDrawList.Contains(children[index]))
                    drawList = children[index].CreateDrawList(drawList);
            }

            //then draw children according to priority list
            foreach (var child in priorityDrawList)
                drawList = child.CreateDrawList(drawList);

            return drawList;
        }

        internal virtual void Draw(GameTime time)
        {
            if (!Visible) return;

            this.DrawInternal(time);
#if DEBUG
            if (drawInteractiveAreas && interactiveAreas != null && interactiveAreas.Count > 0)
            {
                spriteBatch.Begin();
                foreach (Rectangle r in interactiveAreas)
                {
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y, r.Width, 1), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y, 1, r.Height), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X + r.Width - 1, r.Y, 1, r.Height), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y + r.Height - 1, r.Width, 1), MappedColor.DebugColor.C());
                }
                spriteBatch.End();
            }
#endif

            this.redrawRequest = false;
        }

#if DEBUG
        internal void DebugPrintOrder(int level)
        {
            if (!Visible) return;

            string printline = "";
            for (int i = 0; i < level; i++)
                printline += level.ToString();
            ScopeApp.DebugTextList.Add(printline + this.ToString());

            //First draw all children on top of this drawable, not in prio list
            for (int i = 0; i < children.Count; i++)
            {
                int index = DrawOrder == Direction.Forward ? i : children.Count - i - 1;
                if (!priorityDrawList.Contains(children[index]))
                    children[index].DebugPrintOrder(level + 1);
            }

            //then draw children according to priority list
            foreach (var child in priorityDrawList)
                child.DebugPrintOrder(level + 1);
        }
#endif

        internal virtual void DrawPropagating(GameTime time)
        {
            //not used anymore at all for deferred rendering -- here only for debugging purposes
            if (!Visible) return;

            this.DrawInternal(time);

            if (drawInteractiveAreas && interactiveAreas != null && interactiveAreas.Count > 0)
            {
                spriteBatch.Begin();
                foreach (Rectangle r in interactiveAreas)
                {
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y, r.Width, 1), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y, 1, r.Height), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X + r.Width - 1, r.Y, 1, r.Height), MappedColor.DebugColor.C());
                    spriteBatch.Draw(whiteTexture, new Rectangle(r.X, r.Y + r.Height - 1, r.Width, 1), MappedColor.DebugColor.C());
                }
                spriteBatch.End();
            }

            //First draw all children on top of this drawable, not in prio list
            for (int i = 0; i < children.Count; i++)
            {
                int index = DrawOrder == Direction.Forward ? i : children.Count - i - 1;
                if (!priorityDrawList.Contains(children[index]))
                    children[index].DrawPropagating(time);
            }

            //then draw children according to priority list
            foreach (var child in priorityDrawList)
                child.DrawPropagating(time);
        }
        abstract protected void DrawInternal(GameTime time);

        private List<EDrawable> priorityDrawList = new List<EDrawable>();
        virtual public void SetHighestDrawPriority(EDrawable child)
        {
            if (!_children.Contains(child))
                return;

            if (priorityDrawList.Contains(child))
                priorityDrawList.Remove(child);

            priorityDrawList.Insert(priorityDrawList.Count, child);
        }

        #endregion

        #region gesture handling

        protected virtual void ReleaseGestureControl()
        {
            ScopeApp.GestureMustBeReleased = true;
        }

        public bool PassGestureControl(EDrawable receiver, GestureSample gesture)
        {
            return receiver.ReceiveGestureControl(this, gesture);
        }

        public bool ReceiveGestureControl(EDrawable sender, GestureSample gesture)
        {
            if (ShouldHandleGesture(gesture, true))
            {
                HandleGestureInternal(gesture);
                redrawRequest = true;
                return true;
            }
            else
                return false;
        }

        internal virtual void HandleGesture(GestureSample gesture)
        {
            //if this parent item is not visible, there's no point in forwarding gesture handling calls to its children
            if (Visible || ScopeApp.GestureClaimer == this)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    int index = DrawOrder == Direction.Backward ? i : children.Count - 1 - i;
                    children[index].HandleGesture(gesture);
                }
            }

            if (!ScopeApp.GestureMustBeReleased && ShouldHandleGesture(gesture))
            {
                redrawRequest = true;
                this.HandleGestureInternal(gesture);
            }
        }

        virtual protected void HandleGestureInternal(GestureSample gesture)
        {
            //By default, if not overridden, release gesture so we don't get stuck
            //with a drawable claiming but never releasing it
            ReleaseGestureControl();
        }

        /// <summary>
        /// If the gesturelist is null or empty, or the drawable is hidden or has no interactive
        /// areas defined, this method immediately returns false. Otherwise, this method returns 
        /// true if
        /// - No drawable claimed the gesture control before and this drawable supports it and
        ///   the gesture was made in it's interactive area
        /// - This drawable claimed gesture control before and hasn't released the gesture control yet
        /// </summary>
        /// <param name="gesture">The gesture to test acceptance of</param>
        /// <param name="passingGestureControl">Used when passing a gesture to another control. Disables some checks that would otherwise prevent this gesture to be claimed</param>
        /// <returns></returns>
        protected bool ShouldHandleGesture(GestureSample gesture, bool passingGestureControl = false)
        {
            //If a drawable got hidden while a gesture (i.e. a drag) was
            //being performed on it, let it handle the gesture so it can
            //release the control when happy (i.e. dragComplete)
            if (ScopeApp.GestureClaimer == this && Visible == false)
                return true;
            //cancel in case no gestures need to be processed or no interactive area was defined
            if (Visible == false || interactiveAreas == null)
                return false;

            //If this drawable claimed a gesture before, and hasn't released control yet
            if (ScopeApp.GestureClaimer == this && !passingGestureControl)
                return true;

            //If another drawable claimed the gesture already, fail
            if (ScopeApp.GestureClaimer != null && !passingGestureControl)
                return false;

            //Now, we know the gesture control has not been claimed at all yet (claimer is null)
            //Do we support this type of gesture?
            if ((gesture.GestureType & supportedGestures) == 0)
                return false;

            //If this drawable is not the gestureclaimer, it's impossible that a 
            //gesture Completion should be assigned to it. This needs to be checked though,
            //since gesture completion has (0,0) as gesture coordinates.
            if (gesture.GestureType == GestureType.DragComplete || gesture.GestureType == GestureType.PinchComplete)
                return false;


            //Is the gesture made in our interactive area?
            if (!PositionInsideInteractiveArea(gesture.Position))
                return false;

            ScopeApp.GestureClaimer = this;
            return true;
        }

        internal bool PositionInsideInteractiveArea(Vector2 position)
        {
            if (interactiveAreas == null || interactiveAreas.Count == 0)
                return false;

            bool gestureOutOfInteractiveArea = true;
            foreach (Rectangle r in interactiveAreas)
            {
                if (r.Contains(Utils.VectorToPoint(position)))
                {
                    gestureOutOfInteractiveArea = false;
                    break;
                }
            }
            return !gestureOutOfInteractiveArea;
        }

        #endregion
    }
}
