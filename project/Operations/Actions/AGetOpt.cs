// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class AGetOpt : Action
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "2", "s", "o", "s" };
        private string _s_opt, _s_var;
        public override ATypeID typeid { get { return ATypeID.GetOpt; } }
        public AGetOpt(int d) : base(d) { _s_opt = _s_var = ""; }
        private string _m_opt
        {
            set { _s_opt = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_opt); }
        }
        private string _a_opt
        {
            set { _s_opt = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_opt); }
        }
        private string _m_var
        {
            set { _s_var = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_var); }
        }
        private string _a_var
        {
            set { _s_var = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_var); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_opt = f.line[f.L++];

            foreach (string s in new[] { "s", "v", "s" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_var = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_opt);

            foreach (string s in new[] { "s", "v", "s" })
                f.line.Add(s);
            f.line.Add(_m_var);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try
            {
                _a_opt = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
                _a_var = match.Groups["s2"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name }.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_opt}{Rx.cD} {Rx.oD}{_a_var}{Rx.cD}");
        }
    }
}