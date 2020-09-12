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
	class myDebug { public static string[] args = { "eskontrol.af" }; }//"__Maybe60W.nav" }; }
#endif
	class CmdLnParms {
		public static string version = "METa Alternate Format (metaf), v.0.7.0.6     GPLv3 Copyright (C) 2020     J. Edwards";
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
		// ??? = 1, // maybe was used for Follow in the past ???
		Recall = 2,
		Pause = 3,
		Chat = 4,
		OpenVendor = 5,
		Portal_NPC = 6,
		NPCTalk = 7,
		Checkpoint = 8,
		Jump = 9
	};
	public enum M_NTypeID // These parallel the NTypeID list, but are used in the metaf file, for NAV node types, by NAME, not by value
	{
		flw = -2,
		Unassigned = -1,
		pnt = 0,
		// ??? = 1, // maybe was used for Follow in the past ???
		rcl = 2,
		pau = 3,
		cht = 4,
		vnd = 5,
		ptl = 6,
		tlk = 7,
		chk = 8,
		jmp = 9
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
			["AnyNavNodeType"] = new Regex(@"^\t(?<type>flw|pnt|rcl|pau|cht|vnd|ptl|tlk|chk|jmp)", RegexOptions.Compiled),
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
			["CreateView"] = "'CreateView' requires two inputs: a string view, a string XML. (" + rx.typeInfo["_S"] + ")",
			["DestroyView"] = "'DestroyView' requires one input: a string view. (" + rx.typeInfo["_S"] + ")",
			["DestroyAllViews"] = "'DestroyAllViews' requires: no inputs.",

			["flw"] = "'flw' requires two inputs: integer target GUID (in hexidecimal), string target name. (" + rx.typeInfo["_I"] + " " + rx.typeInfo["_S"] + ")",
			["pnt"] = "'pnt' requires hree inputs: double xyz-coordinates. (" + rx.typeInfo["_D"] + ")",
			["rcl"] = "'rcl' requires four inputs: double xyz-coordinates, string recall spell name (exact). (" + rx.typeInfo["_D"] + " " + rx.typeInfo["_S"] + ")\nRecognized recall spell names:\n* " + rx.oD + "Primary Portal Recall" + rx.cD + "\n* " + rx.oD + "Secondary Portal Recall" + rx.cD + "\n* " + rx.oD + "Lifestone Recall" + rx.cD + "\n* " + rx.oD + "Portal Recall" + rx.cD + "\n* " + rx.oD + "Recall Aphus Lassel" + rx.cD + "\n* " + rx.oD + "Recall the Sanctuary" + rx.cD + "\n* " + rx.oD + "Recall to the Singularity Caul" + rx.cD + "\n* " + rx.oD + "Glenden Wood Recall" + rx.cD + "\n* " + rx.oD + "Aerlinthe Recall" + rx.cD + "\n* " + rx.oD + "Mount Lethe Recall" + rx.cD + "\n* " + rx.oD + "Ulgrim's Recall" + rx.cD + "\n* " + rx.oD + "Bur Recall" + rx.cD + "\n* " + rx.oD + "Paradox-touched Olthoi Infested Area Recall" + rx.cD + "\n* " + rx.oD + "Call of the Mhoire Forge" + rx.cD + "\n* " + rx.oD + "Lost City of Neftet Recall" + rx.cD + "\n* " + rx.oD + "Return to the Keep" + rx.cD + "\n* " + rx.oD + "Facility Hub Recall" + rx.cD + "\n* " + rx.oD + "Colosseum Recall" + rx.cD + "\n* " + rx.oD + "Gear Knight Invasion Area Camp Recall" + rx.cD + "\n* " + rx.oD + "Rynthid Recall" + rx.cD + "\n* " + rx.oD + "Lifestone Sending" + rx.cD,
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
@"~~ FOR AUTO-COMPLETION ASSISTANCE: testvar getvar setvar touchvar clearallvars clearvar getcharintprop getchardoubleprop getcharquadprop getcharboolprop getcharstringprop getisspellknown getcancastspell_hunt getcancastspell_buff getcharvital_base getcharvital_current getcharvital_buffedmax getcharskill_traininglevel getcharskill_base getcharskill_buffed getplayerlandcell getplayercoordinates coordinategetns coordinategetwe coordinategetz coordinatetostring coordinateparse coordinatedistancewithz coordinatedistanceflat wobjectgetphysicscoordinates wobjectgetname wobjectgetobjectclass wobjectgettemplatetype wobjectgetisdooropen wobjectfindnearestmonster wobjectfindnearestdoor wobjectfindnearestbyobjectclass wobjectfindininventorybytemplatetype wobjectfindininventorybyname wobjectfindininventorybynamerx wobjectgetselection wobjectgetplayer wobjectfindnearestbynameandobjectclass actiontryselect actiontryuseitem actiontryapplyitem actiontrygiveitem actiontryequipanywand actiontrycastbyid actiontrycastbyidontarget chatbox chatboxpaste statushud statushudcolored uigetcontrol uisetlabel isfalse istrue iif randint cstr strlen getobjectinternaltype cstrf stopwatchcreate stopwatchstart stopwatchstop stopwatchelapsedseconds cnumber floor ceiling round abs

~~																						
~~ File auto-generated by metaf, a program created by Eskarina of Morningthaw/Coldeve.	
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
~~ 																						
";

		public const string navHeader =
@"~~ FOR AUTO-COMPLETION ASSISTANCE: testvar getvar setvar touchvar clearallvars clearvar getcharintprop getchardoubleprop getcharquadprop getcharboolprop getcharstringprop getisspellknown getcancastspell_hunt getcancastspell_buff getcharvital_base getcharvital_current getcharvital_buffedmax getcharskill_traininglevel getcharskill_base getcharskill_buffed getplayerlandcell getplayercoordinates coordinategetns coordinategetwe coordinategetz coordinatetostring coordinateparse coordinatedistancewithz coordinatedistanceflat wobjectgetphysicscoordinates wobjectgetname wobjectgetobjectclass wobjectgettemplatetype wobjectgetisdooropen wobjectfindnearestmonster wobjectfindnearestdoor wobjectfindnearestbyobjectclass wobjectfindininventorybytemplatetype wobjectfindininventorybyname wobjectfindininventorybynamerx wobjectgetselection wobjectgetplayer wobjectfindnearestbynameandobjectclass actiontryselect actiontryuseitem actiontryapplyitem actiontrygiveitem actiontryequipanywand actiontrycastbyid actiontrycastbyidontarget chatbox chatboxpaste statushud statushudcolored uigetcontrol uisetlabel isfalse istrue iif randint cstr strlen getobjectinternaltype cstrf stopwatchcreate stopwatchstart stopwatchstop stopwatchelapsedseconds cnumber floor ceiling round abs

~~																						
~~ File auto-generated by metaf, a program created by Eskarina of Morningthaw/Coldeve.	
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
~~ 																						

~~ 																						
~~ 				REMEMBER THAT NAV-ONLY FILES MUST CONTAIN EXACTLY ONE NAV!				
~~ 																						
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
~~		5. COMMENTS, STRINGS, MISCELLANEOUS	
~~		6. QUICK REFERENCE (moved)			
~~		7. FULL REFERENCE (moved)			
~~		8. VIRINDITANK FUNCTIONS (moved)	
~~											

~~ 1. VISUAL CODING ASSISTANCE																								

I strongly recommend you get Notepad++. Your metaf experience will be vastly improved. It's a powerful and free text editor
that can do custom coloring of text (along with several other capabilities I have leveraged). I have created a metaf.xml file
that colors all the relevant keywords, and activating that feature helps substantially.
	1) Download and install Notepad++ from here: https://notepad-plus-plus.org/downloads/
	2) Open it, go to the Language menu, then User Defined Language, and click Define your language...
	3) Click Import..., then navigate to and choose the metaf.xml file. Click Open.
	4) It should now be imported. Close Notepad++, and re-open it, then open this file in it.
