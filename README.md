# PB_ModExtensions
This is a library mod modifying game code to fix bugs and limitations. Will be updated in sync with official releases: if a bug or limitation covered by this mod ends up being addressed in a future Phantom Brigade release, this mod will be adjusted accordingly. Some of the fixes:

- Patched overworld sidebar UI to prevent overflow with a high number of highlighted missions.
- Fixed given pilot names added by mods not appearing in random generation.
- Fixed clamped part rating in unit generation preventing modding of higher rarities.
- Fixed the internal spawn menu not selecting higher rarities.

The mod can additionally be used as a reference for other patch mods and provides utilities simplifying access to private fields and methods.

# File structure
Intended to be used with the [Mod SDK](https://github.com/BraceYourselfGames/PB_ModSDK). The mod is located under a subfolder to allow using the root folder of a local Git repository as a target for `Custom project folders` in the Mod SDK.
