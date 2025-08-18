// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class Rx
    {

        public const string oD = "{"; // opening string delimiter
        public const string cD = "}"; // closing string delimiter
        public const string LC = "~~"; // Line-Comment sequence (changing this would require find/replace-all inside the embedded doc text, and updating the four methods directly below and the _S regex a few lines lower)

        //RULES:
        //	1. INTERNALLY STORE A VALID METAF STRING (minus delimiters).
        //	2. MET Import/Export
        //		a. set: expand, set
        //		b. get: shrink, return
        //	3. METAF Import/Export
        //		a. set: enforce {set | throw} <<< this is taken care of by the regex itself matching, or not
        //		b. get: return
        static public string a_SetStr(string s) { return s; } // metAF set: enforce the rules, but... the regex should already be doing that
        static public string a_GetStr(string s) { return s; } // metAF get: just return the string
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

        // "Core" regexes
        public static string __2EOL = @"\s*(" + LC + @".*)?$"; // "to end of line": from somewhere not necessarily the line's start, it is nothing but whitespace/line-comment content to the end
        public static Regex R__2EOL = new(__2EOL, RegexOptions.Compiled);
        public static Regex R__LN = new(@"^\s*(" + LC + @".*)?$", RegexOptions.Compiled); // effectively empty line (whitespace/line-comment only)
        public static Regex R_Empty = new(@"^$", RegexOptions.Compiled); // empty line

        public const string _D = @"[+\-]?(([1-9][0-9]*\.|[0-9]?\.)([0-9]+([eE][+\-]?[0-9]+)|[0-9]+)|([1-9][0-9]*|0))"; // double
        public const string _I = @"[+\-]?([1-9][0-9]*|0)"; // int
        public const string _H = @"[A-F0-9]{8}"; // hex
        // _Sbase is        ([^oc~]|oo|cc)
        // _S overall is    o   _Sbase*   (~(\\~)*_Sbase+)*   (~(\\~)*|)   c
		//private const string _Sbase = @"([^\" + Rx.oD + @"\" + Rx.cD + @"~]|\" + Rx.oD + @"\" + Rx.oD + @"|\" + Rx.cD + @"\" + Rx.cD + @")"; // enable chained/escaped tildes
		//public const string _S = @"\" + Rx.oD + _Sbase+@"*(~(\\~)*" + _Sbase + @"*)*(~(\\~)*|)\" + Rx.cD; // enable chained/escaped tildes
        public const string _S = @"[\" + oD + @"]([^\" + oD + @"\" + cD + @"]|\" + oD + @"\" + oD + @"|\" + cD + @"\" + cD + @")*[\" + cD + @"]"; // string
        public const string _L = @"[a-zA-Z_][a-zA-Z0-9_]*"; // literal	// @"(?<l> _____ |"+Rx.fieldEmpty+")"

        private static Dictionary<string, string> typeInfo = new()
        {
            ["_D"] = "Doubles are decimal numbers.",
            ["_I"] = "Integers are whole numbers.",
            ["_S"] = "Strings must be enclosed in " + oD + (oD.CompareTo(cD) != 0 ? " " + cD : "") + @" delimiters; any inside them must be escaped by doubling, i.e., single " + oD + @" is not allowed inside metaf strings, and " + oD + oD + @" in metaf results in " + oD + @" in met" + (oD.CompareTo(cD) != 0 ? @" (same for " + cD + ")" : "") + @". Different strings require at least one whitespace character between their delimiters, separating them.",
            //_H omitted
            ["_L"] = "A literal starts with a letter or underscore, followed by letters, digits, or underscores; no whitespace, and no string delimiters (" + oD + (oD.CompareTo(cD) != 0 ? cD : "") + ")!"
        };

        public static Dictionary<string, Regex> getLeadIn = new()
        {
            ["StateIfDoNav"] = new(@"^(?<tabs>[\t]*)(?<type>STATE\:|IF\:|DO\:|NAV\:)", RegexOptions.Compiled),
            ["AnyConditionOp"] = new(@"^(?<tabs>[\t]*)\s*(?<op>Never|Always|All|Any|ChatMatch|MainSlotsLE|SecsInStateGE|NavEmpty|Death|VendorOpen|VendorClosed|ItemCountLE|ItemCountGE|MobsInDist_Name|MobsInDist_Priority|NeedToBuff|NoMobsInDist|BlockE|CellE|IntoPortal|ExitPortal|Not|PSecsInStateGE|SecsOnSpellGE|BuPercentGE|DistToRteGE|Expr|ChatCapture)", RegexOptions.Compiled),
            ["AnyActionOp"] = new(@"^(?<tabs>[\t]*)\s*(?<op>None|SetState|DoAll|EmbedNav|CallState|Return|DoExpr|ChatExpr|Chat|SetWatchdog|ClearWatchdog|GetOpt|SetOpt|CreateView|DestroyView|DestroyAllViews)", RegexOptions.Compiled),
            ["AnyNavNodeType"] = new(@"^\t(?<type>flw|pnt|prt|rcl|pau|cht|vnd|ptl|tlk|chk|jmp)", RegexOptions.Compiled),
            ["GuessOpSwap"] = new(@"^\t(?<op>DoAll|DoExpr|All|Expr)", RegexOptions.Compiled) // I expect All and Expr to often be accidentally used instead of DoAll and DoExpr; help the users out...
        };

        public static Dictionary<string, Regex> getParms = new()
        {
            ["STATE:"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["NAV:"] = new(@"^\s+(?<l>" + _L + @")\s+(?<l2>circular|linear|once|follow)$", RegexOptions.Compiled),

            ["Never"] = R_Empty,
            ["Always"] = R_Empty,
            ["All"] = R_Empty,
            ["Any"] = R_Empty,
            ["ChatMatch"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MainSlotsLE"] = new(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["SecsInStateGE"] = new(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["NavEmpty"] = R_Empty,
            ["Death"] = R_Empty,
            ["VendorOpen"] = R_Empty,
            ["VendorClosed"] = R_Empty,
            ["ItemCountLE"] = new(@"^\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ItemCountGE"] = new(@"^\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MobsInDist_Name"] = new(@"^\s+(?<i>" + _I + @")\s+(?<d>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["MobsInDist_Priority"] = new(@"^\s+(?<i>" + _I + @")\s+(?<d>" + _D + @")\s+(?<i2>" + _I + ")$", RegexOptions.Compiled),
            ["NeedToBuff"] = R_Empty,
            ["NoMobsInDist"] = new(@"^\s+(?<d>" + _D + ")$", RegexOptions.Compiled),
            ["BlockE"] = new(@"^\s+(?<h>" + _H + ")$", RegexOptions.Compiled),
            ["CellE"] = new(@"^\s+(?<h>" + _H + ")$", RegexOptions.Compiled),
            ["IntoPortal"] = R_Empty,
            ["ExitPortal"] = R_Empty,
            //["Not"] = CTypeID.Not,
            ["PSecsInStateGE"] = new(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["SecsOnSpellGE"] = new(@"^\s+(?<i>" + _I + @")\s+(?<i2>" + _I + ")$", RegexOptions.Compiled),
            ["BuPercentGE"] = new(@"^\s+(?<i>" + _I + ")$", RegexOptions.Compiled),
            ["DistToRteGE"] = new(@"^\s+(?<d>" + _D + ")$", RegexOptions.Compiled),
            ["Expr"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ChatCapture"] = new(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),

            ["None"] = R_Empty,
            ["SetState"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["Chat"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["DoAll"] = R_Empty,
            ["EmbedNav"] = new(@"^\s+(?<l>" + _L + @")\s+(?<s>" + _S + @")(\s+(?<xf>" + _S + @"))?$", RegexOptions.Compiled),
            ["CallState"] = new(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["Return"] = R_Empty,
            ["DoExpr"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ChatExpr"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["SetWatchdog"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ClearWatchdog"] = R_Empty,
            ["GetOpt"] = new(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["SetOpt"] = new(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["CreateView"] = new(@"^\s+(?<s>" + _S + @")\s+(?<s2>" + _S + ")$", RegexOptions.Compiled),
            ["DestroyView"] = new(@"^\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["DestroyAllViews"] = R_Empty,

            ["flw"] = new(@"^\s+(?<h>" + _H + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["pnt"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")$", RegexOptions.Compiled),
            ["prt"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<h>" + _H + @")$", RegexOptions.Compiled),
            ["rcl"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["pau"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + ")$", RegexOptions.Compiled),
            ["cht"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["vnd"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<h>" + _H + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["ptl"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<d5>" + _D + @")\s+(?<d6>" + _D + @")\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["tlk"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<d5>" + _D + @")\s+(?<d6>" + _D + @")\s+(?<i>" + _I + @")\s+(?<s>" + _S + ")$", RegexOptions.Compiled),
            ["chk"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")$", RegexOptions.Compiled),
            ["jmp"] = new(@"^\s+(?<d>" + _D + @")\s+(?<d2>" + _D + @")\s+(?<d3>" + _D + @")\s+(?<d4>" + _D + @")\s+(?<s>" + _S + @")\s+(?<d5>" + _D + ")$", RegexOptions.Compiled),

            ["ENavXF"] = new(@"^\s*(?<a>" + _D + @")\s+(?<b>" + _D + @")\s+(?<c>" + _D + @")\s+(?<d>" + _D + @")\s+(?<e>" + _D + @")\s+(?<f>" + _D + @")\s+(?<g>" + _D + @")\s*$", RegexOptions.Compiled)
        };

        public static Dictionary<string, string> getInfo = new()
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
}