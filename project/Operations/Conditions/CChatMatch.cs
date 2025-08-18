// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CChatMatch : Condition
    {
        private static readonly string[] metSeq = { "s" };
        private string _s_chat;
        public override CTypeID typeid { get { return CTypeID.ChatMatch; } }
        public CChatMatch(int d) : base(d) { _s_chat = ""; }

        private string _m_chat
        {
            set { _s_chat = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_chat); }
        }
        private string _a_chat
        {
            set { _s_chat = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_chat); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_chat = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_chat);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            _a_chat = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_chat}{Rx.cD}");
        }
    }
}