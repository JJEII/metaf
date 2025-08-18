// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CExpr : Condition
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "1", "s", "e", "s" };
        private string _s_expr;
        public override CTypeID typeid { get { return CTypeID.Expr; } }
        public CExpr(int d) : base(d) { _s_expr = "false"; }
        private string _m_expr
        {
            set { _s_expr = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_expr); }
        }
        private string _a_expr
        {
            set { _s_expr = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_expr); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_expr = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_expr);
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try { _a_expr = match.Groups["s"].Value[1..^1]; } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_expr);
            SpellIdsText = (SpellIdsText.Length > 0 ? " " + Rx.LC + SpellIdsText : "");
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_expr}{Rx.cD}{SpellIdsText}");
        }
    }
}