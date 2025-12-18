using UnityEngine;
using HarmonyLib;
using PhantomBrigade.Mods;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesMisc
    {
        [HarmonyPatch (typeof (CIViewPauseFooter), "RefreshModDisplay")]
        [HarmonyPostfix]
        public static void RefreshModDisplay_Postfix (CIViewPauseFooter __instance)
        {
            Debug.Log ("ModExtensions | Redrawing mod list...");
            if (__instance == null)
                return;

            var labelMods = __instance.labelMods;
            if (labelMods == null)
                return;

            var modLink = ModLinkCustom.ins;
            var loadedData = ModManager.loadedModsLookup.TryGetValue (modLink.modID, out var v) ? v : null;
            if (loadedData != null)
            {
                int methodCount = loadedData?.patchedMethodNames != null ? loadedData.patchedMethodNames.Count : 0;
                var text = $"\n\n[ff9999]Mod extensions active ♦\n[cc]{methodCount} methods patched";
                labelMods.text += text;
            }
            else
            {
                var text = "\n\n[ff9999]Mod extensions active ♦\n[cc]Load info not found[-]";
                labelMods.text += text;
            }
        }
    }
}