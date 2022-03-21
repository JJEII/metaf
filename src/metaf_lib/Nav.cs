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
    // NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV
    // NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV
    // NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV NAV


    abstract public class NavNode : ImportExport
    {
        abstract public NTypeID typeid { get; } //{ return NTypeID.Unassigned; } }
    }

    public class NUnassigned : NavNode // line# for msgs good
    {
        public NUnassigned(Nav myNav) : base() { throw new Exception(GetType().Name.ToString() + ".NUnassigned: Should never get here."); }
        public override NTypeID typeid { get { return NTypeID.Unassigned; } }
        override public void ImportFromMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: Should never get here."); }
        override public void ExportToMet(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ExportToMet: Should never get here."); }
        override public void ImportFromMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Should never get here."); }
        override public void ExportToMetAF(ref FileLines f) { throw new Exception("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ExportToMetAF: Should never get here."); }
    }

    // Weird one-off case... kind've a pseudo-node, really; the only one with no xyz (for obvious reasons)
    public class NFollow : NavNode // line# for msgs good
    {
        private string _s_tgtName;
        private int _tgtGuid;
        private Nav _myNav;
        public NFollow(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Follow; } }
        private string _m_tgtName
        {
            set { _s_tgtName = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_tgtName); }
        }
        private string _a_tgtName
        {
            set { _s_tgtName = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_tgtName); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            _m_tgtName = f.line[f.L++];
            //try { this._tgtName = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            try { _tgtGuid = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            //f.line.Add(((int)this.typeid).ToString()); // follow node type not output since there's exactly one node, and 'nav type' already determines what type it is
            f.line.Add(_m_tgtName);
            f.line.Add(_tgtGuid.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FOMRAT: flw tgtGUID tgtName
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _tgtGuid = int.Parse(match.Groups["h"].Value, NumberStyles.HexNumber);
                _a_tgtName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _tgtGuid.ToString("X8") + " " + rx.oD + _a_tgtName + rx.cD);
        }
    }

    public class NPoint : NavNode // line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private Nav _myNav;
        public NPoint(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Point; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: pnt myx myy myz
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString());
        }
    }

    public class NPortal : NavNode // !!! VTank DEPRECATED !!!   line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private int _guid;
        private Nav _myNav;
        public NPortal(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Portal; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            try { _guid = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_guid.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: prt myx myy myz guid
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                _guid = int.Parse(match.Groups["h"].Value, NumberStyles.HexNumber);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " " + _guid.ToString("X8"));
        }
    }

    public class NRecall : NavNode // line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private int _spellID;
        private Dictionary<int, string> _recallSpells;
        private Nav _myNav;
        public NRecall(Nav myNav) : base()
        {
            _myNav = myNav;
            _recallSpells = new Dictionary<int, string>();
            _recallSpells.Add(48, "Primary Portal Recall");
            _recallSpells.Add(2647, "Secondary Portal Recall");
            _recallSpells.Add(1635, "Lifestone Recall");
            _recallSpells.Add(1636, "Lifestone Sending");
            _recallSpells.Add(2645, "Portal Recall");
            _recallSpells.Add(2931, "Recall Aphus Lassel");
            _recallSpells.Add(2023, "Recall the Sanctuary");
            _recallSpells.Add(2943, "Recall to the Singularity Caul");
            _recallSpells.Add(3865, "Glenden Wood Recall");
            _recallSpells.Add(2041, "Aerlinthe Recall");
            _recallSpells.Add(2813, "Mount Lethe Recall");
            _recallSpells.Add(2941, "Ulgrim's Recall");
            _recallSpells.Add(4084, "Bur Recall");
            _recallSpells.Add(4198, "Paradox-touched Olthoi Infested Area Recall");
            _recallSpells.Add(4128, "Call of the Mhoire Forge");
            _recallSpells.Add(4213, "Colosseum Recall");
            _recallSpells.Add(5175, "Facility Hub Recall");
            _recallSpells.Add(5330, "Gear Knight Invasion Area Camp Recall");
            _recallSpells.Add(5541, "Lost City of Neftet Recall");
            _recallSpells.Add(4214, "Return to the Keep");
            //this._recallSpells.Add(5175, "Facility Hub Recall"); // repeat of above; unnecessary
            _recallSpells.Add(6150, "Rynthid Recall");
            _recallSpells.Add(6321, "Viridian Rise Recall");
            _recallSpells.Add(6322, "Viridian Rise Great Tree Recall");
            // vvvv Not sure why, but the virindi spelldump lists these SpellIDs instead.
            _recallSpells.Add(6325, "Celestial Hand Stronghold Recall"); // 4907
            _recallSpells.Add(6327, "Radiant Blood Stronghold Recall");  // 4909
            _recallSpells.Add(6326, "Eldrytch Web Stronghold Recall");   // 4908
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
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            try { _spellID = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            if (!_recallSpells.ContainsKey(_spellID))
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Spell ID.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_spellID.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: rcl myx myy myz FullRecallSpellName
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                string tmpStr = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                if (!spellStrToID.ContainsKey(tmpStr))
                    throw new MyException("Unrecognized recall spell name.");
                _spellID = spellStrToID[tmpStr];
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + "\n[" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " " + rx.oD + _recallSpells[_spellID] + rx.cD);
        }
    }

    public class NPause : NavNode // line# for msgs good
    {
        private double _x, _y, _z, _pause;
        private double[] _Txyz;
        private Nav _myNav;
        public NPause(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Pause; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            try { _pause = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_pause.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: pau myx myy myz PauseInMilliseconds
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                _pause = double.Parse(match.Groups["d4"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " " + _pause.ToString());
        }
    }

    public class NChat : NavNode // line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private string _s_chat;
        private Nav _myNav;
        public NChat(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Chat; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
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
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            _m_chat = f.line[f.L++];
            //try { this._chat = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_m_chat);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: cht myx myy myz ChatInput
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                _a_chat = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            string SpellIdsText = OutputText.CommentedAllSpellIdInTextText(_a_chat);
            SpellIdsText = SpellIdsText.Length > 0 ? " ~~" + SpellIdsText : "";
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " " + rx.oD + _a_chat + rx.cD + SpellIdsText);
        }
    }

    public class NOpenVendor : NavNode // line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private int _guid;
        private string _s_vendorName;
        private Nav _myNav;
        public NOpenVendor(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.OpenVendor; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
        private string _m_vendorName
        {
            set { _s_vendorName = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_vendorName); }
        }
        private string _a_vendorName
        {
            set { _s_vendorName = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_vendorName); }
        }

        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            try { _guid = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
            _m_vendorName = f.line[f.L++];
            //try { this._vendorName = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_guid.ToString());
            f.line.Add(_m_vendorName);
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: vnd myx myy myz tgtGUID tgtName
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);

            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                _guid = int.Parse(match.Groups["h"].Value, NumberStyles.HexNumber);
                _a_vendorName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " "
                + _guid.ToString("X8") + " " + rx.oD + _a_vendorName + rx.cD);
        }
    }

    public class NPortal_NPC : NavNode // line# for msgs good
    {
        private double _objx, _objy, _objz, _myx, _myy, _myz;
        private double[] _Tobjxyz, _Tmyxyz;
        private string _s_objName;
        private int _objclass;
        private Nav _myNav;
        public NPortal_NPC(Nav myNav) : base() { _myNav = myNav; }
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
            set { _s_objName = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_objName); }
        }
        private string _a_objName
        {
            set { _s_objName = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_objName); }
        }

        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _myx = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _myy = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _myz = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");

            _m_objName = f.line[f.L++];
            //try { this._objName = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            try { _objclass = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (_objclass != 14 && _objclass != 37 && _objclass != 10) // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Object public class.");
            if (f.line[f.L++].CompareTo("True") != 0) // always "True" ???
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'True'.");

            try { _objx = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _objy = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _objz = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Tmyxyz = _myNav.ApplyXF(_myxyz);
            _Tobjxyz = _myNav.ApplyXF(_objxyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Tmyxyz[0].ToString());
            f.line.Add(_Tmyxyz[1].ToString());
            f.line.Add(_Tmyxyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_m_objName);
            f.line.Add(_objclass.ToString());
            f.line.Add("True"); // always True ???
            f.line.Add(_Tobjxyz[0].ToString());
            f.line.Add(_Tobjxyz[1].ToString());
            f.line.Add(_Tobjxyz[2].ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: ptl tlk myx myy myz tgtx tgty tgtz tgtObjectpublic class tgtName
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _myx = double.Parse(match.Groups["d"].Value);
                _myy = double.Parse(match.Groups["d2"].Value);
                _myz = double.Parse(match.Groups["d3"].Value);
                _objx = double.Parse(match.Groups["d4"].Value);
                _objy = double.Parse(match.Groups["d5"].Value);
                _objz = double.Parse(match.Groups["d6"].Value);
                _objclass = int.Parse(match.Groups["i"].Value);
                if (_objclass != 14 && _objclass != 37 && _objclass != 10)  // object IDs: portal=14, npc=37, container=10 ("Dangerous Portal Device")
                    throw new MyException("Object public class typically must be 14 (portal) or 37 (npc).");
                _a_objName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _myx.ToString() + " " + _myy.ToString() + " " + _myz.ToString() + " "
                + _objx.ToString() + " " + _objy.ToString() + " " + _objz.ToString() + " "
                + _objclass.ToString() + " " + rx.oD + _a_objName + rx.cD);
        }
    }

    public class NNPCTalk : NavNode // line# for msgs good
    {
        private double _objx, _objy, _objz, _myx, _myy, _myz;
        private double[] _Tobjxyz, _Tmyxyz;
        private string _s_objName;
        private int _objclass;
        private Nav _myNav;
        public NNPCTalk(Nav myNav) : base() { _myNav = myNav; }
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
            set { _s_objName = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_objName); }
        }
        private string _a_objName
        {
            set { _s_objName = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_objName); }
        }

        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _myx = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _myy = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _myz = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");

            _m_objName = f.line[f.L++];
            //try { this._objName = f.line[f.L++]; }
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            try { _objclass = int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (_objclass != 37)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Invalid Object public class.");
            if (f.line[f.L++].CompareTo("True") != 0) // always "True" ???
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected 'True'.");

            try { _objx = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _objy = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _objz = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Tmyxyz = _myNav.ApplyXF(_myxyz);
            _Tobjxyz = _myNav.ApplyXF(_objxyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Tmyxyz[0].ToString());
            f.line.Add(_Tmyxyz[1].ToString());
            f.line.Add(_Tmyxyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_m_objName);
            f.line.Add(_objclass.ToString());
            f.line.Add("True"); // always True ???
            f.line.Add(_Tobjxyz[0].ToString());
            f.line.Add(_Tobjxyz[1].ToString());
            f.line.Add(_Tobjxyz[2].ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: tlk myx myy myz tgtx tgty tgtz tgtObjectpublic class tgtName
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _myx = double.Parse(match.Groups["d"].Value);
                _myy = double.Parse(match.Groups["d2"].Value);
                _myz = double.Parse(match.Groups["d3"].Value);
                _objx = double.Parse(match.Groups["d4"].Value);
                _objy = double.Parse(match.Groups["d5"].Value);
                _objz = double.Parse(match.Groups["d6"].Value);
                _objclass = int.Parse(match.Groups["i"].Value);
                if (_objclass != 37)
                    throw new MyException("Object public class must be 37 (npc).");
                _a_objName = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _myx.ToString() + " " + _myy.ToString() + " " + _myz.ToString() + " "
                + _objx.ToString() + " " + _objy.ToString() + " " + _objz.ToString() + " "
                + _objclass.ToString() + " " + rx.oD + _a_objName + rx.cD);
        }
    }

    public class NCheckpoint : NavNode // line# for msgs good
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private Nav _myNav;
        public NCheckpoint(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Checkpoint; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: chk myx myy myz
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString());
        }
    }

    public class NJump : NavNode // line# for msgs good
    {
        private double _x, _y, _z, _headingDeg, _delayMS;
        private double[] _Txyz;
        private string _s_doHoldShift;
        private Nav _myNav;
        public NJump(Nav myNav) : base() { _myNav = myNav; }
        public override NTypeID typeid { get { return NTypeID.Jump; } }
        private double[] _xyz
        {
            get { double[] t = { _x, _y, _z }; return t; }
            set { _x = value[0]; _y = value[1]; _z = value[2]; }
        }
        private string _m_doHoldShift
        {
            set { _s_doHoldShift = rx.m_SetStr(value); }
            get { return rx.m_GetStr(_s_doHoldShift); }
        }
        private string _a_doHoldShift
        {
            set { _s_doHoldShift = rx.a_SetStr(value); }
            get { return rx.a_GetStr(_s_doHoldShift); }
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            try { _x = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _y = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            try { _z = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected '0'.");
            try { _headingDeg = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
            _m_doHoldShift = f.line[f.L++];
            //try { this._doHoldShift = f.line[f.L++]; } // should ALWAYS be either 'True' or 'False'
            //catch (Exception e) { throw new MyException("[LINE " + (f.L+f.offset).ToString() + "] " + this.GetType().Name.ToString() + ".ImportFromMet: " + e.Message); }
            try { _delayMS = double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMet: File format error. Expected a 'double'. [" + e.Message + "]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            f.line.Add(_Txyz[0].ToString());
            f.line.Add(_Txyz[1].ToString());
            f.line.Add(_Txyz[2].ToString());
            f.line.Add("0");
            f.line.Add(_headingDeg.ToString());
            f.line.Add(_m_doHoldShift);
            f.line.Add(_delayMS.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {   // FORMAT: jmp myx myy myz headingInDegrees holdShift delayInMilliseconds
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            //Match match = rx.getParms[((M_NTypeID)this.typeid).ToString()].Match(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length-1)));
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()]);
            try
            {
                _x = double.Parse(match.Groups["d"].Value);
                _y = double.Parse(match.Groups["d2"].Value);
                _z = double.Parse(match.Groups["d3"].Value);
                _headingDeg = double.Parse(match.Groups["d4"].Value);
                _a_doHoldShift = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                if (_s_doHoldShift.CompareTo("True") != 0 && _s_doHoldShift.CompareTo("False") != 0)
                    throw new MyException("'Hold shift' must be " + rx.oD + "True" + rx.cD + " or " + rx.oD + "False" + rx.cD + ".");
                _delayMS = double.Parse(match.Groups["d5"].Value);
            }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo[((M_NTypeID)typeid).ToString()] + " [" + e.Message + "]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("\t" + ((M_NTypeID)typeid).ToString() + " " + _x.ToString() + " " + _y.ToString() + " " + _z.ToString() + " "
                + _headingDeg.ToString() + " " + rx.oD + _a_doHoldShift + rx.cD + " " + _delayMS.ToString());
        }
    }

    public class Nav : ImportExport // line# for msgs good
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
            set { _xf = value; }
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
        public int Count { get { return _node.Count; } }
        public Nav(Meta m) : base() { _nodesInMetNav = 0; _myMeta = m; _node = new List<NavNode>(); my_metAFftagline = -1; }
        public string tag
        {
            set { _tag = value; }  // Still must not contain string delimiters, but regex should inforce that (and, this doesn't even exist in .met)
            get { return _tag; }
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

            if (tag == null) // This happens when navOnly			
                tag = _myMeta.GenerateUniqueNavTag();

            // "uTank" version specifier
            if (f.line[f.L++].CompareTo("uTank2 NAV 1.2") != 0)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Nav.ImportFromMet: File format error. Expected 'uTank2 NAV 1.2'.");

            // type of nav: Circular(1), Linear(2), Follow(3), or Once(4)
            try { _type = (NavTypeID)int.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

            // If it's a "follow" nav, we're basically done already
            if (_type == NavTypeID.Follow)
            {
                tmp = new NFollow(this);
                tmp.ImportFromMet(ref f);
                _node.Add(tmp); // done
            }
            else
            {
                // #nodes in nav again???
                try { _nodesInMetNav = int.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }

                for (int i = 0; i < _nodesInMetNav; i++)
                {
                    NTypeID nID;
                    try
                    {
                        nID = (NTypeID)int.Parse(f.line[f.L++]);
                        tmp = GetNode(nID, ref f); // can also throw (if integer isn't in correct set; although, the typecast above probably would do that, anyway)
                    }
                    catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] Nav.ImportFromMet: File format error. Expected an integer. [" + e.Message + "]"); }
                    tmp.ImportFromMet(ref f);
                    _node.Add(tmp);
                }
            }
            _myMeta.AddNav(_tag, this);
        }
        override public void ExportToMet(ref FileLines f)
        {
            f.line.Add("uTank2 NAV 1.2");
            f.line.Add(((int)_type).ToString());

            // If it's a "follow" nav, we're basically done already
            if (_type == NavTypeID.Follow)
                _node[0].ExportToMet(ref f); // Follow navs only have one node each
            else
            {
                f.line.Add(_node.Count.ToString());
                foreach (NavNode nn in _node)
                    nn.ExportToMet(ref f);
            }
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            // read NAV: info (tag and type)
            my_metAFftagline = f.L; // remember this line (for error-reporting, if needed)

            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = rx.getParms["NAV:"].Match(thisLN);
            //match = rx.getParms["NAV:"].Match(f.line[f.L++].Substring(f.C)); // advance line
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo["NAV:"]);

            try { tag = match.Groups["l"].Value; }
            catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + e.Message); }

            _type = navTypeStrToID[match.Groups["l2"].Value];

            // now import the nodes
            if (_type == NavTypeID.Follow)
            {
                f.L--;
                while (++f.L < f.line.Count && (match = rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Found first non-"blank" line... EOF? not a 'flw' node type?
                if (f.L >= f.line.Count                                                         // EOF? --> short-circuit to true
                        || !(match = rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L])).Success   // apply regex, assign to match (don't advance line) --> short-circuit to true if !Success
                        || match.Groups["type"].Value.CompareTo("flw") != 0                       // check if it's the right node type --> short-circuit to true if no
                    )
                    throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] Nav.ImportFromMetAF: Every 'follow' nav requires exactly one 'flw' nav node.");

                NavNode tmpNode = new NFollow(this);
                tmpNode.ImportFromMetAF(ref f);
                _node.Add(tmpNode);
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
                        if (match.Groups["type"].Value.CompareTo("NAV:") == 0)
                            break;
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. 'STATE:', 'IF:', and 'DO:' lines must all be above the first 'NAV:' line. " + rx.getInfo["NAV:"]);
                    }

                    // Get the node type
                    match = rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L]); // don't advance line
                    if (!match.Success)
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Syntax error. Unknown nav node type. " + rx.getInfo["NAV:"]);

                    // Make sure the node isn't a 'flw' node
                    if (!nodeTypeStrToID.ContainsKey(match.Groups["type"].Value)) // nodeTypeStrToID doesn't contain 'flw'
                        throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Only 'follow' navs can contain 'flw' nodes. " + rx.getInfo["NAV:"]);

                    // Call down to import
                    NavNode tmpNode;
                    try { tmpNode = GetNode(nodeTypeStrToID[match.Groups["type"].Value], ref f); }
                    catch (Exception e) { throw new MyException("[LINE " + (f.L + f.offset + 1).ToString() + "] File format error. Expected a valid nav node type. [" + e.Message + "]"); }
                    f.C = 4;// Math.Min(4,f.line[f.L].Length);
                    tmpNode.ImportFromMetAF(ref f); // advances line inside
                    _node.Add(tmpNode);
                }
            }
            _myMeta.AddNav(tag, this);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add("NAV: " + tag + " " + ((M_NavTypeID)_type).ToString() + " ~~ {");
            foreach (NavNode nn in _node)
                nn.ExportToMetAF(ref f);
            f.line.Add("~~ }");
        }
    }
}
