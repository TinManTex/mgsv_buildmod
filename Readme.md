# mgsv_buildmod
tex: More or less a glorified batch file I created to build Infinite Heaven that's been with me since a month after the game released.
Still a bit messy since it was designed for IHs specific dev layout, but has been streamlined removing a bunch of hacks that were added when snakebite was in its infancy that are no longer nessesary.

At its core it works on a list of folders in makebite-layout, runs various seperate fox engine tools on them then copies and makebites them off a single build folder.

Set tool paths via mgsv_buildmod.exe.config next to the exe.
Run without any args to generate a jsonc of BuildModSettings.
Pass in path to a BuildModSettings to use it.
See comments on BuildSettings.cs
Or the tpp/infinite_heaven-dev.buildmod BuildSettings file on the Infinite Heaven repo which is heavily commented.
