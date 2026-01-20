using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesItemHelper
    {
        [HarmonyPatch (typeof (ItemHelper), nameof (ItemHelper.LoadVisuals))]
        [HarmonyPrefix]
        public static bool LoadVisuals ()
        {
            var type = typeof (ItemHelper);
            
            // Ensure calls to CheckDatabase do not re-run LoadVisuals
            var autoloadAttemptedField = ModUtilities.GetPrivateFieldInfo (type, "autoloadAttempted", true, false);
            if (autoloadAttemptedField != null)
                autoloadAttemptedField.SetValue (null, true);
                
            Debug.Log ($"ModExtensions | ItemHelper.LoadVisuals | Executing stub method, all logic moved to LoadModsFinish patch");
            
            // Stop the original code from executing
            return false;
        }

        [HarmonyPatch (typeof (ItemHelper), nameof (ItemHelper.GetVisual))]
        [HarmonyPrefix]
        public static bool GetVisual (ref ItemVisual __result, string visualName, bool logAbsence = true)
        {
            ItemHelper.CheckDatabase ();

            if (string.IsNullOrEmpty (visualName))
            {
                __result = null;
                return false; // Stop the original code from executing
            }

            ItemVisual visual = null;
            if (ItemHelper.itemVisualPrefabs.TryGetValue (visualName, out visual))
            {
                __result = visual;
                return false; // Stop the original code from executing
            }
        
            // If we're here, prefab was not found
            if (PatchesModManager.assetItemVisualKeysAlt.TryGetValue (visualName, out var visualNameAlt) && !string.IsNullOrEmpty (visualNameAlt))
            {
                // If there's a known fallback name, let's try returning it
                if (ItemHelper.itemVisualPrefabs.TryGetValue (visualNameAlt, out visual))
                {
                    __result = visual;
                    return false; // Stop the original code from executing
                }
            }

            if (logAbsence)
                Debug.LogWarning ($"Failed to find item visual named {visualName}");
            
            __result = null;
            return false; // Stop the original code from executing
        }
    }
}