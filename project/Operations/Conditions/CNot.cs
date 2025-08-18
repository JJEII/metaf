// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CNot : Condition
    {
        private static readonly string[] metSeq = { "TABLE", "2", "K", "V", "n", "n" };
        private int _count_ignored;
        private Rule _myRule;
        public override CTypeID typeid { get { return CTypeID.Not; } }
        public Condition condition;
        public CNot(int d, Rule r) : base(d) { _myRule = r; }

        override public void ImportFromMet(ref FileLines f)
        {
            Condition tmpCond;
            CTypeID cID;
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _count_ignored = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

            if (_count_ignored != 1)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. 'Not' requires exactly one operand.");

            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected 'i'.");
            try { cID = (CTypeID)Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

            try { tmpCond = _myRule.GetCondition(cID, depth); } // don't increment depth
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: Error. [{e.Message}]"); }

            tmpCond.ImportFromMet(ref f); // <--- recurse

            condition = tmpCond;
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);

            f.line.Add("1");
            f.line.Add("i");
            f.line.Add(((int)condition.typeid).ToString());
            condition.ExportToMet(ref f); // <--- recurse
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            // assumes f.C already set...
            int starting_fC = f.C;
            int len = f.line[f.L].Length;

            // Try to get the next op, right after Not
            Match match = Rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L][Math.Min(f.C, len)..]);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: Syntax error. {Rx.getInfo[typeid.ToString()]}");

            // Succeeded. Import it.
            f.C = f.C + match.Groups["op"].Index + match.Groups["op"].Length;
            try { condition = _myRule.GetCondition(_myRule.conditionStrToID[match.Groups["op"].Value], depth); } // do not increase depth
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: Error. [{e.Message}]"); }
            condition.ImportFromMetAF(ref f); // <--- recurse

            f.C = starting_fC;
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            int ln = f.line.Count; // remember location to go back and insert the Not
            condition.ExportToMetAF(ref f); // <--- recurse
            f.line[ln] = $"{new string('\t', depth)}{typeid} {f.line[ln].TrimStart()}";
        }
    }
}