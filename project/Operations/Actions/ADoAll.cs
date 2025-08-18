// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class ADoAll : Action
    {
        private static readonly string[] metSeq = { "TABLE", "2", "K", "V", "n", "n" };
        private int _count;
        private Rule _myRule;
        public override ATypeID typeid { get { return ATypeID.DoAll; } }
        public List<Action> action;
        public ADoAll(int d, Rule r) : base(d) { action = new List<Action>(); _myRule = r; }

        override public void ImportFromMet(ref FileLines f)
        {
            ATypeID aID;
            Action tmpAct;
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _count = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
            for (int i = 0; i < _count; i++)
            {
                if (f.line[f.L++].CompareTo("i") != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected 'i'.");
                try { aID = (ATypeID)Int32.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

                try { tmpAct = _myRule.GetAction(aID, depth + 1); }
                catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: Error. [{e.Message}]"); }

                tmpAct.ImportFromMet(ref f); // <--- recurse
                action.Add(tmpAct);
            }
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(action.Count.ToString());
            foreach (Action a in action)
            {
                f.line.Add("i");
                f.line.Add(((int)a.typeid).ToString());
                a.ExportToMet(ref f); // <--- recurse
            }
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            // Assumes f.C already set correctly

            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);

            // Is there something after the operation, even though there shouldn't be?
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: Syntax error. {Rx.getInfo[typeid.ToString()]}");

            // It's a proper operation. Proceed. (This function only processes the operation keywords themselves, not any potential parameters they might have. It's the down-calls that do that part.)
            while (true) // internal break-outs only
            {
                // Find first non-"blank" line following this one (or EOF)
                f.L--;
                while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file
                if (f.L >= f.line.Count)
                    return;

                // Found first non-"blank" line. Try to get an operation (don't advance lines yet)
                match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
                if (match.Success)
                {
                    if (match.Groups["type"].Value.CompareTo("STATE:") == 0
                        || match.Groups["type"].Value.CompareTo("IF:") == 0
                        || match.Groups["type"].Value.CompareTo("NAV:") == 0
                    )
                    {
                        f.C = 0;
                        return;
                    }
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. A Rule must be composed of an IF-DO pair. It cannot have a 'DO:' block immediately following another 'DO:' block.");
                }

                // It better be a valid Action op...
                match = Rx.getLeadIn["AnyActionOp"].Match(f.line[f.L]);
                if (!match.Success)
                {
                    if (f.C > f.line[f.L].Length) // obscure one-off case of (extraneous) spaces preceding an IF:/etc. with nothing following...
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo["Generic"]}");
                    Match tmatch = Rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L][f.C..]); // don't advance line
                    if (tmatch.Success)
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected an Action operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) {Rx.getInfo["Generic"]}");
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected an Action operation. {Rx.getInfo["Generic"]}");
                }
                // It is.

                // How is it tabbed ?
                int nTabs = match.Groups["tabs"].Length;
                if (nTabs <= Rule.ActionContentTabLevel)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside an Action's All/Any operation. {Rx.getInfo[typeid.ToString()] }");
                if (nTabs <= depth)
                {   // return, since now done with this operation
                    f.C = nTabs;// Math.Max(nTabs - 1, 0);
                    return;
                }
                if (nTabs > depth + 1) // error
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Tabbed-in too far. {Rx.getInfo[typeid.ToString()]}");

                // Here: #tabs does equal depth+1; try to import this op.
                Action tmpAct;
                try { tmpAct = _myRule.GetAction(_myRule.actionStrToID[match.Groups["op"].Value], depth + 1); }
                catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: Error. [{e.Message}]"); }
                f.C = match.Groups["op"].Index + match.Groups["op"].Length;
                tmpAct.ImportFromMetAF(ref f); // <--- recurse
                action.Add(tmpAct);
            }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid}");
            foreach (Action a in action)
                a.ExportToMetAF(ref f); // <--- recurse
        }
    }
}