// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class ACreateView : Action
    {  // For whatever reason, the XML field of the CreateView action fails to include a newline between it and whatever immediately follows it. (VT *requires* this to be missing.)
        private static readonly string[] metSeq = { "TABLE", "2", "k", "v", "n", "n", "2", "s", "n", "s" };
        public static List<int> breakitFixIndices = new List<int>();
        private string _s_vw, _s_xml;
        public override ATypeID typeid { get { return ATypeID.CreateView; } }
        public ACreateView(int d) : base(d) { _s_vw = _s_xml = ""; }
        private string _m_vw
        {
            set { _s_vw = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_vw); }
        }
        private string _a_vw
        {
            set { _s_vw = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_vw); }
        }
        private string _m_xml
        {
            set { _s_xml = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_xml); }
        }
        private string _a_xml
        {
            set { _s_xml = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_xml); }
        }
        override public void ImportFromMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");
            _m_vw = f.line[f.L++];

            foreach (string s in new[]{"s", "x", "ba"})
                if (f.line[f.L++].CompareTo(s) != 0)
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected '{s}'.");

            int tmp;
            try { tmp = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message }]"); }
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
                    throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected line length of at least {f.line[f.L].Length - 1} characters.");

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

            ///// Side trip to deal with the CreateView "bug" (XML line has more on it than it should.)
            int r = f.line.Count;
            f.line.Add(f.line[r - 1]); // duplicate the final line
            for (; r > f.L + 1; r--)   // move all lines down, back to just below the XML line
                f.line[r] = f.line[r - 1];
            f.line[r] = f.line[f.L].Substring(Math.Max(f.line[f.L].Length - 1, 0), 1); // chop apart the XML line ...
            f.line[f.L] = f.line[f.L][..^1];            // ... since it has more on it than it should

            _m_xml = f.line[f.L++];
        }
        override public void ExportToMet(ref FileLines f)
        {
            foreach (string s in metSeq)
                f.line.Add(s);
            f.line.Add(_m_vw);

            foreach (string s in new[] { "s", "x", "ba" })
                f.line.Add(s);
            f.line.Add(_m_xml.Length.ToString()); // nothing??
            f.line.Add(_m_xml);
            breakitFixIndices.Add(f.line.Count - 1); // For dealing with the CreateView "bug"
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");
            try
            {
                _a_vw = match.Groups["s"].Value.Substring(1, match.Groups["s"].Value.Length - 2); // length is at least 2; remove delimiters
                _a_xml = match.Groups["s2"].Value.Substring(1, match.Groups["s2"].Value.Length - 2); // length is at least 2; remove delimiters

                // check if external XML file...
                if (_a_xml.Length > 0 && _a_xml[0] == ':')
                {
                    string fname = _m_xml.Substring(1).Trim();
                    if (System.IO.File.Exists(System.IO.Path.Join(f.path, fname))) // relative path ?
                        fname = System.IO.Path.Join(f.path, fname);
                    else if (!System.IO.File.Exists(fname)) // not absolute path either ?
                        throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: External file not found. ({Rx.getInfo[typeid.ToString()]})");

                    string acc = "";
                    string tmpLine;
                    System.IO.StreamReader file = new System.IO.StreamReader(fname);
                    while ((tmpLine = file.ReadLine()) != null)
                        acc += tmpLine;//.TrimEnd();
                    file.Close();

                    // Slightly altered _S regex string (replacing open/close delimiters with just start/end of string
                    //					string xmlREstr = @"^\" + Rx.oD + @"[^\" + Rx.oD + @"]|[^\" + Rx.oD + @"]\" + Rx.oD + @"[^\" + Rx.oD + @"]|[^\" + Rx.oD + @"]\" + Rx.oD + @"$|^\" + Rx.cD + @"[^\" + Rx.cD + @"]|[^\" + Rx.cD + @"]\" + Rx.cD + @"[^\" + Rx.cD + @"]|[^\" + Rx.cD + @"]\" + Rx.cD + @"$";
                    Match xmlStrMatch = (new Regex(@"^([^\" + Rx.oD + @"\" + Rx.cD + @"]|\" + Rx.oD + @"\" + Rx.oD + @"|\" + Rx.cD + @"\" + Rx.cD + @")*$", RegexOptions.Compiled)).Match(acc);
                    if (!xmlStrMatch.Success) // if not-doubled-up string delimiter found in XML file, throw exception
                        throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: External XML file still must conform to metaf string restrictions, with the exception of newline characters being allowed. Initial/terminal string delimiters, {Rx.oD} and {Rx.cD}, should be omitted, but all internal ones must be doubled-up. ({Rx.getInfo[typeid.ToString()]})");

                    _a_xml = acc;
                }
            }
            catch (MyException e) { throw new MyException(e.Message); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {Rx.oD}{_a_vw}{Rx.cD} {Rx.oD}{_a_xml}{Rx.cD}");
        }
    }
}