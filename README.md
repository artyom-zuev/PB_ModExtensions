# PB_ModExtensions
This is a library mod modifying game code to fix bugs and limitations. It will not introduce significant changes to your game when installed separately, but might be required as a dependency by other mods.

Will be updated in sync with official releases: if a bug or limitation covered by this mod ends up being addressed in a future Phantom Brigade release, this mod will be adjusted accordingly. Some of the fixes:

- Patched overworld sidebar UI to prevent overflow with a high number of highlighted missions.
- Fixed given pilot names added by mods not appearing in random generation.
- Fixed clamped part rating in unit generation preventing modding of higher rarities.
- Fixed the internal spawn menu not selecting higher rarities.

Feel free to use this mod as a dependency or a reference the project setup and source code when developing your own library mods. 

# File structure
Intended to be used with the [Mod SDK](https://github.com/BraceYourselfGames/PB_ModSDK). The mod is located under a subfolder to allow using the root folder of a local Git repository as a target for `Custom project folders` in the Mod SDK.
