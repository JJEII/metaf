using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetAF.enums;
using System.Xml;
using System.Text.RegularExpressions;
using System.Globalization;

namespace MetAF
{
    public abstract class MetaElement
    {
        public MetaElement parent;
        //auto layout settings
        private bool? auto = null;
        private int? _vmargin = null;
        private int? _hmargin = null;
        private int? minWidth = null;
        private int? widthPerChar = null;
        private int? heightPerLine = null;

        //vertical margins/spacing
        public bool HasOwnVmargin { get => _vmargin != null; }
        public int Vmargin { 
            get {
                if (parent != null && _vmargin == null)
                    return parent.Vmargin;
                return (int)(_vmargin == null ? 0 : _vmargin); 
            } set { 
                _vmargin = value; 
            }
        }
        //horizontal margins/spacing
        public bool HasOwnHmargin { get => _hmargin != null; }
        public int Hmargin 
        { 
            get {
                if (parent != null && _hmargin == null)
                    return parent.Hmargin;
                return (int)(_hmargin == null ? 0 : _hmargin); 
            } set {
                _hmargin = value; 
            } 
        }
        //enable auto layout
        public bool HasOwnAuto {get => auto != null;}
        public bool Auto 
        { 
            get {
                if (parent != null && auto == null)
                    return parent.Auto;
                return auto == true ? true : false;
            } set { 
                auto = value; 
            }

        }

        public bool HasOwnMinWidth { get => minWidth != null; }
        public int MinWidth 
        { 
            get {
                if (parent != null && minWidth == null)
                    return parent.MinWidth;
                return (int)(minWidth == null ? 0 : minWidth);
            } set {
                minWidth = value;
            }
        }

        public bool HasOwnWidthPerChar { get => widthPerChar != null; }
        public int WidthPerChar
        {
            get
            {
                if (parent != null && widthPerChar == null)
                    return parent.WidthPerChar;
                return (int)(widthPerChar == null ? 0 : widthPerChar);
            }
            set
            {
                widthPerChar = value;
            }
        }

        public bool HasOwnHeightPerLine { get => heightPerLine != null; }
        public int HeightPerLine
        {
            get {
                if (parent != null && heightPerLine == null)
                    return parent.HeightPerLine;
                return (int)(heightPerLine == null ? 0 : heightPerLine); 
            }
            set { heightPerLine = value; }
        }

        public void layoutChildren(List<MetaElement> elements, out int totalWidth, out int totalHeight)
        {
            totalWidth = 0;
            totalHeight = 0;
            if (!Auto) return;

            //track position
            int currentX = Hmargin;
            int currentY = 0;//wrapping layout should handle this margin?
            //measure this line
            int lineHeight = 0;
            int lineWidth = 0;
            //measure whole thing


            foreach (var child in elements)
            {
                if (child is MetaLayout layout)
                {
                    //track total width
                    totalWidth = Math.Max(totalWidth, lineWidth + Hmargin * 2);

                    //layouts make new line, so.. I guess go down a line first?
                    totalHeight += lineHeight;// + Vmargin;
                    currentY = totalHeight;
                    currentX = Hmargin;
                    lineHeight = 0;
                    lineWidth = 0;

                    //place layout
                    layout.top = currentY;
                    layout.left = layout.Vmargin;

                    int layoutWidth = 0;
                    int layoutHeight = 0;
                    layout.layoutChildren(layout.elements, out layoutWidth, out layoutHeight);
                    layout.width = layoutWidth;
                    layout.height = layoutHeight;
                    
                    int thisLayoutHeight = layout.height + layout.Vmargin;
                    lineHeight = Math.Max(thisLayoutHeight, lineHeight);

                    totalWidth = Math.Max(layout.width, lineWidth + Hmargin * 2);
                }
                else if (child is MetaButton btn)
                {
                    bool outOfRoom = false;
                    if(this is MetaLayout lay)
                    {
                        outOfRoom = lay.width != 0 && lay.width < (lineWidth + btn.Width + btn.Hmargin);
                    } else if (this is MetaView view)
                    {
                        outOfRoom = view.width != 0 && view.width < (lineWidth + btn.Width + btn.Hmargin);
                    }
                    if(outOfRoom) //new line
                    {
                        totalHeight += lineHeight + Vmargin;
                        currentY = totalHeight;
                        currentX = Hmargin;
                        lineHeight = 0;
                        lineWidth = 0;
                    }
                    
                    //place btn at current loc
                    btn.Top = currentY + this.Vmargin;
                    btn.Left = currentX + this.Hmargin;
                    //add btn with and margin to line width and curr X
                    lineWidth += btn.Width + this.Hmargin;
                    currentX += btn.Width + this.Hmargin;

                    //increase lineHeight if needed
                    int thisBtnHeight = btn.Height + this.Vmargin;
                    lineHeight = Math.Max(thisBtnHeight, lineHeight);

                    //track total width
                    totalWidth = Math.Max(totalWidth, lineWidth + Hmargin * 2);
                }
                else
                    throw new MyException(GetType().Name.ToString() + " Auto layout failure. Unknown child type:" + child.GetType().ToString());

            }
            totalHeight += lineHeight;
            return;
        }

