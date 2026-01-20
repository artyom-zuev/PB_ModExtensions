using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesUnitVisualManagerInventory
    {
        private static bool reflectionInitialized = false;
        private static bool reflectionSuccess = false;

        private static FieldInfo fieldInitialized;
        private static FieldInfo fieldHardpointLinks;
        private static FieldInfo fieldSocketLinks;
        
        private static MethodInfo methodInitialize;
        private static MethodInfo methodVisualizeElement;
        
        private const string keyPrefixBaseVisual = "base_";
        private static object[] argsVisualizeElement = new object[7];
        
        [HarmonyPatch (typeof (UnitVisualManagerInventory), nameof(UnitVisualManagerInventory.VisualizePart))]
        [HarmonyPrefix]
        private static bool VisualizePart (UnitVisualManagerInventory __instance, EquipmentEntity part)
        {
            if (reflectionInitialized && !reflectionSuccess)
                return true;

            try
            {
                var view = __instance;
                
                if (!reflectionInitialized)
                {
                    reflectionInitialized = true;
                    var type = typeof (UnitVisualManagerInventory);
                    
                    fieldInitialized = ModUtilities.GetPrivateFieldInfo (type, "initalized", false);
                    fieldHardpointLinks = ModUtilities.GetPrivateFieldInfo (type, "hardpointLinks", false);
                    fieldSocketLinks = ModUtilities.GetPrivateFieldInfo (type, "socketLinks", false);
                    
                    methodInitialize = ModUtilities.GetPrivateMethodInfo (type, "Initialize", false);
                    methodVisualizeElement = ModUtilities.GetPrivateMethodInfo (type, "VisualizeElement", false);
                    
                    reflectionSuccess = true;
                }
                
                if (part == null || !part.isPart)
                    return false;

                bool initalized = fieldInitialized.GetFieldInfoValue<bool> (view);
                if (!initalized)
                    methodInitialize.Invoke (view, null);

                var socketLinks = fieldSocketLinks.GetFieldInfoValue<SortedDictionary<string, UnitVisualManagerInventory.SocketLink>> (view);
                var hardpointLinks = fieldHardpointLinks.GetFieldInfoValue<SortedDictionary<string, SortedDictionary<string, UnitVisualManagerInventory.HardpointLink>>> (view);
                if (socketLinks == null || hardpointLinks == null)
                    return false;
                
                foreach (var kvp in socketLinks)
                {
                    var socketLinkOther = kvp.Value;
                    if (socketLinkOther.body != null)
                        socketLinkOther.body.gameObject.SetActive (false);
                }

                var partBlueprint = part.partBlueprint;
                var socket = partBlueprint.sockets.FirstOrDefault ();
                if (part.hasPartParentUnit)
                    socket = part.partParentUnit.socket;

                if (socket == LoadoutSockets.leftEquipment)
                    socket = LoadoutSockets.rightEquipment;

                if (socket == LoadoutSockets.leftOptionalPart)
                    socket = LoadoutSockets.rightOptionalPart;

                if (!hardpointLinks.ContainsKey (socket) || !socketLinks.ContainsKey (socket))
                {
                    Debug.LogWarning ($"UVI | Failed to find socket link {socket}");
                    return false;
                }

                var hardpointLinksInSocket = hardpointLinks[socket];
                var socketLink = socketLinks[socket];
                if (socketLink.body != null)
                    socketLink.body.gameObject.SetActive (true);
                
                var subsystems = EquipmentUtility.GetSubsystemsInPart (part);
                
                foreach (var subsystem in subsystems)
                {
                    var subsystemBlueprint = subsystem.dataLinkSubsystem.data;
                    // Debug.LogWarning ($"UVI | Applying subsystem {socket}/{subsystem.subsystemParentPart.hardpoint}: {subsystemBlueprintName}");
                    
                    var hardpoint = subsystem.subsystemParentPart.hardpoint;
                    var hardpointInfo = DataMultiLinkerSubsystemHardpoint.GetEntry (hardpoint);
                    
                    if (hardpointInfo == null)
                    {
                        Debug.LogWarning ($"UVI | Failed to find hardpoint info {hardpoint}");
                        continue;
                    }
                    
                    if (!hardpointLinksInSocket.ContainsKey (hardpoint))
                        continue;
                    
                    var hardpointLink = hardpointLinksInSocket[hardpoint];
                    foreach (var meshRendererBase in hardpointLink.meshRenderersBase)
                    {
                        if (meshRendererBase != null)
                            meshRendererBase.enabled = true;
                    }
                    
                    var holders = hardpointLink.holders;
                    if (holders == null || holders.Count == 0)
                    {
                        // Temporary way to filter out internal warnings
                        if (!hardpointInfo.isInternal)
                            Debug.LogWarning ($"UVI | Failed to get holders (null or empty collection) for subsystem {subsystemBlueprint.key} meant for hardpoint {socket}/{hardpoint}");
                        continue;
                    }
                    
                    var visuals = subsystemBlueprint.visualsProcessed;
                    if (visuals != null && visuals.Count > 0)
                    {
                        for (int i = 0; i < subsystemBlueprint.visualsProcessed.Count; ++i)
                        {
                            var visualName = visuals[i];
                            var visualIndex = hardpointLink.holderPerVisual ? i : 0;
                            
                            if (!visualIndex.IsValidIndex (holders))
                            {
                                Debug.LogWarning ($"Subsystem {subsystemBlueprint.key} can't be visualized in hardpoint {hardpoint} | Used index: {visualIndex} | Holder count: {holders.Count} | Holder per visual: {hardpointLink.holderPerVisual}");
                                continue;
                            }

                            var holder = holders[visualIndex];
                            if (holder == null)
                            {
                                Debug.LogWarning ($"Subsystem {subsystemBlueprint.key} could not add a visual {i} to hardpoint {hardpoint} due to holder at that index being null");
                                continue;
                            }
                            
                            var mrBase = visualIndex.IsValidIndex (hardpointLink.meshRenderersBase) ? hardpointLink.meshRenderersBase[visualIndex] : null;
                            VisualizeElement (view, visualName, holder, Vector3.zero, Vector3.zero, Vector3.one, mrBase, false);
                        }
                    }
                    
                    var attachments = subsystemBlueprint.attachmentsProcessed;
                    if (attachments != null && attachments.Count > 0)
                    {
                        var holder = hardpointLink.holders[0];
                        if (holder == null)
                            continue;
                        
                        foreach (var kvp in attachments)
                        {
                            var block = kvp.Value;
                            if (block == null)
                                continue;
                            
                            var visualName = block.key;
                            int visualIndex = -1;
                            if (kvp.Key.StartsWith (keyPrefixBaseVisual))
                            {
                                var keyTrimmed = kvp.Key.Substring (keyPrefixBaseVisual.Length);
                                if (int.TryParse (keyTrimmed, out int i))
                                    visualIndex = i;
                            }

                            var mrBase = visualIndex.IsValidIndex (hardpointLink.meshRenderersBase) ? hardpointLink.meshRenderersBase[visualIndex] : null;
                            VisualizeElement (view, visualName, holder, block.position, block.rotation, block.scale, mrBase, block.centered);
                        }
                    }
                }
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
        
        private static void VisualizeElement
        (
            UnitVisualManagerInventory view,
            string visualKey, 
            Transform holder, 
            Vector3 position, 
            Vector3 rotation, 
            Vector3 scale, 
            MeshRenderer mrBase, 
            bool centered
        )
        {
            argsVisualizeElement[0] = visualKey;
            argsVisualizeElement[1] = holder;
            argsVisualizeElement[2] = position;
            argsVisualizeElement[3] = rotation;
            argsVisualizeElement[4] = scale;
            argsVisualizeElement[5] = mrBase;
            argsVisualizeElement[6] = centered;
            methodVisualizeElement.Invoke (view, argsVisualizeElement);
        }
    }
}