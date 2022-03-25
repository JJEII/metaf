using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetAF.enums;
using System.Xml;

namespace MetAF
{
    public abstract class MetaElement : ImportExport
    {

    }

    public class MetaButton : MetaElement
    {
        public string name = "";
        public int top = 0;
        public int left = 0;
        public int width = 0;
        public int height = 0;
        public string text = "";
        public string setState = "";
        public string actionExp = "";

        private int readIntProp(XmlElement node, string name)
        {
            if (node.Attributes[name]?.Value != null)
                return int.Parse(node.Attributes[name]?.Value);
            else
                return 0;
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            var doc = new XmlDocument();
            var blankNode = doc.CreateElement("view");
            ImportFromMet(ref f, blankNode);
        }
        public void ImportFromMet(ref FileLines f, XmlElement buttonNode) // line# for msgs good
        {
            this.name = buttonNode.Attributes["name"]?.Value;
            this.left = readIntProp(buttonNode, "left");
            this.top = readIntProp(buttonNode, "top");
            this.width = readIntProp(buttonNode, "width");
            this.height = readIntProp(buttonNode, "height");
            this.text = buttonNode.Attributes["text"]?.Value;
            this.setState = buttonNode.Attributes["setstate"]?.Value;
            this.actionExp = buttonNode.Attributes["actionexpr"]?.Value;
        }
        override public void ExportToMet(ref FileLines f)
        {

        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {

        }
        override public void ExportToMetAF(ref FileLines f)
        {

        }

        public XmlElement toXML()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement btnNode = XMLSUCKS.MS_IS_DUMB_getNode("button");

            btnNode.MS_IS_DUMB_SetAttribute("name", name);
            btnNode.MS_IS_DUMB_SetAttribute("top", top.ToString());
            btnNode.MS_IS_DUMB_SetAttribute("left", left.ToString());
            btnNode.MS_IS_DUMB_SetAttribute("width", width.ToString());
            btnNode.MS_IS_DUMB_SetAttribute("height", height.ToString());
            btnNode.MS_IS_DUMB_SetAttribute("setState", setState);
            btnNode.MS_IS_DUMB_SetAttribute("actionExp", actionExp);

            return btnNode;
        }
    }
    public class MetaLayout : MetaElement // line# for msgs good
    {
        private List<MetaElement> _nodes = new List<MetaElement>();

        public string name = "";//not in met's xml(hopefully I can add it without breaking stuff)
        public int top = 0;
        public int left = 0;
        public int width = 0;
        public int height = 0;

        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            var doc = new XmlDocument();
            var blankNode = doc.CreateElement("view");
            ImportFromMet(ref f, blankNode);
        }

        private int readIntProp(XmlElement node, string name)
        {
            if (node.Attributes[name]?.Value != null)
                return int.Parse(node.Attributes[name]?.Value);
            else
                return 0;
        }
        public void ImportFromMet(ref FileLines f, XmlElement viewNode) // line# for msgs good
        {
            this.name = viewNode.Attributes["name"]?.Value;
            this.left = readIntProp(viewNode, "left");
            this.top = readIntProp(viewNode, "top");
            this.width = readIntProp(viewNode, "width");
            this.height = readIntProp(viewNode, "height");

            foreach (XmlElement child in viewNode.ChildNodes)
            {

                if (child.Attributes["type"].Value.Equals("layout", StringComparison.OrdinalIgnoreCase))
                {
                    var layout = new MetaLayout();
                    layout.ImportFromMet(ref f, child);
                    _nodes.Add(layout);
                }
                else if (child.Attributes["type"].Value.Equals("button", StringComparison.OrdinalIgnoreCase))
                {
                    var button = new MetaButton();
                    button.ImportFromMet(ref f, child);
                    _nodes.Add(button);
                }
                else
                    throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Invalid XML node " + child.Name);
            }
        }
        override public void ExportToMet(ref FileLines f)
        {

        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {

        }
        override public void ExportToMetAF(ref FileLines f)
        {

        }
        public XmlElement toXML()
        {
            
            XmlElement layout = XMLSUCKS.MS_IS_DUMB_getNode("layout");
            layout.MS_IS_DUMB_SetAttribute("name", name);
            layout.MS_IS_DUMB_SetAttribute("top", top.ToString());
            layout.MS_IS_DUMB_SetAttribute("left", left.ToString());
            layout.MS_IS_DUMB_SetAttribute("width", width.ToString());
            layout.MS_IS_DUMB_SetAttribute("height", height.ToString());


            foreach (var child in _nodes)
            {
                if(child is MetaLayout)
                {
                    var metaLayout = (MetaLayout)child;
                    var node = metaLayout.toXML();
                    //layout.OwnerDocument.ImportNode(node, true);
                    layout.AppendChild(node);
                }
                if (child is MetaButton)
                {
                    var metaButton = (MetaButton)child;
                    var node = metaButton.toXML();
                    //layout.OwnerDocument.ImportNode(node, true);
                    layout.AppendChild(metaButton.toXML());
                }
            }

            return layout;
        }
    }
    public class MetaView : MetaElement // line# for msgs good
    {
        private Meta _myMeta;

