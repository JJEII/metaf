// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

namespace Metaf
{
    public abstract class Condition : ImportExport
    {
        public abstract CTypeID typeid { get; } // get { return CTypeID.Unassigned; } }
        private int _d;
        protected int depth
        {
            get { return _d; }
            set { _d = value; }
        }
        public Condition(int d)
        {
            depth = d;
        }
    }
}