        public virtual void FromXml(XmlElement xml)
        {
            string vmarg = xml.Attributes["vmargin"]?.Value;
            if (vmarg != null)
                Vmargin = int.Parse(vmarg);

            string hmarg = xml.Attributes["hmargin"]?.Value;
            if (hmarg != null)
                Hmargin = int.Parse(hmarg);

            string minWid = xml.Attributes["minwidth"]?.Value;
            if (minWid != null)
                MinWidth = int.Parse(minWid);


            string widthPerChar = xml.Attributes["widthperchar"]?.Value;
            if(widthPerChar != null)
                WidthPerChar = int.Parse(widthPerChar);

            string heightPerLine = xml.Attributes["heightperline"]?.Value;
            if(heightPerLine != null)
                HeightPerLine = int.Parse(heightPerLine);

            string autoValue = xml.Attributes["auto"]?.Value;
            if (autoValue == "true")
                Auto = true;
            if (autoValue == "false")
                Auto = false;
        }

        public abstract XmlDocument ToXml();
        public void addStandardFields(XmlElement btnNode)
        {
            if(HasOwnVmargin && (parent == null || Vmargin != parent.Vmargin))
                xmlHelpers.SetAttribute(btnNode, "vmargin", Vmargin.ToString());

            if(HasOwnHmargin && (parent == null || Hmargin != parent.Hmargin))
                xmlHelpers.SetAttribute(btnNode, "hmargin", Hmargin.ToString());

            if(HasOwnMinWidth && (parent == null || MinWidth != parent.MinWidth))
                xmlHelpers.SetAttribute(btnNode, "minwidth", MinWidth.ToString());

            if(HasOwnAuto && (parent == null || Auto != parent.Auto))
                xmlHelpers.SetAttribute(btnNode, "auto", Auto.ToString());

            if(HasOwnWidthPerChar && (parent == null || widthPerChar != parent.widthPerChar))
                xmlHelpers.SetAttribute(btnNode, "widthperchar", widthPerChar.ToString());

            if(HasOwnHeightPerLine && (parent == null || heightPerLine != parent.heightPerLine))
                xmlHelpers.SetAttribute(btnNode, "heightperline", heightPerLine.ToString());
        }
    }

    public class MetaButton : MetaElement
    {
        public string name = "";
        private int top = 0;
        private int left = 0;
        private int width = 0;
        private int height = 0;
        public string text = "";
        public string setState = "";
        public string actionExp = "";

