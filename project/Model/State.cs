// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class State : ImportExport
    {
        private string _s_name;
        private Meta _myMeta;
        private List<Rule> _rule;
        public string name { get { return _a_name; } }
        public int ruleCount { get { return _rule.Count; } }
        private string _m_name
        {
            set { _s_name = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_name); }
        }
        private string _a_name
        {
            set { _s_name = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_name); }
        }
        public State(string name, Meta myM, bool isMetCalling_isNotMetAFCalling)
        {
            if (isMetCalling_isNotMetAFCalling)
                _m_name = name;
            else
                _a_name = name;

            _rule = new List<Rule>();
            _myMeta = myM;
        }
        public void AddRule(Rule r)
        {
            _rule.Add(r);
        }
        public override void ImportFromMet(ref FileLines f)
        {
            throw new Exception("State.ImportFromMet: Don't ever call this function; use State's parameter-taking constructor instead.");
        }
        public override void ExportToMet(ref FileLines f)
        {
            foreach (Rule r in _rule)
                r.ExportToMet(ref f, _m_name);
        }
        public override void ImportFromMetAF(ref FileLines f)
        {
            Match match;
            int fLineAtStart = f.L;

            // loop this until EOF; break out on 'STATE:' or 'NAV:'
            while (f.L < f.line.Count)
            {
                // Find next non-"blank" line, or EOF
                f.L--;
                while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file; done
                if (f.L >= f.line.Count)
                    break;

                // Found first non-"blank" line... done reading Rules for this state ?  ("STATE:" or "NAV:" line ?)
                match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                if (!match.Success)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. {Rx.getInfo["STATE:"]}");
                if (match.Groups["type"].Value.CompareTo("STATE:") == 0 || match.Groups["type"].Value.CompareTo("NAV:") == 0)
                    break;

                // Start of a new Rule ? ("IF:" line ?)
                if (match.Groups["type"].Value.CompareTo("IF:") != 0) // i.e., it must be a "DO:" line if !="IF:" since it matched StateIfDoNav, and State & Nav were already checked above
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. (Missing Condition for this Action?) {Rx.getInfo["STATE:"]}");

                // It's an "IF:" line; try to import this Rule
                f.C = 0;
                Rule tmpRule = new Rule(_myMeta, this);
                tmpRule.ImportFromMetAF(ref f);
                _rule.Add(tmpRule);
            }

            if (_rule.Count == 0)
                throw new MyException($"[LINE {fLineAtStart}] {GetType().Name}.ImportFromMetAF: Every state must contain at least one Rule, even if it's just Never-None. [{Rx.getInfo["STATE:"]}]");
        }

        public override void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"STATE: {Rx.oD}{_a_name}{Rx.cD} {Rx.LC} {{");
            foreach (Rule r in _rule)
                r.ExportToMetAF(ref f);
            f.line.Add($"{Rx.LC} }}");
        }
    }
}