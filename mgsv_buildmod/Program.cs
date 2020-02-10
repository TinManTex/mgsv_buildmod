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

namespace mgsv_buildmod {
    class Program {
        //todo: pull game version from readme?

        //tex TODO: validate paths, then check existance of

        //static string projectPath = @"D:\Projects\MGS\LDTWorkspace";
        static string projectPath = @"D:\Projects\MGS\InfiniteHeavenLDT\InfiniteHeaven\src";
        static string projectDataFilesSubpath = @"\Data";// in respect to project path
        static string projectPackFilesSubPath = @"\fpkcombined";
        static string projectDataFilesPathFull = projectPath + projectDataFilesSubpath;
        static string projectPackFilesPathFull = projectPath + projectPackFilesSubPath;

        static string modBuildPath = @"D:\Projects\MGS\build\infiniteheaven\snakebitemod";
        static string modArchiveFilesSubpath = @"\archiveFiles";
        static string modPackFilesSubpath = @"\packFiles";

        static string buildFolder = ConfigurationManager.AppSettings.Get("buildFolder");


        static string gzsToolPath = ConfigurationManager.AppSettings.Get("gzsToolPath");
        static string qarToolPath = ConfigurationManager.AppSettings.Get("qarToolPath");
        static string makeBitePath = ConfigurationManager.AppSettings.Get("makeBitePath");
        static string snakeBitePath = ConfigurationManager.AppSettings.Get("snakeBitePath");
        static string gamePath = ConfigurationManager.AppSettings.Get("gamePath");

        static string cleanDataPath = ConfigurationManager.AppSettings.Get("cleanDataPath");
        static string gameArchiveSubpath = @"\master";
        static string targetGamePatchFolder0Subpath = @"\0";
        static string patchArchive00Subpath = "\\00";

        static string targetGameArchiveSubpath = "\\00";
        static string targetGameArchiveFileName = "00.dat";
        static string targetGameArchiveFlags = "3150304";

        // TODO: just point to sperate file
        static string modName = "Infinite Heaven";
        static string modVersionDefault = "r40zz";
        static string modFileName = "Infinite Heaven";
        static string readMeName = "Infinite Heaven Readme.txt";



        public class BuildFileInfo {
            public string fullPath = "";
            public bool doBuild = false;
            public string originalQar = "";
            public string filePath = "";
            public string packPath = "";
            public string qarPath = "";
            public string packInfo = "";
            public string hash = "";
            public string key = "";
            public string compressed = "";
            public string qarToolInfString = "";
        }

        public static string UnfuckPath(string path) {
            String unfucked = new Uri(path).LocalPath;
            return unfucked;
        }

        public delegate void ProcessFileDelegate(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList);

        static void Main(string[] args) {

            string value = ConfigurationManager.AppSettings.Get("buildModOnly");
            int doBuildMod = 0;
            if (!Int32.TryParse(value, out doBuildMod)) {
            }

            bool buildModOnly = doBuildMod > 0;
            bool quickBuild = false;

            bool buildFpks = true;
            bool buildFpksForSperate = true;
            bool copyFpkLoose = false;

            bool wantCleanBuildInfo = true;
            bool wantCleanData = true;
            bool wantCleanFpks = true;
            //bool allowExtractData = false;
            bool wantInstall = true;
            bool generateQarInfo = false;



            if (args.Length != 0) {
                foreach (string arg in args) {
                    if (arg=="quickbuild") {
                        quickBuild = true;
                    }
                }
            }


            if (buildModOnly) {
                wantCleanBuildInfo = true;
                wantCleanData = false;
                wantCleanFpks = buildFpksForSperate;
                //   allowExtractData = false;
                wantInstall = false;
            }

            if (buildFpksForSperate) {
                buildFpks = true;
            }

            if (quickBuild) {
                wantCleanData = false;
                wantCleanFpks = false;

            }


            String appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);