Even this documentation file works better with that custom coloring activated. And, meta files work much, much better. So,
get that going as soon as possible! And, feel free to modify the coloration to your own preferences.

NOTE: If you prefer a 'dark mode' coloration style instead, I've also provided a metaf_dark.xml. Follow the instructions
above for it, then also go to the Settings menu, then Style Configurator... and select Language: Global Styles, then select
Style: Default Style, then swap the Foreground and Background colors in the 'Colour Style' box to make the default foreground
white, and background black. Click Save & Close. (Although, while you're in there, I'd also recommend changing a few other
styles' colors: 'Indent guideline style' Foreground (then 'More Colours...') RGB[81,81,81] and Background[0,0,0]; 'Brace
highlight style' Foreground[255,255,255] and Background[0,0,0]; 'Current line background'[28,28,66]; and 'Caret colour'
[191,128,255].)

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
	* It can't display more than a short piece of a text field (and can't even scroll it!), so it's incredibly inefficient
	  to edit anything but the very shortest of text entries
	* It can't duplicate anything (operations, rules, or states) for subsequent minor modification
	* The interface is quite opaque (lacking transparency) in conveying a meta's logical structure
	* It artificially forces states to be ordered alphabetically
	* It can't search for anything (much less search-and-replace) to find where it appears within a meta
	* It doesn't have any sort of annotation capability (i.e., 'code comments')
	* And, I'm sure there are other things, but you get the idea

KEY: Long story, short: metaf suffers from none of the issues listed above. It's a much more friendly way to make metas.

That being said, there is one drawback of some significance that metaf has versus the in-game editor: you don't get instant
feedback on invalid text entries (e.g., expressions). Metaf does not parse the meaning of the inputs you provide; it simply
ensures the basic structure and datatypes are correct, while providing a powerful and transparent editing interface. So, if
you enter gevar[x] when you mean getvar[x], metaf will translate that into .met for VirindiTank to load, and you'll only
discover the mistake at that point. However, if you have already activated in Notepad++ the custom coloration I've provided,
you should have noticed something important just now: the correct function name is bold and a different color, so you can
know you got it right, even in metaf. (Every documented VirindiTank function does this. And, if you haven't yet activated
the custom colors... seriously: do it. Right now. It helps a ton.) I beleive this, in conjunction with the suggested auto-
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
	* For little additional effort, I created a layered-on feature: metaf can transform navroutes during translation into
	  .met. What that means is that for duplicate dungeons, you can create one nav, then simply transform it to create navs
	  for all the other dungeons with the same layout! Seven numbers = entirely new nav! For more, see EmbedNav in Section 7.
	* Also for relatively small effort, I added the ability for metaf to directly edit .nav files (without first being
	  embedded inside a .met file). Just remember that a nav-only file contains exactly one navroute. No more, no less.

