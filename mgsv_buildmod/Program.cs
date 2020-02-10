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

namespace mgsv_buildmod {
    class Program {
        static string projectPath = ConfigurationManager.AppSettings.Get("projectPath");
        static string luaSubPath = ConfigurationManager.AppSettings.Get("luaSubPath");
        static string docsSubPath = ConfigurationManager.AppSettings.Get("docsSubPath");
        static string luaDataFilesSubpath = ConfigurationManager.AppSettings.Get("luaDataFilesSubpath");
        static string luaPackFilesSubPath = ConfigurationManager.AppSettings.Get("luaPackFilesSubPath");
        static string projectPackFilesSubPath = ConfigurationManager.AppSettings.Get("packSubPath");
        static string customPackFilesSubPath = ConfigurationManager.AppSettings.Get("customPackSubPath");
        static string otherMgsvsSubPath = ConfigurationManager.AppSettings.Get("otherMgsvsSubPath");
        static string externalLuaSubPath = ConfigurationManager.AppSettings.Get("externalLuaSubPath");

        static string luaPath = projectPath + luaSubPath;
        static string luaDataFilesPathFull = luaPath + luaDataFilesSubpath;
        static string luaPackFilesPathFull = luaPath + luaPackFilesSubPath;
        static string projectPackFilesPathFull = projectPath + projectPackFilesSubPath;
        static string customPackFilesPathFull = projectPath + customPackFilesSubPath;
        static string otherMgsvsPathFull = projectPath + otherMgsvsSubPath;

        static string docsPathFull = projectPath + docsSubPath;

        static string externalLuaPathFull = projectPath + externalLuaSubPath;
        static string externalLuaPathFullRelease = projectPath + externalLuaSubPath + "Release";

        static string makebiteBuildPath = ConfigurationManager.AppSettings.Get("modBuildPath");
        static string modPackFilesSubpath = @"\packFiles";

        static string buildFolder = ConfigurationManager.AppSettings.Get("buildFolder");

        static string gzsToolPath = ConfigurationManager.AppSettings.Get("gzsToolPath");
        static string langToolPath = ConfigurationManager.AppSettings.Get("langToolPath");
        static string foxToolPath = ConfigurationManager.AppSettings.Get("foxToolPath");
        static string subpToolPath = ConfigurationManager.AppSettings.Get("subpToolPath");
        static string makeBitePath = ConfigurationManager.AppSettings.Get("makeBitePath");
        static string snakeBitePath = ConfigurationManager.AppSettings.Get("snakeBitePath");

        static string gamePath = ConfigurationManager.AppSettings.Get("gamePath");

        static string cleanDataPath = ConfigurationManager.AppSettings.Get("cleanDataPath");
        static string gameArchiveSubpath = @"\master";
        static string targetGamePatchFolder0Subpath = @"\0";
        //static string patchArchive00Subpath = "\\00";

        static string targetGameArchiveSubpath = "\\01";


        //static string lngPackName = "";

        // TODO: just point to sperate file
        static string modVersionDefault = "rXXX";
        static string modFileName = "Infinite Heaven";
        static string readMeName = "Infinite Heaven Readme.txt";

        public class BuildFileInfo {
            public string fullPath = "";
            public bool doBuild = false;
            //  public string originalQar = "";
            public string filePath = "";
            public string packPath = "";
            public string qarPath = "";
        }

        public static string UnfungePath(string path) {
            String unfucked = new Uri(path).LocalPath;
            return unfucked;
        }

        public delegate void ProcessFileDelegate(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);

        static void Main(string[] args) {
            bool waitEnd = true;
            bool makeMod = true;
            bool installMod = true;


            bool copyFpkLoose = true;

            //bool wantCleanFpks = false;

            bool buildLng2s = true;
            bool buildSubps = false;
            bool buildFox2s = true;
            bool copyProcessFolders = true;
            bool copyExternalLua = true;

            bool buildCustomFpks = false;

            bool release = false;

            foreach (string s in args) {
                if (s == "-release") {
                    release = true;
                }
            }

            if (release) {
                Console.WriteLine("doing release build");
            }

            //tex folders to run various tools over
            List<string> processFolders = new List<string>();
            //TODO: load from txt file

            processFolders.Add(projectPackFilesPathFull);

            //tex testexternal
            if (release == false) {
                processFolders.Add(customPackFilesPathFull);
            }


            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);

