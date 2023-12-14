using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal interface IBoundaryDefined
    {
        void UpdateBoundaries(float lower, float upper);
    }

    internal class BoundaryDefiner
    {
        private Dictionary<IBoundaryDefined, float> elements = new Dictionary<IBoundaryDefined, float>();
        float min;
        float max;
        
        internal BoundaryDefiner(float min, float max)
        {
            this.UpdateLimits(min, max);
        }

        public void UpdateLimits(float min, float max)
        {
            this.min = min;
            this.max = max;
        }

        public void AddElement(IBoundaryDefined o, float value)
        {
            elements.Add(o, value);
        }

        public void RemoveElement(IBoundaryDefined o)
        {
            if (elements.ContainsKey(o))
                elements.Remove(o);
            else
                throw new Exception("Element " + o.ToString() + " not found in BoundaryDefiner");
        }

        public void UpdateElement(IBoundaryDefined o, float val)
        {
            if (!elements.ContainsKey(o))
                throw new Exception("Element " + o.ToString() + " not found in BoundaryDefiner.UpdateElement");

            elements[o] = val;

            ////////////////////////////////////////
            //update Boundaries of all elements
            //FIXME: probably only o-1, o and o+1 should be updated?
            var ordered = elements.OrderBy(x => x.Value);
            int count = ordered.Count();
            if (count == 1) return;

            //first element
            ordered.ElementAt(0).Key.UpdateBoundaries(min, (ordered.ElementAt(0).Value + ordered.ElementAt(1).Value) / 2f);

            for (int i = 1; i < count-1; i++)
                ordered.ElementAt(i).Key.UpdateBoundaries((ordered.ElementAt(i - 1).Value + ordered.ElementAt(i).Value) / 2f, (ordered.ElementAt(i).Value + ordered.ElementAt(i + 1).Value) / 2f);

            //last element
            ordered.ElementAt(count - 1).Key.UpdateBoundaries((ordered.ElementAt(count - 2).Value + ordered.ElementAt(count-1).Value) / 2f, max);
        }
    }
}
