# PB_ModExtensions
ModExtensions is a library mod for Phantom Brigade that patches issues interfering with certain mods.

  - Patches some overworld UI to improve mission display in custom campaigns.
  - Fixes modded given pilot names not appearing in random generation.
  - Fixes clamped part rating in unit generation preventing modding of higher rarities.
  - Fixes the internal spawn menu not selecting higher rarities.

The mod can additionally be used as a reference for other patch mods and provides utilities simplifying access to private fields and methods.

# File structure
Intended to be used with the [Mod SDK](https://github.com/BraceYourselfGames/PB_ModSDK). The mod is located under a subfolder to allow using the root folder of a local Git repository as a target for `Custom project folders` in the Mod SDK.