            // get version from readme, superhax i know
            string modVersion = modVersionDefault;
            string readmePathFull = buildFolder + "\\" + readMeName;
            if (File.Exists(readmePathFull)) {
                string[] readmeLines = File.ReadAllLines(readmePathFull);
                // ASSUMPTION: version on 2nd line, 1st chars
                if (readmeLines.Length>1) {
                    char[] splitchar = { ' ' };
                    string[] split = readmeLines[1].Split(splitchar);
                    if (split.Length!=0) {
                        modVersion = split[0];
                    }
                }
            }

            //String buildInfoFileFull = appPath + "\\" + buildInfoFile;
            //buildInfoFileFull = UnfuckPath(buildInfoFileFull);

            Console.WriteLine("generating buildInfo");
            Dictionary<string, BuildFileInfo> modFilesInfo = new Dictionary<string, BuildFileInfo>();
            TraverseTree(projectPath, ".lua", ReadLuaBuildInfoProcess, ref modFilesInfo);
            if (modFilesInfo.Count == 0) {
                Console.WriteLine("no mod files found");
                return;
            }

            if (buildModOnly) {
                if (Directory.Exists(modBuildPath)) {
                    Directory.Delete(modBuildPath, true);
                }
            }

            // todo: wantcleardata selection
            // if wantcleardata or new file added/deleted??
            if (wantCleanData) {
                Console.WriteLine();
                Console.WriteLine("deleting buildFolder " + targetGameArchiveFileName + " and folder");
                string buildPatchFilePath = buildFolder + "\\" + targetGameArchiveFileName;
                string buildPatchPath = buildFolder + targetGameArchiveSubpath;

                if (Directory.Exists(buildPatchPath)) {
                    Directory.Delete(buildPatchPath, true);
                }

                if (File.Exists(buildPatchFilePath)) {
                    File.Delete(buildPatchFilePath);
                }

                Console.WriteLine();
                Console.WriteLine("copying clean " + targetGameArchiveFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(buildPatchFilePath));
                string cleanDataFileSource = cleanDataPath + targetGamePatchFolder0Subpath + "\\" + targetGameArchiveFileName;
                File.Copy(cleanDataFileSource, buildPatchFilePath, true);

                Console.WriteLine();
                Console.WriteLine("extracting " + targetGameArchiveFileName);
                string output = UsePackTool(buildPatchFilePath, false);
                //Console.WriteLine(output);
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

            // Console.WriteLine();
            // Console.WriteLine("looking up GzsTool xml info for buildFiles");
            //tex for each xml found, for each entry, for each modfile find matching archive path 
            //  TraverseTree(cleanDataPath, ".xml", ReadGzsToolXmlProcess, ref modFilesInfo);

            if (generateQarInfo) {
                Console.WriteLine();
                Console.WriteLine("looking up Qartool info for buildFiles");
                //tex for each inf found, for each line, for each modfile find matching archive path 
                TraverseTree(cleanDataPath, ".inf", ReadQarToolInfProcess, ref modFilesInfo);
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

            if (wantCleanFpks) {
                Console.WriteLine();
                Console.WriteLine("wantCleanFpks");
                if (fpks.Count == 0) {
                    Console.WriteLine("no fpks needed to process");
                }

                CleanAllFpks(fpks);
            }

            Console.WriteLine();
            Console.WriteLine("copying mod files to build folder");
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.doBuild) {
                    string buildBaseFolder = buildFolder;
                    if (buildModOnly) {
                        string luaFileDestination = modBuildPath + buildFileInfo.filePath;
                        if (IsForFpk(buildFileInfo)) {
                            //string cleanPackFileName = Path.GetFileName(buildFileInfo.filePath);
                            //string cleanPackFileBuildPath = buildFolder + modPackFilesSubpath + "\\" + cleanPackFileName;
                            string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;
                            luaFileDestination = cleanPackFolderBuildPath + buildFileInfo.filePath;
                            //tex snakebite future style?
                            if (copyFpkLoose && !buildFpksForSperate) {
                                luaFileDestination = modBuildPath + buildFileInfo.packPath;
                                luaFileDestination = luaFileDestination.Replace(".", "_");
                                luaFileDestination += buildFileInfo.filePath;
                            }
                        }
                        Console.WriteLine(luaFileDestination);

                        Directory.CreateDirectory(Path.GetDirectoryName(luaFileDestination));
                        File.Copy(buildFileInfo.fullPath, luaFileDestination, true);
                    } else {

                        string luaFileDestination = buildFolder + buildFileInfo.qarPath + buildFileInfo.filePath;
                        Console.WriteLine(luaFileDestination);

                        Directory.CreateDirectory(Path.GetDirectoryName(luaFileDestination));
                        File.Copy(buildFileInfo.fullPath, luaFileDestination, true);
                    }
                }
            }
            /*
            if (buildModOnly && !buildFpksForSperate) {
                Console.WriteLine("done buildModOnly");
                return;
            }*/

