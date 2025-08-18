// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Metaf
{
    public class Nav : ImportExport
    {
        private List<NavNode> _node;
        private NavTypeID _type;
        private int _nodesInMetNav;
        private string _tag;
        private Meta _myMeta;
        public int my_metAFftagline; // "NAV:" line of this nav in a metAF file
        private double[] _xf = { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 }; // a, b, c, d, e, f, g:
                                                                      // [ a  b (0)][x] [e]
                                                                      // [ c  d (0)][y]+[f]
                                                                      // [(0)(0)(1)][z] [g]

        public double[] transform
        {
            set { _xf = value; }
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
        public Nav(Meta m) : base()
        {
            _nodesInMetNav = 0;
            _myMeta = m;
            _node = new List<NavNode>();
            my_metAFftagline = -1;
        }
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

        public Dictionary<string, NavTypeID> navTypeStrToID = new()
        {
            ["circular"] = NavTypeID.Circular,
            ["linear"] = NavTypeID.Linear,
            ["follow"] = NavTypeID.Follow,
            ["once"] = NavTypeID.Once
        };
        public Dictionary<string, NTypeID> nodeTypeStrToID = new()
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

        override public void ImportFromMet(ref FileLines f)
        {   // Note: this should never be called in the first place if there aren't already known to be nodes in the nav
            NavNode tmp;

            if (tag == null) // This happens when navOnly
                tag = _myMeta.GenerateUniqueNavTag("");

            // "uTank" version specifier
            if (f.line[f.L++].CompareTo("uTank2 NAV 1.2") != 0)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected 'uTank2 NAV 1.2'.");

            // type of nav: Circular(1), Linear(2), Follow(3), or Once(4)
            try { _type = (NavTypeID)Int32.Parse(f.line[f.L++]); }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message }]"); }

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
                try { _nodesInMetNav = Int32.Parse(f.line[f.L++]); }
                catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }

                for (int i = 0; i < _nodesInMetNav; i++)
                {
                    NTypeID nID;
                    try
                    {
                        nID = (NTypeID)Int32.Parse(f.line[f.L++]);
                        tmp = GetNode(nID, ref f); // can also throw (if integer isn't in correct set; although, the typecast above probably would do that, anyway)
                    }
                    catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMet: File format error. Expected an integer. [{e.Message}]"); }
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
        override public void ImportFromMetAF(ref FileLines f)
        {
            // read NAV: info (tag and type)
            my_metAFftagline = f.L; // remember this line (for error-reporting, if needed)

            string thisLN = Rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");
            Match match = Rx.getParms["NAV:"].Match(thisLN);
            if (!match.Success)
                throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {Rx.getInfo["NAV:"]}");

            try { tag = match.Groups["l"].Value; }
            catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset}] {GetType().Name}.ImportFromMetAF: {e.Message}"); }

            _type = navTypeStrToID[match.Groups["l2"].Value];

            // now import the nodes
            if (_type == NavTypeID.Follow)
            {
                f.L--;
                while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                    ;

                // Found first non-"blank" line... EOF? not a 'flw' node type?
                if (f.L >= f.line.Count                                                         // EOF? --> short-circuit to true
                        || (!(match = Rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L])).Success)   // apply regex, assign to match (don't advance line) --> short-circuit to true if !Success
                        || (match.Groups["type"].Value.CompareTo("flw") != 0)                       // check if it's the right node type --> short-circuit to true if no
                    )
                    throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Every 'follow' nav requires exactly one 'flw' nav node.");

                NavNode tmpNode = new NFollow(this);
                tmpNode.ImportFromMetAF(ref f);
                _node.Add(tmpNode);
            }
            else
            {
                while (f.L < f.line.Count)
                {
                    f.L--;
                    while (++f.L < f.line.Count && (match = Rx.R__LN.Match(f.line[f.L])).Success)
                        ;

                    // Hit EOF (empty navs allowed)
                    if (f.L >= f.line.Count)
                        break;

                    // Found first non-"blank" line... is it a "NAV:" line ?
                    match = Rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L]); // don't advance line
                    if (match.Success)
                    {
                        if (match.Groups["type"].Value.CompareTo("NAV:") == 0)
                            break;
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. 'STATE:', 'IF:', and 'DO:' lines must all be above the first 'NAV:' line. {Rx.getInfo["NAV:"]}");
                    }

                    // Get the node type
                    match = Rx.getLeadIn["AnyNavNodeType"].Match(f.line[f.L]); // don't advance line
                    if (!match.Success)
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Syntax error. Unknown nav node type. {Rx.getInfo["NAV:"]}");

                    // Make sure the node isn't a 'flw' node
                    if (!nodeTypeStrToID.ContainsKey(match.Groups["type"].Value)) // nodeTypeStrToID doesn't contain 'flw'
                        throw new MyException($"[LINE {f.L + f.offset + 1}] {GetType().Name}.ImportFromMetAF: Only 'follow' navs can contain 'flw' nodes. {Rx.getInfo["NAV:"]}");

                    // Call down to import
                    NavNode tmpNode;
                    try { tmpNode = GetNode(nodeTypeStrToID[match.Groups["type"].Value], ref f); }
                    catch (Exception e) { throw new MyException($"[LINE {f.L + f.offset + 1}] File format error. Expected a valid nav node type. [{e.Message}]"); }
                    f.C = 4;
                    tmpNode.ImportFromMetAF(ref f); // advances line inside
                    _node.Add(tmpNode);
                }
            }
            _myMeta.AddNav(tag, this);
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            f.line.Add($"NAV: {tag} {(M_NavTypeID)_type} {Rx.LC} {{");
            foreach (NavNode nn in _node)
                nn.ExportToMetAF(ref f);
            f.line.Add($"{Rx.LC} }}");
        }
    }
}