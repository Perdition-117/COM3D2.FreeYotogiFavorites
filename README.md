# COM3D2.FreeYotogiFavorites

Allows pinning favorite yotogi skills for quick access in memories mode. You may also enable history mode in order to show recently used skills instead of favorites.

Shift-click skills in the skill tree to toggle favorite status. Favorite skills are highlighted in yellow. You may set up to six favorite skills.

To enable history mode, edit `BepInEx\config\net.perdition.com3d2.freeyotogifavorites.cfg` (created after first run) to set `HistoryMode = true`.

![playlist](https://user-images.githubusercontent.com/87424475/161384594-05302b97-408d-440c-b305-e2162208b1a7.png)
![skill-tree](https://user-images.githubusercontent.com/87424475/161384481-6092db59-e7a8-4464-93d3-12ebf04a7762.png)

## Installing

Get the latest version from [the release page](https://github.com/Perdition-117/COM3D2.FreeYotogiFavorites/releases/latest). Extract the archive and place `COM3D2.FreeYotogiFavorites.dll` in `BepInEx\plugins`.

## Caveats

If one or more favorite skills are unavailable to the current maid, it may appear as though the number of available favorites are reduced, since the unavailable skills count towards the limit. The same is true for history mode, where a reduced number of recent skills may be shown until unavailable skills have been flushed out.
