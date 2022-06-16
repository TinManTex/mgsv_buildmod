//mgsv_buildmod
//tex: a house of cards built up over time to build and install infinite heaven file for development, or just build for release
//set tool paths via mgsv_buildmod.exe.config
//run without any args to generate a .json of BuildModSettings
//pass in path to a BuildModSettings .json to use it
//see comments on BuildModSettings below

//terms:
//internal: files in dat
//external: files in gamedir

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

namespace mgsv_buildmod {
    class Program {
        class BuildModSettings {
            //tex smakebite metadata
            public string Version = "";
            public string Name = "";
            public string Author = "";
            public string Website = "";

            public string projectPath = @"C:\Projects\MGS\InfiniteHeaven\tpp";//tex TODO: rethink, currently ony for copying last built mgsv 

            public string luaFpkdFilesPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\fpkd-combined-lua";//tex for copyLuaFpkdFiles 

            //tex folders have various tools run on them (see buildFox2s etc settings)
            //then are copied outright to makebitepath
            ///so need to be in makebiteable layout
            //GOTCHA: don't fill this out with example because json entries are added rather than replace
            public List<string> modPackPaths = new List<string> { };

            public string modFileName = "Infinite Heaven";//tex .mgsv name
            public string readMeFileName = "Readme.txt";//STRUCURE inside docs path
            public string docsPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\docs";
            public string metadataPath = @"C:\Projects\MGS\InfiniteHeaven\tpp";

            public string externalLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir";//tex for copyExternalLuaToInternal //TODO: point to GameDir/mod/ when you makbitify it
            public string modulesLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\mod-gamedir\modules";//tex for copyModulesToInternal
            public string modulesInternalPath = @"\Assets\tpp\script\ih";//tex for copyModulesToInternal

            public string makebiteBuildPath = @"C:\Projects\MGS\build\infiniteheaven\makebite"; //tex: where the various files are actually pulled together before being makebitten
            public string buildPath = @"C:\Projects\MGS\build\infiniteheaven\build";//tex where the built .mgsv and docs folder are placed ready for being zipped for release //TODO: zip it too

            public string gamePath = @"C:\Games\Steam\SteamApps\common\MGS_TPP";

            public bool copyDocsToBuild = true;//tex copies docsPath to build, so they can be included in release zip for user to check out without installing or unzipping .mgsv

            public bool copyEngLng2sToOtherLangCodes = true;//tex if you dont have actual translations for lang codes this will copy the eng lng2s to the other lang code lng2s

            public bool compileModPackFiles = true; //tex overall switch of below
            //SYNC: CompileModPackFiles
            public Dictionary<string, bool> fileTypesToCompile = new Dictionary<string, bool>() {
                {".bnd.xml", true },
                {".clo.xml", true },
                {".des.xml", true },
                {".evf.xml", true },
                {".fox2.xml", true },
                {".fsd.xml", true },
                {".lad.xml", true },
                {".parts.xml", true },
                {".ph.xml", true },
                {".phsd.xml", true },
                {".sdf.xml", true },
                {".sim.xml", true },
                {".tgt.xml", true },
                {".veh.xml", true },

                {".lba.xml", true },
                {".lng.xml", true },
                {".lng2.xml", true },
            };
            
            public bool copyLuaFpkdFiles = true;//tex uses luaFpkdFilesPath, fpk internal pathed lua files, their DOBUILD comment headers are used to copy them to full fpk paths
            public bool copyModPackFolders = true;//tex uses modPackPaths
            //tex copies externalLuaPath to internal, intended for release. So you can develop using IHs external (gamedir\mod\<in-dat path>), and then copy them in to in-dat for release
            //WARNING: ih will still try to load external by default, so do not include externalLuaPath files in gamedir-mod\release)
            //uses externalLuaPath
            public bool copyExternalLuaToInternal = false;

            public bool copyModulesToInternal = false;//copies external lua modules to internal [WIP]

            public bool makeMod = true; //tex run makebite on built mod
            //tex (if installmod), uninstall previous mod, false will just install over top,
            //which may save some time, only issue may be if you removed some files in the new version
            //GOTCHA: uninstall only uninstalls 1st entry of Name found (multiple mods of same name may be installed, see GOTCHA below)
            public bool uninstallExistingMod = true;
            public bool installMod = true;//tex install build mod
            //GOTCHA: snakebite will actually add new mod entry for every install, installing with the same mod Name does not overwrite that entry (even though it overwrites files)
            //TODO: snakebite: possibly an overwritemodofsamename option
            public bool waitEnd = true;//tex leaves program window open when done, with press any key to exit
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

        public delegate void ProcessFileDelegateBuildFileInfoList(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);
        static string titlePrefix = "mgsv_buildmod - ";
        static void Main(string[] args) {
            var runWatch = new Stopwatch();
            runWatch.Start();
            Console.Title = titlePrefix;
            var cc = new ConsoleCopy("mgsv_buildmod_log.txt");//tex anything written to Console is also written to log //TODO: actually doesnt capture exceptions, I guess I need to capture Console.Error too? 

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

            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);//UNUSED

