// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CUnassigned : Condition
    {
        public override CTypeID typeid { get { return CTypeID.Unassigned; } }
        public CUnassigned(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f)
        { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMet: Should never get here."); }
        override public void ExportToMet(ref FileLines f)
        { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ExportToMet: Should never get here."); }
        override public void ImportFromMetAF(ref FileLines f)
        { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Should never get here."); }
        override public void ExportToMetAF(ref FileLines f)
        { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ExportToMetAF: Should never get here."); }
    }
}