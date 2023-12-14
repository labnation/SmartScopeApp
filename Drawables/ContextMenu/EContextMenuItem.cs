using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ESuite.Drawables
{
    internal abstract class EContextMenuItem : EDrawable
    {
        public EContextMenuItem() : base()  {}

        protected EContextMenu root
        {
            get
            {
                if (this.parent is EContextMenuItem)
                    return (parent as EContextMenuItem).root;
                else
                    return (EContextMenu)this;
            }
        }

    }
}
