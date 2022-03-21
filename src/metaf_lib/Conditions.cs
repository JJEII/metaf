/*
metaf is a powerful meta/nav editor in an alternate format from that used by the VirindiTank addon to the game Asheron's Call.
Copyright (C) 2020  J. Edwards

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
==================================================================================================================================

This is a C# Console Application, created in Visual Studio 2019.

Yes, I know this code is horribly ugly and breaks all kinds of style and design rules (intermixing and violating object boundaries
and independence and reusability, inconsistent/inappropriate variable naming, lack of code readability in many different ways, one
monolithic file instead of multiple file breakouts, etc.).

I wrote it while simultaneously, in effect, hacking the .met data format to figure out what it was I was actually reading/writing,
and how to do it. The "alternate format" language also evolved extensively throughout this program's creation. I progressed
through the development process one .met operation after another, individually decyphering the data formatting, then kludging
together the required code as I went. The result of this is code that is ... eww. (Although, the actual functionality the program
provides is, in my opinion, much better than other available tools.)

At any rate, it should probably go without saying that if all the data-format information were readily available upfront, this
code would be different. But, I was just trying to produce something that would work, regardless of how ugly, and I have no plans
to re-write it "properly" from scratch.

~ J. Edwards, aka Eskarina of Morningthaw/Coldeve



THIS FILE'S ORGANIZATION, ROUGHLY:
	* A bunch of miscellaneous stuff:
		- Command-line-relevant info
		- Enum definitions
		- "MyException" and "FileLines" public classes
		- Tons of important strings
			. Regexes and error messages
			. Huge output text strings (meta/nav headers, readme file, reference file)
		- An abstract "ImportExport" inherited public class
	* All the Condition operation public classes, in in-game order (starts with abstract inherited public class)
	* All the Action operation public classes, in in-game order (starts with abstract inherited public class)
	* All the NavNode public classes (starts with abstract inherited public class)
	* Nav public class
	* Rule public class
	* State public class
	* Meta public class
	* Main


Ideas for possible future items:
	d Improve docs for newbies (clearer drag/drop, metaf isn't an editor, multi-file conversion(?))
	* Utility Belt functions added to documentation and mark-up XMLs
	d Support external file references and content for "Create View" XML (auto flattened)
		- Also for including states/navs defined in external files??
	* Default "[None]" names for EmbeddedNavs ??
	* Config file? (in/out folder(s)? overwrite? multi-file? UB function support?)

	* USE: (?) capability (external/library file inclusion---navs, states, whatever)
		- Track file and line(s)
		- Remove STATE:-then-NAV: restriction
	* "Continue" lines (e.g., ending in \ (pre-comment))
	* Multi-line comments
	* metaf "meta instructions" (e.g., to load navs into UB lists instead of directly embedding them)
	* EmbedNav "reverse"

	D Sort of related: "metaf like" Loot Rule Editor?

0.7.3.2b -- added detection and error message for an obscure case (~30 lines from end of ADoAll.ImportFromMetAF)
0.7.3.3 -- fixed processing line vs file line misalignment by adding an offset variable all over the place
 */

