// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class NPortal_NPC : NavNode
    {
        private double _objx, _objy, _objz, _myx, _myy, _myz;
        private double[] _Tobjxyz, _Tmyxyz;
        private string _s_objName;
        private int _objClass;
        private Nav _myNav;
        public NPortal_NPC(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Portal_NPC; } }
        private double[] _myxyz
        {
            get
            {
                double[] t = { _myx, _myy, _myz };
                return t;
            }
        }
        private double[] _objxyz
        {
            get { double[] t = { _objx, _objy, _objz }; return t; }
        }
        private string _m_objName
        {
            set { _s_objName = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_objName); }
        }
        private string _a_objName
        {
            set { _s_objName = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_objName); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            try { _myx = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _myy = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _myz = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '0'.");

            _m_objName = f.line[f.L++];
            try { _objClass = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            if (_objClass != 14 && _objClass != 37 && _objClass != 10) // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Invalid Object Class.");
            if (f.line[f.L++].CompareTo("True") != 0) // always "True" ??
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected 'True'.");

            try { _objx = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _objy = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _objz = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Tmyxyz = _myNav.ApplyXF(_myxyz);
            _Tobjxyz = _myNav.ApplyXF(_objxyz);
            f.line.Add(((int)typeid).ToString());
            foreach (double c in _Tmyxyz) // len 3
                f.line.Add(c.ToString());
            f.line.Add("0");
            f.line.Add(_m_objName);
            f.line.Add(_objClass.ToString());
            f.line.Add("True"); // always True ??
            foreach (double c in _Tobjxyz) // len 3
                f.line.Add(c.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {   // FORMAT: ptl tlk myx myy myz tgtx tgty tgtz tgtObjectClass tgtName
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}");
            try
            {
                _myx = Double.Parse(match.Groups["d"].Value);
                _myy = Double.Parse(match.Groups["d2"].Value);
                _myz = Double.Parse(match.Groups["d3"].Value);
                _objx = Double.Parse(match.Groups["d4"].Value);
                _objy = Double.Parse(match.Groups["d5"].Value);
                _objz = Double.Parse(match.Groups["d6"].Value);
                _objClass = Int32.Parse(match.Groups["i"].Value);
                if (_objClass != 14 && _objClass != 37 && _objClass != 10)  // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
                    throw new MyException("Object Class typically must be 14 (portal) or 37 (npc).");
                _a_objName = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"\t{(M_NTypeID)typeid} {_myx} {_myy} {_myz} {_objx} {_objy} {_objz} {_objClass} {Rx.oD}{_a_objName}{Rx.cD}");
        }
    }
}