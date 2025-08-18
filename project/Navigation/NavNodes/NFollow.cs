// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class NFollow : NavNode
    {  // Weird one-off case... kind've a pseudo-node, really; the only one with no xyz (for obvious reasons)
        private string _s_tgtName;
        private int _tgtGuid;
        private Nav _myNav;
        public NFollow(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Follow; } }
        private string _m_tgtName
        {
            set { _s_tgtName = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_tgtName); }
        }
        private string _a_tgtName
        {
            set { _s_tgtName = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_tgtName); }
        }
        override public void ImportFromMet(ref FileLines f)
        {
            _m_tgtName = f.line[f.L++];
            try { _tgtGuid = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            //f.line.Add(((int)this.typeid).ToString()); // follow node type not output since there's exactly one node, and 'nav type' already determines what type it is
            f.line.Add(_m_tgtName);
            f.line.Add(_tgtGuid.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {   // FORMAT: flw tgtGUID tgtName
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}");
            try
            {
                _tgtGuid = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber);
                _a_tgtName = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"\t{(M_NTypeID)typeid} {_tgtGuid:X8} {Rx.oD}{_a_tgtName}{Rx.cD}");
        }
    }
}