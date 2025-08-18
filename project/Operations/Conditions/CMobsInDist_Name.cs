// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CMobsInDist_Name : Condition
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "3", "s", "n", "s" };
        private string _s_regex;
        private int _count;
        private double _range;
        public override CTypeID typeid { get { return CTypeID.MobsInDist_Name; } }
        public CMobsInDist_Name(int d) : base(d) { }
        private string _m_regex
        {
            set { _s_regex = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_regex); }
        }
        private string _a_regex
        {
            set { _s_regex = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_regex); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_regex = f.line[f.L++];

            foreach (string s in new[] {"s", "c", "i" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _count = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

            foreach (string s in new[] { "s", "r", "d" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _range = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_regex);

            foreach (string s in new[] { "s", "c", "i" })
                f.line.Add(s);
            f.line.Add(_count.ToString());

            foreach (string s in new[] { "s", "r", "d" })
                f.line.Add(s);
            f.line.Add(_range.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try
            {
                _count = Int32.Parse(match.Groups["i"].Value);
                _range = Double.Parse(match.Groups["d"].Value);
                _a_regex = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {_count} {_range} {Rx.oD}{_a_regex}{Rx.cD}");
        }
    }
}