//#define _DBG_
using System;
using MetAF.enums;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MetAF
{

	abstract public class ImportExport
	{
		abstract public void ImportFromMet(ref FileLines f);
		abstract public void ExportToMet(ref FileLines f);
		abstract public void ImportFromMetAF(ref FileLines f);
		abstract public void ExportToMetAF(ref FileLines f);
	}



	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION
	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION
	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION

	abstract public class Condition : ImportExport
    {
        abstract public CTypeID typeid { get; } // get { return CTypeID.Unassigned; } }
        private int _d;
        protected int depth { get { return _d; } set { _d = value; } }
        public Condition(int d) { depth = d; }
    }

    public class CUnassigned : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.Unassigned; } }
        public CUnassigned(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f)
        { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Should never get here."); }
        override public void ExportToMet(ref FileLines f)
        { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ExportToMet: Should never get here."); }
        override public void ImportFromMetAF(ref FileLines f)
        { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Should never get here."); }
        override public void ExportToMetAF(ref FileLines f)
        { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ExportToMetAF: Should never get here."); }
    }

    public class CNever : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.Never; } }
        public CNever(int d) : base(d) { }
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

    public class CAlways : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.Always; } }
        public CAlways(int d) : base(d) { }
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

    public class CAll : Condition // line# for msgs good
    {
        private int _count;
        private Rule _myRule;
        public override CTypeID typeid { get { return CTypeID.All; } }
        public List<Condition> condition;
        public CAll(int d, Rule r) : base(d)
        {
            condition = new List<Condition>();
            _myRule = r;
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            Condition tmpCond;
            CTypeID cID;
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
                try { cID = (CTypeID)int.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

                try { tmpCond = _myRule.GetCondition(cID, depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

                tmpCond.ImportFromMet(ref f); // <--- recurse
                condition.Add(tmpCond);
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
            f.line.Add(condition.Count.ToString());
            foreach (Condition c in condition)
            {
                f.line.Add("i");
                f.line.Add(((int)c.typeid).ToString());
                c.ExportToMet(ref f); // <--- recurse
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
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Hit end-of-file but expected a Condition operation, or start of an Action ('DO'). [" + rx.getInfo["STATE:"] + "]");

                // Found first non-"blank" line. Try to get an operation (don't advance lines yet)
                match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
                if (match.Success)
                {
                    if (match.Groups["type"].Value.CompareTo("DO:") == 0)
                    {
                        f.C = 0;
                        return;
                    }
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Missing Action ('DO:') part of Rule." + rx.getInfo[typeid.ToString()]);
                }

                // It better be a valid Condition op...
                match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L]);
                if (!match.Success)
                {
                    Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                    if (tmatch.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. " + rx.getInfo["Generic"]);
                }
                // It is.

                // How is it tabbed ?
                int nTabs = match.Groups["tabs"].Length;
                if (nTabs <= Rule.ConditionContentTabLevel)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside a Condition's All/Any operation. " + rx.getInfo[typeid.ToString()]);
                if (nTabs <= depth)
                {   // return, since now done with this operation
                    f.C = nTabs; // Math.Max(nTabs - 1, 0);
                    return;
                }
                if (nTabs > depth + 1) // error
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[typeid.ToString()]);

                // Here: #tabs does equal depth+1; try to import this op.
                Condition tmpCond;
                try { tmpCond = _myRule.GetCondition(_myRule.conditionStrToID[match.Groups["op"].Value], depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
                f.C = match.Groups["op"].Index + match.Groups["op"].Length;
                tmpCond.ImportFromMetAF(ref f); // <--- recurse
                condition.Add(tmpCond);
            }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
            foreach (Condition c in condition)
                c.ExportToMetAF(ref f); // <--- recurse
        }
    }

    public class CAny : Condition // line# for msgs good
    {
        private int _count;
        private Rule _myRule;
        public override CTypeID typeid { get { return CTypeID.Any; } }
        public List<Condition> condition;
        public CAny(int d, Rule r) : base(d)
        {
            condition = new List<Condition>();
            _myRule = r;
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            Condition tmpCond;
            CTypeID cID;
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
                try { cID = (CTypeID)int.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
                try { tmpCond = _myRule.GetCondition(cID, depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }
                tmpCond.ImportFromMet(ref f); // <--- recurse
                condition.Add(tmpCond);
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
            f.line.Add(condition.Count.ToString());
            foreach (Condition c in condition)
            {
                f.line.Add("i");
                f.line.Add(((int)c.typeid).ToString());
                c.ExportToMet(ref f); // <--- recurse
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
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Hit end-of-file but expected a Condition operation, or start of an Action. [" + rx.getInfo["STATE:"] + "]");

                // Found first non-"blank" line. Try to get an operation (don't advance lines yet)
                match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
                if (match.Success)
                {
                    if (match.Groups["type"].Value.CompareTo("DO:") == 0)
                    {
                        f.C = 0;
                        return;
                    }
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Missing Action ('DO:') part of Rule." + rx.getInfo[typeid.ToString()]);
                }

                // It better be a valid Condition op...
                match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L]);
                if (!match.Success)
                {
                    Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                    if (tmatch.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. " + rx.getInfo["Generic"]);
                }
                // It is.

                // How is it tabbed ?
                int nTabs = match.Groups["tabs"].Length;
                if (nTabs <= Rule.ConditionContentTabLevel)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside a Condition's All/Any operation. " + rx.getInfo[typeid.ToString()]);
                if (nTabs <= depth)
                {   // return, since now done with this operation
                    f.C = nTabs; // Math.Max(nTabs - 1, 0);
                    return;
                }
                if (nTabs > depth + 1) // error
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[typeid.ToString()]);

                // Here: #tabs does equal depth+1; try to import this op.
                Condition tmpCond;
                try { tmpCond = _myRule.GetCondition(_myRule.conditionStrToID[match.Groups["op"].Value], depth + 1); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
                f.C = match.Groups["op"].Index + match.Groups["op"].Length;
                tmpCond.ImportFromMetAF(ref f); // <--- recurse
                condition.Add(tmpCond);
            }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString());
            foreach (Condition c in condition)
                c.ExportToMetAF(ref f); // <--- recurse
        }
    }

    public class CChatMatch : Condition // line# for msgs good
    {
        private string _s_chat;
        public CChatMatch(int d) : base(d) { _s_chat = ""; }
        public override CTypeID typeid { get { return CTypeID.ChatMatch; } }

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
            //try{ this._chat = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }

        override public void ExportToMet(ref FileLines f)
        {
            //f.line.Add("i");
            //f.line.Add(((int)CTypeID.ChatMatch).ToString());
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
            _a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                                                                                                //try { this._chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length-2); } // length is at least 2; remove delimiters
                                                                                                //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_chat + rx.cD);
        }
    }

    public class CMainSlotsLE : Condition // line# for msgs good
    {
        private int _slots;
        public CMainSlotsLE(int d) : base(d) { _slots = 0; }
        public override CTypeID typeid { get { return CTypeID.MainSlotsLE; } }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _slots = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_slots.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _slots = int.Parse(match.Groups["i"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }

        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _slots.ToString());
        }
    }
    public class CSecsInStateGE : Condition // line# for msgs good
    {
        private int _seconds;
        public CSecsInStateGE(int d) : base(d) { _seconds = 0; }
        public override CTypeID typeid { get { return CTypeID.SecsInStateGE; } }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _seconds = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_seconds.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _seconds = int.Parse(match.Groups["i"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _seconds.ToString());
        }
    }

    public class CNavEmpty : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.NavEmpty; } }
        public CNavEmpty(int d) : base(d) { }
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

    public class CDeath : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.Death; } }
        public CDeath(int d) : base(d) { }
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

    public class CVendorOpen : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.VendorOpen; } }
        public CVendorOpen(int d) : base(d) { }
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

    public class CVendorClosed : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.VendorClosed; } }
        public CVendorClosed(int d) : base(d) { }
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

    public class CItemCountLE : Condition // line# for msgs good
    {
        private string _s_invItem;
        private int _invCount;
        public override CTypeID typeid { get { return CTypeID.ItemCountLE; } }
        public CItemCountLE(int d) : base(d) { }
        private string _m_invItem
        {
            set { _s_invItem = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_invItem); }
        }
        private string _a_invItem
        {
            set { _s_invItem = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_invItem); }
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
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_invItem = f.line[f.L++];
            //try { this._invItem = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("c") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _invCount = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
            f.line.Add("n");
            f.line.Add("s");
            f.line.Add(_m_invItem);
            f.line.Add("s");
            f.line.Add("c");
            f.line.Add("i");
            f.line.Add(_invCount.ToString());
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
                _invCount = int.Parse(match.Groups["i"].Value);
                _a_invItem = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _invCount.ToString() + " " + rx.oD + _a_invItem + rx.cD);
        }
    }

    public class CItemCountGE : Condition // line# for msgs good
    {
        private string _s_invItem;
        private int _invCount;
        public override CTypeID typeid { get { return CTypeID.ItemCountGE; } }
        public CItemCountGE(int d) : base(d) { }
        private string _m_invItem
        {
            set { _s_invItem = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_invItem); }
        }
        private string _a_invItem
        {
            set { _s_invItem = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_invItem); }
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
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_invItem = f.line[f.L++];
            //try { this._invItem = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("c") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _invCount = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
            f.line.Add("n");
            f.line.Add("s");
            f.line.Add(_m_invItem);
            f.line.Add("s");
            f.line.Add("c");
            f.line.Add("i");
            f.line.Add(_invCount.ToString());
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
                _invCount = int.Parse(match.Groups["i"].Value);
                _a_invItem = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _invCount.ToString() + " " + rx.oD + _a_invItem + rx.cD);
        }
    }

    public class CMobsInDist_Name : Condition // line# for msgs good
    {
        private string _s_regex;
        private int _count;
        private double _range;
        public override CTypeID typeid { get { return CTypeID.MobsInDist_Name; } }
        public CMobsInDist_Name(int d) : base(d) { }
        private string _m_regex
        {
            set { _s_regex = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_regex); }
        }
        private string _a_regex
        {
            set { _s_regex = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_regex); }
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
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 3.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("n") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_regex = f.line[f.L++];
            //try { this._regex = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("c") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _count = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("r") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _range = double.Parse(f.line[f.L++]); }
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
            f.line.Add("n");
            f.line.Add("s");
            f.line.Add(_m_regex);
            f.line.Add("s");
            f.line.Add("c");
            f.line.Add("i");
            f.line.Add(_count.ToString());
            f.line.Add("s");
            f.line.Add("r");
            f.line.Add("d");
            f.line.Add(_range.ToString());
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
                _count = int.Parse(match.Groups["i"].Value);
                _range = double.Parse(match.Groups["d"].Value);
                _a_regex = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _count.ToString() + " " + _range.ToString() + " " + rx.oD + _a_regex + rx.cD);
        }
    }

    public class CMobsInDist_Priority : Condition // line# for msgs good
    {
        private int _priority;
        private int _count;
        private double _range;
        public override CTypeID typeid { get { return CTypeID.MobsInDist_Priority; } }
        public CMobsInDist_Priority(int d) : base(d) { }
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
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 3.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("p") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'p'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _priority = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("c") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _count = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("r") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _range = double.Parse(f.line[f.L++]); }
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
            f.line.Add("p");
            f.line.Add("i");
            f.line.Add(_priority.ToString());
            f.line.Add("s");
            f.line.Add("c");
            f.line.Add("i");
            f.line.Add(_count.ToString());
            f.line.Add("s");
            f.line.Add("r");
            f.line.Add("d");
            f.line.Add(_range.ToString());
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
                _count = int.Parse(match.Groups["i"].Value);
                _range = double.Parse(match.Groups["d"].Value);
                _priority = int.Parse(match.Groups["i2"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _count.ToString() + " " + _range.ToString() + " " + _priority.ToString());
        }
    }

    public class CNeedToBuff : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.NeedToBuff; } }
        public CNeedToBuff(int d) : base(d) { }
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


    public class CNoMobsInDist : Condition // line# for msgs good
    {
        private double _range;
        public override CTypeID typeid { get { return CTypeID.NoMobsInDist; } }
        public CNoMobsInDist(int d) : base(d) { }
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
            if (f.line[f.L++].CompareTo("r") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _range = double.Parse(f.line[f.L++]); }
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
            f.line.Add("1");
            f.line.Add("s");
            f.line.Add("r");
            f.line.Add("d");
            f.line.Add(_range.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _range = double.Parse(match.Groups["d"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _range.ToString());
        }
    }

    public class CBlockE : Condition // line# for msgs good
    {
        private int _block;
        public CBlockE(int d) : base(d) { }
        public override CTypeID typeid { get { return CTypeID.BlockE; } }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _block = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_block.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _block = int.Parse(match.Groups["h"].Value, NumberStyles.HexNumber); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _block.ToString("X8"));
        }
    }

    public class CCellE : Condition // line# for msgs good
    {
        private int _cell;
        public override CTypeID typeid { get { return CTypeID.CellE; } }
        public CCellE(int d) : base(d) { }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _cell = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_cell.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _cell = int.Parse(match.Groups["h"].Value, NumberStyles.HexNumber); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _cell.ToString("X8"));
        }
    }

    public class CIntoPortal : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.IntoPortal; } }
        public CIntoPortal(int d) : base(d) { }
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

    public class CExitPortal : Condition // line# for msgs good
    {
        public override CTypeID typeid { get { return CTypeID.ExitPortal; } }
        public CExitPortal(int d) : base(d) { }
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

    public class CNot : Condition // line# for msgs good
    {
        private int _count_ignored;
        private Rule _myRule;
        public override CTypeID typeid { get { return CTypeID.Not; } }
        public Condition condition;
        public CNot(int d, Rule r) : base(d) { _myRule = r; }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            Condition tmpCond;
            CTypeID cID;
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
            try { _count_ignored = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

            if (_count_ignored != 1)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. 'Not' requires exactly one operand.");

            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected 'i'.");
            try { cID = (CTypeID)int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }

            try { tmpCond = _myRule.GetCondition(cID, depth); } // don't increment depth
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

            tmpCond.ImportFromMet(ref f); // <--- recurse

            condition = tmpCond;
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("TABLE");
            f.line.Add("2");
            f.line.Add("K");
            f.line.Add("V");
            f.line.Add("n");
            f.line.Add("n");

            f.line.Add("1");

            f.line.Add("i");

            f.line.Add(((int)condition.typeid).ToString());
            condition.ExportToMet(ref f);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            // assumes f.C already set...

            int starting_fC = f.C;
            int len = f.line[f.L].Length;
            //int len = Math.Max(f.line[f.L].Length - 1, 0);

            // Try to get the next op, right after Not
            Match match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L].Substring(Math.Min(f.C, len)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[typeid.ToString()]);

            // Succeeded. Import it.
            f.C = f.C + match.Groups["op"].Index + match.Groups["op"].Length;
            try { condition = _myRule.GetCondition(_myRule.conditionStrToID[match.Groups["op"].Value], depth); } // do not increase depth
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
            condition.ImportFromMetAF(ref f);

            f.C = starting_fC;
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            int ln = f.line.Count; // remember location to go back and insert the Not
            condition.ExportToMetAF(ref f);
            f.line[ln] = new string('\t', depth) + typeid.ToString() + " " + f.line[ln].TrimStart();
        }
    }

    public class CPSecsInStateGE : Condition // line# for msgs good
    {
        private int _seconds;
        public override CTypeID typeid { get { return CTypeID.PSecsInStateGE; } }
        public CPSecsInStateGE(int d) : base(d) { _seconds = 9999; }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _seconds = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_seconds.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _seconds = int.Parse(match.Groups["i"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _seconds.ToString());
        }
    }

    public class CSecsOnSpellGE : Condition // line# for msgs good
    {
        private int _spellID;
        private int _seconds;
        public override CTypeID typeid { get { return CTypeID.SecsOnSpellGE; } }
        public CSecsOnSpellGE(int d) : base(d) { } // no good value for spellID default, so ignore
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
            if (f.line[f.L++].CompareTo("sid") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'sid'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _spellID = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("sec") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'sec'.");
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _seconds = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
            f.line.Add("sid");
            f.line.Add("i");
            f.line.Add(_spellID.ToString());
            f.line.Add("s");
            f.line.Add("sec");
            f.line.Add("i");
            f.line.Add(_seconds.ToString());
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
                _seconds = int.Parse(match.Groups["i"].Value);
                _spellID = int.Parse(match.Groups["i2"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _seconds.ToString() + " " + _spellID.ToString() + " ~~" + OutputText.SpellIdText(_spellID));
        }
    }

    public class CBuPercentGE : Condition // line# for msgs good
    {
        private int _burden;
        public override CTypeID typeid { get { return CTypeID.BuPercentGE; } }
        public CBuPercentGE(int d) : base(d) { _burden = 100; }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
            try { _burden = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("i");
            f.line.Add(_burden.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _burden = int.Parse(match.Groups["i"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _burden.ToString());
        }
    }


    public class CDistToRteGE : Condition // line# for msgs good
    {
        private double _distance;
        public override CTypeID typeid { get { return CTypeID.DistToRteGE; } }
        public CDistToRteGE(int d) : base(d) { _distance = 0; }
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
            if (f.line[f.L++].CompareTo("dist") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'dist'.");
            if (f.line[f.L++].CompareTo("d") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
            try { _distance = double.Parse(f.line[f.L++]); }
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
            f.line.Add("1");
            f.line.Add("s");
            f.line.Add("dist");
            f.line.Add("d");
            f.line.Add(_distance.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[typeid.ToString()].Match(thisLN);
            //Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()]);

            try { _distance = double.Parse(match.Groups["d"].Value); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + _distance.ToString());
        }
    }

    public class CExpr : Condition // line# for msgs good
    {
        private string _s_expr;
        public override CTypeID typeid { get { return CTypeID.Expr; } }
        public CExpr(int d) : base(d) { _s_expr = "false"; }
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

    //
    // During my research, I found this listed somewhere as a type, but it's not on any in-game menus, so I guess it's something deprecated??) : CTypeID=27
    //
    //public class CClientDialogPopup : Condition
    //{
    //	public override CTypeID typeid { get { return CTypeID.ClientDialogPopup; } }
    //}

    public class CChatCapture : Condition // line# for msgs good
    {
        private string _s_regex;
        private string _s_colorIDlist;
        public override CTypeID typeid { get { return CTypeID.ChatCapture; } }
        public CChatCapture(int d) : base(d) { _s_regex = ""; _s_colorIDlist = ""; }
        private string _m_regex
        {
            set { _s_regex = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_regex); }
        }
        private string _a_regex
        {
            set { _s_regex = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_regex); }
        }
        private string _m_colorIDlist
        {
            set { _s_colorIDlist = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_colorIDlist); }
        }
        private string _a_colorIDlist
        {
            set { _s_colorIDlist = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_colorIDlist); }
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
            if (f.line[f.L++].CompareTo("p") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'p'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_regex = f.line[f.L++];
            //try { this._regex = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            if (f.line[f.L++].CompareTo("c") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
            _m_colorIDlist = f.line[f.L++];
            //try { this._colorIDlist = f.line[f.L++]; }
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
            f.line.Add("p");
            f.line.Add("s");
            f.line.Add(_m_regex);
            f.line.Add("s");
            f.line.Add("c");
            f.line.Add("s");
            f.line.Add(_m_colorIDlist);
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
                _a_regex = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                _a_colorIDlist = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[typeid.ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', depth) + typeid.ToString() + " " + rx.oD + _a_regex + rx.cD + " " + rx.oD + _a_colorIDlist + rx.cD);
        }
    }
}