        public int Top { get => top; set => top = value; }
        public int Left { get => left; set => left = value; }
        public int Width 
        { 
            get {
                if(width==0 && Auto == true)
                {
                    int widthFromChars = text.Length * WidthPerChar;
                    int widthFromMargin = Hmargin * 2;
                    int autoWidth = widthFromChars + widthFromMargin;
                    return Math.Max(autoWidth, MinWidth);
                }
                return width;
            } set {
                width = value; 
            } 
        }
        public int Height 
        {
            get {
                if(height==0 && Auto == true)
                {
                    //no line returns in buttons apparently
                    //int heightFromLines = text.Split("\n").Length * HeightPerLine;
                    int heightFromLines = HeightPerLine;
                    int heightFromMargin = Vmargin * 2;
                    int newHeight = heightFromLines + heightFromMargin;
                    return Math.Max(newHeight, 10);
                }
                return height;
            } set {
                height = value;
            } 
        }

        public override XmlDocument ToXml()
        {
            XmlDocument doc = new();
            XmlElement btnNode = doc.CreateElement("control");

            addStandardFields(btnNode);
            btnNode.SetAttribute("type", "button");

            xmlHelpers.SetAttribute(btnNode, "name", name);
            xmlHelpers.SetAttribute(btnNode, "text", text);

            xmlHelpers.SetAttribute(btnNode, "setstate", setState);
            xmlHelpers.SetAttribute(btnNode, "actionexp", actionExp);

            xmlHelpers.SetAttribute(btnNode, "top", Top.ToString());
            xmlHelpers.SetAttribute(btnNode, "left", Left.ToString());
            xmlHelpers.SetAttribute(btnNode, "width", Width.ToString());
            xmlHelpers.SetAttribute(btnNode, "height", Height.ToString());



            doc.AppendChild(btnNode);
            return doc;
        }


        public override void FromXml(XmlElement xml)
        {
            base.FromXml(xml);
            name = xml.Attributes["name"]?.Value;
            Left = xmlHelpers.readIntProp(xml, "left");
            Top = xmlHelpers.readIntProp(xml, "top");
            Width = xmlHelpers.readIntProp(xml, "width");
            Height = xmlHelpers.readIntProp(xml, "height");
            text = xml.Attributes["text"]?.Value;
            
            setState = xml.Attributes["setstate"]?.Value;
            actionExp = xml.Attributes["actionexp"]?.Value;


        }

    }
    public class MetaLayout : MetaElement // line# for msgs good
    {
        public readonly List<MetaElement> elements = new();

        public string name = "";//not in met's xml(hopefully I can add it without breaking stuff)
        public int top = 0;
        public int left = 0;
        public int width = 0;
        public int height = 0;




        public override void FromXml(XmlElement xml)
        {
            base.FromXml(xml);
            name = xml.Attributes["name"]?.Value;
            left = xmlHelpers.readIntProp(xml, "left");
            top = xmlHelpers.readIntProp(xml, "top");
            width = xmlHelpers.readIntProp(xml, "width");
            height = xmlHelpers.readIntProp(xml, "height");


            foreach (XmlElement child in xml.ChildNodes)
            {

                if (child.Attributes["type"].Value.Equals("layout", StringComparison.OrdinalIgnoreCase) || child.Name == "layout")
                {
                    var layout = new MetaLayout();
                    layout.parent = this;
                    layout.FromXml(child);
                    elements.Add(layout);
                }
                else if (child.Attributes["type"].Value.Equals("button", StringComparison.OrdinalIgnoreCase) || child.Name == "layout")
                {
                    var button = new MetaButton();
                    button.parent = this;
                    button.FromXml(child);
                    elements.Add(button);
                }
                else
                    throw new MyException(GetType().Name.ToString() + ".ImportFromMetAF: Invalid XML node " + child.Name);
            }
        }

