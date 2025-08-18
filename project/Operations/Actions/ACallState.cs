// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class ACallState : Action
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "2", "s", "st", "s" };
        private string _s_toState, _s_retState;
        public override ATypeID typeid { get { return ATypeID.CallState; } }
        public ACallState(int d) : base(d) { }
        private string _m_toState
        {
            set { _s_toState = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_toState); }
        }
        private string _a_toState
        {
            set { _s_toState = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_toState); }
        }
        private string _m_retState
        {
            set { _s_retState = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_retState); }
        }
        private string _a_retState
        {
            set { _s_retState = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_retState); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_toState = f.line[f.L++];

            foreach (string s in new[] { "s", "ret", "s" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_retState = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_toState);

            foreach (string s in new[]{"s", "ret", "s"})
                f.line.Add(s);
            f.line.Add(_m_retState);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");
            try
            {
                _a_toState = match.Groups["s"].Value[1..^1];  // length is at least 2; remove delimiters
                _a_retState = match.Groups["s2"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_toState}{Rx.cD} {Rx.oD}{_a_retState}{Rx.cD}");
        }
    }
}