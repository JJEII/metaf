using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using MetAF.enums;

namespace MetAF
{
    // ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION
    // ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION
    // ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION


    abstract public class Action : ImportExport
    {
        abstract public ATypeID typeid { get; } //{ return ATypeID.Unassigned; } }
        private int _d;
        protected int depth { get { return _d; } set { _d = value; } }
        public Action(int d) { depth = d; }
    }

    public class AUnassigned : Action // line# for msgs good
    {
        public override ATypeID typeid { get { return ATypeID.Unassigned; } }
        public AUnassigned(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] AUnassigned.ImportFromMet: Should never get here."); }
        override public void ExportToMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] AUnassigned.ExportToMet: Should never get here."); }
        override public void ImportFromMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] AUnassigned.ImportFromMetAF: Should never get here."); }
        override public void ExportToMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] AUnassigned.ExportToMetAF: Should never get here."); }
    }
    public class ANone : Action // line# for msgs good
    {
        public override ATypeID typeid { get { return ATypeID.None; } }
        public ANone(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
        }
    }

    public class ASetState : Action // line# for msgs good
    {
        private string _s_state;
        public override ATypeID typeid { get { return ATypeID.SetState; } }
        public ASetState(int d) : base(d) { _s_state = ""; }
        private string _m_state
        {
            set { _s_state = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_state); }
        }
        private string _a_state
        {
            set { _s_state = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_state); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_state = f.line[f.L++];
            //try { this._state = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("s");
            f.line.Add(_m_state);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _a_state = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_state + rx.cD);
        }
    }

    public class AChat : Action // line# for msgs good
    {
        private string _s_chat;
        public override ATypeID typeid { get { return ATypeID.Chat; } }
        public AChat(int d) : base(d) { _s_chat = ""; }
        private string _m_chat
        {
            set { _s_chat = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_chat); }
        }
        private string _a_chat
        {
            set { _s_chat = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_chat); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_chat = f.line[f.L++];
            //try { this._chat = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("s");
            f.line.Add(_m_chat);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_chat);
            SpellIdsText = SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "";
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_chat + rx.cD + SpellIdsText);
        }
    }

    public class ADoAll : Action // line# for msgs good
    {
        private int _count;
        private Rule _myRule;
        public override ATypeID typeid { get { return ATypeID.DoAll; } }
        public List<Action> action;
        public ADoAll(int d, Rule r) : base(d) { action = new List<Action>(); _myRule = r; }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            ATypeID aID;
            Action tmpAct;
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("K") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'K'.");
            if (f.line[f.L++].CompareTo("V") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'V'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            try { _count = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            for (int i = 0; i < _count; i++)
            {
                if (f.line[f.L++].CompareTo("i") != 0)
                    throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
                try { aID = (ATypeID)int.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

                try { tmpAct = _myRule.GetAction(aID, depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

                tmpAct.ImportFromMet(ref f); // <--- recurse
                action.Add(tmpAct);
            }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("K");
            f.line.Add("V");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add(action.Count.ToString());
            foreach (Action a in action)
            {
                f.line.Add("i");
                f.line.Add(((int)a.typeid).ToString());
                a.ExportToMet(ref f); // <--- recurse
            }
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            // Assumes f.C already set correctly

            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );

            // Is there something after the operation, even though there shouldn't be?
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[typeid.ToString()]);

            // It's a proper operation. Proceed. (This function only processes the operation keyords themselves, not any potential parameters they might have. It's the down-calls that do that part.)
            while (true) // internal break-outs only
            {
                // Find first non-"blank" line following this one (or EOF)
                f.L--;
                while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file
                if (f.L >= f.line.Count)
                    return;

                // Found first non-"blank" line. Try to get an operation (don't advance lines yet)
                match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
                if (match.Success)
                {
                    if (match.Groups["type"].Value.CompareTo("STATE:") == 0
                        || match.Groups["type"].Value.CompareTo("IF:") == 0
                        || match.Groups["type"].Value.CompareTo("NAV:") == 0
                        || match.Groups["type"].Value.CompareTo("VIEW:") == 0
                    )
                    {
                        f.C = 0;
                        return;
                    }
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. A Rule must be composed of an IF-DO pair. It cannot have a 'DO:' block immediately following another 'DO:' block.");
                }

                // It better be a valid Action op...
                match = rx.getLeadIn["AnyActionOp"].Match(f.line[f.L]);
                if (!match.Success)
                {
                    if (f.C > f.line[f.L].Length) // obscure one-off case of (extraneous) spaces preceding an IF:/etc. with nothing following...
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo["Generic"]);
                    Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                    if (tmatch.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation. " + rx.getInfo["Generic"]);
                }
                // It is.

                // How is it tabbed ?
                int nTabs = match.Groups["tabs"].Length;
                if (nTabs <= Rule.ActionContentTabLevel)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside an Action's All/Any operation. " + rx.getInfo[typeid.ToString()]);
                if (nTabs <= depth)
                {   // return, since now done with this operation
                    f.C = nTabs;// Math.Max(nTabs - 1, 0);
                    return;
                }
                if (nTabs > depth + 1) // error
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[typeid.ToString()]);

                // Here: #tabs does equal depth+1; try to import this op.
                Action tmpAct;
                try { tmpAct = _myRule.GetAction(_myRule.actionStrToID[match.Groups["op"].Value], depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
                f.C = match.Groups["op"].Index + match.Groups["op"].Length;
                tmpAct.ImportFromMetAF(ref f); // <--- recurse
                action.Add(tmpAct);
            }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
            foreach (Action a in action)
                a.ExportToMetAF(ref f); // <--- recurse
        }
    }

    public class AEmbedNav : Action // line# for msgs good
    {
        private string _s_name;
        private string _tag;
        private int _exactCharCountToAfterMetNAV_InclCrLf;
        private int _idxInF_ExactCharCountNumber;
        private Meta _myMeta;
        private double[] _xf = { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 }; // [a,b,c,d,e,f,g]
                                                                      // [ a  b (0)][x] [e]
                                                                      // [ c  d (0)][y]+[f]
                                                                      // [(0)(0)(1)][z] [g]
        public int my_metAFline;
        public AEmbedNav(int d, Meta m) : base(d) { _myMeta = m; my_metAFline = -1; }
        public override ATypeID typeid { get { return ATypeID.EmbedNav; } }
        private string _m_name
        {
            set { _s_name = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_name); }
        }
        private string _a_name
        {
            set { _s_name = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_name); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            int nNodesInNav;
            Nav nav = new Nav(_myMeta);

            // ba = "byte array" ???
            if (f.line[f.L++].CompareTo("ba") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ba'.");
            try { _exactCharCountToAfterMetNAV_InclCrLf = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            // nav's in-game name
            _m_name = f.line[f.L++];
            //try { this._name = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            // # nodes in this nav ???
            try { nNodesInNav = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

            _tag = _myMeta.GenerateUniqueNavTag(_m_name);
            _myMeta.AddToNavsUsed(_tag, this);
            nav.tag = _tag;

            // if a nav got imported in-game (empty or not)... read it; otherwise, we're already done
            if (_exactCharCountToAfterMetNAV_InclCrLf > 5)
                nav.ImportFromMet(ref f);  // hand off importing nav data to the Nav object...

            //this.myMeta.AddNav(this.tag, nav); // added inside Nav instead

            if (_s_name.CompareTo("[None]") == 0)
                _s_name = "[none]";
        }

        override public void ExportToMet(ref FileLines f) // line# for msgs good
        {
            Nav tmp;

            try { tmp = _myMeta.GetNav(_tag); }
            catch (Exception e) { throw new MyException("" + GetType().Name.ToString() + ".ImportFromMet: Error. Unable to find Nav Tag '" + _tag + "'. [" + e.Message + "]"); }

            f.line.Add("ba");
            _idxInF_ExactCharCountNumber = f.line.Count;
            f.line.Add("FILL"); // <----- must fill in after the fact

            if (_s_name.CompareTo("[none]") == 0)
                f.line.Add("[None]"); // nav's in-game name
            else
                f.line.Add(_m_name); // nav's in-game name

            // nodes in nav
            f.line.Add(tmp.Count.ToString());
            {
                tmp.transform = _xf;
                tmp.ExportToMet(ref f);
            }
            // go back and fill in the exact char count ...
            _exactCharCountToAfterMetNAV_InclCrLf = 0;
            for (int i = _idxInF_ExactCharCountNumber + 1; i < f.line.Count; i++)
                _exactCharCountToAfterMetNAV_InclCrLf += f.line[i].Length + 2;
            f.line[_idxInF_ExactCharCountNumber] = _exactCharCountToAfterMetNAV_InclCrLf.ToString();
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good -- seems goods, anyway. after change.
        {
            my_metAFline = f.L;
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
            try
            {
                _tag = match.Groups["l"].Value;  // literals don't have delimiters
                _a_name = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                if (match.Groups["xf"].Success)
                {
                    Match xfMatch = rx.getParms["ENavXF"].Match(match.Groups["xf"].Value.Substring(1, match.Groups["xf"].Value.Length - 2));
                    if (!xfMatch.Success)
                        throw new MyException(rx.getInfo["ENavXF"]);
                    try
                    {
                        _xf[0] = double.Parse(xfMatch.Groups["a"].Value);
                        _xf[1] = double.Parse(xfMatch.Groups["b"].Value);
                        _xf[2] = double.Parse(xfMatch.Groups["c"].Value);
                        _xf[3] = double.Parse(xfMatch.Groups["d"].Value);
                        _xf[4] = double.Parse(xfMatch.Groups["e"].Value);
                        _xf[5] = double.Parse(xfMatch.Groups["f"].Value);
                        _xf[6] = double.Parse(xfMatch.Groups["g"].Value);
                    }
                    catch (Exception e)
                    {
                        throw new MyException(rx.getInfo["ENavXF"] + " [" + e.Message + "]");
                    }
                }
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
            _myMeta.AddNavCitationByAction(_tag, this);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _tag + " " + rx.oD + _a_name + rx.cD);
        }
    }

    public class ACallState : Action // line# for msgs good
    {
        private string _s_toState, _s_retState;
        public override ATypeID typeid { get { return ATypeID.CallState; } }
        public ACallState(int d) : base(d) { }
        private string _m_toState
        {
            set { _s_toState = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_toState); }
        }
        private string _a_toState
        {
            set { _s_toState = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_toState); }
        }
        private string _m_retState
        {
            set { _s_retState = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_retState); }
        }
        private string _a_retState
        {
            set { _s_retState = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_retState); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("st") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'st'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_toState = f.line[f.L++];
            //try { this._toState = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("ret") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ret'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_retState = f.line[f.L++];
            //try { this._retState = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("2");
            f.line.Add("s");
            f.line.Add("st");
            f.line.Add("s");
            f.line.Add(_m_toState);
            f.line.Add("s");
            f.line.Add("ret");
            f.line.Add("s");
            f.line.Add(_m_retState);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
            try
            {
                _a_toState = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2);  // length is at least 2; remove delimiters
                _a_retState = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_toState + rx.cD + " " + rx.oD + _a_retState + rx.cD);
        }
    }

    public class AReturn : Action // line# for msgs good
    {
        public override ATypeID typeid { get { return ATypeID.Return; } }
        public AReturn(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
        }
    }
    public class ADoExpr : Action // line# for msgs good
    {
        private string _s_expr;
        public override ATypeID typeid { get { return ATypeID.DoExpr; } }
        public ADoExpr(int d) : base(d) { _s_expr = ""; }
        private string _m_expr
        {
            set { _s_expr = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_expr); }
        }
        private string _a_expr
        {
            set { _s_expr = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_expr); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("1") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("e") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'e'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_expr = f.line[f.L++];
            //try { this._expr = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("1");
            f.line.Add("s");
            f.line.Add("e");
            f.line.Add("s");
            f.line.Add(_m_expr);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _a_expr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_expr);
            SpellIdsText = SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "";
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_expr + rx.cD + SpellIdsText);
        }
    }

    public class AChatExpr : Action // line# for msgs good
    {
        private string _s_chExpr;
        public override ATypeID typeid { get { return ATypeID.ChatExpr; } }
        public AChatExpr(int d) : base(d) { _s_chExpr = ""; }
        private string _m_chExpr
        {
            set { _s_chExpr = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_chExpr); }
        }
        private string _a_chExpr
        {
            set { _s_chExpr = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_chExpr); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("1") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("e") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'e'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_chExpr = f.line[f.L++];
            //try { this._chExpr = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("1");
            f.line.Add("s");
            f.line.Add("e");
            f.line.Add("s");
            f.line.Add(_m_chExpr);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _a_chExpr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_chExpr);
            SpellIdsText = SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "";
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_chExpr + rx.cD + SpellIdsText);
        }
    }

    public class ASetWatchdog : Action // line# for msgs good
    {
        private string _s_state;
        private double _range, _time;
        public override ATypeID typeid { get { return ATypeID.SetWatchdog; } }
        public ASetWatchdog(int d) : base(d) { }
        private string _m_state
        {
            set { _s_state = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_state); }
        }
        private string _a_state
        {
            set { _s_state = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_state); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("3") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '3'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_state = f.line[f.L++];
            //try { this._state = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("r") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _range = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("t") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 't'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _time = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("3");
            f.line.Add("s");
            f.line.Add("s");
            f.line.Add("s");
            f.line.Add(_m_state);
            f.line.Add("s");
            f.line.Add("r");
            f.line.Add("d");
            f.line.Add(_range.ToString());
            f.line.Add("s");
            f.line.Add("t");
            f.line.Add("d");
            f.line.Add(_time.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
            try
            {
                _range = double.Parse(match.Groups["d"].Value);
                _time = double.Parse(match.Groups["d2"].Value);
                _a_state = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _range.ToString() + " " + _time.ToString() + " " + rx.oD + _a_state + rx.cD);
        }
    }

    public class AClearWatchdog : Action // line# for msgs good
    {
        public override ATypeID typeid { get { return ATypeID.ClearWatchdog; } }
        public AClearWatchdog(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
        }
    }

    public class AGetOpt : Action // line# for msgs good
    {
        private string _s_opt, _s_var;
        public override ATypeID typeid { get { return ATypeID.GetOpt; } }
        public AGetOpt(int d) : base(d) { _s_opt = _s_var = ""; }
        private string _m_opt
        {
            set { _s_opt = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_opt); }
        }
        private string _a_opt
        {
            set { _s_opt = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_opt); }
        }
        private string _m_var
        {
            set { _s_var = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_var); }
        }
        private string _a_var
        {
            set { _s_var = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_var); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("o") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'o'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_opt = f.line[f.L++];
            //try { this._opt = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_var = f.line[f.L++];
            //try { this._var = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("2");
            f.line.Add("s");
            f.line.Add("o");
            f.line.Add("s");
            f.line.Add(_m_opt);
            f.line.Add("s");
            f.line.Add("v");
            f.line.Add("s");
            f.line.Add(_m_var);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
            try
            {
                _a_opt = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                _a_var = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_opt + rx.cD + " " + rx.oD + _a_var + rx.cD);
        }
    }

    public class ASetOpt : Action // line# for msgs good
    {
        private string _s_opt, _s_expr;
        public override ATypeID typeid { get { return ATypeID.SetOpt; } }
        public ASetOpt(int d) : base(d) { _s_opt = _s_expr = ""; }
        private string _m_opt
        {
            set { _s_opt = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_opt); }
        }
        private string _a_opt
        {
            set { _s_opt = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_opt); }
        }
        private string _m_expr
        {
            set { _s_expr = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_expr); }
        }
        private string _a_expr
        {
            set { _s_expr = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_expr); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("o") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'o'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_opt = f.line[f.L++];
            //try { this._opt = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_expr = f.line[f.L++];
            //try { this._expr = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("2");
            f.line.Add("s");
            f.line.Add("o");
            f.line.Add("s");
            f.line.Add(_m_opt);
            f.line.Add("s");
            f.line.Add("v");
            f.line.Add("s");
            f.line.Add(_m_expr);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try
            {
                _a_opt = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                _a_expr = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_expr);
            SpellIdsText = SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "";
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_opt + rx.cD + " " + rx.oD + _a_expr + rx.cD + SpellIdsText);
        }
    }

    public class ACreateView : Action // line# for msgs good
    {
        // For whatever reason, the XML field of the CreateView action fails to include a newline between it and whatever immediately follows it.
        public static List<int> breakitFixIndices = new List<int>();
        private string _s_viewName,_s_viewKey;
        private Meta _meta;


        public override ATypeID typeid { get { return ATypeID.CreateView; } }
        public ACreateView(int d, Meta m) : base(d) 
        {
            _s_viewName = _s_viewKey = ""; 
            _meta = m;
        }
        private string _m_viewName
        {
            set { _s_viewName = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_viewName); }
        }
        private string _a_viewName
        {
            set { _s_viewName = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_viewName); }
        }

        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        { // [LINE 188] ACreateView.ImportFromMet: File format error. Expected 20.
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_viewName = f.line[f.L++];
            //try { this._vw = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("x") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'x'.");
            if (f.line[f.L++].CompareTo("ba") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ba'.");
            int tmp;
            try { tmp = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (tmp != f.line[f.L].Length - 1)
            {
                // SPECIAL ACCOMMODATION: Instead of directly throwing an error, accommodate multi-line XML. (Even though this should never happen, it has been seen in some files.)
                int sum = f.line[f.L].Length;
                int nlines;
                for (nlines = 1; f.L + nlines < f.line.Count; nlines++)
                {
                    sum += 2 + f.line[f.L + nlines].Length; // 2 chars for a newline, and this line's length
                    if (sum >= tmp)
                        break;
                }
                if (tmp != sum - 1)
                    throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected line length of at least " + (f.line[f.L].Length - 1).ToString() + " characters.");

                // Collapse the XML multi-lines into one XML line
                if (nlines > 0)
                    f.offset += nlines - 1; // account for collapsing lines so 'files lines' remain correct in error messages (remember split-off of 's' below)
                while (nlines > 0)
                {
                    f.line[f.L + nlines - 1] += f.line[f.L + nlines];
                    f.line.RemoveAt(f.L + nlines);
                    nlines--;
                }
            }

            ///// Side trip to deal with the CreateView "bug" (XML line has more on it than it should.) /////
            int r = f.line.Count;
            f.line.Add(f.line[r - 1]); // duplicate the final line
            for (; r > f.L + 1; r--)   // move all lines down, back to just below the XML line
                f.line[r] = f.line[r - 1];
            f.line[r] = f.line[f.L].Substring(Math.Max(f.line[f.L].Length - 1, 0), 1); // chop apart the XML line ...
            f.line[f.L] = f.line[f.L].Substring(0, f.line[f.L].Length - 1);            // ... since it has more on it than it should

            string xml = f.line[f.L++];
            
            //try { this._xml = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + ".ImportFromMet: " + e.Message); }

            

            //Parse xml string and create view data structure

#if (!_DBG_)
            try
#endif
            {
                var metaView = new MetaView(_meta);
                metaView.viewId = _meta.GenerateUniqueViewTag(_s_viewName);
                if (xml.Length > 0)
                {
                    metaView.FromXml(xml);
                }
                else
                {
                    
                    //add empty view to meta
                    _meta.AddView(metaView.viewId, metaView);
                }
                _s_viewKey = metaView.viewId;
            }
#if (!_DBG_)
           catch (Exception ex)
            {
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Invlaid XML " + xml);
            }
#endif






        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("2");
            f.line.Add("s");
            f.line.Add("n"); //"o"
            f.line.Add("s");
            f.line.Add(_m_viewName);
            f.line.Add("s");
            f.line.Add("x"); // "v"
            f.line.Add("ba"); // "s"
            MetaView view = _meta.GetView(_s_viewKey);
            string xmlOut = view.toMetXml();
            
            f.line.Add(xmlOut.Length.ToString()); // nothing??
            f.line.Add(xmlOut);
            breakitFixIndices.Add(f.line.Count - 1); // For dealing with the CreateView "bug"
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
            try
            {
                _a_viewName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                _s_viewKey = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters

                // check if external XML file...
                if (_s_viewKey.Length > 0 && _s_viewKey[0] == ':')
                {
                    string fname = _s_viewKey.Substring(1).Trim();
                    if (System.IO.File.Exists(System.IO.Path.Join(f.path, fname))) // relative path ?
                        fname = System.IO.Path.Join(f.path, fname);
                    else if (!System.IO.File.Exists(fname)) // not absolute path either ?
                        throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: External file not found. (" + rx.getInfo[typeid.ToString()] + ")");

                    string acc = "";
                    string tmpLine;
                    System.IO.StreamReader file = new System.IO.StreamReader(fname);
                    while ((tmpLine = file.ReadLine()) != null)
                        acc += tmpLine;//.TrimEnd();
                    file.Close();

                    // Slightly altered _S regex string (replacing open/close delimiters with just start/end of string
                    //					string xmlREstr = @"^\" + rx.oD + @"[^\" + rx.oD + @"]|[^\" + rx.oD + @"]\" + rx.oD + @"[^\" + rx.oD + @"]|[^\" + rx.oD + @"]\" + rx.oD + @"$|^\" + rx.cD + @"[^\" + rx.cD + @"]|[^\" + rx.cD + @"]\" + rx.cD + @"[^\" + rx.cD + @"]|[^\" + rx.cD + @"]\" + rx.cD + @"$";
                    Match xmlStrMatch = new Regex(@"^([^\" + rx.oD + @"\" + rx.cD + @"]|\" + rx.oD + @"\" + rx.oD + @"|\" + rx.cD + @"\" + rx.cD + @")*$", RegexOptions.Compiled).Match(acc);
                    if (!xmlStrMatch.Success) // if not-doubled-up string delimiter found in XML file, throw exception
                        throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: External XML file still must conform to metaf string restrictions, with the exception of newline characters being allowed. Initial/terminal string delimiters, " + rx.oD + " and " + rx.cD + ", should be omitted, but all internal ones must be doubled-up. (" + rx.getInfo[typeid.ToString()] + ")");

                    string xml = acc;
                    var metaView = new MetaView(_meta);
                    metaView.viewId = _meta.GenerateUniqueViewTag(_s_viewName);
                    if (xml.Length > 0)
                    {
                        metaView.FromXml(xml);
                    }
                    else
                    {

                        //add empty view to meta
                        _meta.AddView(metaView.viewId, metaView);
                    }
                    _s_viewKey = metaView.viewId;
                }
                else if (_s_viewKey.Length > 0 && _s_viewKey[0] == '<')
                {
                    string xml = _s_viewKey;
                    var metaView = new MetaView(_meta);
                    metaView.viewId = _meta.GenerateUniqueViewTag(_s_viewName);
                    if (xml.Length > 0)
                    {
                        metaView.FromXml(xml);
                    }
                    else
                    {

                        //add empty view to meta
                        _meta.AddView(metaView.viewId, metaView);
                    }
                    _s_viewKey = metaView.viewId;
                }
            }
            catch (MyException e) { throw new MyException(e.Message); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            //f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_viewName + rx.cD + " " + rx.oD + _a_view_xml + rx.cD);
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_viewName + rx.cD + " " + rx.oD + _s_viewKey + rx.cD);
        }
    }

    public class ADestroyView : Action // line# for msgs good
    {
        private string _s_vw;
        public override ATypeID typeid { get { return ATypeID.DestroyView; } }
        public ADestroyView(int d) : base(d) { _s_vw = ""; }
        private string _m_vw
        {
            set { _s_vw = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_vw); }
        }
        private string _a_vw
        {
            set { _s_vw = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_vw); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("1") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_vw = f.line[f.L++];
            //try { this._vw = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("1");
            f.line.Add("s");
            f.line.Add("n");
            f.line.Add("s");
            f.line.Add(_m_vw);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _a_vw = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + " " + rx.oD + _a_vw + rx.cD);
            //			f.line.Add(new String('\t', this.depth) + "" + this.GetType().Name.ToString() + " " + rx.oD + this._a_vw + rx.cD);
        }
    }

    public class ADestroyAllViews : Action // line# for msgs good
    {
        public override ATypeID typeid { get { return ATypeID.DestroyAllViews; } }
        public ADestroyAllViews(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("TABLE") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
            if (f.line[f.L++].CompareTo("2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
            if (f.line[f.L++].CompareTo("k") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
            if (f.line[f.L++].CompareTo("v") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("k");
            f.line.Add("v");
            f.line.Add("n");
            f.line.Add("n");
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
        }
    }
}
