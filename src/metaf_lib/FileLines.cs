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

using System.Collections.Generic;

namespace MetAF
{
    public class FileLines
	{
		public int L; // line number
		public int C; // column number
		public int offset; // accounts for collapsing lines (in processing) so 'files lines' remain correct in error messages
		public string path;
		public List<string> line;
		public FileLines() { this.L = this.C = this.offset = 0; this.line = new List<string>(); }
		public void GoToStart() { this.L = this.C = this.offset = 0; }
		public void Clear() { this.L = this.C = this.offset = 0; this.line.Clear(); }
	}
}
