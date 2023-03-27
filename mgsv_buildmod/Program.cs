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
        //CULL CopyLuaFpkdFiles
        public class BuildFileInfo {
            public string fullPath = "";
            public bool doBuild = false;
            // public string filePath = "";
            public string packPath = "";
        }

        public delegate void ProcessFileDelegateBuildFileInfoList(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);
        static string titlePrefix = "mgsv_buildmod - ";
        static void Main(string[] args) {
            var runWatch = new Stopwatch();
            runWatch.Start();

            var stepWatch = new Stopwatch();
            //stepWatch.Start();

            Console.Title = titlePrefix;
            var cc = new ConsoleCopy("mgsv_buildmod_log.txt");//tex anything written to Console is also written to log //TODO: actually doesnt capture external process exceptions? I guess I need to capture Console.Error too? 

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
            configPath = UnfungePath(configPath);

            //TODO: test path exists

            string jsonString = File.ReadAllText(configPath);
            BuildModSettings bs = JsonConvert.DeserializeObject<BuildModSettings>(jsonString);

            if (bs.modPath == null || bs.modPath == "") {
                bs.modPath = Path.GetDirectoryName(configPath);
            }

            bs.modPath = UnfungePath(bs.modPath);
            bs.luaFpkdFilesPath = UnfungePath(bs.luaFpkdFilesPath);
            bs.docsPath = UnfungePath(bs.docsPath);
            bs.metadataPath = UnfungePath(bs.metadataPath);
            bs.externalLuaPath = UnfungePath(bs.externalLuaPath);
            bs.modulesLuaPath = UnfungePath(bs.modulesLuaPath);
            bs.modulesInternalPath = UnfungePath(bs.modulesInternalPath);
            bs.makebiteBuildPath = UnfungePath(bs.makebiteBuildPath);
            bs.buildPath = UnfungePath(bs.buildPath);
            bs.gamePath = UnfungePath(bs.gamePath);

            //TODO: unfunge modFolderPaths

            if (bs.gamePath != null && !Directory.Exists(bs.gamePath)) {
                Console.WriteLine($"ERROR: BuildModSettings: Could not find gamePath {bs.gamePath}");
                return;
            }

            if (!Directory.Exists(bs.modPath)) {
                Console.WriteLine($"ERROR: BuildModSettings: Could not find modPath {bs.modPath}");
                return;
            }
   
            
            Environment.CurrentDirectory = bs.modPath;

            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);//UNUSED

            ConsoleTitleAndWriteLine("deleting existing makebite build folder");
            DeleteAndWait(bs.makebiteBuildPath);//tex GOTCHA will complain if open in explorer

            ConsoleTitleAndWriteLine("Copy docs and exes");
            if (bs.copyDocsToBuild) {
                CopyDocsToBuild(bs);
            }

            //CULL supersceded by copyModArchiveFiles
            if (bs.copyLuaFpkdFiles) {
                CopyLuaFpkdFiles(bs);
            }

            if (bs.copyModFolders) {
                Console.WriteLine();
                ConsoleTitleAndWriteLine("copying modFolderPaths folders");
                foreach (string path in bs.modFolderPaths) {
                    if (Directory.Exists(path)) {
                        Console.WriteLine(path);
                        CopyFilesRecursively(new DirectoryInfo(path), new DirectoryInfo(bs.makebiteBuildPath), "", "");
                    }
                }
            }

            if (bs.copyModFileLists) {
                Console.WriteLine();
                CopyModFileLists(bs);
            }

            if (bs.copyModArchiveFiles) {
                Console.WriteLine();
                CopyModArchiveFiles(bs);
            }

            if (bs.copyExternalLuaToInternal) {
                Console.WriteLine();
                CopyExternalLuaToInternal(bs);
            }

            if (bs.copyModulesToInternal) {
                Console.WriteLine();
                CopyModulesToInternal(bs);
            }

            if (bs.copyEngLng2sToOtherLangCodes) {
                CopyEngLng2sToOtherLangCodes(bs);
            }

            if (bs.compileMakebiteBuildFiles) {
                Console.WriteLine();
                CompileMakebiteBuildFiles(bs);
            }

            UpdateMetadata(bs);

            string makebiteMgsvOutputFilePath = $"{bs.makebiteBuildPath}\\mod.mgsv";
            string makeBiteMgsvDestFilePath = $"{bs.buildPath}\\{bs.modFileName}.mgsv";

            if (bs.makeMod) {
                stepWatch.Restart();
                ConsoleTitleAndWriteLine("makebite building " + makebiteMgsvOutputFilePath);
                string toolArgs = "";
                //tex WORKAROUND: currently (smakebite 0.9.2.2) makebite crahes on a qarFile in root (init.lua) if provided with build folder that has a trailing slash
                string makebiteBuildPath = bs.makebiteBuildPath.TrimEnd('\\');

                toolArgs += makebiteBuildPath;
                UseTool(Properties.Settings.Default.makeBitePath, toolArgs);

                if (!File.Exists(makebiteMgsvOutputFilePath)) {
                    Console.WriteLine("Error! Cannot find " + makebiteMgsvOutputFilePath);
                    Console.ReadKey();
                    return;
                }
                else {
                    Console.WriteLine("Copying built msgv");
                    File.Copy(makebiteMgsvOutputFilePath, makeBiteMgsvDestFilePath, true);
                }
                stepWatch.Stop();
                Console.WriteLine($"step in {stepWatch.ElapsedMilliseconds}ms");
            }

            if (bs.uninstallExistingMod) {
                stepWatch.Restart();
                ConsoleTitleAndWriteLine("uninstalling existing mod with snakebite");
                string snakeBiteArgs = "";
                snakeBiteArgs += " -u";//uninstall
                //snakeBiteArgs += " -s";//skip cleanup
                snakeBiteArgs += " -x";//exit when done
                UseTool(Properties.Settings.Default.snakeBitePath, "\"" + bs.Name + "\"" + snakeBiteArgs);
                stepWatch.Stop();
                Console.WriteLine($"step in {stepWatch.ElapsedMilliseconds}ms");
            }

            if (bs.installMod) {
                stepWatch.Restart();
                ConsoleTitleAndWriteLine("installing mod with snakebite");
                string snakeBiteArgs = "";
                snakeBiteArgs += " -i";//install
                snakeBiteArgs += " -c";//no conflict check
                //snakeBiteArgs += " -d";//reset hash
                //snakeBiteArgs += " -s";//skip cleanup
                snakeBiteArgs += " -x";//exit when done
                UseTool(Properties.Settings.Default.snakeBitePath, makebiteMgsvOutputFilePath + snakeBiteArgs);
                stepWatch.Stop();
                Console.WriteLine($"step in {stepWatch.ElapsedMilliseconds}ms");
            }

            runWatch.Stop();
            ConsoleTitleAndWriteLine($"done in {runWatch.ElapsedMilliseconds}ms");
            if (bs.waitEnd) {
                ConsoleTitleAndWriteLine("press any key to exit");
                Console.ReadKey();
            }
        }//Main

        private static void CopyModFileLists(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("copyModFileLists");
            if (bs.copyModFileLists) {
                foreach (var item in bs.modFileLists) {
                    string listFolderPath = item.Key;
                    List<string> fileList = item.Value;
                    Console.WriteLine(listFolderPath);
                    foreach (string listFilePath in fileList) {
                        Console.WriteLine(listFilePath);
                        string filePath = $"{listFolderPath}/{listFilePath}";
                        filePath = UnfungePath(filePath);

                        string fileDest = $"{bs.makebiteBuildPath}/{listFilePath}";
                        fileDest = UnfungePath(fileDest);
                        if (!Directory.Exists(Path.GetDirectoryName(fileDest))) {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDest));
                        }

                        File.Copy(filePath, fileDest, true);
                    }//foreach in  listFolder
                }//foreach in modFileLists
            }//if copyModFileLists
        }//CopyModFileLists

        private static void CopyModArchiveFiles(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("copyModArchiveFiles");
            //REF
            //Dictionary<string, List<string>> > modArchiveFiles = new Dictionary<string,
            //                                                Dictionary<string, List<string>>>() {
            //REF buildSettings
            //"modArchiveFiles": {
            //    //source folder (absolute or modPath relative)
            //    "fpkd-combined-lua/": {
            //        //{file in source: [target archive folder, another target archive]}
            //        "Assets/tpp/level/mission2/init/init_sequence.lua":          ["Assets/tpp/pack/mission2/init/init_fpkd"],
            if (bs.copyModArchiveFiles) {
                foreach (var item in bs.modArchiveFiles) {
                    //REF "fpkd-combined-lua/"
                    string listFolderPath = item.Key;                    
                    Console.WriteLine(listFolderPath);
                    
                    Dictionary<string, List<string>> fileArchiveLists = item.Value;
                    foreach (var fileArchiveList in fileArchiveLists) {
                        //REF "Assets/tpp/level/mission2/init/init_sequence.lua"
                        string inArchiveFilePath = fileArchiveList.Key;
                        Console.WriteLine(inArchiveFilePath);

                        string fileSource = $"{listFolderPath}/{inArchiveFilePath}";
                        fileSource = UnfungePath(fileSource);

                        //REF ["Assets/tpp/pack/mission2/init/init_fpkd"]
                        List<string> archiveFilePaths = fileArchiveList.Value;
                        foreach (string archivePath in archiveFilePaths) {
                            string fileDest = $"{bs.makebiteBuildPath}/{archivePath}/{inArchiveFilePath}";
                            fileDest = UnfungePath(fileDest);
                            if (!Directory.Exists(Path.GetDirectoryName(fileDest))) {
                                Directory.CreateDirectory(Path.GetDirectoryName(fileDest));
                            }

                            File.Copy(fileSource, fileDest, true);
                        }//foreach in archiveFilePaths
                    }//foreach in  listFolder
                }//foreach in modFileLists
            }//if copyModArchiveFiles
        }//CopyModArchiveFiles

        private static void UpdateMetadata(BuildModSettings bs) {
            string makeBiteMetaDataFilePath = UnfungePath($"{bs.metadataPath}\\metadata.xml");
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
        }//UpdateMetadata

        private static void CompileMakebiteBuildFiles(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("Compiling file types in makebiteBuildPath");
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

            Console.WriteLine("Getting makebiteBuildPath files list");
            var makebiteBuildFiles = Directory.GetFiles(bs.makebiteBuildPath, "*.*", SearchOption.AllDirectories);

            Console.WriteLine("Compiling makebiteBuildPath files");
            var taskWatch = new Stopwatch();
            taskWatch.Start();
            var tasks = new List<Task>();
            foreach (var filePath in makebiteBuildFiles) {
                foreach (var item in bs.fileTypesToCompile) {
                    if (filePath.Contains(item.Key) && item.Value == true) {
                        //Console.WriteLine($"filePath: {filePath}");//DEBUGNOW //tex GOTCHA: any logging in loops will dratsically increase the processing time,
                        //don't need to inform the user of progress of something thats only going to take a few seconds, moreso if doing so will double that time.
                        tasks.Add(Task.Run(() => UseTool(fileTypesToCompileToolPaths[item.Key], filePath)));
                        //UseTool(fileTypesToCompileToolPaths[item.Key], filePath);//DEBUGNOW
                    }
                }
            }//foreach filePath
            Task.WaitAll(tasks.ToArray());
            taskWatch.Stop();
            Console.WriteLine($"time to compile: {taskWatch.ElapsedMilliseconds}ms");
        }//CompileMakebiteBuildFiles

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
                    //DEBUGNOW also don't like this, modules will have been blindly copied via modFolderPaths, so kill them
                    string buildExternalAssetsPath = $"{bs.makebiteBuildPath}\\GameDir\\mod\\{subPath}";
                    if (Directory.Exists(buildExternalAssetsPath)) {
                        DeleteAndWait(buildExternalAssetsPath);
                    }
                }
            }
        }

        private static void CopyLuaFpkdFiles(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("CopyLuaFpkdFiles");
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
                        var luaFpkdFilesPath = bs.luaFpkdFilesPath;
                        if (!Path.IsPathRooted(luaFpkdFilesPath)) {
                            luaFpkdFilesPath = Path.Combine(bs.modPath, bs.luaFpkdFilesPath);
                        }
                        string internalPath = buildFileInfo.fullPath.Substring(luaFpkdFilesPath.Length);
                        luaFileDestination = $"{bs.makebiteBuildPath}\\{packPath}\\{internalPath}";
                        luaFileDestination = UnfungePath(luaFileDestination);
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
        }//CopyLuaFpkdFiles

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
                string sourcePath = bs.docsPath;
                CopyFilesRecursively(new DirectoryInfo(sourcePath), new DirectoryInfo(docsDestinationPath), "", "");
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
        //TODO: skip base game lngs
        private static void CopyEngLng2sToOtherLangCodes(BuildModSettings bs) {
            if (!Directory.Exists(bs.makebiteBuildPath)) {
                return;
            }

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

            string langCodeEng = "eng";
            string lngPackPath = @"Assets\tpp\pack\ui\lang";
            string lngInternalPath = @"Assets\tpp\lang\ui\";
            string lngPackName = "lang_default_data";//_eng_fpk"; //TODO: let cfg provide names

            //REF C:\\Projects\\MGS\\build\\infiniteheaven\\makebite]\\Assets\\tpp\\pack\\ui\\lang\\lang_default_data_eng_fpk\\Assets\\tpp\\lang\\ui\\"
            string engLangPackPath = $"{bs.makebiteBuildPath}\\{lngPackPath}\\{lngPackName}_{langCodeEng}_fpk\\{lngInternalPath}";
            engLangPackPath = UnfungePath(engLangPackPath);
            if (!Directory.Exists(engLangPackPath)) {
                Console.WriteLine($"WARNING: CopyEngLng2sToOtherLangCodes could not find lngPackPathDest: {engLangPackPath}");
                return;
            }
            string[] engLangFiles = Directory.GetFiles(engLangPackPath);
            foreach (string langCode in langCodes) {
                foreach (string engLangFile in engLangFiles) {
                    if (Path.GetExtension(engLangFile) != ".xml") {
                        continue;
                    }
                    //REF fileName: ih_general.eng.lng2.xml
                    string langFilePre = Path.GetFileName(engLangFile);

                    string trimString = ".eng.lng2.xml";
                    int trimPos = langFilePre.Length - trimString.Length;
                    langFilePre = langFilePre.Remove(trimPos, trimString.Length);
                    //REF langFilePre="ih_general"

                    string langFileDest = $"{bs.makebiteBuildPath}\\{lngPackPath}\\{lngPackName}_{langCode}_fpk\\{lngInternalPath}{langFilePre}.{langCode}.lng2.xml";
                    langFileDest = UnfungePath(langFileDest);

                    if (!Directory.Exists(Path.GetDirectoryName(langFileDest))) {
                        Directory.CreateDirectory(Path.GetDirectoryName(langFileDest));
                    }

                    bool overwrite = true;
                    File.Copy(engLangFile, langFileDest, overwrite);
                }//foreach langFile

            }//foreach langCode
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

        //CULL
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
        public static string UnfungePath(string path) {
            if (path == null) return null;
            if (path.Length == 0) return null;

            //GOTCHA: paths that start wit / or \ are assumed rooted.
            if (path[0] == '/' || path[0] == '\\') {
                path = path.TrimStart('/');
                path = path.TrimStart('\\');
            }

            path = path.Replace("/", "\\");

            //if (Path.IsPathRooted(path)) {
            //    String unfucked = new Uri(path).LocalPath;//tex outputs backslashes, and uhh what else was i using this for?
            //    return unfucked;
            //} else {
            //    return path;
            //}
            return path;
        }//UnfungePath

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
                    catch (UnauthorizedAccessException e) {
                        Console.WriteLine($"DeleteEmptyDirs({dir})"); 
                        Console.WriteLine(e.Message);
                    }
                    catch (DirectoryNotFoundException e) {
                        Console.WriteLine($"DeleteEmptyDirs({dir})");
                        Console.WriteLine(e.Message);
                    }
                }
            }
            catch (UnauthorizedAccessException e) {
                Console.WriteLine($"DeleteEmptyDirs({dir})");
                Console.WriteLine(e.Message);
            }
        }//DeleteEmptyDirs
    }//class Program
}//namespace mgsv_buildmod
