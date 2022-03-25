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
		- "MyException" and "FileLines" classes
		- Tons of important strings
			. Regexes and error messages
			. Huge output text strings (meta/nav headers, readme file, reference file)
		- An abstract "ImportExport" inherited class
	* All the Condition operation classes, in in-game order (starts with abstract inherited class)
	* All the Action operation classes, in in-game order (starts with abstract inherited class)
	* All the NavNode classes (starts with abstract inherited class)
	* Nav class
	* Rule class
	* State class
	* Meta class
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
using System.Collections.Generic;
using System.Text.RegularExpressions;

using MetAF.enums;




namespace MetAF
{

    public class rx
    {
        //RULES:
        //	1. INTERNALLY STORE A VALID METAF STRING (minus delimiters).
        //	2. MET Import/Export
        //		a. set: expand, set
        //		b. get: shrink, return
        //	3. METAF Import/Export
        //		a. set: enforce {set | throw} <<< this is taken care of by the regex itself matching, or not
        //		b. get: return
        static public string a_SetStr(string s) // metAF set: enforce the rules, but... the regex should already be doing that
        {
            return s;
        }
        static public string a_GetStr(string s) // metAF get: just return the string
        {
            return s;
        }
        static public string m_SetStr(string s) // met set: grow the string (oD --> oDoD and cD --> cDcD)
        {
            string t = new Regex(@"\" + oD).Replace(s, oD + oD);
            if (oD.CompareTo(cD) != 0)
                t = new Regex(@"\" + cD).Replace(t, cD + cD);
            return t;
        }
        static public string m_GetStr(string s) // met get: shrink the string (oDoD --> oD and cDcD --> cD)
        {
            string t = new Regex(@"\" + oD + @"\" + oD).Replace(s, oD);
            if (oD.CompareTo(cD) != 0)
                t = new Regex(@"\" + cD + @"\" + cD).Replace(t, cD);
            return t;
        }

        public const string oD = "{"; // opening string delimiter
        public const string cD = "}"; // closing string delimiter

        public static string __2EOL = @"\s*(~~.*)?$";//new Regex( , RegexOptions.Compiled);
        public static Regex R__2EOL = new Regex(__2EOL, RegexOptions.Compiled);
        public static Regex R__LN = new Regex(@"^\s*(~~.*)?$", RegexOptions.Compiled);
        public static Regex R_Empty = new Regex(@"^$", RegexOptions.Compiled);

        // "Core" regexes
        public const string _D = @"[+\-]?(([1-9][0-9]*\.|[0-9]?\.)([0-9]+([eE][+\-]?[0-9]+)|[0-9]+)|([1-9][0-9]*|0))";
        public const string _I = @"[+\-]?([1-9][0-9]*|0)";
        public const string _H = @"[A-F0-9]{8}";
        // [o]([^oc]|[oo]|[cc])*[c]
        public const string _S = @"[\" + oD + @"]([^\" + oD + @"\" + cD + @"]|\" + oD + @"\" + oD + @"|\" + cD + @"\" + cD + @")*[\" + cD + @"]";
        public const string _L = @"[a-zA-Z_][a-zA-Z0-9_]*"; // literal	// @"(?<l> _____ |"+rx.fieldEmpty+")"

        private static Dictionary<string, string> typeInfo = new Dictionary<string, string>()
        {
            ["_D"] = "Doubles are decimal numbers.",
            ["_I"] = "Integers are whole numbers.",
            ["_S"] = "Strings must be enclosed in " + oD + (oD.CompareTo(cD) != 0 ? " " + cD : "") + @" delimiters; any inside them must be escaped by doubling, i.e., single " + oD + @" is not allowed inside metaf strings, and " + oD + oD + @" in metaf results in " + oD + @" in met" + (oD.CompareTo(cD) != 0 ? @" (same for " + cD + ")" : "") + @". Different strings require at least one whitespace character between their delimiters, separating them.",
            //_H omitted
            ["_L"] = "A literal starts with a letter or underscore, followed by letters, digits, or underscores; no whitespace, and no string delimiters (" + oD + (oD.CompareTo(cD) != 0 ? cD : "") + ")!"
        };

        public static Dictionary<string, Regex> getLeadIn = new Dictionary<string, Regex>()
        {
            ["StateIfDoNav"] = new Regex(@"^(?<tabs>[\t]*)(?<type>STATE\:|IF\:|DO\:|NAV\:)", RegexOptions.Compiled),
            ["AnyConditionOp"] = new Regex(@"^(?<tabs>[\t]*)\s*(?<op>Never|Always|All|Any|ChatMatch|MainSlotsLE|SecsInStateGE|NavEmpty|Death|VendorOpen|VendorClosed|ItemCountLE|ItemCountGE|MobsInDist_Name|MobsInDist_Priority|NeedToBuff|NoMobsInDist|BlockE|CellE|IntoPortal|ExitPortal|Not|PSecsInStateGE|SecsOnSpellGE|BuPercentGE|DistToRteGE|Expr|ChatCapture)", RegexOptions.Compiled),
            ["AnyActionOp"] = new Regex(@"^(?<tabs>[\t]*)\s*(?<op>None|SetState|DoAll|EmbedNav|CallState|Return|DoExpr|ChatExpr|Chat|SetWatchdog|ClearWatchdog|GetOpt|SetOpt|CreateView|DestroyView|DestroyAllViews)", RegexOptions.Compiled),
            ["AnyNavNodeType"] = new Regex(@"^\t(?<type>flw|pnt|prt|rcl|pau|cht|vnd|ptl|tlk|chk|jmp)", RegexOptions.Compiled),
            ["GuessOpSwap"] = new Regex(@"^\t(?<op>DoAll|DoExpr|All|Expr)", RegexOptions.Compiled) // I expect All and Expr to often be accidentally used instead of DoAll and DoExpr; help the users out...
        };

        public static Dictionary<string, Regex> getParms = new Dictionary<string, Regex>()
        {
            ["STATE:"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["NAV:"] = new Regex(@"^\s+(?<l>" + _L + @")\s+(?<l2>circular|linear|once|follow)$", RegexOptions.Compiled),

            ["Never"] = R_Empty,
            ["Always"] = R_Empty,
            ["All"] = R_Empty,
            ["Any"] = R_Empty,
            ["ChatMatch"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MainSlotsLE"] = new Regex(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["SecsInStateGE"] = new Regex(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["NavEmpty"] = R_Empty,
            ["Death"] = R_Empty,
            ["VendorOpen"] = R_Empty,
            ["VendorClosed"] = R_Empty,
            ["ItemCountLE"] = new Regex(@"^\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ItemCountGE"] = new Regex(@"^\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MobsInDist_Name"] = new Regex(@"^\s+(?<i>" + _I + @")\s+(?<d>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MobsInDist_Priority"] = new Regex(@"^\s+(?<i>" + _I + @")\s+(?<d>" + _D + @")\s+(?<i2>" + _I + ")$", RegexOptions.Compiled),
            ["NeedToBuff"] = R_Empty,
            ["NoMobsInDist"] = new Regex(@"^\s+(?<d>" + _D + ")$", RegexOptions.Compiled),
            ["BlockE"] = new Regex(@"^\s+(?<h>" + _H + ")$", RegexOptions.Compiled),
            ["CellE"] = new Regex(@"^\s+(?<h>" + _H + ")$", RegexOptions.Compiled),
            ["IntoPortal"] = R_Empty,
            ["ExitPortal"] = R_Empty,
            //["Not"] = CTypeID.Not,
            ["PSecsInStateGE"] = new Regex(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["SecsOnSpellGE"] = new Regex(@"^\s+(?<i>" + _I + @")\s+(?<i2>" + _I + ")$", RegexOptions.Compiled),
            ["BuPercentGE"] = new Regex(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["DistToRteGE"] = new Regex(@"^\s+(?<d>" + _D + ")$", RegexOptions.Compiled),
            ["Expr"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ChatCapture"] = new Regex(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),

            ["None"] = R_Empty,
            ["SetState"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["Chat"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["DoAll"] = R_Empty,
            ["EmbedNav"] = new Regex(@"^\s+(?<l>" + _L + @")\s+(?<s>" + _S + @")(\s+(?<xf>" + _S + @"))?$", RegexOptions.Compiled),
            ["CallState"] = new Regex(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["Return"] = R_Empty,
            ["DoExpr"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ChatExpr"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["SetWatchdog"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ClearWatchdog"] = R_Empty,
            ["GetOpt"] = new Regex(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["SetOpt"] = new Regex(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["CreateView"] = new Regex(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["DestroyView"] = new Regex(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["DestroyAllViews"] = R_Empty,

            ["flw"] = new Regex(@"^\s+(?<h>" + _H + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["pnt"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")$", RegexOptions.Compiled),
            ["prt"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<h>" + _H + @")$", RegexOptions.Compiled),
            ["rcl"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["pau"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + ")$", RegexOptions.Compiled),
            ["cht"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["vnd"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<h>" + _H + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ptl"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<d5>" + _D + @")\s+(?<d6>" + _D + @")\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["tlk"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<d5>" + _D + @")\s+(?<d6>" + _D + @")\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["chk"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")$", RegexOptions.Compiled),
            ["jmp"] = new Regex(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<s>" + _S + @")\s+(?<d5>" + _D + ")$", RegexOptions.Compiled),

            ["ENavXF"] = new Regex(@"^\s*(?<a>" + _D + @")\s+(?<b>" + _D + @")\s+(?<c>" + _D + @")\s+(?<d>" + _D + @")\s+(?<e>" + _D + @")\s+(?<f>" + _D + @")\s+(?<g>" + _D + @")\s*$", RegexOptions.Compiled)
        };

        public static Dictionary<string, string> getInfo = new Dictionary<string, string>()
        {
            ["STATE:"] = "Required: 'STATE:' must be at the start of the line, followed by a string state name. (" + typeInfo["_S"] + ") Every state must contain at least one Rule (IF-DO pair) with proper tabbing in.",
            ["IF:"] = "Required: 'IF:' must be tabbed in once, followed by a Condition operation, on the same line.",
            ["DO:"] = "Required: 'DO:' must be tabbed in twice, followed by an Action operation, on the same line.",
            ["NAV:"] = "Required: 'NAV:' must be at the start of the line, followed by a literal tag and a literal nav type (circular, linear, once, or follow). (" + typeInfo["_L"] + ")",

            ["Generic"] = "Syntax error. (General tips: double-check tabbing and state/rule structure (IF-DO pairs?), and ensure there are no stray characters, and that you're using " + oD + (oD.CompareTo(cD) != 0 ? " " + cD : "") + @" string delimiters properly.)",

            ["Never"] = "'Never' requires: no inputs.",
            ["Always"] = "'Always' requires: no inputs.",
            ["All"] = "'All' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
            ["Any"] = "'Any' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
            ["ChatMatch"] = "'ChatMatch' requires one input: a string 'chat regex' to match. (" + typeInfo["_S"] + ")",
            ["MainSlotsLE"] = "'MainSlotsLE' requires one input: an integer slot-count. (" + typeInfo["_I"] + ")",
            ["SecsInStateGE"] = "'SecsInStateGE' requires one input: an integer time (in seconds). (" + typeInfo["_I"] + ")",
            ["NavEmpty"] = "'NavEmpty' requires: no inputs.",
            ["Death"] = "'Death' requires: no inputs.",
            ["VendorOpen"] = "'VendorOpen' requires: no inputs.",
            ["VendorClosed"] = "'VendorClosed' requires: no inputs.",
            ["ItemCountLE"] = "'ItemCountLE' requires two inputs: an integer item-count, a string item-name. (" + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["ItemCountGE"] = "'ItemCountGE' requires two inputs: an integer item-count, a string item-name. (" + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["MobsInDist_Name"] = "'MobsInDist_Name' requires three inputs: an integer monster-count, a double distance, a string monster-name. (" + typeInfo["_I"] + " " + typeInfo["_D"] + " " + typeInfo["_S"] + ")",
            ["MobsInDist_Priority"] = "'MobsInDist_Priority' requires three inputs: an integer monster-count, a double distance, an integer monster-priority. (" + typeInfo["_I"] + " " + typeInfo["_D"] + ")",
            ["NeedToBuff"] = "'NeedToBuff' requires: no inputs.",
            ["NoMobsInDist"] = "'NoMobsInDist' requires one input: a double distance. (" + typeInfo["_D"] + ")",
            ["BlockE"] = "'BlockE' requires one input: an integer landblock (expressed in hexidecimal). (" + typeInfo["_I"] + ")",
            ["CellE"] = "'CellE' requires one input: an integer landcell (expressed in hexidecimal). (" + typeInfo["_I"] + ")",
            ["IntoPortal"] = "'IntoPortal' requires: no inputs.",
            ["ExitPortal"] = "'ExitPortal' requires: no inputs.",
            ["Not"] = "'Not' requires: a following operation, on the same line.",
            ["PSecsInStateGE"] = "'PSecsInStateGE' requires one input: an integer burden. (" + typeInfo["_I"] + ")",
            ["SecsOnSpellGE"] = "'SecsOnSpellGE' requires two inputs: an integer time (in seconds), an integer SpellID. (" + typeInfo["_I"] + ")",
            ["BuPercentGE"] = "'BuPercentGE' requires one input: an integer burden. (" + typeInfo["_I"] + ")",
            ["DistToRteGE"] = "'DistToRteGE' requires one input: a double distance. (" + typeInfo["_D"] + ")",
            ["Expr"] = "'Expr' requires one input: a string 'code expression' to evaluate. (" + typeInfo["_S"] + ")",
            ["ChatCapture"] = "'ChatCapture' requires two inputs: a string 'chat regex' to match/capture, a string 'color ID list'. (" + typeInfo["_S"] + ")",

            ["None"] = "'None' requires: no inputs.",
            ["SetState"] = "'SetState' requires one input: a string state name. (" + typeInfo["_S"] + ")",
            ["Chat"] = "'Chat' requires one input: a string to send to chat. (" + typeInfo["_S"] + ")",
            ["DoAll"] = "'DoAll' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
            ["EmbedNav"] = "'EmbedNav' requires two inputs: a literal tag, a string name. It can also take an optional string input that must contain seven space-separated doubles that represent a mathematical transform of the nav points: a b c d e f g, where newX=aX+bY+e, newY=cX+dY+f, and newZ=Z+g.  (" + typeInfo["_L"] + " " + typeInfo["_S"] + " " + typeInfo["_D"] + ")",
            ["CallState"] = "'CallState' requires two inputs: a string 'go-to' state name, a string 'return-to' state name. (" + typeInfo["_S"] + ")",
            ["Return"] = "'Return' requires: no inputs.",
            ["DoExpr"] = "'DoExpr' requires one input: a string 'code expression' to evaluate. (" + typeInfo["_S"] + ")",
            ["ChatExpr"] = "'ChatExpr' requires one input: a string 'code expression' to evaluate then send as chat. (" + typeInfo["_S"] + ")",
            ["SetWatchdog"] = "'SetWatchdog' requires three inputs: a double distance, a double time (in seconds), a string state name. (" + typeInfo["_D"] + " " + typeInfo["_S"] + ")",
            ["ClearWatchdog"] = "'ClearWatchdog' requires: no inputs.",
            ["GetOpt"] = "'GetOpt' requires two inputs: a string VT-option, a string variable-name. (" + typeInfo["_S"] + ")",
            ["SetOpt"] = "'SetOpt' requires two inputs: a string VT-option, a string variable-name. (" + typeInfo["_S"] + ")",
            ["CreateView"] = "'CreateView' requires two inputs: a string view, a string XML (or XML file reference). (" + typeInfo["_S"] + ")",
            ["DestroyView"] = "'DestroyView' requires one input: a string view. (" + typeInfo["_S"] + ")",
            ["DestroyAllViews"] = "'DestroyAllViews' requires: no inputs.",

            ["flw"] = "'flw' requires two inputs: integer target GUID (in hexidecimal), string target name. (" + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["pnt"] = "'pnt' requires three inputs: double xyz-coordinates. (" + typeInfo["_D"] + ")",
            ["prt"] = "'prt' requires four inputs: double xyz-coordinates, integer portal GUID (in hexidecimal). (" + typeInfo["_D"] + " " + typeInfo["_I"] + ")",
            ["rcl"] = "'rcl' requires four inputs: double xyz-coordinates, string recall spell name (exact). (" + typeInfo["_D"] + " " + typeInfo["_S"] + ")\nRecognized recall spell names:\n* " + oD + "Primary Portal Recall" + cD + "\n* " + oD + "Secondary Portal Recall" + cD + "\n* " + oD + "Lifestone Recall" + cD + "\n* " + oD + "Lifestone Sending" + cD + "\n* " + oD + "Portal Recall" + cD + "\n* " + oD + "Recall Aphus Lassel" + cD + "\n* " + oD + "Recall the Sanctuary" + cD + "\n* " + oD + "Recall to the Singularity Caul" + cD + "\n* " + oD + "Glenden Wood Recall" + cD + "\n* " + oD + "Aerlinthe Recall" + cD + "\n* " + oD + "Mount Lethe Recall" + cD + "\n* " + oD + "Ulgrim's Recall" + cD + "\n* " + oD + "Bur Recall" + cD + "\n* " + oD + "Paradox-touched Olthoi Infested Area Recall" + cD + "\n* " + oD + "Call of the Mhoire Forge" + cD + "\n* " + oD + "Colosseum Recall" + cD + "\n* " + oD + "Facility Hub Recall" + cD + "\n* " + oD + "Gear Knight Invasion Area Camp Recall" + cD + "\n* " + oD + "Lost City of Neftet Recall" + cD + "\n* " + oD + "Return to the Keep" + cD + "\n* " + oD + "Rynthid Recall" + cD + "\n* " + oD + "Viridian Rise Recall" + cD + "\n* " + oD + "Viridian Rise Great Tree Recall" + cD + "\n* " + oD + "Celestial Hand Stronghold Recall" + cD + "\n* " + oD + "Radiant Blood Stronghold Recall" + cD + "\n* " + oD + "Eldrytch Web Stronghold Recall" + cD,
            ["pau"] = "'pau' requires four inputs: double xyz-coordinates, double pause time (in seconds). (" + typeInfo["_D"] + ")",
            ["cht"] = "'cht' requires four inputs: double xyz-coordinates, string recall spell name (exact). (" + typeInfo["_D"] + " " + typeInfo["_S"] + ")",
            ["vnd"] = "'vnd' requires five inputs: double xyz-coordinates, integer vendor GUID (in hexidecimal), string vendor name. (" + typeInfo["_D"] + " " + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["ptl"] = "'ptl' requires eight inputs: double xyz-coordinates, double xyz-coordinates of object, integer ObjectClass (portal=14, npc=37), string object name. (" + typeInfo["_D"] + " " + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["tlk"] = "'tlk' requires eight inputs: double xyz-coordinates, double xyz-coordinates of object, integer ObjectClass (npc=37), string object name. (" + typeInfo["_D"] + " " + typeInfo["_I"] + " " + typeInfo["_S"] + ")",
            ["chk"] = "'chk' requires three inputs: double xyz-coordinates. (" + typeInfo["_D"] + ")",
            ["jmp"] = "'jmp' requires six inputs: double xyz-coordinates, double heading (in degrees), string holdShift (" + oD + "True" + cD + " or " + oD + "False" + cD + "), double time-delay (in milliseconds). (" + typeInfo["_D"] + " " + typeInfo["_S"] + ")",

            ["ENavXF"] = "When present, the transform string input (third input) for EmbedNav must contain seven space-separated doubles, representing a mathematical transform of the nav points: a b c d e f g, where newX=aX+bY+e, newY=cX+dY+f, and newZ=Z+g."
        };
    }







    // REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER
    // REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER
    // REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER


    public class Rule// : ImportExport // line# for msgs good
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

        public Dictionary<string, CTypeID> conditionStrToID = new Dictionary<string, CTypeID>()
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

        public Dictionary<string, ATypeID> actionStrToID = new Dictionary<string, ATypeID>()
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
                case ATypeID.CreateView: return new ACreateView(d, _myMeta);
                case ATypeID.DestroyView: return new ADestroyView(d);
                case ATypeID.DestroyAllViews: return new ADestroyAllViews(d);
            }
            throw new MyException("Invalid Action Type ID integer.");
        }

        public string ImportFromMet(ref FileLines f) // line# for msgs good
        {
            CTypeID cID;
            ATypeID aID;

            // Read the condition type, and set-up the data structure for reading the data in a moment
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected 'i'.");
            try { cID = (CTypeID)int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }

            try { _condition = GetCondition(cID, ConditionContentTabLevel); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

            // Read the action type, and set-up the data structure for reading the data in a moment
            if (f.line[f.L++].CompareTo("i") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected 'i'.");
            try { aID = (ATypeID)int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }

            try { _action = GetAction(aID, ActionContentTabLevel); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

            // Read the condition data
            _condition.ImportFromMet(ref f);

            // Read the action data
            _action.ImportFromMet(ref f);

            // Read and return the state name
            if (f.line[f.L++].CompareTo("s") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] File format error. Expected 's'.");

            return f.line[f.L++]; // no need to check it for single internal string delimiters because it's checked for that upon return ///////////////////////
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
            f.line.Add(stateName);/////////////////////////////////
        }
        public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            Match match;


            // CONDITION

            // Find first non-"blank" line
            f.L--;
            while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                ;

            // Prematurely hit end of file
            if (f.L >= f.line.Count)
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Hit end-of-file but needed a Condition ('IF:' line).");

            // Found first non-"blank" line... "IF:" ?
            match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
            if (!match.Success
                || match.Groups["type"].Value.CompareTo("IF:") != 0
                || match.Groups["tabs"].Value.Length != ConditionContentTabLevel - 1
                )
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected Condition ('IF:' line). " + rx.getInfo["IF:"]);
            f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

            // Try to grab the Condition keyword
            match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L].Substring(f.C)); // don't advance line
            if (!match.Success)
            {
                Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                if (tmatch.Success)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["IF:"]);
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. " + rx.getInfo["IF:"]);
            }

            // Try to import this Condition
            try { _condition = GetCondition(conditionStrToID[match.Groups["op"].Value], ConditionContentTabLevel); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
            f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//, f.line[f.L].Length-1);
            _condition.ImportFromMetAF(ref f); // advances line inside


            // ACTION

            // Find first non-"blank" line
            f.L--;
            while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                ;

            // Prematurely hit end of file
            if (f.L >= f.line.Count)
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Hit end-of-file but needed a Rule Action ('DO:' line).");

            // Found first non-"blank" line... "DO:" ?
            match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
            if (!match.Success
                || match.Groups["type"].Value.CompareTo("DO:") != 0
                || match.Groups["tabs"].Value.Length != ActionContentTabLevel - 1
                )
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected Action ('DO:' line). " + rx.getInfo["DO:"]);
            f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

            // Try to grab the Action keyword
            match = rx.getLeadIn["AnyActionOp"].Match(f.line[f.L].Substring(f.C)); // don't advance line
            if (!match.Success)
            {
                Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                if (tmatch.Success)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["DO:"]);
                throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. " + rx.getInfo["DO:"]);
            }

            // Try to import this Action
            try { _action = GetAction(actionStrToID[match.Groups["op"].Value], ActionContentTabLevel); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }

            f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//=Math.Min(f.C+..., f.line[f.L].Length-1);
            _action.ImportFromMetAF(ref f); // advances line inside

            f.C = 0;
        }
        public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add(new string('\t', ConditionContentTabLevel - 1) + "IF:");
            _condition.ExportToMetAF(ref f);
            f.line.Add(new string('\t', ActionContentTabLevel - 1) + "DO:");
            _action.ExportToMetAF(ref f);
        }
    }

    public class State : ImportExport // line# for msgs good
    {
        private string _s_name;
        private Meta _myMeta;
        private List<Rule> _rule;
        public string name { get { return _s_name; } }
        public int ruleCount { get { return _rule.Count; } }
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
        public State(string name, Meta myM, bool isMetCalling_isNotMetAFCalling)
        {
            if (isMetCalling_isNotMetAFCalling)
                _m_name = name;
            else
                _a_name = name;
            //try { this._name = name; }
            //catch (Exception e) { throw new MyException("State.State: " + e.Message); }
            _rule = new List<Rule>();
            _myMeta = myM;
        }
        public void AddRule(Rule r)
        {
            _rule.Add(r);
        }
        override public void ImportFromMet(ref FileLines f) { throw new Exception("State.ImportFromMet: Don't ever call this function; use State's parameter-taking constructor instead."); }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (Rule r in _rule)
                r.ExportToMet(ref f, _m_name);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            Match match;
            int fLineAtStart = f.L;

            // loop this until EOF; break out on 'STATE:' or 'NAV:'
            while (f.L < f.line.Count)
            {
                // Find next non-"blank" line, or EOF
                f.L--;
                while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file; done
                if (f.L >= f.line.Count)
                    break;

                // Found first non-"blank" line... done reading Rules for this state ?  ("STATE:" or "NAV:" line ?)
                match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                if (!match.Success)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. " + rx.getInfo["STATE:"]);
                if (match.Groups["type"].Value.CompareTo("STATE:") == 0 || match.Groups["type"].Value.CompareTo("NAV:") == 0)
                    break;

                // Start of a new Rule ? ("IF:" line ?)
                if (match.Groups["type"].Value.CompareTo("IF:") != 0) // i.e., it must be a "DO:" line if !="IF:" since it matched StateIfDoNav, and State & Nav were already checked above
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. (Missing Condition for this Action?) " + rx.getInfo["STATE:"]);

                // It's an "IF:" line; try to import this Rule
                f.C = 0;
                Rule tmpRule = new Rule(_myMeta, this);
                tmpRule.ImportFromMetAF(ref f);
                _rule.Add(tmpRule);
            }

            if (_rule.Count == 0)
                throw new MyException("[LINE " + fLineAtStart.ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Every state must contain at least one Rule, even if it's just Never-None. [" + rx.getInfo["STATE:"] + "]");
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("STATE: " + rx.oD + _a_name + rx.cD + " ~~ {");
            foreach (Rule r in _rule)
                r.ExportToMetAF(ref f);
            f.line.Add("~~ }");
        }
    }
    public class Meta : ImportExport // line# for msgs good
    {
        private int _uniqueTagCounter;
        private static string[] _firstLines = { "1", "CondAct", "5", "CType", "AType", "CData", "AData", "State", "n", "n", "n", "n", "n" };
        private List<State> _state;                                     // all states that exist
        public List<State> States { 
            get { return _state; } 
            set { _state = value; }
        }
        private Dictionary<string, Nav> _nav;                           // all navs that exist, cited or not by Actions
        private Dictionary<string, MetaView> _views;                           // all navs that exist, cited or not by Actions

        private Dictionary<string, List<AEmbedNav>> _actionUsingNav;    // dictionary[tag] of: list of Actions actually using 'tag' nav (Action cites it, and nav exists)
        private Dictionary<string, List<AEmbedNav>> _actionCitesNav;    // dictionary[tag] of: list of Actions citing use of 'tag' nav
        private string _s_sn; // just a scratch 'state name' variable
        private bool _navOnly; 
        private bool _loadedNavOnly;
        private readonly bool _loadedMet;

        public bool IsNavOnly { get { return _navOnly; } }
        public bool LoadedNavOnly { get { return _loadedNavOnly; } }
        public bool IsMet { get { return _loadedMet; } }
        private string _m_sn
        {
            set { _s_sn = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_sn); }
        }
        private string _a_sn
        {
            set { _s_sn = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_sn); }
        }
        public Meta(bool loadedMet, bool loadedNavOnly = false)
        {
            _state = new List<State>();
            _nav = new Dictionary<string, Nav>();
            _views = new Dictionary<string, MetaView>();
            _uniqueTagCounter = 0;
            _actionUsingNav = new Dictionary<string, List<AEmbedNav>>();
            _actionCitesNav = new Dictionary<string, List<AEmbedNav>>();
            _loadedNavOnly = _navOnly = loadedNavOnly;
            _loadedMet = loadedMet;
        }

        public string GenerateUniqueNavTag()
        {
            return "nav" + _uniqueTagCounter++.ToString();
        }
        public string GenerateUniqueViewTag()
        {
            //this may be lazy, but... I don't wanna keep a counter, so, here we are.
            string baseName = "view";
            int indexSuffix = 0;
            while (_views.ContainsKey(baseName + indexSuffix.ToString()))
            {
                indexSuffix++;
            }
            return baseName + indexSuffix.ToString();
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
        // Used to add 'tag' Nav to the list of all those that exist, and to barf if a 'tag' Nav already exists
        public void AddNav(string tag, Nav nav)
        {
            if (_nav.ContainsKey(tag))
                throw new MyException("NAV already defined for tag '" + tag + "'.");
            _nav.Add(tag, nav);
        }
        // Used to find out if 'tag' Nav exists, and to return it, if so
        public Nav GetNav(string tag)
        {
            if (!_nav.ContainsKey(tag))
                throw new MyException("No NAV found with tag '" + tag + "'.");
            return _nav[tag];
        }


        public void AddView(string tag, MetaView view)
        {
            if (_views.ContainsKey(tag))
                throw new MyException("NAV already defined for tag '" + tag + "'.");
            _views.Add(tag, view);
        }
        // Used to find out if 'tag' Nav exists, and to return it, if so
        public MetaView GetView(string tag)
        {
            if (!_views.ContainsKey(tag))
                throw new MyException("No NAV found with tag '" + tag + "'.");
            return _views[tag];
        }


        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            if (!_navOnly)
            {
                // Intro lines
                foreach (string s in _firstLines)
                    if (s.CompareTo(f.line[f.L++]) != 0)
                        throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Unknown file type: First lines do not match expected format.");

                // Number of rules in file
                try { Rule.Count = uint.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Expected number of rules saved in file but didn't find that. [" + e.Message + "]"); }


                // Read all the rules, including embedded navs
                int nRules = 0;
                string prev_sn = null;
                State curState = null;// new State();
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
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] Unknown file type: First lines do not match expected format.");
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
                foreach (string s in _firstLines)
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
                    Console.WriteLine("WARNING: Multiple navroutes detected. A .nav file contains only one navroute. Ignoring all but one.");
                foreach (KeyValuePair<string, Nav> kv in _nav)
                {
                    kv.Value.ExportToMet(ref f);
                    break;
                }
            }
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            Match match;

            if (!_navOnly)
            {
                // loop until EOF or "NAV:" line found
                while (f.L < f.line.Count)
                {
                    // Find next non-"blank" line, or EOF
                    f.L--;
                    while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                        ;

                    // Hit end of file; done
                    if (f.L >= f.line.Count)
                        break;

                    // Found first non-"blank" line... done reading States for this meta ? ("NAV:" line ?)
                    match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                    if (!match.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["STATE:"]);
                    if (match.Groups["type"].Value.CompareTo("NAV:") == 0)
                        break;

                    // Start of new State ? ("STATE:" line ?)
                    if (match.Groups["type"].Value.CompareTo("STATE:") != 0)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["STATE:"]);

                    // Try to import this State
                    f.C = 6; // Math.Min(6, f.line[f.L].Length - 1);
                    string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(f.C), ""); // don't advance line
                    match = rx.getParms["STATE:"].Match(thisLN);
                    //f.C = Math.Min(6, f.line[f.L].Length - 1);
                    //match = rx.getParms["STATE:"].Match(f.line[f.L].Substring(f.C)); // don't advance line
                    if (!match.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. (Did you put a space between the colon and state name?) " + rx.getInfo["STATE:"]);

                    // Double check that this state name does not already exist
                    string tmpStr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // remove string delimiters from ends
                    foreach (State st in _state)
                        if (st.name.CompareTo(tmpStr) == 0)
                            throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] Meta.ImportFromMetAF: State names must be unique; the state name " + rx.oD + tmpStr + rx.cD + " is already in use.");

                    // Import this state's contents, and add it to the state list
                    State tmpState = new State(tmpStr, this, false); // tempStr is an "AF string"
                    f.C = 0;
                    f.L++;
                    tmpState.ImportFromMetAF(ref f);
                    _state.Add(tmpState);
                }
                if (_state.Count == 0)
                {
                    Console.WriteLine("[LINE " + (f.L + f.offset + 1).ToString() + "] Meta.ImportFromMetAF: WARNING: You defined no meta states. Handling as a nav-only file.");
                    _navOnly = true;
                    //	throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] Meta.ImportFromMetAF: You must define at least one state!");
                }
            }


            // NAVS

            // loop until EOF
            while (f.L < f.line.Count)
            {
                // Find next non-"blank" line, or EOF
                f.L--;
                while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Hit end of file; done
                if (f.L >= f.line.Count)
                    break;

                // Found first non-"blank" line... does it start with "NAV:" ? (It needs to.)
                match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                if (!match.Success || match.Groups["type"].Value.CompareTo("NAV:") != 0)
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["NAV:"]);

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
                            tmpStr += "[LINE " + (en.my_metAFline + 1).ToString() + "] Meta.ImportFromMetAF: Nav (" + TagEN.Key + ") cited for embedding but is never defined.";
                            addNewline = true;
                        }
                        throw new MyException(tmpStr);
                    }
                }

                // And now the opposite check, to see if all defined navs are actually being used (just issue a warning, though)
                foreach (KeyValuePair<string, Nav> en in _nav)
                    if (!_actionCitesNav.ContainsKey(en.Key))
                        Console.WriteLine("[LINE " + (en.Value.my_metAFftagline + 1).ToString() + "] WARNING: " + GetType().Name.ToString() + ".ImportFromMetAF: Nav tag (" + en.Key + ") is never used.");
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
                if (f.line[trail].Length >= strConditionCmp.Length && 0 == f.line[trail].Substring(0, Math.Min(strConditionCmp.Length, f.line[trail].Length)).CompareTo(strConditionCmp)
                     || f.line[trail].Length >= strActionCmp.Length && 0 == f.line[trail].Substring(0, Math.Min(strActionCmp.Length, f.line[trail].Length)).CompareTo(strActionCmp))
                    break;
                trail++;
            }
            lead = trail + 1;

            // if collapse is needed, collapse lead onto trail, then increment both counters, check lead<Count{copy lead into trail, increment lead}else{break}
            // else increment trail, copy lead into trail, increment lead
            while (lead < f.line.Count)
            {
                // if f.line[trail] "starts" with "IF:" or "DO:"
                if (0 == f.line[trail].Substring(0, Math.Min(strConditionCmp.Length, f.line[trail].Length)).CompareTo(strConditionCmp)
                     || 0 == f.line[trail].Substring(0, Math.Min(strActionCmp.Length, f.line[trail].Length)).CompareTo(strActionCmp))
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
                {
                    //f.line.Add("~~\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t");
                    s.ExportToMetAF(ref f);
                }
                CollapseIfDo(ref f);

                if (_nav.Count > 0)
                {
                    f.line.Add("");
                    f.line.Add("~~========================= ONLY NAVS APPEAR BELOW THIS LINE =========================~~");
                    f.line.Add("");

                    foreach (KeyValuePair<string, Nav> sn in _nav)
                        sn.Value.ExportToMetAF(ref f);
                }

                if (_views.Count > 0)
                {
                    f.line.Add("");
                    f.line.Add("~~========================= ONLY META VIEWS APPEAR BELOW THIS LINE =========================~~");
                    f.line.Add("");

                    foreach (KeyValuePair<string, MetaView> view in _views)
                        view.Value.ExportToMetAF(ref f);
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