            //tex start actually doing shit

            Console.WriteLine("deleting existing makebite build folder");
            DeleteAndWait(makebiteBuildPath);//tex GOTCHA will complain if open in explorer

            string docsDestinationPath = makebiteBuildPath + @"\GameDir\mod\docs\";
            if (Directory.Exists(docsPathFull)) {
                Console.WriteLine("copying docs files");
                if (!Directory.Exists(docsDestinationPath)) {
                    Directory.CreateDirectory(docsDestinationPath);
                }
                CopyFilesRecursively(new DirectoryInfo(docsPathFull), new DirectoryInfo(docsDestinationPath), "", "");   
            }

            //tex get version from readme, superhax i know
            string modVersion = modVersionDefault;
            string readmePathFull = docsDestinationPath + readMeName;
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
            Console.WriteLine("got modVersion:{0}", modVersion);

            Console.WriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            TraverseTree(luaPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);
            //tex allow text files as subsituted
            TraverseTree(luaPath, ".txt", ReadLuaBuildInfoProcess, ref modFilesInfo);
            if (modFilesInfo.Count == 0) {
                Console.WriteLine("no mod files found");
                return;
            }

            //tex figure out qar build folder
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    if (IsForFpk(buildFileInfo)) {
                        buildFileInfo.qarPath = "\\" + Path.GetFileName(buildFileInfo.packPath);
                        buildFileInfo.qarPath = buildFileInfo.qarPath.Replace(".", "_");
                    } else {
                        buildFileInfo.qarPath = targetGameArchiveSubpath;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("building list of fpks used");
            Dictionary<string, List<BuildFileInfo>> fpks = new Dictionary<string, List<BuildFileInfo>>();
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    if (IsForFpk(buildFileInfo)) {
                        List<BuildFileInfo> filesForFpk;
                        if (!fpks.TryGetValue(buildFileInfo.packPath, out filesForFpk)) {
                            filesForFpk = new List<BuildFileInfo>();
                            fpks.Add(buildFileInfo.packPath, filesForFpk);
                            Console.WriteLine("fpklistadd:" + buildFileInfo.packPath);
                        }

                        filesForFpk.Add(buildFileInfo);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("copying mod files to build folder");
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    string buildBaseFolder = buildFolder;
                    string luaFileDestination = makebiteBuildPath + buildFileInfo.filePath;
                    if (IsForFpk(buildFileInfo)) {
                        string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;
                        luaFileDestination = cleanPackFolderBuildPath + buildFileInfo.filePath;
                        if (copyFpkLoose) {
                            luaFileDestination = makebiteBuildPath + buildFileInfo.packPath;
                            luaFileDestination = luaFileDestination.Replace(".", "_");
                            luaFileDestination += buildFileInfo.filePath;
                        }
                    }
                    Console.WriteLine(buildFileInfo.filePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(luaFileDestination));
                    File.Copy(buildFileInfo.fullPath, luaFileDestination, true);
                }
            }

            if (buildLng2s) {
                Console.WriteLine("building lng2s");
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
                //for .lng2.xml files in processfolderpath > lngPackPath + lang_default_data_eng_fpk + lngInternalPath
                //strip filename of .eng.lng2.xml?

                string lngPackPathTotal = @"\Assets\tpp\pack\ui\lang\lang_default_data_eng_fpk\Assets\tpp\lang\ui\";

                foreach (string processFolderPath in processFolders) {
                    if (!Directory.Exists(processFolderPath)) {
                        continue;
                    }
                    string totalPath = processFolderPath + lngPackPathTotal;
                    if (!Directory.Exists(processFolderPath)) {
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
                            string langFileEng = processFolderPath + lngPackPath + @"\lang_default_data_eng_fpk" + lngInternalPath + langFilePre + "." + "eng" + ".lng2.xml";
                            langFileEng = UnfungePath(langFileEng);


                            string langFileDest = processFolderPath + lngPackPath + @"\lang_default_data_" + langCode + "_fpk" + lngInternalPath + langFilePre + "." + langCode + ".lng2.xml";
                            langFileDest = UnfungePath(langFileDest);

                            File.Copy(langFileEng, langFileDest, true);
                        }

                    }

                }


