// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CItemCountGE : Condition
    {
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "2", "s", "n", "s" };
        private string _s_invItem;
        private int _invCount;
        public override CTypeID typeid { get { return CTypeID.ItemCountGE; } }
        public CItemCountGE(int d) : base(d) { }
        private string _m_invItem
        {
            set { _s_invItem = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_invItem); }
        }
        private string _a_invItem
        {
            set { _s_invItem = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_invItem); }
        }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_invItem = f.line[f.L++];

            foreach (string s in new[] {"s", "c", "i" })
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _invCount = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_invItem);
            
            foreach (string s in new[] { "s", "c", "i" })
                f.line.Add(s);
            f.line.Add(_invCount.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try
            {
                _invCount = Int32.Parse(match.Groups["i"].Value);
                _a_invItem = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {_invCount} {Rx.oD}{_a_invItem}{Rx.cD}");
        }
    }
}