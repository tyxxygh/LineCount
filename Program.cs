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

            [Option('f', "by-file", Required = false, HelpText = "showing detail result")]
            public bool bByFile { get; set; } = false;

            [Option('d', "debug", Required = false, HelpText = "debug tool")]
            public bool bDebug { get; set; } = false;
        }
        static void DBG(string format, int lineNum, string line)
        { 
            if(bVerbos && bDebug)
                Console.WriteLine(format, lineNum, line);
        }

        static bool bVerbos = false;
        static bool bByFile = false;
        static bool bDebug = false;

        static string[] fileExts = { ".cpp", ".h", ".c", ".hpp", ".inl"};
        static string[] exclusiveDirs = {};
        static void CountInFile(string filePath, ref int totalCommentNum, ref int totalBlankNum, ref int totalCodeNum, ref int totalMacroNum, ref string[] skipMacro)
        {
            int commentNum = 0;
            int blankNum = 0;
            int codeNum = 0;
            int macroNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            blankNum++;
                            DBG("BL: {0}", blankNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            commentNum++;
                            DBG("CM: {0}\t{1}", commentNum, line);
                        
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
                            DBG("CM: {0}\t{1}", commentNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            macroNum++;
                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("MC: {0}\t{1}", macroNum, line);
                            continue;
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                commentNum++;
                                DBG("CM: {0}\t{1}", commentNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                codeNum++;
                                DBG("C-: {0}\t{1}", codeNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            commentNum++;
                            DBG("C-: {0}\t{1}", commentNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                macroNum++;
                                DBG("MC: {0}\t{1}", macroNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach(string macro in skipMacro)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        macroNum++;
                                        DBG("MC: {0}\t{1}", macroNum, line);
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
                        DBG("CO: {0}\t{1}", codeNum, line);
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
                    Console.WriteLine("{0} {1}", filePath, codeNum);
                }
            }
        }

        static void CountInDirectory(string fileDir, ref int fileNum, ref int commentNum, ref int blankNum, ref int codeNum, ref int macroNum, ref string[] skipMacros, bool bRecursive)
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
                            Program.CountInFile(filePath, ref commentNum, ref blankNum, ref codeNum, ref macroNum, ref skipMacros);
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
                        CountInDirectory(folder, ref fileNum, ref commentNum, ref blankNum, ref codeNum, ref macroNum, ref skipMacros, bRecursive);
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

            string[] skipMacros = option.skipMacroString.Split(',');
            string fileDir = Path.GetFullPath(option.filePath);
            bool bRecursive = option.bRecursive;
            bVerbos = option.bVerbos;
            bByFile = option.bByFile;
            bDebug = option.bDebug;

            if (bByFile && bVerbos)
            {
                Console.WriteLine("------------------file-----------------  blank  comment  skipMicro   code");
            }

            string shortName = fileDir;
            if (Directory.Exists(fileDir))
            {
                shortName = Path.GetFileName(fileDir);
                Program.CountInDirectory(fileDir, ref fileNum, ref commentNum, ref blankNum, ref codeNum, ref macroNum, ref skipMacros, bRecursive);
            }
            else if (File.Exists(fileDir))
            {
                fileNum = 1;
                shortName = Path.GetFileName(fileDir);
                Program.CountInFile(fileDir, ref commentNum, ref blankNum, ref codeNum, ref macroNum, ref skipMacros);
            }
            else
            {
                shortName = Path.GetFileName(fileDir);
            }

            if (bByFile)
            {
                Console.WriteLine("---------------summary-----------------");
            }
            if (bVerbos || bByFile)
            {
                Console.WriteLine("files  blank  comment  skipMicro   code");
                Console.WriteLine(" {0} \t {1} \t  {2} \t {3} \t    {4}", fileNum, blankNum, commentNum, macroNum, codeNum);
            }
            else
            {
                Console.WriteLine("{0} {1}", shortName, codeNum);
            }
        }
    }
}
