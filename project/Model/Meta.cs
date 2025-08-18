// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class Meta : ImportExport
    {
        private static readonly string[] metSeq = { "1", "CondAct", "5", "CType", "AType", "CData", "AData", "State", "n", "n", "n", "n", "n" };
        private int _uniqueTagCounter;
        private List<State> _state;                                     // all states that exist
        private Dictionary<string, Nav> _nav;                           // all navs that exist, cited or not by Actions
        private Dictionary<string, List<AEmbedNav>> _actionUsingNav;    // dictionary[tag] of: list of Actions actually using 'tag' nav (Action cites it, and nav exists)
        private Dictionary<string, List<AEmbedNav>> _actionCitesNav;    // dictionary[tag] of: list of Actions citing use of 'tag' nav
        private string _s_sn; // just a scratch 'state name' variable
        private bool _navOnly;
        public bool IsNavOnly { get { return _navOnly; } }
        private string _m_sn
        {
            set { _s_sn = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_sn); }
        }
        private string _a_sn
        {
            set { _s_sn = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_sn); }
        }

        public Meta(bool navOnly = false)
        {
            _state = new List<State>();
            _nav = new Dictionary<string, Nav>();
            _uniqueTagCounter = 0;
            _actionUsingNav = new Dictionary<string, List<AEmbedNav>>();
            _actionCitesNav = new Dictionary<string, List<AEmbedNav>>();
            _navOnly = navOnly;
        }

        public string GenerateUniqueNavTag(string postfix)
        {
            postfix = new Regex(@"[^a-zA-Z0-9_]").Replace(postfix, "_");
            postfix = (postfix.Length > 0 ? $"__{postfix}" : "");
            return $"nav{_uniqueTagCounter++}{postfix}";
        }
        public void AddToNavsUsed(string tag, AEmbedNav actionEmbNav)
        {
            if (!_actionUsingNav.ContainsKey(tag))
                _actionUsingNav.Add(tag, new List<AEmbedNav>());
            _actionUsingNav[tag].Add(actionEmbNav);
        }
        public void AddNavCitationByAction(string tag, AEmbedNav actionEmbNav)
        {
            if (!_actionCitesNav.ContainsKey(tag))
                _actionCitesNav.Add(tag, new List<AEmbedNav>());
            _actionCitesNav[tag].Add(actionEmbNav);
        }
        public void AddNav(string tag, Nav nav)
        { // Add 'tag' Nav to list of extant Navs, with 'tag' collision detection
            if (_nav.ContainsKey(tag))
                throw new MyException($"NAV already defined for tag '{tag}'.");
            _nav.Add(tag, nav);
        }
        public Nav GetNav(string tag)
        { // Get 'tag' Nav; if it doesn't exist, complain
            if (!_nav.ContainsKey(tag))
                throw new MyException($"No NAV found with tag '{tag}'.");
            return _nav[tag];
        }

        override public void ImportFromMet(ref FileLines f)
        {
            if (!_navOnly)
            {
                // Intro lines
                foreach (var s in metSeq)
                    if (s.CompareTo(f.line[f.L++]) != 0)
                        throw new MyException($"[LINE {f.L + f.offset}] Unknown file type: First lines do not match expected format.");

                // Number of rules in file
                try { Rule.Count = UInt32.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] Expected number of rules saved in file but didn't find that. [{e.Message}]"); }

                // Read all the rules, including embedded navs
                int nRules = 0;
                string prev_sn = null;
                State curState = null;
                while (nRules < Rule.Count)
                {
                    Rule r = new Rule(this);
                    _m_sn = r.ImportFromMet(ref f); // accessor enforces single internal string delimiter rules   // !!!!!
                    if (prev_sn == null || prev_sn.CompareTo(_s_sn) != 0 || curState == null)
                    {
                        if (curState != null)
                            _state.Add(curState);
                        curState = new State(_m_sn, this, true); // !!!!!
                        prev_sn = _s_sn;
                    }
                    curState.AddRule(r);
                    r.SetMetaState(curState);
                    nRules++;
                }
                if (nRules > 0)
                    _state.Add(curState);
            }
            else
            {
                if ("uTank2 NAV 1.2".CompareTo(f.line[f.L]) != 0)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] Unknown file type: First lines do not match expected format.");
                Nav n = new Nav(this);
                n.ImportFromMet(ref f);
            }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.Clear();

            if (!_navOnly)
            {
                // Intro lines
                foreach (string s in metSeq)
                {
                    f.line.Add(s);
                    f.L++;
                }

                // Number of rules in file
                int ruleCount = 0;
                foreach (State s in _state)
                    ruleCount += s.ruleCount;
                f.line.Add(ruleCount.ToString());

                // ...this.state.Sort()...    by name
                // ^^^ It turns out... VTank doesn't seem to care! :)

                // The rules
                foreach (State s in _state)
                    s.ExportToMet(ref f);

                // Deliberately create an "error" by removing CreateView's final newline, making it run straight into the following data. (For whatever reason, VT does this, and requires it.)
                if (ACreateView.breakitFixIndices.Count > 0)
                {
                    foreach (int i in ACreateView.breakitFixIndices)
                        f.line[i + 1] = f.line[i] + f.line[i + 1];
                    int s = ACreateView.breakitFixIndices[0];
                    int nxt = 1;
                    for (int d = ACreateView.breakitFixIndices[0]; d < f.line.Count - ACreateView.breakitFixIndices.Count; d++)
                    {
                        s++;
                        if (nxt < ACreateView.breakitFixIndices.Count)
                        {
                            if (s == ACreateView.breakitFixIndices[nxt])
                            {
                                s++;
                                nxt++;
                            }
                        }
                        f.line[d] = f.line[s];
                    }
                    f.line.RemoveRange(f.line.Count - ACreateView.breakitFixIndices.Count - 1, ACreateView.breakitFixIndices.Count);
                    ACreateView.breakitFixIndices.Clear();
                }
            }
            else
            {
                if (_nav.Count == 0)
                    throw new MyException("No navroutes to output!");
                if (_nav.Count > 1)
                    Console.WriteLine("WARNING: Multiple navroutes detected. A .nav file contains only one navroute. Ignoring all but the first.");
                foreach (KeyValuePair<string, Nav> kv in _nav)
                {
                    kv.Value.ExportToMet(ref f);
                    break;
                }
            }
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            Match match;

            if (!_navOnly)
            {
                // loop until EOF or "NAV:" line found
                while (f.L < f.line.Count)
                {
                    // Find next non-"blank" line, or EOF
                    f.L--;
                    while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                        ;

                    // Hit end of file; done
                    if (f.L >= f.line.Count)
                        break;

                    // Found first non-"blank" line... done reading States for this meta ? ("NAV:" line ?)
                    match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                    if (!match.Success)
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. {Rx.getInfo["STATE:"]}");
                    if (match.Groups["type"].Value.CompareTo("NAV:") == 0)
                        break;

                    // Start of new State ? ("STATE:" line ?)
                    if (match.Groups["type"].Value.CompareTo("STATE:") != 0)
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. {Rx.getInfo["STATE:"]}");

                    // Try to import this State
                    f.C = 6; // Math.Min(6, f.line[f.L].Length - 1);
                    string thisLN = Rx.R__2EOL.Replace(f.line[f.L][f.C..], ""); // don't advance line
                    match = Rx.getParms["STATE:"].Match(thisLN);
                    //f.C = Math.Min(6, f.line[f.L].Length - 1);
                    //match = Rx.getParms["STATE:"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                    if (!match.Success)
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. (Did you put a space between the colon and state name?) {Rx.getInfo["STATE:"]}");

                    // Double check that this state name does not already exist
                    string tmpStr = match.Groups["s"].Value[1..^1]; // remove string delimiters from ends
                    foreach (State st in _state)
                        if (st.name.CompareTo(tmpStr) == 0)
                            throw new MyException($"[LINE {f.L + f.offset + 1}] Meta.ImportFromMetAF: State names must be unique; the state name {Rx.oD}{tmpStr}{Rx.cD} is already in use.");

                    // Import this state's contents, and add it to the state list
                    State tmpState = new State(tmpStr, this, false); // tempStr is an "AF string"
                    f.C = 0;
                    f.L++;
                    tmpState.ImportFromMetAF(ref f);
                    _state.Add(tmpState);
                }
                if (_state.Count == 0)
                {
                    Console.WriteLine($"[LINE {f.L + f.offset + 1}] Meta.ImportFromMetAF: WARNING: You defined no meta states. Handling as a nav-only file.");
                    _navOnly = true;
                }
            }


            // NAVS

            // loop until EOF
            while (f.L < f.line.Count)
            {
                // Find next non-"blank" line, or EOF
                f.L--;
                while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file; done
                if (f.L >= f.line.Count)
                    break;

                // Found first non-"blank" line... does it start with "NAV:" ? (It needs to.)
                match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                if (!match.Success || match.Groups["type"].Value.CompareTo("NAV:") != 0)
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. {Rx.getInfo["NAV:"]}");

                // Import this nav's contents
                Nav tmpNav = new Nav(this);
                f.C = 4; // Math.Min(4,f.line[f.L].Length-1);
                tmpNav.ImportFromMetAF(ref f);  // the nav adds itself to the list at the end of this call
            }


            // DONE reading all meta info (state and nav) from file

            if (!_navOnly)
            {
                // Establish successful cross-linking of cited navs to navs that actually exist
                foreach (KeyValuePair<string, List<AEmbedNav>> TagEN in _actionCitesNav)
                {
                    if (_nav.ContainsKey(TagEN.Key))
                    {
                        foreach (AEmbedNav en in TagEN.Value)
                            AddToNavsUsed(TagEN.Key, en);
                    }
                    else // Error: cited nav does not exist...
                    {   // Build error message string to include all citations to this non-existent nav.
                        string tmpStr = "";
                        bool addNewline = false;
                        foreach (AEmbedNav en in TagEN.Value)
                        {
                            if (addNewline)
                                tmpStr += "\n";
                            tmpStr += $"[LINE {en.my_metAFline + 1}] Meta.ImportFromMetAF: Nav ({TagEN.Key}) cited for embedding but is never defined.";
                            addNewline = true;
                        }
                        throw new MyException(tmpStr);
                    }
                }

                // And now the opposite check, to see if all defined navs are actually being used (just issue a warning, though)
                foreach (KeyValuePair<string, Nav> en in _nav)
                    if (!_actionCitesNav.ContainsKey(en.Key))
                        Console.WriteLine($"[LINE {en.Value.my_metAFftagline + 1}] WARNING: {GetType().Name}.ImportFromMetAF: Nav tag ({en.Key}) is never used.");
            }
        }
        private void CollapseIfDo(ref FileLines f)
        {
            int lead = 0;
            int trail = 0;
            string strConditionCmp = new string('\t', Rule.ConditionContentTabLevel - 1) + "IF:";
            string strActionCmp = new string('\t', Rule.ActionContentTabLevel - 1) + "DO:";

            // Find the first collapse point (first IF: or DO:)
            while (trail < f.line.Count)
            {
                if ((f.line[trail].Length >= strConditionCmp.Length && 0 == f.line[trail][..Math.Min(strConditionCmp.Length, f.line[trail].Length)].CompareTo(strConditionCmp))
                     || (f.line[trail].Length >= strActionCmp.Length && 0 == f.line[trail][..Math.Min(strActionCmp.Length, f.line[trail].Length)].CompareTo(strActionCmp)))
                    break;
                trail++;
            }
            lead = trail + 1;

            // if collapse is needed, collapse lead onto trail, then increment both counters, check lead<Count{copy lead into trail, increment lead}else{break}
            // else increment trail, copy lead into trail, increment lead
            while (lead < f.line.Count)
            {
                // if f.line[trail] "starts" with "IF:" or "DO:"
                if (0 == f.line[trail][..Math.Min(strConditionCmp.Length, f.line[trail].Length)].CompareTo(strConditionCmp)
                     || 0 == f.line[trail][..Math.Min(strActionCmp.Length, f.line[trail].Length)].CompareTo(strActionCmp))
                {   // collapse & advance
                    f.line[trail++] += "\t" + f.line[lead++].TrimStart();
                    if (lead >= f.line.Count)
                    {
                        trail--;
                        break;
                    }
                    f.line[trail] = f.line[lead++];
                }
                else // copy & advance
                    f.line[++trail] = f.line[lead++];
            }
            // f.line[trail] is indexing the last valid line at this point
            if (trail + 1 < f.line.Count)
                f.line.RemoveRange(trail + 1, f.line.Count - (trail + 1));
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.Clear();

            if (!_navOnly)
            {
                f.line.Add(OutputText.metaHeader);
                foreach (State s in _state)
                    s.ExportToMetAF(ref f);
                CollapseIfDo(ref f);

                if (_nav.Count > 0)
                {
                    f.line.Add("");
                    f.line.Add(Rx.LC + "========================= ONLY NAVS APPEAR BELOW THIS LINE =========================" + Rx.LC);
                    f.line.Add("");

                    foreach (KeyValuePair<string, Nav> sn in _nav)
                        sn.Value.ExportToMetAF(ref f);
                }
            }
            else
            {
                f.line.Add(OutputText.navHeader);
                foreach (KeyValuePair<string, Nav> sn in _nav)
                {
                    sn.Value.ExportToMetAF(ref f);
                    break; // only one
                }
            }
        }
    }
}