            ConsoleTitleAndWriteLine("deleting existing makebite build folder");
            DeleteAndWait(bs.makebiteBuildPath);//tex GOTCHA will complain if open in explorer

            ConsoleTitleAndWriteLine("Copy docs and exes");
            if (bs.copyDocsToBuild) {
                CopyDocsToBuild(bs);
            }

            if (bs.copyLuaFpkdFiles) {
                CopyLuaFpkdFiles(bs);
            }

            if (bs.copyEngLng2sToOtherLangCodes) {
                CopyEngLng2sToOtherLangCodes(bs);
            }

            if (bs.compileModPackFiles) {
                CompileModPackFiles(bs);
            }

            if (bs.copyModPackFolders) {
                Console.WriteLine();
                ConsoleTitleAndWriteLine("copying modPackPaths folders");
                foreach (string path in bs.modPackPaths) {
                    if (Directory.Exists(path)) {
                        Console.WriteLine(path);
                        CopyFilesRecursively(new DirectoryInfo(path), new DirectoryInfo(bs.makebiteBuildPath), "", ".xml");
                    }
                }
            }

            if (bs.copyExternalLuaToInternal) {
                Console.WriteLine();
                CopyExternalLuaToInternal(bs);
            }

            if (bs.copyModulesToInternal) {
                Console.WriteLine();
                CopyModulesToInternal(bs);
            }

            string makeBiteMetaDataFilePath = $"{bs.metadataPath}\\metadata.xml";
            string makeBiteMetaDataDestFilePath = $"{bs.makebiteBuildPath}\\metadata.xml";

            ConsoleTitleAndWriteLine("Updating metadata version tag");
            if (File.Exists(makeBiteMetaDataFilePath)) {
                XDocument xmlFile = XDocument.Load(makeBiteMetaDataFilePath);

                var query = from c in xmlFile.Elements("ModEntry")
                            select c;

                foreach (XElement entry in query) {
                    entry.Attribute("Version").Value = bs.Version;
                    entry.Attribute("Name").Value = bs.Name;
                    entry.Attribute("Author").Value = bs.Author;
                    entry.Attribute("Website").Value = bs.Website;
                }

                xmlFile.Save(makeBiteMetaDataFilePath);
            }
            //tex alternative would be to read the file and push it into the metadata description tag above
            ConsoleTitleAndWriteLine("Copying mod readme");                
            string snakeBiteReadMeFilePath = $"{bs.docsPath}\\{bs.readMeFileName}";
            string snakeBiteReadMeDestFilePath = $"{bs.makebiteBuildPath}\\readme.txt";
            if (File.Exists(snakeBiteReadMeFilePath)) {
                File.Copy(snakeBiteReadMeFilePath, snakeBiteReadMeDestFilePath, true);
            }

            ConsoleTitleAndWriteLine("Copying mod metadata");
            if (File.Exists(makeBiteMetaDataFilePath)) {
                File.Copy(makeBiteMetaDataFilePath, makeBiteMetaDataDestFilePath, true);
            }                
            
            string makebiteMgsvOutputFilePath = $"{bs.makebiteBuildPath}\\mod.mgsv";
            string makeBiteMgsvDestFilePath = $"{bs.buildPath}\\{bs.modFileName}.mgsv";

            if (bs.makeMod) {                            
                ConsoleTitleAndWriteLine("makebite building " + makebiteMgsvOutputFilePath);
                string toolArgs = "";
                toolArgs += bs.makebiteBuildPath;
                UseTool(Properties.Settings.Default.makeBitePath, toolArgs);

                if (!File.Exists(makebiteMgsvOutputFilePath)) {
                    Console.WriteLine("Error! Cannot find " + makebiteMgsvOutputFilePath);
                    Console.ReadKey();
                    return;
                }
                else {
                    Console.WriteLine("Copying built msgv");


                    File.Copy(makebiteMgsvOutputFilePath, makeBiteMgsvDestFilePath, true);
                    string lastBuildPath = bs.projectPath + "\\" + bs.modFileName + ".mgsv";
                    File.Copy(makebiteMgsvOutputFilePath, lastBuildPath, true);
                }
            }

            if (bs.uninstallExistingMod) {
                ConsoleTitleAndWriteLine("uninstalling existing mod with snakebite");
                string snakeBiteArgs = "";
                snakeBiteArgs += " -u";//uninstall
                //snakeBiteArgs += " -s";//skip cleanup
                snakeBiteArgs += " -x";//exit when done
                UseTool(Properties.Settings.Default.snakeBitePath, bs.Name + snakeBiteArgs);
            }

            if (bs.installMod) {
                ConsoleTitleAndWriteLine("intalling mod with snakebite");
                string snakeBiteArgs = "";
                snakeBiteArgs += " -i";//install
                //snakeBiteArgs += " -c";//no conflict check
                snakeBiteArgs += " -d";//reset hash
                //snakeBiteArgs += " -s";//skip cleanup
                snakeBiteArgs += " -x";//exit when done
                UseTool(Properties.Settings.Default.snakeBitePath, makebiteMgsvOutputFilePath + snakeBiteArgs);
            }