        public override XmlDocument ToXml()
        {
            if(Auto)
            {
                int layoutWidth;
                int layoutHeight;
                this.layoutChildren(elements, out layoutWidth, out layoutHeight);
                width = layoutWidth;
                height = layoutHeight;
            }
            
            XmlDocument xmlDoc = new();
            XmlElement layout = xmlDoc.CreateElement("control");
            addStandardFields(layout);
            xmlHelpers.SetAttribute(layout, "type", "layout");

            xmlHelpers.SetAttribute(layout, "name", name);
            xmlHelpers.SetAttribute(layout, "top", top.ToString());
            xmlHelpers.SetAttribute(layout, "left", left.ToString());
            xmlHelpers.SetAttribute(layout, "width", width.ToString());
            xmlHelpers.SetAttribute(layout, "height", height.ToString());


            foreach (var child in elements)
            {
                if (child is MetaLayout)
                {
                    var metaLayout = (MetaLayout)child;
                    var node = metaLayout.ToXml();
                    //layout.OwnerDocument.ImportNode(node, true);
                    var importNode = xmlDoc.ImportNode((XmlNode)node.DocumentElement, true);
                    layout.AppendChild(importNode);
                }
                if (child is MetaButton)
                {
                    var metaButton = (MetaButton)child;
                    var node = metaButton.ToXml();
                    //layout.OwnerDocument.ImportNode(node, true);
                    var importNode = xmlDoc.ImportNode((XmlNode)node.DocumentElement, true);
                    layout.AppendChild(importNode);
                }
            }

            xmlDoc.AppendChild(layout);
            return xmlDoc;
        }
    }
    public class MetaView : MetaElement // line# for msgs good
    {
        private readonly Meta _myMeta;

        public readonly List<MetaElement> elements = new();
        public string viewId;
        public string title = "";
        public int width = 0;
        public int height = 0;
        public int my_metAFftagline; // "VIEW:" line of this view in a metAF file



        public MetaView(Meta m) : base() {
            _myMeta = m;
            elements = new();
            my_metAFftagline = -1;

            WidthPerChar = 6;
            HeightPerLine = 12;
        }



        public void FromXml(string xml)
        {
            XmlDocument xmlDoc = new();
            if(!xml.StartsWith("<?xml"))
            {
                xml = "<?xml version=\"1.0\"?>" + xml;
            }
            xmlDoc.LoadXml(xml);

            XmlElement viewNode = (XmlElement)xmlDoc.GetElementsByTagName("view")[0];
            
            
            FromXml(viewNode);
        }
        public override void FromXml(XmlElement xml)
        {
            base.FromXml(xml);
            width = xmlHelpers.readIntProp(xml, "width");
            height = xmlHelpers.readIntProp(xml, "height");
            title = xml.Attributes["title"]?.Value;

            if (viewId == null || viewId.Length == 0)
            { // no id, make one
                viewId = _myMeta.GenerateUniqueViewTag(title);
            }

            foreach (XmlElement child in xml.ChildNodes)
            {

                if (child.Attributes["type"].Value.Equals("layout", StringComparison.OrdinalIgnoreCase) || child.Name == "layout")
                {
                    var layout = new MetaLayout();
                    layout.parent = this;
                    layout.FromXml(child);
                    elements.Add(layout);
                }
                else if (child.Attributes["type"].Value.Equals("button", StringComparison.OrdinalIgnoreCase) || child.Name == "button")
                {
                    var button = new MetaButton();
                    button.parent = this;
                    button.FromXml(child);
                    elements.Add(button);
                }
                else
                    throw new MyException(GetType().Name.ToString() + ".ImportFromMetAF: Invalid XML node " + child.Name);
            }

            _myMeta.AddView(viewId, this);
        }

        public string toMetXml()
        {
            XmlDocument xmlOut = ToXml();
            return xmlHelpers.ToMetString(xmlOut);
        }
        public override XmlDocument ToXml()
        {

            if (Auto)
            {
                int layoutWidth;
                int layoutHeight;
                this.layoutChildren(elements, out layoutWidth, out layoutHeight);
                width = layoutWidth;
                height = layoutHeight;
            }

            XmlDocument xmlDoc = new();
            XmlElement viewNode = xmlDoc.CreateElement("view");
            addStandardFields(viewNode);
            //var t = XmlElement.
            xmlHelpers.SetAttribute(viewNode, "width", width.ToString());
            xmlHelpers.SetAttribute(viewNode, "height", height.ToString());
            xmlHelpers.SetAttribute(viewNode, "title", title);

            foreach (var child in elements)
            {
                if (child is MetaLayout metaLayout)
                {
                    var node = metaLayout.ToXml();
                    var importedNode = xmlDoc.ImportNode(node.DocumentElement, true);
                    viewNode.AppendChild(importedNode);
                }
                if (child is MetaButton metaButton)
                {
                    var node = metaButton.ToXml();
                    var importedNode = xmlDoc.ImportNode(node.DocumentElement, true);
                    viewNode.AppendChild(importedNode);
                }
            }

            xmlDoc.AppendChild(viewNode);
            return xmlDoc;
        }



