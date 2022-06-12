//mgsv_buildmod
//tex: a house of cards built up over time to build and install infinite heaven file for development, or just build for release
//set tool paths via mgsv_buildmod.exe.config
//run without any args to generate a .json of BuildModSettings
//pass in path to a BuildModSettings .json to use it

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Threading;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace mgsv_buildmod {
    class Program {
        class BuildModSettings {
            public string projectPath = @"C:\Projects\MGS\InfiniteHeaven\tpp";

            public string luaPackFilesPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\fpkd-combined-lua";

            //tex folders have various tools run on them (see buildFox2s etc settings)
            //then are copied outright to makebitepath
            ///so need to be in makebiteable layout
            public List<string> modPackPaths = new List<string> {
                @"C:\Projects\MGS\InfiniteHeaven\tpp-release\ih-data1_dat-lua",
                @"C:\Projects\MGS\InfiniteHeaven\tpp-release\fpk-mod",
                @"C:\Projects\MGS\InfiniteHeaven\tpp\fpk-mod-ih",
            };

            public string otherMgsvsPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\othermods";//tex: folder of other mgsv files to install when release: false, installOtherMods: true

            public string docsPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\docs";

            public string internalLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\Assets";//tex for copyInternalLua
            public string externalLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir";//tex for copyExternalLua
            public string modulesLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\modules";//tex for copyModulesToInternal

            public string buildFolder = @"C:\Projects\MGS\build\infiniteheaven"; //tex: where the various files are actually pulled together before being makebitten      
            public string makebiteBuildPath = @"C:\Projects\MGS\build\infiniteheaven\makebite";

            public string gamePath = @"C:\Games\Steam\SteamApps\common\MGS_TPP";

            public string ihExtPath = @"D:\GitHub\IHExt\IHExt\bin\Release\IHExt.exe";
            public bool copyIHExt = false; //tex only run on release


            // TODO: just point to sperate file
            public string modVersionDefault = "rXXX";
            public string modFileName = "Infinite Heaven";//tex .mgsv name
            public string readMeName = "Infinite Heaven Readme.txt";

            public bool copyDocsToBuild = true;//tex copies docsPath to build, so they can be included in release zip for user to check out without installing or unzipping .mgsv

            public bool cleanDat = false;

            public bool copyEngLng2sToOtherLangCodes = true;//tex if you dont have actual translations for lang codes this will copy the eng lng2s to the other lang code lng2s

            public Dictionary<string, bool> fileTypesToCompile = new Dictionary<string, bool>() {
                {".fox2.xml", true },
                {".sdf.xml", true },
                {".parts.xml", true },
                {".tgt.xml", true },
                {".lba.xml", true },
                {".lng2.xml", true },
            };

            public bool copyModPackFolders = true;
            //tex copies internalLuaPath/core external lua to internal
            //WARNING: ih will still try to load external by default, so do not include internalLuaPath files in gamedir-mod\release)
            public bool copyInternalLua = false;

            //tex copies externalLuaPath to makeBite/GameDir
            //WARNING: will overwrite MGS_TPP\mod if installMod true, so only should be for release, since for non release I have symlinked game path/mod to externalLuaPath
            public bool copyExternalLua = false;

            public bool copyModulesToInternal = false;//copies external lua modules to internal [WIP]

            public bool makeMod = true; //tex run makebite on built mod
            //tex (if installmod), uninstall previous mod, false will just install over top,
            ///which may save some time, only issue may be if you removed some files in the new version
            public bool uninstallExistingMod = true;
            public bool installMod = true;//tex install build mod, not run if release
            public bool installOtherMods = true;//tex install .mgsvs in otherMgsvsPath (done before actual mod), not run if release

            public bool release = false;//DEBUGNOW dont forget to also set copyExternalLua true


            public bool waitEnd = true;
        }//BuildModSettings

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

        public delegate void ProcessFileDelegate(string fileName);
        public delegate void ProcessFileDelegateBuildFileInfoList(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);
        static string titlePrefix = "mgsv_buildmod - ";
        static void Main(string[] args) {
            var runWatch = new Stopwatch();
            runWatch.Start();
            Console.Title = titlePrefix;
            var cc = new ConsoleCopy("mgsv_buildmod_log.txt");//tex anything written to Console is also written to log

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


            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);//UNUSED

            BuildIHHookRelease(bs.makeMod);
            ConsoleTitleAndWriteLine("deleting existing makebite build folder");
            DeleteAndWait(bs.makebiteBuildPath);//tex GOTCHA will complain if open in explorer
            ConsoleTitleAndWriteLine("Copy docs and exes");
            if (bs.copyDocsToBuild) {
                CopyDocsToBuild(bs);
            }
            if (bs.release) {
                CopyIHExt(bs);
            }

            string modVersion = bs.modVersionDefault;

            string readmePathFull = bs.docsPath + "\\" + bs.readMeName;//DEBUGNOW

            modVersion = GetModVersion(modVersion, readmePathFull);
            Console.WriteLine("got modVersion:{0}", modVersion);

            ConsoleTitleAndWriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            //tex TODO restrict to Data1Lua,FpkCombineLua
            TraverseTreeFileInfoList(bs.luaPackFilesPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);
            //tex allow text files as subsituted //TODO wut
            //TraverseTreeFileInfoList(luaPath, ".txt", ReadLuaBuildInfoProcess, ref modFilesInfo);
            if (modFilesInfo.Count == 0) {
                //DEBUGNOW hmm
                Console.WriteLine("no mod files found");
                return;
            }

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

            if (bs.copyEngLng2sToOtherLangCodes) {
                CopyEngLng2sToOtherLangCodes(bs);
            }

            //TODO: problem: subptool names its decompiled files .xml instead of .subp.xml, also needs encoding?
            //TODO: add other fox2 types, and other tools, in IH cull the .xml files of those you haven't actually modified and use the original retail files
            //TODO: even though I've just toolspaths to Properties.Settings, this is making me think of moving to toolspaths .json in exe dir
            var fileTypesToCompileToolPaths = new Dictionary<string, string>() {
                {".fox2.xml", Properties.Settings.Default.foxToolPath },
                {".sdf.xml", Properties.Settings.Default.foxToolPath },
                {".parts.xml", Properties.Settings.Default.foxToolPath },
                {".tgt.xml", Properties.Settings.Default.foxToolPath },
                {".lba.xml", Properties.Settings.Default.lbaToolPath },
                {".lng2.xml", Properties.Settings.Default.langToolPath },
            };

            Console.WriteLine("Getting modPackPaths list");
            var modPackFiles = bs.modPackPaths.SelectMany(modPackPath => Directory.EnumerateFiles(modPackPath, "*.*", SearchOption.AllDirectories));

            Console.WriteLine("Compiling modPackPaths files");
            var taskWatch = new Stopwatch();
            taskWatch.Start();
            var tasks = new List<Task>();
            foreach (var filePath in modPackFiles) {
                foreach (var item in bs.fileTypesToCompile) {
                    if (filePath.Contains(item.Key) && item.Value == true) {
                        //Console.WriteLine($"filePath: {filePath}");//DEBUGNOW //tex GOTCHA: any logging in loops will dratsically increase the processing time,
                        //don't need to inform the user of progress of something thats only going to take a few seconds, moreso if doing so will double that time.
                        tasks.Add(Task.Run(() => UseTool(fileTypesToCompileToolPaths[item.Key], filePath)));
                        //UseTool(fileTypesToCompileToolPaths[item.Key], filePath);//DEBUGNOW
                    }
                }
            }//foreach modPackPath
            Task.WaitAll(tasks.ToArray());
            taskWatch.Stop();
            Console.WriteLine($"time to compile: {taskWatch.ElapsedMilliseconds}ms");

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

            if (bs.copyInternalLua) {
                Console.WriteLine();

                ConsoleTitleAndWriteLine("copying core external folder to internal");

                if (Directory.Exists(bs.internalLuaPath)) {
                    string destPath = bs.makebiteBuildPath + @"\Assets";
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(bs.internalLuaPath), new DirectoryInfo(destPath), "", "");
                    //DEBUGNOW also don't like this, modules will have been blindly copied via copyExternalLua, so kill them
                    string buildExternalAssetsPath = bs.makebiteBuildPath + @"\GameDir\mod\Assets\";
                    if (Directory.Exists(buildExternalAssetsPath)) {
                        DeleteAndWait(buildExternalAssetsPath);
                    }
                }
            }

            if (bs.copyModulesToInternal) {
                Console.WriteLine();

                ConsoleTitleAndWriteLine("copying external modules folder to internal");

                if (Directory.Exists(bs.modulesLuaPath)) {
                    string destPath = bs.makebiteBuildPath + @"\Assets\tpp\script\ih";//DEBUGNOW dont like this
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(bs.modulesLuaPath), new DirectoryInfo(destPath), "", "");
                    //DEBUGNOW also don't like this, modules will have been blindly copied via copyExternalLua, so kill them
                    string modulesPath = bs.makebiteBuildPath + @"\GameDir\mod\modules";
                    if (Directory.Exists(modulesPath)) {
                        DeleteAndWait(modulesPath);
                        Directory.CreateDirectory(modulesPath);
                        File.CreateText(modulesPath + "/ih_files.txt").Close();
                    }
                }
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
                UseTool(Properties.Settings.Default.makeBitePath, toolArgs);

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
                    TraverseTree(bs.otherMgsvsPath, ".mgsv", RunSnakeBiteProcess);
                }

                if (bs.uninstallExistingMod) {
                    ConsoleTitleAndWriteLine("uninstalling existing mod with snakebite");
                    string snakeBiteArgs = "";
                    snakeBiteArgs += " -u";//uninstall
                    //snakeBiteArgs += " -s";//skip cleanup
                    snakeBiteArgs += " -x";//exit
                    UseTool(Properties.Settings.Default.snakeBitePath, bs.modFileName + snakeBiteArgs);
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
                    UseTool(Properties.Settings.Default.snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
                }
            }

            runWatch.Stop();
            ConsoleTitleAndWriteLine($"done in {runWatch.ElapsedMilliseconds}ms");
            if (bs.waitEnd) {
                ConsoleTitleAndWriteLine("press any key to exit");
                Console.ReadKey();
            }
        }//Main

        private static void CopyIHExt(BuildModSettings bs) {
            if (bs.copyIHExt) {
                if (!File.Exists(bs.ihExtPath)) {
                    Console.WriteLine("WARNING: could not find ihExtPath " + bs.ihExtPath);
                }
                else {
                    Console.WriteLine("copying IHExt");
                    string destPath = bs.makebiteBuildPath + @"\GameDir\mod\";
                    if (!Directory.Exists(destPath)) {
                        Directory.CreateDirectory(destPath);
                    }
                    File.Copy(bs.ihExtPath, destPath + "IHExt.exe");
                }
            }
        }//CopyIHExt

        private static void CopyDocsToBuild(BuildModSettings bs) {
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
        }

        private static void WriteDefaultConfigJson() {
            var config = new BuildModSettings();
            string jsonOutPath = @".\build-config-example.json";
            Console.WriteLine($"Writing default config to {jsonOutPath}");
            JsonSerializerSettings serializeSettings = new JsonSerializerSettings();
            serializeSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            string jsonStringOut = JsonConvert.SerializeObject(config, serializeSettings);

            File.WriteAllText(jsonOutPath, jsonStringOut);
        }

        private static void CopyEngLng2sToOtherLangCodes(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("copy eng lang files to other lang codes");
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

            string lngPackPath = @"\Assets\tpp\pack\ui\lang";
            string lngInternalPath = @"\Assets\tpp\lang\ui\";

            //TODO
            // \Assets\tpp\pack\ui\lang\lang_default_data_eng_fpk\Assets\tpp\lang\ui
            //for .lng2.xml files in modPackPath > lngPackPath + lang_default_data_eng_fpk + lngInternalPath
            //strip filename of .eng.lng2.xml?

            string lngPackPathTotal = @"\Assets\tpp\pack\ui\lang\lang_default_data_eng_fpk\Assets\tpp\lang\ui\";

            foreach (string modPackPath in bs.modPackPaths) {
                if (!Directory.Exists(modPackPath)) {
                    continue;
                }
                string totalPath = modPackPath + lngPackPathTotal;
                if (!Directory.Exists(totalPath)) {
                    continue;
                }

                foreach (string langCode in langCodes) {
                    string[] langFiles = Directory.GetFiles(totalPath);
                    foreach (string langFile in langFiles) {
                        string langFilePre = Path.GetFileName(langFile);
                        if (Path.GetExtension(langFile) != ".xml") {
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

                }//foreach langCode
            }//foreach modPackPath
        }//CopyEngLng2sToOtherLangCodes

        //CULL this should be handled by ihhook repo now that it's unbundled
        private static void BuildIHHookRelease(bool makeMod) {
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

            if (makeMod) {

                string ihhookMakeBiteMgvsDestFilePath = ihhookBuildFolder + "\\" + "IHHook" + ".mgsv";
                string ihhookMakeBiteMgvsFilePath = ihhMakebiteBuildPath + "\\" + "mod.mgsv";

                ConsoleTitleAndWriteLine("makebite building " + ihhookMakeBiteMgvsFilePath);
                string toolArgs = "";
                toolArgs += ihhMakebiteBuildPath;
                UseTool(Properties.Settings.Default.makeBitePath, toolArgs);

                if (!File.Exists(ihhookMakeBiteMgvsFilePath)) {
                    Console.WriteLine("Error! Cannot find " + ihhookMakeBiteMgvsFilePath);
                    Console.ReadKey();
                    return;
                }
                else {
                    Console.WriteLine("Copying built msgv");
                    File.Copy(ihhookMakeBiteMgvsFilePath, ihhookMakeBiteMgvsDestFilePath, true);
                }
            }
        }//BuildIHHookRelease

        private static void ConsoleTitleAndWriteLine(string logLine) {
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

        public static void TraverseTree(string root, string extension, ProcessFileDelegate processFile) {
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
                            processFile(fi.FullName);
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
        //TODO: only difference between this and TraverseTree is extra param being passed into processFile delegate
        public static void TraverseTreeFileInfoList(string root, string extension, ProcessFileDelegateBuildFileInfoList processFile, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
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
        }//TraverseTreeFileInfoList

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

        public static void RunSnakeBiteProcess(string fileName) {
            if (!fileName.Contains(".mgsv")) {
                return;
            }

            string snakeBiteMgsvPath = "\"" + fileName + "\"";
            string snakeBiteArgs = "";
            snakeBiteArgs += " -i";//install
            snakeBiteArgs += " -c";//no conflict check
            snakeBiteArgs += " -d";//reset hash
            snakeBiteArgs += " -s";//skip cleanup
            snakeBiteArgs += " -x";//exit

            UseTool(Properties.Settings.Default.snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
        }

        private static void UseTool(string toolPath, string args) {
            //Console.WriteLine(toolPath);
            //Console.WriteLine(args);
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
                        //Console.WriteLine(file.Name);
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
