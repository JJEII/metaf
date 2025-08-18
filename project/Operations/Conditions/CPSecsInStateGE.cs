// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class CPSecsInStateGE : Condition
    {
        private static readonly string[] metSeq = { "i" };
        private int _seconds;
        public override CTypeID typeid { get { return CTypeID.PSecsInStateGE; } }
        public CPSecsInStateGE(int d) : base(d) { _seconds = 9999; }

        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            try { _seconds = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_seconds.ToString());
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L][Math.Min(f.C, f.line[f.L++].Length)..], "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");

            try { _seconds = Int32.Parse(match.Groups["i"].Value); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {_seconds}");
        }
    }
}