                foreach (string path in processFolders) {
                    if (Directory.Exists(path)) {
                        TraverseTree(path, ".xml", RunLangToolProcess, ref modFilesInfo);

                        //TraverseTree(path, ".xml", DeleteLng2XmlProcess, ref modFilesInfo);
                    }
                }
            }

            if (buildFox2s) {
                Console.WriteLine("building fox2s");
                foreach (string path in processFolders) {
                    if (Directory.Exists(path)) {
                        TraverseTree(path, ".xml", RunFoxToolProcess, ref modFilesInfo);
                    }
                }
            }

            if (buildSubps) {
                Console.WriteLine("building subps");
                foreach (string path in processFolders) {
                    if (Directory.Exists(path)) {
                        TraverseTree(path, ".xml", RunSubpToolProcess, ref modFilesInfo);
                    }
                }
            }

            if (copyProcessFolders) {
                Console.WriteLine();
                Console.WriteLine("copying processFolders folder");
                foreach (string path in processFolders) {
                    if (Directory.Exists(path)) {
                        CopyFilesRecursively(new DirectoryInfo(path), new DirectoryInfo(makebiteBuildPath), "", ".xml");
                    }
                }
            }

            if (copyExternalLua && release) {
                Console.WriteLine();
                Console.WriteLine("copying ExternalLua folder");

                if (Directory.Exists(externalLuaPathFullRelease)) {
                    string destPath = makebiteBuildPath + @"\GameDir\mod\";
                    Directory.CreateDirectory(destPath);
                    CopyFilesRecursively(new DirectoryInfo(externalLuaPathFullRelease), new DirectoryInfo(destPath), "", "");
                }
            }


            string snakeBiteMgvsDestFilePath = buildFolder + "\\" + modFileName + ".mgsv";
            string snakeBiteMgvsFilePath = makebiteBuildPath + "\\" + "mod.mgsv";
            snakeBiteMgvsFilePath = UnfungePath(snakeBiteMgvsFilePath);
            string snakeBiteMetaDataFilePath = buildFolder + "\\" + "metadata.xml";
            string snakeBiteMetaDataDestFilePath = makebiteBuildPath + "\\" + "metadata.xml";
            string snakeBiteReadMeFilePath = docsPathFull + "\\" + readMeName;
            snakeBiteReadMeFilePath = UnfungePath(snakeBiteReadMeFilePath);
            string snakeBiteReadMeDestFilePath = makebiteBuildPath + "\\" + "readme.txt";
            snakeBiteReadMeDestFilePath = UnfungePath(snakeBiteReadMeDestFilePath);



            Console.WriteLine("Updating metadata version tag");
            if (File.Exists(snakeBiteMetaDataFilePath)) {
                XDocument xmlFile = XDocument.Load(snakeBiteMetaDataFilePath);

                var query = from c in xmlFile.Elements("ModEntry")
                            select c;

                foreach (XElement entry in query) {
                    entry.Attribute("Version").Value = modVersion;
                }

                xmlFile.Save(snakeBiteMetaDataFilePath);
            }

            Console.WriteLine("Copying mod metadata");
            if (File.Exists(snakeBiteMetaDataFilePath)) {
                File.Copy(snakeBiteMetaDataFilePath, snakeBiteMetaDataDestFilePath, true);
            }

            Console.WriteLine("Copying mod readme");
            if (File.Exists(snakeBiteReadMeFilePath)) {
                File.Copy(snakeBiteReadMeFilePath, snakeBiteReadMeDestFilePath, true);
            }

