// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license: https://github.com/jjeii/metaf

using System.Collections.Generic;

namespace Metaf
{
    public class FileLines
    {
        public int L; // line number
        public int C; // column number
        public int offset; // accounts for collapsing lines (in processing) so 'file lines' remain correct in error messages
        public string path;
        public List<string> line;
        public FileLines()
        {
            L = C = offset = 0;
            line = new List<string>();
        }
        public void GoToStart() { L = C = offset = 0; }
        public void Clear()
        {
            L = C = offset = 0;
            line.Clear();
        }
    }
}