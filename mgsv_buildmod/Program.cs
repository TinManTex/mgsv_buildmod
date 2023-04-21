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
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace mgsv_buildmod {
    class Program {
        static string titlePrefix = "mgsv_buildmod - ";
        static void Main(string[] args) {
            var runWatch = new Stopwatch();
            runWatch.Start();

            var stepWatch = new Stopwatch();
            //stepWatch.Start();

            Console.Title = titlePrefix;
            var cc = new ConsoleCopy("mgsv_buildmod_log.txt");//tex anything written to Console is also written to log //TODO: actually doesnt capture external process exceptions? I guess I need to capture Console.Error too? 

            if (args.Length == 0) {
                Console.WriteLine("Usage: mgsv_buildmod <buildSettings filePath>");
                WriteDefaultBuildSettingsJson();
                return;
            }//args 0

            ConsoleTitleAndWriteLine("Read BuildSettings");
            string buildSettingsPath = GetPath(args[0]);

            if (buildSettingsPath == null) {
                Console.Write("ERROR: could not find buildSettings path");
                return;
            }
            buildSettingsPath = UnfungePath(buildSettingsPath);

            //TODO: test path exists

            string jsonString = File.ReadAllText(buildSettingsPath);
            //tex in case you want to go with System.Text.Json (and its millions of dlls)
            //var serializeOptions = new JsonSerializerOptions {
            //    ReadCommentHandling = JsonCommentHandling.Skip,
            //    AllowTrailingCommas = true,
            //    //PropertyNameCaseInsensitive = true,
            //    //PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //    IncludeFields = true,
            //};
            BuildModSettings bs = JsonConvert.DeserializeObject<BuildModSettings>(jsonString);

            if (bs.modPath == null || bs.modPath == "") {
                bs.modPath = Path.GetDirectoryName(buildSettingsPath);
            }

            bs.modPath = UnfungePath(bs.modPath);
            bs.docsPath = UnfungePath(bs.docsPath);
            bs.metadataPath = UnfungePath(bs.metadataPath);
            bs.externalLuaPath = UnfungePath(bs.externalLuaPath);
            bs.modulesLuaPath = UnfungePath(bs.modulesLuaPath);
            bs.modulesInternalPath = UnfungePath(bs.modulesInternalPath);
            bs.makebiteBuildPath = UnfungePath(bs.makebiteBuildPath);
            bs.buildPath = UnfungePath(bs.buildPath);
            bs.gamePath = UnfungePath(bs.gamePath);

            //TODO: unfunge modFolders, modFiles, modArchiveFiles

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

            //buildmod

            ConsoleTitleAndWriteLine("deleting existing makebite build folder");
            DeleteAndWait(bs.makebiteBuildPath);//tex GOTCHA will complain if open in explorer

            ConsoleTitleAndWriteLine("Copy docs and exes");
            if (bs.copyDocsToBuild) {
                CopyDocsToBuild(bs);
            }

            Console.WriteLine();
            stepWatch.Restart();
            CopyModFiles(bs);
            stepWatch.Stop();
            Console.WriteLine($"step in {stepWatch.ElapsedMilliseconds}ms");

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

            if (bs.addLuaFileVersions) {
                Console.WriteLine();
                AddLuaFileVersions(bs);
            }

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

            //done

            runWatch.Stop();
            ConsoleTitleAndWriteLine($"done in {runWatch.ElapsedMilliseconds}ms");
            if (bs.waitEnd) {
                ConsoleTitleAndWriteLine("press any key to exit");
                Console.ReadKey();
            }
        }//Main

        //tex copies all files in mod folder(s), to makebitebuildpath
        //or specific file(s) in mod folder, to makebitebuildpath
        //or specific file(s) in mod folder to target folder(s), in makebitebuildpath
        private static void CopyModFiles(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("CopyModFiles");
            //REF
            //Dictionary<string, List<string>> > modArchiveFiles = new Dictionary<string,
            //                                                Dictionary<string, List<string>>>() {
            //REF buildSettings json bs.manifest
            //"fpkd-combined-lua/": {
            //  "Assets/tpp/level/mission2/init/init_sequence.lua":
            //      ["Assets/tpp/pack/mission2/init/init_fpkd"],
            // ==
            //'modFolderPath': {// absolute path or relative to modPath
            //  //entries optional, no entries = copy whole folder to makeBiteBuildPath
            //  'file in source':
            //      [
            //          //target entries optional, no entries = copy to makeBiteBuildPath root
            //          'target folder (eg archive folder)',
            //          'another target folder'
            //          ...
            //       ],
            //  ...
            //}

            foreach (var folderFileList in bs.modFiles) {
                // REF
                // "data1_dat-lua-ih/": {},
                // folderFileList.Key : folderFileList.Value,   
                // modFolderPath : fileAndTargetFolderPaths,
                string modFolderPath = folderFileList.Key;
                Dictionary<string, List<string>> fileAndTargetFolderPaths = folderFileList.Value;

                Console.WriteLine(modFolderPath);

                //REF "data1_dat-lua-ih/": {},
                //tex empty object means copy whole folder
                if (fileAndTargetFolderPaths.Count == 0) {
                    if (Directory.Exists(modFolderPath)) {
                        CopyFilesRecursively(new DirectoryInfo(modFolderPath), new DirectoryInfo(bs.makebiteBuildPath), "", "");
                    }
                    continue;
                }

                foreach (var fileAndTargetList in fileAndTargetFolderPaths) {
                    //REF
                    //"Assets/tpp/script/lib/Tpp.lua": [],
                    //"Assets/tpp/level/mission2/init/init_sequence.lua": ["Assets/tpp/pack/mission2/init/init_fpkd"],//tex target folderpaths used to put in archive folder(s) 
                    // fileAndTargetList.Key : fileAndTargetList.Value,   
                    // folderRelativeFilePath : targetFolderPaths,
                    string folderRelativeFilePath = fileAndTargetList.Key;
                    List<string> targetFolderPaths = fileAndTargetList.Value;

                    Console.WriteLine(folderRelativeFilePath);

                    //REF "Assets/tpp/script/lib/Tpp.lua": [],
                    //tex empty object means copy to modPath root
                    if (targetFolderPaths.Count == 0) {
                        string filePath = $"{modFolderPath}/{folderRelativeFilePath}";
                        filePath = UnfungePath(filePath);

                        string fileDest = $"{bs.makebiteBuildPath}/{folderRelativeFilePath}";
                        fileDest = UnfungePath(fileDest);
                        if (!Directory.Exists(Path.GetDirectoryName(fileDest))) {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDest));
                        }

                        File.Copy(filePath, fileDest, true);
                        continue;
                    }//if targetFolderPaths == 0

                    //REF: "Assets/tpp/level/mission2/init/init_sequence.lua":          ["Assets/tpp/pack/mission2/init/init_fpkd",...],
                    foreach (string targetPath in targetFolderPaths) {
                        string fileDest = $"{bs.makebiteBuildPath}/{targetPath}/{folderRelativeFilePath}";
                        fileDest = UnfungePath(fileDest);
                        if (!Directory.Exists(Path.GetDirectoryName(fileDest))) {
                            Directory.CreateDirectory(Path.GetDirectoryName(fileDest));
                        }
                        string fileSource = $"{modFolderPath}/{folderRelativeFilePath}";
                        fileSource = UnfungePath(fileSource);
                        File.Copy(fileSource, fileDest, true);
                    }//foreach in targetFolderPaths
                }//foreach in  fileAndTargetFolderPaths
            }//foreach in modFiles
        }//CopyModFiles

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

        //Adds this.modId and this.modVersion to makebiteBuild .lua files, used by IH for error checking
        //GOTCHA: looks specifically for 'local this'
        private static void AddLuaFileVersions(BuildModSettings bs) {
            ConsoleTitleAndWriteLine("AddLuaFileVersions");
            Console.WriteLine("Getting lua files list");

            string modIdLine=$"modId=\"{bs.Name}\"";
            string modVersionLine = $"version=\"{bs.Version}\"";

            var makebiteBuildFiles = Directory.GetFiles(bs.makebiteBuildPath, "*.lua", SearchOption.AllDirectories);

            Console.WriteLine("Updating data");
            var taskWatch = new Stopwatch();
            taskWatch.Start();
            //var tasks = new List<Task>();
            foreach (var filePath in makebiteBuildFiles) {
                //DEBUGNOW tasks.Add(Task.Run(() => UseTool(fileTypesToCompileToolPaths[item.Key], filePath)));
                //tex WORKAROUND DEBUGNOW think this through
                if (filePath.Contains("InfCore.lua")) {
                    continue;
                }

                var parts = filePath.Split('\\');
                string fileName = parts[parts.Length-1];

                int extPos = fileName.IndexOf(".lua");
                string moduleName = fileName.Substring(0,extPos);


                string[] lines = File.ReadAllLines(filePath);
                using (StreamWriter writer = new StreamWriter(filePath)) {
                    bool foundInsertPoint = false;
                    foreach (string line in lines) {
                        writer.WriteLine(line);
                        //tex DEBUGNOW  mbdvc_map luas have the bad practice of just declaring self as global
                        if ((line.IndexOf("local this") != -1 || line.IndexOf($"local {moduleName}") != -1) && line.IndexOf("=") != -1) {
                            foundInsertPoint = true;
                            if (line.IndexOf("{}") != -1) {
                                writer.WriteLine("--added by mgsv_buildmod >");
                                writer.WriteLine($"this.{modIdLine}");
                                writer.WriteLine($"this.{modVersionLine}");
                                writer.WriteLine("--<");
                            } else {
                                if (line.IndexOf("{") != -1) {
                                    writer.WriteLine("--added by mgsv_buildmod >");
                                    writer.WriteLine($"{modIdLine},");
                                    writer.WriteLine($"{modVersionLine},");
                                    writer.WriteLine("--<");
                                }
                                else {
                                    //tex TODO: deal with hits to this
                                    Console.WriteLine($"WARNING: could not parse {line} in {filePath}");
                                }
                            }//if {} or {
                        }//if line local this or moduleName
                    }//foreach lines
                    if (!foundInsertPoint) {
                        //tex TODO: deal with hits to this
                        Console.WriteLine($"WARNING: could not find 'local this' in {filePath}");
                    }
                }//using StreamWriter
            }//foreach filePath
            //Task.WaitAll(tasks.ToArray());
            taskWatch.Stop();
            Console.WriteLine($"time to update lua files: {taskWatch.ElapsedMilliseconds}ms");
        }//AddLuaFileVersions

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
                    //DEBUGNOW TODO also don't like this, modules will have been blindly copied via modFolders, so kill them
                    string buildExternalAssetsPath = $"{bs.makebiteBuildPath}\\GameDir\\mod\\{subPath}";
                    if (Directory.Exists(buildExternalAssetsPath)) {
                        DeleteAndWait(buildExternalAssetsPath);
                    }
                }
            }
        }

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

        private static void WriteDefaultBuildSettingsJson() {
            var buildSettings = new BuildModSettings();
            string jsonOutPath = @".\buildSettings-example.buildmod";
            Console.WriteLine($"Writing default buildSettings to {jsonOutPath}");
            JsonSerializerSettings serializeSettings = new JsonSerializerSettings();
            serializeSettings.Formatting = Newtonsoft.Json.Formatting.Indented;
            string jsonStringOut = JsonConvert.SerializeObject(buildSettings, serializeSettings);

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
            path = path.Replace("\\\\", "\\");

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
