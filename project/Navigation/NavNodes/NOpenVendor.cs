// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class NOpenVendor : NavNode
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private int _guid;
        private string _s_vendorName;
        private Nav _myNav;
        public NOpenVendor(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.OpenVendor; } }
        private double[] _xyz
        {
            get
            {
                double[] t = { _x, _y, _z };
                return t;
            }
        }
        private string _m_vendorName
        {
            set { _s_vendorName = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_vendorName); }
        }
        private string _a_vendorName
        {
            set { _s_vendorName = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_vendorName); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            try { _x = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _y = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _z = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '0'.");
            try { _guid = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
            _m_vendorName = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            foreach (double c in _Txyz) // len 3
                f.line.Add(c.ToString());
            f.line.Add("0");
            f.line.Add(_guid.ToString());
            f.line.Add(_m_vendorName);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {   // FORMAT: vnd myx myy myz tgtGUID tgtName
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}");
            try
            {
                _x = Double.Parse(match.Groups["d"].Value);
                _y = Double.Parse(match.Groups["d2"].Value);
                _z = Double.Parse(match.Groups["d3"].Value);
                _guid = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber);
                _a_vendorName = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"\t{(M_NTypeID)typeid} {_x} {_y} {_z} {_guid:X8} {Rx.oD}{_a_vendorName}{Rx.cD}");
        }
    }
}