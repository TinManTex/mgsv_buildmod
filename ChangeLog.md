mgsv_buildmod changelog

1.1 - 2023-03-
Added BuildSettings modPath
Acts as root / working folder of mod.
Other path parameters can still be absolute, or relative to modPath.
If modPath not set then modPath is set to the path of the buildSettings file.

BuildSettings modFiles now superscedes modPackFiles, the old DOBUILD header in in-fpkd files, and can list individual files in a folder so (ih data1_lua can be reunified).

compileModPackFiles changed to compileMakebiteBuildFiles
As the name suggests, tools now run over makebiteBuildPath instead of the individual modPackPaths.

copyEngLng2sToOtherLangCodes now run on makebiteBuildPath instead of the individual modPackPaths.

A ton of buildsettings commenting, which mostly mirrors comments from infinite_heaven-dev.buildmod buildSettings file.

1.0 -
Was never really versioned till 1.0 - and that was just because that's the default vs version sets, and it was first public release.

Pre 1.0 - 
A bunch of evolutions from a glorified batch file as it developed alongside IH starting from about a month after the game released in 2015.

Some snapshots of those up on the repo when it started in 2020.