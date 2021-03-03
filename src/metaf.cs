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

	D Sort of related: "metaf like" Loot Rule Editor?
*/

//#define _DBG_

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;	// Needed to force .NET to behave properly in
using System.Threading;     // other countries, with decimal numbers.

namespace metaf
{
#if (_DBG_)
	class myDebug {
		public static string[] args = { "eskontrol.af" };
	}
#endif
	class CmdLnParms {
		public static string version = "METa Alternate Format (metaf), v.0.7.3.1     GPLv3 Copyright (C) 2021     J. Edwards";
		public static string newFileName = "__NEW__.af";
		public static string newnavFileName = "__NEWNAV__.af";
		public static string readmeFileName = "metafREADME.af";
		public static string refFileName = "metafReference.af";
	}

	public enum CTypeID {
		Unassigned = -1,
		Never = 0,
		Always = 1,
		All = 2,
		Any = 3,
		ChatMatch = 4,
		MainSlotsLE = 5,
		SecsInStateGE = 6,
		NavEmpty = 7,
		Death = 8,
		VendorOpen = 9,
		VendorClosed = 10,
		ItemCountLE = 11,
		ItemCountGE = 12,
		MobsInDist_Name = 13,
		MobsInDist_Priority = 14,
		NeedToBuff = 15,
		NoMobsInDist = 16,
		BlockE = 17,
		CellE = 18,
		IntoPortal = 19,
		ExitPortal = 20,
		Not = 21,
		PSecsInStateGE = 22,
		SecsOnSpellGE = 23,
		BuPercentGE = 24,
		DistToRteGE = 25,
		Expr = 26,
		//ClientDialogPopup = 27, // some type from the past? it's not in vt now.
		ChatCapture = 28
	};

	public enum ATypeID {
		Unassigned = -1,
		None = 0,
		SetState = 1,
		Chat = 2,
		DoAll = 3,
		EmbedNav = 4,
		CallState = 5,
		Return = 6,
		DoExpr = 7,
		ChatExpr = 8,
		SetWatchdog = 9,
		ClearWatchdog = 10,
		GetOpt = 11,
		SetOpt = 12,
		CreateView = 13,
		DestroyView = 14,
		DestroyAllViews = 15
	};

	public enum NavTypeID
	{
		Circular = 1,
		Linear = 2,
		Follow = 3,
		Once = 4
	};

	public enum M_NavTypeID // These parallel the NavTypeID list, but are used in the metaf file, for NAV types, by NAME, not by value
	{
		circular = 1,
		linear = 2,
		follow = 3,
		once = 4
	};

	public enum NTypeID
	{
		Follow = -2, // workaround = MY VALUE FOR THIS, not VTank's!
		Unassigned = -1,
		Point = 0,
		Portal = 1, // DEPRECATED Portal node (only has one set of coordinates instead of two (like "ptl" type has)
		Recall = 2,
		Pause = 3,
		Chat = 4,
		OpenVendor = 5,
		Portal_NPC = 6,
		NPCTalk = 7,
		Checkpoint = 8,
		Jump = 9
		//Other = 99 // defined in VTank source
	};

	public enum M_NTypeID // These parallel the NTypeID list, but are used in the metaf file, for NAV node types, by NAME, not by value
	{
		flw = -2,
		Unassigned = -1,
		pnt = 0,
		prt = 1, // DEPRECATED Portal node (only has one set of coordinates instead of two (like "ptl" type has)
		rcl = 2,
		pau = 3,
		cht = 4,
		vnd = 5,
		ptl = 6,
		tlk = 7,
		chk = 8,
		jmp = 9
		// otr = 99 // "Other" is in VTank source
	};

	[global::System.Serializable]
	public class MyException : Exception
	{
		public MyException() { }
		public MyException(string message) : base(message) { }
		public MyException(string message, Exception inner) : base(message, inner) { }
		protected MyException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
	}

	public class FileLines
	{
		public int L;
		public int C;
		public string path;
		public List<string> line;
		public FileLines() { this.L = this.C = 0; this.line = new List<string>(); }
		public void GoToStart() { this.L = this.C = 0; }
		public void Clear() { this.L = this.C = 0; this.line.Clear(); }
	}

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
			string t = new Regex(@"\" + rx.oD).Replace(s, rx.oD + rx.oD);
			if (rx.oD.CompareTo(rx.cD) != 0)
				t = new Regex(@"\" + rx.cD).Replace(t, rx.cD + rx.cD);
			return t;
		}
		static public string m_GetStr(string s) // met get: shrink the string (oDoD --> oD and cDcD --> cD)
		{
			string t = new Regex(@"\" + rx.oD + @"\" + rx.oD).Replace(s, rx.oD);
			if (rx.oD.CompareTo(rx.cD) != 0)
				t = new Regex(@"\" + rx.cD + @"\" + rx.cD).Replace(t, rx.cD);
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
		public const string _S = @"[\" + rx.oD + @"]([^\" + rx.oD + @"\" + rx.cD + @"]|\" + rx.oD + @"\" + rx.oD + @"|\" + rx.cD + @"\" + rx.cD + @")*[\" + rx.cD + @"]";
		public const string _L = @"[a-zA-Z_][a-zA-Z0-9_]*"; // literal	// @"(?<l> _____ |"+rx.fieldEmpty+")"

		private static Dictionary<string, string> typeInfo = new Dictionary<string, string>()
		{
			["_D"] = "Doubles are decimal numbers.",
			["_I"] = "Integers are whole numbers.",
			["_S"] = "Strings must be enclosed in " + rx.oD + (rx.oD.CompareTo(rx.cD) != 0 ? " " + rx.cD : "") + @" delimiters; any inside them must be escaped by doubling, i.e., single " + rx.oD + @" is not allowed inside metaf strings, and " + rx.oD + rx.oD + @" in metaf results in " + rx.oD + @" in met" + (rx.oD.CompareTo(rx.cD) != 0 ? @" (same for " + rx.cD + ")" : "") + @". Different strings require at least one whitespace character between their delimiters, separating them.",
			//_H omitted
			["_L"] = "A literal starts with a letter or underscore, followed by letters, digits, or underscores; no whitespace, and no string delimiters (" + rx.oD + (rx.oD.CompareTo(rx.cD) != 0 ? rx.cD : "") + ")!"
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
			["STATE:"] = "Required: 'STATE:' must be at the start of the line, followed by a string state name. (" + rx.typeInfo["_S"] + ") Every state must contain at least one Rule (IF-DO pair) with proper tabbing in.",
			["IF:"] = "Required: 'IF:' must be tabbed in once, followed by a Condition operation, on the same line.",
			["DO:"] = "Required: 'DO:' must be tabbed in twice, followed by an Action operation, on the same line.",
			["NAV:"] = "Required: 'NAV:' must be at the start of the line, followed by a literal tag and a literal nav type (circular, linear, once, or follow). (" + rx.typeInfo["_L"] + ")",

			["Generic"] = "Syntax error. (General tips: double-check tabbing and state/rule structure (IF-DO pairs?), and ensure there are no stray characters, and that you're using " + rx.oD + (rx.oD.CompareTo(rx.cD) != 0 ? " " + rx.cD : "") + @" string delimiters properly.)",

			["Never"] = "'Never' requires: no inputs.",
			["Always"] = "'Always' requires: no inputs.",
			["All"] = "'All' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
			["Any"] = "'Any' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
			["ChatMatch"] = "'ChatMatch' requires one input: a string 'chat regex' to match. (" + rx.typeInfo["_S"] + ")",
			["MainSlotsLE"] = "'MainSlotsLE' requires one input: an integer slot-count. (" + rx.typeInfo["_I"] + ")",
			["SecsInStateGE"] = "'SecsInStateGE' requires one input: an integer time (in seconds). (" + rx.typeInfo["_I"] + ")",
			["NavEmpty"] = "'NavEmpty' requires: no inputs.",
			["Death"] = "'Death' requires: no inputs.",
			["VendorOpen"] = "'VendorOpen' requires: no inputs.",
			["VendorClosed"] = "'VendorClosed' requires: no inputs.",
			["ItemCountLE"] = "'ItemCountLE' requires two inputs: an integer item-count, a string item-name. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["ItemCountGE"] = "'ItemCountGE' requires two inputs: an integer item-count, a string item-name. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["MobsInDist_Name"] = "'MobsInDist_Name' requires three inputs: an integer monster-count, a double distance, a string monster-name. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")",
			["MobsInDist_Priority"] = "'MobsInDist_Priority' requires three inputs: an integer monster-count, a double distance, an integer monster-priority. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_D"] + ")",
			["NeedToBuff"] = "'NeedToBuff' requires: no inputs.",
			["NoMobsInDist"] = "'NoMobsInDist' requires one input: a double distance. (" + rx.typeInfo["_D"] + ")",
			["BlockE"] = "'BlockE' requires one input: an integer landblock (expressed in hexidecimal). (" + rx.typeInfo["_I"] + ")",
			["CellE"] = "'CellE' requires one input: an integer landcell (expressed in hexidecimal). (" + rx.typeInfo["_I"] + ")",
			["IntoPortal"] = "'IntoPortal' requires: no inputs.",
			["ExitPortal"] = "'ExitPortal' requires: no inputs.",
			["Not"] = "'Not' requires: a following operation, on the same line.",
			["PSecsInStateGE"] = "'PSecsInStateGE' requires one input: an integer burden. (" + rx.typeInfo["_I"] + ")",
			["SecsOnSpellGE"] = "'SecsOnSpellGE' requires two inputs: an integer time (in seconds), an integer SpellID. (" + rx.typeInfo["_I"] + ")",
			["BuPercentGE"] = "'BuPercentGE' requires one input: an integer burden. (" + rx.typeInfo["_I"] + ")",
			["DistToRteGE"] = "'DistToRteGE' requires one input: a double distance. (" + rx.typeInfo["_D"] + ")",
			["Expr"] = "'Expr' requires one input: a string 'code expression' to evaluate. (" + rx.typeInfo["_S"] + ")",
			["ChatCapture"] = "'ChatCapture' requires two inputs: a string 'chat regex' to match/capture, a string 'color ID list'. (" + rx.typeInfo["_S"] + ")",

			["None"] = "'None' requires: no inputs.",
			["SetState"] = "'SetState' requires one input: a string state name. (" + rx.typeInfo["_S"] + ")",
			["Chat"] = "'Chat' requires one input: a string to send to chat. (" + rx.typeInfo["_S"] + ")",
			["DoAll"] = "'DoAll' requires: no same-line inputs. Enclosed operations must appear on following lines, tabbed in once more.",
			["EmbedNav"] = "'EmbedNav' requires two inputs: a literal tag, a string name. It can also take an optional string input that must contain seven space-separated doubles that represent a mathematical transform of the nav points: a b c d e f g, where newX=aX+bY+e, newY=cX+dY+f, and newZ=Z+g.  (" + rx.typeInfo["_L"] + " " + rx.typeInfo["_S"] + " " + rx.typeInfo["_D"] + ")",
			["CallState"] = "'CallState' requires two inputs: a string 'go-to' state name, a string 'return-to' state name. (" + rx.typeInfo["_S"] + ")",
			["Return"] = "'Return' requires: no inputs.",
			["DoExpr"] = "'DoExpr' requires one input: a string 'code expression' to evaluate. (" + rx.typeInfo["_S"] + ")",
			["ChatExpr"] = "'ChatExpr' requires one input: a string 'code expression' to evaluate then send as chat. (" + rx.typeInfo["_S"] + ")",
			["SetWatchdog"] = "'SetWatchdog' requires three inputs: a double distance, a double time (in seconds), a string state name. (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")",
			["ClearWatchdog"] = "'ClearWatchdog' requires: no inputs.",
			["GetOpt"] = "'GetOpt' requires two inputs: a string VT-option, a string variable-name. (" + rx.typeInfo["_S"] + ")",
			["SetOpt"] = "'SetOpt' requires two inputs: a string VT-option, a string variable-name. (" + rx.typeInfo["_S"] + ")",
			["CreateView"] = "'CreateView' requires two inputs: a string view, a string XML (or XML file reference). (" + rx.typeInfo["_S"] + ")",
			["DestroyView"] = "'DestroyView' requires one input: a string view. (" + rx.typeInfo["_S"] + ")",
			["DestroyAllViews"] = "'DestroyAllViews' requires: no inputs.",

			["flw"] = "'flw' requires two inputs: integer target GUID (in hexidecimal), string target name. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["pnt"] = "'pnt' requires three inputs: double xyz-coordinates. (" + rx.typeInfo["_D"] + ")",
			["prt"] = "'prt' requires four inputs: double xyz-coordinates, integer portal GUID (in hexidecimal). (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_I"] + ")",
			["rcl"] = "'rcl' requires four inputs: double xyz-coordinates, string recall spell name (exact). (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")\nRecognized recall spell names:\n* " + rx.oD + "Primary Portal Recall" + rx.cD + "\n* " + rx.oD + "Secondary Portal Recall" + rx.cD + "\n* " + rx.oD + "Lifestone Recall" + rx.cD + "\n* " + rx.oD + "Lifestone Sending" + rx.cD + "\n* " + rx.oD + "Portal Recall" + rx.cD + "\n* " + rx.oD + "Recall Aphus Lassel" + rx.cD + "\n* " + rx.oD + "Recall the Sanctuary" + rx.cD + "\n* " + rx.oD + "Recall to the Singularity Caul" + rx.cD + "\n* " + rx.oD + "Glenden Wood Recall" + rx.cD + "\n* " + rx.oD + "Aerlinthe Recall" + rx.cD + "\n* " + rx.oD + "Mount Lethe Recall" + rx.cD + "\n* " + rx.oD + "Ulgrim's Recall" + rx.cD + "\n* " + rx.oD + "Bur Recall" + rx.cD + "\n* " + rx.oD + "Paradox-touched Olthoi Infested Area Recall" + rx.cD + "\n* " + rx.oD + "Call of the Mhoire Forge" + rx.cD + "\n* " + rx.oD + "Colosseum Recall" + rx.cD + "\n* " + rx.oD + "Facility Hub Recall" + rx.cD + "\n* " + rx.oD + "Gear Knight Invasion Area Camp Recall" + rx.cD + "\n* " + rx.oD + "Lost City of Neftet Recall" + rx.cD + "\n* " + rx.oD + "Return to the Keep" + rx.cD + "\n* " + rx.oD + "Rynthid Recall" + rx.cD + "\n* " + rx.oD + "Viridian Rise Recall" + rx.cD + "\n* " + rx.oD + "Viridian Rise Great Tree Recall" + rx.cD + "\n* " + rx.oD + "Celestial Hand Stronghold Recall" + rx.cD + "\n* " + rx.oD + "Radiant Blood Stronghold Recall" + rx.cD + "\n* " + rx.oD + "Eldrytch Web Stronghold Recall" + rx.cD,
			["pau"] = "'pau' requires four inputs: double xyz-coordinates, double pause time (in seconds). (" + rx.typeInfo["_D"] + ")",
			["cht"] = "'cht' requires four inputs: double xyz-coordinates, string recall spell name (exact). (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")",
			["vnd"] = "'vnd' requires five inputs: double xyz-coordinates, integer vendor GUID (in hexidecimal), string vendor name. (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["ptl"] = "'ptl' requires eight inputs: double xyz-coordinates, double xyz-coordinates of object, integer ObjectClass (portal=14, npc=37), string object name. (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["tlk"] = "'tlk' requires eight inputs: double xyz-coordinates, double xyz-coordinates of object, integer ObjectClass (npc=37), string object name. (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["chk"] = "'chk' requires three inputs: double xyz-coordinates. (" + rx.typeInfo["_D"] + ")",
			["jmp"] = "'jmp' requires six inputs: double xyz-coordinates, double heading (in degrees), string holdShift (" + rx.oD + "True" + rx.cD + " or " + rx.oD + "False" + rx.cD + "), double time-delay (in milliseconds). (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")",

			["ENavXF"] = "When present, the transform string input (third input) for EmbedNav must contain seven space-separated doubles, representing a mathematical transform of the nav points: a b c d e f g, where newX=aX+bY+e, newY=cX+dY+f, and newZ=Z+g."
		};
	}

	class OutputText
	{
		public const string metaHeader =
@"~~ {
~~ FOR AUTO-COMPLETION ASSISTANCE: testvar getvar setvar touchvar clearallvars clearvar getcharintprop getchardoubleprop getcharquadprop getcharboolprop getcharstringprop getisspellknown getcancastspell_hunt getcancastspell_buff getcharvital_base getcharvital_current getcharvital_buffedmax getcharskill_traininglevel getcharskill_base getcharskill_buffed getplayerlandcell getplayercoordinates coordinategetns coordinategetwe coordinategetz coordinatetostring coordinateparse coordinatedistancewithz coordinatedistanceflat wobjectgetphysicscoordinates wobjectgetname wobjectgetobjectclass wobjectgettemplatetype wobjectgetisdooropen wobjectfindnearestmonster wobjectfindnearestdoor wobjectfindnearestbyobjectclass wobjectfindininventorybytemplatetype wobjectfindininventorybyname wobjectfindininventorybynamerx wobjectgetselection wobjectgetplayer wobjectfindnearestbynameandobjectclass actiontryselect actiontryuseitem actiontryapplyitem actiontrygiveitem actiontryequipanywand actiontrycastbyid actiontrycastbyidontarget chatbox chatboxpaste statushud statushudcolored uigetcontrol uisetlabel isfalse istrue iif randint cstr strlen getobjectinternaltype cstrf stopwatchcreate stopwatchstart stopwatchstop stopwatchelapsedseconds cnumber floor ceiling round abs getworldname getitemcountininventorybyname getheading getitemcountininventorybynamerx getheadingto actiontrygiveprofile vitae getfellowshipstatus getfellowshipname getfellowshipisopen getfellowshipisleader getfellowshipleaderid getfellowshipcanrecruit getfellowid getfellowshipcount getfellowshiplocked getfellowname getfellowshipisfull sin cos tan sqrt asin acos atan atan2 sinh cosh tanh vtsetmetastate getregexmatch echo chr ord wobjectgetid wobjectgethealth wobjectfindbyid wobjectgetintprop wobjectfindnearestbytemplatetype wobjectgetopencontainer testquestflag getquestktprogress isrefreshingquests getquestktrequired getqueststatus getisday getgamehour getgamehourname getisnight getgameday getgameticks getminutesuntilday getgamemonth getgamemonthname getminutesuntilnight getgameyear uisetvisible uiviewvisible uiviewexists getgvar touchgvar getpvar touchpvar setgvar cleargvar setpvar clearpvar testgvar clearallgvars testpvar clearallpvars dictgetitem dictcreate dicthaskey dictadditem dictkeys dictremovekey dictvalues dictclear dictsize dictcopy listgetitem listpop listcreate listcontains listremove listadd listindexof listremoveat listinsert listlastindexof listclear listcopy listcount listreverse

~~																						
~~ File auto-generated by metaf, a program created by Eskarina of Morningthaw/Coldeve.	
~~		Get metaf here: https://github.com/JJEII/metaf/									
~~																						
~~ All recognized structural designators:												
~~		STATE:				DO:															
~~		IF:					NAV:														
~~																						
~~ All recognized CONDITION (IF:) operation keywords:									
~~		Never				NavEmpty			MobsInDist_Priority		Not				
~~		Always				Death				NeedToBuff				PSecsInStateGE	
~~		All					VendorOpen			NoMobsInDist			SecsOnSpellGE	
~~		Any					VendorClosed		BlockE					BuPercentGE		
~~		ChatMatch			ItemCountLE			CellE					DistToRteGE		
~~		MainSlotsLE			ItemCountGE			IntoPortal				Expr			
~~		SecsInStateGE		MobsInDist_Name		ExitPortal				ChatCapture		
~~																						
~~ All recognized ACTION (DO:) operation keywords:										
~~		None				EmbedNav			ChatExpr				SetOpt			
~~		SetState			CallState			SetWatchdog				CreateView		
~~		Chat				Return				ClearWatchdog			DestroyView		
~~		DoAll				DoExpr				GetOpt					DestroyAllViews	
~~																						
~~ All recognized NAV types:															
~~		circular			follow														
~~		linear				once														
~~																						
~~ All recognized NAV NODE types:														
~~		flw					vnd															
~~		pnt					ptl															
~~		rcl					tlk															
~~		pau					chk															
~~		cht					jmp															
~~		prt (deprecated in VTank)														
~~ }																					
";

		public const string navHeader =
@"~~ {
~~ FOR AUTO-COMPLETION ASSISTANCE: testvar getvar setvar touchvar clearallvars clearvar getcharintprop getchardoubleprop getcharquadprop getcharboolprop getcharstringprop getisspellknown getcancastspell_hunt getcancastspell_buff getcharvital_base getcharvital_current getcharvital_buffedmax getcharskill_traininglevel getcharskill_base getcharskill_buffed getplayerlandcell getplayercoordinates coordinategetns coordinategetwe coordinategetz coordinatetostring coordinateparse coordinatedistancewithz coordinatedistanceflat wobjectgetphysicscoordinates wobjectgetname wobjectgetobjectclass wobjectgettemplatetype wobjectgetisdooropen wobjectfindnearestmonster wobjectfindnearestdoor wobjectfindnearestbyobjectclass wobjectfindininventorybytemplatetype wobjectfindininventorybyname wobjectfindininventorybynamerx wobjectgetselection wobjectgetplayer wobjectfindnearestbynameandobjectclass actiontryselect actiontryuseitem actiontryapplyitem actiontrygiveitem actiontryequipanywand actiontrycastbyid actiontrycastbyidontarget chatbox chatboxpaste statushud statushudcolored uigetcontrol uisetlabel isfalse istrue iif randint cstr strlen getobjectinternaltype cstrf stopwatchcreate stopwatchstart stopwatchstop stopwatchelapsedseconds cnumber floor ceiling round abs getworldname getitemcountininventorybyname getheading getitemcountininventorybynamerx getheadingto actiontrygiveprofile vitae getfellowshipstatus getfellowshipname getfellowshipisopen getfellowshipisleader getfellowshipleaderid getfellowshipcanrecruit getfellowid getfellowshipcount getfellowshiplocked getfellowname getfellowshipisfull sin cos tan sqrt asin acos atan atan2 sinh cosh tanh vtsetmetastate getregexmatch echo chr ord wobjectgetid wobjectgethealth wobjectfindbyid wobjectgetintprop wobjectfindnearestbytemplatetype wobjectgetopencontainer testquestflag getquestktprogress isrefreshingquests getquestktrequired getqueststatus getisday getgamehour getgamehourname getisnight getgameday getgameticks getminutesuntilday getgamemonth getgamemonthname getminutesuntilnight getgameyear uisetvisible uiviewvisible uiviewexists getgvar touchgvar getpvar touchpvar setgvar cleargvar setpvar clearpvar testgvar clearallgvars testpvar clearallpvars dictgetitem dictcreate dicthaskey dictadditem dictkeys dictremovekey dictvalues dictclear dictsize dictcopy listgetitem listpop listcreate listcontains listremove listadd listindexof listremoveat listinsert listlastindexof listclear listcopy listcount listreverse

~~																						
~~ File auto-generated by metaf, a program created by Eskarina of Morningthaw/Coldeve.	
~~		Get metaf here: https://github.com/JJEII/metaf/									
~~																						
~~ All recognized structural designators in a NAV-ONLY file:							
~~		NAV:																			
~~																						
~~ All recognized NAV types:															
~~		circular			follow														
~~		linear				once														
~~																						
~~ All recognized NAV NODE types:														
~~		flw					vnd															
~~		pnt					ptl															
~~		rcl					tlk															
~~		pau					chk															
~~		cht					jmp															
~~		prt (deprecated in VTank)														
~~ 																						

~~ 																						
~~ 				REMEMBER THAT NAV-ONLY FILES MUST CONTAIN EXACTLY ONE NAV!				
~~ }																					
";
		public const string readme =
@"~~																															
~~		METa Alternate Format																					Created by	
~~			   README																							 Eskarina	
~~																															

~~															
~~	TABLE OF CONTENTS:										
~~															
~~		1. VISUAL CODING ASSISTANCE							
~~		2. WHY metaf?										
~~		3. GETTING STARTED									
~~		4. META STRUCTURE									
~~		5. COMMENTS, STRINGS, CODE-FOLDING, MISCELLANEOUS	
~~		6. QUICK REFERENCE (external)						
~~		7. FULL REFERENCE (external)						
~~		8. VIRINDITANK FUNCTIONS (external)					
~~		9. UTILITYBELT FUNCTIONS (external)					
~~															

~~ 1. VISUAL CODING ASSISTANCE																								

I strongly recommend you get Notepad++. Your metaf experience will be vastly improved. It's a powerful and free text editor
that can do custom coloring of text (along with several other capabilities I have leveraged). I have created a metaf.xml file
that colors all the relevant keywords, and activating that feature helps substantially.
	1) Download and install Notepad++ from here: https://notepad-plus-plus.org/downloads/
	2) Open it, go to the Language menu, then User Defined Language, and click Define your language...
	3) Click Import..., then navigate to and choose the metaf.xml (for 'dark mode', see below instead) file. Click Open.
	4) It should now be imported. Close Notepad++, and re-open it, then open this file in it.
Even this documentation file works better with that custom coloring activated. And, meta files work much, much better. So,
get that going as soon as possible! And, feel free to modify the coloration to your own preferences.

NOTE: If you prefer a 'dark mode' coloration style instead, I've also provided a metaf_dark.xml. Follow the instructions
above for it, then also go to the Settings menu, then Style Configurator... and either choose a theme like 'Deep Black' from
the top 'Select theme' dropdown menu, or customize things more specifically like this: select Language: Global Styles, then
select Style: Default Style, then swap the Foreground and Background colors in the 'Colour Style' box to make the default
foreground white, and background black. Click Save & Close. (Although, while you're in there, I'd also recommend changing a
few other styles' colors: 'Indent guideline style' Foreground (then 'More Colours...') RGB[81,81,81] and Background[0,0,0];
'Brace highlight style' Foreground[255,255,255] and Background[0,0,0]; 'Current line background'[28,28,66]; and
'Caret colour' [191,128,255].)

I have leveraged Notepad++'s ability to suggest auto-completions based upon file-content-so-far, which should make entering
those long VirindiTank function names easier and less prone to error.

Notepad++ can auto-close brackets, braces, and parentheses if you tell it to. (Go to the Settings menu, then Preferences...,
and select Auto-Completion in the list on the left, then select your preferences.)

It can tab-in/out multiple lines simultaneously. (Select them, then press tab or shift-tab.)

It can simultaneously comment/uncomment multiple lines. (See the Edit menu, then Comment/Uncomment, and the Block options.)

~~ 2. WHY metaf?																											

VirindiTank is an impressive addon, but I became very frustrated while creating metas through its in-game editor. It is
possible to do so, of course, but it is agonizingly inefficient. I found that rather than spending much of my time thinking
about meta design, I was instead spending most of it just wrestling with the interface. The editor suffers from many
upsetting shortcomings, to include:
	* It can't reorder operations inside of rules (you just have to delete and re-enter, in a different order)
	* It can't change Any<-->All without purging the whole thing and re-entering everything inside it from scratch
	* It can't directly select text in fields, to copy/paste in and out of them
	* It can't display more than a short piece of a text field (and can't scroll it), so it's incredibly inefficient to
	  edit anything but the very shortest of text entries
	* It can't duplicate anything (operations, rules, or states) for subsequent minor modification
	* The interface is quite opaque (lacking transparency) in conveying a meta's 'big picture' logical structure
	* It artificially forces states to be ordered alphabetically
	* It can't search for anything (much less search-and-replace) to find where it appears within a meta
	* It doesn't have any sort of annotation capability (i.e., 'code comments')
	* And, I'm sure there are other things, but you get the idea

KEY: Long story, short: metaf suffers from none of the issues listed above. It's a much more friendly way to make metas.

That being said, there is one drawback of some significance that metaf has versus the in-game editor: you don't get instant
feedback on invalid text entries (e.g., expressions). Metaf does not parse the meaning of the inputs you provide; it simply
ensures the basic structure and datatypes are correct, while providing a powerful and transparent editing interface. So, if
you enter gevar[x] when you mean getvar[x], metaf will convert that into .met for VirindiTank to load, and you'll only
discover the mistake at that point. However, if you have already activated in Notepad++ the custom coloration I've provided,
you should have noticed something important just now: the correct function name is bold and a different color, so you can
know you got it right, even in metaf. (Every documented VirindiTank function does this. And, if you haven't yet activated
the custom colors... seriously: do it. Right now. It helps a ton.) I believe this, in conjunction with the suggested auto-
completions, substantially reduce the impact of this drawback.

I think the only other shortcoming of metaf versus the in-game editor is also mixed with one of its strengths: building
navroutes. For obvious reasons, metaf is not good at adding new navroute nodes that have coordinates in them. (It simply
has no direct connection to Dereth's topography, so how could it?) However, it also has some great navroute strengths:
	* It is good at editing navroutes after those points have been collected
	* It is trivially easy to re-use navroutes in multiple places in metaf code while defining the navroute just one time
		- 'Code' and 'navroute' sections are listed separately in metaf. The way the two are linked is through a unique
		  'tag' that every navroute must have (e.g., nav73). The tag is used to cite which navroute to embed in an EmbedNav
		  operation. A single navroute tag can be cited by as many EmbedNav operations as you want, naming it whatever you
		  want in each location it's cited. Each tag's navroute is defined exactly one time in the 'navroute' section.
	* It is very simple to create EmbedNav placeholders in metaf code, ready to be filled in with coordinate content later
		- Place a single line in the navroute section---e.g., NAV: EmptyNav once---which defines an empty navroute with an
		  'EmptyNav' tag, then reference it anywhere you need to have an embedded navroute but don't yet have its content
		  ready---e.g., EmbedNav EmptyNav {[none]}. When you load it in-game, you'll have embedded navroutes everywhere you
		  need them, just waiting to be filled in.
	* For little additional effort, I created a layered-on feature: metaf can transform navroutes during conversion into
	  .met. What that means is that for duplicate dungeons, you can create one nav, then simply transform it to create navs
	  for all the other dungeons with the same layout! Seven numbers = entirely new nav! For more, see EmbedNav in Section 7.
	* Also for relatively small effort, I added the ability for metaf to directly edit .nav files (without first being
	  embedded inside a .met file). Just remember a nav-only file contains one navroute. No more, no less. And no states.

One goal for metaf was to provide full-coverage support for the .met format. I believe I have achieved that. 'Success' is
not defined by 'the meta runs without errors' but rather by 'VirindiTank successfully loaded it'. (This in itself is
something of an achievement since a single out-of-place character in .met can trigger VirindiTank to refuse to load it,
simply stating that it's a corrupted file.) The inverse is also needed: 'metaf successfully converted .met to .af.'

~~ 3. GETTING STARTED																										

metaf requires .NET Core (5.0) be installed. Get it here: https://dotnet.microsoft.com/download/dotnet/5.0/runtime
The three files (exe, dll, json) are also required in order to run. The metaf.xml (or metaf_dark.xml) file defines custom
colors for easier editing in Notepad++. See below regarding the batch file. You may need to authorize the file to run since I
haven't formally published it with a digital signature. (The only thing the program does is read a text-based file you tell
it to, and write out another one in a different format.)

I have endeavored to lower the learning curve as much as I can. metaf can literally convert between .met/.nav and .af files
by simply dragging either file type onto its metaf.exe icon. (It auto-detects the file type and converts it to the other
format, outputting a new file in the same directory as the one being converted.) However, that approach will never over-
write anything, instead creating a new and uniquely named output file every time, which may soon get a bit unwieldy if
you're actively developing a meta and repeatedly converting it to load in-game. (Read on...)

WARNING: Do not drag multiple-selected files onto metaf! If you do, it will overwrite at least one of them because it
interprets the multiple file name inputs as input *and output* file names. So, drag-and-drop one file at a time. (Read on...)

There is also a command line interface. It takes an input file name followed (optionally) by an output file name (or output
directory name). If you use this interface, it will output to the specified file (or directory), whether or not it already
existed. (So, be careful.) Leveraging this interface, I have provided a batch file (_OverwriteDest.bat) to support force-
overwriting and mass-converting whole collections of files, in any combination of .met, .nav, and .af files. If you wish to
use this capability, edit _OverwriteDest.bat, set-up the .met (also used for .nav files) and .af output directories, ensure
they actually exist, save the batch file, and then drag multiple-selected files onto it instead of metaf.exe. This approach
easily supports directly outputing up-to-date files to VirindiTank's meta directory, overwriting them rather than creating
new files every time. (Note that 'unusual' characters in paths and file names (e.g., parentheses, exclamation marks, etc.)
may disrupt the batch file's successful execution.)

The command line can take four other parameters as well: 'metaf -help' recreates this help file and the metaf reference
file; 'metaf -new' creates a blank template .af file that has the meta-file header text in it, ready for coding a meta;
'metaf -newnav' does the same, but for a 'nav .af'; 'metaf -version' outputs metaf's current version.

I have mapped all the in-game meta operations to a set of recognized text commands in metaf, used for converting back into
.met format. Sections 6 and 7 detail them.

TIP: My recommended first step for getting started is to export a .met file to .af and have a look at it. Generally, if you
are stumped on how to express something in metaf that can be done directly in VirindiTank, then create it there, then export
it to .af to see what it looks like. (I hope my metafReference.af file usually provides sufficient explanations, though.)

~~ 4. META STRUCTURE																										

The meta structure requires the definition of STATES, inside each of which is one or more RULES, each of which is composed
of an IF:-DO: pair. The IF: is a CONDITION, while the DO: is an ACTION (performed if the condition evaluates to True). Every
IF: and every DO: requires exactly one OPERATION to be contained within it, whether that operation be Never or None, or an
All/Any or DoAll that contains more operations wrapped inside of it. Each Condition triggers its corresponding Action if it
evaluates to true. (After all states have been defined, the navroute definitions are listed. NAV: marks the start of an
individual navroute, and any NODES inside the navroute appear on immediately subsequent lines, tabbed in one time.)

Each state must be uniquely named and appear exactly one time (and atomically, i.e., defined in full, and not split apart
into partial definitions spread throughout the file).

State order does not matter in metaf. Place states wherever works best for you. (Even move them around while developing, if
that helps.) Caveat: all states are listed above all navroutes.

Rule order within each state does matter, though. The top rule in each state has the highest priority, and each rule after
that descends on down the list in priority, to the lowest at the bottom.

When all states in the entire meta have been fully defined, all embedded navroutes are listed below them.

Proper tabbing is critical to metaf's code structure. It defines what's inside of what, in exactly the same way the in-game
editor allows. Pay attention to it.

TIP: Notepad++ supports tab/untab of multiple lines, en masse: just select the lines, and press tab or shift-tab (to untab).

I have leveraged the fact that every Condition and Action contains exactly one Operation to make the code more compact.
The logical code structure is like this:
	STATE: {Default}
	^	IF: 				<-- Almost totally blank line
	|		Any
	|			...
	|		DO:				<-- Almost totally blank line
	|			None
	|	IF:					<-- Almost totally blank line
	|	^	Always
	|	|	DO:				<-- Almost totally blank line
	|	|	^	DoAll
	|	|	|	^	...
	|	|	|	|	^		<-- Tabbing relationship is the same as below
But, I expect many rules (IF:-DO: pairs) to exist in most metas, so that would be a massive waste of code-viewing space,
just leaving every single IF: and DO: line essentially blank. Thus, I have condensed the code so that, even though it
continues to maintain its logical structure (and tab-in constraints), it is significantly more compact (especially across
many lines) due to requiring that the mandatory operation be on the same line as its rule label. Like this:
	STATE: {Default}
	^	IF: Any					<-- Now appears on same line as 'IF:'
	|			...			<-- Note: still tabbed-in three times, just like before
	|		DO:	None			<-- Now appears on same line as 'DO:'
	|	IF:	Always				<-- Now appears on same line as 'IF:'
	|	^	DO:	DoAll			<-- Now appears on same line as 'DO:'
	|	|	^	^	...		<-- Note: still tabbed-in four times, just like before
	|	|	|	|	^		<-- Tabbing relationship is the same as above
Note that the actual required tabbing remains the same in both! That's important because tab-in count determines logical
nesting of operations. So, even though the '...' at the bottom there appears to be tabbed in too far (two times beyond DO:,
rather than just one time), it's actually correct, because it's inside of both DO: and DoAll.

The one and only exception to this tab-in rule is the Not operation, which imposes no cumulatively additional tab-in demands
upon the following operation(s). So, if a line with, say, Any is tabbed in four times, then the following lines defining the
operations it encloses must be tabbed-in five times. And, this is also true of a four-tabbed Not Any line, as well as a four-
tabbed Not Not ... Not Any line: the line following is tabbed in five times (assuming the 'Any' isn't empty).

Once all states are defined, all navroutes (NAV:) are listed below. And, just like with states, the navroute order does not
matter; but just likes with rules in each state, the order of the nav nodes in each navroute does matter.

~~ 5. COMMENTS, STRINGS, CODE-FOLDING, MISCELLANEOUS																		

The metaf system supports line commenting. Just put a double-tilde (two ~) anywhere, and the rest of that line is ignored.

Many of the Operation inputs are essentially freeform text strings--from regular expressions to state names and more. In
order to unambiguously identify these inputs as they're intended, metaf requires that strings be delimited by braces { }.
Strings should also be separated by at least one whitespace character (e.g., space or tab). If you need to include braces
inside of a string input, you can do so: simply double it. In other words, when inside a string, {{ in metaf becomes { in
met, and }} becomes }. (Single braces aren't allowed inside metaf strings; if they're there at all, they must be doubled.)

Blank (whitespace-only) lines are ignored. Use them to your advantage if they improve readability.

You can code-fold with Notepad++ and metaf. Just place matching braces behind line comments, and you have it. This includes
nested folding. (Every string input is always enclosed with both opening and closing braces on its own line, so it doesn't
trigger folding, and because the folding braces are behind line comments, metaf itself ignores them during conversion.)
Example:
	STATE: {Default ~~ { <-- this folds the whole state
		IF: Always
			DO: None
		IF: Always ~~ {	  <-- this just folds this piece of the state
			DO: None ~~ } <-- closing piece-of-state fold
		IF: Always
			DO: None
	~~ }                 <-- closing state fold

NOTE: metaf expects decimal numbers to use periods as their decimal separator characters. If you live in a country that uses
commas instead, be aware of that. (.NET defaults to using the local culture for number formatting, which caused metaf to
break in those countries because .met always uses periods (I think). I have now forced .NET to remain culture-invariant for
metaf, wherever it runs, so that it won't break anymore; but it does require periods, not commas.)

NOTE: When metaf exports from .met to .af, it includes a large comment header. There are multiple reasons for this, but the
main two are these: A) Convenient metaf keyword reference, and B) Notepad++ provides predictive auto-completion of words
based upon what's already been entered into a document, so including all VirindiTank function names and all metaf keywords
right at the start achieves a sort of 'poor man's IntelliSense' that should help with getting them input correctly while
coding your metas (especially the very long VT function names).

~~ 																															
~~ 6. QUICK REFERENCE																										
~~ 																															

	(See metafReference.af, Section 1. Run 'metaf -help' if you don't have that file.)

~~																															
~~ 7. FULL REFERENCE																										
~~																															

	(See metafReference.af, Section 2. Run 'metaf -help' if you don't have that file.)

~~																															
~~ 8. VIRINDITANK FUNCTIONS																									
~~																															

	(See metafReference.af, Section 3. Run 'metaf -help' if you don't have that file.)

~~																															
~~ 9. UTILITYBELT FUNCTIONS																									
~~																															

	(See metafReference.af, Section 4. Run 'metaf -help' if you don't have that file.)

~~																															
~~	I hope you find metaf helpful in your meta-making adventures!   ~ Eskarina												
~~																															

~~																															
~~		METa Alternate Format																					Created by	
~~			   README																							 Eskarina	
~~																															";

		public const string reference =
@"~~																															
~~		METa Alternate Format																					Created by	
~~			  REFERENCE																							 Eskarina	
~~																															

~~									
~~	TABLE OF CONTENTS:				
~~									
~~		1. QUICK REFERENCE			
~~		2. FULL REFERENCE			
~~		3. VIRINDITANK FUNCTIONS	
~~		4. UTILITYBELT FUNCTIONS	
~~									

~~ 																															
~~ 1. QUICK REFERENCE																										
~~ 																															

	~~ Data-type abbreviations:		
		i - Integer. A whole number.
		h - Integer expressed in Hexidecimal.
		d - Double. A decimal number.
		s - String. A chain of arbitrary characters inside braces { }. Can't contain braces, unless doubled.
		r - RegEx. Same as S, but expected to be a Regular Expression.
		l - Literal. A chain of characters beginning with a letter or underscore, followed by zero or more letters,
			underscores, or digits. Note: cannot contain any whitespace.
		p - Operation (Condition or Action).

	~~ STATE, RULE					
				 STATE: - Input: s Name, Rule+ (indirectly). A state name, distinct from all other state names. Every state
						  must contain at least one Rule (IF-DO pair).
					IF: - Input: p Condition. Condition may be any type (including Any/All, which may contain more inside).
						  Every Condition (IF) must be followed by an Action (DO).
					DO: - Input: p Action. Action may be any type (including DoAll, which may contain more inside).

	~~ CONDITION operations (IF:)	
				  Never - Input: none. False. (Never do the Action.)
				 Always - Input: none. True. (Always do the Action.)
					All - Input: p* Conditions (none directly). Contains >=0 Condition operations inside it. True if All of
						  them are true. (Empty-All is true; Not empty-All is false.)
					Any - Input: p* Conditions (none directly). Contains >=0 Condition operations inside it. True if Any of
						  them are true. (Empty-Any is false; Not empty-Any is true.)
			  ChatMatch - Input: r Pattern. True if Pattern matches a ChatWindow string.
			MainSlotsLE - Input: i Count. True if number of slots remaining empty in your inventory's main pack <= Count.
		  SecsInStateGE - Input: i Seconds. True if time since meta entered this state >= Seconds. (Timer RESETS if meta
						  turned off/on.)
			   NavEmpty - Input: none. True if the current navroute is empty.
				  Death - Input: none. True if character death is detected.
			 VendorOpen - Input: none. True when any vendor window is opened. !!! check exact functionality
		   VendorClosed - Input: none. True when a vendor window is closed. !!! check exact functionality
			ItemCountLE - Input: i Count, s Item name. True if number of Item in your inventory <= Count.
			ItemCountGE - Input: i Count, s Item name. True if number of Item in your inventory >= Count.
		MobsInDist_Name - Input: i Count, d Distance, r monster Name. True if number of Name within Distance >= Count.
	MobsInDist_Priority - Input: i Count, d Distance, i monster Priority. True if number of monsters of exact-Priority
						  within Distance >= Count.
			 NeedToBuff - Input: none. True if VTank's evaluated settings determine you need to buff.
		   NoMobsInDist - Input: d Distance. True if no monsters (>-1??) are within Distance of character. !!! test
				 BlockE - Input: h Landblock (expressed in hexidecimal). True if character's current landblock matches.
				  CellE - Input: h Landcell (expressed in hexidecimal). True if character's current landcell matches.
			 IntoPortal - Input: none. True upon entering portalspace.
			 ExitPortal - Input: none. True upon exiting portalspace.
					Not - Input: p Condition. Logically inverts any single Condition operation (including All/Any (empty or
						  not), as well as another Not). True if that operation is false.
		 PSecsInStateGE - Input: i Seconds. True if time since meta entered this state >= Seconds. (Timer CONTINUES if meta
						  turned off/on. P=Persistent.)
		  SecsOnSpellGE - Input: i Seconds, i SpellID(?). True if time remaining on spell with SpellID >= Seconds.
			BuPercentGE - Input: i Percent burden. True if character's burden >= Percent.
			DistToRteGE - Input: d Distance. True if character's shortest distance to currently loaded navroute >= Distance.
				   Expr - Input: s VTank 'code Expression'. True when executing Expression evaluates to true.
			ChatCapture - Input: r Pattern, s Color id list. True if ChatWindow message matches Pattern and Color. 'Captures'
						  regex groups in named variables.

	~~ ACTION operations (DO:)		
				   None - Input: none. Do nothing.
			   SetState - Input: s meta State name. Transition to State.
				   Chat - Input: s Text. Input the Text into the ChatWindow. (@t messages, execute vt commands, @e, etc.)
				  DoAll - Input: p* Actions (none directly). Contains >=0 Action operations inside it, allowing multiple
						  Actions to be associated with a single Condition.
			   EmbedNav - Input: s Tag, s Name, optional s Transform. Tag is a just a handle for a nav listed in the file's
						  bottom section; Name shows as the embedded nav's name in-game. See EmbedNav in Section 7 for more
						  on optional Transform input.
			  CallState - Input: s 'GoTo state name', s 'ReturnTo state name'. Transitions to GoTo state, placing ReturnTo
						  state on stack (set-up for use by a later Return operation).
				 Return - Input: none. Pops state from top of 'return to' stack and transitions to that state.
				 DoExpr - Input: s 'code Expression'. Executes the Expression as 'action code'.
			   ChatExpr - Input: s Text. Text is a hybrid; it first evaluates as an expression, then is input to ChatWindow.
						  Enables variable inputs to chat commands.
			SetWatchdog - Input: d Distance, d Seconds, s State name. In a state, if character hasn't moved >=Distance for
						  Seconds of time, call State.
		  ClearWatchdog - Input: none. Clears watchdog for this state.
				 GetOpt - Input: s VTank Option, s Variable name. Gets current value of Option and saves it in Variable.
				 SetOpt - Input: s VTank Option, s Expression. Sets Option based upon result of evaluating Expression.
			 CreateView - Input: s View, s XML. Creates a view View, with characteristics defined in XML. Note: the XML
						  must not contain newlines. Just like VirindiTank requires, so does metaf. (The XML must all be
						  on the same line as its CreateView operation.) See CreateView in Section 7 for support info for
						  external XML files.
			DestroyView - Input: s View. Destroys the designated View.
		DestroyAllViews - Input: none. Destroys all existing Virindi Views for this meta.

	~~ NAV TYPES (NAV:)				
				   NAV: - Input: l Tag, l Type. Tag is distinct from all other nav tags. Type is one of the following:
				   circular - A navroute that infinitely repeats itself by looping back to the start when it hits the end.
					 linear - A navroute that infinitely repeats itself by alternatingly running it forward and backward.
					 follow - A navroute that is a single node in length (see below), to chase a specified player.
					   once - A navroute that does not repeat. Nodes are consumed (disappear) when they are reached.
					Every nav is listed in the nav section, which appear below the code listing section.

	~~ NAV NODE FORMATS				
				 Follow - flw   h Target GUID   {s Target Name}
				  Point - pnt   d X   d Y   d Z
				 Portal - prt   d X   d Y   d Z   h Portal GUID   [This type is DEPRECATED in VTank. Use 'ptl' instead.]
		   Recall Spell - rcl   d X   d Y   d Z   {s Full Name of Recall Spell}
				  Pause - pau   d X   d Y   d Z   d Pause (in ms)
		ChatField (any) - cht   d X   d Y   d Z   {s ChatInput}
			 Use Vendor - vnd   d X   d Y   d Z   h Target GUID   {s Target Name}
		 Use Portal/NPC - ptl   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
		    Talk to NPC - tlk   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
		 Nav Checkpoint - chk   d X   d Y   d Z
			   Nav Jump - jmp   d X   d Y   d Z   d HeadingInDegrees   {s HoldShift (True|False)}   d Delay (in ms)

~~																															
~~ 2. FULL REFERENCE																										
~~																															

	~~ Miscellanous																											
		
		A couple things in this document, not explained elsewhere:
			GUID -  Globally Unique Identifier; it's an integer the game uses to reference every object uniquely. (Every
					object has a different number from every other object.) It is expressed in hexidecimal format, just like
					landblocks and landcells.
			ObjectClass - This is an integer that specifies a general 'object type' (e.g., Portal, NPC, etc.) See here for
					more: http://www.virindi.net/wiki/index.php/Meta_Expressions#Object_Properties

	~~ Datatype abbreviations																								

		i - Integer. A whole number.
		h - Integer expressed in Hexidecimal.
		d - Double. A decimal number.
		s - String. A chain of arbitrary characters inside braces { }. Can't contain braces, unless doubled.
		r - RegEx. Same as S, but expected to be a Regular Expression.
		l - Literal. A chain of characters beginning with a letter or underscore, followed by zero or more letters,
			underscores, or digits. Note: cannot contain any whitespace.
		p - An Operation (Condition or Action).

	~~ STATE, RULE																											

		STATE:   {s Name}   (Rule+ indirectly)
				IN-GAME: drop-down menu (or ""New State..."" button) at left near top when creating a new Rule.
				DETAILS: Two inputs, one direct (on the same line) and one indirect (on subsequent lines). The direct input,
				Name, declares this state's name and must be distinct from all other state names. The indirect input, Rule+,
				indicates that every state must contain at least one Rule (IF-DO pair) on the following lines.
				EXAMPLE: STATE: {Hello, world.}
							IF: Never
								DO: None
						--> Defines a state named 'Hello, world.' containing a single rule (IF-DO pair) that does nothing.
						
		IF:   p Condition
				IN-GAME: generally references the operations in the pane on left side.
				DETAILS: One input. Condition may be any type (including Any/All, which may contain more inside). Every
				Condition (IF) must be followed by an Action (DO).
				EXAMPLE: IF: Any
								Always
						--> Defines a Condition containing an Any operation (which itself contains an Always operation).

		DO:   p Action
				IN-GAME: generally references the operations in the pane on right side.
				DETAILS: One input. Action may be any type (including DoAll, which may contain more inside).
				EXAMPLE: DO: DoAll
								EmbedNav MyFavorite follow
						--> Defines an Action containing a DoAll operation (which itself contains an EmbedNav operation).

	~~ CONDITION operations (IF:)																							

		All   (none directly)
				IN-GAME: ""All"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Condition operations inside it (on
				following lines, tabbed in one more time). True if all directly-wrapped operations are True. (Empty-All is
				true; Not empty-All is false.) Do not confuse this with the Action DoAll.
				EXAMPLE: All
							Never
							Always
						 --> The All evaluates to False because not all the operations inside it
							 evaluate to True. (Never evaluates to False.)

		Always   (none)
				IN-GAME: ""Always"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True always. (Always do the corresponding Action.)
				EXAMPLE: Always
						 --> Always is True. (Always True.)

		Any   (none directly)
				IN-GAME: ""Any"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Condition operations inside it (on
				following lines, tabbed in one more time). True if any directly-wrapped operations are True. (Empty-Any is
				false; Not empty-Any is true.)
				EXAMPLE: Any
							Never
							Always
						 --> The Any evaluates to True because at least one operation inside it evaluates to True. (Always
							 evaluates to True.)

		BlockE   h Landblock
				IN-GAME: ""Landblock =="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if character location is currently in Landblock.
				EXAMPLE: BlockE 8B370000
						 --> True if leading 4 'digits' of character's @loc match leading 4 'digits'.

		BuPercentGE   i Burden
				IN-GAME: ""Burden Percent >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if character burden percent is >= Burden.
				EXAMPLE: BuPercentGE 110
						 --> True if character burden is >= 110%.

		CellE   h Landcell
				IN-GAME: ""Landcell =="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if character location is currently in Landcell.
				EXAMPLE: CellE 8B37E3A1
						 --> True if all 8 'digits' of character's @loc match all 8 'digits'.

		ChatCapture   {r Pattern}   {s ColorIdList}
				IN-GAME: ""Chat Message Capture"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Two inputs. True if both Pattern matches a ChatWindow message and that message's color is in the
						 ColorIdList. (Empty fields for either/both count as a match for that field.) Used to 'capture' text
						 into internal variables, which are given the names designated within Pattern with capturegroup_
						 concatenated as a prefix. The message color is in variable capturecolor, and the list is specified
						 as semicolon-separated numbers. See: http://www.virindi.net/wiki/index.php/Virindi_Chat_System_5
						 NOTE: It does not appear to actually be color IDs, but rather the various ChatWindow message type
						 IDs. (I matched to General and Trade, then changed their colors, and they still matched.)
								Colors I've discovered so far (personally, and with '/ub printcolors'):
									Type			Example			ID				Type			Example			ID
									Broadcast		EnemyDeath		0				Appraisal		?				?
									?				?				?				Spellcasting	AnyMagicCast	17
									Speech			Say				2				Allegiance		AllegianceChat	18
									Tell			YouThink		3				Fellowship		FellowshipChat	19
									OutgoingTell	YouTell			4				WorldBroadcast	Aerfalle?		20
									System			ServerRestart?	5				CombatEnemy		YouEvade/HitBy	21
									Combat			HoT/Surge		6				CombatSelf		Enemy ''/ ''	22
									Magic			YouResist/HitBy	7				Recall			@hom			23
									Channel			?				8				Craft			TinkerApplied	24
									ChannelSend		?				9				Salvaging		AnySalvaging	25
									Social			?				10				?				?				?
									SocialSend		?				11				General			GeneralChat		27
									Emote			AnyEmote		12				Trade			TradeChat		28
									Advancement		LevelUp?		13				LFG				LFGChat?		29
									Abuse			?				14				Roleplay		RoleplayChat?	30
									Help			?				15				AdminTell		?				31
																					Olthoi			?				32
				EXAMPLE: ChatCapture {^.*(?<who>Eskarina).* (says|tells you), \"".+\""$} {2;4}
						--> When True:	Variable capturegroup_who holds string 'Eskarina';
									 	Variable capturecolor holds matched-message's colorID.

		ChatMatch   {r Pattern}
				IN-GAME: ""Chat Message"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if Pattern regex matches a ChatWindow message. (Matches any message if empty.)
				EXAMPLE: ChatMatch {^.*Eskarina.* (says|tells you), \"".+\""$}
						 --> Simply detects a regex match in the ChatWindow. Does not capture anything.

		Death   (none)
				IN-GAME: ""Character Death"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True if character death detected.
				EXAMPLE: Death
						 --> Triggered on character death.

		DistToRteGE   d Distance
				IN-GAME: ""Dist any route pt >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if character's shortest-distance to current navroute is >= Distance (in yards).
				EXAMPLE: DistToRteGE
						 --> True when character exceeds  distance from current navroute.

		ExitPortal   (none)
				IN-GAME: ""Portalspace Exited"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True upon exiting portalspace.
				EXAMPLE: ExitPortal
						 --> True when character leaves portalspace.

		Expr   {s Code}
				IN-GAME: ""Expression"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if Code evaluates to True. Do not confuse this with the Action DoExpr.
				EXAMPLE: Expr {7==getobjectinternaltype[getvar[myvar]]}
						 --> True if variable myvar is an object type.
						(See: http://www.virindi.net/wiki/index.php/Meta_Expressions#Function_Information )

		IntoPortal   (none)
				IN-GAME: ""Portalspace Entered"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True upon entering portalspace.
				EXAMPLE: IntoPortal
						 --> True when character enters portalspace.

		ItemCountGE   i Count   {s Item}
				IN-GAME: ""Inventory Item Count >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Two inputs. True if number of Item in inventory is >= Count. Is not a regex.
				EXAMPLE: ItemCountGE 25 {Prismatic Taper}
						 --> True when Prismatic Taper supply in inventory is >= 25.

		ItemCountLE   i Count   {s Item}
				IN-GAME: ""Inventory Item Count <="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Two inputs. True if number of Item in inventory is <= Count. Is not a regex.
				EXAMPLE: ItemCountLE 25 {Prismatic Taper}
						 --> True when Prismatic Taper supply in inventory is <= 25. (Uh-oh!)

		MainSlotsLE   i Count
				IN-GAME: ""Pack Slots <="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if number of empty slots remaining in character's main pack
				inventory is <= Count.
				EXAMPLE: MainSlotsLE 7
						 --> True when <=7 inventory slots remain empty in character's main pack.

		MobsInDist_Name   i Count   d Distance   {r Name}
				IN-GAME: ""Monster Name Count Within Distance"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Three inputs. True if number of (regex-match) monster Name within Distance is >= Count. Completely
				ignores monster priority (including if it's -1).
				EXAMPLE: MobsInDist_Name 5 13.7 {Drudge Lurker}
						 --> True when >=5 Drudge Lurkers are within 13.7 yards of character.

		MobsInDist_Priority   i Count   d Distance   i Priority
				IN-GAME: ""Monster Priority Count Within Distance"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Three inputs. True if number of exact-Priority monsters within Distance is >= Count.
				EXAMPLE: MobsInDist_Priority 6 4.7 2
						 --> True when >=6 monsters of priority >=2 are within 4.7 yards of character.

		NavEmpty   (none)
				IN-GAME: ""Navroute empty"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True if current navroute is empty.
				EXAMPLE: NavEmpty
						 --> True when the current navroute is empty.

		NeedToBuff   (none)
				IN-GAME: ""Need to Buff"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True if VTank's settings determine character needs to buff.
				EXAMPLE: NeedToBuff
						 --> True when VTank's settings determine the character requires buffing.

		Never   (none)
				IN-GAME: ""Never"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. Never True. (Never do the corresponding Action.)
				EXAMPLE: Never
						 --> Never is False. (Never True.)

		NoMobsInDist   d Distance
				IN-GAME: ""No Monsters Within Distance"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if there are no monsters within Distance of character. Ignores Priority entirely.
				EXAMPLE: NoMobsInDist 20.6
						 --> True when no mobs are within 20.6 yards of character.

		Not   p Condition
				IN-GAME: ""Not"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if Condition operation is False. (May be All or Any.)
				EXAMPLE: Not All
							Always
							Never
						 --> The Not is True because it inverts the All, which is False.

		PSecsInStateGE   i Seconds
				IN-GAME: ""Seconds in state (P) >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if time elapsed since entering current state >= Seconds.
				Persistent timer; does not reset if meta is stopped/started.
				EXAMPLE: PSecsInStateGE 15
						 --> True 15 seconds after entering (and staying in) the rule's state, whether or not the meta's
							 execution is turned off/on.

		SecsInStateGE   i Seconds
				IN-GAME: ""Seconds in state >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: One input. True if time elapsed since entering current state >= Seconds. Resets timer if meta is
				stopped/started.
				EXAMPLE: SecsInStateGE 12
						 --> True 12 seconds after entering (and staying in) the rule's state, so long as the meta has
							 been running the whole time. (It resets the timer counter to zero if the meta is turned off
							 and back on, as if it's just entered the state.)
				
		SecsOnSpellGE   i Seconds   i SpellID
				IN-GAME: ""Time Left On Spell >="" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: Two inputs. True if time remaining on spell with SpellID is >= Seconds.
				EXAMPLE: SecsOnSpellGE 120 4291
						 --> True if >=120 seconds remain on 'Incantation of Armor Self', which has a SpellID of 4291.
							 (Execute a '/vt dumpspells' command in-game. The far left column of the file it creates is the
							 SpellID column.)
				
		VendorOpen   (none)
				IN-GAME: ""Any Vendor Open"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True when any vendor window is opened.
				EXAMPLE: VendorOpen
						 --> True if any vendor window is open.

		VendorClosed   (none)
				IN-GAME: ""Vendor Closed"" on Condition drop-down menu (top left when defining a Rule).
				DETAILS: No inputs. True when a vendor window is closed.
				EXAMPLE: VendorClosed
						 --> True when vendor window is closed.

	~~ ACTION operations (DO:)																								

		CallState   {s ToState}   {s ReturnState}
				IN-GAME: ""Call Meta State"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Two inputs. Transitions to state ToState, placing ReturnState on the 'call stack' in order to
				remember where to go when ready to return. (See: Return.) Keep CallState and Return in careful balance.
				EXAMPLE: CallState {Do Something} {Done With Something}
						 --> Sets state to state 'Do Something', pushing 'Done With Something' onto the call stack, for later
							 popping, to 'return'.

		Chat   {s Text}
				IN-GAME: ""Chat Command"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: One input. 'Send' Text as Chat. Do not confuse this with the Action ChatExpr.
				EXAMPLE: Chat {/vt jump 137 true 648}
						 --> The text is entered into and 'sent' to the ChatWindow, causing VTank to turn your character to
							 face a heading of 137 degrees, and then shift-jump after 'holding space' for 648 milliseconds.

		ChatExpr   {s ChatCode}
				IN-GAME: ""Chat Expression"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: One input. Evaluates ChatCode as a 'code', then 'sends' it to ChatWindow. Do not confuse this with
				the Action Chat.
				EXAMPLE: ChatExpr {\/t +getcharstringprop[1]+\, Hi\!}
						 --> Character @tells itself, 'Hi!'

		DoAll   (none directly)
				IN-GAME: ""All"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Action operations inside it (on
				following lines, tabbed in one more time). Do not confuse with Condition All.
				EXAMPLE: DoAll
							Chat {/t Eskarina, Hi!}
							Chat {*dance*}
						 --> Sends Eskarina a direct message of 'Hi!', then emote-dances.

		DoExpr   {s Code}
				IN-GAME: ""Expression Action"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: One input. Executes Code. Do not confuse this with the Condition Expr, or the Action ChatExpr.
				EXAMPLE: DoExpr {setvar[mycoords,getplayercoordinates[]]}
						 --> Sets variable mycoords to character's current coordinates (coordinate object).

		EmbedNav   l Tag   {s Name} {s Transform (optional)}
				IN-GAME: ""Load Embedded Nav Route"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Two inputs (or three). Tag is only used as a 'handle' to reference a navroute in the list of navs at
				the bottom of a metaf file, where it is marked with the same Tag. Name is the name displayed in-game, when
				you examine the embedded name in the meta. Note that Tag can be anything you want it to be, so long as it's a
				valid literal and is distinct from all other nav tags. (There's no reason it needs to be nav## in
				format; it could just as easily be OlthoiMatronHive instead.) The optional Transform input is a string
				containing seven doubles, separated by spaces: {a b c d e f g}, where
							New			  Old
							[x]	  [a b 0] [x]   [e]
							[y] = [c d 0] [y] + {f]
							[z]	  [0 0 1] [z]   [g].
				Every nav node with coordinates in it gets transformed accordingly during conversion into .met. The default
				transformation is {1 0 0 1 0 0 0}, which leaves points unchanged.
				EXAMPLE (REAL):	STATE: {Hive120}
									IF:	Never
										DO:	EmbedNav navH120 {H120}
								STATE: {Hive90}
									IF:	Never
										DO:	EmbedNav navH120 {H90} {1 0 0 1 0 -0.8 0}
						 --> The first EmbedNav (two inputs) references a complicated nav tagged as navH120, defined in the
							 nav section at the bottom of the file, and named 'H120' where it's embedded in the code. It
							 was created in Matron Hive East (120+), and it gets transformed with the default transform
							 when being converted into .met. The second EmbedNav (three inputs) references the same nav
							 as the first (still navH120), but two important differences happen here when converting into
							 .met: first, this nav gets named 'H90' where it's embedded in the code, and second, that third
							 input gets applied as a transform to all the nav nodes. That is the correct transform to put
							 it exactly in the same relative placement within Matron Hive West (90+) as it was in Matron
							 Hive East (120+). And, that's it. Those seven numbers created an entirely new nav.
							 In case you're wondering: South(40+) is {1 0 0 1 1.6 0 0} and North(60+) is {1 0 0 1 1.6 0.8 0}.

		None   (none)
				IN-GAME: ""None"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: No inputs. Do nothing. (Action: None.)
				EXAMPLE: None
						 --> Nothing happens.

		Return   (none directly)
				IN-GAME: ""Return From Call"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: No direct inputs but does expect a state to be on the 'call stack' because it needs to pop a
				state from the stack in order to transition the meta to whatever that state is. (See CallState.) Keep
				CallState and Return in careful balance.
				EXAMPLE: CallState {Do Something} {Done With Something}
						 --> Sets state to state 'Do Something', pushing 'Done With Something' onto
							 the call stack, for later popping, to 'return'.

		SetState   {s Name}
				IN-GAME: ""Set Meta State"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: One input. Set current state to state Name.
				EXAMPLE: SetState {Target Name}
						 --> Meta transitions to state 'Target Name'.

		SetWatchdog   d Distance   d Seconds   {s State name}
				IN-GAME: ""Set Watchdog"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Three inputs. You can set a watchdog in a state that is triggered if at any time while in that
				state your character has not moved >=Distance over the preceding Seconds of time. If triggered, State is
				called. (Returning from it, re-enters the original state.)
				EXAMPLE: SetWatchdog 12.3 4.6 {Oh, no!}
						--> If at some point while in the current state your character hasn't moved at least 12.3 yards in
							the preceding 4.6 seconds, state 'Oh, no!' is called.

		ClearWatchdog   (none)
				IN-GAME: ""Clear Watchdog"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: No inputs. Clears the watchdog for the current state.
				EXAMPLE: ClearWatchdog
						--> Clears (gets rid of) the current watchdog in this state (if any).

		GetOpt   {s Option}   {s Variable}
				IN-GAME: ""Get VT Option"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Two inputs. Gets the current value of the VirindiTank Option and saves it in Variable.
				EXAMPLE: GetOpt {OpenDoors} {doors}
						--> Gets current status of the 'OpenDoors' VirindiTank option, and stores it in variable 'doors'.

		SetOpt   {s Option}   {s Expression}
				IN-GAME: ""Set VT Option"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Two inputs. Sets the VirindiTank Option based upon the results of evaluating Expression.
				EXAMPLE: SetOpt {OpenDoors} {istrue[wobjectfindnearestdoor[]]}
						--> Sets the VirindiTank 'OpenDoors' option to true if any doors are nearby, false otherwise.

		CreateView   {s view Handle}   {s XML}
				IN-GAME: ""Create View"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Creates a Virindi View with the designated Handle, the layout of which is defined by XML. The XML
				must be on a single line (no line breaks). If the first character in input XML is ':', the remainder is
				treated as a reference to an external XML file, which metaf flattens into a single line and treats as if
				it's the XML input string instead of the file-reference (meaning the same input-string restrictions apply
				with regard to metaf string delimiters). ---- Are other controls (etc.) recognized? I don't know. For a
				bit more, see: http://www.virindi.net/wiki/index.php/Meta_Views
				EXAMPLE: CreateView {myview} {<?xml version=""1.0""?><view width=""300"" height=""200"" title=""My View""><control type=""layout""><control type=""button"" name=""btnA1"" left=""20"" top=""10"" width=""50"" height=""20"" text=""B1"" actionexpr=""chatbox[\/vt echo B\1\!]"" setstate=""st""/></control></view>}
						--> Creates new Virindi View with handle 'myview' that makes a 300x200 window with title 'My View',
							and one 50x20 button with 'B1' text on it, at (20,10). Pressing it sets the state to 'st', and
							evaluates the expression 'chatbox[\/vt echo B1\!]'.
				EXAMPLE2: CreateView {myview} {:myview.xml}
						--> Assuming myview.xml contains the same XML as above (whether split across multiple lines or not),
						    this does exactly the same thing as above.

		DestroyView   {s View}
				IN-GAME: ""Destroy View"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: Destroy the designated View.
				EXAMPLE: DestroyView {myview}
						--> Destroys the Virindi View with handle 'myview'.
						
		DestroyAllViews   (none)
				IN-GAME: ""Destroy All Views"" on Action drop-down menu (top right when defining a Rule).
				DETAILS: No inputs. Destroys all views for this meta.
				EXAMPLE: DestroyAllViews
						--> Destroys any views that exist for this meta.

	~~ NAV TYPES (NAV:)																										

		NAV:   l Tag   l Type
				IN-GAME: Route tab. (Select Type from drop-down menu at bottom left.)
				DETAILS: Two inputs. Tag is only used as a 'handle' to uniquely identify a nav  as distinct from all other
				navs in the nav listing. Type is what type of nav it is. (See below.) Note that Tag can be anything you want
				it to be, so long as it's a valid literal and is distinct from all other nav tags. (There's no reason it
				needs to be nav## in format; it could just as easily be OlthoiMatronHive instead.)
				   circular - A navroute that infinitely repeats itself by looping back to the start when it hits the end.
					 linear - A navroute that infinitely repeats itself by alternatingly running it forward and backward.
					 follow - A navroute that is a single node in length (see below), to chase a specified player.
					   once - A navroute that does not repeat. Nodes are consumed (disappear) when they are reached.
				EXAMPLE: NAV: ToMyXPPlace once
							(Zero or more nav nodes appear on immediately following lines, tabbed in one time.)
						 --> Creates a navroute of type Once that is cited by meta code via the tag ToMyXPPlace.

	~~ NAV NODE FORMATS																										

				 Follow - flw   h Target GUID   {s Target Name}
							IN-GAME: Select another player; click ""FC"" (Follow Character) on mini-remote.
				  Point - pnt   d X   d Y   d Z
							IN-GAME: Route tab. ""Add"" button, at top, near right.
							Colored more lightly because plain points tend to be 'the movement between the action'.
				 Portal - prt   d X   d Y   d Z   h Portal GUID
							IN-GAME: Unavailable. (DEPRECATED in VTank.)
		   Recall Spell - rcl   d X   d Y   d Z   {s Full Name of Recall Spell}
							IN-GAME: Route tab. ""Add Recall"" button with drop-down menu to right of it, in middle.
							Recognized Full Recall Spell Names are, exactly:
								{Primary Portal Recall}					{Paradox-touched Olthoi Infested Area Recall}
								{Secondary Portal Recall}				{Call of the Mhoire Forge}
								{Lifestone Recall}						{Colosseum Recall}
								{Lifestone Sending}						{Facility Hub Recall}
								{Portal Recall}							{Gear Knight Invasion Area Camp Recall}
								{Recall Aphus Lassel}					{Lost City of Neftet Recall}
								{Recall the Sanctuary}					{Return to the Keep}
								{Recall to the Singularity Caul}		{Rynthid Recall}
								{Glenden Wood Recall}					{Viridian Rise Recall}
								{Aerlinthe Recall}						{Viridian Rise Great Tree Recall}}
								{Mount Lethe Recall}					{Celestial Hand Stronghold Recall}
								{Ulgrim's Recall}						{Radiant Blood Stronghold Recall}
								{Bur Recall}							{Eldrytch Web Stronghold Recall}
				  Pause - pau   d X   d Y   d Z   d Pause (in ms)
							IN-GAME: Route tab. ""Add Pause"" button with 'seconds' text field to right of it, near bottom.
		ChatField (any) - cht   d X   d Y   d Z   {s ChatInput}
							IN-GAME: Route tab. ""Add Chat"" button with text field to right of it, at bottom right.
			 Use Vendor - vnd   d X   d Y   d Z   h Target GUID   {s Target Name}
							IN-GAME: Route tab. ""Open Vendor"" button at top right.
		 Use Portal/NPC - ptl   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
							IN-GAME: Route tab. ""Use Portal/NPC"" button, near top, near right.
							Allowed ObjectClass: 14 (Portal), 37 (NPC), 10 (Container, e.g., 'Dangerous Portal Device').
		    Talk to NPC - tlk   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
							IN-GAME: Route tab. ""Add NPC Talk"" button, near top, on right.
							Allowed ObjectClass: 37 (NPC).
		 Nav Checkpoint - chk   d X   d Y   d Z
							IN-GAME: Entered via chat command: /vt addnavcheckpoint
									 http://www.virindi.net/wiki/index.php/Virindi_Tank_Commands
			   Nav Jump - jmp   d X   d Y   d Z   d HeadingInDegrees   {s HoldShift (True|False)}   d Delay (in ms)
							IN-GAME: Entered via chat command: /vt addnavjump [heading] [shift: true or false] [milliseconds]
									 http://www.virindi.net/wiki/index.php/Virindi_Tank_Commands

~~																															
~~ 3. VIRINDITANK FUNCTIONS					http://www.virindi.net/wiki/index.php/Meta_Expressions#Function_Information		
~~																															

	~~ Data-type abbreviations:		
		i - Integer. A whole number.
		d - Double. A decimal number.
		s - String. A chain of arbitrary characters inside braces { }. Can't contain braces, unless doubled.
		r - RegEx. Same as S, but expected to be a Regular Expression.
		c - Coordinates. A coordinates object.
	   dr - Door. A door object.
	   sw - Stopwatch. A stopwatch object.
	   wo - World Object. An in-game world object.
		o - Object. A general object datatype.

	~~ VARIABLES																											
		testvar[s]					touchvar[s]
		getvar[s]					clearallvars[]
		setvar[s,o]					clearvar[s]
		
	~~ CHARACTER   			Character properties http://www.virindi.net/wiki/index.php/Meta_Expressions#Object_Properties	
		getcharintprop[i]				wobjectgetplayer[]						getplayerlandcell[]
		getchardoubleprop[i]			getplayercoordinates[]					getcharvital_base[i] (1=H,2=S,3=M)
		getcharboolprop[i]				getcharskill_base[i]					getcharvital_current[i] (1=H,2=S,3=M)
		getcharquadprop[i]				getcharskill_buffed[i]					getcharvital_buffedmax[i] (1=H,2=S,3=M)
		getcharstringprop[i]			getcharskill_traininglevel[i] (returns: 0=Unuse,1=Untrain,2=Train,3=Spec)
		
	~~ CASTING   '/vt dumpspells'  http://www.virindi.net/wiki/index.php/Virindi_Tank_Commands#.2Fvt_commands_-_Game_Info	
		getisspellknown[i]				getcancastspell_hunt[i]					getcancastspell_buff[i]
		actiontryequipanywand[]			actiontrycastbyid[i]					actiontrycastbyidontarget[i,o]
		
	~~ INVENTORY																											
		wobjectfindininventorybytemplatetype[i]
		wobjectfindininventorybyname[s]
		wobjectfindininventorybynamerx[r]
		
	~~ LOCATION																												
		getplayerlandcell[]					coordinategetns[c]				coordinatedistancewithz[c,c]
		getplayercoordinates[]				coordinategetwe[c]				coordinatedistanceflat[c,c]
		coordinatetostring[c]				coordinategetz[c]				wobjectgetphysicscoordinates[wo]
		coordinateparse[s]
		
	~~ OBJECT																												
		wobjectgetselection[]					wobjectgetname[o]
		wobjectgetplayer[]						wobjectgetphysicscoordinates[wo]
		wobjectfindnearestmonster[]				wobjectfindnearestbyobjectclass[i]
		wobjectfindnearestdoor[]				wobjectfindnearestbynameandobjectclass[i,r]
		wobjectgetisdooropen[dr]				wobjectfindininventorybyname[s]
		wobjectgetobjectclass[o]				wobjectfindininventorybynamerx[r]
		wobjectgettemplatetype[o]				wobjectfindininventorybytemplatetype[i]
		getobjectinternaltype[o]
		
	~~ ACTION																												
		actiontryselect[o]						actiontryequipanywand[]
		actiontryuseitem[o]						actiontrycastbyid[i]
		actiontrygiveitem[o,o tgt]				actiontrycastbyidontarget[i,o]
		actiontryapplyitem[o use,o tgt]
		
	~~ UI																													
		CHAT:			chatbox[s]								chatboxpaste[s]
		HUD, etc.:		statushud[s key,s val]					statushudcolored[s key,s val,s rgbInt]
						uigetcontrol[s window,s ctrl]			uisetlabel[o ctrl,s label]
						
	~~ LOGIC		Note: iif always evaluates both T and F clauses; it just returns only one of them.						
		iif[eval,retIfT,retIfF]			isfalse[o]				istrue[o]
		
	~~ NUMBER																												
		randint[i min,i max]					round[d]					coordinategetns[c]
		cstr[i]									abs[d]						coordinategetwe[c]
		cstrf[d,s format(probably G)]			getcharintprop[i]			coordinategetz[c]
		cnumber[s]								getchardoubleprop[i]
		floor[d]								getcharquadprop[i]
		ceiling[d]								getcharboolprop[i]
		
	~~ STRING																												
		cstr[i]									coordinatetostring[c]
		strlen[s]								coordinateparse[s]
		cstrf[d,s format(probably G)]			wobjectgetname[o]
		cnumber[s]								chatbox[s]
		
	~~ TIME																													
		stopwatchcreate[]						stopwatchstop[sw]
		stopwatchstart[sw]						stopwatchelapsedseconds[sw]
		
	~~ MISCELLANEOUS																										
		getobjectinternaltype[o]  (returns 0=none, 1=number, 3=string, 7=object)

~~																															
~~ 4. UTILITYBELT FUNCTIONS (non-VT-overlapping)							https://utilitybelt.gitlab.io/docs/expressions/	
~~																															
~~ 		NOTE: UtilityBelt independently implements all VirindiTank functions (see Section 3), plus the following functions.	
~~																															

	~~ Data-type abbreviations:		
		i - Integer. A whole number.
		d - Double. A decimal number.
	   ch - Character. A single character from a string.
		s - String. A chain of arbitrary characters inside braces { }. Can't contain braces, unless doubled.
		r - RegEx. Same as S, but expected to be a Regular Expression.
		D - Dictionary. An associative array, i.e., dictionary datatype.
		L - List. An array (equivalent) datatype.
		c - Coordinates. A coordinates object.
	   ui - UIControl. A UIControl object.
	   wo - World Object. An in-game world object.
		o - Object. An object datatype.

	~~ AVATAR+																												
		getworldname[]			getitemcountininventorybyname[s]
		getheading[wo]			getitemcountininventorybynamerx[r]
		getheadingto[wo]		actiontrygiveprofile[s lootprofile, s tgt]
		vitae[]

	~~ FELLOWSHIP																											
		getfellowshipstatus[]		getfellowshipname[]				getfellowshipisopen[]
		getfellowshipisleader[]		getfellowshipleaderid[]			getfellowshipcanrecruit[]
		getfellowid[i]				getfellowshipcount[]			getfellowshiplocked[]
		getfellowname[i]			getfellowshipisfull[]

	~~ MATH																													
		sin[d]		cos[d]		tan[d]		sqrt[d]
		asin[d]		acos[d]		atan[d]		atan2[d y,d x]
		sinh[d]		cosh[d]		tanh[d]

	~~ MISCELLANEOUS: META, STRING, CHARACTER																				
		vtsetmetastate[s]		getregexmatch[s,r]		echo[s msg,i color]		chr[i char]		ord[ch]

	~~ OBJECT																												
		wobjectgetid[wo]							wobjectgethealth[wo]
		wobjectfindbyid[i]							wobjectgetintprop[wo,i]
		wobjectfindnearestbytemplatetype[i]			wobjectgetopencontainer[]

	~~ QUEST																												
		testquestflag[s]				getquestktprogress[s]
		isrefreshingquests[]			getquestktrequired[s]
		getqueststatus[s]

	~~ TIME																													
		getisday[]						getgamehour[]			getgamehourname[i hourIndex]
		getisnight[]					getgameday[]			getgameticks[]
		getminutesuntilday[]			getgamemonth[]			getgamemonthname[i monthIndex]
		getminutesuntilnight[]			getgameyear[]

	~~ UI																													
		uisetvisible[ui ctrl, i visible]		uiviewvisible[s window]			uiviewexists[s window]
	
	~~ VARIABLES																											
		getgvar[s]			touchgvar[s]				getpvar[s]			touchpvar[s]
		setgvar[s,o]		cleargvar[s]				setpvar[s,o]		clearpvar[s]
		testgvar[s]			clearallgvars[]				testpvar[s]			clearallpvars[]

		~~ DICTIONARIES					
		dictgetitem[D,s key]		dictcreate[...items] (items=key,value pairs: k1,v1,k2,v2,...)
		dicthaskey[D,s key]			dictadditem[D,s key,o val]
		dictkeys[D]					dictremovekey[D,s key]
		dictvalues[D]				dictclear[D]
		dictsize[D]					dictcopy[D]

		~~ LISTS						
		listgetitem[L,i]			listpop[L,i=-1]			listcreate[...items] (items=values: v1,v2,v3,...)
		listcontains[L,o]			listremove[L,o]			listadd[L,o]
		listindexof[L,o]			listremoveat[L,i]		listinsert[L,o,i]
		listlastindexof[L,o]		listclear[L]			listcopy[L]
		listcount[L]										listreverse[L]

~~																															
~~		METa Alternate Format																					Created by	
~~			  REFERENCE																							 Eskarina	
~~																															";
		public static Dictionary<int, string> allSpells_IdName = new Dictionary<int, string>()
		{ {1,"Strength Other I"}, {2,"Strength Self I"}, {3,"Weakness Other I"}, {4,"Weakness Self I"}, {5,"Heal Other I"}, {6,"Heal Self I"}, {7,"Harm Other I"}, {8,"Harm Self I"}, {9,"Infuse Mana Other I"}, {15,"Vulnerability Other I"}, {16,"Vulnerability Self I"}, {17,"Invulnerability Other I"}, {18,"Invulnerability Self I"}, {19,"Fire Protection Other I"}, {20,"Fire Protection Self I"}, {21,"Fire Vulnerability Other I"}, {22,"Fire Vulnerability Self I"}, {23,"Armor Other I"}, {24,"Armor Self I"}, {25,"Imperil Other I"}, {26,"Imperil Self I"}, {27,"Flame Bolt I"}, {28,"Frost Bolt I"}, {35,"Aura of Blood Drinker Self I"}, {36,"Blood Loather I"}, {37,"Blade Bane I"}, {38,"Blade Lure I"}, {47,"Primary Portal Tie"}, {48,"Primary Portal Recall"}, {49,"Aura of Swift Killer Self I"}, {50,"Leaden Weapon I"}, {51,"Impenetrability I"}, {53,"Rejuvenation Other I"}, {54,"Rejuvenation Self I"}, {57,"Magic Bolt"}, {58,"Acid Stream I"}, {59,"Acid Stream II"}, {60,"Acid Stream III"}, {61,"Acid Stream IV"}, {62,"Acid Stream V"}, {63,"Acid Stream VI"}, {64,"Shock Wave I"}, {65,"Shock Wave II"}, {66,"Shock Wave III"}, {67,"Shock Wave IV"}, {68,"Shock Wave V"}, {69,"Shock Wave VI"}, {70,"Frost Bolt II"}, {71,"Frost Bolt III"}, {72,"Frost Bolt IV"}, {73,"Frost Bolt V"}, {74,"Frost Bolt VI"}, {75,"Lightning Bolt I"}, {76,"Lightning Bolt II"}, {77,"Lightning Bolt III"}, {78,"Lightning Bolt IV"}, {79,"Lightning Bolt V"}, {80,"Lightning Bolt VI"}, {81,"Flame Bolt II"}, {82,"Flame Bolt III"}, {83,"Flame Bolt IV"}, {84,"Flame Bolt V"}, {85,"Flame Bolt VI"}, {86,"Force Bolt I"}, {87,"Force Bolt II"}, {88,"Force Bolt III"}, {89,"Force Bolt IV"}, {90,"Force Bolt V"}, {91,"Force Bolt VI"}, {92,"Whirling Blade I"}, {93,"Whirling Blade II"}, {94,"Whirling Blade III"}, {95,"Whirling Blade IV"}, {96,"Whirling Blade V"}, {97,"Whirling Blade VI"}, {99,"Acid Blast III"}, {100,"Acid Blast IV"}, {101,"Acid Blast V"}, {102,"Acid Blast VI"}, {103,"Shock Blast III"}, {104,"Shock Blast IV"}, {105,"Shock Blast V"}, {106,"Shock Blast VI"}, {107,"Frost Blast III"}, {108,"Frost Blast IV"}, {109,"Frost Blast V"}, {110,"Frost Blast VI"}, {111,"Lightning Blast III"}, {112,"Lightning Blast IV"}, {113,"Lightning Blast V"}, {114,"Lightning Blast VI"}, {115,"Flame Blast III"}, {116,"Flame Blast IV"}, {117,"Flame Blast V"}, {118,"Flame Blast VI"}, {119,"Force Blast III"}, {120,"Force Blast IV"}, {121,"Force Blast V"}, {122,"Force Blast VI"}, {123,"Blade Blast III"}, {124,"Blade Blast IV"}, {125,"Blade Blast V"}, {126,"Blade Blast VI"}, {127,"Acid Volley III"}, {128,"Acid Volley IV"}, {129,"Acid Volley V"}, {130,"Acid Volley VI"}, {131,"Bludgeoning Volley III"}, {132,"Bludgeoning Volley IV"}, {133,"Bludgeoning Volley V"}, {134,"Bludgeoning Volley VI"}, {135,"Frost Volley III"}, {136,"Frost Volley IV"}, {137,"Frost Volley V"}, {138,"Frost Volley VI"}, {139,"Lightning Volley III"}, {140,"Lightning Volley IV"}, {141,"Lightning Volley V"}, {142,"Lightning Volley VI"}, {143,"Flame Volley III"}, {144,"Flame Volley IV"}, {145,"Flame Volley V"}, {146,"Flame Volley VI"}, {147,"Force Volley III"}, {148,"Force Volley IV"}, {149,"Force Volley V"}, {150,"Force Volley VI"}, {151,"Blade Volley III"}, {152,"Blade Volley IV"}, {153,"Blade Volley V"}, {154,"Blade Volley VI"}, {157,"Summon Primary Portal I"}, {158,"Summon Primary Portal II"}, {159,"Regeneration Other I"}, {160,"Regeneration Other II"}, {161,"Regeneration Other III"}, {162,"Regeneration Other IV"}, {163,"Regeneration Other V"}, {164,"Regeneration Other VI"}, {165,"Regeneration Self I"}, {166,"Regeneration Self II"}, {167,"Regeneration Self III"}, {168,"Regeneration Self IV"}, {169,"Regeneration Self V"}, {170,"Regeneration Self VI"}, {171,"Fester Other I"}, {172,"Fester Other II"}, {173,"Fester Other III"}, {174,"Fester Other IV"}, {175,"Fester Other V"}, {176,"Fester Other VI"}, {178,"Fester Self I"}, {179,"Fester Self II"}, {180,"Fester Self III"}, {181,"Fester Self IV"}, {182,"Fester Self V"}, {183,"Fester Self VI"}, {184,"Rejuvenation Other II"}, {185,"Rejuvenation Other III"}, {186,"Rejuvenation Other IV"}, {187,"Rejuvenation Other V"}, {188,"Rejuvenation Other VI"}, {189,"Rejuvenation Self II"}, {190,"Rejuvenation Self III"}, {191,"Rejuvenation Self IV"}, {192,"Rejuvenation Self V"}, {193,"Rejuvenation Self VI"}, {194,"Exhaustion Other I"}, {195,"Exhaustion Other II"}, {196,"Exhaustion Other III"}, {197,"Exhaustion Other IV"}, {198,"Exhaustion Other V"}, {199,"Exhaustion Other VI"}, {200,"Exhaustion Self I"}, {201,"Exhaustion Self II"}, {202,"Exhaustion Self III"}, {203,"Exhaustion Self IV"}, {204,"Exhaustion Self V"}, {205,"Exhaustion Self VI"}, {206,"Mana Renewal Other I"}, {207,"Mana Renewal Other II"}, {208,"Mana Renewal Other III"}, {209,"Mana Renewal Other IV"}, {210,"Mana Renewal Other V"}, {211,"Mana Renewal Other VI"}, {212,"Mana Renewal Self I"}, {213,"Mana Renewal Self II"}, {214,"Mana Renewal Self III"}, {215,"Mana Renewal Self IV"}, {216,"Mana Renewal Self V"}, {217,"Mana Renewal Self VI"}, {218,"Mana Depletion Other I"}, {219,"Mana Depletion Other II"}, {220,"Mana Depletion Other III"}, {221,"Mana Depletion Other IV"}, {222,"Mana Depletion Other V"}, {223,"Mana Depletion Other VI"}, {224,"Mana Depletion Self I"}, {225,"Mana Depletion Self II"}, {226,"Mana Depletion Self III"}, {227,"Mana Depletion Self IV"}, {228,"Mana Depletion Self V"}, {229,"Mana Depletion Self VI"}, {230,"Vulnerability Other II"}, {231,"Vulnerability Other III"}, {232,"Vulnerability Other IV"}, {233,"Vulnerability Other V"}, {234,"Vulnerability Other VI"}, {235,"Vulnerability Self II"}, {236,"Vulnerability Self III"}, {237,"Vulnerability Self IV"}, {238,"Vulnerability Self V"}, {239,"Vulnerability Self VI"}, {240,"Invulnerability Other II"}, {241,"Invulnerability Other III"}, {242,"Invulnerability Other IV"}, {243,"Invulnerability Other V"}, {244,"Invulnerability Other VI"}, {245,"Invulnerability Self II"}, {246,"Invulnerability Self III"}, {247,"Invulnerability Self IV"}, {248,"Invulnerability Self V"}, {249,"Invulnerability Self VI"}, {250,"Impregnability Other I"}, {251,"Impregnability Other II"}, {252,"Impregnability Other III"}, {253,"Impregnability Other IV"}, {254,"Impregnability Other V"}, {255,"Impregnability Other VI"}, {256,"Impregnability Self I"}, {257,"Impregnability Self II"}, {258,"Impregnability Self III"}, {259,"Impregnability Self IV"}, {260,"Impregnability Self V"}, {261,"Impregnability Self VI"}, {262,"Defenselessness Other I"}, {263,"Defenselessness Other II"}, {264,"Defenselessness Other III"}, {265,"Defenselessness Other IV"}, {266,"Defenselessness Other V"}, {267,"Defenselessness Other VI"}, {268,"Magic Resistance Other I"}, {269,"Magic Resistance Other II"}, {270,"Magic Resistance Other III"}, {271,"Magic Resistance Other IV"}, {272,"Magic Resistance Other V"}, {273,"Magic Resistance Other VI"}, {274,"Magic Resistance Self I"}, {275,"Magic Resistance Self II"}, {276,"Magic Resistance Self III"}, {277,"Magic Resistance Self IV"}, {278,"Magic Resistance Self V"}, {279,"Magic Resistance Self VI"}, {280,"Magic Yield Other I"}, {281,"Magic Yield Other II"}, {282,"Magic Yield Other III"}, {283,"Magic Yield Other IV"}, {284,"Magic Yield Other V"}, {285,"Magic Yield Other VI"}, {286,"Magic Yield Self I"}, {287,"Magic Yield Self II"}, {288,"Magic Yield Self III"}, {289,"Magic Yield Self IV"}, {290,"Magic Yield Self V"}, {291,"Magic Yield Self VI"}, {292,"Light Weapon Mastery Other I"}, {293,"Light Weapon Mastery Other II"}, {294,"Light Weapon Mastery Other III"}, {295,"Light Weapon Mastery Other IV"}, {296,"Light Weapon Mastery Other V"}, {297,"Light Weapon Mastery Other VI"}, {298,"Light Weapon Mastery Self I"}, {299,"Light Weapon Mastery Self II"}, {300,"Light Weapon Mastery Self III"}, {301,"Light Weapon Mastery Self IV"}, {302,"Light Weapon Mastery Self V"}, {303,"Light Weapon Mastery Self VI"}, {304,"Light Weapon Ineptitude Other I"}, {305,"Light Weapon Ineptitude Other II"}, {306,"Light Weapon Ineptitude Other III"}, {307,"Light Weapon Ineptitude Other IV"}, {308,"Light Weapon Ineptitude Other V"}, {309,"Light Weapon Ineptitude Other VI"}, {310,"Light Weapon Ineptitude Self I"}, {311,"Light Weapon Ineptitude Self II"}, {312,"Light Weapon Ineptitude Self III"}, {313,"Light Weapon Ineptitude Self IV"}, {314,"Light Weapon Ineptitude Self V"}, {315,"Light Weapon Ineptitude Self VI"}, {316,"Finesse Weapon Mastery Other I"}, {317,"Finesse Weapon Mastery Other II"}, {318,"Finesse Weapon Mastery Other III"}, {319,"Finesse Weapon Mastery Other IV"}, {320,"Finesse Weapon Mastery Other V"}, {321,"Finesse Weapon Mastery Other VI"}, {322,"Finesse Weapon Mastery Self I"}, {323,"Finesse Weapon Mastery Self II"}, {324,"Finesse Weapon Mastery Self III"}, {325,"Finesse Weapon Mastery Self IV"}, {326,"Finesse Weapon Mastery Self V"}, {327,"Finesse Weapon Mastery Self VI"}, {328,"Finesse Weapon Ineptitude Other I"}, {329,"Finesse Weapon Ineptitude Other II"}, {330,"Finesse Weapon Ineptitude Other III"}, {331,"Finesse Weapon Ineptitude Other IV"}, {332,"Finesse Weapon Ineptitude Other V"}, {333,"Finesse Weapon Ineptitude Other VI"}, {334,"Finesse Weapon Ineptitude Self I"}, {335,"Finesse Weapon Ineptitude Self II"}, {336,"Finesse Weapon Ineptitude Self III"}, {337,"Finesse Weapon Ineptitude Self IV"}, {338,"Finesse Weapon Ineptitude Self V"}, {339,"Finesse Weapon Ineptitude Self VI"}, {340,"Light Weapon Mastery Other I"}, {341,"Light Weapon Mastery Other II"}, {342,"Light Weapon Mastery Other III"}, {343,"Light Weapon Mastery Other IV"}, {344,"Light Weapon Mastery Other V"}, {345,"Light Weapon Mastery Other VI"}, {346,"Light Weapon Mastery Self I"}, {347,"Light Weapon Mastery Self II"}, {348,"Light Weapon Mastery Self III"}, {349,"Light Weapon Mastery Self IV"}, {350,"Light Weapon Mastery Self V"}, {351,"Light Weapon Mastery Self VI"}, {352,"Light Weapon Ineptitude Other I"}, {353,"Light Weapon Ineptitude Other II"}, {354,"Light Weapon Ineptitude Other III"}, {355,"Light Weapon Ineptitude Other IV"}, {356,"Light Weapon Ineptitude Other V"}, {357,"Light Weapon Ineptitude Other VI"}, {358,"Light Weapon Ineptitude Self I"}, {359,"Light Weapon Ineptitude Self II"}, {360,"Light Weapon Ineptitude Self III"}, {361,"Light Weapon Ineptitude Self IV"}, {362,"Light Weapon Ineptitude Self V"}, {363,"Light Weapon Ineptitude Self VI"}, {364,"Light Weapon Mastery Other I"}, {365,"Light Weapon Mastery Other II"}, {366,"Light Weapon Mastery Other III"}, {367,"Light Weapon Mastery Other IV"}, {368,"Light Weapon Mastery Other V"}, {369,"Light Weapon Mastery Other VI"}, {370,"Light Weapon Mastery Self I"}, {371,"Light Weapon Mastery Self II"}, {372,"Light Weapon Mastery Self III"}, {373,"Light Weapon Mastery Self IV"}, {374,"Light Weapon Mastery Self V"}, {375,"Light Weapon Mastery Self VI"}, {376,"Light Weapon Ineptitude Other I"}, {377,"Light Weapon Ineptitude Other II"}, {378,"Light Weapon Ineptitude Other III"}, {379,"Light Weapon Ineptitude Other IV"}, {380,"Light Weapon Ineptitude Other V"}, {381,"Light Weapon Ineptitude Other VI"}, {382,"Light Weapon Ineptitude Self I"}, {383,"Light Weapon Ineptitude Self II"}, {384,"Light Weapon Ineptitude Self III"}, {385,"Light Weapon Ineptitude Self IV"}, {386,"Light Weapon Ineptitude Self V"}, {387,"Light Weapon Ineptitude Self VI"}, {388,"Light Weapon Mastery Other I"}, {389,"Light Weapon Mastery Other II"}, {390,"Light Weapon Mastery Other III"}, {391,"Light Weapon Mastery Other IV"}, {392,"Light Weapon Mastery Other V"}, {393,"Light Weapon Mastery Other VI"}, {394,"Light Weapon Mastery Self I"}, {395,"Light Weapon Mastery Self II"}, {396,"Light Weapon Mastery Self III"}, {397,"Light Weapon Mastery Self IV"}, {398,"Light Weapon Mastery Self V"}, {399,"Light Weapon Mastery Self VI"}, {400,"Light Weapon Ineptitude Other I"}, {401,"Light Weapon Ineptitude Other II"}, {402,"Light Weapon Ineptitude Other III"}, {403,"Light Weapon Ineptitude Other IV"}, {404,"Light Weapon Ineptitude Other V"}, {405,"Light Weapon Ineptitude Other VI"}, {406,"Light Weapon Ineptitude Self I"}, {407,"Light Weapon Ineptitude Self II"}, {408,"Light Weapon Ineptitude Self III"}, {409,"Light Weapon Ineptitude Self IV"}, {410,"Light Weapon Ineptitude Self V"}, {411,"Light Weapon Ineptitude Self VI"}, {412,"Heavy Weapon Mastery Other I"}, {413,"Heavy Weapon Mastery Other II"}, {414,"Heavy Weapon Mastery Other III"}, {415,"Heavy Weapon Mastery Other IV"}, {416,"Heavy Weapon Mastery Other V"}, {417,"Heavy Weapon Mastery Other VI"}, {418,"Heavy Weapon Mastery Self I"}, {419,"Heavy Weapon Mastery Self II"}, {420,"Heavy Weapon Mastery Self III"}, {421,"Heavy Weapon Mastery Self IV"}, {422,"Heavy Weapon Mastery Self V"}, {423,"Heavy Weapon Mastery Self VI"}, {424,"Heavy Weapon Ineptitude Other I"}, {425,"Heavy Weapon Ineptitude Other II"}, {426,"Heavy Weapon Ineptitude Other III"}, {427,"Heavy Weapon Ineptitude Other IV"}, {428,"Heavy Weapon Ineptitude Other V"}, {429,"Heavy Weapon Ineptitude Other VI"}, {430,"Heavy Weapon Ineptitude Self I"}, {431,"Heavy Weapon Ineptitude Self II"}, {432,"Heavy Weapon Ineptitude Self III"}, {433,"Heavy Weapon Ineptitude Self IV"}, {435,"Heavy Weapon Ineptitude Self V"}, {436,"Heavy Weapon Ineptitude Self VI"}, {437,"Light Weapon Mastery Other I"}, {438,"Light Weapon Mastery Other II"}, {439,"Light Weapon Mastery Other III"}, {440,"Light Weapon Mastery Other IV"}, {441,"Light Weapon Mastery Other V"}, {442,"Light Weapon Mastery Other VI"}, {443,"Light Weapon Mastery Self I"}, {444,"Light Weapon Mastery Self II"}, {445,"Light Weapon Mastery Self III"}, {446,"Light Weapon Mastery Self IV"}, {447,"Light Weapon Mastery Self V"}, {448,"Light Weapon Mastery Self VI"}, {449,"Light Weapon Ineptitude Other I"}, {450,"Light Weapon Ineptitude Other II"}, {451,"Light Weapon Ineptitude Other III"}, {452,"Light Weapon Ineptitude Other IV"}, {453,"Light Weapon Ineptitude Other V"}, {454,"Light Weapon Ineptitude Other VI"}, {455,"Light Weapon Ineptitude Self I"}, {456,"Light Weapon Ineptitude Self II"}, {457,"Light Weapon Ineptitude Self III"}, {458,"Light Weapon Ineptitude Self IV"}, {459,"Light Weapon Ineptitude Self V"}, {460,"Light Weapon Ineptitude Self VI"}, {461,"Missile Weapon Mastery Other I"}, {462,"Missile Weapon Mastery Other II"}, {463,"Missile Weapon Mastery Other III"}, {464,"Missile Weapon Mastery Other IV"}, {465,"Missile Weapon Mastery Other V"}, {466,"Missile Weapon Mastery Other VI"}, {467,"Missile Weapon Mastery Self I"}, {468,"Missile Weapon Mastery Self II"}, {469,"Missile Weapon Mastery Self III"}, {470,"Missile Weapon Mastery Self IV"}, {471,"Missile Weapon Mastery Self V"}, {472,"Missile Weapon Mastery Self VI"}, {473,"Missile Weapon Ineptitude Other I"}, {474,"Missile Weapon Ineptitude Other II"}, {475,"Missile Weapon Ineptitude Other III"}, {476,"Missile Weapon Ineptitude Other IV"}, {477,"Missile Weapon Ineptitude Other V"}, {478,"Missile Weapon Ineptitude Other VI"}, {479,"Missile Weapon Ineptitude Self I"}, {480,"Missile Weapon Ineptitude Self II"}, {481,"Missile Weapon Ineptitude Self III"}, {482,"Missile Weapon Ineptitude Self IV"}, {483,"Missile Weapon Ineptitude Self V"}, {484,"Missile Weapon Ineptitude Self VI"}, {485,"Missile Weapon Mastery Other I"}, {486,"Missile Weapon Mastery Other II"}, {487,"Missile Weapon Mastery Other III"}, {488,"Missile Weapon Mastery Other IV"}, {489,"Missile Weapon Mastery Other V"}, {490,"Missile Weapon Mastery Other VI"}, {491,"Missile Weapon Mastery Self I"}, {492,"Missile Weapon Mastery Self II"}, {493,"Missile Weapon Mastery Self III"}, {494,"Missile Weapon Mastery Self IV"}, {495,"Missile Weapon Mastery Self V"}, {496,"Missile Weapon Mastery Self VI"}, {497,"Missile Weapon Ineptitude Other I"}, {498,"Missile Weapon Ineptitude Other II"}, {499,"Missile Weapon Ineptitude Other III"}, {500,"Missile Weapon Ineptitude Other IV"}, {501,"Missile Weapon Ineptitude Other V"}, {502,"Missile Weapon Ineptitude Other VI"}, {503,"Missile Weapon Ineptitude Self I"}, {504,"Missile Weapon Ineptitude Self II"}, {505,"Missile Weapon Ineptitude Self III"}, {506,"Missile Weapon Ineptitude Self IV"}, {507,"Missile Weapon Ineptitude Self V"}, {508,"Missile Weapon Ineptitude Self VI"}, {509,"Acid Protection Other I"}, {510,"Acid Protection Other II"}, {511,"Acid Protection Other III"}, {512,"Acid Protection Other IV"}, {513,"Acid Protection Other V"}, {514,"Acid Protection Other VI"}, {515,"Acid Protection Self I"}, {516,"Acid Protection Self II"}, {517,"Acid Protection Self III"}, {518,"Acid Protection Self IV"}, {519,"Acid Protection Self V"}, {520,"Acid Protection Self VI"}, {521,"Acid Vulnerability Other I"}, {522,"Acid Vulnerability Other II"}, {523,"Acid Vulnerability Other III"}, {524,"Acid Vulnerability Other IV"}, {525,"Acid Vulnerability Other V"}, {526,"Acid Vulnerability Other VI"}, {527,"Acid Vulnerability Self I"}, {528,"Acid Vulnerability Self II"}, {529,"Acid Vulnerability Self III"}, {530,"Acid Vulnerability Self IV"}, {531,"Acid Vulnerability Self V"}, {532,"Acid Vulnerability Self VI"}, {533,"Missile Weapon Mastery Other I"}, {534,"Missile Weapon Mastery Other II"}, {535,"Missile Weapon Mastery Other III"}, {536,"Missile Weapon Mastery Other IV"}, {537,"Missile Weapon Mastery Other V"}, {538,"Missile Weapon Mastery Other VI"}, {539,"Missile Weapon Mastery Self I"}, {540,"Missile Weapon Mastery Self II"}, {541,"Missile Weapon Mastery Self III"}, {542,"Missile Weapon Mastery Self IV"}, {543,"Missile Weapon Mastery Self V"}, {544,"Missile Weapon Mastery Self VI"}, {545,"Missile Weapon Ineptitude Other I"}, {546,"Missile Weapon Ineptitude Other II"}, {547,"Missile Weapon Ineptitude Other III"}, {548,"Missile Weapon Ineptitude Other IV"}, {549,"Missile Weapon Ineptitude Other V"}, {550,"Missile Weapon Ineptitude Other VI"}, {551,"Missile Weapon Ineptitude Self I"}, {552,"Missile Weapon Ineptitude Self II"}, {553,"Missile Weapon Ineptitude Self III"}, {554,"Missile Weapon Ineptitude Self IV"}, {555,"Missile Weapon Ineptitude Self V"}, {556,"Missile Weapon Ineptitude Self VI"}, {557,"Creature Enchantment Mastery Self I"}, {558,"Creature Enchantment Mastery Self II"}, {559,"Creature Enchantment Mastery Self III"}, {560,"Creature Enchantment Mastery Self IV"}, {561,"Creature Enchantment Mastery Self V"}, {562,"Creature Enchantment Mastery Self VI"}, {563,"Creature Enchantment Mastery Other I"}, {564,"Creature Enchantment Mastery Other II"}, {565,"Creature Enchantment Mastery Other III"}, {566,"Creature Enchantment Mastery Other IV"}, {567,"Creature Enchantment Mastery Other V"}, {568,"Creature Enchantment Mastery Other VI"}, {569,"Creature Enchantment Ineptitude Other I"}, {570,"Creature Enchantment Ineptitude Other II"}, {571,"Creature Enchantment Ineptitude Other III"}, {572,"Creature Enchantment Ineptitude Other IV"}, {573,"Creature Enchantment Ineptitude Other V"}, {574,"Creature Enchantment Ineptitude Other VI"}, {575,"Creature Enchantment Ineptitude Self I"}, {576,"Creature Enchantment Ineptitude Self II"}, {577,"Creature Enchantment Ineptitude Self III"}, {578,"Creature Enchantment Ineptitude Self IV"}, {579,"Creature Enchantment Ineptitude Self V"}, {580,"Creature Enchantment Ineptitude Self VI"}, {581,"Item Enchantment Mastery Self I"}, {582,"Item Enchantment Mastery Self II"}, {583,"Item Enchantment Mastery Self III"}, {584,"Item Enchantment Mastery Self IV"}, {585,"Item Enchantment Mastery Self V"}, {586,"Item Enchantment Mastery Self VI"}, {587,"Item Enchantment Mastery Other I"}, {588,"Item Enchantment Mastery Other II"}, {589,"Item Enchantment Mastery Other III"}, {590,"Item Enchantment Mastery Other IV"}, {591,"Item Enchantment Mastery Other V"}, {592,"Item Enchantment Mastery Other VI"}, {593,"Item Enchantment Ineptitude Other I"}, {594,"Item Enchantment Ineptitude Other II"}, {595,"Item Enchantment Ineptitude Other III"}, {596,"Item Enchantment Ineptitude Other IV"}, {597,"Item Enchantment Ineptitude Other V"}, {598,"Item Enchantment Ineptitude Other VI"}, {599,"Item Enchantment Ineptitude Self I"}, {600,"Item Enchantment Ineptitude Self II"}, {601,"Item Enchantment Ineptitude Self III"}, {602,"Item Enchantment Ineptitude Self IV"}, {603,"Item Enchantment Ineptitude Self V"}, {604,"Item Enchantment Ineptitude Self VI"}, {605,"Life Magic Mastery Self I"}, {606,"Life Magic Mastery Self II"}, {607,"Life Magic Mastery Self III"}, {608,"Life Magic Mastery Self IV"}, {609,"Life Magic Mastery Self V"}, {610,"Life Magic Mastery Self VI"}, {611,"Life Magic Mastery Other I"}, {612,"Life Magic Mastery Other II"}, {613,"Life Magic Mastery Other III"}, {614,"Life Magic Mastery Other IV"}, {615,"Life Magic Mastery Other V"}, {616,"Life Magic Mastery Other VI"}, {617,"Life Magic Ineptitude Self I"}, {618,"Life Magic Ineptitude Self II"}, {619,"Life Magic Ineptitude Self III"}, {620,"Life Magic Ineptitude Self IV"}, {621,"Life Magic Ineptitude Self V"}, {622,"Life Magic Ineptitude Self VI"}, {623,"Life Magic Ineptitude Other I"}, {624,"Life Magic Ineptitude Other II"}, {625,"Life Magic Ineptitude Other III"}, {626,"Life Magic Ineptitude Other IV"}, {627,"Life Magic Ineptitude Other V"}, {628,"Life Magic Ineptitude Other VI"}, {629,"War Magic Mastery Self I"}, {630,"War Magic Mastery Self II"}, {631,"War Magic Mastery Self III"}, {632,"War Magic Mastery Self IV"}, {633,"War Magic Mastery Self V"}, {634,"War Magic Mastery Self VI"}, {635,"War Magic Mastery Other I"}, {636,"War Magic Mastery Other II"}, {637,"War Magic Mastery Other III"}, {638,"War Magic Mastery Other IV"}, {639,"War Magic Mastery Other V"}, {640,"War Magic Mastery Other VI"}, {641,"War Magic Ineptitude Self I"}, {642,"War Magic Ineptitude Self II"}, {643,"War Magic Ineptitude Self III"}, {644,"War Magic Ineptitude Self IV"}, {645,"War Magic Ineptitude Self V"}, {646,"War Magic Ineptitude Self VI"}, {647,"War Magic Ineptitude Other I"}, {648,"War Magic Ineptitude Other II"}, {649,"War Magic Ineptitude Other III"}, {650,"War Magic Ineptitude Other IV"}, {651,"War Magic Ineptitude Other V"}, {652,"War Magic Ineptitude Other VI"}, {653,"Mana Conversion Mastery Self I"}, {654,"Mana Conversion Mastery Self II"}, {655,"Mana Conversion Mastery Self III"}, {656,"Mana Conversion Mastery Self IV"}, {657,"Mana Conversion Mastery Self V"}, {658,"Mana Conversion Mastery Self VI"}, {659,"Mana Conversion Mastery Other I"}, {660,"Mana Conversion Mastery Other II"}, {661,"Mana Conversion Mastery Other III"}, {662,"Mana Conversion Mastery Other IV"}, {663,"Mana Conversion Mastery Other V"}, {664,"Mana Conversion Mastery Other VI"}, {665,"Mana Conversion Ineptitude Self I"}, {666,"Vitae"}, {667,"Mana Conversion Ineptitude Self II"}, {668,"Mana Conversion Ineptitude Self III"}, {669,"Mana Conversion Ineptitude Self IV"}, {670,"Mana Conversion Ineptitude Self V"}, {671,"Mana Conversion Ineptitude Self VI"}, {672,"Mana Conversion Ineptitude Other I"}, {673,"Mana Conversion Ineptitude Other II"}, {674,"Mana Conversion Ineptitude Other III"}, {675,"Mana Conversion Ineptitude Other IV"}, {676,"Mana Conversion Ineptitude Other V"}, {677,"Mana Conversion Ineptitude Other VI"}, {678,"Arcane Enlightenment Self I"}, {679,"Arcane Enlightenment Self II"}, {680,"Arcane Enlightenment Self III"}, {681,"Arcane Enlightenment Self IV"}, {682,"Arcane Enlightenment Self V"}, {683,"Arcane Enlightenment Self VI"}, {684,"Arcane Enlightenment Other I"}, {685,"Arcane Enlightenment Other II"}, {686,"Arcane Enlightenment Other III"}, {687,"Arcane Enlightenment Other IV"}, {688,"Arcane Enlightenment Other V"}, {689,"Arcane Enlightenment Other VI"}, {690,"Arcane Benightedness Self I"}, {691,"Arcane Benightedness Self II"}, {692,"Arcane Benightedness Self III"}, {693,"Arcane Benightedness Self IV"}, {694,"Arcane Benightedness Self V"}, {695,"Arcane Benightedness Self VI"}, {696,"Arcane Benightedness Other I"}, {697,"Arcane Benightedness Other II"}, {698,"Arcane Benightedness Other III"}, {699,"Arcane Benightedness Other IV"}, {700,"Arcane Benightedness Other V"}, {701,"Arcane Benightedness Other VI"}, {702,"Armor Tinkering Expertise Self I"}, {703,"Armor Tinkering Expertise Self II"}, {704,"Armor Tinkering Expertise Self III"}, {705,"Armor Tinkering Expertise Self IV"}, {706,"Armor Tinkering Expertise Self V"}, {707,"Armor Tinkering Expertise Self VI"}, {708,"Armor Tinkering Expertise Other I"}, {709,"Armor Tinkering Expertise Other II"}, {710,"Armor Tinkering Expertise Other III"}, {711,"Armor Tinkering Expertise Other IV"}, {712,"Armor Tinkering Expertise Other V"}, {713,"Armor Tinkering Expertise Other VI"}, {714,"Armor Tinkering Ignorance Self I"}, {715,"Armor Tinkering Ignorance Self II"}, {716,"Armor Tinkering Ignorance Self III"}, {717,"Armor Tinkering Ignorance Self IV"}, {718,"Armor Tinkering Ignorance Self V"}, {719,"Armor Tinkering Ignorance Self VI"}, {720,"Armor Tinkering Ignorance Other I"}, {721,"Armor Tinkering Ignorance Other II"}, {722,"Armor Tinkering Ignorance Other III"}, {723,"Armor Tinkering Ignorance Other IV"}, {724,"Armor Tinkering Ignorance Other V"}, {725,"Armor Tinkering Ignorance Other VI"}, {726,"Item Tinkering Expertise Self I"}, {727,"Item Tinkering Expertise Self II"}, {728,"Item Tinkering Expertise Self III"}, {729,"Item Tinkering Expertise Self IV"}, {730,"Item Tinkering Expertise Self V"}, {731,"Item Tinkering Expertise Self VI"}, {732,"Item Tinkering Expertise Other I"}, {733,"Item Tinkering Expertise Other II"}, {734,"Item Tinkering Expertise Other III"}, {735,"Item Tinkering Expertise Other IV"}, {736,"Item Tinkering Expertise Other V"}, {737,"Item Tinkering Expertise Other VI"}, {738,"Item Tinkering Ignorance Self I"}, {739,"Item Tinkering Ignorance Self II"}, {740,"Item Tinkering Ignorance Self III"}, {741,"Item Tinkering Ignorance Self IV"}, {742,"Item Tinkering Ignorance Self V"}, {743,"Item Tinkering Ignorance Self VI"}, {744,"Item Tinkering Ignorance Other I"}, {745,"Item Tinkering Ignorance Other II"}, {746,"Item Tinkering Ignorance Other III"}, {747,"Item Tinkering Ignorance Other IV"}, {748,"Item Tinkering Ignorance Other V"}, {749,"Item Tinkering Ignorance Other VI"}, {750,"Magic Item Tinkering Expertise Self I"}, {751,"Magic Item Tinkering Expertise Self II"}, {752,"Magic Item Tinkering Expertise Self III"}, {753,"Magic Item Tinkering Expertise Self IV"}, {754,"Magic Item Tinkering Expertise Self V"}, {755,"Magic Item Tinkering Expertise Self VI"}, {756,"Magic Item Tinkering Expertise Other I"}, {757,"Magic Item Tinkering Expertise Other II"}, {758,"Magic Item Tinkering Expertise Other III"}, {759,"Magic Item Tinkering Expertise Other IV"}, {760,"Magic Item Tinkering Expertise Other V"}, {761,"Magic Item Tinkering Expertise Other VI"}, {762,"Magic Item Tinkering Ignorance Self I"}, {763,"Magic Item Tinkering Ignorance Self II"}, {764,"Magic Item Tinkering Ignorance Self III"}, {765,"Magic Item Tinkering Ignorance Self IV"}, {766,"Magic Item Tinkering Ignorance Self V"}, {767,"Magic Item Tinkering Ignorance Self VI"}, {768,"Magic Item Tinkering Ignorance Other I"}, {769,"Magic Item Tinkering Ignorance Other II"}, {770,"Magic Item Tinkering Ignorance Other III"}, {771,"Magic Item Tinkering Ignorance Other IV"}, {772,"Magic Item Tinkering Ignorance Other V"}, {773,"Magic Item Tinkering Ignorance Other VI"}, {774,"Weapon Tinkering Expertise Self I"}, {775,"Weapon Tinkering Expertise Self II"}, {776,"Weapon Tinkering Expertise Self III"}, {777,"Weapon Tinkering Expertise Self IV"}, {778,"Weapon Tinkering Expertise Self V"}, {779,"Weapon Tinkering Expertise Self VI"}, {780,"Weapon Tinkering Expertise Other I"}, {781,"Weapon Tinkering Expertise Other II"}, {782,"Weapon Tinkering Expertise Other III"}, {783,"Weapon Tinkering Expertise Other IV"}, {784,"Weapon Tinkering Expertise Other V"}, {785,"Weapon Tinkering Expertise Other VI"}, {786,"Weapon Tinkering Ignorance Self I"}, {787,"Weapon Tinkering Ignorance Self II"}, {788,"Weapon Tinkering Ignorance Self III"}, {789,"Weapon Tinkering Ignorance Self IV"}, {790,"Weapon Tinkering Ignorance Self V"}, {791,"Weapon Tinkering Ignorance Self VI"}, {792,"Weapon Tinkering Ignorance Other I"}, {793,"Weapon Tinkering Ignorance Other II"}, {794,"Weapon Tinkering Ignorance Other III"}, {795,"Weapon Tinkering Ignorance Other IV"}, {796,"Weapon Tinkering Ignorance Other V"}, {797,"Weapon Tinkering Ignorance Other VI"}, {798,"Monster Attunement Self I"}, {799,"Monster Attunement Self II"}, {800,"Monster Attunement Self III"}, {801,"Monster Attunement Self IV"}, {802,"Monster Attunement Self V"}, {803,"Monster Attunement Self VI"}, {804,"Monster Attunement Other I"}, {805,"Monster Attunement Other II"}, {806,"Monster Attunement Other III"}, {807,"Monster Attunement Other IV"}, {808,"Monster Attunement Other V"}, {809,"Monster Attunement Other VI"}, {810,"Fire Protection Other II"}, {811,"Monster Unfamiliarity Self I"}, {812,"Monster Unfamiliarity Self II"}, {813,"Monster Unfamiliarity Self III"}, {814,"Monster Unfamiliarity Self IV"}, {815,"Monster Unfamiliarity Self V"}, {816,"Monster Unfamiliarity Self VI"}, {817,"Monster Unfamiliarity Other I"}, {818,"Monster Unfamiliarity Other II"}, {819,"Monster Unfamiliarity Other III"}, {820,"Monster Unfamiliarity Other IV"}, {821,"Monster Unfamiliarity Other V"}, {822,"Monster Unfamiliarity Other VI"}, {824,"Person Attunement Self I"}, {825,"Person Attunement Self II"}, {826,"Person Attunement Self III"}, {827,"Person Attunement Self IV"}, {828,"Person Attunement Self V"}, {829,"Person Attunement Self VI"}, {830,"Person Attunement Other I"}, {831,"Person Attunement Other II"}, {832,"Person Attunement Other III"}, {833,"Person Attunement Other IV"}, {834,"Person Attunement Other V"}, {835,"Person Attunement Other VI"}, {836,"Fire Protection Other III"}, {837,"Person Unfamiliarity Self I"}, {838,"Person Unfamiliarity Self II"}, {839,"Person Unfamiliarity Self III"}, {840,"Person Unfamiliarity Self IV"}, {841,"Person Unfamiliarity Self V"}, {842,"Person Unfamiliarity Self VI"}, {843,"Person Unfamiliarity Other I"}, {844,"Person Unfamiliarity Other II"}, {845,"Person Unfamiliarity Other III"}, {846,"Person Unfamiliarity Other IV"}, {847,"Person Unfamiliarity Other V"}, {848,"Person Unfamiliarity Other VI"}, {849,"Fire Protection Other IV"}, {850,"Deception Mastery Self I"}, {851,"Deception Mastery Self II"}, {852,"Deception Mastery Self III"}, {853,"Deception Mastery Self IV"}, {854,"Deception Mastery Self V"}, {855,"Deception Mastery Self VI"}, {856,"Deception Mastery Other I"}, {857,"Deception Mastery Other II"}, {858,"Deception Mastery Other III"}, {859,"Deception Mastery Other IV"}, {860,"Deception Mastery Other V"}, {861,"Deception Mastery Other VI"}, {862,"Deception Ineptitude Self I"}, {863,"Deception Ineptitude Self II"}, {864,"Deception Ineptitude Self III"}, {865,"Deception Ineptitude Self IV"}, {866,"Deception Ineptitude Self V"}, {867,"Deception Ineptitude Self VI"}, {868,"Deception Ineptitude Other I"}, {869,"Deception Ineptitude Other II"}, {870,"Deception Ineptitude Other III"}, {871,"Deception Ineptitude Other IV"}, {872,"Deception Ineptitude Other V"}, {873,"Deception Ineptitude Other VI"}, {874,"Healing Mastery Self I"}, {875,"Healing Mastery Self II"}, {876,"Healing Mastery Self III"}, {877,"Healing Mastery Self IV"}, {878,"Healing Mastery Self V"}, {879,"Healing Mastery Self VI"}, {880,"Healing Mastery Other I"}, {881,"Healing Mastery Other II"}, {882,"Healing Mastery Other III"}, {883,"Healing Mastery Other IV"}, {884,"Healing Mastery Other V"}, {885,"Healing Mastery Other VI"}, {886,"Healing Ineptitude Self I"}, {887,"Healing Ineptitude Self II"}, {888,"Healing Ineptitude Self III"}, {889,"Healing Ineptitude Self IV"}, {890,"Healing Ineptitude Self V"}, {891,"Healing Ineptitude Self VI"}, {892,"Healing Ineptitude Other I"}, {893,"Healing Ineptitude Other II"}, {894,"Healing Ineptitude Other III"}, {895,"Healing Ineptitude Other IV"}, {896,"Healing Ineptitude Other V"}, {897,"Healing Ineptitude Other VI"}, {898,"Leadership Mastery Self I"}, {899,"Leadership Mastery Self II"}, {900,"Leadership Mastery Self III"}, {901,"Leadership Mastery Self IV"}, {902,"Leadership Mastery Self V"}, {903,"Leadership Mastery Self VI"}, {904,"Leadership Mastery Other I"}, {905,"Leadership Mastery Other II"}, {906,"Leadership Mastery Other III"}, {907,"Leadership Mastery Other IV"}, {908,"Leadership Mastery Other V"}, {909,"Leadership Mastery Other VI"}, {910,"Leadership Ineptitude Self I"}, {911,"Leadership Ineptitude Self II"}, {912,"Leadership Ineptitude Self III"}, {913,"Leadership Ineptitude Self IV"}, {914,"Leadership Ineptitude Self V"}, {915,"Leadership Ineptitude Self VI"}, {916,"Leadership Ineptitude Other I"}, {917,"Leadership Ineptitude Other II"}, {918,"Leadership Ineptitude Other III"}, {919,"Leadership Ineptitude Other IV"}, {920,"Leadership Ineptitude Other V"}, {921,"Leadership Ineptitude Other VI"}, {922,"Lockpick Mastery Self I"}, {923,"Lockpick Mastery Self II"}, {924,"Lockpick Mastery Self III"}, {925,"Lockpick Mastery Self IV"}, {926,"Lockpick Mastery Self V"}, {927,"Lockpick Mastery Self VI"}, {928,"Lockpick Mastery Other I"}, {929,"Lockpick Mastery Other II"}, {930,"Lockpick Mastery Other III"}, {931,"Lockpick Mastery Other IV"}, {932,"Lockpick Mastery Other V"}, {933,"Lockpick Mastery Other VI"}, {934,"Lockpick Ineptitude Self I"}, {935,"Lockpick Ineptitude Self II"}, {936,"Lockpick Ineptitude Self III"}, {937,"Lockpick Ineptitude Self IV"}, {938,"Lockpick Ineptitude Self V"}, {939,"Lockpick Ineptitude Self VI"}, {940,"Lockpick Ineptitude Other I"}, {941,"Lockpick Ineptitude Other II"}, {942,"Lockpick Ineptitude Other III"}, {943,"Lockpick Ineptitude Other IV"}, {944,"Lockpick Ineptitude Other V"}, {945,"Lockpick Ineptitude Other VI"}, {946,"Fealty Self I"}, {947,"Fealty Self II"}, {948,"Fealty Self III"}, {949,"Fealty Self IV"}, {950,"Fealty Self V"}, {951,"Fealty Self VI"}, {952,"Fealty Other I"}, {953,"Fealty Other II"}, {954,"Fealty Other III"}, {955,"Fealty Other IV"}, {956,"Fealty Other V"}, {957,"Fealty Other VI"}, {958,"Faithlessness Self I"}, {959,"Faithlessness Self II"}, {960,"Faithlessness Self III"}, {961,"Faithlessness Self IV"}, {962,"Faithlessness Self V"}, {963,"Faithlessness Self VI"}, {964,"Faithlessness Other I"}, {965,"Faithlessness Other II"}, {966,"Faithlessness Other III"}, {967,"Faithlessness Other IV"}, {968,"Faithlessness Other V"}, {969,"Faithlessness Other VI"}, {970,"Jumping Mastery Self I"}, {971,"Jumping Mastery Self II"}, {972,"Jumping Mastery Self III"}, {973,"Jumping Mastery Self IV"}, {974,"Jumping Mastery Self V"}, {975,"Jumping Mastery Self VI"}, {976,"Jumping Mastery Other I"}, {977,"Jumping Mastery Other II"}, {978,"Jumping Mastery Other III"}, {979,"Jumping Mastery Other IV"}, {980,"Jumping Mastery Other V"}, {981,"Jumping Mastery Other VI"}, {982,"Sprint Self I"}, {983,"Sprint Self II"}, {984,"Sprint Self III"}, {985,"Sprint Self IV"}, {986,"Sprint Self V"}, {987,"Sprint Self VI"}, {988,"Sprint Other I"}, {989,"Sprint Other II"}, {990,"Sprint Other III"}, {991,"Sprint Other IV"}, {992,"Sprint Other V"}, {993,"Sprint Other VI"}, {994,"Leaden Feet Self I"}, {995,"Leaden Feet Self II"}, {996,"Leaden Feet Self III"}, {997,"Leaden Feet Self IV"}, {998,"Leaden Feet Self V"}, {999,"Leaden Feet Self VI"}, {1000,"Leaden Feet Other I"}, {1001,"Leaden Feet Other II"}, {1002,"Leaden Feet Other III"}, {1003,"Leaden Feet Other IV"}, {1004,"Leaden Feet Other V"}, {1005,"Leaden Feet Other VI"}, {1006,"Jumping Ineptitude Self I"}, {1007,"Jumping Ineptitude Self II"}, {1008,"Jumping Ineptitude Self III"}, {1009,"Jumping Ineptitude Self IV"}, {1010,"Jumping Ineptitude Self V"}, {1011,"Jumping Ineptitude Self VI"}, {1012,"Jumping Ineptitude Other I"}, {1013,"Jumping Ineptitude Other II"}, {1014,"Jumping Ineptitude Other III"}, {1015,"Jumping Ineptitude Other IV"}, {1016,"Jumping Ineptitude Other V"}, {1017,"Jumping Ineptitude Other VI"}, {1018,"Bludgeoning Protection Self I"}, {1019,"Bludgeoning Protection Self II"}, {1020,"Bludgeoning Protection Self III"}, {1021,"Bludgeoning Protection Self IV"}, {1022,"Bludgeoning Protection Self V"}, {1023,"Bludgeoning Protection Self VI"}, {1024,"Bludgeoning Protection Other I"}, {1025,"Bludgeoning Protection Other II"}, {1026,"Bludgeoning Protection Other III"}, {1027,"Bludgeoning Protection Other IV"}, {1028,"Bludgeoning Protection Other V"}, {1029,"Bludgeoning Protection Other VI"}, {1030,"Cold Protection Self I"}, {1031,"Cold Protection Self II"}, {1032,"Cold Protection Self III"}, {1033,"Cold Protection Self IV"}, {1034,"Cold Protection Self V"}, {1035,"Cold Protection Self VI"}, {1036,"Cold Protection Other I"}, {1037,"Cold Protection Other II"}, {1038,"Cold Protection Other III"}, {1039,"Cold Protection Other IV"}, {1040,"Cold Protection Other V"}, {1041,"Cold Protection Other VI"}, {1042,"Bludgeoning Vulnerability Self I"}, {1043,"Bludgeoning Vulnerability Self II"}, {1044,"Bludgeoning Vulnerability Self III"}, {1045,"Bludgeoning Vulnerability Self IV"}, {1046,"Bludgeoning Vulnerability Self V"}, {1047,"Bludgeoning Vulnerability Self VI"}, {1048,"Bludgeoning Vulnerability Other I"}, {1049,"Bludgeoning Vulnerability Other II"}, {1050,"Bludgeoning Vulnerability Other III"}, {1051,"Bludgeoning Vulnerability Other IV"}, {1052,"Bludgeoning Vulnerability Other V"}, {1053,"Bludgeoning Vulnerability Other VI"}, {1054,"Cold Vulnerability Self I"}, {1055,"Cold Vulnerability Self II"}, {1056,"Cold Vulnerability Self III"}, {1057,"Cold Vulnerability Self IV"}, {1058,"Cold Vulnerability Self V"}, {1059,"Cold Vulnerability Self VI"}, {1060,"Cold Vulnerability Other I"}, {1061,"Cold Vulnerability Other II"}, {1062,"Cold Vulnerability Other III"}, {1063,"Cold Vulnerability Other IV"}, {1064,"Cold Vulnerability Other V"}, {1065,"Cold Vulnerability Other VI"}, {1066,"Lightning Protection Self I"}, {1067,"Lightning Protection Self II"}, {1068,"Lightning Protection Self III"}, {1069,"Lightning Protection Self IV"}, {1070,"Lightning Protection Self V"}, {1071,"Lightning Protection Self VI"}, {1072,"Lightning Protection Other I"}, {1073,"Lightning Protection Other II"}, {1074,"Lightning Protection Other III"}, {1075,"Lightning Protection Other IV"}, {1076,"Lightning Protection Other V"}, {1077,"Lightning Protection Other VI"}, {1078,"Lightning Vulnerability Self I"}, {1079,"Lightning Vulnerability Self II"}, {1080,"Lightning Vulnerability Self III"}, {1081,"Lightning Vulnerability Self IV"}, {1082,"Lightning Vulnerability Self V"}, {1083,"Lightning Vulnerability Self VI"}, {1084,"Lightning Vulnerability Other I"}, {1085,"Lightning Vulnerability Other II"}, {1086,"Lightning Vulnerability Other III"}, {1087,"Lightning Vulnerability Other IV"}, {1088,"Lightning Vulnerability Other V"}, {1089,"Lightning Vulnerability Other VI"}, {1090,"Fire Protection Self II"}, {1091,"Fire Protection Self III"}, {1092,"Fire Protection Self IV"}, {1093,"Fire Protection Self V"}, {1094,"Fire Protection Self VI"}, {1095,"Fire Protection Other V"}, {1096,"Fire Protection Other VI"}, {1097,"Flaming Missile"}, {1098,"Fire Vulnerability Self II"}, {1099,"Fire Vulnerability Self III"}, {1100,"Fire Vulnerability Self IV"}, {1101,"Fire Vulnerability Self V"}, {1102,"Fire Vulnerability Self VI"}, {1104,"Fire Vulnerability Other II"}, {1105,"Fire Vulnerability Other III"}, {1106,"Fire Vulnerability Other IV"}, {1107,"Fire Vulnerability Other V"}, {1108,"Fire Vulnerability Other VI"}, {1109,"Blade Protection Self I"}, {1110,"Blade Protection Self II"}, {1111,"Blade Protection Self III"}, {1112,"Blade Protection Self IV"}, {1113,"Blade Protection Self V"}, {1114,"Blade Protection Self VI"}, {1115,"Blade Protection Other I"}, {1116,"Blade Protection Other II"}, {1117,"Blade Protection Other III"}, {1118,"Blade Protection Other IV"}, {1119,"Blade Protection Other V"}, {1120,"Blade Protection Other VI"}, {1121,"Blade Vulnerability Self I"}, {1122,"Blade Vulnerability Self II"}, {1123,"Blade Vulnerability Self III"}, {1124,"Blade Vulnerability Self IV"}, {1125,"Blade Vulnerability Self V"}, {1126,"Blade Vulnerability Self VI"}, {1127,"Blade Vulnerability Other I"}, {1128,"Blade Vulnerability Other II"}, {1129,"Blade Vulnerability Other III"}, {1130,"Blade Vulnerability Other IV"}, {1131,"Blade Vulnerability Other V"}, {1132,"Blade Vulnerability Other VI"}, {1133,"Piercing Protection Self I"}, {1134,"Piercing Protection Self II"}, {1135,"Piercing Protection Self III"}, {1136,"Piercing Protection Self IV"}, {1137,"Piercing Protection Self V"}, {1138,"Piercing Protection Self VI"}, {1139,"Piercing Protection Other I"}, {1140,"Piercing Protection Other II"}, {1141,"Piercing Protection Other III"}, {1142,"Piercing Protection Other IV"}, {1143,"Piercing Protection Other V"}, {1144,"Piercing Protection Other VI"}, {1145,"Piercing Vulnerability Self I"}, {1146,"Piercing Vulnerability Self II"}, {1147,"Piercing Vulnerability Self III"}, {1148,"Piercing Vulnerability Self IV"}, {1149,"Piercing Vulnerability Self V"}, {1150,"Piercing Vulnerability Self VI"}, {1151,"Piercing Vulnerability Other I"}, {1152,"Piercing Vulnerability Other II"}, {1153,"Piercing Vulnerability Other III"}, {1154,"Piercing Vulnerability Other IV"}, {1155,"Piercing Vulnerability Other V"}, {1156,"Piercing Vulnerability Other VI"}, {1157,"Heal Self II"}, {1158,"Heal Self III"}, {1159,"Heal Self IV"}, {1160,"Heal Self V"}, {1161,"Heal Self VI"}, {1162,"Heal Other II"}, {1163,"Heal Other III"}, {1164,"Heal Other IV"}, {1165,"Heal Other V"}, {1166,"Heal Other VI"}, {1167,"Harm Self II"}, {1168,"Harm Self III"}, {1169,"Harm Self IV"}, {1170,"Harm Self V"}, {1171,"Harm Self VI"}, {1172,"Harm Other II"}, {1173,"Harm Other III"}, {1174,"Harm Other IV"}, {1175,"Harm Other V"}, {1176,"Harm Other VI"}, {1177,"Revitalize Self I"}, {1178,"Revitalize Self II"}, {1179,"Revitalize Self III"}, {1180,"Revitalize Self IV"}, {1181,"Revitalize Self V"}, {1182,"Revitalize Self VI"}, {1183,"Revitalize Other I"}, {1184,"Revitalize Other II"}, {1185,"Revitalize Other III"}, {1186,"Revitalize Other IV"}, {1187,"Revitalize Other V"}, {1188,"Revitalize Other VI"}, {1189,"Enfeeble Self I"}, {1190,"Enfeeble Self II"}, {1191,"Enfeeble Self III"}, {1192,"Enfeeble Self IV"}, {1193,"Enfeeble Self V"}, {1194,"Enfeeble Self VI"}, {1195,"Enfeeble Other I"}, {1196,"Enfeeble Other II"}, {1197,"Enfeeble Other III"}, {1198,"Enfeeble Other IV"}, {1199,"Enfeeble Other V"}, {1200,"Enfeeble Other VI"}, {1201,"Mana Boost Self I"}, {1202,"Mana Boost Self II"}, {1203,"Mana Boost Self III"}, {1204,"Mana Boost Self IV"}, {1205,"Mana Boost Self V"}, {1206,"Mana Boost Self VI"}, {1207,"Mana Boost Other I"}, {1208,"Mana Boost Other II"}, {1209,"Mana Boost Other III"}, {1210,"Mana Boost Other IV"}, {1211,"Mana Boost Other V"}, {1212,"Mana Boost Other VI"}, {1213,"Mana Drain Self I"}, {1214,"Mana Drain Self II"}, {1215,"Mana Drain Self III"}, {1216,"Mana Drain Self IV"}, {1217,"Mana Drain Self V"}, {1218,"Mana Drain Self VI"}, {1219,"Mana Drain Other I"}, {1220,"Mana Drain Other II"}, {1221,"Mana Drain Other III"}, {1222,"Mana Drain Other IV"}, {1223,"Mana Drain Other V"}, {1224,"Mana Drain Other VI"}, {1225,"Infuse Health Other I"}, {1226,"Infuse Health Other II"}, {1227,"Infuse Health Other III"}, {1228,"Infuse Health Other IV"}, {1229,"Infuse Health Other V"}, {1230,"Infuse Health Other VI"}, {1237,"Drain Health Other I"}, {1238,"Drain Health Other II"}, {1239,"Drain Health Other III"}, {1240,"Drain Health Other IV"}, {1241,"Drain Health Other V"}, {1242,"Drain Health Other VI"}, {1243,"Infuse Stamina Other I"}, {1244,"Infuse Stamina Other II"}, {1245,"Infuse Stamina Other III"}, {1246,"Infuse Stamina Other IV"}, {1247,"Infuse Stamina Other V"}, {1248,"Infuse Stamina Other VI"}, {1249,"Drain Stamina Other I"}, {1250,"Drain Stamina Other II"}, {1251,"Drain Stamina Other III"}, {1252,"Drain Stamina Other IV"}, {1253,"Drain Stamina Other V"}, {1254,"Drain Stamina Other VI"}, {1255,"Infuse Mana Other II"}, {1256,"Infuse Mana Other III"}, {1257,"Infuse Mana Other IV"}, {1258,"Infuse Mana Other V"}, {1259,"Infuse Mana Other VI"}, {1260,"Drain Mana Other I"}, {1261,"Drain Mana Other II"}, {1262,"Drain Mana Other III"}, {1263,"Drain Mana Other IV"}, {1264,"Drain Mana Other V"}, {1265,"Drain Mana Other VI"}, {1266,"Health to Stamina Other I"}, {1267,"Health to Stamina Other II"}, {1268,"Health to Stamina Other III"}, {1269,"Health to Stamina Other IV"}, {1270,"Health to Stamina Other V"}, {1271,"Health to Stamina Other VI"}, {1272,"Health to Stamina Self I"}, {1273,"Health to Stamina Self II"}, {1274,"Health to Stamina Self III"}, {1275,"Health to Stamina Self IV"}, {1276,"Health to Stamina Self V"}, {1277,"Health to Stamina Self VI"}, {1278,"Health to Mana Self I"}, {1279,"Health to Mana Self II"}, {1280,"Health to Mana Self III"}, {1281,"Health to Mana Other IV"}, {1282,"Health to Mana Other V"}, {1283,"Health to Mana Other VI"}, {1284,"Mana to Health Other I"}, {1285,"Mana to Health Other II"}, {1286,"Mana to Health Other III"}, {1287,"Mana to Health Other IV"}, {1288,"Mana to Health Other V"}, {1289,"Mana to Health Other VI"}, {1290,"Mana to Health Self I"}, {1291,"Mana to Health Self II"}, {1292,"Mana to Health Self III"}, {1293,"Mana to Health Self IV"}, {1294,"Mana to Health Self V"}, {1295,"Mana to Health Self VI"}, {1296,"Mana to Stamina Self I"}, {1297,"Mana to Stamina Self II"}, {1298,"Mana to Stamina Self III"}, {1299,"Mana to Stamina Self IV"}, {1300,"Mana to Stamina Self V"}, {1301,"Mana to Stamina Self VI"}, {1302,"Mana to Stamina Other I"}, {1303,"Mana to Stamina Other II"}, {1304,"Mana to Stamina Other III"}, {1305,"Mana to Stamina Other IV"}, {1306,"Mana to Stamina Other V"}, {1307,"Mana to Stamina Other VI"}, {1308,"Armor Self II"}, {1309,"Armor Self III"}, {1310,"Armor Self IV"}, {1311,"Armor Self V"}, {1312,"Armor Self VI"}, {1313,"Armor Other II"}, {1314,"Armor Other III"}, {1315,"Armor Other IV"}, {1316,"Armor Other V"}, {1317,"Armor Other VI"}, {1318,"Imperil Self II"}, {1319,"Imperil Self III"}, {1320,"Imperil Self IV"}, {1321,"Imperil Self V"}, {1322,"Imperil Self VI"}, {1323,"Imperil Other II"}, {1324,"Imperil Other III"}, {1325,"Imperil Other IV"}, {1326,"Imperil Other V"}, {1327,"Imperil Other VI"}, {1328,"Strength Self II"}, {1329,"Strength Self III"}, {1330,"Strength Self IV"}, {1331,"Strength Self V"}, {1332,"Strength Self VI"}, {1333,"Strength Other II"}, {1334,"Strength Other III"}, {1335,"Strength Other IV"}, {1336,"Strength Other V"}, {1337,"Strength Other VI"}, {1339,"Weakness Other II"}, {1340,"Weakness Other III"}, {1341,"Weakness Other IV"}, {1342,"Weakness Other V"}, {1343,"Weakness Other VI"}, {1344,"Weakness Self II"}, {1345,"Weakness Self III"}, {1346,"Weakness Self IV"}, {1347,"Weakness Self V"}, {1348,"Weakness Self VI"}, {1349,"Endurance Self I"}, {1350,"Endurance Self II"}, {1351,"Endurance Self III"}, {1352,"Endurance Self IV"}, {1353,"Endurance Self V"}, {1354,"Endurance Self VI"}, {1355,"Endurance Other I"}, {1356,"Endurance Other II"}, {1357,"Endurance Other III"}, {1358,"Endurance Other IV"}, {1359,"Endurance Other V"}, {1360,"Endurance Other VI"}, {1361,"Frailty Self I"}, {1362,"Frailty Self II"}, {1363,"Frailty Self III"}, {1364,"Frailty Self IV"}, {1365,"Frailty Self V"}, {1366,"Frailty Self VI"}, {1367,"Frailty Other I"}, {1368,"Frailty Other II"}, {1369,"Frailty Other III"}, {1370,"Frailty Other IV"}, {1371,"Frailty Other V"}, {1372,"Frailty Other VI"}, {1373,"Coordination Self I"}, {1374,"Coordination Self II"}, {1375,"Coordination Self III"}, {1376,"Coordination Self IV"}, {1377,"Coordination Self V"}, {1378,"Coordination Self VI"}, {1379,"Coordination Other I"}, {1380,"Coordination Other II"}, {1381,"Coordination Other III"}, {1382,"Coordination Other IV"}, {1383,"Coordination Other V"}, {1384,"Coordination Other VI"}, {1385,"Clumsiness Self I"}, {1386,"Clumsiness Self II"}, {1387,"Clumsiness Self III"}, {1388,"Clumsiness Self IV"}, {1389,"Clumsiness Self V"}, {1390,"Clumsiness Self VI"}, {1391,"Clumsiness Other I"}, {1392,"Clumsiness Other II"}, {1393,"Clumsiness Other III"}, {1394,"Clumsiness Other IV"}, {1395,"Clumsiness Other V"}, {1396,"Clumsiness Other VI"}, {1397,"Quickness Self I"}, {1398,"Quickness Self II"}, {1399,"Quickness Self III"}, {1400,"Quickness Self IV"}, {1401,"Quickness Self V"}, {1402,"Quickness Self VI"}, {1403,"Quickness Other I"}, {1404,"Quickness Other II"}, {1405,"Quickness Other III"}, {1406,"Quickness Other IV"}, {1407,"Quickness Other V"}, {1408,"Quickness Other VI"}, {1409,"Slowness Self I"}, {1410,"Slowness Self II"}, {1411,"Slowness Self III"}, {1412,"Slowness Self IV"}, {1413,"Slowness Self V"}, {1414,"Slowness Self VI"}, {1415,"Slowness Other I"}, {1416,"Slowness Other II"}, {1417,"Slowness Other III"}, {1418,"Slowness Other IV"}, {1419,"Slowness Other V"}, {1420,"Slowness Other VI"}, {1421,"Focus Self I"}, {1422,"Focus Self II"}, {1423,"Focus Self III"}, {1424,"Focus Self IV"}, {1425,"Focus Self V"}, {1426,"Focus Self VI"}, {1427,"Focus Other I"}, {1428,"Focus Other II"}, {1429,"Focus Other III"}, {1430,"Focus Other IV"}, {1431,"Focus Other V"}, {1432,"Focus Other VI"}, {1433,"Bafflement Self I"}, {1434,"Bafflement Self II"}, {1435,"Bafflement Self III"}, {1436,"Bafflement Self IV"}, {1437,"Bafflement Self V"}, {1438,"Bafflement Self VI"}, {1439,"Bafflement Other I"}, {1440,"Bafflement Other II"}, {1441,"Bafflement Other III"}, {1442,"Bafflement Other IV"}, {1443,"Bafflement Other V"}, {1444,"Bafflement Other VI"}, {1445,"Willpower Self I"}, {1446,"Willpower Self II"}, {1447,"Willpower Self III"}, {1448,"Willpower Self IV"}, {1449,"Willpower Self V"}, {1450,"Willpower Self VI"}, {1451,"Willpower Other I"}, {1452,"Willpower Other II"}, {1453,"Willpower Other III"}, {1454,"Willpower Other IV"}, {1455,"Willpower Other V"}, {1456,"Willpower Other VI"}, {1457,"Feeblemind Self I"}, {1458,"Feeblemind Self II"}, {1459,"Feeblemind Self III"}, {1460,"Feeblemind Self IV"}, {1461,"Feeblemind Self V"}, {1462,"Feeblemind Self VI"}, {1463,"Feeblemind Other I"}, {1464,"Feeblemind Other II"}, {1465,"Feeblemind Other III"}, {1466,"Feeblemind Other IV"}, {1467,"Feeblemind Other V"}, {1468,"Feeblemind Other VI"}, {1469,"Hermetic Void I"}, {1470,"Hermetic Void II"}, {1471,"Hermetic Void III"}, {1472,"Hermetic Void IV"}, {1473,"Hermetic Void V"}, {1474,"Hermetic Void VI"}, {1475,"Aura of Hermetic Link Self I"}, {1476,"Aura of Hermetic Link Self II"}, {1477,"Aura of Hermetic Link Self III"}, {1478,"Aura of Hermetic Link Self IV"}, {1479,"Aura of Hermetic Link Self V"}, {1480,"Aura of Hermetic Link Self VI"}, {1481,"Flaming Missile Volley"}, {1482,"Impenetrability II"}, {1483,"Impenetrability III"}, {1484,"Impenetrability IV"}, {1485,"Impenetrability V"}, {1486,"Impenetrability VI"}, {1487,"Brittlemail I"}, {1488,"Brittlemail II"}, {1489,"Brittlemail III"}, {1490,"Brittlemail IV"}, {1491,"Brittlemail V"}, {1492,"Brittlemail VI"}, {1493,"Acid Bane I"}, {1494,"Acid Bane II"}, {1495,"Acid Bane III"}, {1496,"Acid Bane IV"}, {1497,"Acid Bane V"}, {1498,"Acid Bane VI"}, {1499,"Acid Lure I"}, {1500,"Acid Lure II"}, {1501,"Acid Lure III"}, {1502,"Acid Lure IV"}, {1503,"Acid Lure V"}, {1504,"Acid Lure VI"}, {1505,"Bludgeon Lure I"}, {1506,"Bludgeon Lure II"}, {1507,"Bludgeon Lure III"}, {1508,"Bludgeon Lure IV"}, {1509,"Bludgeon Lure V"}, {1510,"Bludgeon Lure VI"}, {1511,"Bludgeon Bane I"}, {1512,"Bludgeon Bane II"}, {1513,"Bludgeon Bane III"}, {1514,"Bludgeon Bane IV"}, {1515,"Bludgeon Bane V"}, {1516,"Bludgeon Bane VI"}, {1517,"Frost Lure I"}, {1518,"Frost Lure II"}, {1519,"Frost Lure III"}, {1520,"Frost Lure IV"}, {1521,"Frost Lure V"}, {1522,"Frost Lure VI"}, {1523,"Frost Bane I"}, {1524,"Frost Bane II"}, {1525,"Frost Bane III"}, {1526,"Frost Bane IV"}, {1527,"Frost Bane V"}, {1528,"Frost Bane VI"}, {1529,"Lightning Lure I"}, {1530,"Lightning Lure II"}, {1531,"Lightning Lure III"}, {1532,"Lightning Lure IV"}, {1533,"Lightning Lure V"}, {1534,"Lightning Lure VI"}, {1535,"Lightning Bane I"}, {1536,"Lightning Bane II"}, {1537,"Lightning Bane III"}, {1538,"Lightning Bane IV"}, {1539,"Lightning Bane V"}, {1540,"Lightning Bane VI"}, {1541,"Flame Lure I"}, {1542,"Flame Lure II"}, {1543,"Flame Lure III"}, {1544,"Flame Lure IV"}, {1545,"Flame Lure V"}, {1546,"Flame Lure VI"}, {1547,"Flame Bane I"}, {1548,"Flame Bane II"}, {1549,"Flame Bane III"}, {1550,"Flame Bane IV"}, {1551,"Flame Bane V"}, {1552,"Flame Bane VI"}, {1553,"Blade Lure II"}, {1554,"Blade Lure III"}, {1555,"Blade Lure IV"}, {1556,"Blade Lure V"}, {1557,"Blade Lure VI"}, {1558,"Blade Bane II"}, {1559,"Blade Bane III"}, {1560,"Blade Bane IV"}, {1561,"Blade Bane V"}, {1562,"Blade Bane VI"}, {1563,"Piercing Lure I"}, {1564,"Piercing Lure II"}, {1565,"Piercing Lure III"}, {1566,"Piercing Lure IV"}, {1567,"Piercing Lure V"}, {1568,"Piercing Lure VI"}, {1569,"Piercing Bane I"}, {1570,"Piercing Bane II"}, {1571,"Piercing Bane III"}, {1572,"Piercing Bane IV"}, {1573,"Piercing Bane V"}, {1574,"Piercing Bane VI"}, {1575,"Strengthen Lock I"}, {1576,"Strengthen Lock II"}, {1577,"Strengthen Lock III"}, {1578,"Strengthen Lock IV"}, {1579,"Strengthen Lock V"}, {1580,"Strengthen Lock VI"}, {1581,"Weaken Lock I"}, {1582,"Weaken Lock II"}, {1583,"Weaken Lock III"}, {1584,"Weaken Lock IV"}, {1585,"Weaken Lock V"}, {1586,"Weaken Lock VI"}, {1587,"Aura of Heart Seeker Self I"}, {1588,"Aura of Heart Seeker Self II"}, {1589,"Aura of Heart Seeker Self III"}, {1590,"Aura of Heart Seeker Self IV"}, {1591,"Aura of Heart Seeker Self V"}, {1592,"Aura of Heart Seeker Self VI"}, {1593,"Turn Blade I"}, {1594,"Turn Blade II"}, {1595,"Turn Blade III"}, {1596,"Turn Blade IV"}, {1597,"Turn Blade V"}, {1598,"Turn Blade VI"}, {1599,"Aura of Defender Self I"}, {1601,"Aura of Defender Self II"}, {1602,"Aura of Defender Self III"}, {1603,"Aura of Defender Self IV"}, {1604,"Aura of Defender Self V"}, {1605,"Aura of Defender Self VI"}, {1606,"Lure Blade I"}, {1607,"Lure Blade II"}, {1608,"Lure Blade III"}, {1609,"Lure Blade IV"}, {1610,"Lure Blade V"}, {1611,"Lure Blade VI"}, {1612,"Aura of Blood Drinker Self II"}, {1613,"Aura of Blood Drinker Self III"}, {1614,"Aura of Blood Drinker Self IV"}, {1615,"Aura of Blood Drinker Self V"}, {1616,"Aura of Blood Drinker Self VI"}, {1617,"Blood Loather II"}, {1618,"Blood Loather III"}, {1619,"Blood Loather IV"}, {1620,"Blood Loather V"}, {1621,"Blood Loather VI"}, {1623,"Aura of Swift Killer Self II"}, {1624,"Aura of Swift Killer Self III"}, {1625,"Aura of Swift Killer Self IV"}, {1626,"Aura of Swift Killer Self V"}, {1627,"Aura of Swift Killer Self VI"}, {1629,"Leaden Weapon II"}, {1630,"Leaden Weapon III"}, {1631,"Leaden Weapon IV"}, {1632,"Leaden Weapon V"}, {1633,"Leaden Weapon VI"}, {1634,"Portal Sending"}, {1635,"Lifestone Recall"}, {1636,"Lifestone Sending"}, {1637,"Summon Primary Portal III"}, {1638,"Defenselessness Self I"}, {1639,"Defenselessness Self II"}, {1640,"Defenselessness Self III"}, {1641,"Defenselessness Self IV"}, {1642,"Defenselessness Self V"}, {1643,"Defenselessness Self VI"}, {1644,"The Gift of Sarneho"}, {1658,"Stamina to Health Other I"}, {1659,"Stamina to Health Other II"}, {1660,"Stamina to Health Other III"}, {1661,"Stamina to Health Other IV"}, {1662,"Stamina to Health Other V"}, {1663,"Stamina to Health Other VI"}, {1664,"Stamina to Health Self I"}, {1665,"Stamina to Health Self II"}, {1666,"Stamina to Health Self III"}, {1667,"Stamina to Health Self IV"}, {1668,"Stamina to Health Self V"}, {1669,"Stamina to Health Self VI"}, {1670,"Stamina to Mana Other I"}, {1671,"Stamina to Mana Other II"}, {1672,"Stamina to Mana Other III"}, {1673,"Stamina to Mana Other IV"}, {1674,"Stamina to Mana Other V"}, {1675,"Stamina to Mana Other VI"}, {1676,"Stamina to Mana Self I"}, {1677,"Stamina to Mana Self II"}, {1678,"Stamina to Mana Self III"}, {1679,"Stamina to Mana Self IV"}, {1680,"Stamina to Mana Self V"}, {1681,"Stamina to Mana Self VI"}, {1702,"Health to Mana Self IV"}, {1703,"Health to Mana Self V"}, {1704,"Health to Mana Self VI"}, {1705,"Health to Mana Other I"}, {1706,"Health to Mana Other II"}, {1707,"Health to Mana Other III"}, {1708,"Wedding Bliss"}, {1709,"Cooking Mastery Other I"}, {1710,"Cooking Mastery Other II"}, {1711,"Cooking Mastery Other III"}, {1712,"Cooking Mastery Other IV"}, {1713,"Cooking Mastery Other V"}, {1714,"Cooking Mastery Other VI"}, {1715,"Cooking Mastery Self I"}, {1716,"Cooking Mastery Self II"}, {1717,"Cooking Mastery Self III"}, {1718,"Cooking Mastery Self IV"}, {1719,"Cooking Mastery Self V"}, {1720,"Cooking Mastery Self VI"}, {1721,"Cooking Ineptitude Other I"}, {1722,"Cooking Ineptitude Other II"}, {1723,"Cooking Ineptitude Other III"}, {1724,"Cooking Ineptitude Other IV"}, {1725,"Cooking Ineptitude Other V"}, {1726,"Cooking Ineptitude Other VI"}, {1727,"Cooking Ineptitude Self I"}, {1728,"Cooking Ineptitude Self II"}, {1729,"Cooking Ineptitude Self III"}, {1730,"Cooking Ineptitude Self IV"}, {1731,"Cooking Ineptitude Self V"}, {1732,"Cooking Ineptitude Self VI"}, {1733,"Fletching Mastery Other I"}, {1734,"Fletching Mastery Other II"}, {1735,"Fletching Mastery Other III"}, {1736,"Fletching Mastery Other IV"}, {1737,"Fletching Mastery Other V"}, {1738,"Fletching Mastery Other VI"}, {1739,"Fletching Mastery Self I"}, {1740,"Fletching Mastery Self II"}, {1741,"Fletching Mastery Self III"}, {1742,"Fletching Mastery Self IV"}, {1743,"Fletching Mastery Self V"}, {1744,"Fletching Mastery Self VI"}, {1745,"Fletching Ineptitude Other I"}, {1746,"Fletching Ineptitude Other II"}, {1747,"Fletching Ineptitude Other III"}, {1748,"Fletching Ineptitude Other IV"}, {1749,"Fletching Ineptitude Other V"}, {1750,"Fletching Ineptitude Other VI"}, {1751,"Fletching Ineptitude Self I"}, {1752,"Fletching Ineptitude Self II"}, {1753,"Fletching Ineptitude Self III"}, {1754,"Fletching Ineptitude Self IV"}, {1755,"Fletching Ineptitude Self V"}, {1756,"Fletching Ineptitude Self VI"}, {1757,"Alchemy Mastery Other I"}, {1758,"Alchemy Mastery Other II"}, {1759,"Alchemy Mastery Other III"}, {1760,"Alchemy Mastery Other IV"}, {1761,"Alchemy Mastery Other V"}, {1762,"Alchemy Mastery Other VI"}, {1763,"Alchemy Mastery Self I"}, {1764,"Alchemy Mastery Self II"}, {1765,"Alchemy Mastery Self III"}, {1766,"Alchemy Mastery Self IV"}, {1767,"Alchemy Mastery Self V"}, {1768,"Alchemy Mastery Self VI"}, {1769,"Alchemy Ineptitude Other I"}, {1770,"Alchemy Ineptitude Other II"}, {1771,"Alchemy Ineptitude Other III"}, {1772,"Alchemy Ineptitude Other IV"}, {1773,"Alchemy Ineptitude Other V"}, {1774,"Alchemy Ineptitude Other VI"}, {1775,"Alchemy Ineptitude Self I"}, {1776,"Alchemy Ineptitude Self II"}, {1777,"Alchemy Ineptitude Self III"}, {1778,"Alchemy Ineptitude Self IV"}, {1779,"Alchemy Ineptitude Self V"}, {1780,"Alchemy Ineptitude Self VI"}, {1781,"Exploding Magma"}, {1782,"Gertarh's Curse"}, {1783,"Searing Disc"}, {1784,"Horizon's Blades"}, {1785,"Cassius' Ring of Fire"}, {1786,"Nuhmudira's Spines"}, {1787,"Halo of Frost"}, {1788,"Eye of the Storm"}, {1789,"Tectonic Rifts"}, {1790,"Acid Streak I"}, {1791,"Acid Streak II"}, {1792,"Acid Streak III"}, {1793,"Acid Streak IV"}, {1794,"Acid Streak V"}, {1795,"Acid Streak VI"}, {1796,"Flame Streak I"}, {1797,"Flame Streak II"}, {1798,"Flame Streak III"}, {1799,"Flame Streak IV"}, {1800,"Flame Streak V"}, {1801,"Flame Streak VI"}, {1802,"Force Streak I"}, {1803,"Force Streak II"}, {1804,"Force Streak III"}, {1805,"Force Streak IV"}, {1806,"Force Streak V"}, {1807,"Force Streak VI"}, {1808,"Frost Streak I"}, {1809,"Frost Streak II"}, {1810,"Frost Streak III"}, {1811,"Frost Streak IV"}, {1812,"Frost Streak V"}, {1813,"Frost Streak VI"}, {1814,"Lightning Streak I"}, {1815,"Lightning Streak II"}, {1816,"Lightning Streak III"}, {1817,"Lightning Streak IV"}, {1818,"Lightning Streak V"}, {1819,"Lightning Streak VI"}, {1820,"Shock Wave Streak I"}, {1821,"Shock Wave Streak II"}, {1822,"Shock Wave Streak III"}, {1823,"Shock Wave Streak IV"}, {1824,"Shock Wave Streak V"}, {1825,"Shock Wave Streak VI"}, {1826,"Whirling Blade Streak I"}, {1827,"Whirling Blade Streak II"}, {1828,"Whirling Blade Streak III"}, {1829,"Whirling Blade Streak IV"}, {1830,"Whirling Blade Streak V"}, {1831,"Whirling Blade Streak VI"}, {1832,"Torrential Acid"}, {1833,"Squall of Swords"}, {1834,"Firestorm"}, {1835,"Splinterfall"}, {1836,"Avalanche"}, {1837,"Lightning Barrage"}, {1838,"Stone Fists"}, {1839,"Blistering Creeper"}, {1840,"Bed of Blades"}, {1841,"Slithering Flames"}, {1842,"Spike Strafe"}, {1843,"Foon-Ki's Glacial Floe"}, {1844,"Os' Wall"}, {1845,"Hammering Crawler"}, {1846,"Curse of Black Fire"}, {1847,"Evaporate All Magic Other"}, {1848,"Evaporate All Magic Other"}, {1849,"Evaporate All Magic Other"}, {1850,"Evaporate All Magic Self"}, {1851,"Evaporate All Magic Self"}, {1852,"Evaporate All Magic Self"}, {1853,"Extinguish All Magic Other"}, {1854,"Extinguish All Magic Other"}, {1855,"Extinguish All Magic Other"}, {1856,"Extinguish All Magic Self"}, {1857,"Extinguish All Magic Self"}, {1858,"Extinguish All Magic Self"}, {1859,"Cleanse All Magic Other"}, {1860,"Cleanse All Magic Other"}, {1861,"Cleanse All Magic Other"}, {1862,"Cleanse All Magic Self"}, {1863,"Cleanse All Magic Self"}, {1864,"Cleanse All Magic Self"}, {1865,"Devour All Magic Other"}, {1866,"Devour All Magic Other"}, {1867,"Devour All Magic Other"}, {1868,"Devour All Magic Self"}, {1869,"Devour All Magic Self"}, {1870,"Devour All Magic Self"}, {1871,"Purge All Magic Other"}, {1872,"Purge All Magic Other"}, {1873,"Purge All Magic Other"}, {1874,"Purge All Magic Self"}, {1875,"Purge All Magic Self"}, {1876,"Purge All Magic Self"}, {1877,"Nullify All Magic Other"}, {1878,"Nullify All Magic Other"}, {1879,"Nullify All Magic Other"}, {1880,"Nullify All Magic Self"}, {1881,"Nullify All Magic Self"}, {1882,"Nullify All Magic Self"}, {1883,"Evaporate Creature Magic Other"}, {1884,"Evaporate Creature Magic Other"}, {1885,"Evaporate Creature Magic Other"}, {1886,"Evaporate Creature Magic Self"}, {1887,"Evaporate Creature Magic Self"}, {1888,"Evaporate Creature Magic Self"}, {1889,"Extinguish Creature Magic Other"}, {1890,"Extinguish Creature Magic Other"}, {1891,"Extinguish Creature Magic Other"}, {1892,"Extinguish Creature Magic Self"}, {1893,"Extinguish Creature Magic Self"}, {1894,"Extinguish Creature Magic Self"}, {1895,"Cleanse Creature Magic Other"}, {1896,"Cleanse Creature Magic Other"}, {1897,"Cleanse Creature Magic Other"}, {1898,"Cleanse Creature Magic Self"}, {1899,"Cleanse Creature Magic Self"}, {1900,"Cleanse Creature Magic Self"}, {1901,"Devour Creature Magic Other"}, {1902,"Devour Creature Magic Other"}, {1903,"Devour Creature Magic Other"}, {1904,"Devour Creature Magic Self"}, {1905,"Devour Creature Magic Self"}, {1906,"Devour Creature Magic Self"}, {1907,"Purge Creature Magic Other"}, {1908,"Purge Creature Magic Other"}, {1909,"Purge Creature Magic Other"}, {1910,"Purge Creature Magic Self"}, {1911,"Purge Creature Magic Self"}, {1912,"Purge Creature Magic Self"}, {1913,"Nullify Creature Magic Other"}, {1914,"Nullify Creature Magic Other"}, {1915,"Nullify Creature Magic Other"}, {1916,"Nullify Creature Magic Self"}, {1917,"Nullify Creature Magic Self"}, {1918,"Nullify Creature Magic Self"}, {1919,"Evaporate Item Magic"}, {1920,"Evaporate Item Magic"}, {1921,"Evaporate Item Magic"}, {1922,"Evaporate Item Magic"}, {1923,"Evaporate Item Magic"}, {1924,"Evaporate Item Magic"}, {1925,"Extinguish Item Magic"}, {1926,"Extinguish Item Magic"}, {1927,"Extinguish Item Magic"}, {1928,"Extinguish Item Magic"}, {1929,"Extinguish Item Magic"}, {1930,"Extinguish Item Magic"}, {1931,"Cleanse Item Magic"}, {1932,"Cleanse Item Magic"}, {1933,"Cleanse Item Magic"}, {1934,"Cleanse Item Magic"}, {1935,"Cleanse Item Magic"}, {1936,"Cleanse Item Magic"}, {1937,"Devour Item Magic"}, {1938,"Devour Item Magic"}, {1939,"Devour Item Magic"}, {1940,"Devour Item Magic"}, {1941,"Devour Item Magic"}, {1942,"Devour Item Magic"}, {1943,"Purge Item Magic"}, {1944,"Purge Item Magic"}, {1945,"Purge Item Magic"}, {1946,"Purge Item Magic"}, {1947,"Purge Item Magic"}, {1948,"Purge Item Magic"}, {1949,"Nullify Item Magic"}, {1950,"Nullify Item Magic"}, {1951,"Nullify Item Magic"}, {1952,"Nullify Item Magic"}, {1953,"Nullify Item Magic"}, {1954,"Nullify Item Magic"}, {1955,"Evaporate Life Magic Other"}, {1956,"Evaporate Life Magic Other"}, {1957,"Evaporate Life Magic Other"}, {1958,"Evaporate Life Magic Self"}, {1959,"Evaporate Life Magic Self"}, {1960,"Evaporate Life Magic Self"}, {1961,"Extinguish Life Magic Other"}, {1962,"Extinguish Life Magic Other"}, {1963,"Extinguish Life Magic Other"}, {1964,"Extinguish Life Magic Self"}, {1965,"Extinguish Life Magic Self"}, {1966,"Extinguish Life Magic Self"}, {1967,"Cleanse Life Magic Other"}, {1968,"Cleanse Life Magic Other"}, {1969,"Cleanse Life Magic Other"}, {1970,"Cleanse Life Magic Self"}, {1971,"Cleanse Life Magic Self"}, {1972,"Cleanse Life Magic Self"}, {1973,"Devour Life Magic Other"}, {1974,"Devour Life Magic Other"}, {1975,"Devour Life Magic Other"}, {1976,"Devour Life Magic Self"}, {1977,"Devour Life Magic Self"}, {1978,"Devour Life Magic Self"}, {1979,"Purge Life Magic Other"}, {1980,"Purge Life Magic Other"}, {1981,"Purge Life Magic Other"}, {1982,"Purge Life Magic Self"}, {1983,"Purge Life Magic Self"}, {1984,"Purge Life Magic Self"}, {1985,"Nullify Life Magic Other"}, {1986,"Nullify Life Magic Other"}, {1987,"Nullify Life Magic Other"}, {1988,"Nullify Life Magic Self"}, {1989,"Nullify Life Magic Self"}, {1990,"Nullify Life Magic Self"}, {1991,"Mana Blight"}, {1992,"Camping Mastery"}, {1993,"Camping Ineptitude"}, {1994,"Aura of Wound Twister"}, {1995,"Aura of Alacrity"}, {1996,"Aura of Soul Hunter"}, {1997,"Life Giver"}, {1998,"Stamina Giver"}, {1999,"Mana Giver"}, {2000,"Portal Sending"}, {2001,"Portal Sending"}, {2002,"Portal Sending"}, {2003,"Warrior's Lesser Vitality"}, {2004,"Warrior's Vitality"}, {2005,"Warrior's Greater Vitality"}, {2006,"Warrior's Ultimate Vitality"}, {2007,"Warrior's Lesser Vigor"}, {2008,"Warrior's Vigor"}, {2009,"Warrior's Greater Vigor"}, {2010,"Warrior's Ultimate Vigor"}, {2011,"Wizard's Lesser Intellect"}, {2012,"Wizard's Intellect"}, {2013,"Wizard's Greater Intellect"}, {2014,"Wizard's Ultimate Intellect"}, {2015,"Aerfalle's Ward"}, {2016,"Impulse"}, {2017,"Bunny Smite"}, {2018,"Tormenter of Flesh"}, {2019,"The sundering of the crystal"}, {2020,"Recall Asmolum 1"}, {2021,"Thaumaturgic Shroud"}, {2022,"Soul Shroud"}, {2023,"Recall the Sanctuary"}, {2024,"Recall Asmolum 2"}, {2025,"RecallAsmolum 3"}, {2026,"Nerve Burn"}, {2027,"Martyr"}, {2028,"The Path to Kelderam's Ward"}, {2029,"Stamina Blight"}, {2030,"Flaming Blaze"}, {2031,"Steel Thorns"}, {2032,"Electric Blaze"}, {2033,"Acidic Spray"}, {2034,"Exploding Fury"}, {2035,"Electric Discharge"}, {2036,"Fuming Acid"}, {2037,"Flaming Irruption"}, {2038,"Exploding Ice"}, {2039,"Sparking Fury"}, {2040,"The Path to Kelderam's Ward"}, {2041,"Aerlinthe Recall"}, {2042,"Demon's Tongues"}, {2043,"Weight of Eternity"}, {2044,"Item Befoulment"}, {2045,"Demon Fists"}, {2046,"Portal to Teth"}, {2047,"Demonskin"}, {2048,"Boon of the Demon"}, {2049,"Bile of the Hopeslayer"}, {2050,"Young Love"}, {2051,"Young Love"}, {2052,"Executor's Boon"}, {2053,"Executor's Blessing"}, {2054,"Synaptic Misfire"}, {2055,"Bafflement Self VII"}, {2056,"Ataxia"}, {2057,"Clumsiness Self VII"}, {2058,"Boon of Refinement"}, {2059,"Honed Control"}, {2060,"Temeritous Touch"}, {2061,"Perseverance"}, {2062,"Anemia"}, {2063,"Enfeeble Self VII"}, {2064,"Self Loathing"}, {2065,"Feeblemind Self VII"}, {2066,"Calming Gaze"}, {2067,"Inner Calm"}, {2068,"Brittle Bones"}, {2069,"Frailty Self VII"}, {2070,"Heart Rend"}, {2071,"Harm Self VII"}, {2072,"Adja's Gift"}, {2073,"Adja's Intervention"}, {2074,"Gossamer Flesh"}, {2075,"Imperil Self VII"}, {2076,"Mana Boost Other VII"}, {2077,"Mana Boost Self VII"}, {2078,"Void's Call"}, {2079,"Mana Drain Self VII"}, {2080,"Ogfoot"}, {2081,"Hastening"}, {2082,"Replenish"}, {2083,"Robustification"}, {2084,"Belly of Lead"}, {2085,"Slowness Self VII"}, {2086,"Might of the 5 Mules"}, {2087,"Might of the Lugians"}, {2088,"Senescence"}, {2089,"Weakness Self VII"}, {2090,"Bolstered Will"}, {2091,"Mind Blossom"}, {2092,"Olthoi's Bane"}, {2093,"Olthoi Bait"}, {2094,"Swordsman's Bane"}, {2095,"Swordsman Bait"}, {2096,"Aura of Infected Caress"}, {2097,"Pacification"}, {2098,"Tusker's Bane"}, {2099,"Tusker Bait"}, {2100,"Tattercoat"}, {2101,"Aura of Cragstone's Will"}, {2102,"Inferno's Bane"}, {2103,"Inferno Bait"}, {2104,"Gelidite's Bane"}, {2105,"Gelidite Bait"}, {2106,"Aura of Elysa's Sight"}, {2107,"Cabalistic Ostracism"}, {2108,"Brogard's Defiance"}, {2109,"Lugian's Speed"}, {2110,"Astyrrian's Bane"}, {2111,"Astyrrian Bait"}, {2112,"Wi's Folly"}, {2113,"Archer's Bane"}, {2114,"Archer Bait"}, {2115,"Fortified Lock"}, {2116,"Aura of Atlan's Alacrity"}, {2117,"Aura of Mystic's Blessing"}, {2118,"Clouded Motives"}, {2119,"Vagabond's Gift"}, {2120,"Dissolving Vortex"}, {2121,"Corrosive Flash"}, {2122,"Disintegration"}, {2123,"Celdiseth's Searing"}, {2124,"Sau Kolin's Sword"}, {2125,"Flensing Wings"}, {2126,"Thousand Fists"}, {2127,"Silencia's Scorn"}, {2128,"Ilservian's Flame"}, {2129,"Sizzling Fury"}, {2130,"Infernae"}, {2131,"Stinging Needles"}, {2132,"The Spike"}, {2133,"Outlander's Insolence"}, {2134,"Fusillade"}, {2135,"Winter's Embrace"}, {2136,"Icy Torment"}, {2137,"Sudden Frost"}, {2138,"Blizzard"}, {2139,"Luminous Wrath"}, {2140,"Alset's Coil"}, {2141,"Lhen's Flare"}, {2142,"Tempest"}, {2143,"Pummeling Storm"}, {2144,"Crushing Shame"}, {2145,"Cameron's Curse"}, {2146,"Evisceration"}, {2147,"Rending Wind"}, {2148,"Caustic Boon"}, {2149,"Caustic Blessing"}, {2150,"Boon of the Blade Turner"}, {2151,"Blessing of the Blade Turner"}, {2152,"Boon of the Mace Turner"}, {2153,"Blessing of the Mace Turner"}, {2154,"Icy Boon"}, {2155,"Icy Blessing"}, {2156,"Fiery Boon"}, {2157,"Fiery Blessing"}, {2158,"Storm's Boon"}, {2159,"Storm's Blessing"}, {2160,"Boon of the Arrow Turner"}, {2161,"Blessing of the Arrow Turner"}, {2162,"Olthoi's Gift"}, {2163,"Acid Vulnerability Self VII"}, {2164,"Swordsman's Gift"}, {2165,"Blade Vulnerability Self VII"}, {2166,"Tusker's Gift"}, {2167,"Bludgeoning Vulnerability Self VII"}, {2168,"Gelidite's Gift"}, {2169,"Cold Vulnerability Self VII"}, {2170,"Inferno's Gift"}, {2171,"Fire Vulnerability Self VII"}, {2172,"Astyrrian's Gift"}, {2173,"Lightning Vulnerability Self VII"}, {2174,"Archer's Gift"}, {2175,"Piercing Vulnerability Self VII"}, {2176,"Enervation"}, {2177,"Exhaustion Self VII"}, {2178,"Decrepitude's Grasp"}, {2179,"Fester Self VII"}, {2180,"Energy Flux"}, {2181,"Mana Depletion Self VII"}, {2182,"Battlemage's Boon"}, {2183,"Battlemage's Blessing"}, {2184,"Hydra's Head"}, {2185,"Robustify"}, {2186,"Tenaciousness"}, {2187,"Unflinching Persistence"}, {2188,"Bottle Breaker"}, {2189,"Alchemy Ineptitude Self VII"}, {2190,"Silencia's Boon"}, {2191,"Silencia's Blessing"}, {2192,"Hands of Chorizite"}, {2193,"Arcane Benightedness Self VII"}, {2194,"Aliester's Boon"}, {2195,"Aliester's Blessing"}, {2196,"Jibril's Boon"}, {2197,"Jibril's Blessing"}, {2198,"Jibril's Vitae"}, {2199,"Armor Tinkering Ignorance Self VII"}, {2200,"Light Weapon Ineptitude Other VII"}, {2201,"Light Weapon Ineptitude Self VII"}, {2202,"Light Weapon Mastery Other VII"}, {2203,"Light Weapon Mastery Self VII"}, {2204,"Missile Weapon Ineptitude Other VII"}, {2205,"Missile Weapon Ineptitude Self VII"}, {2206,"Missile Weapon Mastery Other VII"}, {2207,"Missile Weapon Mastery Self VII"}, {2208,"Challenger's Legacy"}, {2209,"Cooking Ineptitude Self VII"}, {2210,"Morimoto's Boon"}, {2211,"Morimoto's Blessing"}, {2212,"Wrath of Adja"}, {2213,"Creature Enchantment Ineptitude Self VII"}, {2214,"Adja's Boon"}, {2215,"Adja's Blessing"}, {2216,"Missile Weapon Ineptitude Other VII"}, {2217,"Missile Weapon Ineptitude Self VII"}, {2218,"Missile Weapon Mastery Other VII"}, {2219,"Missile Weapon Mastery Self VII"}, {2220,"Finesse Weapon Ineptitude Other VII"}, {2221,"Finesse Weapon Ineptitude Self VII"}, {2222,"Finesse Weapon Mastery Other VII"}, {2223,"Finesse Weapon Mastery Self VII"}, {2224,"Hearts on Sleeves"}, {2225,"Deception Ineptitude Self VII"}, {2226,"Ketnan's Boon"}, {2227,"Ketnan's Blessing"}, {2228,"Broadside of a Barn"}, {2229,"Defenselessness Self VII"}, {2230,"Sashi Mu's Kiss"}, {2231,"Faithlessness Self VII"}, {2232,"Odif's Boon"}, {2233,"Odif's Blessing"}, {2234,"Twisted Digits"}, {2235,"Fletching Ineptitude Self VII"}, {2236,"Lilitha's Boon"}, {2237,"Lilitha's Blessing"}, {2238,"Unsteady Hands"}, {2239,"Healing Ineptitude Self VII"}, {2240,"Avalenne's Boon"}, {2241,"Avalenne's Blessing"}, {2242,"Web of Deflection"}, {2243,"Aura of Deflection"}, {2244,"Web of Defense"}, {2245,"Aura of Defense"}, {2246,"Wrath of Celcynd"}, {2247,"Item Enchantment Ineptitude Self VII"}, {2248,"Celcynd's Boon"}, {2249,"Celcynd's Blessing"}, {2250,"Yoshi's Boon"}, {2251,"Yoshi's Blessing"}, {2252,"Unfortunate Appraisal"}, {2253,"Item Tinkering Ignorance Self VII"}, {2254,"Feat of Radaz"}, {2255,"Jumping Ineptitude Self VII"}, {2256,"Jahannan's Boon"}, {2257,"Jahannan's Blessing"}, {2258,"Gears Unwound"}, {2259,"Leaden Feet Self VII"}, {2260,"Kwipetian Vision"}, {2261,"Leadership Ineptitude Self VII"}, {2262,"Ar-Pei's Boon"}, {2263,"Ar-Pei's Blessing"}, {2264,"Wrath of Harlune"}, {2265,"Life Magic Ineptitude Self VII"}, {2266,"Harlune's Boon"}, {2267,"Harlune's Blessing"}, {2268,"Fat Fingers"}, {2269,"Lockpick Ineptitude Self VII"}, {2270,"Oswald's Boon"}, {2271,"Oswald's Blessing"}, {2272,"Light Weapon Ineptitude Other VII"}, {2273,"Light Weapon Ineptitude Self VII"}, {2274,"Light Weapon Mastery Other VII"}, {2275,"Light Weapon Mastery Self VII"}, {2276,"Celdiseth's Boon"}, {2277,"Celdiseth's Blessing"}, {2278,"Eyes Clouded"}, {2279,"Magic Item Tinkering Ignorance Self VII"}, {2280,"Web of Resistance"}, {2281,"Aura of Resistance"}, {2282,"Futility"}, {2283,"Magic Yield Self VII"}, {2284,"Inefficient Investment"}, {2285,"Mana Conversion Ineptitude Self VII"}, {2286,"Nuhmudira's Boon"}, {2287,"Nuhmudira's Blessing"}, {2288,"Topheron's Boon"}, {2289,"Topheron's Blessing"}, {2290,"Ignorance's Bliss"}, {2291,"Monster Unfamiliarity Self VII"}, {2292,"Kaluhc's Boon"}, {2293,"Kaluhc's Blessing"}, {2294,"Introversion"}, {2295,"Person Unfamiliarity Self VII"}, {2296,"Light Weapon Ineptitude Other VII"}, {2297,"Light Weapon Ineptitude Self VII"}, {2298,"Light Weapon Mastery Other VII"}, {2299,"Light Weapon Mastery Self VII"}, {2300,"Saladur's Boon"}, {2301,"Saladur's Blessing"}, {2302,"Light Weapon Ineptitude Other VII"}, {2303,"Light Weapon Ineptitude Self VII"}, {2304,"Light Weapon Mastery Other VII"}, {2305,"Light Weapon Mastery Self VII"}, {2306,"Heavy Weapon Ineptitude Other VII"}, {2307,"Heavy Weapon Ineptitude Self VII"}, {2308,"Heavy Weapon Mastery Other VII"}, {2309,"Heavy Weapon Mastery Self VII"}, {2310,"Missile Weapon Ineptitude Other VII"}, {2311,"Missile Weapon Ineptitude Self VII"}, {2312,"Missile Weapon Mastery Other VII"}, {2313,"Missile Weapon Mastery Self VII"}, {2314,"Light Weapon Ineptitude Other VII"}, {2315,"Light Weapon Mastery Other VII"}, {2316,"Light Weapon Mastery Self VII"}, {2317,"Light Weapon Ineptitude Other VII"}, {2318,"Gravity Well"}, {2319,"Vulnerability Self VII"}, {2320,"Wrath of the Hieromancer"}, {2321,"War Magic Ineptitude Self VII"}, {2322,"Hieromancer's Boon"}, {2323,"Hieromancer's Blessing"}, {2324,"Koga's Boon"}, {2325,"Koga's Blessing"}, {2326,"Eye of the Grunt"}, {2327,"Weapon Tinkering Ignorance Self VII"}, {2328,"Vitality Siphon"}, {2329,"Essence Void"}, {2330,"Vigor Siphon"}, {2331,"Health to Mana Other VII"}, {2332,"Cannibalize"}, {2333,"Health to Stamina Other VII"}, {2334,"Self Sacrifice"}, {2335,"Gift of Vitality"}, {2336,"Gift of Essence"}, {2337,"Gift of Vigor"}, {2338,"Mana to Health Other VII"}, {2339,"Energize Vitality"}, {2340,"Mana to Stamina Other VII"}, {2341,"Energize Vigor"}, {2342,"Stamina to Health Other VII"}, {2343,"Rushed Recovery"}, {2344,"Stamina to Mana Other VII"}, {2345,"Meditative Trance"}, {2346,"Malediction"}, {2347,"Concentration"}, {2348,"Brilliance"}, {2349,"Hieromancer's Ward"}, {2350,"Greater Decay Durance"}, {2351,"Greater Consumption Durance"}, {2352,"Greater Stasis Durance"}, {2353,"Greater Stimulation Durance"}, {2354,"Lesser Piercing Durance"}, {2355,"Lesser Slashing Durance"}, {2356,"Lesser Bludgeoning Durance"}, {2357,"Fauna Perlustration"}, {2358,"Lyceum Recall"}, {2359,"Portal Sending"}, {2360,"Portal Sending"}, {2361,"Portal Sending"}, {2362,"Portal Sending"}, {2363,"Portal Sending"}, {2364,"Egress"}, {2365,"something you're gonna fear for a long time"}, {2366,"Bovine Intervention"}, {2367,"Groovy Portal Sending"}, {2368,"a powerful force"}, {2369,"Expulsion"}, {2370,"Gift of Rotting Flesh"}, {2371,"Curse of Mortal Flesh"}, {2372,"Price of Immortality"}, {2373,"Enervation of the Heart"}, {2374,"Enervation of the Limb"}, {2375,"Enervation of the Mind"}, {2376,"Glimpse of Annihilation"}, {2377,"Vision of Annihilation"}, {2378,"Beast Murmur"}, {2379,"Beast Whisper"}, {2380,"Grip of Instrumentality"}, {2381,"Touch of Instrumentality"}, {2382,"Unnatural Persistence"}, {2383,"Dark Flame"}, {2384,"Arcane Restoration"}, {2385,"Vigilance"}, {2386,"Indomitability"}, {2387,"Determination"}, {2388,"Caution"}, {2389,"Vigor"}, {2390,"Haste"}, {2391,"Prowess"}, {2392,"Serenity"}, {2393,"Force Armor"}, {2394,"Acid Shield"}, {2395,"Electric Shield"}, {2396,"Flame Shield"}, {2397,"Ice Shield"}, {2398,"Bludgeon Shield"}, {2399,"Piercing Shield"}, {2400,"Slashing Shield"}, {2401,"Into the Garden"}, {2402,"Essence Lull"}, {2403,"Balanced Breakfast"}, {2404,"Collector Acid Protection"}, {2405,"Collector Blade Protection"}, {2406,"Collector Bludgeoning Protection"}, {2407,"Collector Cold Protection"}, {2408,"Collector Fire Protection"}, {2409,"Collector Lightning Protection"}, {2410,"Collector Piercing Protection"}, {2411,"Discipline"}, {2412,"Enduring Coordination"}, {2413,"Enduring Focus"}, {2414,"Enduring Stoicism"}, {2415,"Eye of the Hunter"}, {2416,"High Tension String"}, {2417,"Obedience"}, {2418,"Occult Potence"}, {2419,"Panic Attack"}, {2420,"Panoply of the Queenslayer"}, {2421,"Paralyzing Fear"}, {2422,"Send to Dryreach"}, {2423,"Precise"}, {2424,"Rabbit's Eye"}, {2425,"Stone Wall"}, {2426,"Strong Pull"}, {2427,"Sugar Rush"}, {2428,"Timaru's Shelter"}, {2429,"Timaru's Shelter"}, {2430,"Timaru's Shelter"}, {2431,"Vivification"}, {2432,"Acid Ward"}, {2433,"Flame Ward"}, {2434,"Frost Ward"}, {2435,"Lightning Ward"}, {2436,"Laying on of Hands"}, {2437,"Greater Rockslide"}, {2438,"Lesser Rockslide"}, {2439,"Rockslide"}, {2440,"Greater Stone Cliffs"}, {2441,"Lesser Stone Cliffs"}, {2442,"Stone Cliffs"}, {2443,"Greater Strength of Earth"}, {2444,"Lesser Strength of Earth"}, {2445,"Strength of Earth"}, {2446,"Greater Growth"}, {2447,"Lesser Growth"}, {2448,"Growth"}, {2449,"Greater Hunter's Acumen"}, {2450,"Lesser Hunter's Acumen"}, {2451,"Hunter's Acumen"}, {2452,"Greater Thorns"}, {2453,"Lesser Thorns"}, {2454,"Thorns"}, {2455,"Greater Cascade"}, {2456,"Lesser Cascade"}, {2457,"Cascade"}, {2458,"Greater Cascade"}, {2459,"Lesser Cascade"}, {2460,"Cascade"}, {2461,"Greater Cascade"}, {2462,"Lesser Cascade"}, {2463,"Cascade"}, {2464,"Greater Cascade"}, {2465,"Lesser Cascade"}, {2466,"Cascade"}, {2467,"Greater Cascade"}, {2468,"Lesser Cascade"}, {2469,"Cascade"}, {2470,"Greater Still Water"}, {2471,"Lesser Still Water"}, {2472,"Still Water"}, {2473,"Greater Torrent"}, {2474,"Lesser Torrent"}, {2475,"Torrent"}, {2476,"Safe Harbor"}, {2477,"Free Trip to the Aluvian Casino"}, {2478,"Cragstone Reinforcements camp recall"}, {2479,"Advance Camp Recall"}, {2480,"Free Trip to the Gharun'dim Casino"}, {2481,"Zaikhal Reinforcement Camp Recall"}, {2482,"Zaikhal Advance Camp Recall"}, {2483,"Free Trip to the Sho Casino"}, {2484,"Hebian-to Reinforcements Camp Portal"}, {2485,"Hebian-to Advance Camp Recall"}, {2486,"Blood Thirst"}, {2487,"Spirit Strike"}, {2488,"Weapon Familiarity"}, {2489,"Free Ride to the Shoushi Southeast Outpost Portal"}, {2490,"Free Ride to the Holtburg South Outpost"}, {2491,"Free Ride to the Holtburg West Outpost"}, {2492,"Free Ride to the Shoushi West Outpost"}, {2493,"Free Ride to the Yaraq East Outpost"}, {2494,"Free Ride to the Yaraq North Outpost"}, {2495,"Send Reinforcements"}, {2496,"Send Reinforcements"}, {2497,"Send Reinforcements"}, {2498,"Send Reinforcements"}, {2499,"Send Reinforcements"}, {2500,"Send Reinforcements"}, {2501,"Major Alchemical Prowess"}, {2502,"Major Arcane Prowess"}, {2503,"Major Armor Tinkering Expertise"}, {2504,"Major Light Weapon Aptitude"}, {2505,"Major Missile Weapon Aptitude"}, {2506,"Major Cooking Prowess"}, {2507,"Major Creature Enchantment Aptitude"}, {2508,"Major Missile Weapon Aptitude"}, {2509,"Major Finesse Weapon Aptitude"}, {2510,"Major Deception Prowess"}, {2511,"Major Fealty"}, {2512,"Major Fletching Prowess"}, {2513,"Major Healing Prowess"}, {2514,"Major Impregnability"}, {2515,"Major Invulnerability"}, {2516,"Major Item Enchantment Aptitude"}, {2517,"Major Item Tinkering Expertise"}, {2518,"Major Jumping Prowess"}, {2519,"Major Leadership"}, {2520,"Major Life Magic Aptitude"}, {2521,"Major Lockpick Prowess"}, {2522,"Major Light Weapon Aptitude"}, {2523,"Major Magic Item Tinkering Expertise"}, {2524,"Major Magic Resistance"}, {2525,"Major Mana Conversion Prowess"}, {2526,"Major Monster Attunement"}, {2527,"Major Person Attunement"}, {2528,"Major Light Weapon Aptitude"}, {2529,"Major Sprint"}, {2530,"Major Light Weapon Aptitude"}, {2531,"Major Heavy Weapon Aptitude"}, {2532,"Major Missile Weapon Aptitude"}, {2533,"Major Light Weapon Aptitude"}, {2534,"Major War Magic Aptitude"}, {2535,"Major Weapon Tinkering Expertise"}, {2536,"Minor Alchemical Prowess"}, {2537,"Minor Arcane Prowess"}, {2538,"Minor Armor Tinkering Expertise"}, {2539,"Minor Light Weapon Aptitude"}, {2540,"Minor Missile Weapon Aptitude"}, {2541,"Minor Cooking Prowess"}, {2542,"Minor Creature Enchantment Aptitude"}, {2543,"Minor Missile Weapon Aptitude"}, {2544,"Minor Finesse Weapon Aptitude"}, {2545,"Minor Deception Prowess"}, {2546,"Minor Fealty"}, {2547,"Minor Fletching Prowess"}, {2548,"Minor Healing Prowess"}, {2549,"Minor Impregnability"}, {2550,"Minor Invulnerability"}, {2551,"Minor Item Enchantment Aptitude"}, {2552,"Minor Item Tinkering Expertise"}, {2553,"Minor Jumping Prowess"}, {2554,"Minor Leadership"}, {2555,"Minor Life Magic Aptitude"}, {2556,"Minor Lockpick Prowess"}, {2557,"Minor Light Weapon Aptitude"}, {2558,"Minor Magic Item Tinkering Expertise"}, {2559,"Minor Magic Resistance"}, {2560,"Minor Mana Conversion Prowess"}, {2561,"Minor Monster Attunement"}, {2562,"Minor Person Attunement"}, {2563,"Minor Light Weapon Aptitude"}, {2564,"Minor Sprint"}, {2565,"Minor Light Weapon Aptitude"}, {2566,"Minor Heavy Weapon Aptitude"}, {2567,"Minor Missile Weapon Aptitude"}, {2568,"Minor Light Weapon Aptitude"}, {2569,"Minor War Magic Aptitude"}, {2570,"Minor Weapon Tinkering Expertise"}, {2571,"Major Armor"}, {2572,"Major Coordination"}, {2573,"Major Endurance"}, {2574,"Major Focus"}, {2575,"Major Quickness"}, {2576,"Major Strength"}, {2577,"Major Willpower"}, {2578,"Minor Armor"}, {2579,"Minor Coordination"}, {2580,"Minor Endurance"}, {2581,"Minor Focus"}, {2582,"Minor Quickness"}, {2583,"Minor Strength"}, {2584,"Minor Willpower"}, {2585,"Major Acid Bane"}, {2586,"Major Blood Thirst"}, {2587,"Major Bludgeoning Bane"}, {2588,"Major Defender"}, {2589,"Major Flame Bane"}, {2590,"Major Frost Bane"}, {2591,"Major Heart Thirst"}, {2592,"Major Impenetrability"}, {2593,"Major Piercing Bane"}, {2594,"Major Slashing Bane"}, {2595,"Major Storm Bane"}, {2596,"Major Swift Hunter"}, {2597,"Minor Acid Bane"}, {2598,"Minor Blood Thirst"}, {2599,"Minor Bludgeoning Bane"}, {2600,"Minor Defender"}, {2601,"Minor Flame Bane"}, {2602,"Minor Frost Bane"}, {2603,"Minor Heart Thirst"}, {2604,"Minor Impenetrability"}, {2605,"Minor Piercing Bane"}, {2606,"Minor Slashing Bane"}, {2607,"Minor Storm Bane"}, {2608,"Minor Swift Hunter"}, {2609,"Major Acid Ward"}, {2610,"Major Bludgeoning Ward"}, {2611,"Major Flame Ward"}, {2612,"Major Frost Ward"}, {2613,"Major Piercing Ward"}, {2614,"Major Slashing Ward"}, {2615,"Major Storm Ward"}, {2616,"Minor Acid Ward"}, {2617,"Minor Bludgeoning Ward"}, {2618,"Minor Flame Ward"}, {2619,"Minor Frost Ward"}, {2620,"Minor Piercing Ward"}, {2621,"Minor Slashing Ward"}, {2622,"Minor Storm Ward"}, {2623,"Major Health Gain"}, {2624,"Major Mana Gain"}, {2625,"Major Stamina Gain"}, {2626,"Minor Health Gain"}, {2627,"Minor Mana Gain"}, {2628,"Minor Stamina Gain"}, {2629,"Huntress' Boon"}, {2630,"Prey's Reflex"}, {2631,"Secret Descent"}, {2632,"Secret Ascent"}, {2633,"Breaking and Entering"}, {2634,"Cautious Egress"}, {2635,"Witshire Passage"}, {2636,"Karenua's Curse"}, {2637,"Invoking Aun Tanua"}, {2638,"Heart of Oak"}, {2639,"Repulsion"}, {2640,"Devourer"}, {2641,"Force to Arms"}, {2642,"Consumption"}, {2643,"Stasis"}, {2644,"Lifestone Tie"}, {2645,"Portal Recall"}, {2646,"Secondary Portal Tie"}, {2647,"Secondary Portal Recall"}, {2648,"Summon Secondary Portal I"}, {2649,"Summon Secondary Portal II"}, {2650,"Summon Secondary Portal III"}, {2651,"Portal Sending Self Sacrifice"}, {2652,"Portal Sending Merciless"}, {2653,"Feeble Willpower"}, {2654,"Feeble Endurance"}, {2655,"Feeble Focus"}, {2656,"Feeble Quickness"}, {2657,"Feeble Strength"}, {2658,"Feeble Coordination"}, {2659,"Moderate Coordination"}, {2660,"Moderate Endurance"}, {2661,"Moderate Focus"}, {2662,"Moderate Quickness"}, {2663,"Moderate Strength"}, {2664,"Moderate Willpower"}, {2665,"Essence Sluice"}, {2666,"Essence Glutton"}, {2667,"Essence Spike"}, {2668,"Nuhmudiras Benefaction"}, {2669,"Nuhmudiras Bestowment"}, {2670,"Nuhmudiras Endowment"}, {2671,"Portal to the Callous Heart"}, {2672,"Ring of True Pain"}, {2673,"Ring of Unspeakable Agony"}, {2674,"Vicious Rebuke"}, {2675,"Feeble Light Weapon Aptitude"}, {2676,"Feeble Missile Weapon Aptitude"}, {2677,"Feeble Missile Weapon Aptitude"}, {2678,"Feeble Finesse Weapon Aptitude"}, {2679,"Feeble Light Weapon Aptitude"}, {2680,"Feeble Mana Conversion Prowess"}, {2681,"Feeble Light Weapon Aptitude"}, {2682,"Feeble Light Weapon Aptitude"}, {2683,"Feeble Heavy Weapon Aptitude"}, {2684,"Feeble Missile Weapon Aptitude"}, {2685,"Feeble Light Weapon Aptitude"}, {2686,"Moderate Light Weapon Aptitude"}, {2687,"Moderate Missile Weapon Aptitude"}, {2688,"Moderate Missile Weapon Aptitude"}, {2689,"Moderate Finesse Weapon Aptitude"}, {2690,"Moderate Light Weapon Aptitude"}, {2691,"Moderate Mana Conversion Prowess"}, {2692,"Moderate Light Weapon Aptitude"}, {2693,"Moderate Light Weapon Aptitude"}, {2694,"Moderate Heavy Weapon Aptitude"}, {2695,"Moderate Missile Weapon Aptitude"}, {2696,"Moderate Light Weapon Aptitude"}, {2697,"Aerfalle's Touch"}, {2698,"Aerfalle's Embrace"}, {2699,"Auroric Whip"}, {2700,"Corrosive Cloud"}, {2701,"Elemental Fury"}, {2702,"Elemental Fury"}, {2703,"Elemental Fury"}, {2704,"Elemental Fury"}, {2705,"Aerfalle's Enforcement"}, {2706,"Aerfalle's Gaze"}, {2707,"Elemental Pit"}, {2708,"Stasis Field"}, {2709,"Summon Primary Portal I"}, {2710,"Volcanic Blast"}, {2711,"Acid Arc I"}, {2712,"Acid Arc II"}, {2713,"Acid Arc III"}, {2714,"Acid Arc IV"}, {2715,"Acid Arc V"}, {2716,"Acid Arc VI"}, {2717,"Acid Arc VII"}, {2718,"Force Arc I"}, {2719,"Force Arc II"}, {2720,"Force Arc III"}, {2721,"Force Arc IV"}, {2722,"Force Arc V"}, {2723,"Force Arc VI"}, {2724,"Force Arc VII"}, {2725,"Frost Arc I"}, {2726,"Frost Arc II"}, {2727,"Frost Arc III"}, {2728,"Frost Arc IV"}, {2729,"Frost Arc V"}, {2730,"Frost Arc VI"}, {2731,"Frost Arc VII"}, {2732,"Lightning Arc I"}, {2733,"Lightning Arc II"}, {2734,"Lightning Arc III"}, {2735,"Lightning Arc IV"}, {2736,"Lightning Arc V"}, {2737,"Lightning Arc VI"}, {2738,"Lightning Arc VII"}, {2739,"Flame Arc I"}, {2740,"Flame Arc II"}, {2741,"Flame Arc III"}, {2742,"Flame Arc IV"}, {2743,"Flame Arc V"}, {2744,"Flame Arc VI"}, {2745,"Flame Arc VII"}, {2746,"Shock Arc I"}, {2747,"Shock Arc II"}, {2748,"Shock Arc III"}, {2749,"Shock Arc IV"}, {2750,"Shock Arc V"}, {2751,"Shock Arc VI"}, {2752,"Shock Arc VII"}, {2753,"Blade Arc I"}, {2754,"Blade Arc II"}, {2755,"Blade Arc III"}, {2756,"Blade Arc IV"}, {2757,"Blade Arc V"}, {2758,"Blade Arc VI"}, {2759,"Blade Arc VII"}, {2760,"Martyr's Hecatomb I"}, {2761,"Martyr's Hecatomb II"}, {2762,"Martyr's Hecatomb III"}, {2763,"Martyr's Hecatomb IV"}, {2764,"Martyr's Hecatomb V"}, {2765,"Martyr's Hecatomb VI"}, {2766,"Martyr's Hecatomb VII"}, {2767,"Martyr's Tenacity I"}, {2768,"Martyr's Tenacity II"}, {2769,"Martyr's Tenacity III"}, {2770,"Martyr's Tenacity IV"}, {2771,"Martyr's Tenacity V"}, {2772,"Martyr's Tenacity VI"}, {2773,"Martyr's Tenacity VII"}, {2774,"Martyr's Blight I"}, {2775,"Martyr's Blight II"}, {2776,"Martyr's Blight III"}, {2777,"Martyr's Blight IV"}, {2778,"Martyr's Blight V"}, {2779,"Martyr's Blight VI"}, {2780,"Martyr's Blight VII"}, {2781,"Lesser Elemental Fury"}, {2782,"Lesser Elemental Fury"}, {2783,"Lesser Elemental Fury"}, {2784,"Lesser Elemental Fury"}, {2785,"Lesser Stasis Field"}, {2786,"Madness"}, {2787,"Supremacy"}, {2788,"Essence Blight"}, {2789,"Elemental Destruction"}, {2790,"Weight of the World"}, {2791,"Rolling Death"}, {2792,"Rolling Death"}, {2793,"Rolling Death"}, {2794,"Rolling Death"}, {2795,"Citadel Library"}, {2796,"Citadel Surface"}, {2797,"Proving Grounds Rolling Death"}, {2798,"Proving Grounds High"}, {2799,"Proving Grounds Low"}, {2800,"Proving Grounds Mid"}, {2801,"Proving Grounds Extreme"}, {2802,"Proving Grounds High"}, {2803,"Proving Grounds Low"}, {2804,"Proving Grounds Mid"}, {2805,"Impudence"}, {2806,"Impudence"}, {2807,"Impudence"}, {2808,"Impudence"}, {2809,"Moderate Arcane Prowess"}, {2810,"Moderate Life Magic Aptitude"}, {2811,"Moderate Magic Resistance"}, {2812,"Moderate War Magic Aptitude"}, {2813,"Mount Lethe Recall"}, {2814,"Priest's Curse"}, {2815,"Boom Black Firework OUT"}, {2816,"Big Boom Black Firework OUT"}, {2817,"Shockwave Black Firework OUT"}, {2818,"Spiral Black Firework OUT"}, {2819,"Sparkle Black Firework OUT"}, {2820,"Blossom Black Firework OUT"}, {2821,"Ring Black Firework OUT"}, {2822,"Boom Blue Firework OUT"}, {2823,"Big Boom Blue Firework OUT"}, {2824,"Shockwave Blue Firework OUT"}, {2825,"Spiral Blue Firework OUT"}, {2826,"Sparkle Blue Firework OUT"}, {2827,"Blossom Blue Firework OUT"}, {2828,"Ring Blue Firework OUT"}, {2829,"Boom Green Firework OUT"}, {2830,"Big Boom Green Firework OUT"}, {2831,"Shockwave Green Firework OUT"}, {2832,"Spiral Green Firework OUT"}, {2833,"Sparkle Green Firework OUT"}, {2834,"Blossom Green Firework OUT"}, {2835,"Ring Green Firework OUT"}, {2836,"Boom Orange Firework OUT"}, {2837,"Big Boom Orange Firework OUT"}, {2838,"Shockwave Orange Firework OUT"}, {2839,"Spiral Orange Firework OUT"}, {2840,"Sparkle Orange Firework OUT"}, {2841,"Blossom Orange Firework OUT"}, {2842,"Ring Orange Firework OUT"}, {2843,"Boom Purple Firework OUT"}, {2844,"Big Boom Purple Firework OUT"}, {2845,"Shockwave Purple Firework OUT"}, {2846,"Spiral Purple Firework OUT"}, {2847,"Sparkle Purple Firework OUT"}, {2848,"Blossom Purple Firework OUT"}, {2849,"Ring Purple Firework OUT"}, {2850,"Boom Red Firework OUT"}, {2851,"Big Boom Red Firework OUT"}, {2852,"Shockwave Red Firework OUT"}, {2853,"Spiral Red Firework OUT"}, {2854,"Sparkle Red Firework OUT"}, {2855,"Blossom Red Firework OUT"}, {2856,"Ring Red Firework OUT"}, {2857,"Boom White Firework OUT"}, {2858,"Big Boom White Firework OUT"}, {2859,"Shockwave White Firework OUT"}, {2860,"Spiral White Firework OUT"}, {2861,"Sparkle White Firework OUT"}, {2862,"Blossom White Firework OUT"}, {2863,"Ring White Firework OUT"}, {2864,"Boom Yellow Firework OUT"}, {2865,"Big Boom Yellow Firework OUT"}, {2866,"Shockwave Yellow Firework OUT"}, {2867,"Spiral Yellow Firework OUT"}, {2868,"Sparkle Yellow Firework OUT"}, {2869,"Blossom Yellow Firework OUT"}, {2870,"Ring Yellow Firework OUT"}, {2871,"Boom Black Firework UP"}, {2872,"Big Boom Black Firework UP"}, {2873,"Shockwave Black Firework UP"}, {2874,"Spiral Black Firework UP"}, {2875,"Sparkle Black Firework UP"}, {2876,"Blossom Black Firework UP"}, {2877,"Ring Black Firework UP"}, {2878,"Boom Blue Firework UP"}, {2879,"Big Boom Blue Firework UP"}, {2880,"Shockwave Blue Firework UP"}, {2881,"Spiral Blue Firework UP"}, {2882,"Sparkle Blue Firework UP"}, {2883,"Blossom Blue Firework UP"}, {2884,"Ring Blue Firework UP"}, {2885,"Boom Green Firework UP"}, {2886,"Big Boom Green Firework UP"}, {2887,"Shockwave Green Firework UP"}, {2888,"Spiral Green Firework UP"}, {2889,"Sparkle Green Firework UP"}, {2890,"Blossom Green Firework UP"}, {2891,"Ring Green Firework UP"}, {2892,"Boom Orange Firework UP"}, {2893,"Big Boom Orange Firework UP"}, {2894,"Shockwave Orange Firework UP"}, {2895,"Spiral Orange Firework UP"}, {2896,"Sparkle Orange Firework UP"}, {2897,"Blossom Orange Firework UP"}, {2898,"Ring Orange Firework UP"}, {2899,"Boom Purple Firework UP"}, {2900,"Big Boom Purple Firework UP"}, {2901,"Shockwave Purple Firework UP"}, {2902,"Spiral Purple Firework UP"}, {2903,"Sparkle Purple Firework UP"}, {2904,"Blossom Purple Firework UP"}, {2905,"Ring Purple Firework UP"}, {2906,"Boom Red Firework UP"}, {2907,"Big Boom Red Firework UP"}, {2908,"Shockwave Red Firework UP"}, {2909,"Spiral Red Firework UP"}, {2910,"Sparkle Red Firework UP"}, {2911,"Blossom Red Firework UP"}, {2912,"Ring Red Firework UP"}, {2913,"Boom White Firework UP"}, {2914,"Big Boom White Firework UP"}, {2915,"Shockwave White Firework UP"}, {2916,"Spiral White Firework UP"}, {2917,"Sparkle White Firework UP"}, {2918,"Blossom White Firework UP"}, {2919,"Ring White Firework UP"}, {2920,"Boom Yellow Firework UP"}, {2921,"Big Boom Yellow Firework UP"}, {2922,"Shockwave Yellow Firework UP"}, {2923,"Spiral Yellow Firework UP"}, {2924,"Sparkle Yellow Firework UP"}, {2925,"Blossom Yellow Firework UP"}, {2926,"Ring Yellow Firework UP"}, {2927,"Old School Fireworks"}, {2928,"Tusker Hide"}, {2929,"Tusker Might"}, {2930,"Tusker Skin"}, {2931,"Recall Aphus Lassel"}, {2932,"Tusker Leap"}, {2933,"Tusker Sprint"}, {2934,"Tusker Fists"}, {2935,"Trial of the Tusker Hero"}, {2936,"Entrance to Tusker Island"}, {2937,"Moderate Impregnability"}, {2938,"Moderate Invulnerability"}, {2939,"Entering the Temple"}, {2940,"Entering the Temple"}, {2941,"Ulgrim's Recall"}, {2942,"Free Ride to the Abandoned Mine"}, {2943,"Recall to the Singularity Caul"}, {2944,"Storage Warehouse"}, {2945,"Storage Warehouse"}, {2946,"Moderate Creature Magic Aptitude"}, {2947,"Nullify All Magic Other"}, {2948,"Hieromancer's Great Ward"}, {2949,"Lightbringer's Way"}, {2950,"Maiden's Kiss"}, {2951,"Gates of Knorr"}, {2952,"Courtyard of Knorr"}, {2953,"Interior Gates of Knorr"}, {2954,"Barracks Conveyance"}, {2955,"Forge Conveyance"}, {2956,"Research Chambers Conveyance"}, {2957,"Seat of Knorr"}, {2958,"Blessing of the Priestess"}, {2959,"Mark of the Priestess"}, {2960,"Greater Bludgeoning Durance"}, {2961,"Greater Piercing Durance"}, {2962,"Greater Slashing Durance"}, {2963,"Aura of Hunter's Cunning"}, {2964,"Aura of Hunter's Mark"}, {2965,"Aura of Murderous Intent"}, {2966,"Aura of Murderous Thirst"}, {2967,"Aura of The Speedy Hunter"}, {2968,"Vision of the Hunter"}, {2969,"Mother's Blessing"}, {2970,"Hunter's Lash"}, {2971,"Bullseye"}, {2972,"Oswald's Room"}, {2973,"Access to the Secret Lair"}, {2974,"Vagabond Passed"}, {2975,"Moderate Item Enchantment Aptitude"}, {2976,"Acid Spray"}, {2977,"Portal spell to a hidden place"}, {2978,"Nullify All Magic Other"}, {2979,"Destiny's Wind"}, {2980,"Endless Vigor"}, {2981,"Fellowship Heal I"}, {2982,"Fellowship Alchemy Mastery I"}, {2983,"Fellowship Evaporate Life Magic Self"}, {2984,"Lyceum of Kivik Lir"}, {2985,"Ardence"}, {2986,"Vim"}, {2987,"Volition"}, {2988,"Beaten into Submission"}, {2989,"Portal to the Bandit Hideout."}, {2990,"Knocked Out"}, {2991,"Winter's Kiss"}, {2992,"Depletion"}, {2993,"Grace of the Unicorn"}, {2994,"Plague"}, {2995,"Power of the Dragon"}, {2996,"Scourge"}, {2997,"Splendor of the Firebird"}, {2998,"Wrath of the Puppeteer"}, {2999,"Endurance of the Abyss"}, {3000,"Ire of the Dark Prince"}, {3001,"Puppet String"}, {3002,"Will of the Quiddity"}, {3003,"Dark Wave"}, {3004,"Puppet Strings"}, {3005,"Dispersion"}, {3006,"Foresight"}, {3007,"Uncanny Dodge"}, {3008,"Finesse"}, {3009,"Thew"}, {3010,"Zeal"}, {3011,"Endless Sight"}, {3012,"Far Sight"}, {3013,"Fruit of the Oasis"}, {3014,"Water of the Oasis"}, {3015,"Shade of the Oasis"}, {3016,"Raptor's Sight"}, {3017,"Greater Battle Dungeon Sending from Candeth Keep"}, {3018,"Greater Battle Dungeon Sending from Fort Tethana"}, {3019,"Greater Battle Dungeon Sending from Nanto"}, {3020,"Greater Battle Dungeon Sending from Plateau"}, {3021,"Greater Battle Dungeon Sending from Qalabar"}, {3022,"Greater Battle Dungeon Sending from Tou-Tou"}, {3023,"Greater Battle Dungeon Sending from Xarabydun"}, {3024,"Greater Battle Dungeon Sending from Yaraq"}, {3025,"Shriek"}, {3026,"Lesser Battle Dungeon Sending from Candeth Keep"}, {3027,"Lesser Battle Dungeon Sending from Fort Tethana"}, {3028,"Lesser Battle Dungeon Sending from Nanto"}, {3029,"Lesser Battle Dungeon Sending from Plateau"}, {3030,"Lesser Battle Dungeon Sending from Qalabar"}, {3031,"Lesser Battle Dungeon Sending from Tou-Tou"}, {3032,"Lesser Battle Dungeon Sending from Xarabydun"}, {3033,"Lesser Battle Dungeon Sending from Yaraq"}, {3034,"Benediction of Immortality"}, {3035,"Closing of the Great Divide"}, {3036,"Cold Grip of the Grave"}, {3037,"Death's Call"}, {3038,"Death's Embrace"}, {3039,"Death's Feast"}, {3040,"Places Death's Kiss upon you."}, {3041,"Essence Dissolution"}, {3042,"Grip of Death"}, {3043,"Kiss of the Grave"}, {3044,"Lesser Benediction of Immortality"}, {3045,"Lesser Closing of the Great Divide"}, {3046,"Lesser Mists of Bur"}, {3047,"Matron's Barb"}, {3048,"Minor Benediction of Immortality"}, {3049,"Minor Closing of the Great Divide"}, {3050,"Minor Mists of Bur"}, {3051,"Mire Foot"}, {3052,"Mists of Bur"}, {3053,"Paralyzing Touch"}, {3054,"Soul Dissolution"}, {3055,"Asphyxiation"}, {3056,"Death's Vice"}, {3057,"Enervation"}, {3058,"Asphyiaxtion"}, {3059,"Enervation"}, {3060,"Poison Blood"}, {3061,"Taint Mana"}, {3062,"Asphyxiation"}, {3063,"Enervation"}, {3064,"Poison Blood"}, {3065,"Taint Mana"}, {3066,"Lesser Ward of Rebirth"}, {3067,"Matron's Curse"}, {3068,"Minor Ward of Rebirth"}, {3069,"Poison Blood"}, {3070,"Taint Mana"}, {3071,"Ward of Rebirth"}, {3072,"Hall of the Temple Guardians"}, {3073,"Matron's Outer Chamber"}, {3074,"Bruised Flesh"}, {3075,"Flesh of Cloth"}, {3076,"Exposed Flesh"}, {3077,"Flesh of Flint"}, {3078,"Weaken Flesh"}, {3079,"Thin Skin"}, {3080,"Bruised Flesh"}, {3081,"Flesh of Cloth"}, {3082,"Exposed Flesh"}, {3083,"Flesh of Flint"}, {3084,"Weaken Flesh"}, {3085,"Bruised Flesh"}, {3086,"Flesh of Cloth"}, {3087,"Exposed Flesh"}, {3088,"Flesh of Flint"}, {3089,"Weaken Flesh"}, {3090,"Thin Skin"}, {3091,"Thin Skin"}, {3092,"Lesser Skin of the Fiazhat"}, {3093,"Minor Skin of the Fiazhat"}, {3094,"Skin of the Fiazhat"}, {3095,"Crypt of Jexki Ki"}, {3096,"Crypt of Ibrexi Jekti"}, {3097,"Hall of the Guardians"}, {3098,"Hall of the Greater Guardians"}, {3099,"Hall of the Lesser Guardians"}, {3100,"Antechamber of Ixir Zi's Temple"}, {3101,"Crypt of Ixir Zi"}, {3102,"Kivik Lir's Temple"}, {3103,"Crypt of Kixkti Xri"}, {3104,"Hall of the Arbiter"}, {3105,"Hall of the Arbiter"}, {3106,"Hall of the Arbiter"}, {3107,"Flay Soul"}, {3108,"Flay Soul"}, {3109,"Liquefy Flesh"}, {3110,"Sear Flesh"}, {3111,"Soul Hammer"}, {3112,"Soul Spike"}, {3113,"Flay Soul"}, {3114,"Liquefy Flesh"}, {3115,"Sear Flesh"}, {3116,"Soul Hammer"}, {3117,"Soul Spike"}, {3118,"Liquefy Flesh"}, {3119,"Sear Flesh"}, {3120,"Soul Hammer"}, {3121,"Soul Spike"}, {3122,"Sacrificial Edge"}, {3123,"Sacrificial Edges"}, {3124,"Blight Mana"}, {3125,"EnervateBeing"}, {3126,"Poison Health"}, {3127,"Fell Wind"}, {3128,"Infected Blood"}, {3129,"Infirmed Mana"}, {3130,"Halls of Liazk Itzi"}, {3131,"Halls of Liazk Itzi"}, {3132,"Halls of Liazk Itzi"}, {3133,"Halls of Liazk Itzi"}, {3134,"Halls of Liazk Itzi"}, {3135,"Halls of Liazk Itzi"}, {3136,"Halls of Liazk Itzi"}, {3137,"Halls of Liazk Itzi"}, {3138,"Antechamber of Liazk Itzi"}, {3139,"Liazk Itzi's Crypt"}, {3140,"Lair of Liazk Itzi"}, {3141,"Lair of Liazk Itzi"}, {3142,"Lair of Liazk Itzi"}, {3143,"Lair of Liazk Itzi"}, {3144,"Liazk Itzi Guardians"}, {3145,"Liazk Itzi Guardians"}, {3146,"Liazk Itzi Guardians"}, {3147,"Liazk Itzi Guardians"}, {3148,"Liazk Itzi's Offering Room"}, {3149,"Liazk Itzi's Offering Room"}, {3150,"Liazk Itzi's Offering Room"}, {3151,"Liazk Itzi's Offering Room"}, {3152,"Inferior Scythe Aegis"}, {3153,"Lesser Scythe Aegis"}, {3154,"Scythe Aegis"}, {3155,"Lesser Alacrity of the Conclave"}, {3156,"Alacrity of the Conclave"}, {3157,"Greater Alacrity of the Conclave"}, {3158,"Superior Alacrity of the Conclave"}, {3159,"Lesser Vivify the Conclave"}, {3160,"Vivify the Conclave"}, {3161,"Greater Vivify the Conclave"}, {3162,"Superior Vivify the Conclave"}, {3163,"Lesser Acumen of the Conclave"}, {3164,"Acumen of the Conclave"}, {3165,"Greater Acumen of the Conclave"}, {3166,"Superior Acumen of the Conclave"}, {3167,"Lesser Speed the Conclave"}, {3168,"Speed the Conclave"}, {3169,"Greater Speed the Conclave"}, {3170,"Superior Speed the Conclave"}, {3171,"Lesser Volition of the Conclave"}, {3172,"Volition of the Conclave"}, {3173,"Greater Volition of the Conclave"}, {3174,"Superior Volition of the Conclave"}, {3175,"Lesser Empowering the Conclave"}, {3176,"Empowering the Conclave"}, {3177,"Greater Empowering the Conclave"}, {3178,"Superior Empowering the Conclave"}, {3179,"Eradicate All Magic Other"}, {3180,"Eradicate All Magic Self"}, {3181,"Nullify All Magic Other"}, {3182,"Nullify All Magic Self"}, {3183,"Nullify All Magic Self"}, {3184,"Eradicate Creature Magic Other"}, {3185,"Eradicate Creature Magic Self"}, {3186,"Nullify Creature Magic Other"}, {3187,"Nullify Creature Magic Self"}, {3188,"Nullify Creature Magic Other"}, {3189,"Nullify Creature Magic Self"}, {3190,"Eradicate Item Magic"}, {3191,"Nullify Item Magic"}, {3192,"Nullify Item Magic"}, {3193,"Eradicate Life Magic Other"}, {3194,"Eradicate Life Magic Self"}, {3195,"Nullify Life Magic Other"}, {3196,"Nullify Life Magic Self"}, {3197,"Nullify Life Magic Other"}, {3198,"Nullify Life Magic Self"}, {3199,"Minor Hermetic Link"}, {3200,"Major Hermetic Link"}, {3201,"Feeble Hermetic Link"}, {3202,"Moderate Hermetic Link"}, {3203,"Eradicate All Magic Other"}, {3204,"Blazing Heart"}, {3205,"Good Eating"}, {3206,"Enliven"}, {3207,"Ore Fire"}, {3208,"Innervate"}, {3209,"Refreshment"}, {3210,"Agitate"}, {3211,"Annoyance"}, {3212,"Guilt Trip"}, {3213,"Heart Ache"}, {3214,"Sorrow"}, {3215,"Underfoot"}, {3216,"Transport to the Forbidden Catacombs"}, {3217,"Cascade"}, {3218,"Greater Cascade"}, {3219,"Lesser Cascade"}, {3220,"Cascade"}, {3221,"Greater Cascade"}, {3222,"Lesser Cascade"}, {3223,"Cascade"}, {3224,"Greater Cascade"}, {3225,"Lesser Cascade"}, {3226,"Cascade"}, {3227,"Greater Cascade"}, {3228,"Lesser Cascade"}, {3229,"Cascade"}, {3230,"Greater Cascade"}, {3231,"Lesser Cascade"}, {3232,"Cascade"}, {3233,"Greater Cascade"}, {3234,"Lesser Cascade"}, {3235,"Dark Power"}, {3236,"Restorative Draught"}, {3237,"Fanaticism"}, {3238,"Portal to Nanner Island"}, {3239,"Insight of the Khe"}, {3240,"Wisdom of the Khe"}, {3241,"Flame Burst"}, {3242,"Weave of Chorizite"}, {3243,"Consecration"}, {3244,"Divine Manipulation"}, {3245,"Sacrosanct Touch"}, {3246,"Adja's Benefaction"}, {3247,"Adja's Favor"}, {3248,"Adja's Grace"}, {3249,"Ghostly Chorus"}, {3250,"Major Spirit Thirst"}, {3251,"Minor Spirit Thirst"}, {3252,"Spirit Thirst"}, {3253,"Aura of Spirit Drinker Self I"}, {3254,"Aura of Spirit Drinker Self II"}, {3255,"Aura of Spirit Drinker Self III"}, {3256,"Aura of Spirit Drinker Self IV"}, {3257,"Aura of Spirit Drinker Self V"}, {3258,"Aura of Spirit Drinker Self VI"}, {3259,"Aura of Infected Spirit Caress"}, {3260,"Spirit Loather I"}, {3261,"Spirit Loather II"}, {3262,"Spirit Loather III"}, {3263,"Spirit Loather IV"}, {3264,"Spirit Loather V"}, {3265,"Spirit Loather VI"}, {3266,"Spirit Pacification"}, {3267,"Bit Between Teeth"}, {3268,"Biting Bonds"}, {3269,"Under The Lash"}, {3270,"Hezhit's Safety"}, {3271,"Hezhit's Safety"}, {3272,"Hezhit's Safety"}, {3273,"Prison"}, {3274,"Prison"}, {3275,"Prison"}, {3276,"Prison"}, {3277,"Prison"}, {3278,"Prison"}, {3279,"Entrance to Hizk Ri's Temple"}, {3280,"Return to the Corridor"}, {3281,"Hizk Ri's Test"}, {3282,"Hizk Ri's Test"}, {3283,"Hizk Ri's Test"}, {3284,"Consort Hezhit"}, {3285,"Attendant Jrvik"}, {3286,"Well of Tears"}, {3287,"Well of Tears"}, {3288,"Well of Tears"}, {3289,"Patriarch Zixki"}, {3290,"Jrvik's Safety"}, {3291,"Jrvik's Safety"}, {3292,"Jrvik's Safety"}, {3293,"Prison"}, {3294,"Prison"}, {3295,"Prison"}, {3296,"Prison"}, {3297,"Prison"}, {3298,"Prison"}, {3299,"Zixk's Safety"}, {3300,"Zixk's Safety"}, {3301,"Zixk's Safety"}, {3302,"Prison"}, {3303,"Prison"}, {3304,"Prison"}, {3305,"Prison"}, {3306,"Prison"}, {3307,"Prison"}, {3308,"Flange Aegis"}, {3309,"Inferior Flange Aegis"}, {3310,"Inferior Lance Aegis"}, {3311,"Lance Aegis"}, {3312,"Lesser Flange Aegis"}, {3313,"Lesser Lance Aegis"}, {3314,"Chained to the Wall"}, {3315,"The Sewer"}, {3316,"The Sewer"}, {3317,"The Sewer"}, {3318,"Hizk Ri's Crypt"}, {3319,"Portal to Izji Qo's Temple"}, {3320,"Lesser Corrosive Ward"}, {3321,"Corrosive Ward"}, {3322,"Greater Corrosive Ward"}, {3323,"Superior Corrosive Ward"}, {3324,"Lesser Scythe Ward"}, {3325,"Scythe Ward"}, {3326,"Greater Scythe Ward"}, {3327,"Superior Scythe Ward"}, {3328,"Lesser Flange Ward"}, {3329,"Flange Ward"}, {3330,"Greater Flange Ward"}, {3331,"Superior Flange Ward"}, {3332,"Lesser Frore Ward"}, {3333,"Frore Ward"}, {3334,"Greater Frore Ward"}, {3335,"Superior Frore Ward"}, {3336,"Lesser Inferno Ward"}, {3337,"Inferno Ward"}, {3338,"Greater Inferno Ward"}, {3339,"Superior Inferno Ward"}, {3340,"Lesser Voltaic Ward"}, {3341,"Voltaic Ward"}, {3342,"Greater Voltaic Ward"}, {3343,"Superior Voltaic Ward"}, {3344,"Lesser Lance Ward"}, {3345,"Lance Ward"}, {3346,"Greater Lance Ward"}, {3347,"Superior Lance Ward"}, {3348,"Lesser Warden of the Clutch"}, {3349,"Inferior Warden of the Clutch"}, {3350,"Warden of the Clutch"}, {3351,"Potent Warden of the Clutch"}, {3352,"Lesser Guardian of the Clutch"}, {3353,"Inferior Guardian of the Clutch"}, {3354,"Guardian of the Clutch"}, {3355,"Potent Guardian of the Clutch"}, {3356,"Lesser Sanctifier of the Clutch"}, {3357,"Inferior Sanctifier of the Clutch"}, {3358,"Sanctifier of the Clutch"}, {3359,"Potent Sanctifier of the Clutch"}, {3360,"Entrance to the Burun Shrine"}, {3361,"The Art of Destruction"}, {3362,"Blessing of the Horn"}, {3363,"Blessing of the Scale"}, {3364,"Blessing of the Wing"}, {3365,"Gift of Enhancement"}, {3366,"The Heart's Touch"}, {3367,"Leaping Legs"}, {3368,"Mage's Understanding"}, {3369,"On the Run"}, {3370,"Power of Enchantment"}, {3371,"Greater Life Giver"}, {3372,"Debilitating Spore"}, {3373,"Diseased Air"}, {3374,"Kivik Lir's Scorn"}, {3375,"Fungal Bloom"}, {3376,"Lesser Vision Beyond the Grave"}, {3377,"Minor Vision Beyond the Grave"}, {3378,"Vision Beyond the Grave"}, {3379,"Vitae"}, {3380,"Vitae"}, {3381,"Debilitating Spore"}, {3382,"Diseased Air"}, {3383,"Fungal Bloom"}, {3384,"Lesser Conjurant Chant"}, {3385,"Conjurant Chant"}, {3386,"Greater Conjurant Chant"}, {3387,"Superior Conjurant Chant"}, {3388,"Lesser Artificant Chant"}, {3389,"Artificant Chant"}, {3390,"Greater Artificant Chant"}, {3391,"Superior Artificant Chant"}, {3392,"Lesser Vitaeic Chant"}, {3393,"Vitaeic Chant"}, {3394,"Greater Vitaeic Chant"}, {3395,"Superior Vitaeic Chant"}, {3396,"Lesser Conveyic Chant"}, {3397,"Conveyic Chant"}, {3398,"Greater Conveyic Chant"}, {3399,"Superior Conveyic Chant"}, {3400,"Lesser Hieromantic Chant"}, {3401,"Hieromantic Chant"}, {3402,"Greater Hieromantic Chant"}, {3403,"Superior Hieromantic Chant"}, {3404,"Evil Thirst"}, {3405,"Gift of the Fiazhat"}, {3406,"Kivik Lir's Boon"}, {3407,"Lesser Evil Thirst"}, {3408,"Lesser Gift of the Fiazhat"}, {3409,"Minor Evil Thirst"}, {3410,"Minor Gift of the Fiazhat"}, {3411,"Portal spell to a Hidden Chamber"}, {3412,"Halls of Kivik Lir"}, {3413,"Lesser Arena of Kivik Lir"}, {3414,"Arena of Kivik Lir"}, {3415,"Greater Arena of Kivik Lir"}, {3416,"Gallery of Kivik Lir"}, {3417,"Gallery of Kivik Lir"}, {3418,"Gallery of Kivik Lir"}, {3419,"Crypt of Kivik Lir"}, {3420,"Lesser Haven of Kivik Lir"}, {3421,"Haven of Kivik Lir"}, {3422,"Greater Haven of Kivik Lir"}, {3423,"Trials of Kivik Lir"}, {3424,"Triumph Against the Trials"}, {3425,"Lyceum of Kivik Lir"}, {3426,"Greater Withering"}, {3427,"Lesser Withering"}, {3428,"Withering"}, {3429,"Kivik Lir's Venom"}, {3430,"Inferior Scourge Aegis"}, {3431,"Lesser Scourge Aegis"}, {3432,"Scourge Aegis"}, {3433,"Decay"}, {3434,"Eyes Beyond the Mist"}, {3435,"Greater Mucor Blight"}, {3436,"Lesser Eyes Beyond the Mist"}, {3437,"Lesser Mucor Blight"}, {3438,"Minor Eyes Beyond the Mist"}, {3439,"Mucor Blight"}, {3440,"Health of the Lugian"}, {3441,"Insight of the Lugian"}, {3442,"Stamina of the Lugian"}, {3443,"Blight of the Swamp"}, {3444,"Justice of The Sleeping One"}, {3445,"The Sleeping One's Purge"}, {3446,"Wrath of the Swamp"}, {3447,"Asphyxiating Spore Cloud"}, {3448,"Mass Blood Affliction"}, {3449,"Mass Blood Disease"}, {3450,"Cloud of Mold Spores"}, {3451,"Concussive Belch"}, {3452,"Concussive Wail"}, {3453,"Feelun Blight"}, {3454,"Wrath of the Feelun"}, {3455,"Koruu Cloud"}, {3456,"Koruu's Wrath"}, {3457,"Mana Bolt"}, {3458,"Mana Purge"}, {3459,"Mucor Cloud"}, {3460,"Dissolving Vortex"}, {3461,"Batter Flesh"}, {3462,"Canker Flesh"}, {3463,"Char Flesh"}, {3464,"Numb Flesh"}, {3465,"Blood Affliction"}, {3466,"Blood Disease"}, {3467,"Choking Spores"}, {3468,"Mold Spores"}, {3469,"Parasitic Affliction"}, {3470,"Lesser Endless Well"}, {3471,"The Endless Well"}, {3472,"Greater Endless Well"}, {3473,"Superior Endless Well"}, {3474,"Lesser Soothing Wind"}, {3475,"The Soothing Wind"}, {3476,"Greater Soothing Wind"}, {3477,"Superior Soothing Wind"}, {3478,"Lesser Golden Wind"}, {3479,"The Golden Wind"}, {3480,"Greater Golden Wind"}, {3481,"Superior Golden Wind"}, {3482,"Izji Qo's Antechamber"}, {3483,"Izji Qo's Defenders"}, {3484,"Izji Qo's Defenders"}, {3485,"Izji Qo's Defenders"}, {3486,"Into the Receiving Chamber"}, {3487,"Into the Receiving Chamber"}, {3488,"Into the Receiving Chamber"}, {3489,"Into the Receiving Chamber"}, {3490,"Into the Receiving Chamber"}, {3491,"Into the Receiving Chamber"}, {3492,"Into the Receiving Chamber"}, {3493,"Into the Receiving Chamber"}, {3494,"Izji Qo's Crypt"}, {3495,"Izji Qo's Test"}, {3496,"Inzji Qo's Test"}, {3497,"Izji Qo's Test"}, {3498,"Disintegrated"}, {3499,"Arcanum Salvaging Self I"}, {3500,"Arcanum Salvaging Self II"}, {3501,"Arcanum Salvaging Self III"}, {3502,"Arcanum Salvaging Self IV"}, {3503,"Arcanum Salvaging Self V"}, {3504,"Arcanum Salvaging Self VI"}, {3505,"Arcanum Salvaging VII"}, {3506,"Arcanum Enlightenment I"}, {3507,"Arcanum Enlightenment II"}, {3508,"Arcanum Enlightenment III"}, {3509,"Arcanum Enlightenment IV"}, {3510,"Arcanum Enlightenment V"}, {3511,"Arcanum Enlightenment VI"}, {3512,"Arcanum Enlightenment VII"}, {3513,"Nuhmudira's Wisdom I"}, {3514,"Nuhmudira's Wisdom II"}, {3515,"Nuhmudira's Wisdom III"}, {3516,"Nuhmudira's Wisdom IV"}, {3517,"Nuhmudira's Wisdom V"}, {3518,"Nuhmudira's Wisdom VI"}, {3519,"Nuhmudira's Wisdom VII"}, {3520,"Nuhmudira's Enlightenment I"}, {3521,"Nuhmudira's Enlightenment II"}, {3522,"Nuhmudira's Enlightenment III"}, {3523,"Nuhmudira's Enlightenment IV"}, {3524,"Nuhmudira's Enlightenment V"}, {3525,"Nuhmudira Enlightenment VI"}, {3526,"Nuhmudira's Enlightenment"}, {3527,"Intoxication I"}, {3528,"Intoxication II"}, {3529,"Intoxication III"}, {3530,"Ketnan's Eye"}, {3531,"Bobo's Quickening"}, {3532,"Bobo's Focused Blessing"}, {3533,"Brighteyes' Favor"}, {3534,"Free Ride to the K'nath Lair"}, {3535,"Free Ride to Sanamar"}, {3536,"Portal Sending"}, {3537,"Portal Sending"}, {3538,"Portal Sending"}, {3539,"Portal Sending"}, {3540,"Portal Sending"}, {3541,"Portal Sending"}, {3542,"Portal Sending"}, {3543,"Portal Sending"}, {3544,"Portal Sending"}, {3545,"Portal Sending"}, {3546,"Portal Sending"}, {3547,"Portal Sending"}, {3548,"Portal Sending"}, {3549,"Portal Sending"}, {3550,"Portal Sending"}, {3551,"Portal Sending"}, {3552,"Portal Sending"}, {3553,"Portal Sending"}, {3554,"Portal Sending"}, {3555,"Portal Sending"}, {3556,"Portal Sending"}, {3557,"Portal Sending"}, {3558,"Portal Sending"}, {3559,"Portal Sending"}, {3560,"Portal Sending"}, {3561,"Portal Sending"}, {3562,"Portal Sending"}, {3563,"Portal Sending"}, {3564,"Portal Sending"}, {3565,"Portal Sending"}, {3566,"Portal Sending"}, {3567,"Fiun Flee"}, {3568,"Fiun Efficiency"}, {3569,"Mana Boost"}, {3570,"Stamina Boost"}, {3571,"Health Boost"}, {3572,"Inner Brilliance"}, {3573,"Inner Might"}, {3574,"Inner Will"}, {3575,"Perfect Balance"}, {3576,"Perfect Health"}, {3577,"Perfect Speed"}, {3578,"Depths of Liazk Itzi's Temple"}, {3579,"Underpassage of Liazk Itzi's Temple"}, {3580,"Center of Liazk Itzi's Temple"}, {3581,"Secrets of Liazk Itzi's Temple"}, {3582,"Eaten!"}, {3583,"Regurgitated"}, {3584,"Qin Xikit's Receiving Chamber"}, {3585,"Eaten!"}, {3586,"Qin Xikit's Antechamber"}, {3587,"Portal Sending"}, {3588,"Secrets of Qin Xikit's Temple"}, {3589,"Qin Xikit's Tomb"}, {3590,"Regurgitated!"}, {3591,"Access to Xi Ru's Font"}, {3592,"Qin Xikit's Island"}, {3593,"Eaten!"}, {3594,"Underpassage of Hizk Ri's Temple"}, {3595,"Underpassage of Hizk Ri's Temple"}, {3596,"Center of Hizk Ri's Temple"}, {3597,"Secrets of Hizk Ri's Temple"}, {3598,"Regurgitated"}, {3599,"Eaten!"}, {3600,"Depths of Ixir Zi's Temple"}, {3601,"Underpassage of Ixir Zi's Temple"}, {3602,"Center of Ixir Zi's Temple"}, {3603,"Secrets of Ixir Zi's Temple"}, {3604,"Regurgitated"}, {3605,"Portal to Cragstone"}, {3606,"Eaten!"}, {3607,"Depth's of Izji Qo's Temple"}, {3608,"Underpassage of Izji Qo's Temple"}, {3609,"Center of Izji Qo's Temple"}, {3610,"Secrets of Izji Qo's Temple"}, {3611,"Regurgitated"}, {3612,"Eaten!"}, {3613,"Regurgitated"}, {3614,"Underpassage of Kivik Lir's Temple"}, {3615,"Center of Kivik Lir's Temple"}, {3616,"Secrets of Kivik Lir's Temple"}, {3617,"Depths of Kivik Lir's Temple"}, {3618,"Portal to Western Aphus Lassel"}, {3619,"Portal to the Black Death Catacombs"}, {3620,"Portal to Black Spawn Den"}, {3621,"Portal to Black Spawn Den"}, {3622,"Portal to Black Spawn Den"}, {3623,"Portal to Center of the Obsidian Plains"}, {3624,"Portal to Hills Citadel"}, {3625,"Portal to the Kara Wetlands"}, {3626,"Portal to the Marescent Plateau Base"}, {3627,"Portal to Neydisa Castle"}, {3628,"Portal to the Northern Landbridge"}, {3629,"Portal to the Olthoi Horde Nest"}, {3630,"Portal to the Olthoi North"}, {3631,"Portal to the Renegade Fortress"}, {3632,"Portal to Ridge Citadel"}, {3633,"Portal to the Southern Landbridge"}, {3634,"Portal To Valley of Death"}, {3635,"Portal to Wilderness Citadel"}, {3636,"Kern's Boon"}, {3637,"Ranger's Boon"}, {3638,"Ranger's Boon"}, {3639,"Ranger's Boon"}, {3640,"Enchanter's Boon"}, {3641,"Hieromancer's Boon"}, {3642,"Fencer's Boon"}, {3643,"Life Giver's Boon"}, {3644,"Kern's Boon"}, {3645,"Kern's Boon"}, {3646,"Kern's Boon"}, {3647,"Kern's Boon"}, {3648,"Soldier's Boon"}, {3649,"Aerfalle's Embrace"}, {3650,"Aerfalle's Enforcement"}, {3651,"Aerfalle's Gaze"}, {3652,"Aerfalle's Touch"}, {3653,"Acid Blast III"}, {3654,"Acid Volley I"}, {3655,"Acid Volley II"}, {3656,"Blade Blast III"}, {3657,"Blade Blast IV"}, {3658,"Blade Volley I"}, {3659,"Blade Volley II"}, {3660,"Bludgeoning Volley I"}, {3661,"Bludgeoning Volley II"}, {3662,"Flame Blast I"}, {3663,"Flame Volley III"}, {3664,"Flame Volley IV"}, {3665,"Force Blast III"}, {3666,"Force Blast IV"}, {3667,"Force Volley III"}, {3668,"Force Volley IV"}, {3669,"Frost Blast III"}, {3670,"Frost Blast IV"}, {3671,"Frost Volley III"}, {3672,"Frost Volley IV"}, {3673,"Lightning Blast III"}, {3674,"Lightning Blast IV"}, {3675,"Lightning Volley III"}, {3676,"Lightning Volley IV"}, {3677,"Shock Blast III"}, {3678,"Shock Blast IV"}, {3679,"Prodigal Acid Bane"}, {3680,"Prodigal Acid Protection"}, {3681,"Prodigal Alchemy Mastery"}, {3682,"Prodigal Arcane Enlightenment"}, {3683,"Prodigal Armor Expertise"}, {3684,"Prodigal Armor"}, {3685,"Prodigal Light Weapon Mastery"}, {3686,"Prodigal Blade Bane"}, {3687,"Prodigal Blade Protection"}, {3688,"Prodigal Blood Drinker"}, {3689,"Prodigal Bludgeon Bane"}, {3690,"Prodigal Bludgeon Protection"}, {3691,"Prodigal Missile Weapon Mastery"}, {3692,"Prodigal Cold Protection"}, {3693,"Prodigal Cooking Mastery"}, {3694,"Prodigal Coordination"}, {3695,"Prodigal Creature Enchantment Mastery"}, {3696,"Prodigal Missile Weapon Mastery"}, {3697,"Prodigal Finesse Weapon Mastery"}, {3698,"Prodigal Deception Mastery"}, {3699,"Prodigal Defender"}, {3700,"Prodigal Endurance"}, {3701,"Prodigal Fealty"}, {3702,"Prodigal Fire Protection"}, {3703,"Prodigal Flame Bane"}, {3704,"Prodigal Fletching Mastery"}, {3705,"Prodigal Focus"}, {3706,"Prodigal Frost Bane"}, {3707,"Prodigal Healing Mastery"}, {3708,"Prodigal Heart Seeker"}, {3709,"Prodigal Hermetic Link"}, {3710,"Prodigal Impenetrability"}, {3711,"Prodigal Impregnability"}, {3712,"Prodigal Invulnerability"}, {3713,"Prodigal Item Enchantment Mastery"}, {3714,"Prodigal Item Expertise"}, {3715,"Prodigal Jumping Mastery"}, {3716,"Prodigal Leadership Mastery"}, {3717,"Prodigal Life Magic Mastery"}, {3718,"Prodigal Lightning Bane"}, {3719,"Prodigal Lightning Protection"}, {3720,"Prodigal Lockpick Mastery"}, {3721,"Prodigal Light Weapon Mastery"}, {3722,"Prodigal Magic Item Expertise"}, {3723,"Prodigal Magic Resistance"}, {3724,"Prodigal Mana Conversion Mastery"}, {3725,"Prodigal Mana Renewal"}, {3726,"Prodigal Monster Attunement"}, {3727,"Prodigal Person Attunement"}, {3728,"Prodigal Piercing Bane"}, {3729,"Prodigal Piercing Protection"}, {3730,"Prodigal Quickness"}, {3731,"Prodigal Regeneration"}, {3732,"Prodigal Rejuvenation"}, {3733,"Prodigal Willpower"}, {3734,"Prodigal Light Weapon Mastery"}, {3735,"Prodigal Spirit Drinker"}, {3736,"Prodigal Sprint"}, {3737,"Prodigal Light Weapon Mastery"}, {3738,"Prodigal Strength"}, {3739,"Prodigal Swift Killer"}, {3740,"Prodigal Heavy Weapon Mastery"}, {3741,"Prodigal Missile Weapon Mastery"}, {3742,"Prodigal Light Weapon Mastery"}, {3743,"Prodigal War Magic Mastery"}, {3744,"Prodigal Weapon Expertise"}, {3745,"Inferior Inferno Aegis"}, {3746,"Inferno Aegis"}, {3747,"Lesser Inferno Aegis"}, {3748,"Master Salvager's Greater Boon"}, {3749,"Master Alchemist's Boon"}, {3750,"Master Alchemist's Greater Boon"}, {3751,"Master Chef's Boon"}, {3752,"Master Chef's Greater Boon"}, {3753,"Fletching Master's Boon"}, {3754,"Fletching Master's Greater Boon"}, {3755,"Master Lockpicker's Boon"}, {3756,"Master Lockpicker's Greater Boon"}, {3757,"Master Salvager's Boon"}, {3758,"Inky Armor"}, {3759,"Mana Giver"}, {3760,"Culinary Ecstasy"}, {3761,"Fiun Resistance"}, {3762,"Defiled Temple Portal Sending"}, {3763,"Balloon Ride"}, {3764,"Summons a portal to the Banderling Shrine"}, {3765,"Entry to the Mausoleum of Bitterness"}, {3766,"Entry to the Mausoleum of Bitterness"}, {3767,"Entry to the Mausoleum of Bitterness"}, {3768,"Bitter Punishment"}, {3769,"Entry to the Mausoleum of Anger"}, {3770,"Entry to the Mausoleum of Anger"}, {3771,"Entry to the Mausoleum of Anger"}, {3772,"Entry to the Mausoleum of Anger"}, {3773,"Entry to the Mausoleum of Anger"}, {3774,"Entry to the Mausoleum of Anger"}, {3775,"Angry Punishment"}, {3776,"Entry to the Mausoleum of Cruelty"}, {3777,"Entry to the Mausoleum of Cruelty"}, {3778,"Entry to the Mausoleum of Cruelty"}, {3779,"Entry to the Mausoleum of Cruelty"}, {3780,"Entry to the Mausoleum of Cruelty"}, {3781,"Entry to the Mausoleum of Cruelty"}, {3782,"Cruel Punishment"}, {3783,"Entry to the Accursed Mausoleum of Bitterness"}, {3784,"Entry to the Accursed Mausoleum of Bitterness"}, {3785,"Entry to the Accursed Mausoleum of Bitterness"}, {3786,"Entry to the Accursed Mausoleum of Bitterness"}, {3787,"Entry to the Accursed Mausoleum of Bitterness"}, {3788,"Entry to the Accursed Mausoleum of Bitterness"}, {3789,"Slaughter Punishment"}, {3790,"Entry to the Unholy Mausoleum of Bitterness"}, {3791,"Entry to the Unholy Mausoleum of Bitterness"}, {3792,"Entry to the Unholy Mausoleum of Bitterness"}, {3793,"Entry to the Unholy Mausoleum of Bitterness"}, {3794,"Entry to the Unholy Mausoleum of Bitterness"}, {3795,"Entry to the Unholy Mausoleum of Bitterness"}, {3796,"Entry to the Mausoleum of Bitterness"}, {3797,"Entry to the Mausoleum of Bitterness"}, {3798,"Entry to the Mausoleum of Bitterness"}, {3799,"Black Marrow Bliss"}, {3800,"Burning Spirit"}, {3801,"Shadow Touch"}, {3802,"Shadow Reek"}, {3803,"Shadow Shot"}, {3804,"Shadow Shot"}, {3805,"Acid Ring"}, {3806,"Flame Ring"}, {3807,"Force Ring"}, {3808,"Lightning Ring"}, {3809,"Minor Salvaging Aptitude"}, {3810,"Asheron s Benediction"}, {3811,"Blackmoor s Favor"}, {3812,"Tursh's Lair"}, {3813,"Free Ride to Shoushi"}, {3814,"Free Ride to Yaraq"}, {3815,"Free Ride to Holtburg"}, {3816,"Marksman's Ken"}, {3817,"Hunter's Ward"}, {3818,"Curse of Raven Fury"}, {3819,"Conscript's Might"}, {3820,"Conscript's Ward"}, {3821,"Augur's Will"}, {3822,"Augur's Glare"}, {3823,"Augur's Ward"}, {3824,"Marksman's Ken"}, {3825,"Marksman's Ken"}, {3826,"a powerful force"}, {3827,"Lunnum's Embrace"}, {3828,"Rage of Grael"}, {3829,"Blessing of the Sundew"}, {3830,"Blessing of the Fly Trap"}, {3831,"Blessing of the Pitcher Plant"}, {3832,"Master's Voice"}, {3833,"Minor Salvaging Aptitude"}, {3834,"Major Salvaging Aptitude"}, {3835,"Leviathan's Curse"}, {3836,"Breath of the Deep"}, {3837,"Water Island Access"}, {3838,"Abandoned Mines Portal Sending"}, {3839,"Brilliant Access"}, {3840,"Dazzling Access"}, {3841,"Devastated Access"}, {3842,"Northeast Coast Portal Sending"}, {3843,"Fire Island Access"}, {3844,"Gatekeeper Access"}, {3845,"Radiant Access"}, {3846,"Ruined Access"}, {3847,"Cataracts of Xik Minru"}, {3848,"Combat Medication"}, {3849,"Night Runner"}, {3850,"Selflessness"}, {3851,"Corrupted Essence"}, {3852,"Ravenous Armor"}, {3853,"Ardent Defense"}, {3854,"True Loyalty"}, {3855,"Flight of Bats"}, {3856,"Ulgrim's Recall"}, {3857,"Pumpkin Rain"}, {3858,"Pumpkin Ring"}, {3859,"Pumpkin Wall"}, {3860,"Sweet Speed"}, {3861,"Taste for Blood"}, {3862,"Duke Raoul's Pride"}, {3863,"Hunter's Hardiness"}, {3864,"Zongo's Fist"}, {3865,"Glenden Wood Recall"}, {3866,"Glacial Speed"}, {3867,"Embrace of the Chill Shadow"}, {3868,"Dardante's Keep Portal Sending"}, {3869,"Invocation of the Black Book"}, {3870,"Syphon Creature Essence"}, {3871,"Syphon Item Essence"}, {3872,"Syphon Life Essence"}, {3873,"Essence's Command"}, {3874,"Death's Aura"}, {3875,"Acidic Curse"}, {3876,"Curse of the Blades"}, {3877,"Corrosive Strike"}, {3878,"Incendiary Strike"}, {3879,"Glacial Strike"}, {3880,"Galvanic Strike"}, {3881,"Corrosive Ring"}, {3882,"Incendiary Ring"}, {3883,"Pyroclastic Explosion"}, {3884,"Glacial Ring"}, {3885,"Galvanic Ring"}, {3886,"Magic Disarmament"}, {3887,"Entering the Hatch"}, {3888,"Passage to the Rare Chambers"}, {3889,"Inner Burial Chamber Portal Sending"}, {3890,"Will of the People"}, {3891,"Honor of the Bull"}, {3892,"Summon Flame Seekers"}, {3893,"Summon Burning Haze"}, {3894,"Dark Persistence"}, {3895,"Dark Reflexes"}, {3896,"Dark Equilibrium"}, {3897,"Dark Purpose"}, {3898,"Pooky's Recall 1"}, {3899,"Pooky's Recall 2"}, {3900,"Pooky's Recall 3"}, {3901,"Egg Bomb"}, {3902,"Ring around the Rabbit"}, {3903,"Whirlwind"}, {3904,"Essence's Fury"}, {3905,"Essence's Fury"}, {3906,"Essence's Fury"}, {3907,"Essence's Fury"}, {3908,"Mana Blast"}, {3909,"Mana Syphon"}, {3910,"Brain Freeze"}, {3911,"Spiral of Souls"}, {3912,"Lower Black Spear Temple Portal Sending"}, {3913,"Aegis of the Golden Flame"}, {3914,"Dark Vortex"}, {3915,"Black Madness"}, {3916,"Flayed Flesh"}, {3917,"Numbing Chill"}, {3918,"Flammable"}, {3919,"Lightning Rod"}, {3920,"Tunnels to the Harbinger"}, {3921,"Harbinger's Lair"}, {3922,"Tunnels to the Harbinger"}, {3923,"Tunnels to the Harbinger"}, {3924,"Tunnels to the Harbinger"}, {3925,"Harbinger's Lair"}, {3926,"Harbinger's Fiery Touch"}, {3927,"Charge Flesh"}, {3928,"Disarmament"}, {3929,"Rossu Morta Chapterhouse Recall"}, {3930,"Whispering Blade Chapterhouse Recall"}, {3931,"Dark Vortex"}, {3932,"Grael's Rage"}, {3933,"Black Spear Strike"}, {3934,"Heavy Acid Ring"}, {3935,"Heavy Blade Ring"}, {3936,"Fire Bomb"}, {3937,"Heavy Force Ring"}, {3938,"Heavy Frost Ring"}, {3939,"Thaumic Bleed"}, {3940,"Exsanguinating Wave"}, {3941,"Heavy Lightning Ring"}, {3942,"Heavy Shock Ring"}, {3943,"Burning Earth"}, {3944,"Rain of Spears"}, {3945,"Raging Storm"}, {3946,"Acid Wave"}, {3947,"Blade Wave"}, {3948,"Flame Wave"}, {3949,"Force Wave"}, {3950,"Frost Wave"}, {3951,"Lightning Wave"}, {3952,"Shock Waves"}, {3953,"Carraida s Benediction"}, {3954,"Access to the White Tower"}, {3955,"Epic Bludgeon Ward"}, {3956,"Epic Piercing Ward"}, {3957,"Epic Slashing Ward"}, {3958,"White Tower Egress"}, {3959,"Redirect Motives"}, {3960,"Authority"}, {3961,"Defense of the Just"}, {3962,"Bound to the Law"}, {3963,"Epic Coordination"}, {3964,"Epic Focus"}, {3965,"Epic Strength"}, {3966,"Ringleader's Chambers"}, {3967,"Bandit Trap"}, {3968,"Bandit Hideout"}, {3969,"Acid Bomb"}, {3970,"Blade Bomb"}, {3971,"Fire Bomb"}, {3972,"Force Bomb"}, {3973,"Frost Bomb"}, {3974,"Lightning Bomb"}, {3975,"Shock Bomb"}, {3976,"Incantation of Armor Other"}, {3977,"Coordination Other Incantation"}, {3978,"Focus Other Incantation"}, {3979,"Strength Other Incantation"}, {3980,"Impenetrability Incantation"}, {3981,"Mana Renewal Other Incantation"}, {3982,"Regeneration Other Incantation"}, {3983,"Rejuvenation Other Incantation"}, {3984,"Mukkir's Ferocity"}, {3985,"Mukkir Sense"}, {3986,"Rock Fall"}, {3987,"Black Spear Strike"}, {3988,"Black Spear Strike"}, {3989,"Dark Lightning"}, {3990,"Heavy Frost Ring"}, {3991,"Thaumic Bleed"}, {3992,"Heavy Acid Ring"}, {3993,"Heavy Blade Ring"}, {3994,"Fire Bomb"}, {3995,"Heavy Force Ring"}, {3996,"Heavy Frost Ring"}, {3997,"Heavy Lightning Ring"}, {3998,"Dark Vortex"}, {3999,"Exsanguinating Wave"}, {4000,"Heavy Shock Ring"}, {4001,"Burning Earth"}, {4002,"Burning Earth"}, {4003,"Wall of Spears"}, {4004,"Wall of Spears"}, {4005,"Acid Wave"}, {4006,"Blade Wave"}, {4007,"Flame Wave"}, {4008,"Force Wave"}, {4009,"Frost Wave"}, {4010,"Lightning Wave"}, {4011,"Shock Waves"}, {4012,"White Totem Temple Sending"}, {4013,"Black Totem Temple Sending"}, {4014,"Abyssal Totem Temple Sending"}, {4015,"Ruschk Skin"}, {4016,"Shadow's Heart"}, {4017,"Phial's Accuracy"}, {4018,"Permafrost"}, {4019,"Epic Quickness"}, {4020,"Epic Deception Prowess"}, {4021,"Flurry of Stars"}, {4022,"Zombies Persistence"}, {4023,"Disco Inferno Portal Sending"}, {4024,"Asheron s Lesser Benediction"}, {4025,"Cast Iron Stomach"}, {4026,"Hematic Verdure"}, {4027,"Messenger's Stride"}, {4028,"Snowball"}, {4029,"Return to the Hall of Champions"}, {4030,"Colosseum Arena"}, {4031,"Advanced Colosseum Arena"}, {4032,"Colosseum Arena"}, {4033,"Advanced Colosseum Arena"}, {4034,"Colosseum Arena"}, {4035,"Advanced Colosseum Arena"}, {4036,"Colosseum Arena"}, {4037,"Advanced Colosseum Arena"}, {4038,"Colosseum Arena"}, {4039,"Advanced Colosseum Arena"}, {4040,"Master's Innervation"}, {4041,"The Path to Bur"}, {4042,"The Winding Path to Bur"}, {4043,"Kukuur Hide"}, {4044,"Acid Ball"}, {4045,"Flame Ball"}, {4046,"Lightning Ball"}, {4047,"Artisan Alchemist's Inspiration"}, {4048,"Artisan Cook's Inspiration"}, {4049,"Artisan Fletcher's Inspiration"}, {4050,"Artisan Lockpicker's Inspiration"}, {4051,"Master Alchemist's Inspiration"}, {4052,"Master Cook's Inspiration"}, {4053,"Master Fletcher's Inspiration"}, {4054,"Master Lockpicker's Inspiration"}, {4055,"Journeyman Alchemist's Inspiration"}, {4056,"Journeyman Cook's Inspiration"}, {4057,"Journeyman Fletcher's Inspiration"}, {4058,"Journeyman Lockpicker's Inspiration"}, {4059,"Endurance Other Incantation"}, {4060,"Quickness Other Incantation"}, {4061,"Willpower Other Incantation"}, {4062,"Empyrean Aegis"}, {4063,"Exit the Upper Catacomb"}, {4064,"Access the Upper Catacomb"}, {4065,"Lower Catacomb Portal Sending"}, {4066,"Access to the Ley Line Cavern"}, {4067,"Mucor Bolt"}, {4068,"Mucor Mana Well"}, {4069,"Mucor Jolt"}, {4070,"Empyrean Mana Absorbtion"}, {4071,"Empyrean Stamina Absorbtion"}, {4072,"Aurlanaa's Resolve"}, {4073,"Empyrean Regeneration"}, {4074,"Empyrean Rejuvenation"}, {4075,"Empyrean Mana Renewal"}, {4076,"Empyrean Enlightenment"}, {4077,"Mana Conversion Mastery Incantation"}, {4078,"Egg"}, {4079,"Work it Off"}, {4080,"Paid in Full"}, {4081,"Eye of the Tempest"}, {4082,"Big Fire"}, {4083,"Kresovus' Warren Portal Sending"}, {4084,"Bur Recall"}, {4085,"Entering Harraag's Hideout"}, {4086,"Icy Shield"}, {4087,"Armor Breach"}, {4088,"Withering Poison"}, {4089,"Assassin's Gift"}, {4090,"Scarab's Shell"}, {4091,"Spear"}, {4092,"Flame Grenade"}, {4093,"Don't Bite Me"}, {4094,"Don't Burn Me"}, {4095,"Don't Stab Me"}, {4096,"Flame Chain"}, {4097,"Violet Rain"}, {4098,"Treasure Room"}, {4099,"Strength of Diemos"}, {4100,"Breath of Renewal"}, {4101,"Champion's Skullduggery"}, {4102,"Champion's Clever Ruse"}, {4103,"Black Water Portal Sending"}, {4104,"Champion Arena"}, {4105,"Champion Arena"}, {4106,"Travel to the Paradox-touched Olthoi Queen's Lair"}, {4107,"Marrow Blight"}, {4108,"Apathy"}, {4109,"Greater Marrow Blight"}, {4110,"Poison"}, {4111,"Lesser Tusker Hide"}, {4112,"Spirit Nullification"}, {4113,"FoulRing"}, {4114,"Hypnotic Suggestion"}, {4115,"Mesmerizing Gaze"}, {4116,"Trance"}, {4117,"Dark Shield"}, {4118,"Dark Shield"}, {4119,"Dark Shield"}, {4120,"Dark Shield"}, {4121,"Dark Shield"}, {4122,"Dark Shield"}, {4123,"Dark Shield"}, {4124,"Dark Nanners"}, {4125,"Dark Nanners"}, {4126,"Rain of Nanners"}, {4127,"Portal Punch"}, {4128,"Call of the Mhoire Forge"}, {4129,"Travel to the Prodigal Shadow Child's Lair"}, {4130,"Travel to the Prodigal Shadow Child's Sanctum"}, {4131,"Spectral Light Weapon Mastery"}, {4132,"Spectral Blood Drinker"}, {4133,"Spectral Missile Weapon Mastery"}, {4134,"Spectral Missile Weapon Mastery"}, {4135,"Spectral Finesse Weapon Mastery"}, {4136,"Spectral Light Weapon Mastery"}, {4137,"Spectral Light Weapon Mastery"}, {4138,"Spectral Light Weapon Mastery"}, {4139,"Spectral Heavy Weapon Mastery"}, {4140,"Spectral Missile Weapon Mastery"}, {4141,"Spectral Light Weapon Mastery"}, {4142,"Spectral War Magic Mastery"}, {4143,"Witnessing History"}, {4144,"Witnessing History"}, {4145,"Crossing the Threshold of Darkness"}, {4146,"Crossing the Threshold of Darkness"}, {4147,"Crossing the Threshold of Darkness"}, {4148,"Crossing the Threshold of Darkness"}, {4149,"Crossing the Threshold of Darkness"}, {4150,"Expulsion from Claude's Mind"}, {4151,"Sending to the Other World"}, {4152,"Sending to the Other World"}, {4153,"Sending to the Other World"}, {4154,"Sending to the Other World"}, {4155,"Sending to the Other World"}, {4156,"Delving into Claude's Mind"}, {4157,"Delving into Claude's Mind"}, {4158,"Delving into Claude's Mind"}, {4159,"Delving into Claude's Mind"}, {4160,"Delving into Claude's Mind"}, {4161,"Exploring the Past"}, {4162,"Exploring the Past"}, {4163,"Exploring the Past"}, {4164,"Exploring the Past"}, {4165,"Exploring the Past"}, {4166,"Witnessing History"}, {4167,"Witnessing History"}, {4168,"Witnessing History"}, {4169,"Harbinger Blood Infusion"}, {4170,"Harbinger's Coordination"}, {4171,"Harbinger's Endurance"}, {4172,"Harbinger's Focus"}, {4173,"Harbinger's Quickness"}, {4174,"Harbinger's Strength"}, {4175,"Harbinger's Willpower"}, {4176,"Prodigal Harbinger's Lair"}, {4177,"Prodigal Harbinger's Antechamber"}, {4178,"Prodigal Harbinger's Antechamber"}, {4179,"Prodigal Harbinger's Antechamber"}, {4180,"Prodigal Harbinger's Antechamber"}, {4181,"Essence Bolt"}, {4182,"Ball Lightning"}, {4183,"Corrosive Veil"}, {4184,"Essence Bolt"}, {4185,"Essence Bolt"}, {4186,"Hoar Frost"}, {4187,"Essence Bolt"}, {4188,"Shadowed Flame"}, {4189,"Harbinger Acid Protection"}, {4190,"Harbinger Cold Protection"}, {4191,"Harbinger Flame Protection"}, {4192,"Harbinger Lightning Protection"}, {4193,"Harbinger Magic Defense"}, {4194,"Magical Void"}, {4195,"Harbinger Melee Defense"}, {4196,"Harbinger Missile Defense"}, {4197,"Naked to the Elements"}, {4198,"Paradox-touched Olthoi Infested Area Recall"}, {4199,"Frozen Armor"}, {4200,"Into the Darkness"}, {4201,"Numbing Chill"}, {4202,"Trevor's Zombie Strike"}, {4203,"Dark Crypt Entrance"}, {4204,"Dark Crypt Entrance"}, {4205,"Dark Crypt Entrance"}, {4206,"Chewy Center"}, {4207,"Arena of the Pumpkin King"}, {4208,"Spectral Flame"}, {4209,"Gummy Shield"}, {4210,"The Jitters"}, {4211,"Licorice Leap"}, {4212,"Sticky Melee"}, {4213,"Colosseum Recall"}, {4214,"Return to the Keep"}, {4215,"Shadow Armor"}, {4216,"Frost Wave"}, {4217,"Gourd Guard"}, {4218,"Knockback"}, {4219,"Trial of the Arm"}, {4220,"Trial of the Heart"}, {4221,"Spectral Life Magic Mastery"}, {4222,"Chambers Beneath"}, {4223,"Trials Graduation Chamber"}, {4224,"Trial of the Mind"}, {4225,"Trials of the Arm, Mind and Heart"}, {4226,"Epic Endurance"}, {4227,"Epic Willpower"}, {4228,"Awakening"}, {4229,"Journey Into the Past"}, {4230,"Bael'Zharon Dream Sending"}, {4231,"Leadership Mastery Other Incantation"}, {4232,"Epic Leadership"}, {4233,"Aerbax Recall Center Platform"}, {4234,"Aerbax Recall East Platform"}, {4235,"Aerbax Recall North Platform"}, {4236,"Aerbax Recall South Platform"}, {4237,"Aerbax Recall West Platform"}, {4238,"Aerbax Expulsion"}, {4239,"Ring of Death"}, {4240,"Aerbax's Magic Shield"}, {4241,"Aerbax Magic Shield Down"}, {4242,"Aerbax's Melee Shield"}, {4243,"Aerbax Melee Shield Down"}, {4244,"Aerbax's Missile Shield"}, {4245,"Aerbax Missile Shield Down"}, {4246,"MeteorStrike"}, {4247,"Tanada Battle Burrows Portal Sending"}, {4248,"Shroud Cabal North Outpost Sending"}, {4249,"Shroud Cabal South Outpost Sending"}, {4250,"Aerbax's Platform"}, {4251,"Jester's Boot"}, {4252,"Entrance to the Jester's Cell"}, {4253,"Entrance to the Jester's Cell"}, {4254,"Jester's Prison Hallway"}, {4255,"Jester's Prison Entryway"}, {4256,"Jester Recall 1"}, {4257,"Jester Recall 2"}, {4258,"Jester Recall 3"}, {4259,"Jester Recall 4"}, {4260,"Jester Recall 5"}, {4261,"Jester Recall 6"}, {4262,"Jester Recall 7"}, {4263,"Jester Recall 8"}, {4264,"Arcane Death"}, {4265,"Arcane Pyramid"}, {4266,"Blood Bolt"}, {4267,"Cow"}, {4268,"Fireworks"}, {4269,"Present"}, {4270,"Table"}, {4271,"Acid Whip"}, {4272,"Razor Whip"}, {4273,"Spray of Coins"}, {4274,"Flame Whip"}, {4275,"Electric Whip"}, {4276,"Jester's Malevolent Eye"}, {4277,"Jester's Prison Access"}, {4278,"Rytheran's Library Portal Sending"}, {4280,"Deck of Hands Favor"}, {4281,"Deck of Eyes Favor"}, {4282,"Arcane Death"}, {4283,"Arcane Death"}, {4284,"Harm Self"}, {4285,"Harm Self"}, {4286,"Harm Self"}, {4287,"Harm Self"}, {4288,"Harm Self"}, {4289,"Access the Messenger's Sanctuary"}, {4290,"Incantation of Armor Other"}, {4291,"Incantation of Armor Self"}, {4292,"Incantation of Bafflement Other"}, {4293,"Incantation of Bafflement Self"}, {4294,"Incantation of Clumsiness Other"}, {4295,"Incantation of Clumsiness Self"}, {4296,"Incantation of Coordination Other"}, {4297,"Incantation of Coordination Self"}, {4298,"Incantation of Endurance Other"}, {4299,"Incantation of Endurance Self"}, {4300,"Incantation of Enfeeble Other"}, {4301,"Incantation of Enfeeble Self"}, {4302,"Incantation of Feeblemind Other"}, {4303,"Incantation of Feeblemind Self"}, {4304,"Incantation of Focus Other"}, {4305,"Incantation of Focus Self"}, {4306,"Incantation of Frailty Other"}, {4307,"Incantation of Frailty Self"}, {4308,"Incantation of Harm Other"}, {4309,"Incantation of Harm Self"}, {4310,"Incantation of Heal Other"}, {4311,"Incantation of Heal Self"}, {4312,"Incantation of Imperil Other"}, {4313,"Incantation of Imperil Self"}, {4314,"Incantation of Mana Boost Other"}, {4315,"Incantation of Mana Boost Self"}, {4316,"Incantation of Mana Drain Other"}, {4317,"Incantation of Mana Drain Self"}, {4318,"Incantation of Quickness Other"}, {4319,"Incantation of Quickness Self"}, {4320,"Incantation of Revitalize Other"}, {4321,"Incantation of Revitalize Self"}, {4322,"Incantation of Slowness Other"}, {4323,"Incantation of Slowness Self"}, {4324,"Incantation of Strength Other"}, {4325,"Incantation of Strength Self"}, {4326,"Incantation of Weakness Other"}, {4327,"Incantation of Weakness Self"}, {4328,"Incantation of Willpower Other"}, {4329,"Incantation of Willpower Self"}, {4330,"Incantation of Nullify All Magic Other"}, {4331,"Incantation of Nullify All Magic Self"}, {4332,"Incantation of Nullify All Magic Other"}, {4333,"Incantation of Nullify All Magic Self"}, {4334,"Incantation of Nullify All Magic Other"}, {4335,"Incantation of Nullify All Magic Self"}, {4336,"Incantation of Nullify Creature Magic Other"}, {4337,"Incantation of Nullify Creature Magic Self"}, {4338,"Incantation of Nullify Creature Magic Other"}, {4339,"Incantation of Nullify Creature Magic Self"}, {4340,"Incantation of Nullify Creature Magic Other"}, {4341,"Incantation of Nullify Creature Magic Self"}, {4342,"Incantation of Nullify Item Magic"}, {4343,"Incantation of Nullify Item Magic"}, {4344,"Incantation of Nullify Item Magic"}, {4345,"Incantation of Nullify Life Magic Other"}, {4346,"Incantation of Nullify Life Magic Self"}, {4347,"Incantation of Nullify Life Magic Other"}, {4348,"Incantation of Nullify Life Magic Self"}, {4349,"Incantation of Nullify Life Magic Other"}, {4350,"Incantation of Nullify Life Magic Self"}, {4351,"Incantation of Greater Alacrity of the Conclave"}, {4352,"Incantation of Greater Vivify the Conclave"}, {4353,"Incantation of Greater Acumen of the Conclave"}, {4354,"Incantation of Greater Speed the Conclave"}, {4355,"Incantation of Greater Volition of the Conclave"}, {4356,"Incantation of Greater Empowering the Conclave"}, {4357,"Incantation of Greater Corrosive Ward"}, {4358,"Incantation of Greater Scythe Ward"}, {4359,"Incantation of Greater Flange Ward"}, {4360,"Incantation of Greater Frore Ward"}, {4361,"Incantation of Greater Inferno Ward"}, {4362,"Incantation of Greater Voltaic Ward"}, {4363,"Incantation of Greater Lance Ward"}, {4364,"Incantation of Greater Endless Well"}, {4365,"Incantation of Greater Soothing Wind"}, {4366,"Incantation of Greater Golden Wind"}, {4367,"Incantation of Greater Conjurant Chant"}, {4368,"Incantation of Warden of the Clutch"}, {4369,"Incantation of Guardian of the Clutch"}, {4370,"Incantation of Greater Artificant Chant"}, {4371,"Incantation of Greater Vitaeic Chant"}, {4372,"Incantation of Sanctifier of the Clutch"}, {4373,"Incantation of Greater Conveyic Chant"}, {4374,"Incantation of Greater Hieromantic Chant"}, {4375,"Incantation of Blossom Black Firework OUT"}, {4376,"Incantation of Blossom Blue Firework OUT"}, {4377,"Incantation of Blossom Green Firework OUT"}, {4378,"Incantation of Blossom Orange Firework OUT"}, {4379,"Incantation of Blossom Purple Firework OUT"}, {4380,"Incantation of Blossom Red Firework OUT"}, {4381,"Incantation of Blossom White Firework OUT"}, {4382,"Incantation of Blossom Yellow Firework OUT"}, {4383,"Incantation of Blossom Black Firework UP"}, {4384,"Incantation of Blossom Blue Firework UP"}, {4385,"Incantation of Blossom Green Firework UP"}, {4386,"Incantation of Blossom Orange Firework UP"}, {4387,"Incantation of Blossom Purple Firework UP"}, {4388,"Incantation of Blossom Red Firework UP"}, {4389,"Incantation of Blossom White Firework UP"}, {4390,"Incantation of Blossom Yellow Firework UP"}, {4391,"Incantation of Acid Bane"}, {4392,"Incantation of Acid Lure"}, {4393,"Incantation of Blade Bane"}, {4394,"Incantation of Blade Lure"}, {4395,"Aura of Incantation of Blood Drinker Self"}, {4396,"Incantation of Blood Loather"}, {4397,"Incantation of Bludgeon Bane"}, {4398,"Incantation of Bludgeon Lure"}, {4399,"Incantation of Brittlemail"}, {4400,"Aura of Incantation of Defender Self"}, {4401,"Incantation of Flame Bane"}, {4402,"Incantation of Flame Lure"}, {4403,"Incantation of Frost Bane"}, {4404,"Incantation of Frost Lure"}, {4405,"Aura of Incantation of Heart Seeker Self"}, {4406,"Incantation of Hermetic Void"}, {4407,"Incantation of Impenetrability"}, {4408,"Incantation of Leaden Weapon"}, {4409,"Incantation of Lightning Bane"}, {4410,"Incantation of Lightning Lure"}, {4411,"Incantation of Lure Blade"}, {4412,"Incantation of Piercing Bane"}, {4413,"Incantation of Piercing Lure"}, {4414,"Aura of Incantation of Spirit Drinker Self"}, {4415,"Incantation of Spirit Loather"}, {4416,"Incantation of Strengthen Lock"}, {4417,"Aura of Incantation of Swift Killer Self"}, {4418,"Aura of Incantation of Hermetic Link Self"}, {4419,"Incantation of Turn Blade"}, {4420,"Incantation of Weaken Lock"}, {4421,"Incantation of Acid Arc"}, {4422,"Incantation of Blade Arc"}, {4423,"Incantation of Flame Arc"}, {4424,"Incantation of Force Arc"}, {4425,"Incantation of Frost Arc"}, {4426,"Incantation of Lightning Arc"}, {4427,"Incantation of Shock Arc"}, {4428,"Incantation of Martyr's Hecatomb"}, {4429,"Incantation of Martyr's Blight"}, {4430,"Incantation of Martyr's Tenacity"}, {4431,"Incantation of Acid Blast"}, {4432,"Incantation of Acid Streak"}, {4433,"Incantation of Acid Stream"}, {4434,"Incantation of Acid Volley"}, {4435,"Incantation of Blade Blast"}, {4436,"Incantation of Blade Volley"}, {4437,"Incantation of Bludgeoning Volley"}, {4438,"Incantation of Flame Blast"}, {4439,"Incantation of Flame Bolt"}, {4440,"Incantation of Flame Streak"}, {4441,"Incantation of Flame Volley"}, {4442,"Incantation of Force Blast"}, {4443,"Incantation of Force Bolt"}, {4444,"Incantation of Force Streak"}, {4445,"Incantation of Force Volley"}, {4446,"Incantation of Frost Blast"}, {4447,"Incantation of Frost Bolt"}, {4448,"Incantation of Frost Streak"}, {4449,"Incantation of Frost Volley"}, {4450,"Incantation of Lightning Blast"}, {4451,"Incantation of Lightning Bolt"}, {4452,"Incantation of Lightning Streak"}, {4453,"Incantation of Lightning Volley"}, {4454,"Incantation of Shock Blast"}, {4455,"Incantation of Shock Wave"}, {4456,"Incantation of Shock Wave Streak"}, {4457,"Incantation of Whirling Blade"}, {4458,"Incantation of Whirling Blade Streak"}, {4459,"Incantation of Acid Protection Other"}, {4460,"Incantation of Acid Protection Self"}, {4461,"Incantation of Blade Protection Other"}, {4462,"Incantation of Blade Protection Self"}, {4463,"Incantation of Bludgeoning Protection Other"}, {4464,"Incantation of Bludgeoning Protection Self"}, {4465,"Incantation of Cold Protection Other"}, {4466,"Incantation of Cold Protection Self"}, {4467,"Incantation of Fire Protection Other"}, {4468,"Incantation of Fire Protection Self"}, {4469,"Incantation of Lightning Protection Other"}, {4470,"Incantation of Lightning Protection Self"}, {4471,"Incantation of Piercing Protection Other"}, {4472,"Incantation of Piercing Protection Self"}, {4473,"Incantation of Acid Vulnerability Other"}, {4474,"Incantation of Acid Vulnerability Self"}, {4475,"Incantation of Blade Vulnerability Other"}, {4476,"Incantation of Blade Vulnerability Self"}, {4477,"Incantation of Bludgeoning Vulnerability Other"}, {4478,"Incantation of Bludgeoning Vulnerability Self"}, {4479,"Incantation of Cold Vulnerability Other"}, {4480,"Incantation of Cold Vulnerability Self"}, {4481,"Incantation of Fire Vulnerability Other"}, {4482,"Incantation of Fire Vulnerability Self"}, {4483,"Incantation of Lightning Vulnerability Other"}, {4484,"Incantation of Lightning Vulnerability Self"}, {4485,"Incantation of Piercing Vulnerability Other"}, {4486,"Incantation of Piercing Vulnerability Self"}, {4487,"Incantation of Exhaustion Other"}, {4488,"Incantation of Exhaustion Self"}, {4489,"Incantation of Fester Other"}, {4490,"Incantation of Fester Self"}, {4491,"Incantation of Mana Depletion Other"}, {4492,"Incantation of Mana Depletion Self"}, {4493,"Incantation of Mana Renewal Other"}, {4494,"Incantation of Mana Renewal Self"}, {4495,"Incantation of Regeneration Other"}, {4496,"Incantation of Regeneration Self"}, {4497,"Incantation of Rejuvenation Other"}, {4498,"Incantation of Rejuvenation Self"}, {4499,"Incantation of Arcanum Salvaging Self"}, {4500,"Incantation of Arcanum Enlightenment"}, {4501,"Incantation of Nuhmudira's Wisdom"}, {4502,"Incantation of Nuhmudira Enlightenment"}, {4503,"Incantation of Alchemy Ineptitude Other"}, {4504,"Incantation of Alchemy Ineptitude Self"}, {4505,"Incantation of Alchemy Mastery Other"}, {4506,"Incantation of Alchemy Mastery Self"}, {4507,"Incantation of Arcane Benightedness Other"}, {4508,"Incantation of Arcane Benightedness Self"}, {4509,"Incantation of Arcane Enlightenment Other"}, {4510,"Incantation of Arcane Enlightenment Self"}, {4511,"Incantation of Armor Tinkering Expertise Other"}, {4512,"Incantation of Armor Tinkering Expertise Self"}, {4513,"Incantation of Armor Tinkering Ignorance Other"}, {4514,"Incantation of Armor Tinkering Ignorance Self"}, {4515,"Incantation of Light Weapon Ineptitude Other"}, {4516,"Incantation of Light Weapon Ineptitude Self"}, {4517,"Incantation of Light Weapon Mastery Other"}, {4518,"Incantation of Light Weapon Mastery Self"}, {4519,"Incantation of Missile Weapon Ineptitude Other"}, {4520,"Incantation of Missile Weapon Ineptitude Self"}, {4521,"Incantation of Missile Weapon Mastery Other"}, {4522,"Incantation of Missile Weapon Mastery Self"}, {4523,"Incantation of Cooking Ineptitude Other"}, {4524,"Incantation of Cooking Ineptitude Self"}, {4525,"Incantation of Cooking Mastery Other"}, {4526,"Incantation of Cooking Mastery Self"}, {4527,"Incantation of Creature Enchantment Ineptitude Other"}, {4528,"Incantation of Creature Enchantment Ineptitude Self"}, {4529,"Incantation of Creature Enchantment Mastery Other"}, {4530,"Incantation of Creature Enchantment Mastery Self"}, {4531,"Incantation of Missile Weapon Ineptitude Other"}, {4532,"Incantation of Missile Weapon Ineptitude Self"}, {4533,"Incantation of Missile Weapon Mastery Other"}, {4534,"Incantation of Missile Weapon Mastery Self"}, {4535,"Incantation of Finesse Weapon Ineptitude Other"}, {4536,"Incantation of Finesse Weapon Ineptitude Self"}, {4537,"Incantation of Finesse Weapon Mastery Other"}, {4538,"Incantation of Finesse Weapon Mastery Self"}, {4539,"Incantation of Deception Ineptitude Other"}, {4540,"Incantation of Deception Ineptitude Self"}, {4541,"Incantation of Deception Mastery Other"}, {4542,"Incantation of Deception Mastery Self"}, {4543,"Incantation of Defenselessness Other"}, {4544,"Incantation of Defenselessness Self"}, {4545,"Incantation of Faithlessness Other"}, {4546,"Incantation of Faithlessness Self"}, {4547,"Incantation of Fealty Other"}, {4548,"Incantation of Fealty Self"}, {4549,"Incantation of Fletching Ineptitude Other"}, {4550,"Incantation of Fletching Ineptitude Self"}, {4551,"Incantation of Fletching Mastery Other"}, {4552,"Incantation of Fletching Mastery Self"}, {4553,"Incantation of Healing Ineptitude Other"}, {4554,"Incantation of Healing Ineptitude Self"}, {4555,"Incantation of Healing Mastery Other"}, {4556,"Incantation of Healing Mastery Self"}, {4557,"Incantation of Impregnability Other"}, {4558,"Incantation of Impregnability Self"}, {4559,"Incantation of Invulnerability Other"}, {4560,"Incantation of Invulnerability Self"}, {4561,"Incantation of Item Enchantment Ineptitude Other"}, {4562,"Incantation of Item Enchantment Ineptitude Self"}, {4563,"Incantation of Item Enchantment Mastery Other"}, {4564,"Incantation of Item Enchantment Mastery Self"}, {4565,"Incantation of Item Tinkering Expertise Other"}, {4566,"Incantation of Item Tinkering Expertise Self"}, {4567,"Incantation of Item Tinkering Ignorance Other"}, {4568,"Incantation of Item Tinkering Ignorance Self"}, {4569,"Incantation of Jumping Ineptitude Other"}, {4570,"Incantation of Jumping Ineptitude Self"}, {4571,"Incantation of Jumping Mastery Other"}, {4572,"Incantation of Jumping Mastery Self"}, {4573,"Incantation of Leaden Feet Other"}, {4574,"Incantation of Leaden Feet Self"}, {4575,"Incantation of Leadership Ineptitude Other"}, {4576,"Incantation of Leadership Ineptitude Self"}, {4577,"Incantation of Leadership Mastery Other"}, {4578,"Incantation of Leadership Mastery Self"}, {4579,"Incantation of Life Magic Ineptitude Other"}, {4580,"Incantation of Life Magic Ineptitude Self"}, {4581,"Incantation of Life Magic Mastery Other"}, {4582,"Incantation of Life Magic Mastery Self"}, {4583,"Incantation of Lockpick Ineptitude Other"}, {4584,"Incantation of Lockpick Ineptitude Self"}, {4585,"Incantation of Lockpick Mastery Other"}, {4586,"Incantation of Lockpick Mastery Self"}, {4587,"Incantation of Light Weapon Ineptitude Other"}, {4588,"Incantation of Light Weapon Ineptitude Self"}, {4589,"Incantation of Light Weapon Mastery Other"}, {4590,"Incantation of Light Weapon Mastery Self"}, {4591,"Incantation of Magic Item Tinkering Expertise Other"}, {4592,"Incantation of Magic Item Tinkering Expertise Self"}, {4593,"Incantation of Magic Item Tinkering Ignorance Other"}, {4594,"Incantation of Magic Item Tinkering Ignorance Self"}, {4595,"Incantation of Magic Resistance Other"}, {4596,"Incantation of Magic Resistance Self"}, {4597,"Incantation of Magic Yield Other"}, {4598,"Incantation of Magic Yield Self"}, {4599,"Incantation of Mana Conversion Ineptitude Other"}, {4600,"Incantation of Mana Conversion Ineptitude Self"}, {4601,"Incantation of Mana Conversion Mastery Other"}, {4602,"Incantation of Mana Conversion Mastery Self"}, {4603,"Incantation of Monster Attunement Other"}, {4604,"Incantation of Monster Attunement Self"}, {4605,"Incantation of Monster Unfamiliarity Other"}, {4606,"Incantation of Monster Unfamiliarity Self"}, {4607,"Incantation of Person Attunement Other"}, {4608,"Incantation of Person Attunement Self"}, {4609,"Incantation of Person Unfamiliarity Other"}, {4610,"Incantation of Person Unfamiliarity Self"}, {4611,"Incantation of Light Weapon Ineptitude Other"}, {4612,"Incantation of Light Weapon Ineptitude Self"}, {4613,"Incantation of Light Weapon Mastery Other"}, {4614,"Incantation of Light Weapon Mastery Self"}, {4615,"Incantation of Sprint Other"}, {4616,"Incantation of Sprint Self"}, {4617,"Incantation of Light Weapon Ineptitude Other"}, {4618,"Incantation of Light Weapon Ineptitude Self"}, {4619,"Incantation of Light Weapon Mastery Other"}, {4620,"Incantation of Light Weapon Mastery Self"}, {4621,"Incantation of Heavy Weapon Ineptitude Other"}, {4622,"Incantation of Heavy Weapon Ineptitude Self"}, {4623,"Incantation of Heavy Weapon Mastery Other"}, {4624,"Incantation of Heavy Weapon Mastery Self"}, {4625,"Incantation of Missile Weapon Ineptitude Other"}, {4626,"Incantation of Missile Weapon Ineptitude Self"}, {4627,"Incantation of Missile Weapon Mastery Other"}, {4628,"Incantation of Missile Weapon Mastery Self"}, {4629,"Incantation of Light Weapon Ineptitude Other"}, {4630,"Incantation of Light Weapon Mastery Other"}, {4631,"Incantation of Light Weapon Mastery Self"}, {4632,"Incantation of Light Weapon Ineptitude Other"}, {4633,"Incantation of Vulnerability Other"}, {4634,"Incantation of Vulnerability Self"}, {4635,"Incantation of War Magic Ineptitude Other"}, {4636,"Incantation of War Magic Ineptitude Self"}, {4637,"Incantation of War Magic Mastery Other"}, {4638,"Incantation of War Magic Mastery Self"}, {4639,"Incantation of Weapon Tinkering Expertise Other"}, {4640,"Incantation of Weapon Tinkering Expertise Self"}, {4641,"Incantation of Weapon Tinkering Ignorance Other"}, {4642,"Incantation of Weapon Tinkering Ignorance Self"}, {4643,"Incantation of Drain Health Other"}, {4644,"Incantation of Drain Mana Other"}, {4645,"Incantation of Drain Stamina Other"}, {4646,"Incantation of Health to Mana Other"}, {4647,"Incantation of Health to Mana Self"}, {4648,"Incantation of Health to Stamina Other"}, {4649,"Incantation of Health to Stamina Self"}, {4650,"Incantation of Infuse Health Other"}, {4651,"Incantation of Infuse Mana Other"}, {4652,"Incantation of Infuse Stamina Other"}, {4653,"Incantation of Mana to Health Other"}, {4654,"Incantation of Mana to Health Self"}, {4655,"Incantation of Mana to Stamina Other"}, {4656,"Incantation of Mana to Stamina Self"}, {4657,"Incantation of Stamina to Health Other"}, {4658,"Incantation of Stamina to Health Self"}, {4659,"Incantation of Stamina to Mana Other"}, {4660,"Epic Acid Bane"}, {4661,"Epic Blood Thirst"}, {4662,"Epic Bludgeoning Bane"}, {4663,"Epic Defender"}, {4664,"Epic Flame Bane"}, {4665,"Epic Frost Bane"}, {4666,"Epic Heart Thirst"}, {4667,"Epic Impenetrability"}, {4668,"Epic Piercing Bane"}, {4669,"Epic Slashing Bane"}, {4670,"Epic Spirit Thirst"}, {4671,"Epic Storm Bane"}, {4672,"Epic Swift Hunter"}, {4673,"Epic Acid Ward"}, {4674,"Epic Bludgeoning Ward"}, {4675,"Epic Flame Ward"}, {4676,"Epic Frost Ward"}, {4677,"Epic Piercing Ward"}, {4678,"Epic Slashing Ward"}, {4679,"Epic Storm Ward"}, {4680,"Epic Health Gain"}, {4681,"Epic Mana Gain"}, {4682,"Epic Stamina Gain"}, {4683,"Epic Alchemical Prowess"}, {4684,"Epic Arcane Prowess"}, {4685,"Epic Armor Tinkering Expertise"}, {4686,"Epic Light Weapon Aptitude"}, {4687,"Epic Missile Weapon Aptitude"}, {4688,"Epic Cooking Prowess"}, {4689,"Epic Creature Enchantment Aptitude"}, {4690,"Epic Missile Weapon Aptitude"}, {4691,"Epic Finesse Weapon Aptitude"}, {4692,"Epic Fealty"}, {4693,"Epic Fletching Prowess"}, {4694,"Epic Healing Prowess"}, {4695,"Epic Impregnability"}, {4696,"Epic Invulnerability"}, {4697,"Epic Item Enchantment Aptitude"}, {4698,"Epic Item Tinkering Expertise"}, {4699,"Epic Jumping Prowess"}, {4700,"Epic Life Magic Aptitude"}, {4701,"Epic Lockpick Prowess"}, {4702,"Epic Light Weapon Aptitude"}, {4703,"Epic Magic Item Tinkering Expertise"}, {4704,"Epic Magic Resistance"}, {4705,"Epic Mana Conversion Prowess"}, {4706,"Epic Monster Attunement"}, {4707,"Epic Person Attunement"}, {4708,"Epic Salvaging Aptitude"}, {4709,"Epic Light Weapon Aptitude"}, {4710,"Epic Sprint"}, {4711,"Epic Light Weapon Aptitude"}, {4712,"Epic Heavy Weapon Aptitude"}, {4713,"Epic Missile Weapon Aptitude"}, {4714,"Epic Light Weapon Aptitude"}, {4715,"Epic War Magic Aptitude"}, {4716,"Burning Curse"}, {4717,"Expedient Return to Ulgrim"}, {4718,"Welcomed by the Blood Witches"}, {4719,"Welcomed by the Blood Witches"}, {4720,"Welcomed by the Blood Witches"}, {4721,"Travel to the Ruins of Degar'Alesh"}, {4722,"Bleed Other"}, {4723,"Bleed Self"}, {4724,"Gateway to Nyr'leha"}, {4725,"The Pit of Heretics"}, {4726,"Poison"}, {4727,"Poison"}, {4728,"Poison"}, {4729,"Travel to the Catacombs of Tar'Kelyn"}, {4730,"Novice Duelist's Coordination"}, {4731,"Apprentice Duelist's Coordination"}, {4732,"Journeyman Duelist's Coordination"}, {4733,"Master Duelist's Coordination"}, {4734,"Novice Hero's Endurance"}, {4735,"Apprentice Hero's Endurance"}, {4736,"Journeyman Hero's Endurance"}, {4737,"Master Hero's Endurance"}, {4738,"Novice Sage's Focus"}, {4739,"Apprentice Sage's Focus"}, {4740,"Journeyman Sage's Focus"}, {4741,"Master Sage's Focus"}, {4742,"Novice Rover's Quickness"}, {4743,"Apprentice Rover's Quickness"}, {4744,"Journeyman Rover's Quickness"}, {4745,"Master Rover's Quickness"}, {4746,"Novice Brute's Strength"}, {4747,"Apprentice Brute's Strength"}, {4748,"Journeyman Brute's Strength"}, {4749,"Master Brute's Strength"}, {4750,"Novice Adherent's Willpower"}, {4751,"Apprentice Adherent's Willpower"}, {4752,"Journeyman Adherent's Willpower"}, {4753,"Master Adherent's Willpower"}, {4754,"Apprentice Survivor's Health"}, {4755,"Journeyman Survivor's Health"}, {4756,"Apprentice Clairvoyant's Mana"}, {4757,"Journeyman Clairvoyant's Mana"}, {4758,"Apprentice Tracker's Stamina"}, {4759,"Journeyman Tracker's Stamina"}, {4760,"Incidental Acid Resistance"}, {4761,"Crude Acid Resistance"}, {4762,"Effective Acid Resistance"}, {4763,"Masterwork Acid Resistance"}, {4764,"Incidental Bludgeoning Resistance"}, {4765,"Crude Bludgeoning Resistance"}, {4766,"Effective Bludgeoning Resistance"}, {4767,"Masterwork Bludgeoning Resistance"}, {4768,"Incidental Flame Resistance"}, {4769,"Crude Flame Resistance"}, {4770,"Effective Flame Resistance"}, {4771,"Masterwork Flame Resistance"}, {4772,"Incidental Frost Resistance"}, {4773,"Crude Frost Resistance"}, {4774,"Effective Frost Resistance"}, {4775,"Masterwork Frost Resistance"}, {4776,"Incidental Lightning Resistance"}, {4777,"Crude Lightning Resistance"}, {4778,"Effective Lightning Resistance"}, {4779,"Masterwork Lightning Resistance"}, {4780,"Incidental Piercing Resistance"}, {4781,"Crude Piercing Resistance"}, {4782,"Effective Piercing Resistance"}, {4783,"Masterwork Piercing Resistance"}, {4784,"Incidental Slashing Resistance"}, {4785,"Crude Slashing Resistance"}, {4786,"Effective Slashing Resistance"}, {4787,"Masterwork Slashing Resistance"}, {4788,"Novice Concoctor's Alchemy Aptitude"}, {4789,"Apprentice Concoctor's Alchemy Aptitude"}, {4790,"Journeyman Concoctor's Alchemy Aptitude"}, {4791,"Master Concoctor's Alchemy Aptitude"}, {4792,"Novice Armorer's Armor Tinkering Aptitude"}, {4793,"Apprentice Armorer's Armor Tinkering Aptitude"}, {4794,"Journeyman Armorer's Armor Tinkering Aptitude"}, {4795,"Master Armorer's Armor Tinkering Aptitude"}, {4796,"Novice Soldier's Light Weapon Aptitude"}, {4797,"Apprentice Soldier's Light Weapon Aptitude"}, {4798,"Journeyman Soldier's Light Weapon Aptitude"}, {4799,"Master Soldier's Light Weapon Aptitude"}, {4800,"Novice Archer's Missile Weapon Aptitude"}, {4801,"Apprentice Archer's Missile Weapon Aptitude"}, {4802,"Journeyman Archer's Missile Weapon Aptitude"}, {4803,"Master Archer's Missile Weapon Aptitude"}, {4804,"Novice Chef's Cooking Aptitude"}, {4805,"Apprentice Chef's Cooking Aptitude"}, {4806,"Journeyman Chef's Cooking Aptitude"}, {4807,"Master Chef's Cooking Aptitude"}, {4808,"Novice Enchanter's Creature Aptitude"}, {4809,"Apprentice Enchanter's Creature Aptitude"}, {4810,"Journeyman Enchanter's Creature Aptitude"}, {4811,"Master Enchanter's Creature Aptitude"}, {4812,"Novice Archer's Missile Weapon Aptitude"}, {4813,"Apprentice Archer's Missile Weapon Aptitude"}, {4814,"Journeyman Archer's Missile Weapon Aptitude"}, {4815,"Master Archer's Missile Weapon Aptitude"}, {4816,"Novice Soldier's Finesse Weapon Aptitude"}, {4817,"Apprentice Soldier's Finesse Weapon Aptitude"}, {4818,"Journeyman Soldier's Finesse Weapon Aptitude"}, {4819,"Master Soldier's Finesse Weapon Aptitude"}, {4820,"Novice Huntsman's Fletching Aptitude"}, {4821,"Apprentice Huntsman's Fletching Aptitude"}, {4822,"Journeyman Huntsman's Fletching Aptitude"}, {4823,"Master Huntsman's Fletching Aptitude"}, {4824,"Novice Artifex's Item Aptitude"}, {4825,"Apprentice Artifex's Item Aptitude"}, {4826,"Journeyman Artifex's Item Aptitude"}, {4827,"Master Artifex's Item Aptitude"}, {4828,"Novice Inventor's Item Tinkering Aptitude"}, {4829,"Apprentice Inventor's Item Tinkering Aptitude"}, {4830,"Journeyman Inventor's Item Tinkering Aptitude"}, {4831,"Master Inventor's Item Tinkering Aptitude"}, {4832,"Novice Leaper's Jumping Aptitude"}, {4833,"Apprentice Leaper's Jumping Aptitude"}, {4834,"Journeyman Leaper's Jumping Aptitude"}, {4835,"Master Leaper's Jumping Aptitude"}, {4836,"Novice Theurge's Life Magic Aptitude"}, {4837,"Apprentice Theurge's Life Magic Aptitude"}, {4838,"Journeyman Theurge's Life Magic Aptitude"}, {4839,"Master Theurge's Life Magic Aptitude"}, {4840,"Novice Locksmith's Lockpick Aptitude"}, {4841,"Apprentice Locksmith's Lockpick Aptitude"}, {4842,"Journeyman Locksmith's Lockpick Aptitude"}, {4843,"Master Locksmith's Lockpick Aptitude"}, {4844,"Yeoman's Loyalty"}, {4845,"Squire's Loyalty"}, {4846,"Novice Soldier's Light Weapon Aptitude"}, {4847,"Apprentice Soldier's Light Weapon Aptitude"}, {4848,"Journeyman Soldier's Light Weapon Aptitude"}, {4849,"Master Soldier's Light Weapon Aptitude"}, {4850,"Novice Negator's Magic Resistance"}, {4851,"Apprentice Negator's Magic Resistance"}, {4852,"Journeyman Negator's Magic Resistance"}, {4853,"Master Negator's Magic Resistance"}, {4854,"Novice Arcanist's Magic Item Tinkering Aptitude"}, {4855,"Apprentice Arcanist's Magic Item Tinkering Aptitude"}, {4856,"Journeyman Arcanist's Magic Item Tinkering Aptitude"}, {4857,"Master Arcanist's Magic Item Tinkering Aptitude"}, {4858,"Novice Guardian's Invulnerability"}, {4859,"Apprentice Guardian's Invulnerability"}, {4860,"Journeyman Guardian's Invulnerability"}, {4861,"Master Guardian's Invulnerability"}, {4862,"Novice Wayfarer's Impregnability"}, {4863,"Apprentice Wayfarer's Impregnability"}, {4864,"Journeyman Wayfarer's Impregnability"}, {4865,"Master Wayfarer's Impregnability"}, {4866,"Novice Scavenger's Salvaging Aptitude"}, {4867,"Apprentice Scavenger's Salvaging Aptitude"}, {4868,"Novice Soldier's Light Weapon Aptitude"}, {4869,"Apprentice Soldier's Light Weapon Aptitude"}, {4870,"Journeyman Soldier's Light Weapon Aptitude"}, {4871,"Master Soldier's Light Weapon Aptitude"}, {4872,"Novice Messenger's Sprint Aptitude"}, {4873,"Apprentice Messenger's Sprint Aptitude"}, {4874,"Journeyman Messenger's Sprint Aptitude"}, {4875,"Master Messenger's Sprint Aptitude"}, {4876,"Novice Soldier's Light Weapon Aptitude"}, {4877,"Apprentice Soldier's Light Weapon Aptitude"}, {4878,"Journeyman Soldier's Light Weapon Aptitude"}, {4879,"Master Soldier's Light Weapon Aptitude"}, {4880,"Novice Soldier's Heavy Weapon Aptitude"}, {4881,"Apprentice Soldier's Heavy Weapon Aptitude"}, {4882,"Journeyman Soldier's Heavy Weapon Aptitude"}, {4883,"Master Soldier's Heavy Weapon Aptitude"}, {4884,"Novice Archer's Missile Weapon Aptitude"}, {4885,"Apprentice Archer's Missile Weapon Aptitude"}, {4886,"Journeyman Archer's Missile Weapon Aptitude"}, {4887,"Master Archer's Missile Weapon Aptitude"}, {4888,"Novice Soldier's Light Weapon Aptitude"}, {4889,"Apprentice Soldier's Light Weapon Aptitude"}, {4890,"Journeyman Soldier's Light Weapon Aptitude"}, {4891,"Master Soldier's Light Weapon Aptitude"}, {4892,"Novice Warlock's War Magic Aptitude"}, {4893,"Apprentice Warlock's War Magic Aptitude"}, {4894,"Journeyman Warlock's War Magic Aptitude"}, {4895,"Master Warlock's War Magic Aptitude"}, {4896,"Novice Swordsmith's Weapon Tinkering Aptitude"}, {4897,"Apprentice Swordsmith's Weapon Tinkering Aptitude"}, {4898,"Journeyman Swordsmith's Weapon Tinkering Aptitude"}, {4899,"Master Swordsmith's Weapon Tinkering Aptitude"}, {4900,"Society Initiate's Blessing"}, {4901,"Society Adept's Blessing"}, {4902,"Society Knight's Blessing"}, {4903,"Society Lord's Blessing"}, {4904,"Society Master's Blessing"}, {4905,"Novice Challenger's Rejuvenation"}, {4906,"Apprentice Challenger's Rejuvenation"}, {4907,"Celestial Hand Stronghold Recall"}, {4908,"Eldrytch Web Stronghold Recall"}, {4909,"Radiant Blood Stronghold Recall"}, {4910,"Raider Tag"}, {4911,"Epic Armor"}, {4912,"Epic Weapon Tinkering Expertise"}, {4913,"Aerlinthe Pyramid Portal Sending"}, {4914,"Aerlinthe Pyramid Portal Exit"}, {4915,"A'mun Pyramid Portal Sending"}, {4916,"A'mun Pyramid Portal Exit"}, {4917,"Esper Pyramid Portal Sending"}, {4918,"Esper Pyramid Portal Exit"}, {4919,"Halaetan Pyramid Portal Sending"}, {4920,"Halaetan Pyramid Portal Exit"}, {4921,"Linvak Pyramid Portal Sending"}, {4922,"Linvak Pyramid Portal Exit"}, {4923,"Obsidian Pyramid Portal Sending"}, {4924,"Obsidian Pyramid Portal Exit"}, {4925,"Dance"}, {4926,"Smite"}, {4927,"Incantation of Acid Stream with 300 Spellpower"}, {4928,"Incantation of Acid Stream with 350 Spellpower"}, {4929,"Harm"}, {4930,"Flame Bolt I"}, {4931,"Mini Fireball"}, {4932,"Mini Fireball"}, {4933,"Slowness"}, {4934,"Slowness"}, {4935,"Slowness"}, {4936,"Flame Bolt I"}, {4937,"Flame Bolt I"}, {4938,"Flame Bolt I"}, {4939,"Mini Uber"}, {4940,"Mini Ring"}, {4941,"Mini Ring"}, {4942,"Mini Ring"}, {4943,"Mini Fireball"}, {4944,"Slowness"}, {4945,"Flame Bolt I"}, {4946,"Mini Ring"}, {4947,"Harm"}, {4948,"Harm"}, {4949,"Harm"}, {4950,"Tactical Defense"}, {4951,"Tactical Defense"}, {4952,"Tactical Defense"}, {4953,"Test Portal"}, {4954,"Crystalline Portal"}, {4955,"Portal Space Eddy"}, {4956,"Tanada Sanctum Portal Sending"}, {4957,"Tanada Sanctum Return"}, {4958,"Greater Rockslide"}, {4959,"Lesser Rockslide"}, {4960,"Lesser Rockslide"}, {4961,"Lesser Rockslide"}, {4962,"Rockslide"}, {4963,"Rockslide"}, {4964,"Rockslide"}, {4965,"Greater Rockslide"}, {4966,"Greater Rockslide"}, {4967,"Cleansing Ring of Fire"}, {4968,"Ranger's Boon"}, {4969,"Ranger's Boon"}, {4970,"Ranger's Boon"}, {4971,"Enchanter's Boon"}, {4972,"Hieromancer's Boon"}, {4973,"Fencer's Boon"}, {4974,"Life Giver's Boon"}, {4975,"Kern's Boon"}, {4976,"Kern's Boon"}, {4977,"Kern's Boon"}, {4978,"Kern's Boon"}, {4979,"Soldier's Boon"}, {4980,"Kern's Boon"}, {4981,"Incantation of Stamina to Mana Self"}, {4982,"Nimble Fingers - Lockpick"}, {4983,"Nimble Fingers - Alchemy"}, {4984,"Nimble Fingers - Cooking"}, {4985,"Nimble Fingers - Fletching"}, {4986,"Assassin's Alchemy Kit"}, {4987,"Olthoi Spit"}, {4988,"Tunnel Out"}, {4989,"Mysterious Portal"}, {4990,"Floor Puzzle Bypass"}, {4991,"Jump Puzzle Bypass"}, {4992,"Direct Assassin Access"}, {4993,"Portal to Derethian Combat Arena"}, {4994,"Get over here!"}, {4995,"Portal to Derethian Combat Arena"}, {4996,"Portal to Derethian Combat Arena"}, {4997,"Portal to Derethian Combat Arena"}, {4998,"Arena Stamina"}, {4999,"Arena Life"}, {5000,"Arena Mana"}, {5001,"Arena Piercing Protection Other"}, {5002,"Arena Acid Protection Other"}, {5003,"Arena Blade Protection Other"}, {5004,"Arena Bludgeoning Protection Other"}, {5005,"Arena Cold Protection Other"}, {5006,"Arena Fire Protection Other"}, {5007,"Arena Lightning Protection Other"}, {5008,"Apostate Nexus Portal Sending"}, {5009,"Aerfalle's Greater Ward"}, {5010,"Entering Aerfalle's Sanctum"}, {5011,"Geomantic Raze"}, {5012,"Mar'uun"}, {5013,"Mar'uun"}, {5014,"Mar'uun"}, {5015,"Mar'uun"}, {5016,"Mar'uun"}, {5017,"Mar'uun"}, {5018,"Story of the Unknown Warrior"}, {5019,"Portalspace Rift"}, {5020,"Portalspace Rift"}, {5021,"Portalspace Rift"}, {5022,"Portalspace Rift"}, {5023,"Spectral Two Handed Combat Mastery"}, {5024,"Spectral Item Expertise"}, {5025,"Prodigal Item Expertise"}, {5026,"Prodigal Two Handed Combat Mastery"}, {5027,"Greater Cascade"}, {5028,"Lesser Cascade"}, {5029,"Cascade"}, {5030,"Two Handed Fighter's Boon"}, {5031,"Two Handed Fighter's Boon"}, {5032,"Incantation of Two Handed Combat Mastery Self"}, {5033,"Epic Item Tinkering Expertise"}, {5034,"Epic Two Handed Combat Aptitude"}, {5035,"Feeble Sword Aptitude"}, {5036,"Feeble Two Handed Combat Aptitude"}, {5037,"Item Tinkering Ignorance Other I"}, {5038,"Item Tinkering Ignorance Other II"}, {5039,"Item Tinkering Ignorance Other III"}, {5040,"Item Tinkering Ignorance Other IV"}, {5041,"Item Tinkering Ignorance Other V"}, {5042,"Item Tinkering Ignorance Other VI"}, {5043,"Unfortunate Appraisal"}, {5044,"Incantation of Item Tinkering Ignorance Other"}, {5045,"Item Tinkering Ignorance Self I"}, {5046,"Item Tinkering Ignorance Self II"}, {5047,"Item Tinkering Ignorance Self III"}, {5048,"Item Tinkering Ignorance Self IV"}, {5049,"Item Tinkering Ignorance Self V"}, {5050,"Item Tinkering Ignorance Self VI"}, {5051,"Item Tinkering Ignorance Self VII"}, {5052,"Incantation of Item Tinkering Ignorance Self"}, {5053,"Item Tinkering Expertise Other I"}, {5054,"Item Tinkering Expertise Other II"}, {5055,"Item Tinkering Expertise Other III"}, {5056,"Item Tinkering Expertise Other IV"}, {5057,"Item Tinkering Expertise Other V"}, {5058,"Item Tinkering Expertise Other VI"}, {5059,"Yoshi's Boon"}, {5060,"Incantation of Item Tinkering Expertise Other"}, {5061,"Item Tinkering Expertise Self I"}, {5062,"Item Tinkering Expertise Self II"}, {5063,"Item Tinkering Expertise Self III"}, {5064,"Item Tinkering Expertise Self IV"}, {5065,"Item Tinkering Expertise Self V"}, {5066,"Item Tinkering Expertise Self VI"}, {5067,"Yoshi's Blessing"}, {5068,"Incantation of Item Tinkering Expertise Self"}, {5069,"Major Item Tinkering Expertise"}, {5070,"Major Two Handed Combat Aptitude"}, {5071,"Minor Item Tinkering Expertise"}, {5072,"Minor Two Handed Combat Aptitude"}, {5073,"Moderate Item Tinkering Expertise"}, {5074,"Moderate Two Handed Combat Aptitude"}, {5075,"Two Handed Combat Ineptitude Other I"}, {5076,"Two Handed Combat Ineptitude Other II"}, {5077,"Two Handed Combat Ineptitude Other III"}, {5078,"Two Handed Combat Ineptitude Other IV"}, {5079,"Two Handed Combat Ineptitude Other V"}, {5080,"Two Handed Combat Ineptitude Other VI"}, {5081,"Greased Palms"}, {5082,"Incantation of Two Handed Combat Ineptitude Other"}, {5083,"Two Handed Combat Ineptitude Self I"}, {5084,"Two Handed Combat Ineptitude Self II"}, {5085,"Two Handed Combat Ineptitude Self III"}, {5086,"Two Handed Combat Ineptitude Self IV"}, {5087,"Two Handed Combat Ineptitude Self V"}, {5088,"Two Handed Combat Ineptitude Self VI"}, {5089,"Two Handed Combat Ineptitude Self VII"}, {5090,"Incantation of Two Handed Combat Ineptitude Self"}, {5091,"Two Handed Combat Mastery Other I"}, {5092,"Two Handed Combat Mastery Other II"}, {5093,"Two Handed Combat Mastery Other III"}, {5094,"Two Handed Combat Mastery Other IV"}, {5095,"Two Handed Combat Mastery Other V"}, {5096,"Two Handed Combat Mastery Other VI"}, {5097,"Boon of T'ing"}, {5098,"Incantation of Two Handed Combat Mastery Other"}, {5099,"Two Handed Combat Mastery Self I"}, {5100,"Two Handed Combat Mastery Self II"}, {5101,"Two Handed Combat Mastery Self III"}, {5102,"Two Handed Combat Mastery Self IV"}, {5103,"Two Handed Combat Mastery Self V"}, {5104,"Two Handed Combat Mastery Self VI"}, {5105,"Blessing of T'ing"}, {5106,"Master Inventor's Item Tinkering Aptitude"}, {5107,"Novice Soldier's Two Handed Combat Aptitude"}, {5108,"Apprentice Soldier's Two Handed Combat Aptitude"}, {5109,"Journeyman Soldier's Two Handed Combat Aptitude"}, {5110,"Master Soldier's Two Handed Combat Aptitude"}, {5111,"Novice Inventor's Item Tinkering Aptitude"}, {5112,"Apprentice Inventor's Item Tinkering Aptitude"}, {5113,"Journeyman Inventor's Item Tinkering Aptitude"}, {5114,"Expose Weakness VIII"}, {5115,"Expose Weakness I"}, {5116,"Expose Weakness II"}, {5117,"Expose Weakness III"}, {5118,"Expose Weakness IV"}, {5119,"Expose Weakness V"}, {5120,"Expose Weakness VI"}, {5121,"Expose Weakness VII"}, {5122,"Call of Leadership V"}, {5123,"Answer of Loyalty (Mana) I"}, {5124,"Answer of Loyalty (Mana) II"}, {5125,"Answer of Loyalty (Mana) III"}, {5126,"Answer of Loyalty (Mana) IV"}, {5127,"Answer of Loyalty (Mana) V"}, {5128,"Answer of Loyalty (Stamina) I"}, {5129,"Answer of Loyalty (Stamina) II"}, {5130,"Answer of Loyalty (Stamina) III"}, {5131,"Answer of Loyalty (Stamina) IV"}, {5132,"Answer of Loyalty (Stamina) V"}, {5133,"Call of Leadership I"}, {5134,"Call of Leadership II"}, {5135,"Call of Leadership III"}, {5136,"Call of Leadership IV"}, {5137,"Augmented Understanding III"}, {5138,"Augmented Damage I"}, {5139,"Augmented Damage II"}, {5140,"Augmented Damage III"}, {5141,"Augmented Damage Reduction I"}, {5142,"Augmented Damage Reduction II"}, {5143,"Augmented Damage Reduction III"}, {5144,"Augmented Health I"}, {5145,"Augmented Health II"}, {5146,"Augmented Health III"}, {5147,"Augmented Mana I"}, {5148,"Augmented Mana II"}, {5149,"Augmented Mana III"}, {5150,"Augmented Stamina I"}, {5151,"Augmented Stamina II"}, {5152,"Augmented Stamina III"}, {5153,"Augmented Understanding I"}, {5154,"Augmented Understanding II"}, {5155,"Virindi Whisper IV"}, {5156,"Virindi Whisper V"}, {5157,"Virindi Whisper I"}, {5158,"Virindi Whisper II"}, {5159,"Virindi Whisper III"}, {5160,"Mhoire Castle"}, {5161,"Mhoire Castle Great Hall"}, {5162,"Mhoire Castle Northeast Tower"}, {5163,"Mhoire Castle Northwest Tower"}, {5164,"Mhoire Castle Southeast Tower"}, {5165,"Mhoire Castle Southwest Tower"}, {5166,"Flaming Skull"}, {5167,"Mhoire Castle Exit Portal"}, {5168,"a spectacular view of the Mhoire lands"}, {5169,"a descent into the Mhoire catacombs"}, {5170,"a descent into the Mhoire catacombs"}, {5171,"Spectral Fountain Sip"}, {5172,"Spectral Fountain Sip"}, {5173,"Spectral Fountain Sip"}, {5174,"Mhoire's Blessing of Power"}, {5175,"Facility Hub Recall"}, {5176,"Celestial Hand Basement"}, {5177,"Radiant Blood Basement"}, {5178,"Eldrytch Web Basement"}, {5179,"Celestial Hand Basement"}, {5180,"Radiant Blood Basement"}, {5181,"Eldrytch Web Basement"}, {5182,"Aura of Incantation of Spirit Drinker"}, {5183,"Aura of Incantation of Blood Drinker Self"}, {5184,"Rare Damage Boost VII"}, {5185,"Rare Damage Boost VIII"}, {5186,"Rare Damage Boost IX"}, {5187,"Rare Damage Boost X"}, {5188,"Rare Damage Reduction I"}, {5189,"Rare Damage Reduction II"}, {5190,"Rare Damage Reduction III"}, {5191,"Rare Damage Reduction IV"}, {5192,"Rare Damage Reduction V"}, {5193,"Rare Damage Reduction V"}, {5194,"Rare Damage Reduction V"}, {5195,"Rare Damage Reduction V"}, {5196,"Rare Damage Reduction V"}, {5197,"Rare Damage Reduction V"}, {5198,"Rare Damage Boost I"}, {5199,"Rare Damage Boost II"}, {5200,"Rare Damage Boost III"}, {5201,"Rare Damage Boost IV"}, {5202,"Rare Damage Boost V"}, {5203,"Rare Damage Boost VI"}, {5204,"Surge of Destruction"}, {5205,"Surge of Affliction"}, {5206,"Surge of Protection"}, {5207,"Surge of Festering"}, {5208,"Surge of Regeneration"}, {5209,"Sigil of Fury I (Critical Damage)"}, {5210,"Sigil of Fury II (Critical Damage)"}, {5211,"Sigil of Fury III (Critical Damage)"}, {5212,"Sigil of Fury IV (Critical Damage)"}, {5213,"Sigil of Fury V (Critical Damage)"}, {5214,"Sigil of Fury VI (Critical Damage)"}, {5215,"Sigil of Fury VII (Critical Damage)"}, {5216,"Sigil of Fury VIII (Critical Damage)"}, {5217,"Sigil of Fury IX (Critical Damage)"}, {5218,"Sigil of Fury X (Critical Damage)"}, {5219,"Sigil of Fury XI (Critical Damage)"}, {5220,"Sigil of Fury XII (Critical Damage)"}, {5221,"Sigil of Fury XIII (Critical Damage)"}, {5222,"Sigil of Fury XIV (Critical Damage)"}, {5223,"Sigil of Fury XV (Critical Damage)"}, {5224,"Sigil of Destruction I"}, {5225,"Sigil of Destruction II"}, {5226,"Sigil of Destruction III"}, {5227,"Sigil of Destruction IV"}, {5228,"Sigil of Destruction V"}, {5229,"Sigil of Destruction VI"}, {5230,"Sigil of Destruction VII"}, {5231,"Sigil of Destruction VIII"}, {5232,"Sigil of Destruction IX"}, {5233,"Sigil of Destruction X"}, {5234,"Sigil of Destruction XI"}, {5235,"Sigil of Destruction XII"}, {5236,"Sigil of Destruction XIII"}, {5237,"Sigil of Destruction XIV"}, {5238,"Sigil of Destruction XV"}, {5239,"Sigil of Defense I"}, {5240,"Sigil of Defense II"}, {5241,"Sigil of Defense III"}, {5242,"Sigil of Defense IV"}, {5243,"Sigil of Defense V"}, {5244,"Sigil of Defense VI"}, {5245,"Sigil of Defense VII"}, {5246,"Sigil of Defense VIII"}, {5247,"Sigil of Defense IX"}, {5248,"Sigil of Defense X"}, {5249,"Sigil of Defense XI"}, {5250,"Sigil of Defense XII"}, {5251,"Sigil of Defense XIII"}, {5252,"Sigil of Defense XIV"}, {5253,"Sigil of Defense XV"}, {5254,"Sigil of Growth I"}, {5255,"Sigil of Growth II"}, {5256,"Sigil of Growth III"}, {5257,"Sigil of Growth IV"}, {5258,"Sigil of Growth V"}, {5259,"Sigil of Growth VI"}, {5260,"Sigil of Growth VII"}, {5261,"Sigil of Growth VIII"}, {5262,"Sigil of Growth IX"}, {5263,"Sigil of Growth X"}, {5264,"Sigil of Growth XI"}, {5265,"Sigil of Growth XII"}, {5266,"Sigil of Growth XIII"}, {5267,"Sigil of Growth XIV"}, {5268,"Sigil of Growth XV"}, {5269,"Sigil of Vigor I (Health)"}, {5270,"Sigil of Vigor II (Health)"}, {5271,"Sigil of Vigor III (Health)"}, {5272,"Sigil of Vigor IV (Health)"}, {5273,"Sigil of Vigor V (Health)"}, {5274,"Sigil of Vigor VI (Health)"}, {5275,"Sigil of Vigor VII (Health)"}, {5276,"Sigil of Vigor VIII (Health)"}, {5277,"Sigil of Vigor IX (Health)"}, {5278,"Sigil of Vigor X (Health)"}, {5279,"Sigil of Vigor XI (Health)"}, {5280,"Sigil of Vigor XII (Health)"}, {5281,"Sigil of Vigor XIII (Health)"}, {5282,"Sigil of Vigor XIV (Health)"}, {5283,"Sigil of Vigor XV (Health)"}, {5284,"Sigil of Vigor I (Mana)"}, {5285,"Sigil of Vigor II (Mana)"}, {5286,"Sigil of Vigor III (Mana)"}, {5287,"Sigil of Vigor IV (Mana)"}, {5288,"Sigil of Vigor V (Mana)"}, {5289,"Sigil of Vigor VI (Mana)"}, {5290,"Sigil of Vigor VII (Mana)"}, {5291,"Sigil of Vigor VIII (Mana)"}, {5292,"Sigil of Vigor IX (Mana)"}, {5293,"Sigil of Vigor X (Mana)"}, {5294,"Sigil of Vigor XI (Mana)"}, {5295,"Sigil of Vigor XII (Mana)"}, {5296,"Sigil of Vigor XIII (Mana)"}, {5297,"Sigil of Vigor XIV (Mana)"}, {5298,"Sigil of Vigor XV (Mana)"}, {5299,"Sigil of Vigor I (Stamina)"}, {5300,"Sigil of Vigor II (Stamina)"}, {5301,"Sigil of Vigor III (Stamina)"}, {5302,"Sigil of Vigor IV (Stamina)"}, {5303,"Sigil of Vigor V (Stamina)"}, {5304,"Sigil of Vigor VI (Stamina)"}, {5305,"Sigil of Vigor VII (Stamina)"}, {5306,"Sigil of Vigor VIII (Stamina)"}, {5307,"Sigil of Vigor IX (Stamina)"}, {5308,"Sigil of Vigor X (Stamina)"}, {5309,"Sigil of Vigor XI (Stamina)"}, {5310,"Sigil of Vigor XII (Stamina)"}, {5311,"Sigil of Vigor XIII (Stamina)"}, {5312,"Sigil of Vigor XIV (Stamina)"}, {5313,"Sigil of Vigor XV (Stamina)"}, {5314,"Blessing of Unity"}, {5315,"Sigil of Fury I (Endurance)"}, {5316,"Sigil of Fury II (Endurance)"}, {5317,"Sigil of Fury III (Endurance)"}, {5318,"Sigil of Fury IV (Endurance)"}, {5319,"Sigil of Fury V (Endurance)"}, {5320,"Sigil of Fury VI (Endurance)"}, {5321,"Sigil of Fury VII (Endurance)"}, {5322,"Sigil of Fury VIII (Endurance)"}, {5323,"Sigil of Fury IX (Endurance)"}, {5324,"Sigil of Fury X (Endurance)"}, {5325,"Sigil of Fury XI (Endurance)"}, {5326,"Sigil of Fury XII (Endurance)"}, {5327,"Sigil of Fury XIII (Endurance)"}, {5328,"Sigil of Fury XIV (Endurance)"}, {5329,"Sigil of Fury XV (Endurance)"}, {5330,"Gear Knight Invasion Area Camp Recall"}, {5331,"Clouded Soul"}, {5332,"Bael'zharon's Nether Streak"}, {5333,"Bael'zharon's Nether Arc"}, {5334,"Bael'zharons Curse of Destruction"}, {5335,"Bael'zharons Curse of Minor Destruction"}, {5336,"Bael'zharons Curse of Festering"}, {5337,"Destructive Curse VII"}, {5338,"Incantation of Destructive Curse"}, {5339,"Destructive Curse I"}, {5340,"Destructive Curse II"}, {5341,"Destructive Curse III"}, {5342,"Destructive Curse IV"}, {5343,"Destructive Curse V"}, {5344,"Destructive Curse VI"}, {5345,"Nether Streak V"}, {5346,"Nether Streak VI"}, {5347,"Nether Streak VII"}, {5348,"Incantation of Nether Streak"}, {5349,"Nether Bolt I"}, {5350,"Nether Bolt II"}, {5351,"Nether Bolt III"}, {5352,"Nether Bolt IV"}, {5353,"Nether Bolt V"}, {5354,"Nether Bolt VI"}, {5355,"Nether Bolt VII"}, {5356,"Incantation of Nether Bolt"}, {5357,"Nether Streak I"}, {5358,"Nether Streak II"}, {5359,"Nether Streak III"}, {5360,"Nether Streak IV"}, {5361,"Clouded Soul"}, {5362,"Nether Arc II"}, {5363,"Nether Arc III"}, {5364,"Nether Arc IV"}, {5365,"Nether Arc V"}, {5366,"Nether Arc VI"}, {5367,"Nether Arc VII"}, {5368,"Incantation of Nether Arc"}, {5369,"Nether Arc I"}, {5370,"Incantation of Nether Streak"}, {5371,"Festering Curse I"}, {5372,"Festering Curse II"}, {5373,"Festering Curse III"}, {5374,"Festering Curse IV"}, {5375,"Festering Curse V"}, {5376,"Festering Curse VI"}, {5377,"Festering Curse VII"}, {5378,"Incantation of Festering Curse"}, {5379,"Weakening Curse I"}, {5380,"Weakening Curse II"}, {5381,"Weakening Curse III"}, {5382,"Weakening Curse IV"}, {5383,"Weakening Curse V"}, {5384,"Weakening Curse VI"}, {5385,"Weakening Curse VII"}, {5386,"Incantation of Weakening Curse"}, {5387,"Corrosion I"}, {5388,"Corrosion II"}, {5389,"Corrosion III"}, {5390,"Corrosion IV"}, {5391,"Corrosion V"}, {5392,"Corrosion VI"}, {5393,"Corrosion VII"}, {5394,"Incantation of Corrosion"}, {5395,"Corruption I"}, {5396,"Corruption II"}, {5397,"Corruption III"}, {5398,"Corruption IV"}, {5399,"Corruption V"}, {5400,"Corruption VI"}, {5401,"Corruption VII"}, {5402,"Incantation of Corruption"}, {5403,"Void Magic Mastery Other I"}, {5404,"Void Magic Mastery Other II"}, {5405,"Void Magic Mastery Other III"}, {5406,"Void Magic Mastery Other IV"}, {5407,"Void Magic Mastery Other V"}, {5408,"Void Magic Mastery Other VI"}, {5409,"Void Magic Mastery Other VII"}, {5410,"Incantation of Void Magic Mastery Other"}, {5411,"Void Magic Mastery Self I"}, {5412,"Void Magic Mastery Self II"}, {5413,"Void Magic Mastery Self III"}, {5414,"Void Magic Mastery Self IV"}, {5415,"Void Magic Mastery Self V"}, {5416,"Void Magic Mastery Self VI"}, {5417,"Void Magic Mastery Self VII"}, {5418,"Incantation of Void Magic Mastery Self"}, {5419,"Void Magic Ineptitude Other I"}, {5420,"Void Magic Ineptitude Other II"}, {5421,"Void Magic Ineptitude Other III"}, {5422,"Void Magic Ineptitude Other IV"}, {5423,"Void Magic Ineptitude Other V"}, {5424,"Void Magic Ineptitude Other VI"}, {5425,"Void Magic Ineptitude Other VII"}, {5426,"Incantation of Void Magic Ineptitude Other"}, {5427,"Minor Void Magic Aptitude"}, {5428,"Major Void Magic Aptitude"}, {5429,"Epic Void Magic Aptitude"}, {5430,"Moderate Void Magic Aptitude"}, {5431,"Novice Shadow's Void Magic Aptitude"}, {5432,"Apprentice Voidlock's Void Magic Aptitude"}, {5433,"Journeyman Voidlock's Void Magic Aptitude"}, {5434,"Master Voidlock's Void Magic Aptitude"}, {5435,"Spectral Void Magic Mastery"}, {5436,"Prodigal Void Magic Mastery"}, {5437,"Corruptor's Boon"}, {5438,"Corruptor's Boon"}, {5439,"Acid Spit Streak 1"}, {5440,"Acid Spit 1"}, {5441,"Acid Spit 2"}, {5442,"Acid Spit Arc 1"}, {5443,"Acid Spit Arc 2"}, {5444,"Acid Spit Blast 1"}, {5445,"Acid Spit Blast 2"}, {5446,"Acid Spit Volley 1"}, {5447,"Acid Spit Volley 2"}, {5448,"Acid Spit Streak"}, {5449,"Surging Strength"}, {5450,"Towering Defense"}, {5451,"Luminous Vitality"}, {5452,"Queen's Willpower"}, {5453,"Queen's Armor"}, {5454,"Queen's Coordination"}, {5455,"Queen's Endurance"}, {5456,"Queen's Focus"}, {5457,"Queen's Quickness"}, {5458,"Queen's Strength"}, {5459,"Queen's Piercing Protection"}, {5460,"Queen's Acid Protection"}, {5461,"Queen's Blade Protection"}, {5462,"Queen's Bludgeoning Protection"}, {5463,"Queen's Cold Protection"}, {5464,"Queen's Fire Protection"}, {5465,"Queen's Lightning Protection"}, {5466,"Queen's Rejuvenation"}, {5467,"Queen's Mana Renewal"}, {5468,"Queen's Regeneration"}, {5469,"Queen's Impregnability Other"}, {5470,"Queen's Invulnerability Other"}, {5471,"Queen's Magic Resistance"}, {5472,"Queen's Mana Conversion Mastery"}, {5473,"Queen's Salvaging Mastery Other"}, {5474,"Queen's Sprint"}, {5475,"Queen's Light Weapon Mastery"}, {5476,"Queen's War Magic Mastery"}, {5477,"Critical Damage Metamorphi I"}, {5478,"Critical Damage Metamorphi II"}, {5479,"Critical Damage Metamorphi III"}, {5480,"Critical Damage Metamorphi IV"}, {5481,"Critical Damage Metamorphi V"}, {5482,"Critical Damage Metamorphi VI"}, {5483,"Critical Damage Metamorphi VII"}, {5484,"Critical Damage Metamorphi VIII"}, {5485,"Critical Damage Metamorphi IX"}, {5486,"Critical Damage Metamorphi X"}, {5487,"Critical Damage Metamorphi XI"}, {5489,"Critical Damage Reduction Metamorphi I"}, {5490,"Critical Damage Reduction Metamorphi II"}, {5491,"Critical Damage Reduction Metamorphi III"}, {5492,"Critical Damage Reduction Metamorphi IV"}, {5493,"Critical Damage Reduction Metamorphi V"}, {5494,"Critical Damage Reduction Metamorphi VI"}, {5495,"Critical Damage Reduction Metamorphi VII"}, {5496,"Critical Damage Reduction Metamorphi VIII"}, {5497,"Critical Damage Reduction Metamorphi IX"}, {5498,"Critical Damage Reduction Metamorphi X"}, {5499,"Critical Damage Reduction Metamorphi XI"}, {5500,"Damage Metamorphi I"}, {5501,"Damage Metamorphi II"}, {5502,"Damage Metamorphi III"}, {5503,"Damage Metamorphi IV"}, {5504,"Damage Metamorphi V"}, {5505,"Damage Metamorphi VI"}, {5506,"Damage Metamorphi VII"}, {5507,"Damage Metamorphi VIII"}, {5508,"Damage Metamorphi IX"}, {5509,"Damage Metamorphi X"}, {5510,"Damage Metamorphi XI"}, {5511,"Damage Reduction Metamorphi I"}, {5512,"Damage Reduction Metamorphi II"}, {5513,"Damage Reduction Metamorphi III"}, {5514,"Damage Reduction Metamorphi IV"}, {5515,"Damage Reduction Metamorphi V"}, {5516,"Damage Reduction Metamorphi VI"}, {5517,"Damage Reduction Metamorphi VII"}, {5518,"Damage Reduction Metamorphi VIII"}, {5519,"Damage Reduction Metamorphi IX"}, {5520,"Damage Reduction Metamorphi X"}, {5521,"Damage Reduction Metamorphi XI"}, {5522,"Acid Spit Vulnerability 1"}, {5523,"Acid Spit Vulnerability 2"}, {5524,"Falling stalactite"}, {5525,"Bloodstone Bolt I"}, {5526,"Bloodstone Bolt II"}, {5527,"Bloodstone Bolt III"}, {5528,"Bloodstone Bolt IV"}, {5529,"Bloodstone Bolt V"}, {5530,"Bloodstone Bolt VI"}, {5531,"Bloodstone Bolt VII"}, {5532,"Incantation of Bloodstone Bolt"}, {5533,"Entering Lord Kastellar's Lab"}, {5534,"Entering the Bloodstone Factory"}, {5535,"Acidic Blood"}, {5536,"Acidic Blood"}, {5537,"Acidic Blood"}, {5538,"Darkened Heart"}, {5539,"Warded Cavern Passage"}, {5540,"Warded Dungeon Passage"}, {5541,"Lost City of Neftet Recall"}, {5542,"Burning Sands Infliction"}, {5543,"Curse of the Burning Sands"}, {5544,"Nether Blast I"}, {5545,"Nether Blast II"}, {5546,"Nether Blast III"}, {5547,"Nether Blast IV"}, {5548,"Nether Blast V"}, {5549,"Nether Blast VI"}, {5550,"Nether Blast VII"}, {5551,"Incantation of Nether Blast"}, {5552,"Sigil of Purity IX"}, {5553,"Sigil of Perserverance I"}, {5554,"Sigil of Perserverance X"}, {5555,"Sigil of Perserverance XI"}, {5556,"Sigil of Perserverance XII"}, {5557,"Sigil of Perserverance XIII"}, {5558,"Sigil of Perserverance XIV"}, {5559,"Sigil of Perserverance XV"}, {5560,"Sigil of Perserverance II"}, {5561,"Sigil of Perserverance III"}, {5562,"Sigil of Perserverance IV"}, {5563,"Sigil of Perserverance V"}, {5564,"Sigil of Perserverance VI"}, {5565,"Sigil of Perserverance VII"}, {5566,"Sigil of Perserverance VIII"}, {5567,"Sigil of Perserverance IX"}, {5568,"Sigil of Purity I"}, {5569,"Sigil of Purity X"}, {5570,"Sigil of Purity XI"}, {5571,"Sigil of Purity XII"}, {5572,"Sigil of Purity XIII"}, {5573,"Sigil of Purity XIV"}, {5574,"Sigil of Purity XV"}, {5575,"Sigil of Purity II"}, {5576,"Sigil of Purity III"}, {5577,"Sigil of Purity IV"}, {5578,"Sigil of Purity V"}, {5579,"Sigil of Purity VI"}, {5580,"Sigil of Purity VII"}, {5581,"Sigil of Purity VIII"}, {5582,"Nullify All Rares"}, {5583,"Weave of Alchemy I"}, {5584,"Weave of Alchemy II"}, {5585,"Weave of Alchemy III"}, {5586,"Weave of Alchemy IV"}, {5587,"Weave of Alchemy V"}, {5588,"Weave of Arcane Lore I"}, {5589,"Weave of Arcane Lore II"}, {5590,"Weave of Arcane Lore III"}, {5591,"Weave of Arcane Lore IV"}, {5592,"Weave of Arcane Lore V"}, {5593,"Weave of Armor Tinkering I"}, {5594,"Weave of Armor Tinkering II"}, {5595,"Weave of Armor Tinkering III"}, {5596,"Weave of Armor Tinkering IV"}, {5597,"Weave of Armor Tinkering V"}, {5598,"Weave of Person Attunement I"}, {5599,"Weave of Person Attunement II"}, {5600,"Weave of Person Attunement III"}, {5601,"Weave of Person Attunement IV"}, {5602,"Weave of the Person Attunement V"}, {5603,"Weave of Light Weapons I"}, {5604,"Weave of Light Weapons II"}, {5605,"Weave of Light Weapons III"}, {5606,"Weave of Light Weapons IV"}, {5607,"Weave of Light Weapons V"}, {5608,"Weave of Missile Weapons I"}, {5609,"Weave of Missile Weapons II"}, {5610,"Weave of Missile Weapons III"}, {5611,"Weave of Missile Weapons IV"}, {5612,"Weave of Missile Weapons V"}, {5613,"Weave of Cooking I"}, {5614,"Weave of Cooking II"}, {5615,"Weave of Cooking III"}, {5616,"Weave of Cooking IV"}, {5617,"Weave of the Cooking V"}, {5618,"Weave of Creature Enchantment I"}, {5619,"Weave of Creature Enchantment II"}, {5620,"Weave of Creature Enchantment III"}, {5621,"Weave of Creature Enchantment IV"}, {5622,"Weave of the Creature Enchantment V"}, {5623,"Weave of Missile Weapons I"}, {5624,"Weave of Missile Weapons II"}, {5625,"Weave of Missile Weapons III"}, {5626,"Weave of Missile Weapons IV"}, {5627,"Weave of Missile Weapons V"}, {5628,"Weave of Finesse Weapons I"}, {5629,"Weave of Finesse Weapons II"}, {5630,"Weave of Finesse Weapons III"}, {5631,"Weave of Finesse Weapons IV"}, {5632,"Weave of Finesse Weapons V"}, {5633,"Weave of Deception I"}, {5634,"Weave of the Deception II"}, {5635,"Weave of the Deception III"}, {5636,"Weave of the Deception IV"}, {5637,"Weave of the Deception V"}, {5638,"Weave of Fletching I"}, {5639,"Weave of the Fletching II"}, {5640,"Weave of the Fletching III"}, {5641,"Weave of the Fletching IV"}, {5642,"Weave of the Fletching V"}, {5643,"Weave of Healing I"}, {5644,"Weave of the Healing II"}, {5645,"Weave of the Healing III"}, {5646,"Weave of the Healing IV"}, {5647,"Weave of the Healing V"}, {5648,"Weave of Item Enchantment I"}, {5649,"Weave of Item Enchantment II"}, {5650,"Weave of Item Enchantment III"}, {5651,"Weave of Item Enchantment IV"}, {5652,"Weave of the Item Enchantment V"}, {5653,"Weave of Item Tinkering I"}, {5654,"Weave of Item Tinkering II"}, {5655,"Weave of Item Tinkering III"}, {5656,"Weave of Item Tinkering IV"}, {5657,"Weave of the Item Tinkering V"}, {5658,"Weave of Leadership I"}, {5659,"Weave of Leadership II"}, {5660,"Weave of Leadership III"}, {5661,"Weave of Leadership IV"}, {5662,"Weave of Leadership V"}, {5663,"Weave of Life Magic I"}, {5664,"Weave of Life Magic II"}, {5665,"Weave of Life Magic III"}, {5666,"Weave of Life Magic IV"}, {5667,"Weave of Life Magic V"}, {5668,"Weave of Fealty I"}, {5669,"Weave of Fealty II"}, {5670,"Weave of Fealty III"}, {5671,"Weave of Fealty IV"}, {5672,"Weave of Fealty V"}, {5673,"Weave of Light Weapons I"}, {5674,"Weave of Light Weapons II"}, {5675,"Weave of Light Weapons III"}, {5676,"Weave of Light Weapons IV"}, {5677,"Weave of Light Weapons V"}, {5678,"Weave of Magic Resistance I"}, {5679,"Weave of Magic Resistance II"}, {5680,"Weave of Magic Resistance III"}, {5681,"Weave of Magic Resistance IV"}, {5682,"Weave of the Magic Resistance V"}, {5683,"Weave of Magic Item Tinkering I"}, {5684,"Weave of Magic Item Tinkering II"}, {5685,"Weave of Magic Item Tinkering III"}, {5686,"Weave of Magic Item Tinkering IV"}, {5687,"Weave of the Magic Item Tinkering V"}, {5688,"Weave of Mana Conversion I"}, {5689,"Weave of Mana Conversion II"}, {5690,"Weave of Mana Conversion III"}, {5691,"Weave of Mana Conversion IV"}, {5692,"Weave of Mana Conversion V"}, {5693,"Weave of Invulnerability I"}, {5694,"Weave of Invulnerability II"}, {5695,"Weave of Invulnerability III"}, {5696,"Weave of Invulnerability IV"}, {5697,"Weave of the Invulnerability V"}, {5698,"Weave of Impregnability I"}, {5699,"Weave of Impregnability II"}, {5700,"Weave of Impregnability III"}, {5701,"Weave of Impregnability IV"}, {5702,"Weave of the Impregnability V"}, {5703,"Weave of Salvaging I"}, {5704,"Weave of Salvaging II"}, {5705,"Weave of Salvaging III"}, {5706,"Weave of Salvaging IV"}, {5707,"Weave of Salvaging V"}, {5708,"Weave of Light Weapons I"}, {5709,"Weave of Light Weapons II"}, {5710,"Weave of Light Weapons III"}, {5711,"Weave of Light Weapons IV"}, {5712,"Weave of Light Weapons V"}, {5713,"Weave of Light Weapons I"}, {5714,"Weave of Light Weapons II"}, {5715,"Weave of Light Weapons III"}, {5716,"Weave of Light Weapons IV"}, {5717,"Weave of Light Weapons V"}, {5718,"Weave of Heavy Weapons I"}, {5719,"Weave of Heavy Weapons II"}, {5720,"Weave of Heavy Weapons III"}, {5721,"Weave of Heavy Weapons IV"}, {5722,"Weave of Heavy Weapons V"}, {5723,"Weave of Missile Weapons I"}, {5724,"Weave of Missile Weapons II"}, {5725,"Weave of Missile Weapons III"}, {5726,"Weave of Missile Weapons IV"}, {5727,"Weave of Missile Weapons V"}, {5728,"Weave of Two Handed Combat I"}, {5729,"Weave of Two Handed Combat II"}, {5730,"Weave of Two Handed Combat III"}, {5731,"Weave of Two Handed Combat IV"}, {5732,"Weave of Two Handed Combat V"}, {5733,"Weave of Light Weapons I"}, {5734,"Weave of Light Weapons II"}, {5735,"Weave of Light Weapons III"}, {5736,"Weave of Light Weapons IV"}, {5737,"Weave of Light Weapons V"}, {5738,"Weave of Void Magic I"}, {5739,"Weave of Void Magic II"}, {5740,"Weave of Void Magic III"}, {5741,"Weave of Void Magic IV"}, {5742,"Weave of Void Magic V"}, {5743,"Weave of War Magic I"}, {5744,"Weave of War Magic II"}, {5745,"Weave of War Magic III"}, {5746,"Weave of War Magic IV"}, {5747,"Weave of War Magic V"}, {5748,"Weave of Weapon Tinkering I"}, {5749,"Weave of Weapon Tinkering II"}, {5750,"Weave of Weapon Tinkering III"}, {5751,"Weave of Weapon Tinkering IV"}, {5752,"Weave of the Weapon Tinkering V"}, {5753,"Cloaked in Skill"}, {5754,"Shroud of Darkness (Magic)"}, {5755,"Shroud of Darkness (Melee)"}, {5756,"Shroud of Darkness (Missile)"}, {5757,"Weave of Creature Attunement III"}, {5758,"Weave of Creature Attunement IV"}, {5759,"Weave of the Creature Attunement V"}, {5760,"Weave of Creature Attunement I"}, {5761,"Weave of Creature Attunement II"}, {5762,"Rolling Death"}, {5763,"Dirty Fighting Ineptitude Other I"}, {5764,"Dirty Fighting Ineptitude Other II"}, {5765,"Dirty Fighting Ineptitude Other III"}, {5766,"Dirty Fighting Ineptitude Other IV"}, {5767,"Dirty Fighting Ineptitude Other V"}, {5768,"Dirty Fighting Ineptitude Other VI"}, {5769,"Dirty Fighting Ineptitude Other VII"}, {5770,"Incantation of Dirty Fighting Ineptitude Other"}, {5771,"Dirty Fighting Mastery Other I"}, {5772,"Dirty Fighting Mastery Other II"}, {5773,"Dirty Fighting Mastery Other III"}, {5774,"Dirty Fighting Mastery Other IV"}, {5775,"Dirty Fighting Mastery Other V"}, {5776,"Dirty Fighting Mastery Other VI"}, {5777,"Dirty Fighting Mastery Other VII"}, {5778,"Incantation of Dirty Fighting Mastery Other"}, {5779,"Dirty Fighting Mastery Self I"}, {5780,"Dirty Fighting Mastery Self II"}, {5781,"Dirty Fighting Mastery Self III"}, {5782,"Dirty Fighting Mastery Self IV"}, {5783,"Dirty Fighting Mastery Self V"}, {5784,"Dirty Fighting Mastery Self VI"}, {5785,"Dirty Fighting Mastery Self VII"}, {5786,"Incantation of Dirty Fighting Mastery Self"}, {5787,"Dual Wield Ineptitude Other I"}, {5788,"Dual Wield Ineptitude Other II"}, {5789,"Dual Wield Ineptitude Other III"}, {5790,"Dual Wield Ineptitude Other IV"}, {5791,"Dual Wield Ineptitude Other V"}, {5792,"Dual Wield Ineptitude Other VI"}, {5793,"Dual Wield Ineptitude Other VII"}, {5794,"Incantation of Dual Wield Ineptitude Other"}, {5795,"Dual Wield Mastery Other I"}, {5796,"Dual Wield Mastery Other II"}, {5797,"Dual Wield Mastery Other III"}, {5798,"Dual Wield Mastery Other IV"}, {5799,"Dual Wield Mastery Other V"}, {5800,"Dual Wield Mastery Other VI"}, {5801,"Dual Wield Mastery Other VII"}, {5802,"Incantation of Dual Wield Mastery Other"}, {5803,"Dual Wield Mastery Self I"}, {5804,"Dual Wield Mastery Self II"}, {5805,"Dual Wield Mastery Self III"}, {5806,"Dual Wield Mastery Self IV"}, {5807,"Dual Wield Mastery Self V"}, {5808,"Dual Wield Mastery Self VI"}, {5809,"Dual Wield Mastery Self VII"}, {5810,"Incantation of Dual Wield Mastery Self"}, {5811,"Recklessness Ineptitude Other I"}, {5812,"Recklessness Ineptitude Other II"}, {5813,"Recklessness Ineptitude Other III"}, {5814,"Recklessness Ineptitude Other IV"}, {5815,"Recklessness Ineptitude Other V"}, {5816,"Recklessness Ineptitude Other VI"}, {5817,"Recklessness Ineptitude Other VII"}, {5818,"Incantation of Recklessness Ineptitude Other"}, {5819,"Recklessness Mastery Other I"}, {5820,"Recklessness Mastery Other II"}, {5821,"Recklessness Mastery Other III"}, {5822,"Recklessness Mastery Other IV"}, {5823,"Recklessness Mastery Other V"}, {5824,"Recklessness Mastery Other VI"}, {5825,"Recklessness Mastery Other VII"}, {5826,"Incantation of Recklessness Mastery Other"}, {5827,"Recklessness Mastery Self I"}, {5828,"Recklessness Mastery Self II"}, {5829,"Recklessness Mastery Self III"}, {5830,"Recklessness Mastery Self IV"}, {5831,"Recklessness Mastery Self V"}, {5832,"Recklessness Mastery Self VI"}, {5833,"Recklessness Mastery Self VII"}, {5834,"Incantation of Recklessness Mastery Self"}, {5835,"Shield Ineptitude Other I"}, {5836,"Shield Ineptitude Other II"}, {5837,"Shield Ineptitude Other III"}, {5838,"Shield Ineptitude Other IV"}, {5839,"Shield Ineptitude Other V"}, {5840,"Shield Ineptitude Other VI"}, {5841,"Shield Ineptitude Other VII"}, {5842,"Incantation of Shield Ineptitude Other"}, {5843,"Shield Mastery Other I"}, {5844,"Shield Mastery Other II"}, {5845,"Shield Mastery Other III"}, {5846,"Shield Mastery Other IV"}, {5847,"Shield Mastery Other V"}, {5848,"Shield Mastery Other VI"}, {5849,"Shield Mastery Other VII"}, {5850,"Incantation of Shield Mastery Other"}, {5851,"Shield Mastery Self I"}, {5852,"Shield Mastery Self II"}, {5853,"Shield Mastery Self III"}, {5854,"Shield Mastery Self IV"}, {5855,"Shield Mastery Self V"}, {5856,"Shield Mastery Self VI"}, {5857,"Shield Mastery Self VII"}, {5858,"Incantation of Shield Mastery Self"}, {5859,"Sneak Attack Ineptitude Other I"}, {5860,"Sneak Attack Ineptitude Other II"}, {5861,"Sneak Attack Ineptitude Other III"}, {5862,"Sneak Attack Ineptitude Other IV"}, {5863,"Sneak Attack Ineptitude Other V"}, {5864,"Sneak Attack Ineptitude Other VI"}, {5865,"Sneak Attack Ineptitude Other VII"}, {5866,"Incantation of Sneak Attack Ineptitude Other"}, {5867,"Sneak Attack Mastery Other I"}, {5868,"Sneak Attack Mastery Other II"}, {5869,"Sneak Attack Mastery Other III"}, {5870,"Sneak Attack Mastery Other IV"}, {5871,"Sneak Attack Mastery Other V"}, {5872,"Sneak Attack Mastery Other VI"}, {5873,"Sneak Attack Mastery Other VII"}, {5874,"Incantation of Sneak Attack Mastery Other"}, {5875,"Sneak Attack Mastery Self I"}, {5876,"Sneak Attack Mastery Self II"}, {5877,"Sneak Attack Mastery Self III"}, {5878,"Sneak Attack Mastery Self IV"}, {5879,"Sneak Attack Mastery Self V"}, {5880,"Sneak Attack Mastery Self VI"}, {5881,"Sneak Attack Mastery Self VII"}, {5882,"Incantation of Sneak Attack Mastery Self"}, {5883,"Minor Dirty Fighting Prowess"}, {5884,"Minor Dual Wield Aptitude"}, {5885,"Minor Recklessness Prowess"}, {5886,"Minor Shield Aptitude"}, {5887,"Minor Sneak Attack Prowess"}, {5888,"Major Dirty Fighting Prowess"}, {5889,"Major Dual Wield Aptitude"}, {5890,"Major Recklessness Prowess"}, {5891,"Major Shield Aptitude"}, {5892,"Major Sneak Attack Prowess"}, {5893,"Epic Dirty Fighting Prowess"}, {5894,"Epic Dual Wield Aptitude"}, {5895,"Epic Recklessness Prowess"}, {5896,"Epic Shield Aptitude"}, {5897,"Epic Sneak Attack Prowess"}, {5898,"Moderate Dirty Fighting Prowess"}, {5899,"Moderate Dual Wield Aptitude"}, {5900,"Moderate Recklessness Prowess"}, {5901,"Moderate Shield Aptitude"}, {5902,"Moderate Sneak Attack Prowess"}, {5903,"Prodigal Dual Wield Mastery"}, {5904,"Spectral Dual Wield Mastery"}, {5905,"Prodigal Recklessness Mastery"}, {5906,"Spectral Recklessness Mastery"}, {5907,"Prodigal Shield Mastery"}, {5908,"Spectral Shield Mastery"}, {5909,"Prodigal Sneak Attack Mastery"}, {5910,"Spectral Sneak Attack Mastery"}, {5911,"Prodigal Dirty Fighting Mastery"}, {5912,"Spectral Dirty Fighting Mastery"}, {5913,"Weave of Dirty Fighting I"}, {5914,"Weave of Dirty Fighting II"}, {5915,"Weave of Dirty Fighting III"}, {5916,"Weave of Dirty Fighting IV"}, {5917,"Weave of Dirty Fighting V"}, {5918,"Weave of Dual Wield I"}, {5919,"Weave of Dual Wield II"}, {5920,"Weave of Dual Wield III"}, {5921,"Weave of Dual Wield IV"}, {5922,"Weave of Dual Wield V"}, {5923,"Weave of Recklessness I"}, {5924,"Weave of Recklessness II"}, {5925,"Weave of Recklessness III"}, {5926,"Weave of Recklessness IV"}, {5927,"Weave of Recklessness V"}, {5928,"Weave of Shield I"}, {5929,"Weave of Shield II"}, {5930,"Weave of Shield III"}, {5931,"Weave of Shield IV"}, {5932,"Weave of Shield V"}, {5933,"Weave of Sneak Attack I"}, {5934,"Weave of Sneak Attack II"}, {5935,"Weave of Sneak Attack III"}, {5936,"Weave of Sneak Attack IV"}, {5937,"Weave of Sneak Attack V"}, {5938,"Blinding Assault"}, {5939,"Bleeding Assault"}, {5940,"Unbalancing Assault"}, {5941,"Traumatic Assault"}, {5942,"Blinding Blow"}, {5943,"Bleeding Blow"}, {5944,"Unbalancing Blow"}, {5945,"Traumatic Blow"}, {5946,"Novice Soldier's Dirty Fighting Aptitude"}, {5947,"Apprentice Soldier's Dirty Fighting Aptitude"}, {5948,"Journeyman Soldier's Dirty Fighting Aptitude"}, {5949,"Master Soldier's Dirty Fighting Aptitude"}, {5950,"Novice Soldier's Dual Wield Aptitude"}, {5951,"Apprentice Soldier's Dual Wield Aptitude"}, {5952,"Journeyman Soldier's Dual Wield Aptitude"}, {5953,"Master Soldier's Dual Wield Aptitude"}, {5954,"Novice Soldier's Recklessness Aptitude"}, {5955,"Apprentice Soldier's Recklessness Aptitude"}, {5956,"Journeyman Soldier's Recklessness Aptitude"}, {5957,"Master Soldier's Recklessness Aptitude"}, {5958,"Novice Soldier's Shield Aptitude"}, {5959,"Apprentice Soldier's Shield Aptitude"}, {5960,"Journeyman Soldier's Shield Aptitude"}, {5961,"Master Soldier's Shield Aptitude"}, {5962,"Novice Soldier's Sneak Attack Aptitude"}, {5963,"Apprentice Soldier's Sneak Attack Aptitude"}, {5964,"Journeyman Soldier's Sneak Attack Aptitude"}, {5965,"Master Soldier's Sneak Attack Aptitude"}, {5966,"Vigor of Mhoire"}, {5967,"Galvanic Arc"}, {5968,"Galvanic Blast"}, {5969,"Galvanic Strike"}, {5970,"Galvanic Streak"}, {5971,"Galvanic Volley"}, {5972,"Galvanic Bomb"}, {5973,"Protection of Mouf"}, {5974,"Rare Armor Damage Boost I"}, {5975,"Rare Armor Damage Boost II"}, {5976,"Rare Armor Damage Boost III"}, {5977,"Rare Armor Damage Boost IV"}, {5978,"Rare Armor Damage Boost V"}, {5979,"Blighted Touch"}, {5980,"Corrupted Touch"}, {5981,"Sath'tik's Curse"}, {5982,"Aura of Hermetic Link Other I"}, {5983,"Aura of Hermetic Link Other II"}, {5984,"Aura of Hermetic Link Other III"}, {5985,"Aura of Hermetic Link Other IV"}, {5986,"Aura of Hermetic Link Other V"}, {5987,"Aura of Hermetic Link Other VI"}, {5988,"Aura of Hermetic Link Other VII"}, {5989,"Aura of Incantation of Hermetic Link Other"}, {5990,"Aura of Blood Drinker Other I"}, {5991,"Aura of Blood Drinker Other II"}, {5992,"Aura of Blood Drinker Other III"}, {5993,"Aura of Blood Drinker Other IV"}, {5994,"Aura of Blood Drinker Other V"}, {5995,"Aura of Blood Drinker Other VI"}, {5996,"Aura of Blood Drinker Other VII"}, {5997,"Aura of Incantation of Blood Drinker Other"}, {5998,"Aura of Incantation of Blood Drinker Other"}, {5999,"Aura of Defender Other I"}, {6000,"Aura of Defender Other II"}, {6001,"Aura of Defender Other III"}, {6002,"Aura of Defender Other IV"}, {6003,"Aura of Defender Other V"}, {6004,"Aura of Defender Other VI"}, {6005,"Aura of Defender Other VII"}, {6006,"Aura of Incantation of Defender Other"}, {6007,"Aura of Heart Seeker Other I"}, {6008,"Aura of Heart Seeker Other II"}, {6009,"Aura of Heart Seeker Other III"}, {6010,"Aura of Heart Seeker Other IV"}, {6011,"Aura of Heart Seeker Other V"}, {6012,"Aura of Heart Seeker Other VI"}, {6013,"Aura of Heart Seeker Other VII"}, {6014,"Aura of Incantation of Heart Seeker Other"}, {6015,"Aura of Spirit Drinker Other I"}, {6016,"Aura of Spirit Drinker Other II"}, {6017,"Aura of Spirit Drinker Other III"}, {6018,"Aura of Spirit Drinker Other IV"}, {6019,"Aura of Spirit Drinker Other V"}, {6020,"Aura of Spirit Drinker Other VI"}, {6021,"Aura of Spirit Drinker Other VII"}, {6022,"Aura of Incantation of Spirit Drinker Other"}, {6023,"Aura of Incantation of Spirit Drinker Other"}, {6024,"Aura of Swift Killer Other I"}, {6025,"Aura of Swift Killer Other II"}, {6026,"Aura of Swift Killer Other III"}, {6027,"Aura of Swift Killer Other IV"}, {6028,"Aura of Swift Killer Other V"}, {6029,"Aura of Swift Killer Other VI"}, {6030,"Aura of Swift Killer Other VII"}, {6031,"Aura of Incantation of Swift Killer Other"}, {6032,"Imprisoned"}, {6033,"Impudence"}, {6034,"Proving Grounds Rolling Death"}, {6035,"Spirit of Izexi"}, {6036,"No Escape"}, {6037,"Fleeting Will"}, {6038,"Warm and Fuzzy"}, {6039,"Legendary Weapon Tinkering Expertise"}, {6040,"Legendary Alchemical Prowess"}, {6041,"Legendary Arcane Prowess"}, {6042,"Legendary Armor Tinkering Expertise"}, {6043,"Legendary Light Weapon Aptitude"}, {6044,"Legendary Missile Weapon Aptitude"}, {6045,"Legendary Cooking Prowess"}, {6046,"Legendary Creature Enchantment Aptitude"}, {6047,"Legendary Finesse Weapon Aptitude"}, {6048,"Legendary Deception Prowess"}, {6049,"Legendary Dirty Fighting Prowess"}, {6050,"Legendary Dual Wield Aptitude"}, {6051,"Legendary Fealty"}, {6052,"Legendary Fletching Prowess"}, {6053,"Legendary Healing Prowess"}, {6054,"Legendary Impregnability"}, {6055,"Legendary Invulnerability"}, {6056,"Legendary Item Enchantment Aptitude"}, {6057,"Legendary Item Tinkering Expertise"}, {6058,"Legendary Jumping Prowess"}, {6059,"Legendary Leadership"}, {6060,"Legendary Life Magic Aptitude"}, {6061,"Legendary Lockpick Prowess"}, {6062,"Legendary Magic Item Tinkering Expertise"}, {6063,"Legendary Magic Resistance"}, {6064,"Legendary Mana Conversion Prowess"}, {6065,"Legendary Monster Attunement"}, {6066,"Legendary Person Attunement"}, {6067,"Legendary Recklessness Prowess"}, {6068,"Legendary Salvaging Aptitude"}, {6069,"Legendary Shield Aptitude"}, {6070,"Legendary Sneak Attack Prowess"}, {6071,"Legendary Sprint"}, {6072,"Legendary Heavy Weapon Aptitude"}, {6073,"Legendary Two Handed Combat Aptitude"}, {6074,"Legendary Void Magic Aptitude"}, {6075,"Legendary War Magic Aptitude"}, {6076,"Legendary Stamina Gain"}, {6077,"Legendary Health Gain"}, {6078,"Legendary Mana Gain"}, {6079,"Legendary Storm Ward"}, {6080,"Legendary Acid Ward"}, {6081,"Legendary Bludgeoning Ward"}, {6082,"Legendary Flame Ward"}, {6083,"Legendary Frost Ward"}, {6084,"Legendary Piercing Ward"}, {6085,"Legendary Slashing Ward"}, {6086,"Epic Hermetic Link"}, {6087,"Legendary Hermetic Link"}, {6088,"Legendary Acid Bane"}, {6089,"Legendary Blood Thirst"}, {6090,"Legendary Bludgeoning Bane"}, {6091,"Legendary Defender"}, {6092,"Legendary Flame Bane"}, {6093,"Legendary Frost Bane"}, {6094,"Legendary Heart Thirst"}, {6095,"Legendary Impenetrability"}, {6096,"Legendary Piercing Bane"}, {6097,"Legendary Slashing Bane"}, {6098,"Legendary Spirit Thirst"}, {6099,"Legendary Storm Bane"}, {6100,"Legendary Swift Hunter"}, {6101,"Legendary Willpower"}, {6102,"Legendary Armor"}, {6103,"Legendary Coordination"}, {6104,"Legendary Endurance"}, {6105,"Legendary Focus"}, {6106,"Legendary Quickness"}, {6107,"Legendary Strength"}, {6108,"Summoning Mastery Other I"}, {6109,"Summoning Mastery Other II"}, {6110,"Summoning Mastery Other III"}, {6111,"Summoning Mastery Other IV"}, {6112,"Summoning Mastery Other V"}, {6113,"Summoning Mastery Other VI"}, {6114,"Summoning Mastery Other VII"}, {6115,"Incantation of Summoning Mastery Other"}, {6116,"Summoning Mastery Self I"}, {6117,"Summoning Mastery Self II"}, {6118,"Summoning Mastery Self III"}, {6119,"Summoning Mastery Self IV"}, {6120,"Summoning Mastery Self V"}, {6121,"Summoning Mastery Self VI"}, {6122,"Summoning Mastery Self VII"}, {6123,"Incantation of Summoning Mastery Self"}, {6124,"Epic Summoning Prowess"}, {6125,"Legendary Summoning Prowess"}, {6126,"Major Summoning Prowess"}, {6127,"Minor Summoning Prowess"}, {6128,"Moderate Summoning Prowess"}, {6129,"Summoning Ineptitude Other I"}, {6130,"Summoning Ineptitude Other II"}, {6131,"Summoning Ineptitude Other III"}, {6132,"Summoning Ineptitude Other IV"}, {6133,"Summoning Ineptitude Other V"}, {6134,"Summoning Ineptitude Other VI"}, {6135,"Summoning Ineptitude Other VII"}, {6136,"Incantation of Summoning Ineptitude Other"}, {6137,"Weave of Summoning II"}, {6138,"Weave of Summoning III"}, {6139,"Weave of Summoning IV"}, {6140,"Weave of Summoning V"}, {6141,"Weave of Summoning I"}, {6142,"Novice Invoker's Summoning Aptitude"}, {6143,"Apprentice Invoker's Summoning Aptitude"}, {6144,"Journeyman Invoker's Summoning Aptitude"}, {6145,"Master Invoker's Summoning Aptitude"}, {6146,"Ride The Lightning"}, {6147,"Entrance to the Frozen Valley"}, {6148,"Begone and Be Afraid"}, {6149,"Rynthid Vision"}, {6150,"Rynthid Recall"}, {6151,"Crimson Storm"}, {6152,"Rocky Shrapnel"}, {6153,"Tryptophan Coma"}, {6154,"Entering the Basement"}, {6155,"Earthen Stomp"}, {6156,"Viridian Ring"}, {6157,"Withering Ring"}, {6158,"Poison Breath"}, {6159,"Thorn Volley"}, {6160,"Thorns"}, {6161,"Acidic Thorns"}, {6162,"Thorn Arc"}, {6163,"Ring of Thorns"}, {6164,"Deadly Ring of Thorns"}, {6165,"Deadly Thorn Volley"}, {6166,"Poisoned Wounds"}, {6167,"Poisoned Vitality"}, {6168,"Deadly Ring of Lightning"}, {6169,"Deadly Lightning Volley"}, {6170,"Honeyed Life Mead"}, {6171,"Honeyed Mana Mead"}, {6172,"Honeyed Vigor Mead"}, {6173,"Raging Heart"}, {6174,"Twisting Wounds"}, {6175,"Increasing Pain"}, {6176,"Genius"}, {6177,"Gauntlet Item Tinkering Mastery"}, {6178,"Gauntlet Weapon Tinkering Mastery"}, {6179,"Gauntlet Magic Item Tinkering Mastery"}, {6180,"Gauntlet Armor Tinkering Mastery"}, {6181,"Singeing Flames"}, {6182,"Over-Exerted"}, {6183,"Gauntlet Arena"}, {6184,"Gauntlet Arena"}, {6185,"Gauntlet Arena"}, {6186,"Deafening Wail"}, {6187,"Screeching Howl"}, {6188,"Earthquake"}, {6189,"Searing Disc II"}, {6190,"Horizon's Blades II"}, {6191,"Cassius' Ring of Fire II"}, {6192,"Nuhmudira's Spines II"}, {6193,"Halo of Frost II"}, {6194,"Eye of the Storm II"}, {6195,"Clouded Soul II"}, {6196,"Tectonic Rifts II"}, {6197,"Eye of the Storm II"}, {6198,"Incantation of Lightning Bolt"}, {6199,"Incantation of Lightning Arc"}, {6200,"Paragon's Dual Wield Mastery V"}, {6201,"Paragon's Finesse Weapon Mastery I"}, {6202,"Paragon's Finesse Weapon Mastery II"}, {6203,"Paragon's Finesse Weapon Mastery III"}, {6204,"Paragon's Finesse Weapon Mastery IV"}, {6205,"Paragon's Finesse Weapon Mastery V"}, {6206,"Paragon's Heavy Weapon Mastery I"}, {6207,"Paragon's Heavy Weapon Mastery II"}, {6208,"Paragon's Heavy Weapon Mastery III"}, {6209,"Paragon's Heavy Weapon Mastery IV"}, {6210,"Paragon's Heavy Weapon Mastery V"}, {6211,"Paragon's Life Magic Mastery I"}, {6212,"Paragon's Life Magic Mastery II"}, {6213,"Paragon's Life Magic Mastery III"}, {6214,"Paragon's Life Magic Mastery IV"}, {6215,"Paragon's Life Magic Mastery V"}, {6216,"Paragon's Light Weapon Mastery I"}, {6217,"Paragon's Light Weapon Mastery II"}, {6218,"Paragon's Light Weapon Mastery III"}, {6219,"Paragon's Light Weapon Mastery IV"}, {6220,"Paragon's Light Weapon Mastery V"}, {6221,"Paragon's Missile Weapon Mastery I"}, {6222,"Paragon's Missile Weapon Mastery II"}, {6223,"Paragon's Missile Weapon Mastery III"}, {6224,"Paragon's Missile Weapon Mastery IV"}, {6225,"Paragon's Missile Weapon Mastery V"}, {6226,"Paragon's Recklessness Mastery I"}, {6227,"Paragon's Recklessness Mastery II"}, {6228,"Paragon's Recklessness Mastery III"}, {6229,"Paragon's Recklessness Mastery IV"}, {6230,"Paragon's Recklessness Mastery IV"}, {6231,"Paragon's Sneak Attack Mastery I"}, {6232,"Paragon's Sneak Attack Mastery II"}, {6233,"Paragon's Sneak Attack Mastery III"}, {6234,"Paragon's Sneak Attack Mastery IV"}, {6235,"Paragon's Sneak Attack Mastery V"}, {6236,"Paragon's Two Handed Combat Mastery I"}, {6237,"Paragon's Two Handed Combat Mastery II"}, {6238,"Paragon's Two Handed Combat Mastery III"}, {6239,"Paragon's Two Handed Combat Mastery IV"}, {6240,"Paragon's Two Handed Combat Mastery V"}, {6241,"Paragon's Void Magic Mastery I"}, {6242,"Paragon's Void Magic Mastery II"}, {6243,"Paragon's Void Magic Mastery III"}, {6244,"Paragon's Void Magic Mastery IV"}, {6245,"Paragon's Void Magic Mastery V"}, {6246,"Paragon's War Magic Mastery I"}, {6247,"Paragon's War Magic Mastery II"}, {6248,"Paragon's War Magic Mastery III"}, {6249,"Paragon's War Magic Mastery IV"}, {6250,"Paragon's War Magic Mastery V"}, {6251,"Paragon's Dirty Fighting Mastery I"}, {6252,"Paragon's Dirty Fighting Mastery II"}, {6253,"Paragon's Dirty Fighting Mastery III"}, {6254,"Paragon's Dirty Fighting Mastery IV"}, {6255,"Paragon's Dirty Fighting Mastery V"}, {6256,"Paragon's Dual Wield Mastery I"}, {6257,"Paragon's Dual Wield Mastery II"}, {6258,"Paragon's Dual Wield Mastery III"}, {6259,"Paragon's Dual Wield Mastery IV"}, {6260,"Paragon's Willpower V"}, {6261,"Paragon's Coordination I"}, {6262,"Paragon's Coordination II"}, {6263,"Paragon's Coordination III"}, {6264,"Paragon's Coordination IV"}, {6265,"Paragon's Coordination V"}, {6266,"Paragon's Endurance I"}, {6267,"Paragon's Endurance II"}, {6268,"Paragon's Endurance III"}, {6269,"Paragon's Endurance IV"}, {6270,"Paragon's Endurance V"}, {6271,"Paragon's Focus I"}, {6272,"Paragon's Focus II"}, {6273,"Paragon's Focus III"}, {6274,"Paragon's Focus IV"}, {6275,"Paragon's Focus V"}, {6276,"Paragon Quickness I"}, {6277,"Paragon Quickness II"}, {6278,"Paragon Quickness III"}, {6279,"Paragon Quickness IV"}, {6280,"Paragon Quickness V"}, {6281,"Paragon's Strength I"}, {6282,"Paragon's Strength II"}, {6283,"Paragon's Strength III"}, {6284,"Paragon's Strength IV"}, {6285,"Paragon's Strength V"}, {6286,"Paragon's Willpower I"}, {6287,"Paragon's Willpower II"}, {6288,"Paragon's Willpower III"}, {6289,"Paragon's Willpower IV"}, {6290,"Paragon's Stamina V"}, {6291,"Paragon's Critical Boost I"}, {6292,"Paragon's Critical Damage Boost II"}, {6293,"Paragon's Critical Damage Boost III"}, {6294,"Paragon's Critical Damage Boost IV"}, {6295,"Paragon's Critical Damage Boost V"}, {6296,"Paragon's Critical Damage Reduction I"}, {6297,"Paragon's Critical Damage Reduction II"}, {6298,"Paragon's Critical Damage Reduction III"}, {6299,"Paragon's Critical Damage Reduction IV"}, {6300,"Paragon's Critical Damage Reduction V"}, {6301,"Paragon's Damage Boost I"}, {6302,"Paragon's Damage Boost II"}, {6303,"Paragon's Damage Boost III"}, {6304,"Paragon's Damage Boost IV"}, {6305,"Paragon's Damage Boost V"}, {6306,"Paragon's Damage Reduction I"}, {6307,"Paragon's Damage Reduction II"}, {6308,"Paragon's Damage Reduction III"}, {6309,"Paragon's Damage Reduction IV"}, {6310,"Paragon's Damage Reduction V"}, {6311,"Paragon's Mana I"}, {6312,"Paragon's Mana II"}, {6313,"Paragon's Mana III"}, {6314,"Paragon's Mana IV"}, {6315,"Paragon's Mana V"}, {6316,"Paragon's Stamina I"}, {6317,"Paragon's Stamina II"}, {6318,"Paragon's Stamina III"}, {6319,"Paragon's Stamina IV"}, {6320,"Ring of Skulls II"}, {6321,"Viridian Rise Recall"}, {6322,"Viridian Rise Great Tree Recall"} };

		public static string SpellIdText(int spellID)
		{
			string pre = " SpellId "+ spellID.ToString()+" is ";
			if (OutputText.allSpells_IdName.ContainsKey(spellID))
				return pre + "\"" + OutputText.allSpells_IdName[spellID] + "\".";
			else
				return pre + "not a recognized spell.";
		}

		private static Regex spellRE = new Regex(@"(getisspellknown|getcancastspell_(buff|hunt)|actiontrycastbyidontarget|actiontrycastbyid)\s*\[\s*(?<sid>[0-9]+)\s*(\]|,)", RegexOptions.Compiled);
		public static string CommentedAllSpellIdInTextText(string text)
        {
			string allText = "";

			foreach ( Match m in OutputText.spellRE.Matches(text) )
				allText += OutputText.SpellIdText(Int32.Parse(m.Groups["sid"].Value));
			return allText;
		}

	}
	abstract class ImportExport
	{
		abstract public void ImportFromMet(ref FileLines f);
		abstract public void ExportToMet(ref FileLines f);
		abstract public void ImportFromMetAF(ref FileLines f);
		abstract public void ExportToMetAF(ref FileLines f);
	}


	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION
	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION
	// CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION CONDITION

	abstract class Condition : ImportExport
	{
		abstract public CTypeID typeid { get; } // get { return CTypeID.Unassigned; } }
		private int _d;
		protected int depth { get { return this._d; } set { this._d = value; } }
		public Condition(int d) { this.depth = d; }
	}

	class CUnassigned : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.Unassigned; } }
		public CUnassigned(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f)
		{ throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Should never get here."); }
		override public void ExportToMet(ref FileLines f)
		{ throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ExportToMet: Should never get here."); }
		override public void ImportFromMetAF(ref FileLines f)
		{ throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Should never get here."); }
		override public void ExportToMetAF(ref FileLines f)
		{ throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ExportToMetAF: Should never get here."); }
	}

	class CNever : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.Never; } }
		public CNever(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f) {
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CAlways : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.Always; } }
		public CAlways(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CAll : Condition // line# for msgs good
	{
		private int _count;
		private Rule _myRule;
		public override CTypeID typeid { get { return CTypeID.All; } }
		public List<Condition> condition;
		public CAll(int d, Rule r) : base(d)
		{
			this.condition = new List<Condition>();
			this._myRule = r;
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			Condition tmpCond;
			CTypeID cID;
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("K") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'K'.");
			if (f.line[f.L++].CompareTo("V") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'V'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			try { this._count = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			for (int i = 0; i < this._count; i++)
			{
				if (f.line[f.L++].CompareTo("i") != 0)
					throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
				try { cID = (CTypeID)Int32.Parse(f.line[f.L++]); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

				try { tmpCond = this._myRule.GetCondition(cID, this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

				tmpCond.ImportFromMet(ref f); // <--- recurse
				this.condition.Add(tmpCond);
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
			f.line.Add(this.condition.Count.ToString());
			foreach (Condition c in this.condition)
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );

			// Is there something after the operation, even though there shouldn't be?
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[this.typeid.ToString()]);

			// It's a proper operation. Proceed. (This function only processes the operation keyords themselves, not any potential parameters they might have. It's the down-calls that do that part.)
			while (true) // internal break-outs only
			{
				// Find first non-"blank" line following this one (or EOF)
				f.L--;
				while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
					;

				// Hit end of file
				if (f.L >= f.line.Count)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Hit end-of-file but expected a Condition operation, or start of an Action ('DO'). [" + rx.getInfo["STATE:"] + "]");

				// Found first non-"blank" line. Try to get an operation (don't advance lines yet)
				match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
				if (match.Success)
				{
					if (match.Groups["type"].Value.CompareTo("DO:") == 0)
					{
						f.C = 0;
						return;
					}
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Missing Action ('DO:') part of Rule." + rx.getInfo[this.typeid.ToString()]);
				}

				// It better be a valid Condition op...
				match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L]);
				if (!match.Success)
				{
					Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
					if (tmatch.Success)
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. " + rx.getInfo["Generic"]);
				}
				// It is.

				// How is it tabbed ?
				int nTabs = match.Groups["tabs"].Length;
				if (nTabs <= Rule.ConditionContentTabLevel)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside a Condition's All/Any operation. " + rx.getInfo[this.typeid.ToString()]);
				if (nTabs <= depth)
				{   // return, since now done with this operation
					f.C = nTabs; // Math.Max(nTabs - 1, 0);
					return;
				}
				if (nTabs > depth + 1) // error
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[this.typeid.ToString()]);

				// Here: #tabs does equal depth+1; try to import this op.
				Condition tmpCond;
				try { tmpCond = this._myRule.GetCondition(this._myRule.conditionStrToID[match.Groups["op"].Value], this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
				f.C = match.Groups["op"].Index + match.Groups["op"].Length;
				tmpCond.ImportFromMetAF(ref f); // <--- recurse
				this.condition.Add(tmpCond);
			}
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
			foreach (Condition c in this.condition)
				c.ExportToMetAF(ref f); // <--- recurse
		}
	}

	class CAny : Condition // line# for msgs good
	{
		private int _count;
		private Rule _myRule;
		public override CTypeID typeid { get { return CTypeID.Any; } }
		public List<Condition> condition;
		public CAny(int d, Rule r) : base(d)
		{
			this.condition = new List<Condition>();
			this._myRule = r;
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			Condition tmpCond;
			CTypeID cID;
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("K") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'K'.");
			if (f.line[f.L++].CompareTo("V") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'V'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			try { this._count = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			for (int i = 0; i < this._count; i++)
			{
				if (f.line[f.L++].CompareTo("i") != 0)
					throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
				try { cID = (CTypeID)Int32.Parse(f.line[f.L++]); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
				try { tmpCond = this._myRule.GetCondition(cID, this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }
				tmpCond.ImportFromMet(ref f); // <--- recurse
				this.condition.Add(tmpCond);
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
			f.line.Add(this.condition.Count.ToString());
			foreach (Condition c in this.condition)
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );

			// Is there something after the operation, even though there shouldn't be?
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[this.typeid.ToString()]);

			// It's a proper operation. Proceed. (This function only processes the operation keyords themselves, not any potential parameters they might have. It's the down-calls that do that part.)
			while (true) // internal break-outs only
			{
				// Find first non-"blank" line following this one (or EOF)
				f.L--;
				while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
					;

				// Hit end of file
				if (f.L >= f.line.Count)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Hit end-of-file but expected a Condition operation, or start of an Action. [" + rx.getInfo["STATE:"] + "]");

				// Found first non-"blank" line. Try to get an operation (don't advance lines yet)
				match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]);
				if (match.Success)
				{
					if (match.Groups["type"].Value.CompareTo("DO:") == 0)
					{
						f.C = 0;
						return;
					}
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Missing Action ('DO:') part of Rule." + rx.getInfo[this.typeid.ToString()]);
				}

				// It better be a valid Condition op...
				match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L]);
				if (!match.Success)
				{
					Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
					if (tmatch.Success)
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation. " + rx.getInfo["Generic"]);
				}
				// It is.

				// How is it tabbed ?
				int nTabs = match.Groups["tabs"].Length;
				if (nTabs <= Rule.ConditionContentTabLevel)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside a Condition's All/Any operation. " + rx.getInfo[this.typeid.ToString()]);
				if (nTabs <= depth)
				{   // return, since now done with this operation
					f.C = nTabs; // Math.Max(nTabs - 1, 0);
					return;
				}
				if (nTabs > depth + 1) // error
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[this.typeid.ToString()]);

				// Here: #tabs does equal depth+1; try to import this op.
				Condition tmpCond;
				try { tmpCond = this._myRule.GetCondition(this._myRule.conditionStrToID[match.Groups["op"].Value], this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
				f.C = match.Groups["op"].Index + match.Groups["op"].Length;
				tmpCond.ImportFromMetAF(ref f); // <--- recurse
				this.condition.Add(tmpCond);
			}
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
			foreach (Condition c in this.condition)
				c.ExportToMetAF(ref f); // <--- recurse
		}
	}

	class CChatMatch : Condition // line# for msgs good
	{
		private string _s_chat;
		public CChatMatch(int d) : base(d) { this._s_chat = ""; }
		public override CTypeID typeid { get { return CTypeID.ChatMatch; } }

		private string _m_chat
		{
			set { this._s_chat = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_chat); }
		}
		private string _a_chat
		{
			set { this._s_chat = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_chat); }
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_chat = f.line[f.L++];
			//try{ this._chat = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
		}

		override public void ExportToMet(ref FileLines f)
		{
			//f.line.Add("i");
			//f.line.Add(((int)CTypeID.ChatMatch).ToString());
			f.line.Add("s");
			f.line.Add(this._m_chat);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			this._a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
																									 //try { this._chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length-2); } // length is at least 2; remove delimiters
																									 //catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_chat + rx.cD);
		}
	}

	class CMainSlotsLE : Condition // line# for msgs good
	{
		private int _slots;
		public CMainSlotsLE(int d) : base(d) { this._slots = 0; }
		public override CTypeID typeid { get { return CTypeID.MainSlotsLE; } }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._slots = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._slots.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._slots = Int32.Parse(match.Groups["i"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}

		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._slots.ToString());
		}
	}
	class CSecsInStateGE : Condition // line# for msgs good
	{
		private int _seconds;
		public CSecsInStateGE(int d) : base(d) { this._seconds = 0; }
		public override CTypeID typeid { get { return CTypeID.SecsInStateGE; } }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._seconds = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._seconds.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._seconds = Int32.Parse(match.Groups["i"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._seconds.ToString());
		}
	}

	class CNavEmpty : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.NavEmpty; } }
		public CNavEmpty(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CDeath : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.Death; } }
		public CDeath(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CVendorOpen : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.VendorOpen; } }
		public CVendorOpen(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CVendorClosed : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.VendorClosed; } }
		public CVendorClosed(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CItemCountLE : Condition // line# for msgs good
	{
		private string _s_invItem;
		private int _invCount;
		public override CTypeID typeid { get { return CTypeID.ItemCountLE; } }
		public CItemCountLE(int d) : base(d) { }
		private string _m_invItem
		{
			set { this._s_invItem = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_invItem); }
		}
		private string _a_invItem
		{
			set { this._s_invItem = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_invItem); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_invItem = f.line[f.L++];
			//try { this._invItem = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("c") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._invCount = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
			f.line.Add(this._m_invItem);
			f.line.Add("s");
			f.line.Add("c");
			f.line.Add("i");
			f.line.Add(this._invCount.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._invCount = Int32.Parse(match.Groups["i"].Value);
				this._a_invItem = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._invCount.ToString() + " " + rx.oD + this._a_invItem + rx.cD);
		}
	}

	class CItemCountGE : Condition // line# for msgs good
	{
		private string _s_invItem;
		private int _invCount;
		public override CTypeID typeid { get { return CTypeID.ItemCountGE; } }
		public CItemCountGE(int d) : base(d) { }
		private string _m_invItem
		{
			set { this._s_invItem = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_invItem); }
		}
		private string _a_invItem
		{
			set { this._s_invItem = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_invItem); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_invItem = f.line[f.L++];
			//try { this._invItem = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("c") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._invCount = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
			f.line.Add(this._m_invItem);
			f.line.Add("s");
			f.line.Add("c");
			f.line.Add("i");
			f.line.Add(this._invCount.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._invCount = Int32.Parse(match.Groups["i"].Value);
				this._a_invItem = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._invCount.ToString() + " " + rx.oD + this._a_invItem + rx.cD);
		}
	}

	class CMobsInDist_Name : Condition // line# for msgs good
	{
		private string _s_regex;
		private int _count;
		private double _range;
		public override CTypeID typeid { get { return CTypeID.MobsInDist_Name; } }
		public CMobsInDist_Name(int d) : base(d) { }
		private string _m_regex
		{
			set { this._s_regex = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_regex); }
		}
		private string _a_regex
		{
			set { this._s_regex = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_regex); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("3") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 3.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_regex = f.line[f.L++];
			//try { this._regex = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("c") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._count = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("r") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._range = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
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
			f.line.Add(this._m_regex);
			f.line.Add("s");
			f.line.Add("c");
			f.line.Add("i");
			f.line.Add(this._count.ToString());
			f.line.Add("s");
			f.line.Add("r");
			f.line.Add("d");
			f.line.Add(this._range.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._count = Int32.Parse(match.Groups["i"].Value);
				this._range = Double.Parse(match.Groups["d"].Value);
				this._a_regex = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._count.ToString() + " " + this._range.ToString() + " " + rx.oD + this._a_regex + rx.cD);
		}
	}

	class CMobsInDist_Priority : Condition // line# for msgs good
	{
		private int _priority;
		private int _count;
		private double _range;
		public override CTypeID typeid { get { return CTypeID.MobsInDist_Priority; } }
		public CMobsInDist_Priority(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("3") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 3.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("p") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'p'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._priority = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("c") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._count = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("r") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._range = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
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
			f.line.Add(this._priority.ToString());
			f.line.Add("s");
			f.line.Add("c");
			f.line.Add("i");
			f.line.Add(this._count.ToString());
			f.line.Add("s");
			f.line.Add("r");
			f.line.Add("d");
			f.line.Add(this._range.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._count = Int32.Parse(match.Groups["i"].Value);
				this._range = Double.Parse(match.Groups["d"].Value);
				this._priority = Int32.Parse(match.Groups["i2"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._count.ToString() + " " + this._range.ToString() + " " + this._priority.ToString());
		}
	}

	class CNeedToBuff : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.NeedToBuff; } }
		public CNeedToBuff(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}


	class CNoMobsInDist : Condition // line# for msgs good
	{
		private double _range;
		public override CTypeID typeid { get { return CTypeID.NoMobsInDist; } }
		public CNoMobsInDist(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("r") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._range = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
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
			f.line.Add(this._range.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._range = Double.Parse(match.Groups["d"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._range.ToString());
		}
	}

	class CBlockE : Condition // line# for msgs good
	{
		private int _block;
		public CBlockE(int d) : base(d) { }
		public override CTypeID typeid { get { return CTypeID.BlockE; } }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._block = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._block.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._block = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._block.ToString("X8"));
		}
	}

	class CCellE : Condition // line# for msgs good
	{
		private int _cell;
		public override CTypeID typeid { get { return CTypeID.CellE; } }
		public CCellE(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._cell = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._cell.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._cell = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._cell.ToString("X8"));
		}
	}

	class CIntoPortal : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.IntoPortal; } }
		public CIntoPortal(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CExitPortal : Condition // line# for msgs good
	{
		public override CTypeID typeid { get { return CTypeID.ExitPortal; } }
		public CExitPortal(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class CNot : Condition // line# for msgs good
	{
		private int _count_ignored;
		private Rule _myRule;
		public override CTypeID typeid { get { return CTypeID.Not; } }
		public Condition condition;
		public CNot(int d, Rule r) : base(d) { this._myRule = r; }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			Condition tmpCond;
			CTypeID cID;
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("K") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'K'.");
			if (f.line[f.L++].CompareTo("V") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'V'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			try { this._count_ignored = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

			if (this._count_ignored != 1)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. 'Not' requires exactly one operand.");

			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected 'i'.");
			try { cID = (CTypeID)Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }

			try { tmpCond = this._myRule.GetCondition(cID, this.depth); } // don't increment depth
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

			tmpCond.ImportFromMet(ref f); // <--- recurse

			this.condition = tmpCond;
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

			f.line.Add(((int)this.condition.typeid).ToString());
			this.condition.ExportToMet(ref f);
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
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[this.typeid.ToString()]);

			// Succeeded. Import it.
			f.C = f.C + match.Groups["op"].Index + match.Groups["op"].Length;
			try { this.condition = this._myRule.GetCondition(this._myRule.conditionStrToID[match.Groups["op"].Value], this.depth); } // do not increase depth
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
			this.condition.ImportFromMetAF(ref f);

			f.C = starting_fC;
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			int ln = f.line.Count; // remember location to go back and insert the Not
			this.condition.ExportToMetAF(ref f);
			f.line[ln] = (new String('\t', this.depth) + this.typeid.ToString()) + " " + f.line[ln].TrimStart();
		}
	}

	class CPSecsInStateGE : Condition // line# for msgs good
	{
		private int _seconds;
		public override CTypeID typeid { get { return CTypeID.PSecsInStateGE; } }
		public CPSecsInStateGE(int d) : base(d) { this._seconds = 9999; }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._seconds = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._seconds.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._seconds = Int32.Parse(match.Groups["i"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._seconds.ToString());
		}
	}

	class CSecsOnSpellGE : Condition // line# for msgs good
	{
		private int _spellID;
		private int _seconds;
		public override CTypeID typeid { get { return CTypeID.SecsOnSpellGE; } }
		public CSecsOnSpellGE(int d) : base(d) { } // no good value for spellID default, so ignore
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("sid") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'sid'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._spellID = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("sec") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'sec'.");
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._seconds = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
			f.line.Add(this._spellID.ToString());
			f.line.Add("s");
			f.line.Add("sec");
			f.line.Add("i");
			f.line.Add(this._seconds.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._seconds = Int32.Parse(match.Groups["i"].Value);
				this._spellID = Int32.Parse(match.Groups["i2"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._seconds.ToString() + " " + this._spellID.ToString() + " ~~" + OutputText.SpellIdText(this._spellID) );
		}
	}

	class CBuPercentGE : Condition // line# for msgs good
	{
		private int _burden;
		public override CTypeID typeid { get { return CTypeID.BuPercentGE; } }
		public CBuPercentGE(int d) : base(d) { this._burden = 100; }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			try { this._burden = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("i");
			f.line.Add(this._burden.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._burden = Int32.Parse(match.Groups["i"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._burden.ToString());
		}
	}


	class CDistToRteGE : Condition // line# for msgs good
	{
		private double _distance;
		public override CTypeID typeid { get { return CTypeID.DistToRteGE; } }
		public CDistToRteGE(int d) : base(d) { this._distance = 0; }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("dist") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'dist'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._distance = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
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
			f.line.Add(this._distance.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._distance = Double.Parse(match.Groups["d"].Value); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._distance.ToString());
		}
	}

	class CExpr : Condition // line# for msgs good
	{
		private string _s_expr;
		public override CTypeID typeid { get { return CTypeID.Expr; } }
		public CExpr(int d) : base(d) { this._s_expr = "false"; }
		private string _m_expr
		{
			set { this._s_expr = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_expr); }
		}
		private string _a_expr
		{
			set { this._s_expr = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_expr); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("e") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'e'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_expr = f.line[f.L++];
			//try { this._expr = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_expr);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_expr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_expr);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_expr + rx.cD + SpellIdsText);
		}
	}

	//
	// During my research, I found this listed somewhere as a type, but it's not on any in-game menus, so I guess it's something deprecated??) : CTypeID=27
	//
	//class CClientDialogPopup : Condition
	//{
	//	public override CTypeID typeid { get { return CTypeID.ClientDialogPopup; } }
	//}

	class CChatCapture : Condition // line# for msgs good
	{
		private string _s_regex;
		private string _s_colorIDlist;
		public override CTypeID typeid { get { return CTypeID.ChatCapture; } }
		public CChatCapture(int d) : base(d) { this._s_regex = ""; this._s_colorIDlist = ""; }
		private string _m_regex
		{
			set { this._s_regex = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_regex); }
		}
		private string _a_regex
		{
			set { this._s_regex = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_regex); }
		}
		private string _m_colorIDlist
		{
			set { this._s_colorIDlist = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_colorIDlist); }
		}
		private string _a_colorIDlist
		{
			set { this._s_colorIDlist = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_colorIDlist); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("p") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'p'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_regex = f.line[f.L++];
			//try { this._regex = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("c") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'c'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_colorIDlist = f.line[f.L++];
			//try { this._colorIDlist = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_regex);
			f.line.Add("s");
			f.line.Add("c");
			f.line.Add("s");
			f.line.Add(this._m_colorIDlist);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._a_regex = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				this._a_colorIDlist = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_regex + rx.cD + " " + rx.oD + this._a_colorIDlist + rx.cD);
		}
	}





	// ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION
	// ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION
	// ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION ACTION


	abstract class Action : ImportExport
	{
		abstract public ATypeID typeid { get; } //{ return ATypeID.Unassigned; } }
		private int _d;
		protected int depth { get { return _d; } set { _d = value; } }
		public Action(int d) { this.depth = d; }
	}

	class AUnassigned : Action // line# for msgs good
	{
		public override ATypeID typeid { get { return ATypeID.Unassigned; } }
		public AUnassigned(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] AUnassigned.ImportFromMet: Should never get here."); }
		override public void ExportToMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] AUnassigned.ExportToMet: Should never get here."); }
		override public void ImportFromMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] AUnassigned.ImportFromMetAF: Should never get here."); }
		override public void ExportToMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] AUnassigned.ExportToMetAF: Should never get here."); }
	}
	class ANone : Action // line# for msgs good
	{
		public override ATypeID typeid { get { return ATypeID.None; } }
		public ANone(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class ASetState : Action // line# for msgs good
	{
		private string _s_state;
		public override ATypeID typeid { get { return ATypeID.SetState; } }
		public ASetState(int d) : base(d) { this._s_state = ""; }
		private string _m_state
		{
			set { this._s_state = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_state); }
		}
		private string _a_state
		{
			set { this._s_state = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_state); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_state = f.line[f.L++];
			//try { this._state = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("s");
			f.line.Add(this._m_state);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_state = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_state + rx.cD);
		}
	}

	class AChat : Action // line# for msgs good
	{
		private string _s_chat;
		public override ATypeID typeid { get { return ATypeID.Chat; } }
		public AChat(int d) : base(d) { this._s_chat = ""; }
		private string _m_chat
		{
			set { this._s_chat = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_chat); }
		}
		private string _a_chat
		{
			set { this._s_chat = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_chat); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_chat = f.line[f.L++];
			//try { this._chat = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("s");
			f.line.Add(this._m_chat);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_chat);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_chat + rx.cD + SpellIdsText);
		}
	}

	class ADoAll : Action // line# for msgs good
	{
		private int _count;
		private Rule _myRule;
		public override ATypeID typeid { get { return ATypeID.DoAll; } }
		public List<Action> action;
		public ADoAll(int d, Rule r) : base(d) { this.action = new List<Action>(); this._myRule = r; }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			ATypeID aID;
			Action tmpAct;
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("K") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'K'.");
			if (f.line[f.L++].CompareTo("V") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'V'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			try { this._count = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			for (int i = 0; i < this._count; i++)
			{
				if (f.line[f.L++].CompareTo("i") != 0)
					throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
				try { aID = (ATypeID)Int32.Parse(f.line[f.L++]); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

				try { tmpAct = this._myRule.GetAction(aID, this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

				tmpAct.ImportFromMet(ref f); // <--- recurse
				this.action.Add(tmpAct);
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
			f.line.Add(this.action.Count.ToString());
			foreach (Action a in this.action)
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );

			// Is there something after the operation, even though there shouldn't be?
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo[this.typeid.ToString()]);

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
					)
					{
						f.C = 0;
						return;
					}
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. A Rule must be composed of an IF-DO pair. It cannot have a 'DO:' block immediately following another 'DO:' block.");
				}

				// It better be a valid Action op...
				match = rx.getLeadIn["AnyActionOp"].Match(f.line[f.L]);
				if (!match.Success)
				{
					Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
					if (tmatch.Success)
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["Generic"]);
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation. " + rx.getInfo["Generic"]);
				}
				// It is.

				// How is it tabbed ?
				int nTabs = match.Groups["tabs"].Length;
				if (nTabs <= Rule.ActionContentTabLevel)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Not tabbed-in enough to be inside an Action's All/Any operation. " + rx.getInfo[this.typeid.ToString()]);
				if (nTabs <= depth)
				{   // return, since now done with this operation
					f.C = nTabs;// Math.Max(nTabs - 1, 0);
					return;
				}
				if (nTabs > depth + 1) // error
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Tabbed-in too far. " + rx.getInfo[this.typeid.ToString()]);

				// Here: #tabs does equal depth+1; try to import this op.
				Action tmpAct;
				try { tmpAct = this._myRule.GetAction(this._myRule.actionStrToID[match.Groups["op"].Value], this.depth + 1); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
				f.C = match.Groups["op"].Index + match.Groups["op"].Length;
				tmpAct.ImportFromMetAF(ref f); // <--- recurse
				this.action.Add(tmpAct);
			}
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
			foreach (Action a in this.action)
				a.ExportToMetAF(ref f); // <--- recurse
		}
	}

	class AEmbedNav : Action // line# for msgs good
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
		public AEmbedNav(int d, Meta m) : base(d) { this._myMeta = m; this.my_metAFline = -1; }
		public override ATypeID typeid { get { return ATypeID.EmbedNav; } }
		private string _m_name
		{
			set { this._s_name = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_name); }
		}
		private string _a_name
		{
			set { this._s_name = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_name); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			int nNodesInNav;
			Nav nav = new Nav(this._myMeta);

			// ba = "byte array" ???
			if (f.line[f.L++].CompareTo("ba") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ba'.");
			try { this._exactCharCountToAfterMetNAV_InclCrLf = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			// nav's in-game name
			this._m_name = f.line[f.L++];
			//try { this._name = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			// # nodes in this nav ???
			try { nNodesInNav = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

			this._tag = this._myMeta.GenerateUniqueNavTag();
			this._myMeta.AddToNavsUsed(this._tag, this);
			nav.tag = this._tag;

			// if a nav got imported in-game (empty or not)... read it; otherwise, we're already done
			if (this._exactCharCountToAfterMetNAV_InclCrLf > 5)
				nav.ImportFromMet(ref f);  // hand off importing nav data to the Nav object...

			//this.myMeta.AddNav(this.tag, nav); // added inside Nav instead

			if (this._s_name.CompareTo("[None]") == 0)
				this._s_name = "[none]";
		}

		override public void ExportToMet(ref FileLines f) // line# for msgs good
		{
			Nav tmp;

			try { tmp = this._myMeta.GetNav(this._tag); }
			catch (Exception e) { throw new MyException("" + this.GetType().Name.ToString() + ".ImportFromMet: Error. Unable to find Nav Tag '" + this._tag + "'. [" + e.Message + "]"); }

			f.line.Add("ba");
			this._idxInF_ExactCharCountNumber = f.line.Count;
			f.line.Add("FILL"); // <----- must fill in after the fact

			if (this._s_name.CompareTo("[none]") == 0)
				f.line.Add("[None]"); // nav's in-game name
			else
				f.line.Add(this._m_name); // nav's in-game name

			// nodes in nav
			f.line.Add(tmp.Count.ToString());
			{
				tmp.transform = this._xf;
				tmp.ExportToMet(ref f);
			}
			// go back and fill in the exact char count ...
			this._exactCharCountToAfterMetNAV_InclCrLf = 0;
			for (int i = this._idxInF_ExactCharCountNumber + 1; i < f.line.Count; i++)
				this._exactCharCountToAfterMetNAV_InclCrLf += f.line[i].Length + 2;
			f.line[this._idxInF_ExactCharCountNumber] = this._exactCharCountToAfterMetNAV_InclCrLf.ToString();
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good -- seems goods, anyway. after change.
		{
			this.my_metAFline = f.L;
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._tag = match.Groups["l"].Value;  // literals don't have delimiters
				this._a_name = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				if (match.Groups["xf"].Success)
				{
					Match xfMatch = rx.getParms["ENavXF"].Match(match.Groups["xf"].Value.Substring(1, match.Groups["xf"].Value.Length - 2));
					if(!xfMatch.Success)
						throw new MyException(rx.getInfo["ENavXF"]);
					try
					{
						_xf[0] = Double.Parse(xfMatch.Groups["a"].Value);
						_xf[1] = Double.Parse(xfMatch.Groups["b"].Value);
						_xf[2] = Double.Parse(xfMatch.Groups["c"].Value);
						_xf[3] = Double.Parse(xfMatch.Groups["d"].Value);
						_xf[4] = Double.Parse(xfMatch.Groups["e"].Value);
						_xf[5] = Double.Parse(xfMatch.Groups["f"].Value);
						_xf[6] = Double.Parse(xfMatch.Groups["g"].Value);
					}
					catch(Exception e)
					{
						throw new MyException(rx.getInfo["ENavXF"]+" ["+e.Message+"]");
					}
				}
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
			this._myMeta.AddNavCitationByAction(this._tag, this);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._tag + " " + rx.oD + this._a_name + rx.cD);
		}
	}

	class ACallState : Action // line# for msgs good
	{
		private string _s_toState, _s_retState;
		public override ATypeID typeid { get { return ATypeID.CallState; } }
		public ACallState(int d) : base(d) { }
		private string _m_toState
		{
			set { this._s_toState = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_toState); }
		}
		private string _a_toState
		{
			set { this._s_toState = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_toState); }
		}
		private string _m_retState
		{
			set { this._s_retState = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_retState); }
		}
		private string _a_retState
		{
			set { this._s_retState = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_retState); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("st") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'st'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_toState = f.line[f.L++];
			//try { this._toState = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("ret") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ret'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_retState = f.line[f.L++];
			//try { this._retState = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_toState);
			f.line.Add("s");
			f.line.Add("ret");
			f.line.Add("s");
			f.line.Add(this._m_retState);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._a_toState = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2);  // length is at least 2; remove delimiters
				this._a_retState = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_toState + rx.cD + " " + rx.oD + this._a_retState + rx.cD);
		}
	}

	class AReturn : Action // line# for msgs good
	{
		public override ATypeID typeid { get { return ATypeID.Return; } }
		public AReturn(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'i'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}
	class ADoExpr : Action // line# for msgs good
	{
		private string _s_expr;
		public override ATypeID typeid { get { return ATypeID.DoExpr; } }
		public ADoExpr(int d) : base(d) { this._s_expr = ""; }
		private string _m_expr
		{
			set { this._s_expr = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_expr); }
		}
		private string _a_expr
		{
			set { this._s_expr = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_expr); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("e") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'e'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_expr = f.line[f.L++];
			//try { this._expr = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_expr);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_expr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_expr);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_expr + rx.cD + SpellIdsText);
		}
	}

	class AChatExpr : Action // line# for msgs good
	{
		private string _s_chExpr;
		public override ATypeID typeid { get { return ATypeID.ChatExpr; } }
		public AChatExpr(int d) : base(d) { this._s_chExpr = ""; }
		private string _m_chExpr
		{
			set { this._s_chExpr = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_chExpr); }
		}
		private string _a_chExpr
		{
			set { this._s_chExpr = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_chExpr); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("e") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'e'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_chExpr = f.line[f.L++];
			//try { this._chExpr = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_chExpr);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_chExpr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_chExpr);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_chExpr + rx.cD + SpellIdsText);
		}
	}

	class ASetWatchdog : Action // line# for msgs good
	{
		private string _s_state;
		private double _range, _time;
		public override ATypeID typeid { get { return ATypeID.SetWatchdog; } }
		public ASetWatchdog(int d) : base(d) { }
		private string _m_state
		{
			set { this._s_state = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_state); }
		}
		private string _a_state
		{
			set { this._s_state = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_state); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("3") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '3'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_state = f.line[f.L++];
			//try { this._state = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("r") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'r'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._range = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("t") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 't'.");
			if (f.line[f.L++].CompareTo("d") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'd'.");
			try { this._time = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
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
			f.line.Add(this._m_state);
			f.line.Add("s");
			f.line.Add("r");
			f.line.Add("d");
			f.line.Add(this._range.ToString());
			f.line.Add("s");
			f.line.Add("t");
			f.line.Add("d");
			f.line.Add(this._time.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._range = Double.Parse(match.Groups["d"].Value);
				this._time = Double.Parse(match.Groups["d2"].Value);
				this._a_state = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._range.ToString() + " " + this._time.ToString() + " " + rx.oD + this._a_state + rx.cD);
		}
	}

	class AClearWatchdog : Action // line# for msgs good
	{
		public override ATypeID typeid { get { return ATypeID.ClearWatchdog; } }
		public AClearWatchdog(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}

	class AGetOpt : Action // line# for msgs good
	{
		private string _s_opt, _s_var;
		public override ATypeID typeid { get { return ATypeID.GetOpt; } }
		public AGetOpt(int d) : base(d) { this._s_opt = this._s_var = ""; }
		private string _m_opt
		{
			set { this._s_opt = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_opt); }
		}
		private string _a_opt
		{
			set { this._s_opt = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_opt); }
		}
		private string _m_var
		{
			set { this._s_var = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_var); }
		}
		private string _a_var
		{
			set { this._s_var = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_var); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("o") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'o'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_opt = f.line[f.L++];
			//try { this._opt = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_var = f.line[f.L++];
			//try { this._var = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_opt);
			f.line.Add("s");
			f.line.Add("v");
			f.line.Add("s");
			f.line.Add(this._m_var);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._a_opt = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				this._a_var = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_opt + rx.cD + " " + rx.oD + this._a_var + rx.cD);
		}
	}

	class ASetOpt : Action // line# for msgs good
	{
		private string _s_opt, _s_expr;
		public override ATypeID typeid { get { return ATypeID.SetOpt; } }
		public ASetOpt(int d) : base(d) { this._s_opt = this._s_expr = ""; }
		private string _m_opt
		{
			set { this._s_opt = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_opt); }
		}
		private string _a_opt
		{
			set { this._s_opt = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_opt); }
		}
		private string _m_expr
		{
			set { this._s_expr = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_expr); }
		}
		private string _a_expr
		{
			set { this._s_expr = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_expr); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("o") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'o'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_opt = f.line[f.L++];
			//try { this._opt = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_expr = f.line[f.L++];
			//try { this._expr = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_opt);
			f.line.Add("s");
			f.line.Add("v");
			f.line.Add("s");
			f.line.Add(this._m_expr);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try
			{
				this._a_opt = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				this._a_expr = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_expr);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_opt + rx.cD + " " + rx.oD + this._a_expr + rx.cD + SpellIdsText);
		}
	}

	class ACreateView : Action // line# for msgs good
	{
		// For whatever reason, the XML field of the CreateView action fails to include a newline between it and whatever immediately follows it.
		public static List<int> breakitFixIndices = new List<int>();
		private string _s_vw, _s_xml;
		public override ATypeID typeid { get { return ATypeID.CreateView; } }
		public ACreateView(int d) : base(d) { this._s_vw = this._s_xml = ""; }
		private string _m_vw
		{
			set { this._s_vw = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_vw); }
		}
		private string _a_vw
		{
			set { this._s_vw = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_vw); }
		}
		private string _m_xml
		{
			set { this._s_xml = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_xml); }
		}
		private string _a_xml
		{
			set { this._s_xml = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_xml); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{ // [LINE 188] ACreateView.ImportFromMet: File format error. Expected 20.
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_vw = f.line[f.L++];
			//try { this._vw = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("x") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'x'.");
			if (f.line[f.L++].CompareTo("ba") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'ba'.");
			int tmp;
			try { tmp = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
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
					throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected line length of at least " + (f.line[f.L].Length - 1).ToString() + " characters.");

				// Collapse the XML multi-lines into one XML line
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

			this._m_xml = f.line[f.L++];
			//try { this._xml = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_vw);
			f.line.Add("s");
			f.line.Add("x"); // "v"
			f.line.Add("ba"); // "s"
			f.line.Add((this._m_xml).Length.ToString()); // nothing??
			f.line.Add(this._m_xml);
			ACreateView.breakitFixIndices.Add(f.line.Count - 1); // For dealing with the CreateView "bug"
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
			try
			{
				this._a_vw = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				this._a_xml = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters

				// check if external XML file...
				if (this._a_xml.Length > 0 && this._a_xml[0] == ':')
				{
					string fname = this._m_xml.Substring(1).Trim();
					if (System.IO.File.Exists(System.IO.Path.Join(f.path, fname))) // relative path ?
						fname = System.IO.Path.Join(f.path, fname);
					else if (!System.IO.File.Exists(fname)) // not absolute path either ?
						throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: External file not found. (" + rx.getInfo[this.typeid.ToString()] + ")");

					string acc = "";
					string tmpLine;
					System.IO.StreamReader file = new System.IO.StreamReader(fname);
					while ((tmpLine = file.ReadLine()) != null)
						acc += tmpLine;//.TrimEnd();
					file.Close();

					// Slightly altered _S regex string (replacing open/close delimiters with just start/end of string
					//					string xmlREstr = @"^\" + rx.oD + @"[^\" + rx.oD + @"]|[^\" + rx.oD + @"]\" + rx.oD + @"[^\" + rx.oD + @"]|[^\" + rx.oD + @"]\" + rx.oD + @"$|^\" + rx.cD + @"[^\" + rx.cD + @"]|[^\" + rx.cD + @"]\" + rx.cD + @"[^\" + rx.cD + @"]|[^\" + rx.cD + @"]\" + rx.cD + @"$";
					Match xmlStrMatch = (new Regex(@"^([^\" + rx.oD + @"\" + rx.cD + @"]|\" + rx.oD + @"\" + rx.oD + @"|\" + rx.cD + @"\" + rx.cD + @")*$", RegexOptions.Compiled)).Match(acc);
					if (!xmlStrMatch.Success) // if not-doubled-up string delimiter found in XML file, throw exception
						throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: External XML file still must conform to metaf string restrictions, with the exception of newline characters being allowed. Initial/terminal string delimiters, " + rx.oD + " and " + rx.cD + ", should be omitted, but all internal ones must be doubled-up. (" + rx.getInfo[this.typeid.ToString()] + ")");

					this._a_xml = acc;
				}
			}
			catch (MyException e) { throw new MyException(e.Message); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_vw + rx.cD + " " + rx.oD + this._a_xml + rx.cD);
		}
	}

	class ADestroyView : Action // line# for msgs good
	{
		private string _s_vw;
		public override ATypeID typeid { get { return ATypeID.DestroyView; } }
		public ADestroyView(int d) : base(d) { this._s_vw = ""; }
		private string _m_vw
		{
			set { this._s_vw = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_vw); }
		}
		private string _a_vw
		{
			set { this._s_vw = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_vw); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("1") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 1.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 's'.");
			this._m_vw = f.line[f.L++];
			//try { this._vw = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
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
			f.line.Add(this._m_vw);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);

			try { this._a_vw = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); } // length is at least 2; remove delimiters
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + " " + rx.oD + this._a_vw + rx.cD);
//			f.line.Add(new String('\t', this.depth) + "" + this.GetType().Name.ToString() + " " + rx.oD + this._a_vw + rx.cD);
		}
	}

	class ADestroyAllViews : Action // line# for msgs good
	{
		public override ATypeID typeid { get { return ATypeID.DestroyAllViews; } }
		public ADestroyAllViews(int d) : base(d) { }
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (f.line[f.L++].CompareTo("TABLE") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'TABLE'.");
			if (f.line[f.L++].CompareTo("2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 2.");
			if (f.line[f.L++].CompareTo("k") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'k'.");
			if (f.line[f.L++].CompareTo("v") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'v'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("n") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'n'.");
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 0.");
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
			Match match = rx.getParms[this.typeid.ToString()].Match(thisLN);
			//Match match = rx.getParms[this.typeid.ToString()].Match( f.line[f.L++].Substring(Math.Min(f.C, len)) );
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()]);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString());
		}
	}





	// NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV
	// NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV
	// NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV


	abstract class NavNode : ImportExport
	{
		abstract public NTypeID typeid { get; } //{ return NTypeID.Unassigned; } }
	}

	class NUnassigned : NavNode // line# for msgs good
	{
		public NUnassigned(Nav myNav) : base() { throw new Exception(this.GetType().Name.ToString() + ".NUnassigned: Should never get here."); }
		public override NTypeID typeid { get { return NTypeID.Unassigned; } }
		override public void ImportFromMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Should never get here."); }
		override public void ExportToMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ExportToMet: Should never get here."); }
		override public void ImportFromMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Should never get here."); }
		override public void ExportToMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ExportToMetAF: Should never get here."); }
	}

	// Weird one-off case... kind've a pseudo-node, really; the only one with no xyz (for obvious reasons)
	class NFollow : NavNode // line# for msgs good
	{
		private string _s_tgtName;
		private int _tgtGuid;
		private Nav _myNav;
		public NFollow(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Follow; } }
		private string _m_tgtName
		{
			set { this._s_tgtName = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_tgtName); }
		}
		private string _a_tgtName
		{
			set { this._s_tgtName = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_tgtName); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			this._m_tgtName = f.line[f.L++];
			//try { this._tgtName = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			try { this._tgtGuid = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			//f.line.Add(((int)this.typeid).ToString()); // follow node type not output since there's exactly one node, and 'nav type' already determines what type it is
			f.line.Add(this._m_tgtName);
			f.line.Add(this._tgtGuid.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FOMRAT: flw tgtGUID tgtName
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._tgtGuid = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber);
				this._a_tgtName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._tgtGuid.ToString("X8") + " " + rx.oD + this._a_tgtName + rx.cD);
		}
	}

	class NPoint : NavNode // line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private Nav _myNav;
		public NPoint(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Point; } }
		private double[] _xyz
		{
			get { double[] t = {_x,_y,_z}; return t; }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: pnt myx myy myz
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try {
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString());
		}
	}

	class NPortal : NavNode // !!! VTank DEPRECATED !!!   line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private int _guid;
		private Nav _myNav;
		public NPortal(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Portal; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			try { this._guid = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._guid.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: prt myx myy myz guid
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				this._guid = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " " + this._guid.ToString("X8"));
		}
	}

	class NRecall : NavNode // line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private int _spellID;
		private Dictionary<int, string> _recallSpells;
		private Nav _myNav;
		public NRecall(Nav myNav) : base() {
			this._myNav = myNav;
			this._recallSpells = new Dictionary<int, string>();
			this._recallSpells.Add(48, "Primary Portal Recall");
			this._recallSpells.Add(2647, "Secondary Portal Recall");
			this._recallSpells.Add(1635, "Lifestone Recall");
			this._recallSpells.Add(1636, "Lifestone Sending");
			this._recallSpells.Add(2645, "Portal Recall");
			this._recallSpells.Add(2931, "Recall Aphus Lassel");
			this._recallSpells.Add(2023, "Recall the Sanctuary");
			this._recallSpells.Add(2943, "Recall to the Singularity Caul");
			this._recallSpells.Add(3865, "Glenden Wood Recall");
			this._recallSpells.Add(2041, "Aerlinthe Recall");
			this._recallSpells.Add(2813, "Mount Lethe Recall");
			this._recallSpells.Add(2941, "Ulgrim's Recall");
			this._recallSpells.Add(4084, "Bur Recall");
			this._recallSpells.Add(4198, "Paradox-touched Olthoi Infested Area Recall");
			this._recallSpells.Add(4128, "Call of the Mhoire Forge");
			this._recallSpells.Add(4213, "Colosseum Recall");
			this._recallSpells.Add(5175, "Facility Hub Recall");
			this._recallSpells.Add(5330, "Gear Knight Invasion Area Camp Recall");
			this._recallSpells.Add(5541, "Lost City of Neftet Recall");
			this._recallSpells.Add(4214, "Return to the Keep");
			//this._recallSpells.Add(5175, "Facility Hub Recall"); // repeat of above; unnecessary
			this._recallSpells.Add(6150, "Rynthid Recall");
			this._recallSpells.Add(6321, "Viridian Rise Recall");
			this._recallSpells.Add(6322, "Viridian Rise Great Tree Recall");
																			  // vvvv Not sure why, but the virindi spelldump lists these SpellIDs instead.
			this._recallSpells.Add(6325, "Celestial Hand Stronghold Recall"); // 4907
			this._recallSpells.Add(6327, "Radiant Blood Stronghold Recall");  // 4909
			this._recallSpells.Add(6326, "Eldrytch Web Stronghold Recall");   // 4908
		}
		public override NTypeID typeid { get { return NTypeID.Recall; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}

		public Dictionary<string, int> spellStrToID = new Dictionary<string, int>()
		{
			["Primary Portal Recall"] = 48,
			["Secondary Portal Recall"] = 2647,
			["Lifestone Recall"] = 1635,
			["Lifestone Sending"] = 1636,
			["Portal Recall"] = 2645,
			["Recall Aphus Lassel"] = 2931,
			["Recall the Sanctuary"] = 2023,
			["Recall to the Singularity Caul"] = 2943,
			["Glenden Wood Recall"] = 3865,
			["Aerlinthe Recall"] = 2041,
			["Mount Lethe Recall"] = 2813,
			["Ulgrim's Recall"] = 2941,
			["Bur Recall"] = 4084,
			["Paradox-touched Olthoi Infested Area Recall"] = 4198,
			["Call of the Mhoire Forge"] = 4128,
			["Colosseum Recall"] = 4213,
			["Facility Hub Recall"] = 5175,
			["Gear Knight Invasion Area Camp Recall"] = 5330,
			["Lost City of Neftet Recall"] = 5541,
			["Return to the Keep"] = 4214,
			["Rynthid Recall"] = 6150,
			["Viridian Rise Recall"] = 6321,
			["Viridian Rise Great Tree Recall"] = 6322,
			["Celestial Hand Stronghold Recall"] = 6325,
			["Radiant Blood Stronghold Recall"] = 6327,
			["Eldrytch Web Stronghold Recall"] = 6326
		};
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			try { this._spellID = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			if (!this._recallSpells.ContainsKey(this._spellID))
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Spell ID.");
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._spellID.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: rcl myx myy myz FullRecallSpellName
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				string tmpStr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				if (!this.spellStrToID.ContainsKey(tmpStr))
					throw new MyException("Unrecognized recall spell name.");
				this._spellID = this.spellStrToID[tmpStr];
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + "\n[" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " " + rx.oD + this._recallSpells[this._spellID] + rx.cD);
		}
	}

	class NPause : NavNode // line# for msgs good
	{
		private double _x, _y, _z, _pause;
		private double[] _Txyz;
		private Nav _myNav;
		public NPause(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Pause; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			try { this._pause = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._pause.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: pau myx myy myz PauseInMilliseconds
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				this._pause = Double.Parse(match.Groups["d4"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " " + this._pause.ToString());
		}
	}

	class NChat : NavNode // line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private string _s_chat;
		private Nav _myNav;
		public NChat(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Chat; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}
		private string _m_chat
		{
			set { this._s_chat = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_chat); }
		}
		private string _a_chat
		{
			set { this._s_chat = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_chat); }
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			this._m_chat = f.line[f.L++];
			//try { this._chat = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._m_chat);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: cht myx myy myz ChatInput
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				this._a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(this._a_chat);
			SpellIdsText = (SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "");
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " " + rx.oD + this._a_chat + rx.cD + SpellIdsText);
		}
	}

	class NOpenVendor : NavNode // line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private int _guid;
		private string _s_vendorName;
		private Nav _myNav;
		public NOpenVendor(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.OpenVendor; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}
		private string _m_vendorName
		{
			set { this._s_vendorName = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_vendorName); }
		}
		private string _a_vendorName
		{
			set { this._s_vendorName = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_vendorName); }
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			try { this._guid = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
			this._m_vendorName = f.line[f.L++];
			//try { this._vendorName = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._guid.ToString());
			f.line.Add(this._m_vendorName);
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: vnd myx myy myz tgtGUID tgtName
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);

			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				this._guid = Int32.Parse(match.Groups["h"].Value, System.Globalization.NumberStyles.HexNumber);
				this._a_vendorName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " "
				+ this._guid.ToString("X8") + " " + rx.oD + this._a_vendorName + rx.cD);
		}
	}

	class NPortal_NPC : NavNode // line# for msgs good
	{
		private double _objx, _objy, _objz, _myx, _myy, _myz;
		private double[] _Tobjxyz, _Tmyxyz;
		private string _s_objName;
		private int _objClass;
		private Nav _myNav;
		public NPortal_NPC(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Portal_NPC; } }
		private double[] _myxyz
		{
			get { double[] t = { _myx, _myy, _myz }; return t; }
		}
		private double[] _objxyz
		{
			get { double[] t = { _objx, _objy, _objz }; return t; }
		}
		private string _m_objName
		{
			set { this._s_objName = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_objName); }
		}
		private string _a_objName
		{
			set { this._s_objName = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_objName); }
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._myx = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._myy = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._myz = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");

			this._m_objName = f.line[f.L++];
			//try { this._objName = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			try { this._objClass = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (this._objClass != 14 && this._objClass != 37 && this._objClass != 10) // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Object Class.");
			if (f.line[f.L++].CompareTo("True") != 0) // always "True" ???
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'True'.");

			try { this._objx = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._objy = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._objz = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Tmyxyz = _myNav.ApplyXF(_myxyz);
			_Tobjxyz = _myNav.ApplyXF(_objxyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Tmyxyz[0].ToString());
			f.line.Add(this._Tmyxyz[1].ToString());
			f.line.Add(this._Tmyxyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._m_objName);
			f.line.Add(this._objClass.ToString());
			f.line.Add("True"); // always True ???
			f.line.Add(this._Tobjxyz[0].ToString());
			f.line.Add(this._Tobjxyz[1].ToString());
			f.line.Add(this._Tobjxyz[2].ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: ptl tlk myx myy myz tgtx tgty tgtz tgtObjectClass tgtName
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._myx = Double.Parse(match.Groups["d"].Value);
				this._myy = Double.Parse(match.Groups["d2"].Value);
				this._myz = Double.Parse(match.Groups["d3"].Value);
				this._objx = Double.Parse(match.Groups["d4"].Value);
				this._objy = Double.Parse(match.Groups["d5"].Value);
				this._objz = Double.Parse(match.Groups["d6"].Value);
				this._objClass = Int32.Parse(match.Groups["i"].Value);
				if (this._objClass != 14 && this._objClass != 37 && this._objClass != 10)  // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
					throw new MyException("Object Class typically must be 14 (portal) or 37 (npc).");
				this._a_objName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._myx.ToString() + " " + this._myy.ToString() + " " + this._myz.ToString() + " "
				+ this._objx.ToString() + " " + this._objy.ToString() + " " + this._objz.ToString() + " "
				+ this._objClass.ToString() + " " + rx.oD + this._a_objName + rx.cD);
		}
	}

	class NNPCTalk : NavNode // line# for msgs good
	{
		private double _objx, _objy, _objz, _myx, _myy, _myz;
		private double[] _Tobjxyz, _Tmyxyz;
		private string _s_objName;
		private int _objClass;
		private Nav _myNav;
		public NNPCTalk(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.NPCTalk; } }
		private double[] _myxyz
		{
			get { double[] t = { _myx, _myy, _myz }; return t; }
		}
		private double[] _objxyz
		{
			get { double[] t = { _objx, _objy, _objz }; return t; }
		}
		private string _m_objName
		{
			set { this._s_objName = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_objName); }
		}
		private string _a_objName
		{
			set { this._s_objName = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_objName); }
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._myx = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._myy = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._myz = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");

			this._m_objName = f.line[f.L++];
			//try { this._objName = f.line[f.L++]; }
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			try { this._objClass = Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (this._objClass != 37)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Object Class.");
			if (f.line[f.L++].CompareTo("True") != 0) // always "True" ???
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'True'.");

			try { this._objx = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._objy = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._objz = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Tmyxyz = _myNav.ApplyXF(_myxyz);
			_Tobjxyz = _myNav.ApplyXF(_objxyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Tmyxyz[0].ToString());
			f.line.Add(this._Tmyxyz[1].ToString());
			f.line.Add(this._Tmyxyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._m_objName);
			f.line.Add(this._objClass.ToString());
			f.line.Add("True"); // always True ???
			f.line.Add(this._Tobjxyz[0].ToString());
			f.line.Add(this._Tobjxyz[1].ToString());
			f.line.Add(this._Tobjxyz[2].ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: tlk myx myy myz tgtx tgty tgtz tgtObjectClass tgtName
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._myx = Double.Parse(match.Groups["d"].Value);
				this._myy = Double.Parse(match.Groups["d2"].Value);
				this._myz = Double.Parse(match.Groups["d3"].Value);
				this._objx = Double.Parse(match.Groups["d4"].Value);
				this._objy = Double.Parse(match.Groups["d5"].Value);
				this._objz = Double.Parse(match.Groups["d6"].Value);
				this._objClass = Int32.Parse(match.Groups["i"].Value);
				if (this._objClass != 37)
					throw new MyException("Object Class must be 37 (npc).");
				this._a_objName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._myx.ToString() + " " + this._myy.ToString() + " " + this._myz.ToString() + " "
				+ this._objx.ToString() + " " + this._objy.ToString() + " " + this._objz.ToString() + " "
				+ this._objClass.ToString() + " " + rx.oD + this._a_objName + rx.cD);
		}
	}

	class NCheckpoint : NavNode // line# for msgs good
	{
		private double _x, _y, _z;
		private double[] _Txyz;
		private Nav _myNav;
		public NCheckpoint(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Checkpoint; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: chk myx myy myz
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString());
		}
	}

	class NJump : NavNode // line# for msgs good
	{
		private double _x, _y, _z, _headingDeg, _delayMS;
		private double[] _Txyz;
		private string _s_doHoldShift;
		private Nav _myNav;
		public NJump(Nav myNav) : base() { this._myNav = myNav; }
		public override NTypeID typeid { get { return NTypeID.Jump; } }
		private double[] _xyz
		{
			get { double[] t = { _x, _y, _z }; return t; }
			set { _x = value[0]; _y = value[1]; _z = value[2]; }
		}
		private string _m_doHoldShift
		{
			set { this._s_doHoldShift = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_doHoldShift); }
		}
		private string _a_doHoldShift
		{
			set { this._s_doHoldShift = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_doHoldShift); }
		}
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			try { this._x = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._y = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			try { this._z = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			if (f.line[f.L++].CompareTo("0") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
			try { this._headingDeg = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
			this._m_doHoldShift = f.line[f.L++];
			//try { this._doHoldShift = f.line[f.L++]; } // should ALWAYS be either 'True' or 'False'
			//catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
			try { this._delayMS = Double.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
		}
		override public void ExportToMet(ref FileLines f)
		{
			_Txyz = _myNav.ApplyXF(_xyz);
			f.line.Add(((int)this.typeid).ToString());
			f.line.Add(this._Txyz[0].ToString());
			f.line.Add(this._Txyz[1].ToString());
			f.line.Add(this._Txyz[2].ToString());
			f.line.Add("0");
			f.line.Add(this._headingDeg.ToString());
			f.line.Add(this._m_doHoldShift);
			f.line.Add(this._delayMS.ToString());
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{   // FORMAT: jmp myx myy myz headingInDegrees holdShift delayInMilliseconds
			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(thisLN);
			//Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
			if (!match.Success)
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()]);
			try
			{
				this._x = Double.Parse(match.Groups["d"].Value);
				this._y = Double.Parse(match.Groups["d2"].Value);
				this._z = Double.Parse(match.Groups["d3"].Value);
				this._headingDeg = Double.Parse(match.Groups["d4"].Value);
				this._a_doHoldShift = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
				if (this._s_doHoldShift.CompareTo("True") != 0 && this._s_doHoldShift.CompareTo("False") != 0)
					throw new MyException("'Hold shift' must be " + rx.oD + "True" + rx.cD + " or " + rx.oD + "False" + rx.cD + ".");
				this._delayMS = Double.Parse(match.Groups["d5"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)this.typeid).ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " "
				+ this._headingDeg.ToString() + " " + rx.oD + this._a_doHoldShift + rx.cD + " " + this._delayMS.ToString());
		}
	}

	class Nav : ImportExport // line# for msgs good
	{
		//public static List<Nav> navExisting = new List<Nav>();

		private List<NavNode> _node;
		private NavTypeID _type;
		private int _nodesInMetNav;
		private string _tag;
		private Meta _myMeta;
		public int my_metAFftagline; // "NAV:" line of this nav in a metAF file
		private double[] _xf = { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 }; // [a,b,c,d,e,f,g]
																			 // [ a  b (0)][x] [e]
																			 // [ c  d (0)][y]+[f]
																			 // [(0)(0)(1)][z] [g]
		public double[] transform
		{
			set { this._xf = value; }
			//get { return this._xf; }
		}
		public double[] ApplyXF(double[] xyz)
		{
			double[] nxyz = { 0, 0, 0 };
			nxyz[0] = _xf[0] * xyz[0] + _xf[1] * xyz[1] + _xf[4];
			nxyz[1] = _xf[2] * xyz[0] + _xf[3] * xyz[1] + _xf[5];
			nxyz[2] = xyz[2] + _xf[6];
			return nxyz;
		}
		public int Count { get { return this._node.Count; } }
		public Nav(Meta m) : base() { this._nodesInMetNav = 0; this._myMeta = m; this._node = new List<NavNode>(); this.my_metAFftagline = -1; }
		public string tag
		{
			set { this._tag = value; }	// Still must not contain string delimiters, but regex should inforce that (and, this doesn't even exist in .met)
			get { return this._tag; }
		}
		public NavNode GetNode(NTypeID nid, ref FileLines f)
		{
			switch (nid)
			{
				case NTypeID.Point: return new NPoint(this);
				case NTypeID.Portal: return new NPortal(this);
				case NTypeID.Recall: return new NRecall(this);
				case NTypeID.Pause: return new NPause(this);
				case NTypeID.Chat: return new NChat(this);
				case NTypeID.OpenVendor: return new NOpenVendor(this);
				case NTypeID.Portal_NPC: return new NPortal_NPC(this);
				case NTypeID.NPCTalk: return new NNPCTalk(this);
				case NTypeID.Checkpoint: return new NCheckpoint(this);
				case NTypeID.Jump: return new NJump(this);
			}
			throw new MyException("Invalid Nav Node Type ID.");
		}
		public Dictionary<string, NavTypeID> navTypeStrToID = new Dictionary<string, NavTypeID>()
		{
			["circular"] = NavTypeID.Circular,
			["linear"] = NavTypeID.Linear,
			["follow"] = NavTypeID.Follow,
			["once"] = NavTypeID.Once
		};
		public Dictionary<string, NTypeID> nodeTypeStrToID = new Dictionary<string, NTypeID>()
		{
			["pnt"] = NTypeID.Point,
			["prt"] = NTypeID.Portal,
			["rcl"] = NTypeID.Recall,
			["pau"] = NTypeID.Pause,
			["cht"] = NTypeID.Chat,
			["vnd"] = NTypeID.OpenVendor,
			["ptl"] = NTypeID.Portal_NPC,
			["tlk"] = NTypeID.NPCTalk,
			["chk"] = NTypeID.Checkpoint,
			["jmp"] = NTypeID.Jump
		};
		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{   // Note: should never be called in the first place if there aren't already known to be nodes in the nav
			NavNode tmp;

			if( this.tag == null) // This happens when navOnly			
				this.tag = this._myMeta.GenerateUniqueNavTag();

			// "uTank" version specifier
			if (f.line[f.L++].CompareTo("uTank2 NAV 1.2") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] Nav.ImportFromMet: File format error. Expected 'uTank2 NAV 1.2'.");

			// type of nav: Circular(1), Linear(2), Follow(3), or Once(4)
			try { this._type = (NavTypeID)Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

			// If it's a "follow" nav, we're basically done already
			if (this._type == NavTypeID.Follow)
			{
				tmp = new NFollow(this);
				tmp.ImportFromMet(ref f);
				this._node.Add(tmp); // done
			}
			else
			{
				// #nodes in nav again???
				try { this._nodesInMetNav = Int32.Parse(f.line[f.L++]); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

				for (int i = 0; i < this._nodesInMetNav; i++)
				{
					NTypeID nID;
					try {
						nID = (NTypeID)Int32.Parse(f.line[f.L++]);
						tmp = GetNode(nID, ref f); // can also throw (if integer isn't in correct set; although, the typecast above probably would do that, anyway)
					}
					catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
					tmp.ImportFromMet(ref f);
					this._node.Add(tmp);
				}
			}
			this._myMeta.AddNav(this._tag, this);
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.line.Add("uTank2 NAV 1.2");
			f.line.Add(((int)this._type).ToString());

			// If it's a "follow" nav, we're basically done already
			if (this._type == NavTypeID.Follow)
				this._node[0].ExportToMet(ref f); // Follow navs only have one node each
			else
			{
				f.line.Add(this._node.Count.ToString());
				foreach (NavNode nn in this._node)
					nn.ExportToMet(ref f);
			}
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			// read NAV: info (tag and type)
			this.my_metAFftagline = f.L; // remember this line (for error-reporting, if needed)

			//int len = Math.Max(f.line[f.L].Length - 1, 0);
			string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
			Match match = rx.getParms["NAV:"].Match(thisLN);
			//match = rx.getParms["NAV:"].Match(f.line[f.L++].Substring(f.C)); // advance line
			if( !match.Success )
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo["NAV:"]);

			try { this.tag = match.Groups["l"].Value; }
			catch(Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + e.Message); }

			this._type = this.navTypeStrToID[match.Groups["l2"].Value];

			// now import the nodes
			if (this._type == NavTypeID.Follow)
			{
				f.L--;
				while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
					;

				// Found first non-"blank" line... EOF? not a 'flw' node type?
				if (	f.L >= f.line.Count															// EOF? --> short-circuit to true
						|| (!(match = rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L])).Success)   // apply regex, assign to match (don't advance line) --> short-circuit to true if !Success
						|| (match.Groups["type"].Value.CompareTo("flw") != 0)                       // check if it's the right node type --> short-circuit to true if no
					)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] Nav.ImportFromMetAF: Every 'follow' nav requires exactly one 'flw' nav node.");

				NavNode tmpNode = new NFollow(this);
				tmpNode.ImportFromMetAF(ref f);
				this._node.Add(tmpNode);
			}
			else
			{
				while (f.L < f.line.Count)
				{
					f.L--;
					while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
						;

					// Hit EOF (empty navs allowed)
					if (f.L >= f.line.Count)
						break;

					// Found first non-"blank" line... is it a "NAV:" line ?
					match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
					if (match.Success)
					{
						if( match.Groups["type"].Value.CompareTo("NAV:") == 0)
							break;
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. 'STATE:', 'IF:', and 'DO:' lines must all be above the first 'NAV:' line. " + rx.getInfo["NAV:"]);
					}

					// Get the node type
					match = rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L]); // don't advance line
					if ( ! match.Success )
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Unknown nav node type. " + rx.getInfo["NAV:"]);

					// Make sure the node isn't a 'flw' node
					if ( !nodeTypeStrToID.ContainsKey(match.Groups["type"].Value)) // nodeTypeStrToID doesn't contain 'flw'
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Only 'follow' navs can contain 'flw' nodes. " + rx.getInfo["NAV:"]);

					// Call down to import
					NavNode tmpNode;
					try { tmpNode = this.GetNode(nodeTypeStrToID[match.Groups["type"].Value], ref f); }
					catch (Exception e) { throw new MyException("[LINE " + (f.L + 1).ToString() + "] File format error. Expected a valid nav node type. [" + e.Message + "]"); }
					f.C = 4;// Math.Min(4,f.line[f.L].Length);
					tmpNode.ImportFromMetAF(ref f); // advances line inside
					this._node.Add(tmpNode);
				}
			}
			this._myMeta.AddNav(this.tag, this);
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add("NAV: " + this.tag + " " + ((M_NavTypeID)this._type).ToString() + " ~~ {");
			foreach (NavNode nn in this._node)
				nn.ExportToMetAF(ref f);
			f.line.Add("~~ }");
		}
	}





// REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER
// REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER
// REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER REMAINDER


	class Rule// : ImportExport // line# for msgs good
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
			this._condition = new CUnassigned(Rule.ConditionContentTabLevel);
			this._action = new AUnassigned(Rule.ActionContentTabLevel);
			this._myMeta = myM;
		}
		public Rule(Meta myM, State myS)
		{
			this._condition = new CUnassigned(Rule.ConditionContentTabLevel);
			this._action = new AUnassigned(Rule.ActionContentTabLevel);
			this._myMeta = myM;
			this._myState = myS;
		}
		public void SetMetaState(State s)
		{
			this._myState = s;
		}
		public Condition GetCondition(CTypeID cid, int d)
		{
			switch(cid)
			{
				case CTypeID.Never: return new CNever(d);
				case CTypeID.Always: return new CAlways(d);
				case CTypeID.All: return new CAll(d,this);
				case CTypeID.Any: return new CAny(d,this);
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
				case CTypeID.Not: return new CNot(d,this);
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

		public Action GetAction(ATypeID aid,int d)
		{
			switch (aid)
			{
				case ATypeID.None: return new ANone(d);
				case ATypeID.SetState: return new ASetState(d);
				case ATypeID.Chat: return new AChat(d);
				case ATypeID.DoAll: return new ADoAll(d,this);
				case ATypeID.EmbedNav: return new AEmbedNav(d,this._myMeta);
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

		public string ImportFromMet(ref FileLines f) // line# for msgs good
		{
			CTypeID cID;
			ATypeID aID;

			// Read the condition type, and set-up the data structure for reading the data in a moment
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected 'i'.");
			try { cID = (CTypeID) Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }
			
			try { this._condition = this.GetCondition(cID, Rule.ConditionContentTabLevel); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

			// Read the action type, and set-up the data structure for reading the data in a moment
			if (f.line[f.L++].CompareTo("i") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected 'i'.");
			try { aID = (ATypeID) Int32.Parse(f.line[f.L++]); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected an integer. [" + e.Message + "]"); }

			try { this._action = this.GetAction(aID, Rule.ActionContentTabLevel); }
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: Error. [" + e.Message + "]"); }

			// Read the condition data
			this._condition.ImportFromMet(ref f);

			// Read the action data
			this._action.ImportFromMet(ref f);

			// Read and return the state name
			if (f.line[f.L++].CompareTo("s") != 0)
				throw new MyException("[LINE " + f.L.ToString() + "] File format error. Expected 's'.");

			return f.line[f.L++]; // no need to check it for single internal string delimiters because it's checked for that upon return ///////////////////////
		}

		public void ExportToMet(ref FileLines f, string stateName)
		{
			f.line.Add("i");
			f.line.Add(((int)this._condition.typeid).ToString());
			f.line.Add("i");
			f.line.Add(((int)this._action.typeid).ToString());
			this._condition.ExportToMet(ref f);
			this._action.ExportToMet(ref f);
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
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Hit end-of-file but needed a Condition ('IF:' line).");

			// Found first non-"blank" line... "IF:" ?
			match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
			if( ! match.Success
				|| match.Groups["type"].Value.CompareTo("IF:") != 0
				|| match.Groups["tabs"].Value.Length != Rule.ConditionContentTabLevel-1
				)
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected Condition ('IF:' line). " + rx.getInfo["IF:"]);
			f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

			// Try to grab the Condition keyword
			match = rx.getLeadIn["AnyConditionOp"].Match(f.line[f.L].Substring(f.C)); // don't advance line
			if (!match.Success)
			{
				Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
				if( tmatch.Success )
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["IF:"]);
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected a Condition operation following the 'IF:'. " + rx.getInfo["IF:"]);
			}

			// Try to import this Condition
			try { this._condition = GetCondition(this.conditionStrToID[match.Groups["op"].Value], Rule.ConditionContentTabLevel); }
			catch (Exception e) { throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }
			f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//, f.line[f.L].Length-1);
			this._condition.ImportFromMetAF(ref f); // advances line inside


			// ACTION

			// Find first non-"blank" line
			f.L--;
			while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
				;

			// Prematurely hit end of file
			if (f.L >= f.line.Count)
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Hit end-of-file but needed a Rule Action ('DO:' line).");

			// Found first non-"blank" line... "DO:" ?
			match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
			if (!match.Success
				|| match.Groups["type"].Value.CompareTo("DO:") != 0
				|| match.Groups["tabs"].Value.Length != Rule.ActionContentTabLevel - 1
				)
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected Action ('DO:' line). " + rx.getInfo["DO:"]);
			f.C = match.Groups["tabs"].Value.Length + match.Groups["type"].Value.Length;

			// Try to grab the Action keyword
			match = rx.getLeadIn["AnyActionOp"].Match(f.line[f.L].Substring(f.C)); // don't advance line
			if (!match.Success)
			{
				Match tmatch = rx.getLeadIn["GuessOpSwap"].Match(f.line[f.L].Substring(f.C)); // don't advance line
				if (tmatch.Success)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. (Did you mix up 'All' and 'DoAll', or 'Expr' and 'DoExpr'?) " + rx.getInfo["DO:"]);
				throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected an Action operation following the 'DO:'. " + rx.getInfo["DO:"]);
			}

			// Try to import this Action
			try { this._action = GetAction(this.actionStrToID[match.Groups["op"].Value], Rule.ActionContentTabLevel); }
			catch (Exception e) { throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Error. [" + e.Message + "]"); }

			f.C += match.Groups["op"].Captures[0].Index + match.Groups["op"].Value.Length;//=Math.Min(f.C+..., f.line[f.L].Length-1);
			this._action.ImportFromMetAF(ref f); // advances line inside

			f.C = 0;
		}
		public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add( new String('\t', Rule.ConditionContentTabLevel - 1) + "IF:");
			this._condition.ExportToMetAF(ref f);
			f.line.Add( new String('\t', Rule.ActionContentTabLevel - 1) + "DO:");
			this._action.ExportToMetAF(ref f);
		}
	}

	class State : ImportExport // line# for msgs good
	{
		private string _s_name;
		private Meta _myMeta;
		private List<Rule> _rule;
		public string name { get { return this._s_name; } }
		public int ruleCount { get { return this._rule.Count; } }
		private string _m_name
		{
			set { this._s_name = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_name); }
		}
		private string _a_name
		{
			set { this._s_name = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_name); }
		}
		public State(string name, Meta myM, bool isMetCalling_isNotMetAFCalling)
		{
			if (isMetCalling_isNotMetAFCalling)
				this._m_name = name;
			else
				this._a_name = name;
			//try { this._name = name; }
			//catch (Exception e) { throw new MyException("State.State: " + e.Message); }
			this._rule = new List<Rule>();
			this._myMeta = myM;
		}
		public void AddRule(Rule r)
		{
			this._rule.Add(r);
		}
		override public void ImportFromMet(ref FileLines f) { throw new Exception("State.ImportFromMet: Don't ever call this function; use State's parameter-taking constructor instead."); }
		override public void ExportToMet(ref FileLines f)
		{
			foreach (Rule r in this._rule)
				r.ExportToMet(ref f, this._m_name);
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
				while (++f.L<f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
					;

				// Hit end of file; done
				if (f.L >= f.line.Count)
					break;

				// Found first non-"blank" line... done reading Rules for this state ?  ("STATE:" or "NAV:" line ?)
				match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
				if (!match.Success)
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. " + rx.getInfo["STATE:"]);
				if (match.Groups["type"].Value.CompareTo("STATE:") == 0 || match.Groups["type"].Value.CompareTo("NAV:") == 0)
					break;

				// Start of a new Rule ? ("IF:" line ?)
				if (match.Groups["type"].Value.CompareTo("IF:") != 0) // i.e., it must be a "DO:" line if !="IF:" since it matched StateIfDoNav, and State & Nav were already checked above
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Expected 'STATE:', 'IF:', or 'NAV:' line. (Missing Condition for this Action?) " + rx.getInfo["STATE:"]);

				// It's an "IF:" line; try to import this Rule
				f.C = 0;
				Rule tmpRule = new Rule(this._myMeta, this);
				tmpRule.ImportFromMetAF(ref f);
				this._rule.Add(tmpRule);
			}

			if(this._rule.Count == 0)
				throw new MyException("[LINE " + fLineAtStart.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Every state must contain at least one Rule, even if it's just Never-None. [" + rx.getInfo["STATE:"]+"]");
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add( "STATE: " + rx.oD + this._a_name + rx.cD + " ~~ {");
			foreach( Rule r in this._rule)
				r.ExportToMetAF(ref f);
			f.line.Add("~~ }");
		}
	}
	class Meta : ImportExport // line# for msgs good
	{
		private int _uniqueTagCounter;
		private static string[] _firstLines = { "1", "CondAct", "5", "CType", "AType", "CData", "AData", "State", "n", "n", "n", "n", "n" };
		private List<State> _state;                                     // all states that exist
		private Dictionary<string, Nav> _nav;                           // all navs that exist, cited or not by Actions
		private Dictionary<string, List<AEmbedNav>> _actionUsingNav;    // dictionary[tag] of: list of Actions actually using 'tag' nav (Action cites it, and nav exists)
		private Dictionary<string, List<AEmbedNav>> _actionCitesNav;    // dictionary[tag] of: list of Actions citing use of 'tag' nav
		private string _s_sn; // just a scratch 'state name' variable
		private bool _navOnly;
		public bool IsNavOnly{ get{ return this._navOnly; } }
		private string _m_sn
		{
			set { this._s_sn = rx.m_SetStr(value); }
			get { return rx.m_GetStr(this._s_sn); }
		}
		private string _a_sn
		{
			set { this._s_sn = rx.a_SetStr(value); }
			get { return rx.a_GetStr(this._s_sn); }
		}
		public Meta(bool navOnly=false)
		{
			this._state = new List<State>();
			this._nav = new Dictionary<string, Nav>();
			this._uniqueTagCounter = 0;
			this._actionUsingNav = new Dictionary<string, List<AEmbedNav>>();
			this._actionCitesNav = new Dictionary<string, List<AEmbedNav>>();
			this._navOnly = navOnly;
		}

		public string GenerateUniqueNavTag()
		{
			return ("nav" + (this._uniqueTagCounter++).ToString());
		}
		public void AddToNavsUsed(string tag, AEmbedNav actionEmbNav)
		{
			if (!this._actionUsingNav.ContainsKey(tag))
				this._actionUsingNav.Add(tag, new List<AEmbedNav>());
			this._actionUsingNav[tag].Add(actionEmbNav);
		}
		public void AddNavCitationByAction(string tag, AEmbedNav actionEmbNav)
		{
			if (!this._actionCitesNav.ContainsKey(tag))
				this._actionCitesNav.Add(tag, new List<AEmbedNav>());
			this._actionCitesNav[tag].Add(actionEmbNav);
		}
		// Used to add 'tag' Nav to the list of all those that exist, and to barf if a 'tag' Nav already exists
		public void AddNav(string tag, Nav nav)
		{
			if (this._nav.ContainsKey(tag))
				throw new MyException("NAV already defined for tag '" + tag + "'.");
			this._nav.Add(tag, nav);
		}
		// Used to find out if 'tag' Nav exists, and to return it, if so
		public Nav GetNav(string tag)
		{
			if (!this._nav.ContainsKey(tag))
				throw new MyException("No NAV found with tag '" + tag + "'.");
			return this._nav[tag];
		}

		override public void ImportFromMet(ref FileLines f) // line# for msgs good
		{
			if (!this._navOnly)
			{
				// Intro lines
				foreach (string s in Meta._firstLines)
					if (s.CompareTo(f.line[f.L++]) != 0)
						throw new MyException("[LINE " + f.L.ToString() + "] Unknown file type: First lines do not match expected format.");

				// Number of rules in file
				try { Rule.Count = UInt32.Parse(f.line[f.L++]); }
				catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] Expected number of rules saved in file but didn't find that. [" + e.Message + "]"); }


				// Read all the rules, including embedded navs
				int nRules = 0;
				string prev_sn = null;
				State curState = null;// new State();
				while (nRules < Rule.Count)
				{
					Rule r = new Rule(this);
					this._m_sn = r.ImportFromMet(ref f); // accessor enforces single internal string delimiter rules   // !!!!!
					if (prev_sn == null || prev_sn.CompareTo(this._s_sn) != 0 || curState == null)
					{
						if (curState != null)
							this._state.Add(curState);
						curState = new State(this._m_sn, this, true); // !!!!!
						prev_sn = this._s_sn;
					}
					curState.AddRule(r);
					r.SetMetaState(curState);
					nRules++;
				}
				if (nRules > 0)
					this._state.Add(curState);
			}
			else
			{
				if ("uTank2 NAV 1.2".CompareTo(f.line[f.L]) != 0)
					throw new MyException("[LINE " + (f.L+1).ToString() + "] Unknown file type: First lines do not match expected format.");
				Nav n = new Nav(this);
				n.ImportFromMet(ref f);
			}
		}
		override public void ExportToMet(ref FileLines f)
		{
			f.Clear();

			if (!this._navOnly)
			{
				// Intro lines
				foreach (string s in Meta._firstLines)
				{
					f.line.Add(s);
					f.L++;
				}

				// Number of rules in file
				int ruleCount = 0;
				foreach (State s in this._state)
					ruleCount += s.ruleCount;
				f.line.Add(ruleCount.ToString());

				// ...this.state.Sort()...    by name
				// ^^^ It turns out... VTank doesn't seem to care! :)

				// The rules
				foreach (State s in this._state)
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
				if( this._nav.Count == 0)
					throw new MyException("No navroutes to output!");
				if (this._nav.Count > 1)
					Console.WriteLine("WARNING: Multiple navroutes detected. A .nav file contains only one navroute. Ignoring all but one.");
				foreach (KeyValuePair<string,Nav> kv in this._nav)
				{
					kv.Value.ExportToMet(ref f);
					break;
				}
			}
		}
		override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
		{
			Match match;

			if (!this._navOnly)
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
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["STATE:"]);
					if (match.Groups["type"].Value.CompareTo("NAV:") == 0)
						break;

					// Start of new State ? ("STATE:" line ?)
					if (match.Groups["type"].Value.CompareTo("STATE:") != 0)
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["STATE:"]);

					// Try to import this State
					f.C = 6; // Math.Min(6, f.line[f.L].Length - 1);
					string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(f.C), ""); // don't advance line
					match = rx.getParms["STATE:"].Match(thisLN);
					//f.C = Math.Min(6, f.line[f.L].Length - 1);
					//match = rx.getParms["STATE:"].Match(f.line[f.L].Substring(f.C)); // don't advance line
					if (!match.Success)
						throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. (Did you put a space between the colon and state name?) " + rx.getInfo["STATE:"]);

					// Double check that this state name does not already exist
					string tmpStr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // remove string delimiters from ends
					foreach (State st in this._state)
						if (st.name.CompareTo(tmpStr) == 0)
							throw new MyException("[LINE " + (f.L + 1).ToString() + "] Meta.ImportFromMetAF: State names must be unique; the state name " + rx.oD + tmpStr + rx.cD + " is already in use.");

					// Import this state's contents, and add it to the state list
					State tmpState = new State(tmpStr, this, false); // tempStr is an "AF string"
					f.C = 0;
					f.L++;
					tmpState.ImportFromMetAF(ref f);
					this._state.Add(tmpState);
				}
				if (this._state.Count == 0)
				{
					Console.WriteLine("[LINE " + (f.L + 1).ToString() + "] Meta.ImportFromMetAF: WARNING: You defined no meta states. Handling as a nav-only file.");
					this._navOnly = true;
					//	throw new MyException("[LINE " + f.L.ToString() + "] Meta.ImportFromMetAF: You must define at least one state!");
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
					throw new MyException("[LINE " + (f.L + 1).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. " + rx.getInfo["NAV:"]);

				// Import this nav's contents
				Nav tmpNav = new Nav(this);
				f.C = 4; // Math.Min(4,f.line[f.L].Length-1);
				tmpNav.ImportFromMetAF(ref f);  // the nav adds itself to the list at the end of this call
			}


			// DONE reading all meta info (state and nav) from file

			if (!this._navOnly)
			{
				// Establish successful cross-linking of cited navs to navs that actually exist
				foreach (KeyValuePair<string, List<AEmbedNav>> TagEN in this._actionCitesNav)
				{
					if (this._nav.ContainsKey(TagEN.Key))
					{
						foreach (AEmbedNav en in TagEN.Value)
							this.AddToNavsUsed(TagEN.Key, en);
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
				foreach (KeyValuePair<string, Nav> en in this._nav)
					if (!this._actionCitesNav.ContainsKey(en.Key))
						Console.WriteLine("[LINE " + (en.Value.my_metAFftagline + 1).ToString() + "] WARNING: " + this.GetType().Name.ToString() + ".ImportFromMetAF: Nav tag (" + en.Key + ") is never used.");
			}
		}
		private void CollapseIfDo(ref FileLines f)
		{
			int lead = 0;
			int trail = 0;
			string strConditionCmp = new string('\t', Rule.ConditionContentTabLevel-1) + "IF:";
			string strActionCmp = new string('\t', Rule.ActionContentTabLevel-1) + "DO:";

			// Find the first collapse point (first IF: or DO:)
			while ( trail < f.line.Count )
			{
				if ((f.line[trail].Length >= strConditionCmp.Length && 0 == f.line[trail].Substring(0, Math.Min(strConditionCmp.Length, f.line[trail].Length)).CompareTo(strConditionCmp))
					 || (f.line[trail].Length >= strActionCmp.Length && 0 == f.line[trail].Substring(0, Math.Min(strActionCmp.Length, f.line[trail].Length)).CompareTo(strActionCmp)) )
					break;
				trail++;
			}
			lead = trail + 1;

			// if collapse is needed, collapse lead onto trail, then increment both counters, check lead<Count{copy lead into trail, increment lead}else{break}
			// else increment trail, copy lead into trail, increment lead
			while (lead < f.line.Count)
			{
				// if f.line[trail] "starts" with "IF:" or "DO:"
				if ( 0 == f.line[trail].Substring(0, Math.Min(strConditionCmp.Length, f.line[trail].Length)).CompareTo(strConditionCmp)
					 || 0 == f.line[trail].Substring(0, Math.Min(strActionCmp.Length, f.line[trail].Length)).CompareTo(strActionCmp) )
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
			if ( trail+1 < f.line.Count )
				f.line.RemoveRange(trail+1, f.line.Count - (trail+1));
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.Clear();

			if (!this._navOnly)
			{
				f.line.Add(OutputText.metaHeader);
				foreach (State s in this._state)
				{
					//f.line.Add("~~\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t");
					s.ExportToMetAF(ref f);
				}
				this.CollapseIfDo(ref f);

				if (this._nav.Count > 0)
				{
					f.line.Add("");
					f.line.Add("~~========================= ONLY NAVS APPEAR BELOW THIS LINE =========================~~");
					f.line.Add("");

					foreach (KeyValuePair<string, Nav> sn in this._nav)
						sn.Value.ExportToMetAF(ref f);
				}
			}
			else
			{
				f.line.Add(OutputText.navHeader);
				foreach (KeyValuePair<string, Nav> sn in this._nav)
				{
					sn.Value.ExportToMetAF(ref f);
					break; // only one
				}
			}
		}
	}

	class metaf
    {

		private static string GetOutputFileName(string inFileName, string outExt)
		{
			System.IO.FileInfo fInfo = new System.IO.FileInfo(inFileName);
			string baseFileName = fInfo.Name.Substring(0, fInfo.Name.Length - fInfo.Extension.Length);
			string cutName = new Regex(@"~[0-9]*$").Replace(baseFileName, "");
			int i = 0;
			while (System.IO.File.Exists(cutName + "~" + i.ToString() + outExt))
				i++;
			return cutName + "~" + i.ToString() + outExt;
		}

		static void Main(string[] args)
        {
#if (_DBG_)
			args = myDebug.args;
#endif
			if (args.Length > 0)
			{
				if (args[0].CompareTo("-version") == 0)
				{
					Console.WriteLine("\n" + CmdLnParms.version);
					Environment.Exit(0);
				}
				if (args[0].CompareTo("-new") == 0)
				{
					System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.newFileName);
					fOut.WriteLine(OutputText.metaHeader);
					fOut.Close();
					Console.Write("\n\tOutput file: "+ CmdLnParms.newFileName + "\n");
					Environment.Exit(0);
				}
				if (args[0].CompareTo("-newnav") == 0)
				{
					System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.newnavFileName);
					fOut.WriteLine(OutputText.navHeader);
					fOut.Close();
					Console.Write("\n\tOutput file: "+ CmdLnParms.newnavFileName + "\n");
					Environment.Exit(0);
				}
				if (args[0].CompareTo("-help") == 0)
				{
					System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.readmeFileName);
					fOut.WriteLine(OutputText.readme);
					fOut.Close();
					Console.Write("\n\tOutput file: "+ CmdLnParms.readmeFileName + "\n");

					fOut = new System.IO.StreamWriter(CmdLnParms.refFileName);
					fOut.WriteLine(OutputText.reference);
					fOut.Close();
					Console.Write("\n\tOutput file: " + CmdLnParms.refFileName + "\n");

					Environment.Exit(0);
				}

				string inFileName = args[0];

				// Check if input file exists; if not, exit immediately ... can't continue
				if (!System.IO.File.Exists(inFileName))
				{
					Console.WriteLine("{0} does not exist.", inFileName);
					Environment.Exit(0);
				}

				FileLines f = new FileLines();
				string tmpLine;
				int i = 0;
				bool isMet = true;
				bool isNav = true;

				// Kinda kludgey, but needed for some other countries' handling of doubles (periods vs commas), and easier than editing every
				// to/from string of a double, throughout the code (Parse and Format and CultureInfo.InvariantCulture...)
				// For what was happening, exactly: https://ayulin.net/blog/2015/the-invariant-culture/
				Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; // CultureInfo.CreateSpecificCulture("en-US");

				// Read in the file
				string[] metIntro = { "1", "CondAct", "5", "CType", "AType", "CData", "AData", "State", "n", "n", "n", "n", "n" };
				System.IO.StreamReader file = new System.IO.StreamReader(inFileName);
				while ((tmpLine = file.ReadLine()) != null)
				{
					f.line.Add(tmpLine);
					if (i < metIntro.Length) // Test if it's a .met file
						if (metIntro[i].CompareTo(f.line[i]) != 0)
							isMet = false;
					i++;
				}
				file.Close();

				f.path = (new System.IO.FileInfo(inFileName)).DirectoryName;
				Console.WriteLine(f.path);

				if (f.line.Count == 0)
					throw new MyException("Empty file!");

				if (f.line[0].CompareTo("uTank2 NAV 1.2") != 0)
					isNav = false;
#if (!_DBG_)
				try
				{
#endif
					string outFileName;
					string ext;
					Meta m = new Meta(isNav);
					f.GoToStart();
					if (isMet || isNav)
					{
						m.ImportFromMet(ref f); // Set the line to the start of the lines array, and "import" the data
						m.ExportToMetAF(ref f); // Clear the lines array and "export" the data back into it
						ext = ".af";
					}
					else
					{
						m.ImportFromMetAF(ref f); // Set the line to the start of the lines array, and "import" the data
						m.ExportToMet(ref f); // Clear the lines array and "export" the data back into it
						if (m.IsNavOnly)
							ext = ".nav";
						else
							ext = ".met";
					}

					// Set the output file name
					isNav = m.IsNavOnly;
					if (args.Length > 1)
					{
						// If neither file nor directory exist, ensure directory one up from that specified does exist; outFileName will end up "correct" below
						if ( ! System.IO.File.Exists(args[1]) && ! System.IO.Directory.Exists(args[1]))
                        {
							int li = args[1].LastIndexOf('\\');
							if ( li>0
								|| (li > 1 && args[1].Substring(0,2).CompareTo(@"\\") != 0 ) )
                            {
								System.IO.Directory.CreateDirectory(args[1].Substring( 0, li ));
							}
						}

						// If directory exists, create outFileName for file to place inside it
						if (System.IO.Directory.Exists(args[1]))
						{	// args[1] is a directory. Construct output file name to [over]write inside it.
							System.IO.FileInfo fInfo = new System.IO.FileInfo(inFileName);
							string baseFileName = fInfo.Name.Substring(0, fInfo.Name.Length - fInfo.Extension.Length);
							outFileName = System.IO.Path.Join(args[1],baseFileName+ext);
						}
						else //if (System.IO.File.Exists(args[1]))
						{   // args[1] is a file (or doesn't exist at all). [Over]Write it.
							outFileName = args[1];
						} // else path doesn't exist at all
					}
					else
						outFileName = GetOutputFileName(inFileName, ext);

					// Output the results
					System.IO.StreamWriter fileOut = new System.IO.StreamWriter(outFileName);
					foreach (string ln in f.line)
						fileOut.WriteLine(ln);
					fileOut.Close();
					Console.Write("\n\tOutput file: " + outFileName + "\n");
#if (!_DBG_)
				}
				catch (Exception e)
				{
					Console.WriteLine(e.Message + "\nPress ENTER.");
					System.Console.ReadLine();
				}
#endif
			}
			else // no command-line arguments
				Console.WriteLine("\n\t       USAGE: metaf InputFileName [OutputFileName|OutputDirectoryName]\n\n\t        Help: metaf -help\n\t    New file: metaf -new\n\tNew nav file: metaf -newnav\n\t     Version: metaf -version");
		}
	}
}