            if (release == false) {
                Console.WriteLine("Deleting sbmods.xml");
                string sbmodFilePath = gamePath + "\\snakebite.xml";
                string sbmodCleanFilePath = gamePath + "\\snakebiteclean.xml";
                if (File.Exists(sbmodFilePath)) {
                    File.Copy(sbmodCleanFilePath, sbmodFilePath, true);
                }

                string patchPath = gamePath + gameArchiveSubpath + targetGamePatchFolder0Subpath;
                string sbclean00PathFull = patchPath + "\\" + "00.dat.sbclean";
                string target00 = patchPath + "\\00.dat";

                Console.WriteLine("restoring 00.dat");
                if (File.Exists(sbclean00PathFull)) {
                    File.Copy(sbclean00PathFull, target00, true);
                }
            }


            if (makeMod) {
                Console.WriteLine("makebite building " + snakeBiteMgvsFilePath);
                string toolArgs = "";
                toolArgs += makebiteBuildPath;
                UseTool(makeBitePath, toolArgs);
            } else {
                Console.WriteLine("done.");
                Console.ReadKey();
            }

            if (!File.Exists(snakeBiteMgvsFilePath)) {
                Console.WriteLine("Error! Cannot find " + snakeBiteMgvsFilePath);
                Console.ReadKey();
                return;
            } else {
                Console.WriteLine("Copying built msgv");
                File.Copy(snakeBiteMgvsFilePath, snakeBiteMgvsDestFilePath, true);
                string lastBuildPath = projectPath + "\\" + modFileName + ".mgsv";
                File.Copy(snakeBiteMgvsFilePath, lastBuildPath, true);
            }


            if (release == false) {
                Console.WriteLine("running snakebite on othermods");
                TraverseTree(otherMgsvsPathFull, ".mgsv", RunSnakeBiteProcess, ref modFilesInfo);

                if (installMod) {
                    Console.WriteLine("running snakebite on mod");
                    string snakeBiteMgsvPath = "\"" + snakeBiteMgvsFilePath + "\"";
                    string snakeBiteArgs = "";
                    snakeBiteArgs += " -i";//install
                    snakeBiteArgs += " -c";//no conflict check
                    snakeBiteArgs += " -d";//reset hash
                    snakeBiteArgs += " -s";//skip cleanup
                    snakeBiteArgs += " -x";//exit
                    UseTool(snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
                }
            }

            Console.WriteLine("done");
            if (waitEnd) {
                Console.ReadKey();
            }
        }

        private static void DeleteAndWait(string path) {
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
            while (Directory.Exists(path)) {
                Thread.Sleep(100);
            }
        }

