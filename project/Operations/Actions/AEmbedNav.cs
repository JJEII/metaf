// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class AEmbedNav : Action
    {
        private string _s_name;
        private string _tag;
        private int _exactCharCountToAfterMetNAV_InclCrLf;
        private int _idxInF_ExactCharCountNumber;
        private Meta _myMeta;
        private double[] _xf = { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 }; // a, b, c, d, e, f, g:
                                                                      // [ a  b (0)][x] [e]
                                                                      // [ c  d (0)][y]+[f]
                                                                      // [(0)(0)(1)][z] [g]
        public int my_metAFline;
        public AEmbedNav(int d, Meta m) : base(d) { _myMeta = m; my_metAFline = -1; }
        public override ATypeID typeid { get { return ATypeID.EmbedNav; } }
        private string _m_name
        {
            set { _s_name = Rx.m_SetStr(value); }
            get { return Rx.m_GetStr(_s_name); }
        }
        private string _a_name
        {
            set { _s_name = Rx.a_SetStr(value); }
            get { return Rx.a_GetStr(_s_name); }
        }
        override public void ImportFromMet(ref FileLines f)
        {
            int nNodesInNav;
            Nav nav = new Nav(_myMeta);

            // ba = byte array
            if (f.line[f.L++].CompareTo("ba") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected 'ba'.");
            try { _exactCharCountToAfterMetNAV_InclCrLf = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

            // nav's in-game name
            _m_name = f.line[f.L++];

            // # nodes in this nav
            try { nNodesInNav = Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

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

        override public void ExportToMet(ref FileLines f)
        {
            Nav tmp;

            try { tmp = _myMeta.GetNav(_tag); }
            catch (Exception e) { throw new MyException($"{GetType().Name}.ImportFromMet: Error. Unable to find Nav Tag '{_tag}'. [{e.Message}]"); }

            f.line.Add("ba");
            _idxInF_ExactCharCountNumber = f.line.Count;
            f.line.Add("FILL"); // <----- must fill in after the fact

            if (_s_name.CompareTo("[none]") == 0)
                f.line.Add("[None]"); // nav's in-game name
            else
                f.line.Add(_m_name); // nav's in-game name

            // nodes in nav
            f.line.Add(tmp.Count.ToString());
            tmp.transform = _xf;
            tmp.ExportToMet(ref f);

            // go back and fill in the exact char count ...
            _exactCharCountToAfterMetNAV_InclCrLf = 0;
            for (int i = _idxInF_ExactCharCountNumber + 1; i < f.line.Count; i++)
                _exactCharCountToAfterMetNAV_InclCrLf += f.line[i].Length + 2;
            f.line[_idxInF_ExactCharCountNumber] = _exactCharCountToAfterMetNAV_InclCrLf.ToString();
        }
        override public void ImportFromMetAF(ref FileLines f)
        {
            my_metAFline = f.L;
            string thisLN = Rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = Rx.getParms[typeid.ToString()].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]}");
            try
            {
                _tag = match.Groups["l"].Value;  // literals don't have delimiters
                _a_name = match.Groups["s"].Value[1..^1]; // length is at least 2; remove delimiters
                if (match.Groups["xf"].Success)
                {
                    Match xfMatch = Rx.getParms["ENavXF"].Match(match.Groups["xf"].Value[1..^1]);
                    if (!xfMatch.Success)
                        throw new MyException(Rx.getInfo["ENavXF"]);
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
                    catch (Exception e)
                    {
                        throw new MyException($"{Rx.getInfo["ENavXF"]} [{e.Message}]");
                    }
                }
            }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset }] {GetType().Name}.ImportFromMetAF: {Rx.getInfo[typeid.ToString()]} [{e.Message}]"); }
            _myMeta.AddNavCitationByAction(_tag, this);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"{new string('\t', depth)}{typeid} {_tag} {Rx.oD}{_a_name}{Rx.cD}");
        }
    }
}