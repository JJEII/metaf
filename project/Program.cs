// metaf is a powerful meta/nav editor in an alternate format from that used by the VirindiTank addon to the game Asheron's Call.
// Copyright (C) 2020  J. Edwards
//
// Author: J. Edwards, aka Eskarina of Morningthaw/Coldeve
// See project for license and info: https://github.com/jjeii/metaf

using System;
using System.Globalization;	// Needed to force .NET to behave properly in
using System.Threading;     // other countries, with decimal numbers.

//#define _DBG_

namespace Metaf
{
#if (_DBG_)
	class myDebug {
		public static string[] args = { "__AutoCrafter_IB_corrupted.met" }; //{ "eskontrol.af" };
	}
#endif

    class Metaf
    {
        private static string GetOutputFileName(string inFileName, string outExt)
        {
            System.IO.FileInfo fInfo = new System.IO.FileInfo(inFileName);
            string baseFileName = fInfo.Name[..^fInfo.Extension.Length];
            string cutName = new System.Text.RegularExpressions.Regex(@"~[0-9]*$").Replace(baseFileName, "");
            int i = 0;
            while (System.IO.File.Exists(cutName + "~" + i.ToString() + outExt))
                i++;
            return cutName + "~" + i.ToString() + outExt;
        }

        static void Main(string[] args)
        {
#if (_DBG_)
			args = myDebug.args;
#endif
            if (args.Length > 0)
            {
                if (args[0].CompareTo("-version") == 0)
                {
                    Console.WriteLine("\n" + CmdLnParms.version);
                    Environment.Exit(0);
                }
                if (args[0].CompareTo("-new") == 0)
                {
                    System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.newFileName);
                    fOut.WriteLine(OutputText.metaHeader);
                    fOut.Close();
                    Console.Write("\n\tOutput file: " + CmdLnParms.newFileName + "\n");
                    Environment.Exit(0);
                }
                if (args[0].CompareTo("-newnav") == 0)
                {
                    System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.newnavFileName);
                    fOut.WriteLine(OutputText.navHeader);
                    fOut.Close();
                    Console.Write("\n\tOutput file: " + CmdLnParms.newnavFileName + "\n");
                    Environment.Exit(0);
                }
                if (args[0].CompareTo("-help") == 0)
                {
                    System.IO.StreamWriter fOut = new System.IO.StreamWriter(CmdLnParms.readmeFileName);
                    fOut.WriteLine(OutputText.readme);
                    fOut.Close();
                    Console.Write("\n\tOutput file: " + CmdLnParms.readmeFileName + "\n");

                    fOut = new System.IO.StreamWriter(CmdLnParms.refFileName);
                    fOut.WriteLine(OutputText.reference);
                    fOut.Close();
                    Console.Write("\n\tOutput file: " + CmdLnParms.refFileName + "\n");

                    Environment.Exit(0);
                }

                string inFileName = args[0];

                // Check if input file exists; if not, exit immediately ... can't continue
                if (!System.IO.File.Exists(inFileName))
                {
                    Console.WriteLine("{0} does not exist.", inFileName);
                    Environment.Exit(0);
                }

                FileLines f = new FileLines();
                string tmpLine;
                int i = 0;
                bool isMet = true;
                bool isNav = true;

                // Kinda kludgey, but needed for some other countries' handling of doubles (periods vs commas), and easier than editing every
                // to/from string of a double, throughout the code (Parse and Format and CultureInfo.InvariantCulture...)
                // For what was happening, exactly: https://ayulin.net/blog/2015/the-invariant-culture/
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; // CultureInfo.CreateSpecificCulture("en-US");

                // Read in the file
                string[] metIntro = { "1", "CondAct", "5", "CType", "AType", "CData", "AData", "State", "n", "n", "n", "n", "n" };
                System.IO.StreamReader file = new System.IO.StreamReader(inFileName);
                while ((tmpLine = file.ReadLine()) != null)
                {
                    f.line.Add(tmpLine);
                    if (i < metIntro.Length) // Test if it's a .met file
                        if (metIntro[i].CompareTo(f.line[i]) != 0)
                            isMet = false;
                    i++;
                }
                file.Close();

                f.path = (new System.IO.FileInfo(inFileName)).DirectoryName;
                Console.WriteLine(f.path);

                if (f.line.Count == 0)
                    throw new MyException("Empty file!");

                if (f.line[0].CompareTo("uTank2 NAV 1.2") != 0)
                    isNav = false;
#if (!_DBG_)
                try
                {
#endif
                    string outFileName;
                    string ext;
                    Meta m = new Meta(isNav);
                    f.GoToStart();
                    if (isMet || isNav)
                    {
                        m.ImportFromMet(ref f); // Set the line to the start of the lines array, and "import" the data
                        m.ExportToMetAF(ref f); // Clear the lines array and "export" the data back into it
                        ext = ".af";
                    }
                    else
                    {
                        m.ImportFromMetAF(ref f); // Set the line to the start of the lines array, and "import" the data
                        m.ExportToMet(ref f); // Clear the lines array and "export" the data back into it
                        if (m.IsNavOnly)
                            ext = ".nav";
                        else
                            ext = ".met";
                    }

                    // Set the output file name
                    isNav = m.IsNavOnly;
                    if (args.Length > 1)
                    {
                        // If neither file nor directory exist, ensure directory one up from that specified does exist; outFileName will end up "correct" below
                        if (!System.IO.File.Exists(args[1]) && !System.IO.Directory.Exists(args[1]))
                        {
                            int li = args[1].LastIndexOf('\\');
                            if (li > 0 || (li > 1 && args[1][..2].CompareTo(@"\\") != 0))
                            {
                                System.IO.Directory.CreateDirectory(args[1][..li]);
                            }
                        }

                        // If directory exists, create outFileName for file to place inside it
                        if (System.IO.Directory.Exists(args[1]))
                        {	// args[1] is a directory. Construct output file name to [over]write inside it.
                            System.IO.FileInfo fInfo = new System.IO.FileInfo(inFileName);
                            string baseFileName = fInfo.Name[..^fInfo.Extension.Length];
                            outFileName = System.IO.Path.Join(args[1], baseFileName + ext);
                        }
                        else //if (System.IO.File.Exists(args[1]))
                        {   // args[1] is a file (or doesn't exist at all). [Over]Write it.
                            outFileName = args[1];
                        } // else path doesn't exist at all
                    }
                    else
                        outFileName = GetOutputFileName(inFileName, ext);

                    // Output the results
                    System.IO.StreamWriter fileOut = new System.IO.StreamWriter(outFileName);
                    foreach (string ln in f.line)
                        fileOut.WriteLine(ln);
                    fileOut.Close();
                    Console.Write("\n\tOutput file: " + outFileName + "\n");
#if (!_DBG_)
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "\nPress ENTER.");
                    System.Console.ReadLine();
                }
#endif
            }
            else // no command-line arguments
                Console.WriteLine("\n\t       USAGE: metaf InputFileName [OutputFileName|OutputDirectoryName]\n\n\t        Help: metaf -help\n\t    New file: metaf -new\n\tNew nav file: metaf -newnav\n\t     Version: metaf -version");
        }
    }
}