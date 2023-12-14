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
    internal class Lerp<T>
    {
        public delegate T transitioner(T start, T end, float progress);

        private float progress = 0f; //float between 0 and 1 indicating progress of transition
        public bool done { get { return progress >= 1f; } }
        private T start;
        private T current;
        public T target { get; private set; }
        private transitioner transition;
        private bool inTransition = false;
        public bool TransitionFinishedFlag = false;
        
        private bool restart = true;
        private float speed;
        TimeSpan animationStartingPoint = TimeSpan.Zero;

        public T CurrentValue { get 
            {
                if (this.speed == 0f) return target;
                else return current;
            } 
        }
        public float Progress { get { return this.progress; } }

        /// <summary>
        /// Creates a LERPer
        /// </summary>
        /// <param name="start">Matrix to start interpolation from</param>
        /// <param name="target">Matrix to interpolate towards</param>
        /// <param name="transitionSpeed">Time in seconds of the transition </param>
        /// <param name="transitioner">Function to compute LERP value</param>
        public Lerp(T start, T target, float transitionSpeed, transitioner transitioner) 
        {
            this.start = start;
            this.current = start;
            this.target = target;

            this.progress = 0f;
            this.speed = transitionSpeed;
            this.transition = transitioner;
        }

        public void SetTargetImmediately(T target)
        {
            this.progress = 1f;
            this.target = target;
            this.current = target;
            this.restart = true;
        }

        public void UpdateTarget(T target)
        {
            if (target.Equals(current))
            {
                this.restart = true;
                progress = 1f;
                return;
            }
			//If we're updating the animation when it's properly started,
			//we probably intend to restart a fresh animation
            if (progress >= 0.5f)
            {
                this.start = current;
                this.target = target;
                this.progress = 0f;
                this.restart = true;
            }
            else
            {
                this.target = target;
            }
        }

        public bool Update(GameTime now)
        {
            if (restart)
            {
                //Initialize timespan and return starting value
                animationStartingPoint = now.TotalGameTime;
                inTransition = true;
                restart = false;
                return true;
            }

            if (progress < 1f)
            {
                inTransition = true;
                progress = (float)((now.TotalGameTime - animationStartingPoint).TotalMilliseconds) / (speed * 1000f);

                if (progress >= 1f)
                    this.current = target;
                else
                    this.current = this.transition(start, target, progress);
                return true;
            }
            else if (inTransition)
            {
                progress = 1;
                TransitionFinishedFlag = true;
                inTransition = false;
                return true;
            }
            return false;
        }
    }
}
