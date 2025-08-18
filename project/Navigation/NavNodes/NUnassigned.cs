// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;

namespace Metaf
{
    public class NUnassigned : NavNode
    {
        public NUnassigned(Nav myNav) : base() { throw new Exception($"{GetType().Name}.NUnassigned: Should never get here."); }
        public override NTypeID typeid { get { return NTypeID.Unassigned; } }
        public override void ImportFromMet(ref FileLines f) { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMet: Should never get here."); }
        public override void ExportToMet(ref FileLines f) { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ExportToMet: Should never get here."); }
        public override void ImportFromMetAF(ref FileLines f) { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Should never get here."); }
        public override void ExportToMetAF(ref FileLines f) { throw new Exception($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ExportToMetAF: Should never get here."); }
    }
}