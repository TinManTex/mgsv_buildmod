using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Configuration;
using System.Collections.Specialized;
using System.Xml.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace mgsv_buildmod {
    class Program {
        class BuildModSettings {
            public string projectPath = @"D:\Projects\MGS\InfiniteHeaven\tpp";

            public string luaPackFilesPath = @"D:\Projects\MGS\!InfiniteHeaven\tpp\fpkd-combined-lua";

            //tex folders have various tools run on them (see buildFox2s etc settings)
            //then are copied outright to makebitepath
            public List<string> modPackPaths = new List<string> {
                // @"D:\Projects\MGS\InfiniteHeaven\tpp\modfpk",
                // @"D:\Projects\MGS\InfiniteHeaven\tpp\modfpk-test",
            };

            public string otherMgsvsPath = @"D:\Projects\MGS\!InfiniteHeaven\tpp\othermods";//tex: folder of other mgsv files to install when release: false, installOtherMods: true

            public string docsPath = @"D:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\docs";

            public string internalLuaPath = @"D:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\Assets";
            public string externalLuaPath = @"D:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir";
            public string modulesLuaPath = @"D:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\modules";

            public string buildFolder = @"D:\Projects\MGS\build\infiniteheaven"; //tex: where the various files are actually pulled together before being makebitten      
            public string makebiteBuildPath = @"D:\Projects\MGS\build\infiniteheaven\makebite";

            public string gamePath = @"D:\Games\Steam\SteamApps\common\MGS_TPP";

            public string ihExtPath = @"D:\GitHub\IHExt\IHExt\bin\Release\IHExt.exe";
            public bool copyIHExt = false;


            // TODO: just point to sperate file
            public string modVersionDefault = "rXXX";
            public string modFileName = "Infinite Heaven";
            public string readMeName = "Infinite Heaven Readme.txt";

            public bool cleanDat = true;

            public bool buildLng2s = true;
            public bool buildSubps = false;
            public bool buildFox2s = true;
            public bool buildLbas = true;
            public bool copyModPackFolders = true;
            public bool copyInternalLua = false;//tex copies core external lua to internal (ih will still try to load external by default, so do not include in gamedir-mod\release)
            public bool copyExternalLua = false;//tex copies external to makeBite/GameDir (WARNING: will overwrite MGS_TPP\mod if installMod true, so only should be for release).
            public bool copyModulesToInternal = false;//copies external lua modules to internal

            public bool makeMod = true;
            public bool installMod = true;
            public bool installOtherMods = true;

            public bool release = false;//DEBUGNOW dont forget to also set copyExternalLua true


            public bool waitEnd = true;
        }//BuildModSettings

        class ToolPathSettings
        {
            public static string gzsToolPath = @"C:\Projects\MGS\MGSVTOOLS\GzsTool\GzsTool.exe";
            public static string langToolPath = @"C:\Projects\MGS\MGSVTOOLS\FoxEngine.TranslationTool.v0.2.4\LangTool.exe";
            public static string foxToolPath = @"C:\Projects\MGS\MGSVTOOLS\FoxTool\FoxTool.exe";
            public static string subpToolPath = @"C:\Projects\MGS\MGSVTOOLS\FoxEngine.TranslationTool.v0.2.4\SubpTool.exe";
            public static string lbaToolPath = @"D:\GitHub\LbaTool\LbaTool\bin\Debug\LbaTool.exe";
            public static string makeBitePath = @"D:\GitHub\SnakeBite\makebite\bin\Debug\makebite.exe";
            public static string snakeBitePath = @"D:\GitHub\SnakeBite\snakebite\bin\Debug\snakebite.exe";
        }//ToolPathSettings

        public class BuildFileInfo {
            public string fullPath = "";
            public bool doBuild = false;
            // public string filePath = "";
            public string packPath = "";
        }

        public static string UnfungePath(string path) {
            String unfucked = new Uri(path).LocalPath;
            return unfucked;
        }

        public delegate void ProcessFileDelegate(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);
        static string titlePrefix = "mgsv_buildmod - ";
        static void Main(string[] args) {

            Console.Title = titlePrefix;
            var cc = new ConsoleCopy("mgsv_buildmod_log.txt");

            if (args.Length == 0) { 
                Console.WriteLine("Usage: mgsv_buildmod <config path>.json");
                WriteDefaultConfigJson();
                return;
            }//args 0

            ConsoleTitleAndWriteLine("Read Config");
            string configPath = GetPath(args[0]);
            if (configPath == null) {
                Console.Write("ERROR: could not find config path");
                return;
            }

            string jsonString = File.ReadAllText(configPath);
            BuildModSettings bs = JsonConvert.DeserializeObject<BuildModSettings>(jsonString);
            if (!Directory.Exists(bs.gamePath)) {
                Console.WriteLine($"ERROR: BuildModSettings: Could not find gamePath {bs.gamePath}");
                return;
            }

            if (bs.release) {
                Console.WriteLine("doing release build");
            }


            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);

            BuildIHHookRelease(bs.makeMod);

            ConsoleTitleAndWriteLine("deleting existing makebite build folder");
            DeleteAndWait(bs.makebiteBuildPath);//tex GOTCHA will complain if open in explorer


            ConsoleTitleAndWriteLine("Copy docs and exes");
            if (Directory.Exists(bs.docsPath)) {
                Console.Title = titlePrefix + "Copy docs";
                Console.WriteLine("copying docs files to build folder");
                string docsDestinationPath = bs.buildFolder + @"\docs";
                if (Directory.Exists(docsDestinationPath)) {
                    DeleteAndWait(docsDestinationPath);
                }
                if (!Directory.Exists(docsDestinationPath)) {
                    Directory.CreateDirectory(docsDestinationPath);
                }
                CopyFilesRecursively(new DirectoryInfo(bs.docsPath), new DirectoryInfo(docsDestinationPath), "", "");
            }

            if (bs.release) {
                if (File.Exists(bs.ihExtPath) && bs.copyIHExt) {
                    Console.WriteLine("copying IHExt");
                    string destPath = bs.makebiteBuildPath + @"\GameDir\mod\";
                    if (!Directory.Exists(destPath)) {
                        Directory.CreateDirectory(destPath);
                    }
                    File.Copy(bs.ihExtPath, destPath + "IHExt.exe");
                }
            }


            string modVersion = bs.modVersionDefault;

            string readmePathFull = bs.docsPath + "\\" + bs.readMeName;//DEBUGNOW

            modVersion = GetModVersion(modVersion, readmePathFull);
            Console.WriteLine("got modVersion:{0}", modVersion);

            ConsoleTitleAndWriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            //tex TODO restrict to Data1Lua,FpkCombineLua
            TraverseTree(bs.luaPackFilesPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);
            //tex allow text files as subsituted
            //TraverseTree(luaPath, ".txt", ReadLuaBuildInfoProcess, ref modFilesInfo);
            if (modFilesInfo.Count == 0) {
                Console.WriteLine("no mod files found");
                return;
            }

            /*
            Console.WriteLine();
            Console.WriteLine("building list of fpks used");
            Dictionary<string, List<BuildFileInfo>> fpks = new Dictionary<string, List<BuildFileInfo>>();
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values)
            {
                if (buildFileInfo.doBuild)
                {
                    if (IsForFpk(buildFileInfo))
                    {
                        List<BuildFileInfo> filesForFpk;
                        if (!fpks.TryGetValue(buildFileInfo.packPath, out filesForFpk))
                        {
                            filesForFpk = new List<BuildFileInfo>();
                            fpks.Add(buildFileInfo.packPath, filesForFpk);
                            Console.WriteLine("fpklistadd:" + buildFileInfo.packPath);
                        }

                        filesForFpk.Add(buildFileInfo);
                    }
                }
            }
            */

            Console.WriteLine();

            ConsoleTitleAndWriteLine("copying mod files to build folder");
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    string luaFileDestination = "";// = bs.makebiteBuildPath + buildFileInfo.filePath + "\\";
                    if (IsForFpk(buildFileInfo)) {
                        string packPath = buildFileInfo.packPath.Replace(".", "_");
                        string internalPath = buildFileInfo.fullPath.Substring(bs.luaPackFilesPath.Length);
                        luaFileDestination = bs.makebiteBuildPath + packPath + internalPath;
                    }
                    Console.WriteLine(luaFileDestination);

                    //tex GOTCHA most common crash with ioexception will be due to some file in projectpath
                    //having DOBUILD (example in externallua or mockfox)
                    //I should just restrict buildfileinfo to data1,
                    string dir = Path.GetDirectoryName(luaFileDestination);
                    if (!Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(buildFileInfo.fullPath, luaFileDestination, true);
                }
            }

            if (bs.buildLng2s)
            {
                modFilesInfo = BuildLng2s(bs, modFilesInfo);
            }

            if (bs.buildFox2s) {
                ConsoleTitleAndWriteLine("building fox2s");
                foreach (string path in bs.modPackPaths) {
                    if (Directory.Exists(path)) {
                        TraverseTree(path, ".xml", RunFoxToolProcess, ref modFilesInfo);
                    }
                }
            }

            bs.buildSubps = false;//DEBUGNOW 
            if (bs.buildSubps) {
                ConsoleTitleAndWriteLine("building subps");
                foreach (string path in bs.modPackPaths) {
                    if (Directory.Exists(path)) {
                        TraverseTree(path, ".xml", RunSubpToolProcess, ref modFilesInfo);
                    }
                }
            }

            if (bs.buildLbas) {
                ConsoleTitleAndWriteLine("building lbas");
                foreach (string path in bs.modPackPaths)
                {
                    if (Directory.Exists(path))
                    {
                        TraverseTree(path, ".xml", RunLbaToolProcess, ref modFilesInfo);
                    }
                }
            }

            if (bs.copyModPackFolders) {
                Console.WriteLine();
                ConsoleTitleAndWriteLine("copying modPackPaths folders");
                foreach (string path in bs.modPackPaths) {
                    if (Directory.Exists(path)) {
                        CopyFilesRecursively(new DirectoryInfo(path), new DirectoryInfo(bs.makebiteBuildPath), "", ".xml");
                    }
                }
            }

            if (bs.copyExternalLua) {
                Console.WriteLine();

                ConsoleTitleAndWriteLine("copying external folder to build");

                if (Directory.Exists(bs.externalLuaPath)) {
                    string destPath = bs.makebiteBuildPath + @"\GameDir\mod\";
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(bs.externalLuaPath), new DirectoryInfo(destPath), "", "");
                }
            }

            if (bs.copyInternalLua)
            {
                Console.WriteLine();

                ConsoleTitleAndWriteLine("copying core external folder to internal");

                if (Directory.Exists(bs.internalLuaPath))
                {
                    string destPath = bs.makebiteBuildPath + @"\Assets";
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(bs.internalLuaPath), new DirectoryInfo(destPath), "", "");      
                    //DEBUGNOW also don't like this, modules will have been blindly copied via copyExternalLua, so kill them
                    string buildExternalAssetsPath = bs.makebiteBuildPath + @"\GameDir\mod\Assets\";
                    if (Directory.Exists(buildExternalAssetsPath))
                    {
                        DeleteAndWait(buildExternalAssetsPath);
                    }
                }
            }

            if (bs.copyModulesToInternal)
            {
                Console.WriteLine();

                ConsoleTitleAndWriteLine("copying external modules folder to internal");

                if (Directory.Exists(bs.modulesLuaPath))
                {
                    string destPath = bs.makebiteBuildPath + @"\Assets\tpp\script\ih";//DEBUGNOW dont like this
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(bs.modulesLuaPath), new DirectoryInfo(destPath), "", "");
                    //DEBUGNOW also don't like this, modules will have been blindly copied via copyExternalLua, so kill them
                    string modulesPath = bs.makebiteBuildPath + @"\GameDir\mod\modules";
                    if (Directory.Exists(modulesPath))
                    {
                        DeleteAndWait(modulesPath);
                        Directory.CreateDirectory(modulesPath);
                        File.CreateText(modulesPath+"/ih_files.txt").Close();
                    }
                }
            }

            if (bs.release) {
                ConsoleTitleAndWriteLine("copy docs");//DEBUGNOW then what about copy docs above?
                //tex in case I forget to update docs in gamedir-release
                string docsDestinationPath = bs.makebiteBuildPath + @"\GameDir\mod\docs";
                if (Directory.Exists(docsDestinationPath)) {
                    DeleteAndWait(docsDestinationPath);
                }
                if (!Directory.Exists(docsDestinationPath)) {
                    Directory.CreateDirectory(docsDestinationPath);
                }
                CopyFilesRecursively(new DirectoryInfo(bs.docsPath), new DirectoryInfo(docsDestinationPath), "", "");
            }


            string snakeBiteMgvsDestFilePath = bs.buildFolder + "\\" + bs.modFileName + ".mgsv";
            string snakeBiteMgvsFilePath = bs.makebiteBuildPath + "\\" + "mod.mgsv";
            //tex for if I change makebite from building inputfoldername\\mod.mgsv to build inputpathparent\inputfoldername.mgsv
            //string snakeBiteMgvsDestFilePath = $"{bs.buildFolder}\\{bs.modFileName}.mgsv";
            //string snakeBiteMgvsFilePath = $"{Directory.GetParent(bs.makebiteBuildPath)}\\makebite.mgsv";

            snakeBiteMgvsFilePath = UnfungePath(snakeBiteMgvsFilePath);
            string snakeBiteMetaDataFilePath = bs.buildFolder + "\\" + "metadata.xml";
            string snakeBiteMetaDataDestFilePath = bs.makebiteBuildPath + "\\" + "metadata.xml";
            string snakeBiteReadMeFilePath = bs.docsPath + "\\" + bs.readMeName;
            snakeBiteReadMeFilePath = UnfungePath(snakeBiteReadMeFilePath);
            string snakeBiteReadMeDestFilePath = bs.makebiteBuildPath + "\\" + "readme.txt";
            snakeBiteReadMeDestFilePath = UnfungePath(snakeBiteReadMeDestFilePath);



            ConsoleTitleAndWriteLine("Updating metadata version tag");
            if (File.Exists(snakeBiteMetaDataFilePath)) {
                XDocument xmlFile = XDocument.Load(snakeBiteMetaDataFilePath);

                var query = from c in xmlFile.Elements("ModEntry")
                            select c;

                foreach (XElement entry in query) {
                    entry.Attribute("Version").Value = modVersion;
                }

                xmlFile.Save(snakeBiteMetaDataFilePath);
            }

            ConsoleTitleAndWriteLine("Copying mod readme");
            if (File.Exists(snakeBiteReadMeFilePath)) {
                File.Copy(snakeBiteReadMeFilePath, snakeBiteReadMeDestFilePath, true);
            }

            ConsoleTitleAndWriteLine("Copying mod metadata");
            if (File.Exists(snakeBiteMetaDataFilePath)) {
                File.Copy(snakeBiteMetaDataFilePath, snakeBiteMetaDataDestFilePath, true);
            }

            if (bs.release == false && bs.cleanDat) {
                ConsoleTitleAndWriteLine("cleandat");
                Console.WriteLine("Deleting sbmods.xml");
                string sbmodFilePath = bs.gamePath + "\\snakebite.xml";
                string sbmodCleanFilePath = bs.gamePath + "\\snakebiteclean.xml";
                if (File.Exists(sbmodFilePath)) {
                    File.Copy(sbmodCleanFilePath, sbmodFilePath, true);
                }

                string patchPath = $"{bs.gamePath}/master/0";
                string sbclean00PathFull = patchPath + "\\" + "00.dat.sbclean";
                string target00 = patchPath + "\\00.dat";

                Console.WriteLine("restoring 00.dat");
                if (File.Exists(sbclean00PathFull)) {
                    File.Copy(sbclean00PathFull, target00, true);
                }

                string sbclean01PathFull = patchPath + "\\" + "01.dat.sbclean";
                string target01 = patchPath + "\\01.dat";

                Console.WriteLine("restoring 01.dat");
                if (File.Exists(sbclean01PathFull)) {
                    File.Copy(sbclean01PathFull, target01, true);
                }
            }


            if (bs.makeMod) {
                ConsoleTitleAndWriteLine("makebite building " + snakeBiteMgvsFilePath);
                string toolArgs = "";
                toolArgs += bs.makebiteBuildPath;
                UseTool(ToolPathSettings.makeBitePath, toolArgs);

                if (!File.Exists(snakeBiteMgvsFilePath)) {
                    Console.WriteLine("Error! Cannot find " + snakeBiteMgvsFilePath);
                    Console.ReadKey();
                    return;
                }
                else {
                    Console.WriteLine("Copying built msgv");
                    File.Copy(snakeBiteMgvsFilePath, snakeBiteMgvsDestFilePath, true);
                    string lastBuildPath = bs.projectPath + "\\" + bs.modFileName + ".mgsv";
                    File.Copy(snakeBiteMgvsFilePath, lastBuildPath, true);
                }
            }


            if (bs.release == false) {
                if (bs.installOtherMods) {
                    ConsoleTitleAndWriteLine("running snakebite on othermods");
                    TraverseTree(bs.otherMgsvsPath, ".mgsv", RunSnakeBiteProcess, ref modFilesInfo);
                }

                if (bs.installMod) {
                    ConsoleTitleAndWriteLine("running snakebite on mod");
                    string snakeBiteMgsvPath = "\"" + snakeBiteMgvsFilePath + "\"";
                    string snakeBiteArgs = "";
                    snakeBiteArgs += " -i";//install
                    //snakeBiteArgs += " -c";//no conflict check
                    snakeBiteArgs += " -d";//reset hash
                    //snakeBiteArgs += " -s";//skip cleanup
                    snakeBiteArgs += " -x";//exit
                    UseTool(ToolPathSettings.snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
                }
            }

            ConsoleTitleAndWriteLine("done");
            if (bs.waitEnd) {
                Console.ReadKey();
            }
        }//Main

        private static void WriteDefaultConfigJson()
        {
            var config = new BuildModSettings();
            string jsonOutPath = @".\build-config-example.json";
            Console.WriteLine($"Writing default config to {jsonOutPath}");
            JsonSerializerSettings serializeSettings = new JsonSerializerSettings();
            serializeSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            string jsonStringOut = JsonConvert.SerializeObject(config, serializeSettings);

            File.WriteAllText(jsonOutPath, jsonStringOut);

            var toolSettings = new ToolPathSettings();
            jsonOutPath = @".\tools-config-example.json";
            Console.WriteLine($"Writing default tools config to {jsonOutPath}");
            serializeSettings = new JsonSerializerSettings();
            serializeSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            jsonStringOut = JsonConvert.SerializeObject(config, serializeSettings);

            File.WriteAllText(jsonOutPath, jsonStringOut);
        }

        private static Dictionary<string, BuildFileInfo> BuildLng2s(BuildModSettings bs, Dictionary<string, BuildFileInfo> modFilesInfo)
        {
            ConsoleTitleAndWriteLine("building lng2s");
            //tex copy over lang files to other lang coded
            List<string> langCodes = new List<string> {
                    //"eng",
                    "fre",
                    "ger",
                    "ita",
                    "jpn",
                    "por",
                    "rus",
                    "spa"
                };

            //tex KLUDGE ugh
            List<string> langFilesPre = new List<string> {
                    "ih_general",
                    "ih_quest",
                };

            string lngPackPath = @"\Assets\tpp\pack\ui\lang";
            string lngInternalPath = @"\Assets\tpp\lang\ui\";

            //TODO
            // \Assets\tpp\pack\ui\lang\lang_default_data_eng_fpk\Assets\tpp\lang\ui
            //for .lng2.xml files in modPackPath > lngPackPath + lang_default_data_eng_fpk + lngInternalPath
            //strip filename of .eng.lng2.xml?

            string lngPackPathTotal = @"\Assets\tpp\pack\ui\lang\lang_default_data_eng_fpk\Assets\tpp\lang\ui\";

            foreach (string modPackPath in bs.modPackPaths)
            {
                if (!Directory.Exists(modPackPath))
                {
                    continue;
                }
                string totalPath = modPackPath + lngPackPathTotal;
                if (!Directory.Exists(totalPath))
                {
                    continue;
                }

                foreach (string langCode in langCodes)
                {
                    string[] langFiles = Directory.GetFiles(totalPath);
                    foreach (string langFile in langFiles)
                    {
                        string langFilePre = Path.GetFileName(langFile);
                        if (Path.GetExtension(langFile) != ".xml")
                        {
                            continue;
                        }
                        string trimString = ".eng.lng2.xml";
                        int trimPos = langFilePre.Length - trimString.Length;
                        langFilePre = langFilePre.Remove(trimPos, trimString.Length);
                        string langFileEng = modPackPath + lngPackPath + @"\lang_default_data_eng_fpk" + lngInternalPath + langFilePre + "." + "eng" + ".lng2.xml";
                        langFileEng = UnfungePath(langFileEng);


                        string langFileDest = modPackPath + lngPackPath + @"\lang_default_data_" + langCode + "_fpk" + lngInternalPath + langFilePre + "." + langCode + ".lng2.xml";
                        langFileDest = UnfungePath(langFileDest);

                        File.Copy(langFileEng, langFileDest, true);
                    }

                }

            }


            foreach (string path in bs.modPackPaths)
            {
                if (Directory.Exists(path))
                {
                    TraverseTree(path, ".xml", RunLangToolProcess, ref modFilesInfo);

                    //TraverseTree(path, ".xml", DeleteLng2XmlProcess, ref modFilesInfo);
                }
            }

            return modFilesInfo;
        }

        private static void BuildIHHookRelease(bool makeMod)
        {
            Console.WriteLine("making IHHook release");
            string ihhookBuildFolder = @"C:\Projects\MGS\build\ihhook";
            string ihh_makebiteSourcePath = @"D:\GitHub\IHHook\makebite";
            string ihhMakebiteBuildPath = $"{ihhookBuildFolder}\\makebite\\";

            ConsoleTitleAndWriteLine("deleting existing ihh makebite build folder");
            DeleteAndWait(ihhMakebiteBuildPath);//tex GOTCHA will complain if open in explorer

            Directory.CreateDirectory(ihhMakebiteBuildPath);

            Console.WriteLine("copy IHHook makebite folder");
            CopyFilesRecursively(new DirectoryInfo(ihh_makebiteSourcePath), new DirectoryInfo(ihhMakebiteBuildPath), "", "");

            Console.WriteLine("copy IHHook dll");
            string destPath = ihhMakebiteBuildPath + @"\GameDir\";

            //tex copy readme so makebite builds it into metadata
            string ihh_readmeSource = @"D:\GitHub\IHHook\makebite\GameDir\mod\docs\IHHook-Readme.txt";
            string ihh_readmeDest = ihhMakebiteBuildPath + "\\Readme.txt";
            File.Copy(ihh_readmeSource, ihh_readmeDest, true);

            if (makeMod)
            {

                string ihhookMakeBiteMgvsDestFilePath = ihhookBuildFolder + "\\" + "IHHook" + ".mgsv";
                string ihhookMakeBiteMgvsFilePath = ihhMakebiteBuildPath + "\\" + "mod.mgsv";

                ConsoleTitleAndWriteLine("makebite building " + ihhookMakeBiteMgvsFilePath);
                string toolArgs = "";
                toolArgs += ihhMakebiteBuildPath;
                UseTool(ToolPathSettings.makeBitePath, toolArgs);

                if (!File.Exists(ihhookMakeBiteMgvsFilePath))
                {
                    Console.WriteLine("Error! Cannot find " + ihhookMakeBiteMgvsFilePath);
                    Console.ReadKey();
                    return;
                }
                else
                {
                    Console.WriteLine("Copying built msgv");
                    File.Copy(ihhookMakeBiteMgvsFilePath, ihhookMakeBiteMgvsDestFilePath, true);
                }
            }
        }//BuildIHHookRelease

        private static void ConsoleTitleAndWriteLine(string logLine)
        {
            Console.Title = titlePrefix + logLine;
            Console.WriteLine(logLine);
        }//ConsoleTitleAndWriteLine

        //tex get version from readme, superhax i know
        private static string GetModVersion(string modVersion, string readmePathFull) {
            if (File.Exists(readmePathFull)) {
                string[] readmeLines = File.ReadAllLines(readmePathFull);
                // ASSUMPTION: version on 2nd line, 1st chars
                if (readmeLines.Length > 1) {
                    char[] splitchar = { ' ' };
                    string[] split = readmeLines[1].Split(splitchar);
                    if (split.Length != 0) {
                        modVersion = split[0];
                    }
                }
            }

            return modVersion;
        }

        private static void DeleteAndWait(string path) {
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
            while (Directory.Exists(path)) {
                Thread.Sleep(100);
            }
        }



        private static bool IsForFpk(BuildFileInfo buildFileInfo) {
            return buildFileInfo.packPath != "";
        }

        public static void TraverseTree(string root, string extension, ProcessFileDelegate processFile, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            Stack<string> dirs = new Stack<string>(20);

            if (!System.IO.Directory.Exists(root)) {
                throw new ArgumentException();
            }
            dirs.Push(root);

            while (dirs.Count > 0) {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                catch (UnauthorizedAccessException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (System.IO.DirectoryNotFoundException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }

                string[] files = null;
                try {
                    files = System.IO.Directory.GetFiles(currentDir);
                }
                catch (UnauthorizedAccessException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (System.IO.DirectoryNotFoundException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }

                foreach (string file in files) {
                    try {

                        System.IO.FileInfo fi = new System.IO.FileInfo(file);
                        if (fi.Extension == extension) {
                            processFile(fi, ref buildFileInfoList);
                        }
                    }
                    catch (System.IO.FileNotFoundException e) {
                        Console.WriteLine(e.Message);
                        continue;
                    }
                }

                foreach (string str in subDirs)
                    dirs.Push(str);
            }
        }//TraverseTree

        public static void ReadLuaBuildInfoProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {

            /* REF:
            -- DOBUILD: 1
            -- ORIGINALQAR: chunk0
            -- PACKPATH: \Assets\tpp\pack\mission2\init\init.fpkd
            */
            // ASSUMPTION! no spaces in attrib1

            string[] lines = File.ReadAllLines(fileInfo.FullName);

            if (lines.Length == 0) {
                return;
            }

            if (!lines[0].StartsWith("-- DOBUILD:")) {
                return;
            }

            Console.WriteLine(fileInfo.Name);

            BuildFileInfo buildFileInfo = null;
            if (!buildFileInfoList.TryGetValue(fileInfo.FullName, out buildFileInfo)) {
                buildFileInfo = new BuildFileInfo();
                buildFileInfoList.Add(fileInfo.FullName, buildFileInfo);
            }




            char[] delimiterChars = { ' ' };
            foreach (string line in lines) {
                string[] words = line.Split(delimiterChars);

                if (words.Length == 0) {
                    break;
                }
                if (words[0] != "--") {
                    break;
                }

                string attribId = words[1];
                string attribValue = "";
                if (words.Length > 2) {
                    attribValue = words[2];
                }
                switch (attribId) {
                    case "DOBUILD:":
                        System.Console.WriteLine(line);

                        int doBuild = 0;
                        if (!Int32.TryParse(attribValue, out doBuild)) {
                        }

                        buildFileInfo.doBuild = doBuild > 0;
                        break;
                    /* CULL: case "ORIGINALQAR:":
                        System.Console.WriteLine(line);
                        if (buildFileInfo.originalQar == "") {
                            buildFileInfo.originalQar = attribValue + "_dat";
                        }
                        break;
                        */
                    case "PACKPATH:":
                        System.Console.WriteLine(line);
                        buildFileInfo.packPath = attribValue;
                        break;
                }

                if (fileInfo.Extension == ".lua") {
                    buildFileInfo.fullPath = fileInfo.FullName;
                }
            }
        }//ReadLuaBuildInfoProcess

        public static void RunLangToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".lng2.xml")) {
                return;
            }

            UseTool(ToolPathSettings.langToolPath, fileInfo.FullName);
        }

        public static void DeleteLng2XmlProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (fileInfo.Name.Contains(".lng2.xml")) {
                fileInfo.Delete();
            }
        }

        static string[] fox2Types = {
            ".fox2",
            ".sdf",
            ".parts",
            ".tgt",
        };

        public static void RunFoxToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            bool isFox2Type = false;
            foreach (string fox2Type in fox2Types) {
                if (fileInfo.Name.Contains(fox2Type + ".xml")) {
                    isFox2Type = true;
                    break;
                }
            }
            if (!isFox2Type) {
                return;
            }

            UseTool(ToolPathSettings.foxToolPath, fileInfo.FullName);
        }

        public static void RunSubpToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".xml")) {
                return;
            }

            UseTool(ToolPathSettings.subpToolPath, fileInfo.FullName);
        }

        public static void RunLbaToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList)
        {
            if (!fileInfo.Name.Contains(".lba.xml"))
            {
                return;
            }

            UseTool(ToolPathSettings.lbaToolPath, fileInfo.FullName);
        }

        public static void RunGzToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".fpk.xml") && !fileInfo.Name.Contains(".fpkd.xml")) {
                return;
            }

            UseTool(ToolPathSettings.gzsToolPath, fileInfo.FullName);
        }

        public static void RunSnakeBiteProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".mgsv")) {
                return;
            }

            string snakeBiteMgsvPath = "\"" + fileInfo.FullName + "\"";
            string snakeBiteArgs = "";
            snakeBiteArgs += " -i";//install
            snakeBiteArgs += " -c";//no conflict check
            snakeBiteArgs += " -d";//reset hash
            snakeBiteArgs += " -s";//skip cleanup
            snakeBiteArgs += " -x";//exit

            UseTool(ToolPathSettings.snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
        }

        /* CULL
        private static string UsePackTool(string packPath, bool pack) {
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = gzsToolPath;
            p.StartInfo.Arguments = packPath;
            if (pack) {
                p.StartInfo.Arguments += ".xml";
            }
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var exitCode = p.ExitCode;
            return output;
        }*/


        private static void UseTool(string toolPath, string args) {
            Console.WriteLine(toolPath);
            Console.WriteLine(args);
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            // p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(toolPath);
            p.StartInfo.FileName = toolPath;
            p.StartInfo.Arguments = args;
            p.Start();
            //string output = p.StandardOutput.ReadToEnd(); //tex (m/sn)akebite doesn't have output. TODO: or does it, I know Topher added logging at some point
            p.WaitForExit();
            var exitCode = p.ExitCode;
            // return output;
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, string extension, string exclude) {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), extension, exclude);
            foreach (FileInfo file in source.GetFiles()) {
                if (exclude == "" || file.Extension != exclude) {
                    if (extension == "" || file.Extension == extension) {
                        Console.WriteLine(file.Name);
                        File.Copy(file.FullName, Path.Combine(target.FullName, file.Name), true);
                    }
                }
            }
        }

        private static string GetPath(string path) {
            if (Directory.Exists(path) || File.Exists(path)) {
                if (!Path.IsPathRooted(path)) {
                    path = Path.GetFullPath(path);
                }
            }
            else {
                path = null;
            }

            return path;
        }


        static void DeleteEmptyDirs(string dir) {
            if (String.IsNullOrEmpty(dir))
                throw new ArgumentException(
                    "Starting directory is a null reference or an empty string",
                    "dir");

            try {
                foreach (var d in Directory.EnumerateDirectories(dir)) {
                    DeleteEmptyDirs(d);
                }

                var entries = Directory.EnumerateFileSystemEntries(dir);

                if (!entries.Any()) {
                    try {
                        Directory.Delete(dir);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }
        }//DeleteEmptyDirs
    }//class Program
}//namespace mgsv_buildmod
