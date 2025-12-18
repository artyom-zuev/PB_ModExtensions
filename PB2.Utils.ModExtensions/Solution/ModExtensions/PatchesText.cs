using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using PhantomBrigade.Data;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesText
    {
        private static HashSet<string> keysInGroups = new HashSet<string> ();
        private static string groupPrefix = "group_";
        private static string groupKeyAny = "any_";
        private static List<string> keyInjectionReports = new List<string> ();
        
        [HarmonyPatch(typeof(DataContainerTextSectorMain), "OnAfterDeserialization")]
        [HarmonyPostfix]
        public static void OnAfterDeserialization_Postfix (DataContainerTextSectorMain __instance, string key)
        {
            if (__instance == null)
                return;
            
            if (__instance.groups == null || __instance.entries == null)
                return;
            
            if (!Application.isPlaying)
                return;

            var sectorKey = __instance.key;
            var groups = __instance.groups;
            var entries = __instance.entries;
            
            keysInGroups.Clear ();
            keyInjectionReports.Clear ();

            foreach (var kvp in groups)
            {
                var group = kvp.Value;
                if (group == null)
                    continue;

                foreach (var entryKey in group)
                    keysInGroups.Add (entryKey);
            }
            
            foreach (var kvp in entries)
            {
                var entryKey = kvp.Key;
                if (keysInGroups.Contains (entryKey))
                    continue;
                
                if (!entryKey.StartsWith (groupPrefix))
                    continue;
                
                var entryKeySubstring = entryKey.Substring (groupPrefix.Length);
                if (entryKeySubstring.StartsWith (groupKeyAny))
                {
                    foreach (var kvp2 in groups)
                    {
                        var group = kvp2.Value;
                        group.Add (entryKey);
                        keyInjectionReports.Add ($"{entryKey} → {kvp2.Key}");
                    }
                }
                else
                {
                    foreach (var kvp2 in groups)
                    {
                        var groupKey = kvp2.Key;
                        if (entryKeySubstring.StartsWith (groupKey))
                        {
                            var group = kvp2.Value;
                            group.Add (entryKey);
                            keyInjectionReports.Add ($"{entryKey} → {kvp2.Key}");
                        }
                    }
                }
            }
            
            if (keyInjectionReports.Count > 0)
            {
                Debug.Log ($"ModExtensions | Loaded text sector {__instance.key} with {groups.Count} groups {groups.ToStringFormattedKeys ()} | Discovered keys requiring group changes:\n{keyInjectionReports.ToStringMultilineDash ()}");
            }
            else
            {
                Debug.Log ($"ModExtensions | Loaded text sector {__instance.key} with {groups.Count} groups {groups.ToStringFormattedKeys ()} | No keys requiring group changes discovered");
            }
        }
    }
}