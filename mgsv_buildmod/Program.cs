using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Configuration;
using System.Collections.Specialized;
using System.Xml.Linq;
using System.Threading;

namespace mgsv_buildmod {
    class Program {
        static string projectPath = ConfigurationManager.AppSettings.Get("projectPath");
        static string projectDataFilesSubpath = @"\Data";//tex in respect to project path
        static string projectPackFilesSubPath = @"\fpkcombined";
        static string projectCustomPackFilesSubPath = @"\modfpk";//@"\customfpk";


        static string projectDataFilesPathFull = projectPath + projectDataFilesSubpath;
        static string projectPackFilesPathFull = projectPath + projectPackFilesSubPath;
        static string projectCustomPackFilesPathFull = projectPath + projectCustomPackFilesSubPath;

        static string modBuildPath = ConfigurationManager.AppSettings.Get("modBuildPath");
        static string modPackFilesSubpath = @"\packFiles";

        static string buildFolder = ConfigurationManager.AppSettings.Get("buildFolder");

        static string gzsToolPath = ConfigurationManager.AppSettings.Get("gzsToolPath");
        static string langToolPath = ConfigurationManager.AppSettings.Get("langToolPath");
        static string foxToolPath = ConfigurationManager.AppSettings.Get("foxToolPath");
        //static string makeBitePath = ConfigurationManager.AppSettings.Get("makeBitePath");
        //static string snakeBitePath = ConfigurationManager.AppSettings.Get("snakeBitePath");

        //static string makeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite_0.61\makebite.exe";
        //static string snakeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite_0.61\snakebite.exe";

        //static string makeBitePath = @"D:\Games\[Utils]\[MGSV]\SnakeBite\makebite.exe";
        static string snakeBitePath = @"D:\Games\[Utils]\[MGSV]\SnakeBite\snakebite.exe";

        static string makeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite-master\makebite\bin\Debug\makebite.exe";
        //static string snakeBitePath = @"D:\Games\[Utils]\[MGSV]\SnakeBite\snakebite.exe";

        //static string makeBitePath = @"D:\Projects\MGS\snakebite\SB0511\makebite.exe";
        //static string snakeBitePath = @"D:\Projects\MGS\snakebite\SB0511\snakebite.exe";

        //static string snakeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite-0.5.11src00dat\SnakeBite\bin\Debug\snakebite.exe";
        //static string makeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite-0.8src\makebite\bin\Debug\makebite.exe";
        //static string snakeBitePath = @"D:\Projects\MGS\snakebite\SnakeBite-0.8src\SnakeBite\bin\Debug\snakebite.exe";



        static string langFilePath = @"D:\Projects\MGS\InfiniteHeavenLDT\InfiniteHeaven\src\Data\Assets\tpp\script\lib\InfLang.lua";

        static string gamePath = ConfigurationManager.AppSettings.Get("gamePath");

        static string cleanDataPath = ConfigurationManager.AppSettings.Get("cleanDataPath");
        static string gameArchiveSubpath = @"\master";
        static string targetGamePatchFolder0Subpath = @"\0";
        //static string patchArchive00Subpath = "\\00";

        static string targetGameArchiveSubpath = "\\01";

        // TODO: just point to sperate file
        static string modVersionDefault = "rXXX";
        static string modFileName = "Infinite Heaven";
        static string readMeName = "Infinite Heaven Readme.txt";

        static string[] langCodes = { "eng", "fre", "ger", "ita", "jpn", "por", "rus", "spa" };

        public class BuildFileInfo {
            public string fullPath = "";
            public bool doBuild = false;
            //  public string originalQar = "";
            public string filePath = "";
            public string packPath = "";
            public string qarPath = "";
        }

        public static string UnfuckPath(string path) {
            String unfucked = new Uri(path).LocalPath;
            return unfucked;
        }

        public delegate void ProcessFileDelegate(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);

        static void Main(string[] args) {
            bool waitEnd = true;
            bool makeMod = true;
            bool installMod = true;

            bool quickBuild = false;

            bool copyFpkLoose = true;

            //bool wantCleanFpks = false;

            bool buildLng2s = false;

            bool buildFox2s = true;
            bool copyCustomFpks = true;
            bool buildCustomFpkss = false;

            bool customfpkfix = false;//tex till the file order issue for fpkd+fox2 can be figured out.

            if (args.Length != 0) {
                foreach (string arg in args) {
                    if (arg == "quickbuild") {
                        Console.WriteLine("running in quickbuild");
                        quickBuild = true;
                    }
                }
            }


            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);


            //tex get version from readme, superhax i know
            string modVersion = modVersionDefault;
            string readmePathFull = buildFolder + "\\" + readMeName;
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

            Console.WriteLine("copying lang file");
            string langFileDest = buildFolder + "\\InfLang.txt";
            File.Copy(langFilePath, langFileDest,true);