            if (buildFpks) {

                Console.WriteLine();
                Console.WriteLine("building fpks");
                if (fpks.Count == 0) {
                    Console.WriteLine("no fpks needed to process");
                }

                foreach (var item in fpks) {
                    BuildFileInfo buildFileInfo = item.Value[0];

                    string cleanPackFileName = Path.GetFileName(item.Key);
                    string cleanPackFileBuildPath = buildFolder + modPackFilesSubpath + "\\" + cleanPackFileName;
                    string cleanPackFolderBuildPath = buildFolder + modPackFilesSubpath + buildFileInfo.qarPath;

                    // string cleanPackFileSourcePath = cleanDataPath + "\\" + buildFileInfo.originalQar + buildFileInfo.packPath;

                    string buildBaseFolder = buildFolder;
                    if (buildModOnly) {
                        buildBaseFolder = modBuildPath;
                    }

                    string packQarFolder = buildBaseFolder + targetGameArchiveSubpath + buildFileInfo.packPath;
                    if (buildModOnly) {
                        packQarFolder = buildBaseFolder + buildFileInfo.packPath;
                    }

                    if (!Directory.Exists(cleanPackFolderBuildPath)) {
                        Console.WriteLine("cannot find fpk build folder " + cleanPackFolderBuildPath);
                        return;
                    }
                    //tex TODO: File.Exists(PackToolDatafile)
                    //tex TODO: add to/build packtooldatafile

                    if (buildFpksForSperate) {
                        //tex pack it
                        Console.WriteLine();
                        Console.WriteLine(cleanPackFileName);
                        string output = UsePackTool(cleanPackFileBuildPath, true);
                        Console.WriteLine(output);
                    }

                    Console.WriteLine();
                    Console.WriteLine("copying pack " + cleanPackFileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(packQarFolder));
                    File.Copy(cleanPackFileBuildPath, packQarFolder, true);
                }

            }


            //Console.WriteLine();
            //Console.WriteLine("writing mod.xml");
            //WriteModXml(modFilesInfo);

            if (generateQarInfo) {
                Console.WriteLine();
                Console.WriteLine("writing mod.inf");
                WriteModInf(modFilesInfo);
            }