            runWatch.Stop();
            ConsoleTitleAndWriteLine($"done in {runWatch.ElapsedMilliseconds}ms");
            if (bs.waitEnd) {
                ConsoleTitleAndWriteLine("press any key to exit");
                Console.ReadKey();
            }
        }//Main

        private static void CompileModPackFiles(BuildModSettings bs) {
            //TODO: problem: subptool names its decompiled files .xml instead of .subp.xml, also needs encoding?
            //TODO: add other fox2 types, and other tools, in IH cull the .xml files of those you haven't actually modified and use the original retail files
            //TODO: even though I've just toolspaths to Properties.Settings, this is making me think of moving to toolspaths .json in exe dir
            //SYNC: bs.fileTypesToCompile
            var fileTypesToCompileToolPaths = new Dictionary<string, string>() {
                {".bnd.xml", Properties.Settings.Default.foxToolPath },
                {".clo.xml", Properties.Settings.Default.foxToolPath },
                {".des.xml", Properties.Settings.Default.foxToolPath },
                {".evf.xml", Properties.Settings.Default.foxToolPath },
                {".fox2.xml", Properties.Settings.Default.foxToolPath },
                {".fsd.xml", Properties.Settings.Default.foxToolPath },
                {".lad.xml", Properties.Settings.Default.foxToolPath },
                {".parts.xml", Properties.Settings.Default.foxToolPath },
                {".ph.xml", Properties.Settings.Default.foxToolPath },
                {".phsd.xml", Properties.Settings.Default.foxToolPath },
                {".sdf.xml", Properties.Settings.Default.foxToolPath },
                {".sim.xml", Properties.Settings.Default.foxToolPath },
                {".tgt.xml", Properties.Settings.Default.foxToolPath },
                {".veh.xml", Properties.Settings.Default.foxToolPath },

                {".lba.xml", Properties.Settings.Default.lbaToolPath },
                {".lng.xml", Properties.Settings.Default.langToolPath },
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
        }

        private static void CopyModulesToInternal(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("copying external modules folder to internal");
            if (Directory.Exists(bs.modulesLuaPath)) {
                //DEBUGNOW dont like this, need a more general external path to internal path system?
                string destPath = $"{bs.makebiteBuildPath}\\{bs.modulesInternalPath}";
                Directory.CreateDirectory(destPath);
                CopyFilesRecursively(new DirectoryInfo(bs.modulesLuaPath), new DirectoryInfo(destPath), "", "");
                string modulesPath = bs.makebiteBuildPath + @"\GameDir\mod\modules";
                if (Directory.Exists(modulesPath)) {
                    DeleteAndWait(modulesPath);
                    Directory.CreateDirectory(modulesPath);
                    File.CreateText(modulesPath + "/ih_files.txt").Close();
                }
            }
        }

        private static void CopyExternalLuaToInternal(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("copying external folders to internal");
            //tex just covering internal folders that have lua in tpp base game
            string[] internalFolders = {
                "Assets",
                "Fox",
                "shaders",
                "Tpp"
            };
            foreach (var subPath in internalFolders) {
                var externalPath = $"{bs.externalLuaPath}\\{subPath}";
                if (Directory.Exists(externalPath)) {
                    string internalPath = $"{bs.makebiteBuildPath}\\{subPath}";
                    Directory.CreateDirectory(internalPath);
                    CopyFilesRecursively(new DirectoryInfo(externalPath), new DirectoryInfo(internalPath), "", "");
                    //DEBUGNOW also don't like this, modules will have been blindly copied via modPackPaths, so kill them
                    string buildExternalAssetsPath = $"{bs.makebiteBuildPath}\\GameDir\\mod\\{subPath}";
                    if (Directory.Exists(buildExternalAssetsPath)) {
                        DeleteAndWait(buildExternalAssetsPath);
                    }
                }
            }
        }

        private static void CopyLuaFpkdFiles(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("CopyLuaPackFiles");
            ConsoleTitleAndWriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            //tex TODO restrict to Data1Lua,FpkCombineLua
            TraverseTreeFileInfoList(bs.luaFpkdFilesPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);
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
                        string internalPath = buildFileInfo.fullPath.Substring(bs.luaFpkdFilesPath.Length);
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
        }//CopyLuaPackFiles

        private static void CopyDocsToBuild(BuildModSettings bs) {
            if (Directory.Exists(bs.docsPath)) {
                Console.Title = titlePrefix + "Copy docs";
                Console.WriteLine("copying docs files to build folder");
                string docsDestinationPath = bs.buildPath + @"\docs";
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

        private static void ConsoleTitleAndWriteLine(string logLine) {
            Console.Title = titlePrefix + logLine;
            Console.WriteLine(logLine);
        }//ConsoleTitleAndWriteLine

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