        private List<MetaElement> _nodes;
        public string formId;
        public string title = "";
        public int width = 0;
        public int height = 0;
        public int my_metAFftagline; // "NAV:" line of this nav in a metAF file



        public MetaView(Meta m) : base() {
            _myMeta = m;
            _nodes = new List<MetaElement>();
            my_metAFftagline = -1;
        }
        private int readIntProp(XmlElement node, string name)
        {
            if (node.Attributes[name]?.Value != null)
                return int.Parse(node.Attributes[name]?.Value);
            else
                return 0;
        }
        override public void ImportFromMet(ref FileLines f) // line# for msgs good
        {
            var doc = new XmlDocument();
            var blankNode = doc.CreateElement("view");
            ImportFromMet(ref f, blankNode);
        }
        public void ImportFromMet(ref FileLines f, XmlElement viewNode) // line# for msgs good
        {   // Note: should never be called in the first place if there aren't already known to be nodes in the nav
            this.width = readIntProp(viewNode, "width");
            this.height = readIntProp(viewNode, "height");
            this.title = viewNode.Attributes["title"].Value;

            foreach (XmlElement child in viewNode.ChildNodes)
            {
                
                if(child.Attributes["type"].Value.Equals("layout", StringComparison.OrdinalIgnoreCase))
                {
                    var layout = new MetaLayout();
                    layout.ImportFromMet(ref f, child);
                    _nodes.Add(layout);
                } 
                else if (child.Attributes["type"].Value.Equals("button", StringComparison.OrdinalIgnoreCase))
                {
                    var button = new MetaButton();
                    button.ImportFromMet(ref f, child);
                    _nodes.Add(button);
                }
                else
                    throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: Invalid XML node " + child.Name);
            }

            _myMeta.AddView(this.formId, this);
            //_myMeta.AddNav(_tag, this);
        }
        override public void ExportToMet(ref FileLines f)
        {
/*            f.line.Add("uTank2 NAV 1.2");
            f.line.Add(((int)_type).ToString());

            // If it's a "follow" nav, we're basically done already
            if (_type == NavTypeID.Follow)
                _node[0].ExportToMet(ref f); // Follow navs only have one node each
            else
            {
                f.line.Add(_node.Count.ToString());
                foreach (NavNode nn in _node)
                    nn.ExportToMet(ref f);
            }*/
        }
        override public void ImportFromMetAF(ref FileLines f) // line# for msgs good
        {
            // read NAV: info (tag and type)
            my_metAFftagline = f.L; // remember this line (for error-reporting, if needed)

            /*            

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
                        _myMeta.AddNav(tag, this);*/
        }
        override public void ExportToMetAF(ref FileLines f)
        {
            XmlElement viewNode = XMLSUCKS.MS_IS_DUMB_getNode("view");

            //var t = XmlElement.
            viewNode.MS_IS_DUMB_SetAttribute("width", width.ToString());
            viewNode.MS_IS_DUMB_SetAttribute("height", height.ToString());
            viewNode.MS_IS_DUMB_SetAttribute("title", title);


            foreach (var child in _nodes)
            {
                if (child is MetaLayout)
                {
                    var metaLayout = (MetaLayout)child;
                    XmlElement node = metaLayout.toXML();
                    //view.OwnerDocument.ImportNode(node, true);
                    viewNode.AppendChild(node);
                }
                if (child is MetaButton)
                {
                    var metaButton = (MetaButton)child;
                    XmlElement node = metaButton.toXML();
                    //view.OwnerDocument.ImportNode(node, true);
                    viewNode.AppendChild(node);
                }
            }

            
            f.line.Add("VIEW: " + formId + " {" + title + "} {");
            f.line.Add(viewNode.ToString(1));
            f.line.Add("~~ }");
        }

    }

    //shamelessy stolen:
    // https://stackoverflow.com/questions/6442123/in-c-how-do-i-convert-a-XmlElement-to-string-with-indentation-without-looping
    public static class MyExtensions
    {

        public static void MS_IS_DUMB_SetAttribute(this System.Xml.XmlElement node, string name, string value, Boolean noZeros = true)
        {
            if(value != null && value != "" && !(noZeros == false || value == "0"))
                ((XmlElement)node).SetAttribute(name, value);
        }

        public static string ToString(this System.Xml.XmlElement node, int indentation)
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new System.Xml.XmlTextWriter(sw))
                {
                    xw.Formatting = System.Xml.Formatting.Indented;
                    xw.Indentation = indentation;
                    node.WriteContentTo(xw);
                }
                return sw.ToString();
            }
        }
        
        
    }
    //I may have an anger issue...
    // MS xml api is.. annoying.  a node generated in one doc has to be imported into another document before it can be appended to a node created from that other document.
    // Long story short: if you don't care about xml standards(guessing there's no xsd for this, so that's us) then make all the nodes you wanna stitch together from the same document, use XMLElement, not XMLNode, and don't do anything fancy.
    // since we just want to use their system to read/write simple xml strings, let's wrap all this nonsense into a static class and then pretend it didn't happen.. green?  supergreen.
    public static class XMLSUCKS
    {
        private static XmlDocument doc = new XmlDocument();
        public static XmlElement MS_IS_DUMB_getNode(string name)
        {
            return doc.CreateElement(name);
        }
    }
}
