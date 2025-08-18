// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

namespace Metaf
{
    public abstract class ImportExport
    {
        public abstract void ImportFromMet(ref FileLines f);
        public abstract void ExportToMet(ref FileLines f);
        public abstract void ImportFromMetAF(ref FileLines f);
        public abstract void ExportToMetAF(ref FileLines f);
    }
}