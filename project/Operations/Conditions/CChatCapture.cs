// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CChatCapture : Condition
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "2", "s", "p", "s" };
        private string _s_regex;
        private string _s_colorIDlist;
        public override CTypeID typeid { get { return CTypeID.ChatCapture; } }
        public CChatCapture(int d) : base(d)
        {
            _s_regex = "";
            _s_colorIDlist = "";
        }
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
        private string _m_colorIDlist
        {
            set { _s_colorIDlist = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_colorIDlist); }
        }
        private string _a_colorIDlist
        {
            set { _s_colorIDlist = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_colorIDlist); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_regex = f.line[f.L++];

            foreach (string s in new[] {"s", "c", "s" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_colorIDlist = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_regex);
            
            foreach (string s in new[] { "s", "c", "s" })
                f.line.Add(s);
            f.line.Add(_m_colorIDlist);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try
            {
                _a_regex = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
                _a_colorIDlist = match.Groups["s2"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_regex}{Rx.cD} {Rx.oD}{_a_colorIDlist}{Rx.cD}");
        }
    }
}