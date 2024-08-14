using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace LineCount
{
    class Program
    {
        enum CountLineStat
        {
            CLS_NONE,
            CLS_COMMENT,
            CLS_MACRO,
            CLS_NEST_MACRO,
        };

        public class Options
        {
            [Value(0, HelpText = "path to count")]
            public string filePath { get; set; } = ".";

            [Option('s', "skipMacro", Required = false, HelpText ="skip code surrounded by macros like #if XXXX ... #endif")]
            public string skipMacroString { get; set; } = "";

            [Option('e', "exclusive", Required = false, HelpText ="exclusive dirs")]
            public string exclusiveDirString { get; set; } = "";

            [Option('r', "recursive", Required = false, HelpText = "recursive counting in dir")]
            public bool bRecursive { get; set; } = true;

            [Option('v', "verbos", Required = false, HelpText = "showing detail result")]
            public bool bVerbos { get; set; } = false;

            [Option('f', "by-file", Required = false, HelpText = "show result for each file (ordered by count)")]
            public bool bByFile { get; set; } = false;
            
            [Option('o', "order", Required = false, HelpText = "show result for each file (ordered by count)")]
            public bool bByOrder { get; set; } = false;

            [Option('d', "debug", Required = false, HelpText = "debug tool")]
            public bool bDebug { get; set; } = false;

            [Option('c', "cccs", Required = false, HelpText = "condiction compile code statistic")]
            public bool bCCCS { get; set; } = false;
        }
        static void DBG(string format, int lineNum, int lineCount, string line)
        { 
            if(bVerbos && bDebug)
                Console.WriteLine(format, lineNum, lineCount, line);
        }

        static bool bVerbos = false;
        static bool bByFile = false;
        static bool bByOrder = false;
        static bool bDebug = false;
        static bool bCCCS = false;

        static string[] fileExts = { ".cpp", ".h", ".c", ".hpp", ".inl"};
        static string[] exclusiveDirs = {};
        static Dictionary<string, int> cccsDict = new Dictionary<string, int>();
        static HashSet<string> skipMacros = new HashSet<string>();

        static Dictionary<string, int> fileCountDict = new Dictionary<string, int>();
        static void CountInFile(string filePath, ref int totalCommentNum, ref int totalBlankNum, ref int totalCodeNum, ref int totalMacroNum)
        {
            int commentNum = 0;
            int blankNum = 0;
            int codeNum = 0;
            int macroNum = 0;

            int lineNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    Stack<string> macroNameStack = new Stack<string>();
                    macroNameStack.Push("");
                    string curMacroName = macroNameStack.Peek();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();

                    List<int> macroCountStack = new List<int>();
                    macroCountStack.Add(0);

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;

                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            blankNum++;
                            DBG("{0:0000} BL: {1}", lineNum, blankNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            commentNum++;
                            DBG("{0:0000} CM: {1}\t{2}", lineNum, commentNum, line);
                        
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_COMMENT)
                        {
                            commentNum++;
                            if (line.Contains("*/"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} CM: {1}\t{2}", lineNum, commentNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            bool bStepIn = line.StartsWith("#if");

                            if (bCCCS && !bStepIn)
                            {
                                if (!cccsDict.ContainsKey(curMacroName))
                                {
                                    DBG("{0:0000} ERROR: {1}\t macroName:{2}", lineNum, macroNum, curMacroName);
                                }
                                cccsDict[curMacroName]++;
                            }

                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();

                                int innerMC = macroCountStack[0];
                                macroCountStack.RemoveAt(0);

                                if (bCCCS)
                                {
                                    macroNameStack.Pop();
                                    curMacroName = macroNameStack.Peek();
                                    if (curMacroName.Length != 0)
                                    {
                                        cccsDict[curMacroName] += innerMC;
                                    }
                                }
                            }
                            
                            if (!bStepIn)
                            {
                                macroNum++;
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}\t{2}", lineNum, macroNum, line);
                                continue;
                            }
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                commentNum++;
                                DBG("{0:0000} CM: {1}\t{2}", lineNum, commentNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                codeNum++;
                                DBG("{0:0000} C-: {1}\t{2}", lineNum, codeNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            commentNum++;
                            DBG("{0:0000} C-: {1}\t{2}", lineNum, commentNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            if (bCCCS)
                            {
                                macroNameStack.Push(line);
                                curMacroName = macroNameStack.Peek();

                                if (!cccsDict.ContainsKey(curMacroName))
                                    cccsDict.Add(curMacroName, 0);
                                cccsDict[curMacroName]++;

                                string[] splitor = { " ", "&&", "||" };
                                string[] macros = line.Split(splitor, StringSplitOptions.RemoveEmptyEntries);
                                for (int i = 1; i < macros.Length; i++) //skip #if
                                {
                                    skipMacros.Add(macros[i]);
                                }
                            }
                            macroCountStack.Insert(0, 1);
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                macroNum++;
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}\t{2}", lineNum, macroNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach(string macro in skipMacros)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        macroNum++;
                                        DBG("{0:0000} MC: {1}\t{2}", lineNum, macroNum, line);
                                        isskipMacro = true; 
                                        break;
                                    }
                                }
                                if (isskipMacro)
                                    continue;
                            }
                        }
                        if (line.StartsWith("#elif") || line.StartsWith("#else"))
                        {
                            //do not handle it yet.
                        }
                        codeNum++;
                        DBG("{0:0000} CO: {1}\t{2}", lineNum, codeNum, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error：{ex.Message}");
            }
            totalCommentNum += commentNum;
            totalBlankNum += blankNum;
            totalCodeNum += codeNum;
            totalMacroNum += macroNum;

            if (bByFile)
            {
                if (bVerbos)
                {
                    Console.WriteLine(" {0} \t {1} \t  {2} \t {3} \t    {4}", filePath, blankNum, commentNum, macroNum, codeNum);
                }
                else
                {
                    //Console.WriteLine("{0} {1}", filePath, codeNum);
                    fileCountDict.Add(filePath, codeNum);
                }
            }
        }

        static void CountInDirectory(string fileDir, ref int fileNum, ref int commentNum, ref int blankNum, ref int codeNum, ref int macroNum, bool bRecursive)
        {
            foreach (string exclusiveDir in exclusiveDirs)
            {
                if (exclusiveDir.Length == 0)
                    continue;

                string lowerDir = fileDir.ToLower();
                if (lowerDir.EndsWith(exclusiveDir))
                {
                    return;
                }
            }
            if (Directory.Exists(fileDir))
            {
                string[] filePaths = Directory.GetFiles(fileDir);
                foreach (string filePath in filePaths)
                {
                    string ext = Path.GetExtension(filePath);
                    if (ext is null)
                        continue;
                    ext = ext.ToLower();

                    foreach (string fileExt in fileExts)
                    {
                        if (ext == fileExt)
                        {
                            fileNum++;
                            Program.CountInFile(filePath, ref commentNum, ref blankNum, ref codeNum, ref macroNum);
                            break;
                        }
                    }
                }

                if (bRecursive)
                {
                    // Recurse sub directories
                    string[] folders = Directory.GetDirectories(fileDir);
                    foreach (string folder in folders)
                    {
                        CountInDirectory(folder, ref fileNum, ref commentNum, ref blankNum, ref codeNum, ref macroNum, bRecursive);
                    }
                }
            }
        }
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run)
                .WithNotParsed(HandleParseError);
            if (bDebug)
            {
                Console.ReadLine();
            }
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            
        }
        static void Run(Options option)
        { 
            int fileNum = 0;
            int commentNum = 0;
            int blankNum = 0;
            int codeNum = 0;
            int macroNum = 0;
            
            //string[] skipMacro = { "WITH_EDITOR", "0", "LOGTRACE_ENABLED", "WITH_EDITOR_ONLY_DATA", "UE_TRACE_ENABLED", "!UE_BUILD_SHIPPING", "VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = {"VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = { "0"};

            exclusiveDirs = option.exclusiveDirString.Split(',');
            for (int i = 0; i < exclusiveDirs.Length; i++)
            {
                exclusiveDirs[i] = exclusiveDirs[i].ToLower();
            }

            string[] arrSkipMacros = option.skipMacroString.Split(new char [] {','}, StringSplitOptions.RemoveEmptyEntries);
            foreach (string item in arrSkipMacros)
                skipMacros.Add(item);
            string fileDir = Path.GetFullPath(option.filePath);
            bool bRecursive = option.bRecursive;
            bVerbos = option.bVerbos;
            bByFile = option.bByFile;
            bByOrder = option.bByOrder;
            if (bByOrder)
            {
                bByFile = true;
            }
            bDebug = option.bDebug;
            bCCCS = option.bCCCS;

            if (bByFile && bVerbos)
            {
                Console.WriteLine("------------------file-----------------  blank  comment  skipMicro   code");
            }

            string shortName = fileDir;
            if (Directory.Exists(fileDir))
            {
                shortName = Path.GetFileName(fileDir);
                Program.CountInDirectory(fileDir, ref fileNum, ref commentNum, ref blankNum, ref codeNum, ref macroNum, bRecursive);
            }
            else if (File.Exists(fileDir))
            {
                fileNum = 1;
                shortName = Path.GetFileName(fileDir);
                Program.CountInFile(fileDir, ref commentNum, ref blankNum, ref codeNum, ref macroNum);
            }
            else
            {
                shortName = Path.GetFileName(fileDir);
            }

            if (bByFile && !bVerbos)
            {
                if (bByOrder)
                {
                    var sortedDict = from objDic in fileCountDict orderby objDic.Value ascending select objDic;
                    foreach (KeyValuePair<string, int> item in sortedDict)
                    {
                        Console.WriteLine("{0} {1}", item.Key, item.Value);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, int> item in fileCountDict)
                    {
                        Console.WriteLine("{0} {1}", item.Key, item.Value);
                    }
                }

                Console.WriteLine("---------------summary-----------------");
            }
            if (bVerbos || bByFile)
            {
                Console.WriteLine("files  blank  comment  skipMicro   code");
                Console.WriteLine(" {0} \t {1} \t  {2} \t {3} \t    {4}", fileNum, blankNum, commentNum, macroNum, codeNum);
                if (bVerbos && bByOrder)
                {
                    Console.WriteLine("\n*warning: verbos showing not compatible with by-order, ordering ignored");
                }
            }
            else
            {
                Console.WriteLine("{0} {1}", shortName, codeNum);
            }

            if (bCCCS)
            {
                var sortedDict = from objDic in cccsDict orderby objDic.Value descending select objDic;
                foreach (KeyValuePair<string, int> kv in sortedDict)
                {
                    Console.WriteLine("{0} {1}", kv.Key, kv.Value);
                }
            }
        }
    }
}
