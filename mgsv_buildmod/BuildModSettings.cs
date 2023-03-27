﻿using System;
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
        public string Version = "";
        public string Name = "";
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

        //GOTCHA: don't fill out the following structures with examples because json entries are added rather than replace

        // copyModFolders: Folders entirely copied to makebiteBuildPath
        public List<string> modFolders = new List<string> {
            //REF
            //"fpk-mod/",
            //"fpk-mod-ih/",
        };

        // copyModFiles: Specific files for folders copied to makebiteBuildPath
        public Dictionary<string, List<string>> modFiles = new Dictionary<string, List<string>>() {
            //REF
            //{"data1_dat-lua-ih/", new List<string>{
            //    "init.lua",
            //    "Assets/tpp/level_asset/chara/enemy/Soldier2FaceAndBodyData.lua",
            //    "Assets/tpp/script/lib/Tpp.lua",
            //    "Assets/tpp/script/lib/TppAnimal.lua",
            //    "Assets/tpp/script/lib/TppClock.lua",
            //    "Assets/tpp/script/lib/TppDefine.lua"
            //    }
            // },
        };//modFiles

        // copyModArchiveFiles: Lets you keep files in Assetspath structure, but then have them put in their archive folders in makebiteBuildPath
        public Dictionary<string,
            Dictionary<string, List<string>>> modArchiveFiles = new Dictionary<string,
                                                                    Dictionary<string, List<string>>>() {
                //REF
                //{"fpkd-combined-lua/", new Dictionary<string, List<string>>{
                //    {"Assets/tpp/level/mission2/init/init_sequence.lua", new List<string>{"Assets/tpp/pack/mission2/init/init_fpkd" },
                //}, },
            };//modArchiveFiles

        public string externalLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\gamedir-ih\GameDir\mod";//tex for copyExternalLuaToInternal
 
        //CULL
        public string luaFpkdFilesPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\fpkd-combined-lua";//tex for copyLuaFpkdFiles //CULL supersceded by copyModArchiveFiles

        public string modulesLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\gamedir-ih\GameDir\mod\modules";//tex for copyModulesToInternal
        public string modulesInternalPath = @"\Assets\tpp\script\ih";//tex for copyModulesToInternal

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

        public bool copyLuaFpkdFiles = false;//CULL supersceded by copyModArchiveFiles //tex uses luaFpkdFilesPath, fpk internal pathed lua files, their DOBUILD comment headers are used to copy them to full fpk paths, 

        public bool copyModulesToInternal = false;//copies external lua modules to internal [WIP]

        public bool copyModFolders = true;//tex uses modFolders

        public bool copyModFiles = true;//for modFiles

        public bool copyModArchiveFiles = true;//for modArchiveFiles

        // Copies files in externalLuaPath > "Assets", "Fox", "shaders", "Tpp" to the equivalent makebiteBuildPath
        // ie I keep some internal files external in \mod\ for dev build, but are put back internal for release
        // GOTCHA: ih will still try to load external by default, so do not include externalLuaPath files in release (see ih repo tpp vs tpp-release)
        // The function will delete any of the above mentioned sub paths in makebiteBuild GameDir\mod\ to mitigate this.
        public bool copyExternalLuaToInternal = false;
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
