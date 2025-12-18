using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;
using PhantomBrigade.Data;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesUnitGeneration
    {
        [HarmonyPatch (typeof (CIViewInternalCombatSpawn), "OnQualityRedraw")]
        [HarmonyPrefix]
        public static bool OnQualityRedraw (CIViewInternalCombatSpawn __instance)
        {
            var view = __instance;
            
            try
            {
                var qualitySelectedField = ModUtilities.GetPrivateFieldInfo (view, "qualitySelected", false, true);
                var qualitySelected = qualitySelectedField.GetFieldInfoValue<int> (view);
                
                var text = qualitySelected.IsValidIndex (UnitEquipmentQuality.text) ? UnitEquipmentQuality.text[qualitySelected] : "?";
                view.fieldQuality.labelValue.text = $"{qualitySelected} ({text})";
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | OnQualityRedraw | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }
        
        [HarmonyPatch (typeof (CIViewInternalCombatSpawn), "OnQualityChange")]
        [HarmonyPrefix]
        public static bool OnQualityChange (CIViewInternalCombatSpawn __instance, bool forward)
        {
            var view = __instance;
            
            try
            {
                var qualityRedrawMethod = ModUtilities.GetPrivateMethodInfo (view, "OnQualityRedraw", false, true);
                
                var qualitySelectedField = ModUtilities.GetPrivateFieldInfo (view, "qualitySelected", false, true);
                var qualitySelected = qualitySelectedField.GetFieldInfoValue<int> (view);
                qualitySelected = qualitySelected.OffsetAndWrap (forward, 8);
                qualitySelectedField.SetValue (view, qualitySelected);
                
                qualityRedrawMethod.Invoke (view, null);
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | OnQualityChange | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }
    }

    [HarmonyPatch (typeof (UnitUtilities), nameof (UnitUtilities.CreatePersistentUnitDescription))]
    public class PatchesUnitGeneration_DescriptionRating
    {
        static IEnumerable<CodeInstruction> Transpiler (IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher (instructions);
            codeMatcher.MatchStartForward
            (
                new CodeMatch
                (
                    OpCodes.Call,
                    AccessTools.Method
                    (
                        typeof (Mathf),
                        nameof (Mathf.Clamp),
                        new[] { typeof (int), typeof (int), typeof (int) } // Match the exact overload of Mathf.Clamp
                    )
                )
            )
            .ThrowIfNotMatch
            (
                $"Could not transpile {typeof (UnitUtilities)}." +
                $"{nameof (UnitUtilities.CreatePersistentUnitDescription)}: method does not call " +
                $"{typeof (Mathf)}.{nameof (Mathf.Clamp)}"
            )
            .SetInstructionAndAdvance (new CodeInstruction (OpCodes.Nop)) // neutralize Clamp
            .Insert
            (
                new CodeInstruction (OpCodes.Pop), // pop integer used for max argument (4)
                new CodeInstruction (OpCodes.Pop)  // pop integer used for min argument (0)
            );

            // With all of these changes, we change:
            // - From: int num = Mathf.Clamp (rating1, 0, 4);
            // - To:   int num = rating1;

            return codeMatcher.Instructions ();
        }
    }
}