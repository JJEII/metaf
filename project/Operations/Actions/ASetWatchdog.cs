// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class ASetWatchdog : Action
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "3", "s", "s", "s" };
        private string _s_state;
        private double _range, _time;
        public override ATypeID typeid { get { return ATypeID.SetWatchdog; } }
        public ASetWatchdog(int d) : base(d) { }
        private string _m_state
        {
            set { _s_state = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_state); }
        }
        private string _a_state
        {
            set { _s_state = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_state); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_state = f.line[f.L++];

            foreach (string s in new[] { "s", "r", "d" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _range = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }

            foreach (string s in new[] { "s", "t", "d" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _time = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_state);
            foreach (string s in new[] { "s", "r", "d" })
                f.line.Add(s);
            f.line.Add(_range.ToString());
            foreach (string s in new[] { "s", "t", "d" })
                f.line.Add(s);
            f.line.Add(_time.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");
            try
            {
                _range = Double.Parse(match.Groups["d"].Value);
                _time = Double.Parse(match.Groups["d2"].Value);
                _a_state = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {_range} {_time} {Rx.oD}{_a_state}{Rx.cD}");
        }
    }
}