One goal for metaf was to provide full-coverage support for the .met format. I believe I have achieved that. 'Success' is
not defined by 'the meta runs without errors' but rather by 'VirindiTank successfully loaded it'. (This in itself is
something of an achievement since a single out-of-place character in .met can trigger VirindiTank to refuse to load it,
simply stating that it's a corrupted file.) The inverse is also needed: 'metaf successfully translated .met to .af.'

~~ 3. GETTING STARTED																										

Running metaf requires that .NET Core be installed. Get it here: https://dotnet.microsoft.com/download
The three files (exe, dll, json) are also required in order to run. The metaf.xml file defines custom colors for Notepad++.
Finally, you'll likely need to authorize the file to run since I haven't formally published it with a digital signature.
(The only thing the program does is read a text-based file you tell it to, and write out another one in a different format.)

I have endeavoured to lower the learning curve as much as I can. metaf can literally translate between .met and .af files by
simply dragging either file type onto its metaf.exe icon. (It auto-detects the file type and translates it to the other
format, outputting a new file in the same directory as the one being translated.) However, that approach will never over-
write anything, instead creating a new and uniquely named output file every time, which may soon get a bit unwieldy if
you're actively developing a meta and repeatedly translating it to load in-game.

There is also a command line interface, and it just takes an input file name, followed (optionally) by an output file name.
If you use that instead, it will output to the specified file, whether or not it already exists. (So, be careful.)

The command line can take four other parameters as well: 'metaf -help' recreates this help file and the metaf reference
file; 'metaf -new' creates a blank template .af file that has the meta-file header text in it, ready for coding a meta;
'metaf -newnav' does the same, but for a 'nav .af'; 'metaf -version' outputs metaf's current version.

I have mapped all the in-game meta operations to a set of recognized text commands in metaf, used for translating back into
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

State order does not matter in metaf. Place them wherever works best for you. (Even move them around while developing, if
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
tabbed Not Not ... Not Any line: the line following is tabbed in five times.

TIP: Because metaf uses a similar code-structure style to the Python language (tabbing means something), you can actually
leverage this fact in Notepad++. While you do lose the custom coloration by doing this, if you set the language to Python
instead of metaf, you can get code-folding for free. (Getting both simultaneously requires writing a custom lexer.)

Once all states are defined, all navroutes (NAV:) are listed below. And, just like with states, the navroute order does not
matter; but just likes with rules in each state, the order of the nav nodes in each navroute does matter.

~~ 5. COMMENTS, STRINGS, MISCELLANEOUS																						

The metaf system supports line commenting. Just put a double-tilde (two ~) anywhere, and the rest of that line is ignored.

Many of the Operation inputs are essentially freeform text strings--from regular expressions to state names and more. In
order to unambiguously identify these inputs as they're intended, metaf requires that strings be delimited by braces { }.
Strings should also be separated by at least one whitespace character (e.g., space or tab). If you need to include braces
inside of a string input, you can do so: simply double it. In other words, when inside a string, {{ in metaf becomes { in
met, and }} becomes }. (Single braces aren't allowed inside metaf strings; if they're there at all, they must be doubled.)

Blank (whitespace-only) lines are ignored. Use them to your advantage if they improve readability.

NOTE: metaf expects decimal numbers to use periods as their decimal separator characters. If you live in a country that uses
commas instead, be aware of that. (.NET defaults to using the local culture for number formatting, which caused metaf to
break in those countries because .met always uses periods (I think). I have now forced .NET it to remain culture-invariant
for metaf, wherever it runs, so that it won't break anymore; but it does require periods, not commas.)

NOTE: When metaf exports from .met to .af, it includes a large comment header. There are multiple reasons for this, but the
main two are these: A) Convenient metaf keyword reference, and B) Notepad++ provides predictive auto-completion of words
based upon what's already been entered into a document, so including all VirindiTank function names and all metaf keywords
right at the start achieves a sort of 'poor man's IntelliSense' that should help with getting them input correctly while
coding your metas (especially the very long VT function names).

~~ 																															
~~ 6. QUICK REFERENCE																										
~~ 																															

	(Moved to metafReference.af. Run 'metaf -help' if you don't have that file.)

~~																															
~~ 7. FULL REFERENCE																										
~~																															

	(Moved to metafReference.af. Run 'metaf -help' if you don't have that file.)

~~																															
~~ 8. VIRINDITANK FUNCTIONS																									
~~																															

	(Moved to metafReference.af. Run 'metaf -help' if you don't have that file.)

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
						  on the same line as its CreateView operation.)
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
				DETAILS: Two inputs, one direct (on the same line) and one indirect (on subsequent lines). The direct input,
				Name, declares this state's name and must be distinct from all other state names. The indirect input, Rule+,
				indicates that every state must contain at least one Rule (IF-DO pair) on the following lines.
				EXAMPLE: STATE: {Hello, world.}
							IF: Never
								DO: None
						--> Defines a state named 'Hello, world.' containing a single rule (IF-DO pair) that does nothing.
						
		IF:   p Condition
				DETAILS: One input. Condition may be any type (including Any/All, which may contain more inside). Every
				Condition (IF) must be followed by an Action (DO).
				EXAMPLE: IF: Any
								Always
						--> Defines a Condition containing an Any operation (which itself contains an Always operation).

		DO:   p Action
				DETAILS: One input. Action may be any type (including DoAll, which may contain more inside).
				EXAMPLE: DO: DoAll
								EmbedNav MyFavorite follow
						--> Defines an Action containing a DoAll operation (which itself contains an EmbedNav operation).

	~~ CONDITION operations (IF:)																							

		All   (none directly)
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Condition operations inside it (on
				following lines, tabbed in one more time). True if all directly-wrapped operations are True. (Empty-All is
				true; Not empty-All is false.) Do not confuse this with the Action DoAll.
				EXAMPLE: All
							Never
							Always
						 --> The All evaluates to False because not all the operations inside it
							 evaluate to True. (Never evaluates to False.)

		Always   (none)
				DETAILS: No inputs. True always. (Always do the corresponding Action.)
				EXAMPLE: Always
						 --> Always is True. (Always True.)

		Any   (none directly)
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Condition operations inside it (on
				following lines, tabbed in one more time). True if any directly-wrapped operations are True. (Empty-Any is
				false; Not empty-Any is true.)
				EXAMPLE: Any
							Never
							Always
						 --> The Any evaluates to True because at least one operation inside it evaluates to True. (Always
							 evaluates to True.)

		BlockE   h Landblock
				DETAILS: One input. True if character location is currently in Landblock.
				EXAMPLE: BlockE 8B370000
						 --> True if leading 4 'digits' of character's @loc match leading 4 'digits'.

		BuPercentGE   i Burden
				DETAILS: One input. True if character burden percent is >= Burden.
				EXAMPLE: BuPercentGE 110
						 --> True if character burden is >= 110%.

		CellE   h Landcell
				DETAILS: One input. True if character location is currently in Landcell.
				EXAMPLE: CellE 8B37E3A1
						 --> True if all 8 'digits' of character's @loc match all 8 'digits'.

		ChatCapture   {r Pattern}   {s ColorIdList}
				DETAILS: Two inputs. True if both Pattern matches a ChatWindow message and that message's color is in the
						 ColorIdList. (Empty fields for either/both count as a match for that field.) Used to 'capture' text
						 into internal variables, which are given the names designated within Pattern with capturegroup_
						 concatenated as a prefix. The message color is in variable capturecolor, and the list is specified
						 as semicolon-separated numbers. See: http://www.virindi.net/wiki/index.php/Virindi_Chat_System_5
						 NOTE: It does not appear to actually be color IDs, but rather the various ChatWindow message type
						 IDs. (I matched to General and Trade, then changed their colors, and they still matched.)
								Colors I've discovered so far (and maybe correctly identified?):
										EnemyDeath				0					?							?
										?						1					?							?
										Say						2					MagicCast					17
										YouThink				3					Allegiance					18
										YouTell					4					Fellowship					19
										?						5					?							20
										HoT/Surge				6					YouEvaded/WereHitByEnemy	21
										YouResist(&HitByMagic?)	7					EnemyEvaded/WasHitByYou		22
										?						8					@hom						23
										?						9					Tinker Applied				24
										?						10					General						27
										?						11					Trade						28
										Emote					12					?							?
										?						13					?							?
										?						14					?							?
										?						15					?							?
				EXAMPLE: ChatCapture {^.*(?<who>Eskarina).* (says|tells you), \"".+\""$} {2;4}
						--> When True:	Variable capturegroup_who holds string 'Eskarina';
									 	Variable capturecolor holds matched-message's colorID.

		ChatMatch   {r Pattern}
				DETAILS: One input. True if Pattern regex matches a ChatWindow message. (Matches any message if empty.)
				EXAMPLE: ChatMatch {^.*Eskarina.* (says|tells you), \"".+\""$}
						 --> Simply detects a regex match in the ChatWindow. Does not capture anything.

		Death   (none)
				DETAILS: No inputs. True if character death detected.
				EXAMPLE: Death
						 --> Triggered on character death.

		DistToRteGE   d Distance
				DETAILS: One input. True if character's shortest-distance to current navroute is >= Distance (in yards).
				EXAMPLE: DistToRteGE
						 --> True when character exceeds  distance from current navroute.

		ExitPortal   (none)
				DETAILS: No inputs. True upon exiting portalspace.
				EXAMPLE: ExitPortal
						 --> True when character leaves portalspace.

		Expr   {s Code}
				DETAILS: One input. True if Code evaluates to True. Do not confuse this with the Action DoExpr.
				EXAMPLE: Expr {7==getobjectinternaltype[getvar[myvar]]}
						 --> True if variable myvar is an object type.
						(See: http://www.virindi.net/wiki/index.php/Meta_Expressions#Function_Information )

		IntoPortal   (none)
				DETAILS: No inputs. True upon entering portalspace.
				EXAMPLE: IntoPortal
						 --> True when character enters portalspace.

		ItemCountGE   i Count   {s Item}
				DETAILS: Two inputs. True if number of Item in inventory is >= Count. Is not a regex.
				EXAMPLE: ItemCountGE 25 {Prismatic Taper}
						 --> True when Prismatic Taper supply in inventory is >= 25.

		ItemCountLE   i Count   {s Item}
				DETAILS: Two inputs. True if number of Item in inventory is <= Count. Is not a regex.
				EXAMPLE: ItemCountLE 25 {Prismatic Taper}
						 --> True when Prismatic Taper supply in inventory is <= 25. (Uh-oh!)

		MainSlotsLE   i Count
				DETAILS: One input. True if number of empty slots remaining in character's main pack
				inventory is <= Count.
				EXAMPLE: MainSlotsLE 7
						 --> True when <=7 inventory slots remain empty in character's main pack.

		MobsInDist_Name   i Count   d Distance   {r Name}
				DETAILS: Three inputs. True if number of (regex-match) monster Name within Distance is >= Count. Completely
				ignores monster priority (including if it's -1).
				EXAMPLE: MobsInDist_Name 5 13.7 {Drudge Lurker}
						 --> True when >=5 Drudge Lurkers are within 13.7 yards of character.

		MobsInDist_Priority   i Count   d Distance   i Priority
				DETAILS: Three inputs. True if number of exact-Priority monsters within Distance is >= Count.
				EXAMPLE: MobsInDist_Priority 6 4.7 2
						 --> True when >=6 monsters of priority >=2 are within 4.7 yards of character.

		NavEmpty   (none)
				DETAILS: No inputs. True if current navroute is empty.
				EXAMPLE: NavEmpty
						 --> True when the current navroute is empty.

		NeedToBuff   (none)
				DETAILS: No inputs. True if VTank's settings determine character needs to buff.
				EXAMPLE: NeedToBuff
						 --> True when VTank's settings determine the character requires buffing.

		Never   (none)
				DETAILS: No inputs. Never True. (Never do the corresponding Action.)
				EXAMPLE: Never
						 --> Never is False. (Never True.)

		NoMobsInDist   d Distance
				DETAILS: One input. True if there are no monsters within Distance of character. Ignores Priority entirely.
				EXAMPLE: NoMobsInDist 20.6
						 --> True when no mobs are within 20.6 yards of character.

		Not   p Condition
				DETAILS: One input. True if Condition operation is False. (May be All or Any.)
				EXAMPLE: Not All
							Always
							Never
						 --> The Not is True because it inverts the All, which is False.

		PSecsInStateGE   i Seconds
				DETAILS: One input. True if time elapsed since entering current state >= Seconds.
				Persistent timer; does not reset if meta is stopped/started.
				EXAMPLE: PSecsInStateGE 15
						 --> True 15 seconds after entering (and staying in) the rule's state, whether or not the meta's
							 execution is turned off/on.

		SecsInStateGE   i Seconds
				DETAILS: One input. True if time elapsed since entering current state >= Seconds. Resets timer if meta is
				stopped/started.
				EXAMPLE: SecsInStateGE 12
						 --> True 12 seconds after entering (and staying in) the rule's state, so long as the meta has
							 been running the whole time. (It resets the timer counter to zero if the meta is turned off
							 and back on, as if it's just entered the state.)
				
		SecsOnSpellGE   i Seconds   i SpellID
				DETAILS: Two inputs. True if time remaining on spell with SpellID is >= Seconds.
				EXAMPLE: SecsOnSpellGE 120 4291
						 --> True if >=120 seconds remain on 'Incantation of Armor Self', which has a SpellID of 4291.
							 (Execute a '/vt dumpspells' command in-game. The far left column of the file it creates is the
							 SpellID column.)
				
		VendorOpen   (none)
				DETAILS: No inputs. True when any vendor window is opened.
				EXAMPLE: VendorOpen
						 --> True if any vendor window is open.

		VendorClosed   (none)
				DETAILS: No inputs. True when a vendor window is closed.
				EXAMPLE: VendorClosed
						 --> True when vendor window is closed.

	~~ ACTION operations (DO:)																								

		CallState   {s ToState}   {s ReturnState}
				DETAILS: Two inputs. Transitions to state ToState, placing ReturnState on the 'call stack' in order to
				remember where to go when ready to return. (See: Return.) Keep CallState and Return in careful balance.
				EXAMPLE: CallState {Do Something} {Done With Something}
						 --> Sets state to state 'Do Something', pushing 'Done With Something' onto the call stack, for later
							 popping, to 'return'.

		Chat   {s Text}
				DETAILS: One input. 'Send' Text as Chat. Do not confuse this with the Action ChatExpr.
				EXAMPLE: Chat {/vt jump 137 true 648}
						 --> The text is entered into and 'sent' to the ChatWindow, causing VTank to turn your character to
							 face a heading of 137 degrees, and then shift-jump after 'holding space' for 648 milliseconds.

		ChatExpr   {s ChatCode}
				DETAILS: One input. Evaluates ChatCode as a 'code', then 'sends' it to ChatWindow. Do not confuse this with
				the Action Chat.
				EXAMPLE: ChatExpr {\/t +getcharstringprop[1]+\, Hi\!}
						 --> Character @tells itself, 'Hi!'

		DoAll   (none directly)
				DETAILS: No direct inputs (on the same line) but does wrap zero or more Action operations inside it (on
				following lines, tabbed in one more time). Do not confuse with Condition All.
				EXAMPLE: DoAll
							Chat {/t Eskarina, Hi!}
							Chat {*dance*}
						 --> Sends Eskarina a direct message of 'Hi!', then emote-dances.

		DoExpr   {s Code}
				DETAILS: One input. Executes Code. Do not confuse this with the Condition Expr, or the Action ChatExpr.
				EXAMPLE: DoExpr {setvar[mycoords,getplayercoordinates[]]}
						 --> Sets variable mycoords to character's current coordinates (coordinate object).

		EmbedNav   l Tag   {s Name} {s Transform (optional)}
				DETAILS: Two inputs (or three). Tag is only used as a 'handle' to reference a navroute in the list of navs at
				the bottom of a metaf file, where it is marked with the same Tag. Name is the name displayed in-game, when
				you examine the embedded name in the meta. Note that Tag can be anything you want it to be, so long as it's a
				valid literal and is distinct from all other nav tags. (There's no reason it needs to be nav## in
				format; it could just as easily be OlthoiMatronHive instead.) The optional Transform input is a string
				containing seven doubles, separated by spaces: {a b c d e f g}, where
							New					Old
							[x]	  [ a   b  {0)] [x]   [e]
							[y] = [ c   d  (0)] [y] + {f]
							[z]	  [(0) (0) (1)] [z]   [g].
				Every nav node with coordinates in it gets transformed accordingly during translation into .met. The default
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
							 when being translated into .met. The second EmbedNav (three inputs) references the same nav
							 as the first (still navH120), but two important differences happen here when translating into
							 .met: first, this nav gets named 'H90' where it's embedded in the code, and second, that third
							 input gets applied as a transform to all the nav nodes. That is the correct transform to put
							 it exactly in the same relative placement within Matron Hive West (90+) as it was in Matron
							 Hive East (120+). And, that's it. Those seven numbers created an entirely new nav.
							 In case you're wondering: South(40+) is {1 0 0 1 1.6 0 0} and North(60+) is {1 0 0 1 1.6 0.8 0}.

		None   (none)
				DETAILS: No inputs. Do nothing. (Action: None.)
				EXAMPLE: None
						 --> Nothing happens.

		Return   (none directly)
				DETAILS: No direct inputs but does expect a state to be on the 'call stack' because it needs to pop a
				state from the stack in order to transition the meta to whatever that state is. (See CallState.) Keep
				CallState and Return in careful balance.
				EXAMPLE: CallState {Do Something} {Done With Something}
						 --> Sets state to state 'Do Something', pushing 'Done With Something' onto
							 the call stack, for later popping, to 'return'.

		SetState   {s Name}
				DETAILS: One input. Set current state to state Name.
				EXAMPLE: SetState {Target Name}
						 --> Meta transitions to state 'Target Name'.

		SetWatchdog   d Distance   d Seconds   {s State name}
				DETAILS: Three inputs. You can set a watchdog in a state that is triggered if at any time while in that
				state your character has not moved >=Distance over the preceding Seconds of time. If triggered, State is
				called. (Returning from it, re-enters the original state.)
				EXAMPLE: SetWatchdog 12.3 4.6 {Oh, no!}
						--> If at some point while in the current state your character hasn't moved at least 12.3 yards in
							the preceding 4.6 seconds, state 'Oh, no!' is called.

		ClearWatchdog   (none)
				DETAILS: No inputs. Clears the watchdog for the current state.
				EXAMPLE: ClearWatchdog
						--> Clears (gets rid of) the current watchdog in this state (if any).

		GetOpt   {s Option}   {s Variable}
				DETAILS: Two inputs. Gets the current value of the VirindiTank Option and saves it in Variable.
				EXAMPLE: GetOpt {OpenDoors} {doors}
						--> Gets current status of the 'OpenDoors' VirindiTank option, and stores it in variable 'doors'.

		SetOpt   {s Option}   {s Expression}
				DETAILS: Two inputs. Sets the VirindiTank Option based upon the results of evaluating Expression.
				EXAMPLE: SetOpt {OpenDoors} {istrue[wobjectfindnearestdoor[]]}
						--> Sets the VirindiTank 'OpenDoors' option to true if any doors are nearby, false otherwise.

		CreateView   {s view Handle}   {s XML}
				DETAILS: Creates a Virindi View with the designated Handle, the layout of which is defined by XML. The XML
				must be on a single line (no line breaks). ---- Are other controls (etc.) recognized? I don't know. For a
				bit more, see: http://www.virindi.net/wiki/index.php/Meta_Views
				EXAMPLE: CreateView {myview} {<?xml version=""1.0""?><view width=""300"" height=""200"" title=""My View""><control type=""layout""><control type=""button"" name=""btnA1"" left=""20"" top=""10"" width=""50"" height=""20"" text=""B1"" actionexpr=""chatbox[\/vt echo B\1\!]"" setstate=""st""/></control></view>}
						--> Creates new Virindi View with handle 'myview' that makes a 300x200 window with title 'My View',
							and one 50x20 button with 'B1' text on it, at (20,10). Pressing it sets the state to 'st', and
							evaluates the expression 'chatbox[\/vt echo B1\!]'.

		DestroyView   {s View}
				DETAILS: Destroy the designated View.
				EXAMPLE: DestroyView {myview}
						--> Destroys the Virindi View with handle 'myview'.
						
		DestroyAllViews   (none)
				DETAILS: No inputs. Destroys all views for this meta.
				EXAMPLE: DestroyAllViews
						--> Destroys any views that exist for this meta.

	~~ NAV TYPES (NAV:)																										

		NAV:   l Tag   l Type
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
				  Point - pnt   d X   d Y   d Z
							Colored more lightly because plain points tend to be 'the movement between the action'.
		   Recall Spell - rcl   d X   d Y   d Z   {s Full Name of Recall Spell}
							Recognized Full Recall Spell Names are, exactly:
								{Primary Portal Recall}					{Bur Recall}
								{Secondary Portal Recall}				{Paradox-touched Olthoi Infested Area Recall}
								{Lifestone Recall}						{Call of the Mhoire Forge}
								{Portal Recall}							{Lost City of Neftet Recall}
								{Recall Aphus Lassel}					{Return to the Keep}
								{Recall the Sanctuary}					{Facility Hub Recall}
								{Recall to the Singularity Caul}		{Colosseum Recall}
								{Glenden Wood Recall}					{Gear Knight Invasion Area Camp Recall}
								{Aerlinthe Recall}						{Rynthid Recall}
								{Mount Lethe Recall}					{Lifestone Sending}
								{Ulgrim's Recall}
				  Pause - pau   d X   d Y   d Z   d Pause (in ms)
		ChatField (any) - cht   d X   d Y   d Z   {s ChatInput}
			 Use Vendor - vnd   d X   d Y   d Z   h Target GUID   {s Target Name}
		 Use Portal/NPC - ptl   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
							Allowed ObjectClass: 14 (Portal), 37 (NPC), 10 (Container, e.g., 'Dangerous Portal Device').
		    Talk to NPC - tlk   d X   d Y   d Z   d TargetX   d TargetY   d TargetZ   i Target ObjectClass   {s Target Name}
							Allowed ObjectClass: 37 (NPC).
		 Nav Checkpoint - chk   d X   d Y   d Z
			   Nav Jump - jmp   d X   d Y   d Z   d HeadingInDegrees   {s HoldShift (True|False)}   d Delay (in ms)

~~																															
~~ 3. VIRINDITANK FUNCTIONS					http://www.virindi.net/wiki/index.php/Meta_Expressions#Function_Information		
~~																															

	~~ VARIABLES																											
		testvar[name]					touchvar[name]
		getvar[name]					clearallvars[]
		setvar[name,value]				clearvar[name]
		
	~~ CHARACTER   			Character properties http://www.virindi.net/wiki/index.php/Meta_Expressions#Object_Properties	
		getcharintprop[id]				wobjectgetplayer[]						getplayerlandcell[]
		getchardoubleprop[id]			getplayercoordinates[]					getcharvital_base[1H/2S/3M]
		getcharboolprop[id]				getcharskill_base[skillID]				getcharvital_current[1H/2S/3M]
		getcharquadprop[id]				getcharskill_buffed[skillID]			getcharvital_buffedmax[1H/2S/3M]
		getcharstringprop[id]			getcharskill_traininglevel[0Unuse/1Untrain/2Train/3Spec]	
		
	~~ CASTING   '/vt dumpspells'  http://www.virindi.net/wiki/index.php/Virindi_Tank_Commands#.2Fvt_commands_-_Game_Info	
		getisspellknown[spellID]		getcancastspell_hunt[spellID]			getcancastspell_buff[spellID]
		actiontryequipanywand[]			actiontrycastbyid[spellID]				actiontrycastbyidontarget[spellID,obj]
		
	~~ INVENTORY																											
		wobjectfindininventorybytemplatetype[templateTypeID]
		wobjectfindininventorybyname[name]
		wobjectfindininventorybynamerx[regex]
		
	~~ LOCATION																												
		getplayerlandcell[]					coordinategetns[coordObj]		coordinatedistancewithz[coordObj1,coordObj2]
		getplayercoordinates[]				coordinategetwe[coordObj]		coordinatedistanceflat[coordObj1,coordObj2]
		coordinatetostring[coordObj]		coordinategetz[coordObj]		wobjectgetphysicscoordinates[obj]
		coordinateparse[string]
		
	~~ OBJECT																												
		wobjectgetselection[]					wobjectgetname[obj]	
		wobjectgetplayer[]						wobjectgetphysicscoordinates[obj]	
		wobjectfindnearestmonster[]				wobjectfindnearestbyobjectclass[objClass]
		wobjectfindnearestdoor[]				wobjectfindnearestbynameandobjectclass[objClass,regex]
		wobjectgetisdooropen[doorObj]			wobjectfindininventorybyname[name]
		wobjectgetobjectclass[obj]				wobjectfindininventorybynamerx[regex]
		wobjectgettemplatetype[obj]				wobjectfindininventorybytemplatetype[templateTypeID]
		getobjectinternaltype[obj]
		
	~~ ACTION																												
		actiontryselect[obj]					actiontryequipanywand[]
		actiontryuseitem[obj]					actiontrycastbyid[spellID]
		actiontrygiveitem[obj,tgtObj]			actiontrycastbyidontarget[spellID,obj]
		actiontryapplyitem[useObj,tgtObj]
		
	~~ UI																													
		CHAT:			chatbox[string]							chatboxpaste[string]
		HUD, etc.:		statushud[key,val]						statushudcolored[key,val,intRGB]
						uigetcontrol[strWindow,strCtrl]			uisetlable[objCtrl,strLabel]
						
	~~ LOGIC																												
		iif[eval,retT,retF]				isfalse[obj]			istrue[obj]
		
	~~ NUMBER																												
		randint[minNumber,maxNumber]			round[number]				coordinategetns[coordObj]
		cstr[number]							abs[number]					coordinategetwe[coordObj]
		cstrf[realNumber,format(probably G)]	getcharintprop[id]			coordinategetz[coordObj]
		cnumber[string]							getchardoubleprop[id]                  
		floor[number]							getcharquadprop[id]
		ceiling[number]							getcharboolprop[id]
		
	~~ STRING																												
		cstr[number]							coordinatetostring[coordObj]
		strlen[string]							coordinateparse[string]
		cstrf[realNumber,format(probably G)]	wobjectgetname[obj]
		cnumber[string]							chatbox[strChatExpr]
		
	~~ TIME																													
		stopwatchcreate[]						stopwatchstop[watchObj]
		stopwatchstart[watchObj]				stopwatchelapsedseconds[watchObj]
		
	~~ MISCELLANEOUS																										
		getobjectinternaltype[obj]  (returns 0=none, 1=number, 3=string, 7=object)

~~																															
~~		METa Alternate Format																					Created by	
~~			  REFERENCE																							 Eskarina	
~~																															";

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
				this._spellID = Int32.Parse(match.Groups["i"].Value);
			}
			catch (Exception e) { throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[this.typeid.ToString()] + " [" + e.Message + "]"); }
		}
		override public void ExportToMetAF(ref FileLines f)
		{
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + this._seconds.ToString() + " " + this._spellID.ToString());
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
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_expr + rx.cD);
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
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_chat + rx.cD);
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
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_expr + rx.cD);
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
			f.line.Add(new String('\t', this.depth) + this.typeid.ToString() + " " + rx.oD + this._a_chExpr + rx.cD);
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
		private string _s_opt, _s_var;
		public override ATypeID typeid { get { return ATypeID.SetOpt; } }
		public ASetOpt(int d) : base(d) { this._s_opt = this._s_var = ""; }
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
				throw new MyException("[LINE " + f.L.ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: File format error. Expected " + (f.line[f.L].Length - 1).ToString() + ".");

			///// Side trip to deal with the CreateView "bug"... ! /////
			int r = f.line.Count;
			f.line.Add(f.line[r - 1]);
			for (; r > f.L + 1; r--)
				f.line[r] = f.line[r - 1];
			f.line[r] = f.line[f.L].Substring(Math.Max(f.line[f.L].Length - 1, 0), 1);
			f.line[f.L] = f.line[f.L].Substring(0, f.line[f.L].Length - 1);

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
			f.line.Add("o");
			f.line.Add("s");
			f.line.Add(this._m_vw);
			f.line.Add("s");
			f.line.Add("v");
			f.line.Add("s");
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
			}
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
			f.line.Add("e");
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
			f.line.Add(new String('\t', this.depth) + "" + this.GetType().Name.ToString() + " " + rx.oD + this._a_vw + rx.cD);
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
			this._recallSpells.Add(5541, "Lost City of Neftet Recall");
			this._recallSpells.Add(4214, "Return to the Keep");
			this._recallSpells.Add(5175, "Facility Hub Recall");
			this._recallSpells.Add(4213, "Colosseum Recall");
			this._recallSpells.Add(5330, "Gear Knight Invasion Area Camp Recall");
			this._recallSpells.Add(6150, "Rynthid Recall");
			this._recallSpells.Add(1636, "Lifestone Sending");
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
			["Lost City of Neftet Recall"] = 5541,
			["Return to the Keep"] = 4214,
			["Facility Hub Recall"] = 5175,
			["Colosseum Recall"] = 4213,
			["Gear Knight Invasion Area Camp Recall"] = 5330,
			["Rynthid Recall"] = 6150,
			["Lifestone Sending"] = 1636
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
			f.line.Add("\t" + ((M_NTypeID)this.typeid).ToString() + " " + this._x.ToString() + " " + this._y.ToString() + " " + this._z.ToString() + " " + rx.oD + this._a_chat + rx.cD);
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
			f.line.Add("NAV: " + this.tag + " " + ((M_NavTypeID)this._type).ToString());
			foreach (NavNode nn in this._node)
				nn.ExportToMetAF(ref f);
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
			f.line.Add( "STATE: " + rx.oD + this._a_name + rx.cD);
			foreach( Rule r in this._rule)
				r.ExportToMetAF(ref f);
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
						outFileName = args[1];
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
				Console.WriteLine("\n\t       USAGE: metaf InputFileName [OutputFileName]\n\n\t        Help: metaf -help\n\t    New file: metaf -new\n\tNew nav file: metaf -newnav\n\t     Version: metaf -version");
		}
	}
}
