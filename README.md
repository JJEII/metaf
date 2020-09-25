# metaf v.0.7.0.9
metaf is a powerful meta/nav editor in an alternate format from that used by the VirindiTank addon to the MMORPG game Asheron's Call. metaf provides full-featured capabilities for editing, and very straightforward bidirectional translation between .met/.nav and .af, with VirindiTank still running the end results. Requires .NET Core. Notepad++ strongly recommended.

**The necessary files to run metaf are in a zip file in the 'releases' directory. Read the text file metafREADME.af that's in there.**

### metaf strengths:
* Highly efficient and human-friendly editing capabilities for
  - .met files
  - .nav files
  - With full feature coverage for both (not just a subset of their features)
* Supports code commenting and ignoring blank lines, for readability/understandability
* Vastly increases transparency of meta's logical structure, for greatly improved clarity
* Global search/replace
* Easily copy/cut/paste/change anything anywhere
  - States, rules, pieces of them, navroutes or nodes, etc.
  - Add, remove, or change Any/All/DoAll/Not without disrupting their contents
* Move things around at will
  - Reorder code simply by moving lines around
  - Place states and navroutes in any order you want; the output's functionally the same
  - Easily change nav node sequences (embedded or not)
* Re-use the same nav in multiple places in the code
  - Define a nav once
  - Embed it in the code as many times as you want
  - Name it differently in different embed locations (if you want)
* Mathematically transform navroutes when embedding them
  - Enables one navroute to become many distinct ones in the .met output
  - Makes navs for ALL "duplicate" dungeons as easy as making one nav for one of them, then computing the seven numbers required for transforming it into each other one
* Cannot easily create new/relevant coordinates within a navroute, but trivially simple to create EmbedNav placeholders in code, ready for fill-in, exactly where they're needed
* Extensive help-files included, as well as reference-ready keyword lists in file headers

**Read metafREADME.af text file for more. It's inside a zip file in the 'releases' directory.**

### Advantages of using Notepad++ to edit metaf files:
* Powerful, fast, free, easy-to-use editor
  - Simultaneously tab-in/out multiple lines
  - Native RegEx support
  - Auto-close braces, brackets, and parentheses, if desired
  - Simultaneously comment/uncomment multiple lines
* Custom syntax highlighting (keywords, operators, comments, VTank functions, etc.)
  - XML files are provided for regular and 'dark mode' metaf syntax highlighting
  - Code-folding
* Auto-completion suggestion menus (kind of a "poor man's IntelliSense")
* The above drastically help mitigate the lack of instant feedback for invalid inputs

**Read metafREADME.af text file for more. It's inside a zip file in the 'releases' directory.**
