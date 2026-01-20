using System;
using System.Collections.Generic;
using Area;
using HarmonyLib;
using PhantomBrigade.Data;
using PhantomBrigade.Mods;
using UnityEngine;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesModManager
    {
        public static Dictionary<string, CombatSegmentLinker> assetLookupSegments = new Dictionary<string, CombatSegmentLinker> ();
        public static Dictionary<string, AssetLinker> assetLookupPools = new Dictionary<string, AssetLinker> ();
        public static Dictionary<int, AreaProp> assetLookupProps = new Dictionary<int, AreaProp> ();
        
        public static List<ItemVisual> assetItemVisuals = new List<ItemVisual> ();
        public static Dictionary<string, string> assetItemVisualKeysAlt = new Dictionary<string, string> ();
        
        [HarmonyPatch (typeof (ModManager), "LoadModsFinish")]
        [HarmonyPostfix]
        public static void LoadModsFinish ()
        {
            assetLookupSegments.Clear ();
            assetLookupPools.Clear ();
            assetLookupProps.Clear ();
            assetItemVisuals.Clear ();
            assetItemVisualKeysAlt.Clear ();

            var prefabExtension = ".prefab";
            foreach (var modLoadedData in ModManager.loadedMods)
            {
                if (modLoadedData.assetBundles == null || !modLoadedData.metadata.includesAssetBundles)
                    continue;
                
                if (modLoadedData.metadata == null || string.IsNullOrEmpty (modLoadedData.metadata.id))
                    continue;
                
                foreach (var assetBundle in modLoadedData.assetBundles)
                {
                    var assetPaths = assetBundle.GetAllAssetNames ();
                    foreach (var assetPath in assetPaths)
                    {
                        if (!assetPath.EndsWith (prefabExtension))
                            continue;

                        var asset = assetBundle.LoadAsset (assetPath);
                        if (asset == null || asset is GameObject == false)
                            continue;

                        var prefab = (GameObject)asset;
                        
                        var componentSegment = prefab.GetComponent<CombatSegmentLinker> ();
                        var componentPool = prefab.GetComponent<AssetLinker> ();
                        var componentProp = prefab.GetComponent<AreaProp> ();
                        var componentItem = prefab.GetComponent<ItemVisual> ();
                        
                        if (componentSegment != null)
                        {
                            var path = $"{modLoadedData.metadata.id}/{prefab.name}";
                            assetLookupSegments[path] = componentSegment;
                            Debug.Log ($"Mod {modLoadedData.metadata.id} | ModExtensions | Loaded new AreaSegment asset from bundle {assetBundle.name} | Registered path: {path}");
                        }
                        else if (componentPool != null)
                        {
                            var path = $"{modLoadedData.metadata.id}/{prefab.name}";
                            assetLookupPools[path] = componentPool;
                            Debug.Log ($"Mod {modLoadedData.metadata.id} | ModExtensions | Loaded new AssetPool asset from bundle {assetBundle.name} | Registered path: {path}");
                        }
                        else if (componentProp != null)
                        {
                            assetLookupProps[componentProp.id] = componentProp;
                            Debug.Log ($"Mod {modLoadedData.metadata.id} | ModExtensions | Loaded new AreaProp asset from bundle {assetBundle.name} | Name: {prefab.name} | ID: {componentProp.id}");
                        }
                        else if (componentItem != null)
                        {
                            var visualKeyAlt = $"{assetBundle.name}/{prefab.name}";
                            assetItemVisualKeysAlt.Add (visualKeyAlt, prefab.name);
                            assetItemVisuals.Add (componentItem);
                            Debug.Log ($"Mod {modLoadedData.metadata.id} | ModExtensions | Loaded new ItemVisual asset from bundle {assetBundle.name} | Name: {prefab.name} | Alt. key: {visualKeyAlt}");
                        }
                    }
                }
            }

            var areaList = DataMultiLinkerCombatArea.GetDataList ();
            foreach (var area in areaList)
            {
                if (area == null || area.segments == null)
                    continue;

                foreach (var segment in area.segments)
                {
                    if (segment == null || segment.prefab != null || string.IsNullOrEmpty (segment.path))
                        continue;
                    
                    if (assetLookupSegments.TryGetValue (segment.path, out var prefab))
                    {
                        Debug.Log ($"ModExtensions | Area {area.key} linked to segment prefab with path {segment.path}");
                        segment.prefab = prefab.gameObject;
                    }
                }
            }
            
            var poolList = DataMultiLinkerAssetPools.GetDataList ();
            foreach (var pool in poolList)
            {
                if (pool == null || pool.prefab != null || string.IsNullOrEmpty (pool.path))
                    continue;
                    
                if (assetLookupPools.TryGetValue (pool.path, out var prefab))
                {
                    Debug.Log ($"ModExtensions | Pooled asset {pool.key} linked to segment prefab with path {pool.path}");
                    pool.prefab = prefab;
                }
            }

            var methodInfoRegisterProp = ModUtilities.GetPrivateMethodInfo (typeof (AreaAssetHelper), "RegisterPropPrototype", true, false);
            if (methodInfoRegisterProp != null)
            {
                AreaAssetHelper.CheckResources ();
                foreach (var kvp in assetLookupProps)
                {
                    int id = kvp.Key;
                    var prop = kvp.Value;
                    methodInfoRegisterProp.Invoke (null, new object [] { prop });
                    Debug.Log ($"ModExtensions | Registered area prop {prop.name} with {id} in the area asset system");
                }
            }

            LoadItemVisuals ();
        }

        private static readonly string itemVisualPrefabsPath = "Content/Items";
        
        private static void LoadItemVisuals ()
        {
            // Ensure calls to CheckDatabase do not re-run LoadVisuals
            var type = typeof (ItemHelper);
            var autoloadAttemptedField = ModUtilities.GetPrivateFieldInfo (type, "autoloadAttempted", true, false);
            if (autoloadAttemptedField != null)
                autoloadAttemptedField.SetValue (null, true);
            
            try
            {
                ResourceDatabaseContainer resourceDatabase = ResourceDatabaseManager.GetDatabase ();
        
                ItemHelper.itemVisualPrefabs = new Dictionary<string, ItemVisual> ();
                var itemVisualPrefabs = ItemHelper.itemVisualPrefabs;
                var itemVisualFileEntries = new List<ResourceDatabaseEntryRuntime> ();

                if (!resourceDatabase.entries.ContainsKey (itemVisualPrefabsPath))
                {
                    Debug.LogWarning ($"Failed to find item visual path {itemVisualPrefabsPath} in the resource DB!");
                    return;
                }
                
                var visualInfoDir = ResourceDatabaseManager.GetEntryByPath (itemVisualPrefabsPath);
                ResourceDatabaseManager.FindResourcesRecursive (itemVisualFileEntries, visualInfoDir, 1, ResourceDatabaseEntrySerialized.Filetype.Prefab);
                
                Debug.Log ($"ModExtensions | Executing replacement method for ItemHelper.LoadVisuals | Built-in prefab files: {itemVisualFileEntries.Count} | Sideloaded assets: {assetItemVisuals.Count}");
                
                for (int i = 0; i < itemVisualFileEntries.Count; ++i)
                {
                    var entry = itemVisualFileEntries[i];
                    var prefab = entry.GetContent<GameObject> ();
                    if (prefab == null)
                        continue;
                    
                    var component = prefab.GetComponent<ItemVisual> ();
                    if (component == null)
                        continue;

                    itemVisualPrefabs[prefab.name] = component;
                }

                foreach (var component in assetItemVisuals)
                {
                    Debug.Log ($"ModExtensions | Registered sideloaded ItemVisual prefab {component.name}");
                    itemVisualPrefabs[component.name] = component;
                }
                
                EquipmentVisualHelper.RequestRebuild ();
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | ItemHelperLoadVisuals | Exiting replacement method, exception encountered:\n{e.Message}");
            }
        }
    }
}