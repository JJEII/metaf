// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class Rule
    {
        public static uint Count = 0;
        public const int ConditionContentTabLevel = 2;
        public const int ActionContentTabLevel = 3;
        private Meta _myMeta;
        private State _myState;
        private Condition _condition;
        private Action _action;
        public Rule(Meta myM)
        {
            _condition = new CUnassigned(ConditionContentTabLevel);
            _action = new AUnassigned(ActionContentTabLevel);
            _myMeta = myM;
        }
        public Rule(Meta myM, State myS)
        {
            _condition = new CUnassigned(ConditionContentTabLevel);
            _action = new AUnassigned(ActionContentTabLevel);
            _myMeta = myM;
            _myState = myS;
        }
        public void SetMetaState(State s)
        {
            _myState = s;
        }
        public Condition GetCondition(CTypeID cid, int d)
        {
            switch (cid)
            {
                case CTypeID.Never: return new CNever(d);
                case CTypeID.Always: return new CAlways(d);
                case CTypeID.All: return new CAll(d, this);
                case CTypeID.Any: return new CAny(d, this);
                case CTypeID.ChatMatch: return new CChatMatch(d);
                case CTypeID.MainSlotsLE: return new CMainSlotsLE(d);
                case CTypeID.SecsInStateGE: return new CSecsInStateGE(d);
                case CTypeID.NavEmpty: return new CNavEmpty(d);
                case CTypeID.Death: return new CDeath(d);
                case CTypeID.VendorOpen: return new CVendorOpen(d);
                case CTypeID.VendorClosed: return new CVendorClosed(d);
                case CTypeID.ItemCountLE: return new CItemCountLE(d);
                case CTypeID.ItemCountGE: return new CItemCountGE(d);
                case CTypeID.MobsInDist_Name: return new CMobsInDist_Name(d);
                case CTypeID.MobsInDist_Priority: return new CMobsInDist_Priority(d);
                case CTypeID.NeedToBuff: return new CNeedToBuff(d);
                case CTypeID.NoMobsInDist: return new CNoMobsInDist(d);
                case CTypeID.BlockE: return new CBlockE(d);
                case CTypeID.CellE: return new CCellE(d);
                case CTypeID.IntoPortal: return new CIntoPortal(d);
                case CTypeID.ExitPortal: return new CExitPortal(d);
                case CTypeID.Not: return new CNot(d, this);
                case CTypeID.PSecsInStateGE: return new CPSecsInStateGE(d);
                case CTypeID.SecsOnSpellGE: return new CSecsOnSpellGE(d);
                case CTypeID.BuPercentGE: return new CBuPercentGE(d);
                case CTypeID.DistToRteGE: return new CDistToRteGE(d);
                case CTypeID.Expr: return new CExpr(d);
                //case CTypeID.ClientDialogPopup: return new CClientDialogPopup(d);
                case CTypeID.ChatCapture: return new CChatCapture(d);
            }
            throw new MyException("Invalid Condition Type ID integer.");
        }

        public Dictionary<string, CTypeID> conditionStrToID = new()
        {
            ["Never"] = CTypeID.Never,
            ["Always"] = CTypeID.Always,
            ["All"] = CTypeID.All,
            ["Any"] = CTypeID.Any,
            ["ChatMatch"] = CTypeID.ChatMatch,
            ["MainSlotsLE"] = CTypeID.MainSlotsLE,
            ["SecsInStateGE"] = CTypeID.SecsInStateGE,
            ["NavEmpty"] = CTypeID.NavEmpty,
            ["Death"] = CTypeID.Death,
            ["VendorOpen"] = CTypeID.VendorOpen,
            ["VendorClosed"] = CTypeID.VendorClosed,
            ["ItemCountLE"] = CTypeID.ItemCountLE,
            ["ItemCountGE"] = CTypeID.ItemCountGE,
            ["MobsInDist_Name"] = CTypeID.MobsInDist_Name,
            ["MobsInDist_Priority"] = CTypeID.MobsInDist_Priority,
            ["NeedToBuff"] = CTypeID.NeedToBuff,
            ["NoMobsInDist"] = CTypeID.NoMobsInDist,
            ["BlockE"] = CTypeID.BlockE,
            ["CellE"] = CTypeID.CellE,
            ["IntoPortal"] = CTypeID.IntoPortal,
            ["ExitPortal"] = CTypeID.ExitPortal,
            ["Not"] = CTypeID.Not,
            ["PSecsInStateGE"] = CTypeID.PSecsInStateGE,
            ["SecsOnSpellGE"] = CTypeID.SecsOnSpellGE,
            ["BuPercentGE"] = CTypeID.BuPercentGE,
            ["DistToRteGE"] = CTypeID.DistToRteGE,
            ["Expr"] = CTypeID.Expr,
            ["ChatCapture"] = CTypeID.ChatCapture
        };

        public Dictionary<string, ATypeID> actionStrToID = new()
        {
            ["None"] = ATypeID.None,
            ["SetState"] = ATypeID.SetState,
            ["Chat"] = ATypeID.Chat,
            ["DoAll"] = ATypeID.DoAll,
            ["EmbedNav"] = ATypeID.EmbedNav,
            ["CallState"] = ATypeID.CallState,
            ["Return"] = ATypeID.Return,
            ["DoExpr"] = ATypeID.DoExpr,
            ["ChatExpr"] = ATypeID.ChatExpr,
            ["SetWatchdog"] = ATypeID.SetWatchdog,
            ["ClearWatchdog"] = ATypeID.ClearWatchdog,
            ["GetOpt"] = ATypeID.GetOpt,
            ["SetOpt"] = ATypeID.SetOpt,
            ["CreateView"] = ATypeID.CreateView,
            ["DestroyView"] = ATypeID.DestroyView,
            ["DestroyAllViews"] = ATypeID.DestroyAllViews
        };

        public Action GetAction(ATypeID aid, int d)
        {
            switch (aid)
            {
                case ATypeID.None: return new ANone(d);
                case ATypeID.SetState: return new ASetState(d);
                case ATypeID.Chat: return new AChat(d);
                case ATypeID.DoAll: return new ADoAll(d, this);
                case ATypeID.EmbedNav: return new AEmbedNav(d, _myMeta);
                case ATypeID.CallState: return new ACallState(d);
                case ATypeID.Return: return new AReturn(d);
                case ATypeID.DoExpr: return new ADoExpr(d);
                case ATypeID.ChatExpr: return new AChatExpr(d);
                case ATypeID.SetWatchdog: return new ASetWatchdog(d);
                case ATypeID.ClearWatchdog: return new AClearWatchdog(d);
                case ATypeID.GetOpt: return new AGetOpt(d);
                case ATypeID.SetOpt: return new ASetOpt(d);
                case ATypeID.CreateView: return new ACreateView(d);
                case ATypeID.DestroyView: return new ADestroyView(d);
                case ATypeID.DestroyAllViews: return new ADestroyAllViews(d);
            }
            throw new MyException("Invalid Action Type ID integer.");
        }
        public string ImportFromMet(ref FileLines f)
        {
            CTypeID cID;
            ATypeID aID;

            // Read the condition type, and set-up the data structure for reading the data in a moment
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] File format error. Expected 'i'.");
            try { cID = (CTypeID)Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] File format error. Expected an integer. [{e.Message }]"); }

            try { _condition = GetCondition(cID, ConditionContentTabLevel); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: Error. [{e.Message}]"); }

            // Read the action type, and set-up the data structure for reading the data in a moment
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] File format error. Expected 'i'.");
            try { aID = (ATypeID)Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] File format error. Expected an integer. [{e.Message}]"); }

            try { _action = GetAction(aID, Rule.ActionContentTabLevel); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: Error. [{e.Message}]"); }

            // Read the condition data
            _condition.ImportFromMet(ref f);

            // Read the action data
            _action.ImportFromMet(ref f);

            // Read and return the state name
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] File format error. Expected 's'.");

            return f.line[f.L++]; // no need to check it for single internal string delimiters because it's checked for that upon return
        }

        public void ExportToMet(ref FileLines f, string stateName)
        {
            f.line.Add("i");
            f.line.Add(((int)_condition.typeid).ToString());
            f.line.Add("i");
            f.line.Add(((int)_action.typeid).ToString());
            _condition.ExportToMet(ref f);
            _action.ExportToMet(ref f);
            f.line.Add("s");
            f.line.Add(stateName);
        }
        public void ImportFromMetAF(ref FileLines f)
        {
            Match match;


            // CONDITION

            // Find first non-"blank" line
            f.L--;
            while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                ;

            // Prematurely hit end of file
            if (f.L >= f.line.Count)
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Hit end-of-file but needed a Condition ('IF:' line).");

            // Found first non-"blank" line... "IF:" ?
            match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
            if (!match.Success
                || match.Groups["type"].Value.CompareTo("IF:") != 0
                || match.Groups["tabs"].Value.Length != ConditionContentTabLevel - 1
                )
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected Condition ('IF:' line). {Rx.getInfo["IF:"]}");
            f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

            // Try to grab the Condition keyword
            match = Rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L][f.C..]); // don't advance line
            if (!match.Success)
            {
                Match tmatch = Rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L][f.C..]); // don't advance line
                if (tmatch.Success)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) {Rx.getInfo["IF:"]}");
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. {Rx.getInfo["IF:"]}");
            }

            // Try to import this Condition
            try { _condition = GetCondition(conditionStrToID[match.Groups["op"].Value], Rule.ConditionContentTabLevel); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Error. [{e.Message}]"); }
            f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//, f.line[f.L].Length-1);
            _condition.ImportFromMetAF(ref f); // advances line inside


            // ACTION

            // Find first non-"blank" line
            f.L--;
            while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                ;

            // Prematurely hit end of file
            if (f.L >= f.line.Count)
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Hit end-of-file but needed a Rule Action ('DO:' line).");

            // Found first non-"blank" line... "DO:" ?
            match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
            if (!match.Success
                || match.Groups["type"].Value.CompareTo("DO:") != 0
                || match.Groups["tabs"].Value.Length != ActionContentTabLevel - 1
                )
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected Action ('DO:' line). {Rx.getInfo["DO:"]}");
            f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

            // Try to grab the Action keyword
            match = Rx.getLeadIn["AnyActionOp"].Match(f.line[f.L][f.C..]); // don't advance line
            if (!match.Success)
            {
                Match tmatch = Rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L][f.C..]); // don't advance line
                if (tmatch.Success)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) {Rx.getInfo["DO:"]}");
                throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. {Rx.getInfo["DO:"]}");
            }

            // Try to import this Action
            try { _action = GetAction(actionStrToID[match.Groups["op"].Value], ActionContentTabLevel); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Error. [{e.Message}]"); }

            f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//=Math.Min(f.C+..., f.line[f.L].Length-1);
            _action.ImportFromMetAF(ref f); // advances line inside

            f.C = 0;
        }
        public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', ConditionContentTabLevel - 1)}IF:");
            _condition.ExportToMetAF(ref f);
            f.line.Add($"{new string('\t', ActionContentTabLevel - 1)}DO:");
            _action.ExportToMetAF(ref f);
        }
    }
}