            Console.WriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            TraverseTree(projectPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);

            TraverseTree(projectPath, ".txt", ReadLuaBuildInfoProcess, ref modFilesInfo);
            if (modFilesInfo.Count == 0) {
                Console.WriteLine("no mod files found");
                return;
            }

            Console.WriteLine("deleting mod build folder");
            DeleteAndWait(modBuildPath);

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

            /* CULL:
            if (wantCleanFpks) {
                Console.WriteLine();
                Console.WriteLine("wantCleanFpks");
                if (fpks.Count == 0) {
                    Console.WriteLine("no fpks needed to process");
                }

                CleanAllFpks(fpks);
            }
            */

            Console.WriteLine();
            Console.WriteLine("copying mod files to build folder");
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    string buildBaseFolder = buildFolder;
                    string luaFileDestination = modBuildPath + buildFileInfo.filePath;
                    if (IsForFpk(buildFileInfo)) {
                        //string cleanPackFileName = Path.GetFileName(buildFileInfo.filePath);
                        //string cleanPackFileBuildPath = buildFolder + modPackFilesSubpath + "\\" + cleanPackFileName;
                        string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;
                        luaFileDestination = cleanPackFolderBuildPath + buildFileInfo.filePath;
                        //tex snakebite future style?
                        if (copyFpkLoose) {
                            luaFileDestination = modBuildPath + buildFileInfo.packPath;
                            luaFileDestination = luaFileDestination.Replace(".", "_");
                            luaFileDestination += buildFileInfo.filePath;
                        }
                    }
                    Console.WriteLine(buildFileInfo.filePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(luaFileDestination));
                    File.Copy(buildFileInfo.fullPath, luaFileDestination, true);

                }
            }

            if (!copyFpkLoose) {
                Console.WriteLine();
                Console.WriteLine("copying fpk folder");
                if (fpks.Count == 0) {
                    Console.WriteLine("no fpks needed to process");
                }

                foreach (var item in fpks) {
                    BuildFileInfo buildFileInfo = item.Value[0];


                    string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;

                    string packQarFolder = modBuildPath + buildFileInfo.packPath;
                    packQarFolder = packQarFolder.Replace(".", "_");

                    if (!Directory.Exists(cleanPackFolderBuildPath)) {
                        Console.WriteLine("cannot find fpk build folder " + cleanPackFolderBuildPath);
                        return;
                    }

                    Console.WriteLine();
                    Console.WriteLine("copying pack folder" + buildFileInfo.qarPath);
                    //Directory.CreateDirectory(Path.GetDirectoryName(packQarFolder));
                    //File.Copy(cleanPackFileBuildPath, packQarFolder, true);
                    CopyFilesRecursively(new DirectoryInfo(cleanPackFolderBuildPath), new DirectoryInfo(packQarFolder),"","");
                }
            }

            if (buildLng2s) {
                Console.WriteLine("building lng2s");
                TraverseTree(modBuildPath, ".xml", RunLangToolProcess, ref modFilesInfo);

                TraverseTree(modBuildPath, ".xml", DeleteLng2XmlProcess, ref modFilesInfo);
            }

            if (buildFox2s) {
                Console.WriteLine("building fox2s");
                TraverseTree(projectPath, ".xml", RunFoxToolProcess, ref modFilesInfo);      
            }

            if (buildCustomFpkss) {
                //Console.WriteLine("building fox2s");
                //TraverseTree(projectCustomPackFilesPathFull, ".xml", RunFoxToolProcess, ref modFilesInfo);
                Console.WriteLine("building custom fpks");//tex cause snakede done be borke for some shit
                //TraverseTree(modBuildPath, ".xml", RunGzToolProcess, ref modFilesInfo);
                TraverseTree(projectPath, ".xml", RunGzToolProcess, ref modFilesInfo);
                //
            }

            if (copyCustomFpks) {
                Console.WriteLine();
                Console.WriteLine("copying custom pack folder");
                if (Directory.Exists(projectCustomPackFilesPathFull)) {
                    CopyFilesRecursively(new DirectoryInfo(projectCustomPackFilesPathFull), new DirectoryInfo(modBuildPath), "", ".xml");
                }
            }


            string snakeBiteMgvsDestFilePath = buildFolder + "\\" + modFileName + " " + modVersion + ".mgsv";
            string snakeBiteMgvsFilePath = modBuildPath + "\\" + "mod.mgsv";
            snakeBiteMgvsFilePath = UnfuckPath(snakeBiteMgvsFilePath);
            string snakeBiteMetaDataFilePath = buildFolder + "\\" + "metadata.xml";
            string snakeBiteMetaDataDestFilePath = modBuildPath + "\\" + "metadata.xml";
            string snakeBiteReadMeFilePath = buildFolder + "\\" + readMeName;
            snakeBiteReadMeFilePath = UnfuckPath(snakeBiteReadMeFilePath);
            string snakeBiteReadMeDestFilePath = modBuildPath + "\\" + "readme.txt";
            snakeBiteReadMeDestFilePath = UnfuckPath(snakeBiteReadMeDestFilePath);

            Console.WriteLine("Deleting sbmods.xml");
            string sbmodFilePath = gamePath + "\\snakebite.xml";
            string sbmodCleanFilePath = gamePath + "\\snakebiteclean.xml";
            if (!quickBuild && File.Exists(sbmodFilePath)) {
                File.Copy(sbmodCleanFilePath,sbmodFilePath,true);
            }

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

            string patchPath = gamePath + gameArchiveSubpath + targetGamePatchFolder0Subpath;
           // string backup00Name = "00.dat.original";
           // string backup01Name = "01.dat.original";
           // string backup00PathFull = patchPath + "\\" + backup00Name;
            //string backup01PathFull = patchPath + "\\" + backup01Name;
            string sbclean00PathFull = patchPath + "\\" + "00.dat.sbclean";
            string target00 = patchPath + "\\00.dat";
           // string target01 = patchPath + "\\01.dat";

            if (!quickBuild) {
                Console.WriteLine("restoring 00.dat");
                if (File.Exists(sbclean00PathFull)) {
                    File.Copy(sbclean00PathFull, target00, true);
                }
            }

            if (makeMod) {
                Console.WriteLine("makebite building " + snakeBiteMgvsFilePath);
                string toolArgs = "";
                toolArgs += modBuildPath;
                UseTool(makeBitePath, toolArgs);
            } else {
                Console.WriteLine("done.");
                Console.ReadKey();
            }

            //
            if (customfpkfix) {
                string extractPath = modBuildPath + "\\mod";

                string toolArgs = " x ";
                toolArgs += snakeBiteMgvsFilePath;
                toolArgs += " -o" + extractPath;
                UseTool(@".\7za.exe", toolArgs);

                Console.WriteLine("copying custom pack fpkds");
                CopyFilesRecursively(new DirectoryInfo(projectCustomPackFilesPathFull), new DirectoryInfo(extractPath), ".fpkd","");
                DeleteEmptyDirs(extractPath);


                Console.WriteLine("deleting .mgsv");
                if (File.Exists(snakeBiteMgvsFilePath)) {
                    File.Delete(snakeBiteMgvsFilePath);
                }

                toolArgs = " a -tzip " + snakeBiteMgvsFilePath + " " +  extractPath  +"\\*.* -r";

                UseTool(@".\7za.exe", toolArgs);
            }

            if (!File.Exists(snakeBiteMgvsFilePath)) {
                Console.WriteLine("Error! Cannot find " + snakeBiteMgvsFilePath);
                Console.ReadKey();
                return;
            } else {
                Console.WriteLine("Copying built msgv");
                File.Copy(snakeBiteMgvsFilePath, snakeBiteMgvsDestFilePath, true);
            }

            Console.WriteLine("running snakebite");
            string snakeBiteArgs = "\"" + snakeBiteMgvsFilePath + "\"";
            snakeBiteArgs += " -i";//install
            snakeBiteArgs += " -c";//no conflict check
            snakeBiteArgs += " -d";//reset hash
            snakeBiteArgs += " -x";//exit
            if (installMod) {
                UseTool(snakeBitePath, snakeBiteArgs);
            }
            //tex TODO: check if 01.dat was actually modded

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
                    string projectDataFilesPathFull = projectPath + projectDataFilesSubpath;
                    string projectPackFilesPathFull = projectPath + projectPackFilesSubPath;

                    string prepath = fileInfo.FullName.Substring(0, projectDataFilesPathFull.Length);
                    if (fileInfo.FullName.Contains(projectDataFilesSubpath)) {
                        // buildFileInfo.originalQar = "data1_dat";

                        buildFileInfo.filePath = fileInfo.FullName.Substring(projectDataFilesPathFull.Length);
                    } else if (fileInfo.FullName.Contains(projectPackFilesSubPath)) {
                        buildFileInfo.filePath = fileInfo.FullName.Substring(projectPackFilesPathFull.Length);
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
            if (!fileInfo.Name.Contains(".fox2.xml") && !fileInfo.Name.Contains(".sdf.xml")) {
                return;
            }

            UseTool(foxToolPath, fileInfo.FullName);
        }

        public static void RunGzToolProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            if (!fileInfo.Name.Contains(".fpk.xml") && !fileInfo.Name.Contains(".fpkd.xml")) {
                return;
            }

            UseTool(gzsToolPath, fileInfo.FullName);
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
                if (exclude=="" || file.Extension != exclude) {
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
