// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

namespace Metaf
{
    public abstract class Action : ImportExport
    {
        public abstract ATypeID typeid { get; } //{ return ATypeID.Unassigned; } }
        private int _d;
        protected int depth
        {
            get { return _d; }
            set { _d = value; }
        }
        public Action(int d)
        {
            depth = d;
        }
    }
}