        /*
        private static void CleanAllFpks(Dictionary<string, List<BuildFileInfo>> fpks) {
            foreach (var item in fpks) {
                BuildFileInfo buildFileInfo = item.Value[0];

                string cleanPackFileName = Path.GetFileName(item.Key);
                string cleanPackFileBuildPath = buildFolder + modPackFilesSubpath + "\\" + cleanPackFileName;
                string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;

                //tex ASSUMPTION: only one patch .dat
                string cleanPackFilePatchSourcePath = cleanDataPath + targetGamePatchFolder0Subpath + patchArchive00Subpath + buildFileInfo.packPath;
                string cleanPackFileSourcePath = cleanDataPath + "\\" + buildFileInfo.originalQar + buildFileInfo.packPath;

                Console.WriteLine("deleting buildFolder " + cleanPackFileName + " and folder");

                if (Directory.Exists(cleanPackFolderBuildPath)) {
                    Directory.Delete(cleanPackFolderBuildPath, true);
                }
                if (File.Exists(cleanPackFileBuildPath)) {
                    File.Delete(cleanPackFileBuildPath);
                }

                Console.WriteLine();
                Console.WriteLine("finding clean fpk in cleanDataPath");

                string foundPath = "";
                if (File.Exists(cleanPackFilePatchSourcePath)) {
                    foundPath = cleanPackFilePatchSourcePath;
                    Console.WriteLine("found in patch path");
                } else if (File.Exists(cleanPackFileSourcePath)) {
                    foundPath = cleanPackFileSourcePath;
                    Console.WriteLine("found in base path");
                }

                if (foundPath == "") {
                    // if (!allowExtractData) {
                    Console.WriteLine("could not find extracted pack " + cleanPackFileSourcePath);
                    break;
                    //  } else {
                    //tex TODO: find chunk dat
                    // extract chunk dat
                    // try and find fpk again
                    //  }
                } else {
                    Console.WriteLine();
                    Console.WriteLine("copying clean pack " + cleanPackFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(cleanPackFileBuildPath));
                    File.Copy(foundPath, cleanPackFileBuildPath, true);

                    Console.WriteLine();
                    Console.WriteLine("extracting " + cleanPackFileName);
                    string output = UsePackTool(cleanPackFileBuildPath, false);
                    Console.WriteLine(output);
                }
            }
        }
        */

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
                } catch (UnauthorizedAccessException e) {
                    Console.WriteLine(e.Message);
                    continue;
                } catch (System.IO.DirectoryNotFoundException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }

                string[] files = null;
                try {
                    files = System.IO.Directory.GetFiles(currentDir);
                } catch (UnauthorizedAccessException e) {
                    Console.WriteLine(e.Message);
                    continue;
                } catch (System.IO.DirectoryNotFoundException e) {
                    Console.WriteLine(e.Message);
                    continue;
                }

                foreach (string file in files) {
                    try {

                        System.IO.FileInfo fi = new System.IO.FileInfo(file);
                        if (fi.Extension == extension) {
                            processFile(fi, ref buildFileInfoList);
                        }
                    } catch (System.IO.FileNotFoundException e) {
                        Console.WriteLine(e.Message);
                        continue;
                    }
                }

                foreach (string str in subDirs)
                    dirs.Push(str);
            }
        }

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
                    case "FILEPATH:":
                        System.Console.WriteLine(line);
                        buildFileInfo.filePath = attribValue;
                        break;
                    case "PACKPATH:":
                        System.Console.WriteLine(line);
                        buildFileInfo.packPath = attribValue;
                        break;
                }

                if (fileInfo.Extension == ".lua") {
                    buildFileInfo.fullPath = fileInfo.FullName;
                    // tex find path relative to projectpath?
                    // find 'data' 'pack' - set type
                    // what about orginail archive? useful to more quickly find the inf/tool xml
                    // figure out filepath /root (ie \archive\tpp\blah

                    if (fileInfo.FullName.Contains(luaDataFilesSubpath)) {
                        // buildFileInfo.originalQar = "data1_dat";

                        buildFileInfo.filePath = fileInfo.FullName.Substring(luaDataFilesPathFull.Length);
                    } else if (fileInfo.FullName.Contains(luaPackFilesSubPath)) {
                        buildFileInfo.filePath = fileInfo.FullName.Substring(luaPackFilesPathFull.Length);
                    }
                } else {
                    if (buildFileInfo.filePath != "") {
                        buildFileInfo.fullPath = fileInfo.DirectoryName + buildFileInfo.filePath;
                    } else {
                        Console.WriteLine("cannot find FILEPATH: for " + fileInfo.FullName);
                    }
                }
            }
        }

        public static void RunLangToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".lng2.xml")) {
                return;
            }

            UseTool(langToolPath, fileInfo.FullName);
        }

        public static void DeleteLng2XmlProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (fileInfo.Name.Contains(".lng2.xml")) {
                fileInfo.Delete();
            }
        }

        public static void RunFoxToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".fox2.xml") && !fileInfo.Name.Contains(".sdf.xml") && !fileInfo.Name.Contains(".parts.xml")) {
                return;
            }

            UseTool(foxToolPath, fileInfo.FullName);
        }

        public static void RunSubpToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".xml")) {
                return;
            }

            UseTool(subpToolPath, fileInfo.FullName);
        }

        public static void RunGzToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".fpk.xml") && !fileInfo.Name.Contains(".fpkd.xml")) {
                return;
            }

            UseTool(gzsToolPath, fileInfo.FullName);
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

            UseTool(snakeBitePath, snakeBiteMgsvPath + snakeBiteArgs);
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
                    } catch (UnauthorizedAccessException) { } catch (DirectoryNotFoundException) { }
                }
            } catch (UnauthorizedAccessException) { }
        }
    }
}