            if (buildModOnly) {
                string snakeBiteMgvsDestFilePath = buildFolder + "\\" + modFileName + " " + modVersion + ".mgsv";
                string snakeBiteMgvsFilePath = modBuildPath + "\\" + modFileName + ".mgsv";
                snakeBiteMgvsFilePath = UnfuckPath(snakeBiteMgvsFilePath);
                string snakeBiteMetaDataFilePath = buildFolder + "\\" + "metadata.xml";
                string snakeBiteMetaDataDestFilePath = modBuildPath + "\\" + "metadata.xml";
                string snakeBiteReadMeFilePath = buildFolder + "\\" + readMeName;
                snakeBiteReadMeFilePath = UnfuckPath(snakeBiteReadMeFilePath);
                string snakeBiteReadMeDestFilePath = modBuildPath + "\\" + "readme.txt";
                snakeBiteReadMeDestFilePath = UnfuckPath(snakeBiteReadMeDestFilePath);

                /*if (File.Exists(snakeBiteMgvsFilePath)) {
                    File.Delete(snakeBiteMgvsFilePath);
                }*/

                Console.WriteLine("Deleting sbmods.xml");
                string sbmodFilePath = gamePath + "\\sbmods.xml";
                if (File.Exists(sbmodFilePath)) {
                    File.Delete(sbmodFilePath);
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

                Console.WriteLine("makebite building " + snakeBiteMgvsFilePath);
                string toolArgs ="";
                //toolArgs += "-i "; //tex not an actual arg but its reading from args[1] for modbuildpath for some reason
                toolArgs += modBuildPath;
                /* CULL: 0.31
                toolArgs += modBuildPath;
                toolArgs += " ";
                toolArgs += snakeBiteMgvsFilePath;
                toolArgs += " ";
                toolArgs += snakeBiteMetaDataFilePath;
                toolArgs += " ";
                toolArgs += snakeBiteReadMeFilePath;
                toolArgs += " ";
                toolArgs += "version=" + modVersion;
                */
                UseTool(makeBitePath,toolArgs);


                if (!File.Exists(snakeBiteMgvsFilePath)) {
                    Console.WriteLine("Error! Cannot find " + snakeBiteMgvsFilePath);
                    Console.ReadKey();
                    return;
                } else {
                    Console.WriteLine("Copying built msgv");
                    File.Copy(snakeBiteMgvsFilePath, snakeBiteMgvsDestFilePath, true);
                }

                if (!quickBuild) {
                    Console.WriteLine("restoring 01.dat");
                    string patchPath = gamePath + gameArchiveSubpath + targetGamePatchFolder0Subpath;
                    string backupName = "01.dat.backup";
                    string backupPathFull = patchPath + "\\" + backupName;
                    string target = patchPath + "\\01.dat";
                    if (File.Exists(backupPathFull)) {
                        File.Copy(backupPathFull, target, true);
                    }
                }

                Console.WriteLine("running snakebite");
                string snakeBiteArgs = "-i " + "\"" + snakeBiteMgvsFilePath + "\"";
                // string snakeBiteArgs = snakeBiteMgvsFilePath; //cull 0.31
                UseTool(snakeBitePath,snakeBiteArgs);
                //tex TODO: check if 01.dat was actually modded


                Console.WriteLine("done buildModOnly");
                Console.ReadKey();
                return;
            }

            Console.WriteLine();
            Console.WriteLine("packing " + targetGameArchiveFileName);
            string moddedGameArchivePath = buildFolder + "\\" + targetGameArchiveFileName;
            Process proc = new Process();
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.FileName = qarToolPath; //gzToolPath;
            //proc.StartInfo.Arguments = ppath + ".xml";
            proc.StartInfo.Arguments = moddedGameArchivePath.Substring(0, moddedGameArchivePath.Length - 4) + ".inf -r";//qartool args
            proc.Start();
            string output2 = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            //var exitCode = proc.ExitCode;
            //Console.WriteLine(output2);

            Console.WriteLine();
            Console.WriteLine("finished building " + moddedGameArchivePath);

            if (wantInstall) {
                Console.WriteLine();
                Console.WriteLine("copying " + targetGameArchiveFileName + " to game");
                string targetPath = gamePath + gameArchiveSubpath + targetGamePatchFolder0Subpath + "\\" + targetGameArchiveFileName;
                File.Copy(moddedGameArchivePath, targetPath, true);
            }

            Console.WriteLine();
            Console.WriteLine("done.");


            Console.ReadKey();
        }

        private static void WriteModInf(Dictionary<string, BuildFileInfo> modFilesInfo) {
            List<string> infLines = new List<string>();
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (!buildFileInfo.doBuild) {
                    continue;
                }
                if (buildFileInfo.qarToolInfString == "") {
                    Console.WriteLine("no inf string for" + buildFileInfo.filePath);
                    continue;
                }

                string qarToolInfString = buildFileInfo.qarToolInfString;
                // change the qar name to 00
                int start = qarToolInfString.IndexOf("|") + 1;
                int end = qarToolInfString.IndexOf(@"\");
                string qarName = qarToolInfString.Substring(start, end - start);
                string targetQar = Path.GetFileNameWithoutExtension(targetGameArchiveFileName);
                qarToolInfString = qarToolInfString.Replace(qarName, targetQar);
                infLines.Add(qarToolInfString);
            }
            string modInfPath = modBuildPath + "\\mod.inf";
            File.WriteAllLines(modInfPath, infLines.ToArray());
        }

