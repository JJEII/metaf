// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class NJump : NavNode
    {
        private double _x, _y, _z, _headingDeg, _delayMS;
        private double[] _Txyz;
        private string _s_doHoldShift;
        private Nav _myNav;
        public NJump(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Jump; } }
        private double[] _xyz
        {
            get
            {
                double[] t = { _x, _y, _z };
                return t;
            }
            set
            {
                _x = value[0];
                _y = value[1];
                _z = value[2];
            }
        }
        private string _m_doHoldShift
        {
            set { _s_doHoldShift = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_doHoldShift); }
        }
        private string _a_doHoldShift
        {
            set { _s_doHoldShift = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_doHoldShift); }
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
            try { _headingDeg = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            _m_doHoldShift = f.line[f.L++];
            try { _delayMS = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            foreach (double c in _Txyz) // len 3
                f.line.Add(c.ToString());
            f.line.Add("0");
            f.line.Add(_headingDeg.ToString());
            f.line.Add(_m_doHoldShift);
            f.line.Add(_delayMS.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {   // FORMAT: jmp myx myy myz headingInDegrees holdShift delayInMilliseconds
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}");
            try
            {
                _x = Double.Parse(match.Groups["d"].Value);
                _y = Double.Parse(match.Groups["d2"].Value);
                _z = Double.Parse(match.Groups["d3"].Value);
                _headingDeg = Double.Parse(match.Groups["d4"].Value);
                _a_doHoldShift = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
                if (_s_doHoldShift.CompareTo("True") != 0 && _s_doHoldShift.CompareTo("False") != 0)
                    throw new MyException($"'Hold shift' must be {Rx.oD}True{Rx.cD} or {Rx.oD}False{Rx.cD}.");
                _delayMS = Double.Parse(match.Groups["d5"].Value);
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"\t{(M_NTypeID)typeid} {_x} {_y} {_z} {_headingDeg} {Rx.oD}{_a_doHoldShift}{Rx.cD} {_delayMS}");
        }
    }
}