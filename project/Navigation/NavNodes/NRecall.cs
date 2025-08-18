// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class NRecall : NavNode
    {
        private double _x, _y, _z;
        private double[] _Txyz;
        private int _spellID;
        private Nav _myNav;
        public NRecall(Nav myNav) : base()
        {
            _myNav = myNav;
        }
        public override NTypeID typeid { get { return NTypeID.Recall; } }
        private double[] _xyz
        {
            get
            {
                double[] t = { _x, _y, _z };
                return t;
            }
        }

        private Dictionary<int, string> _recallSpells = new()
        {
            [48] = "Primary Portal Recall",
            [2647] = "Secondary Portal Recall",
            [1635] = "Lifestone Recall",
            [1636] = "Lifestone Sending",
            [2645] = "Portal Recall",
            [2931] = "Recall Aphus Lassel",
            [2023] = "Recall the Sanctuary",
            [2943] = "Recall to the Singularity Caul",
            [3865] = "Glenden Wood Recall",
            [2041] = "Aerlinthe Recall",
            [2813] = "Mount Lethe Recall",
            [2941] = "Ulgrim's Recall",
            [4084] = "Bur Recall",
            [4198] = "Paradox-touched Olthoi Infested Area Recall",
            [4128] = "Call of the Mhoire Forge",
            [4213] = "Colosseum Recall",
            [5175] = "Facility Hub Recall",
            [5330] = "Gear Knight Invasion Area Camp Recall",
            [5541] = "Lost City of Neftet Recall",
            [4214] = "Return to the Keep",
            //[5175] = "Facility Hub Recall", // repeat of above; unnecessary
            [6150] = "Rynthid Recall",
            [6321] = "Viridian Rise Recall",
            [6322] = "Viridian Rise Great Tree Recall",
            // vvvv Not sure why, but the virindi spelldump lists these SpellIDs instead.
            [6325] = "Celestial Hand Stronghold Recall", // 4907
            [6327] = "Radiant Blood Stronghold Recall",  // 4909
            [6326] = "Eldrytch Web Stronghold Recall"   // 4908
        };
        public Dictionary<string, int> spellStrToID = new()
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

        override public void ImportFromMet(ref FileLines f)
        {
            try { _x = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _y = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            try { _z = Double.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected a 'double'. [{e.Message}]"); }
            if (f.line[f.L++].CompareTo("0") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '0'.");
            try { _spellID = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
            if (!_recallSpells.ContainsKey(_spellID))
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Invalid Spell ID.");
        }
        override public void ExportToMet(ref FileLines f)
        {
            _Txyz = _myNav.ApplyXF(_xyz);
            f.line.Add(((int)typeid).ToString());
            foreach (double c in _Txyz) // len 3
                f.line.Add(c.ToString());
            f.line.Add("0");
            f.line.Add(_spellID.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {   // FORMAT: rcl myx myy myz FullRecallSpellName
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[((M_NTypeID)typeid).ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}");
            try
            {
                _x = Double.Parse(match.Groups["d"].Value);
                _y = Double.Parse(match.Groups["d2"].Value);
                _z = Double.Parse(match.Groups["d3"].Value);
                string tmpStr = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
                if (!spellStrToID.ContainsKey(tmpStr))
                    throw new MyException("Unrecognized recall spell name.");
                _spellID = spellStrToID[tmpStr];
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[((M_NTypeID)typeid).ToString()]}\n[{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"\t{(M_NTypeID)typeid} {_x} {_y} {_z} {Rx.oD}{_recallSpells[_spellID]}{Rx.cD}");
        }
    }
}