using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using PhantomBrigade.Mods;

namespace ModExtensions
{
    // Phantom Brigade 2.1.1 implements this fix
    // Code here temporarily commented out before removal
    /*
    [HarmonyPatch]
    public class PatchesEquipmentVisualHelper
    {
        private static int run = 0;
        
        [HarmonyPatch (typeof (EquipmentVisualHelper), nameof (EquipmentVisualHelper.RequestVisual))]
        [HarmonyPrefix]
        public static bool RequestVisual (ref ItemVisual __result, string key)
        {
            try
            {
                run += 1;
                
                // Matching vars to source code for easier syncing
                string visualName = key;
                var ins = EquipmentVisualHelper.ins;
                var visualPools = EquipmentVisualHelper.visualPools;

                bool log = EquipmentVisualHelper.log;
                var logFilter = EquipmentVisualHelper.logFilter;

                var type = typeof (EquipmentVisualHelper);
                var visualsInUseField = ModUtilities.GetPrivateFieldInfo (type, "visualsInUse", true);
                var visualsInUse = visualsInUseField.GetFieldInfoValue<List<ItemVisual>> (null);

                if (string.IsNullOrEmpty (visualName) || ins == null)
                {
                    __result = null;
                    return false; // Stop the original code from executing
                }

                DataBlockVisualPool pool = null;
                if (visualPools.TryGetValue (visualName, out var p1))
                    pool = p1;
                else if (PatchesModManager.assetItemVisualKeysAlt.TryGetValue (visualName, out var visualNameAlt) && !string.IsNullOrEmpty (visualNameAlt))
                {
                    // Main purpose of patching this method: handling special paths like AssetBundleName/PrefabName
                    // If there's a known fallback name, let's try returning it
                    if (visualPools.TryGetValue (visualNameAlt, out var p2))
                    {
                        Debug.Log ($"ModExtensions | EquipmentVisualHelper.RequestVisual ({run}) | Key {visualName} swapped for {visualNameAlt}");
                        visualName = visualNameAlt;
                        pool = p2;
                    }
                }

                if (pool == null)
                {
                    Debug.LogWarning ($"ModExtensions | EquipmentVisualHelper.RequestVisual ({run}) | Failed to find item visual - key {visualName} is not recognized or there is no instance of the visual helper system");
                    __result = null;
                    return false; // Stop the original code from executing
                }

                var instancesAvailableCount = pool.instancesAvailable.Count;
                bool instancesAvailable = instancesAvailableCount > 0;

                if (instancesAvailable)
                {
                    var index = instancesAvailableCount - 1;
                    var instance = pool.instancesAvailable[instancesAvailableCount - 1];
                    pool.instancesAvailable.RemoveAt (index);

                    visualsInUse.Add (instance);
                    instance.gameObject.SetActive (true);

                    if (log && (string.IsNullOrEmpty (logFilter) || visualName.Contains (logFilter)))
                        Debug.Log ($"ModExtensions | EquipmentVisualHelper.RequestVisual | Using preexisting equipment visual {visualName} | Instance ID: {instance.GetInstanceID ()} | Instances now in pool (total, available): {pool.instances.Count}, {pool.instancesAvailable.Count} | Total visuals now in use: {visualsInUse.Count}");
                    
                    __result = instance;
                    return false; // Stop the original code from executing
                }
                else
                {
                    if (pool.instances.Count >= 100)
                    {
                        Debug.LogWarning ($"ModExtensions | EquipmentVisualHelper.RequestVisual | Failed to create new item visual instance: pool {visualName} is at the limit (100)");
                        __result = null;
                        return false; // Stop the original code from executing
                    }

                    var instance = UnityEngine.Object.Instantiate (pool.prefab.gameObject).GetComponent<ItemVisual> ();
                    pool.instances.Add (instance);

                    if (pool.holder == null)
                    {
                        pool.holder = new GameObject (visualName).transform;
                        var th = pool.holder;

                        th.parent = ins.holderPool;
                        th.localPosition = Vector3.zero;
                        th.localRotation = Quaternion.identity;
                        th.localScale = Vector3.one;
                        th.localPosition -= pool.bounds.center;
                        th.gameObject.SetActive (false);
                    }

                    var t = instance.transform;
                    t.name = visualName;
                    t.parent = pool.holder;
                    t.localPosition = (instance.customTransform ? instance.customPosition : Vector3.zero);
                    t.localRotation = (instance.customTransform ? Quaternion.Euler (instance.customRotation) : Quaternion.identity);
                    t.localScale = Vector3.one;

                    foreach (var meshRenderer in instance.renderers)
                    {
                        meshRenderer.SetPropertyBlock (EquipmentVisualHelper.mpb);
                        meshRenderer.gameObject.layer = PhantomBrigade.LayerMasks.unitLayerID;
                    }

                    visualsInUse.Add (instance);
                    instance.gameObject.SetActive (true);

                    if (log && (string.IsNullOrEmpty (logFilter) || visualName.Contains (logFilter)))
                        Debug.Log ($"ModExtensions | EquipmentVisualHelper.RequestVisual | Creating new equipment visual {visualName} | Instance ID: {instance.GetInstanceID ()} | Instances now in pool (total, available): {pool.instances.Count}, {pool.instancesAvailable.Count} | Total visuals now in use: {visualsInUse.Count}");
                    __result = instance;
                }
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | EquipmentVisualHelper.RequestVisual | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }

        
        [HarmonyPatch (typeof (EquipmentVisualHelper), nameof (EquipmentVisualHelper.RequestSubsystemPreview))]
        [HarmonyPrefix]
        public static bool RequestSubsystemPreview (EquipmentEntity subsystem, bool displayFallback = false)
        {
            try
            {
                var ins = EquipmentVisualHelper.ins;

                if (ins == null)
                    return false; // Stop the original code from executing

                ins.body.gameObject.SetActive (false);
                ins.holderPreview.gameObject.SetActive (true);

                EquipmentVisualHelper.ReturnAllVisuals ();

                if (subsystem == null || !subsystem.isSubsystem)
                    return false; // Stop the original code from executing

                var subsystemBlueprint = subsystem.dataLinkSubsystem.data;
                if (subsystemBlueprint == null)
                    return false; // Stop the original code from executing

                bool anyVisualFound = false;

                var visuals = subsystemBlueprint.visualsProcessed;
                if (visuals != null && visuals.Count > 0)
                {
                    foreach (var key in visuals)
                    {
                        var visualInstance = EquipmentVisualHelper.RequestVisual (key);
                        if (visualInstance == null)
                            continue;

                        var t = visualInstance.transform;
                        // t.name = key; // Reason for the patch: do not pollute instance names with keys that might block return
                        t.parent = ins.holderPreview;
                        t.localPosition = (visualInstance.customTransform ? visualInstance.customPosition : Vector3.zero);
                        t.localRotation = (visualInstance.customTransform ? Quaternion.Euler (visualInstance.customRotation) : Quaternion.identity);
                        t.localScale = Vector3.one;
                        anyVisualFound = true;
                    }
                }

                var attachments = subsystemBlueprint.attachmentsProcessed;
                if (attachments != null && attachments.Count > 0)
                {
                    foreach (var kvp in attachments)
                    {
                        var block = kvp.Value;
                        if (block == null)
                            continue;

                        var visualInstance = EquipmentVisualHelper.RequestVisual (block.key);
                        if (visualInstance == null)
                            continue;

                        var t = visualInstance.transform;
                        // t.name = block.key; // Reason for the patch: do not pollute instance names with keys that might block return
                        t.parent = ins.holderPreview;
                        t.localPosition = (visualInstance.customTransform ? visualInstance.customPosition : Vector3.zero) + block.position;
                        t.localRotation = (visualInstance.customTransform ? Quaternion.Euler (visualInstance.customRotation) : Quaternion.identity) * Quaternion.Euler (block.rotation);
                        t.localScale = block.scale;
                        anyVisualFound = true;
                    }
                }

                if (displayFallback && !anyVisualFound)
                    OverworldInspectionSceneHelper.ShowRewardVisual ("battery");
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | EquipmentVisualHelper.RequestVisual | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }
        
        [HarmonyPatch (typeof (EquipmentVisualHelper), "RequestRebuild")]
        [HarmonyPostfix]
        public static void RequestRebuild_Postfix ()
        {
            Debug.Log ($"ModExtensions | EquipmentVisualHelper.RequestRebuild");
        }

        [HarmonyPatch (typeof (EquipmentVisualHelper), "RebuildLookup")]
        [HarmonyPostfix]
        public static void RebuildLookup_Postfix ()
        {
            var visualPools = EquipmentVisualHelper.visualPools;
            Debug.Log ($"ModExtensions | EquipmentVisualHelper.RebuildLookup | Finished with {visualPools.Count} pools");

            foreach (var kvp in visualPools)
            {
                if (kvp.Key.Contains ("mod"))
                    Debug.Log ($"- {kvp.Key}: {kvp.Value.prefab.name}");
            }
        }
    }
    
    */
}