# LineCount
Cold Line Count for C/C++ which take #if xxx #endif into account.

# Usage:
LineCount.exe path [option]
	
	path : path to count. default is current directory.

	Options:
	-e --exclusive : exclude some specify directory, case insensitive.
	-s --skipMacro £ºskip codes surround by #if XXX #endif, case insensitive.
	-r --recursive : recursive count in dir, default is true.
	-v --verbos    : show verbos result.
	-f --by-file   : show result for each file (ordered by file path).
	-o --by-order	: show result for each file (ordered by count, no compatible with verbos).
	-d --debug     : debug this tool to see if it counts like what you'v expected.
	-c --cccs      : show condiction compile code statistic, -s will be ignore.

	Example:
	The following line counts code lines under current directory recursive but not under any of Andorid, ios, or mac. 
	And codes surrounded by #if WITH_EDITOR ... #endif or #if 0 ... #endif or #if WIN32 ... #endif will not be take into account.

	LineCount.exe . -e Android,IOS,MAC -s WITH_EDITOR,0,WIN32
	
	The following line shows condiction compile code statistic

	lineCount.exe . -c