        /*
        private static void WriteModXml(Dictionary<string, BuildFileInfo> modFilesInfo) {
            XmlWriterSettings xmlSettings = new XmlWriterSettings();
            xmlSettings.Indent = true;
            string modXmlFilePath = modBuildPath + targetGameArchiveSubpath + "\\mod.xml";
            XmlWriter datXmlWriter = XmlWriter.Create(modXmlFilePath, xmlSettings);
            datXmlWriter.WriteStartDocument();
            datXmlWriter.WriteStartElement("SnakeBite");
            datXmlWriter.WriteStartElement("ModData");
            datXmlWriter.WriteStartElement("Name");
            datXmlWriter.WriteString(modName);
            datXmlWriter.WriteEndElement();
     
               // datXmlWriter.WriteStartElement("Version");
                //    datXmlWriter.WriteString(modVersion);
              //  datXmlWriter.WriteEndElement();
       
            datXmlWriter.WriteStartElement("Author");
            datXmlWriter.WriteString(modAuthor);
            datXmlWriter.WriteEndElement();
            datXmlWriter.WriteStartElement("Website");
            datXmlWriter.WriteString(modWebsite);
            datXmlWriter.WriteEndElement();
            datXmlWriter.WriteEndElement();//modfata
            datXmlWriter.WriteStartElement("GzsXmlData");

            datXmlWriter.WriteStartElement("ArchiveFile");
            datXmlWriter.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "QarFile");
            datXmlWriter.WriteAttributeString("Name", targetGameArchiveFileName);
            datXmlWriter.WriteAttributeString("Flags", targetGameArchiveFlags);

            datXmlWriter.WriteStartElement("Entries");
            foreach (BuildFileInfo buildFileInfo in modFilesInfo.Values) {
                if (buildFileInfo.hash == "") {
                    Console.WriteLine("hash not set for " + buildFileInfo.filePath);
                    continue;
                }

                datXmlWriter.WriteStartElement("Entry");
                datXmlWriter.WriteAttributeString("Hash", buildFileInfo.hash);
                datXmlWriter.WriteAttributeString("Key", buildFileInfo.key);
                string filePath = buildFileInfo.filePath;
                if (filePath.StartsWith("\\")) {
                    filePath = filePath.Substring(1);
                }
                datXmlWriter.WriteAttributeString("FilePath", filePath);
                datXmlWriter.WriteAttributeString("Compressed", buildFileInfo.compressed);
                datXmlWriter.WriteEndElement();//entry
            }

            datXmlWriter.WriteEndElement();//Entries

            datXmlWriter.WriteEndElement();//ArchiveFile

            datXmlWriter.WriteEndElement();//GzsXmlData
            datXmlWriter.WriteEndElement();//SnakeBite

            datXmlWriter.Close();
            datXmlWriter.Dispose();
        }
    */

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
        }

        public static void ReadLuaBuildInfoProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {

            /* --tex REF:
            -- DOBUILD: 1
            -- ORIGINALQAR: chunk0
            -- FILEPATH: \Assets\tpp\level\mission2\init\init_sequence.lua
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

            Console.WriteLine(fileInfo.FullName);

            BuildFileInfo buildFileInfo = null;
            if (!buildFileInfoList.TryGetValue(fileInfo.FullName, out buildFileInfo)) {
                buildFileInfo = new BuildFileInfo();
                buildFileInfoList.Add(fileInfo.FullName, buildFileInfo);
            }

            buildFileInfo.fullPath = fileInfo.FullName;
            // find path relative to projectpath?
            // find 'data' 'pack' - set type
            // what about orginail archive? useful to more quickly find the inf/tool xml
            // figure out filepath /root (ie \archive\tpp\blah
            string projectDataFilesPathFull = projectPath + projectDataFilesSubpath;
            string projectPackFilesPathFull = projectPath + projectPackFilesSubPath;

            string prepath = fileInfo.FullName.Substring(0, projectDataFilesPathFull.Length);
            if (fileInfo.FullName.Contains(projectDataFilesSubpath)) {
                buildFileInfo.originalQar = "data1_dat";

                buildFileInfo.filePath = fileInfo.FullName.Substring(projectDataFilesPathFull.Length);
            } else if (fileInfo.FullName.Contains(projectPackFilesSubPath)) {
                buildFileInfo.filePath = fileInfo.FullName.Substring(projectPackFilesPathFull.Length);
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
                    case "ORIGINALQAR:":
                        System.Console.WriteLine(line);
                        if (buildFileInfo.originalQar == "") {
                            buildFileInfo.originalQar = attribValue + "_dat";
                        }
                        break;
                    case "FILEPATH:":
                        System.Console.WriteLine(line);
                        //buildFileInfo.filePath = attribValue;
                        break;
                    case "PACKPATH:":
                        System.Console.WriteLine(line);
                        buildFileInfo.packPath = attribValue;
                        break;
                }
            }
        }

        public static string GetNodeAttribute(XmlNode node, string key) {
            if (node.Attributes != null) {
                var attrib = node.Attributes[key];
                if (attrib != null) {
                    return attrib.Value;
                }
            }
            return "";
        }


        public static void ReadGzsToolXmlProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {
            XmlDocument gzsToolXml = new XmlDocument();
            gzsToolXml.Load(fileInfo.FullName);
            //tex TODO: catch exceptions
            XmlNode root = gzsToolXml.SelectSingleNode("//ArchiveFile");
            if (root == null) {
                Console.WriteLine(fileInfo.FullName + " is not a gzstool xml");
                return;
            }

            XmlNode entries = gzsToolXml.SelectSingleNode("//ArchiveFile/Entries");
            if (entries == null) {
                Console.WriteLine(fileInfo.FullName + " is not a gzstool xml");
                return;
            }

            Console.WriteLine(fileInfo.FullName);

            foreach (BuildFileInfo buildFileInfo in buildFileInfoList.Values) {
                string filePath = buildFileInfo.filePath;
                if (filePath.StartsWith("\\")) {
                    filePath = filePath.Substring(1);
                }

                foreach (XmlNode node in entries.ChildNodes) {
                    string nodeFilePath = GetNodeAttribute(node, "FilePath");
                    if (nodeFilePath == filePath) {
                        buildFileInfo.hash = GetNodeAttribute(node, "Hash");
                        buildFileInfo.key = GetNodeAttribute(node, "Key");
                        buildFileInfo.compressed = GetNodeAttribute(node, "Compressed");
                        Console.WriteLine("found " + filePath);
                    }
                }
            }
        }

        public static void ReadQarToolInfProcess(FileInfo fileInfo, ref Dictionary<string, BuildFileInfo> buildFileInfoList) {

            string[] lines = File.ReadAllLines(fileInfo.FullName);

            if (lines.Length == 0) {
                return;
            }

            for (int i = 3; i < lines.Length; i++) {// ASSUMPTION: standard qar tool inf header of 3 lines, with file info 4th/index 3
                string line = lines[i];

                foreach (BuildFileInfo buildFileInfo in buildFileInfoList.Values) {
                    if (buildFileInfo.qarToolInfString != "") {
                        continue;
                    }

                    string filePath = buildFileInfo.filePath;
                    if (filePath.StartsWith("\\")) {
                        filePath = filePath.Substring(1);
                    }
                    filePath=filePath.Replace(@"\\", @"\");

                    if (line.Contains(filePath)) {
                        Console.WriteLine("found " + filePath);
                        buildFileInfo.qarToolInfString = line;
                    }
                }
            }
        }

        private static string UsePackTool(string packPath, bool pack) {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = gzsToolPath;
            p.StartInfo.Arguments = packPath;
            if (pack) {
                p.StartInfo.Arguments += ".xml";
            }
            p.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var exitCode = p.ExitCode;
            return output;
        }


        private static void UseTool(string toolPath, string args)
        {
            Process p = new Process();
            p.StartInfo.UseShellExecute = true;
            // p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName(toolPath);
            p.StartInfo.FileName = toolPath;
            p.StartInfo.Arguments = args;
            p.Start();
            //string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            var exitCode = p.ExitCode;
           // return output;
        }
    }
}
