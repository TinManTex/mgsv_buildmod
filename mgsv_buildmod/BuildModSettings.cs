using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mgsv_buildmod {
    internal class BuildModSettings {
        //tex smakebite metadata
        public string Version = "";
        public string Name = "";
        public string Author = "";
        public string Website = "";

        public string modFileName = "Infinite Heaven";//tex .mgsv name
        public string readMeFileName = "Readme.txt";//STRUCURE inside docs path
        public string docsPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\gamedir-ih\GameDir\mod\docs\Infinite Heaven";
        public string metadataPath = @"C:\Projects\MGS\InfiniteHeaven\tpp";

        public string modPath = null; //@"C:\Projects\MGS\InfiniteHeaven\tpp";//tex root for relative paths within source mod layout, if null or empty modPath will be set to the path of the given buildSettings file

        //tex folders have various tools run on them (see buildFox2s etc settings)
        //then are copied outright to makebitepath
        ///so need to be in makebiteable layout
        //GOTCHA: don't fill this out with example because json entries are added rather than replace
        public List<string> modFolderPaths = new List<string> {
            //REF
            //"fpk-mod/",
            //"fpk-mod-ih/",
        };

        public Dictionary<string, List<string>> modFileLists = new Dictionary<string, List<string>>() {
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
        };//modFileLists

        //copyModArchiveFiles
        public Dictionary<string,
            Dictionary<string, List<string>>> modArchiveFiles = new Dictionary<string,
                                                                    Dictionary<string, List<string>>>() {
                //REF
                //{"fpkd-combined-lua/", new Dictionary<string, List<string>>{
                //    {"Assets/tpp/level/mission2/init/init_sequence.lua", new List<string>{"Assets/tpp/pack/mission2/init/init_fpkd" },
                //}, },
            };//modArchiveFiles

        public string luaFpkdFilesPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\fpkd-combined-lua";//tex for copyLuaFpkdFiles //CULL supersceded by copyModArchiveFiles

        public string externalLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\gamedir-ih\GameDir\mod";//tex for copyExternalLuaToInternal
        public string modulesLuaPath = @"C:\Projects\MGS\InfiniteHeaven\tpp\gamedir-ih\GameDir\mod\modules";//tex for copyModulesToInternal
        public string modulesInternalPath = @"\Assets\tpp\script\ih";//tex for copyModulesToInternal

        public string makebiteBuildPath = @"C:\Projects\MGS\build\infiniteheaven\makebite"; //tex: where the various files are actually pulled together before being makebitten
        public string buildPath = @"C:\Projects\MGS\build\infiniteheaven\build";//tex where the built .mgsv and docs folder are placed ready for being zipped for release //TODO: zip it too

        public string gamePath = null;//@"C:\Games\Steam\SteamApps\common\MGS_TPP";

        public bool copyDocsToBuild = true;//tex copies docsPath to build, so they can be included in release zip for user to check out without installing or unzipping .mgsv

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
        public bool copyModFolders = true;//tex uses modFolderPaths

        public bool copyModFileLists = true;//for modFileLists

        public bool copyModArchiveFiles = true;//for modArchiveFiles

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
}//mgsv_buildmod