        public void readFromMetAF(ref FileLines f) // line# for msgs good
        {
            // read VIEW: info (id and title)
            my_metAFftagline = f.L; // remember this line (for error-reporting, if needed)
            //int len = Math.Max(f.line[f.L].Length - 1, 0);
            string baseLine = f.line[f.L++];
            string lineViewRemoved= baseLine.Substring(Math.Min(5, baseLine.Length)); //remove VIEW:
            string thisLN = rx.R__2EOL.Replace(lineViewRemoved, "");//remove comments?
            Match match;
            var reg = rx.getParms["VIEW:"];
            match = reg.Match(thisLN); 
            if (!match.Success)
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + rx.getInfo["VIEW:"]);

            //try
            {
                viewId = match.Groups["viewId"].Value;
                title = match.Groups["viewTitle"].Value;
                //StringBuilder viewXML = new StringBuilder();
                string xmlOut = "";
                
                while (f.L < f.line.Count && !(match = rx.getLeadIn["StateIfDoNav"].Match(f.line[f.L])).Success) // don't advance line
                {
                    //seems like it's still xml? not that I'm checking.. parser will handle that.

                    //add to string builder and eat the line
                    thisLN = rx.R__2EOL.Replace(f.line[f.L].Substring(Math.Min(f.C, f.line[f.L++].Length)), "");//remove comments?
                    //viewXML.Append(thisLN.Replace("\\\"", "\""));
                    string excapedQuote = "\\\"";
                    var test = thisLN.Contains(excapedQuote);
                    var cleanLN = thisLN.Replace(excapedQuote, "");
                    xmlOut += cleanLN;
                }
                //done eating xml from file, 
                var metaView = new MetaView(_myMeta);
                metaView.viewId = viewId;
                string metaXml = xmlOut;// viewXML.ToString();
                metaView.FromXml(metaXml);
            }
            /*catch (Exception e)
            {
                throw new MyException("[LINE " + (f.L + f.offset).ToString() + "] " + GetType().Name.ToString() + ".ImportFromMetAF: " + e.Message);
            }*/

        }
        public void WriteToMetAF(ref FileLines f)
        {

            XmlDocument viewNode = ToXml();

            f.line.Add("VIEW: " + viewId + " ~~{");
            f.line.Add(xmlHelpers.ToString(viewNode.DocumentElement,1));
            f.line.Add("~~ }");
        }


    }




    public static class xmlHelpers
    {
        public static string ToString(XmlElement node, int indentation)
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new System.Xml.XmlTextWriter(sw))
                {
                    xw.Formatting = System.Xml.Formatting.Indented;
                    xw.Indentation = indentation;
                    node.WriteTo(xw);
                }
                return sw.ToString();
            }
        }
        public static string ToMetString(XmlDocument node)
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new System.Xml.XmlTextWriter(sw))
                {
                    xw.Formatting = System.Xml.Formatting.None;
                    node.WriteContentTo(xw);
                }
                return sw.ToString();
            }
        }
        public static void SetAttribute(XmlElement node, string name, string value, Boolean noZeros = true)
        {
            if (value != null && value != "" && !(noZeros == false || value == "0"))
                node.SetAttribute(name, value);
        }

        public static int readIntProp(XmlElement node, string name)
        {
            if (node.Attributes[name]?.Value != null)
                return int.Parse(node.Attributes[name]?.Value);
            else
                return 0;
        }
    }
}
