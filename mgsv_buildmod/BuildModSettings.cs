using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mgsv_buildmod {
    //tex deserialized from json build mod settings file
    //supplies the paths and drives most of the behavior of mgsv_buildmod
    //defaults are just to give user some idea of values when WriteDefaultConfigJson serializes this.
    //though it's probably better for user to look at Infinite Heavens buildSettings file on its repo.
    //most comments below are from the commented ih buildsettings

    // Paths can either be absolute or relative, to what depends on what the path parameter is,
    // but the majority they are relative to modPath.
    // If supplying relative path remember not to include any leading slashes
    // Slash direction doesnt really matter, but remember if you have windows style backslashes \ you need to escape them ex c:\somepath\subfolder\ = "c:\\somepath\\subfolder\\"
    // relative paths should not have leading slashes at the start
    // folder paths should have trailing slashes at the end

    internal class BuildModSettings {
        // Snakebite metadata
        public string Version = "";//string version, ex for IH == "r256"
        public string Name = "";//pretty name for snakebite //tex also used for to identify in snakebite uninstallExistingMod
        public string ModId = "";//for AddLuaFileVersions
        public string Author = "";
        public string Website = "";
        //

        // Root for relative paths within source mod layout, 
        // if this parameter is not set then modPath will be set to the path of this buildSettings file.
        public string modPath = null; //"C:/Projects/MGS/InfiniteHeaven/tpp";

        // Folder all the following is copied to before being makebitten.
        public string makebiteBuildPath = "C:/Projects/MGS/build/infiniteheaven/makebite";
        // Folder the built .mgsv and docsPath files are copied to.
        public string buildPath = "C:/Projects/MGS/build/infiniteheaven/build";

        // .mgsv name, copied to buildPath 
        public string modFileName = "Infinite Heaven";
        // Name of file inside docsPath, file is copied to makebiteBuildPath/readme.txt so makebite can pull it into metadata. 
        public string readMeFileName = "Readme.txt";
        // For copyDocsToBuild, copied to {buildPath}\docs
        public string docsPath = "gamedir-ih/GameDir/mod/docs/Infinite Heaven";
        // Path to metadata.xml for snakebite, copied to makebiteBuidlPath, edited with above values. 
        // Default relative to modPath. 
        // You can just start by copying any metadata.xml
        // Doesn't matter what anything in the metadata.xml is set to except for MGSVersion. Everything else will get filled out by the above values.
        public string metadataPath = "";

        //GOTCHA: don't fill out with examples because json entries are added rather than replace
        public Dictionary<string,
            Dictionary<string, List<string>>> modFiles = new Dictionary<string,
                                                                    Dictionary<string, List<string>>>() {
                //REF
                //{"fpkd-combined-lua/", new Dictionary<string, List<string>>{
                //    {"Assets/tpp/level/mission2/init/init_sequence.lua", new List<string>{"Assets/tpp/pack/mission2/init/init_fpkd" },
                //}, },
        };//modFiles

        public string gamePath = null;//@"C:\Games\Steam\SteamApps\common\MGS_TPP";

        // Settings 

        // Copies docsPath to {buildPath}\docs, so they can be included in release zip for user to check out without installing or unzipping .mgsv
        public bool copyDocsToBuild = true;

        // If you dont have actual translations for lang codes this will copy the eng lng2s to the other lang code lng2s.
        // Currently only does for lang_default_data_eng_fpk (folder) > lang_default_data_{langCode}_fpk 
        // Should not be run if you are modding base game lngs.
        public bool copyEngLng2sToOtherLangCodes = false;//tex if you dont have actual translations for lang codes this will copy the eng lng2s to the other lang code lng2s

        public bool compileMakebiteBuildFiles = true; //tex overall switch of below
        //SYNC: CompileMakebiteBuildFiles
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

        // Adds .modId and .version to all lua files in makebiteBuildPath, used by IH for error checking
        public bool addLuaFileVersions = false;
        // Run makebite on makebiteBuildPath, copy the .mgsv to buildPath.
        public bool makeMod = true;
        // Uninstalls first mod found that matches {Name}
        // for development it's usually fine to leave this false providing you arent removing files between build versions
        // it will just keep adding mod entries in snakebite though, so eventually you'll have to uninstall them manually.
        public bool uninstallExistingMod = true;
        // Install .mgsv file that was built during makeMod
        public bool installMod = true;//tex install build mod
        // Leave the program window at the end open, waiting for any keypress to close
        public bool waitEnd = true;
    }//BuildModSettings
}//mgsv_buildmod
