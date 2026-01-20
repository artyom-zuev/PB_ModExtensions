using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesUnitVisualManager
    {
        private static bool reflectionInitialized = false;
        private static bool reflectionSuccess = false;
        
        private static FieldInfo fieldSocketVisualsLookup;
        private static FieldInfo fieldHardpointGroupLinks;
        private static FieldInfo fieldUnitPersistentID;
        private static FieldInfo fieldRenderersDisposable;
        
        private static MethodInfo methodClearSubsystemVisuals;
        private static MethodInfo methodVisualizeElement;

        private const string keyPrefixBaseVisual = "base_";
        private static object[] argsClearSubsystemVisuals = new object[3];
        private static object[] argsVisualizeElement = new object[15];
        
        private static bool log = true;
        private static StringBuilder sb = new StringBuilder ();
        
        
        [HarmonyPatch (typeof (UnitVisualManager), nameof(UnitVisualManager.UpdateSubsystemVisual))]
        [HarmonyPrefix]
        private static bool UpdateSubsystemVisual 
        (
            UnitVisualManager __instance,
            string socket,
            string hardpoint,
            EquipmentEntity part,
            EquipmentEntity subsystem,
            bool partHighlight = false
        )
        {
            // Escape to normal method if reflection setup failed once
            if (reflectionInitialized && !reflectionSuccess)
                return true;
            
            try
            {
                var view = __instance;

                if (!reflectionInitialized)
                {
                    reflectionInitialized = true;
                    var type = typeof (UnitVisualManager);
                    
                    fieldSocketVisualsLookup = ModUtilities.GetPrivateFieldInfo (type, "socketVisualsLookup", false);
                    fieldHardpointGroupLinks = ModUtilities.GetPrivateFieldInfo (type, "hardpointGroupLinks", false);
                    fieldUnitPersistentID = ModUtilities.GetPrivateFieldInfo (type, "unitPersistentID", false);
                    methodClearSubsystemVisuals = ModUtilities.GetPrivateMethodInfo (type, "ClearSubsystemVisuals", false);
                    methodVisualizeElement = ModUtilities.GetPrivateMethodInfo (type, "VisualizeElement", false);
                    fieldRenderersDisposable = ModUtilities.GetPrivateFieldInfo (type, "renderersDisposable", false);
                    
                    reflectionSuccess = true;
                }
                
                var socketVisualsLookup = fieldSocketVisualsLookup.GetFieldInfoValue<Dictionary<string, UnitSocketVisualMech>> (view);
                var hardpointGroupLinks = fieldHardpointGroupLinks.GetFieldInfoValue<Dictionary<string, Dictionary<string, HardpointGroupLinkMech>>> (view);
                var unitPersistentID = fieldUnitPersistentID.GetFieldInfoValue<int> (view);
                var renderersDisposable = fieldRenderersDisposable.GetFieldInfoValue <List<MeshRenderer>> (view);
                
                if (string.IsNullOrEmpty (socket) || string.IsNullOrEmpty (hardpoint))
                    return false;
                
                if (socketVisualsLookup == null || !socketVisualsLookup.TryGetValue (socket, out var socketLink) || socketLink == null)
                    return false;
                
                if (!hardpointGroupLinks.TryGetValue (socket, out var hardpointGroupLinksInSocket) || hardpointGroupLinksInSocket == null)
                {
                    Debug.LogWarning ($"Failed to find the socket {socket} in the mech visual instance link collection");
                    return false;
                }
                
                var hardpointInfo = DataMultiLinkerSubsystemHardpoint.GetEntry (hardpoint, false);
                if (hardpointInfo == null)
                {
                    Debug.LogWarning ($"Failed to find hardpoint info {hardpoint}");
                    return false;
                }

                var hardpointGroupKey = hardpointInfo.visualGroup;
                if (string.IsNullOrEmpty (hardpointGroupKey))
                {
                    // Debug.LogWarning ($"Hardpoint info for {hardpoint} contains no valid group key, unable to display any subsystems belonging to that hardpoint");
                    return false;
                }

                if (!hardpointGroupLinksInSocket.TryGetValue (hardpointGroupKey, out var hardpointGroupLink) || hardpointGroupLink == null)
                {
                    if (!hardpointInfo.isInternal)
                        Debug.LogWarning ($"Failed to find the hardpoint group {socket}/{hardpointGroupKey} (from hardpoint {hardpoint}), unable to display any subsystems belonging to that hardpoint");
                    return false;
                }

                argsClearSubsystemVisuals[0] = socket;
                argsClearSubsystemVisuals[1] = hardpoint;
                argsClearSubsystemVisuals[2] = hardpointGroupLink;
                methodClearSubsystemVisuals.Invoke (view, argsClearSubsystemVisuals);

                if (!socketLink.visuals.ContainsKey (hardpoint))
                    socketLink.visuals.Add (hardpoint, new List<ItemVisual> ());
                
                var visualsList = socketLink.visuals[hardpoint];
                visualsList.Clear ();
                
                if (subsystem == null)
                    return false;
                
                var subsystemBlueprint = subsystem.dataLinkSubsystem.data;
                
                var visuals = subsystemBlueprint.visualsProcessed;
                bool visualsPresent = visuals != null && visuals.Count > 0;
                
                var attachments = subsystemBlueprint.attachmentsProcessed;
                bool attachmentsPresent = attachments != null && attachments.Count > 0;
                
                if (!visualsPresent && !attachmentsPresent)
                    return false;
                
                var holders = hardpointGroupLink.visualHolders;
                if (holders == null || holders.Count == 0)
                {
                    // Temporary way to filter out internal warnings
                    if (!hardpointInfo.isInternal)
                        Debug.LogWarning ($"Failed to get holders (null or empty collection) for subsystem {subsystemBlueprint.key} meant for hardpoint {socket}/{hardpoint} in the mech visual instance {view.transform.parent.name}");
                    return false;
                }

                bool baseHidden = subsystemBlueprint.IsFlagPresent (PartCustomFlagKeys.VisualBaseHidden);
                bool combinerUsed = view.combinerUsed;

                if (log)
                {
                    sb.Clear ();
                    sb.Append ($"ModExtensions | UnitVisualManager.UpdateSubsystemVisual | {subsystemBlueprint.key} | Visuals: {visuals?.Count} | Attachments: {attachments?.Count}");
                }
                
                if (visualsPresent)
                {
                    for (int i = 0; i < visuals.Count; ++i)
                    {
                        var visualName = visuals[i];
                        var visualIndex = hardpointGroupLink.holderPerVisual ? i : 0;

                        if (log)
                            sb.Append ($"\n- V{visualIndex}: {visualName}");
                        
                        VisualizeElement
                        (
                            view, visualName, subsystemBlueprint.key, socket, hardpointInfo, hardpointGroupLink, visualIndex,
                            Vector3.zero, Vector3.zero, Vector3.one, visualsList, renderersDisposable,
                            socketLink.activationLinks, combinerUsed, false, true, baseHidden
                        );
                    }
                }
                
                if (attachmentsPresent)
                {
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
                        
                        if (log)
                            sb.Append ($"\n- A{visualIndex}: {visualName}");
                        
                        VisualizeElement
                        (
                            view, visualName, subsystemBlueprint.key, socket, hardpointInfo, hardpointGroupLink, visualIndex,
                            block.position, block.rotation, block.scale, visualsList, renderersDisposable,
                            socketLink.activationLinks, combinerUsed, block.centered, block.activated, baseHidden
                        );
                    }
                }
                
                if (!combinerUsed)
                {
                    var unitPersistent = IDUtility.GetPersistentEntity (unitPersistentID);
                    view.ApplyMaterialToSubsystem (unitPersistent, socket, hardpoint, part, subsystem, partHighlight: partHighlight);
                }
                
                if (log)
                    Debug.Log (sb.ToString ());
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | UnitVisualManager.UpdateSubsystemVisual | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }

        private static void VisualizeElement
        (
            UnitVisualManager view,
            string visualName,
            string subsystemKey,
            string socket,
            DataContainerSubsystemHardpoint hardpointInfo,
            HardpointGroupLink hardpointGroupLink,
            int visualIndex,
            Vector3 position,
            Vector3 rotation,
            Vector3 scale,
            List<ItemVisual> visualsList,
            List<MeshRenderer> renderersDisposable,
            List<ItemActivationLink> activationLinks,
            bool combined,
            bool centered,
            bool activated,
            bool baseHidden
        )
        {
            /*
            argsVisualizeElement[0] = visualKey;
            argsVisualizeElement[1] = subsystemKey;
            argsVisualizeElement[2] = socket;
            argsVisualizeElement[3] = hardpointInfo;
            argsVisualizeElement[4] = hardpointGroupLink;
            argsVisualizeElement[5] = visualIndex;
            argsVisualizeElement[6] = position;
            argsVisualizeElement[7] = rotation;
            argsVisualizeElement[8] = scale;
            argsVisualizeElement[9] = visualsList;
            argsVisualizeElement[10] = activationLinks;
            argsVisualizeElement[11] = combined;
            argsVisualizeElement[12] = centered;
            argsVisualizeElement[13] = activated;
            argsVisualizeElement[14] = baseHidden;
            methodVisualizeElement.Invoke (view, argsVisualizeElement);
            */
            
            // The above would be sufficient if the original VisualizeElement worked as needed,
            // but unfortunately it needs replacement. Since it's not invoked anywhere else,
            // we can just directly implement it here without a separate patch...
            
            if (string.IsNullOrEmpty (visualName) || hardpointInfo == null || hardpointGroupLink == null || visualsList == null)
                return;
            
            var visualHolders = hardpointGroupLink.visualHolders;
            if (visualHolders == null || visualHolders.Count == 0)
            {
                // Temporary way to filter out internal warnings
                if (!hardpointInfo.isInternal)
                    Debug.LogWarning ($"ModExtensions | UnitVisualManager.VisualizeElement | Failed to get holders (null or empty collection) for subsystem {subsystemKey} meant for hardpoint {socket}/{hardpointInfo.key} in the mech visual instance {view.transform.parent.name}");
                return;
            }
            
            var indexUsed = hardpointGroupLink.holderPerVisual ? visualIndex : 0;
            if (!indexUsed.IsValidIndex (visualHolders))
            {
                Debug.LogWarning ($"ModExtensions | UnitVisualManager.VisualizeElement | Subsystem {subsystemKey} can't be visualized in hardpoint {hardpointInfo.key} | Used index: {indexUsed} | Holder count: {visualHolders.Count} | Holder per visual: {hardpointGroupLink.holderPerVisual}");
                return;
            }

            Transform visualHolder = visualHolders[indexUsed];
            if (visualHolder == null)
            {
                Debug.LogWarning ($"ModExtensions | UnitVisualManager.VisualizeElement | Subsystem {subsystemKey} could not add a visual {visualHolder} to hardpoint {hardpointInfo.key} due to holder {indexUsed} being null");
                return;
            }

            ItemVisual visualInstance = null;
            
            if (combined)
            {
                var visualPrefab = ItemHelper.GetVisual (visualName);
                if (visualPrefab == null)
                {
                    Debug.LogWarning ($"ModExtensions | UnitVisualManager.VisualizeElement | Subsystem {subsystemKey} failed to fetch visual prefab {visualIndex} named {visualName}");
                    return;
                }
            
                visualInstance = UnityEngine.Object.Instantiate (visualPrefab.gameObject).GetComponent<ItemVisual> ();
                visualInstance.name = visualPrefab.name;
                visualInstance.CheckInitialization ();
            }
            else
            {
                visualInstance = EquipmentVisualHelper.RequestVisual (visualName);
            }
            
            if (visualInstance == null)
            {
                Debug.LogWarning ($"ModExtensions | UnitVisualManager.VisualizeElement | Subsystem {subsystemKey} failed to instantiate visual prefab named {visualName}");
                return;
            }

            visualsList.Add (visualInstance);
            var t = visualInstance.transform;
            var rotationQt = Quaternion.Euler (rotation);

            if (centered)
            {
                if (visualInstance.renderers != null && visualInstance.renderers.Count > 0)
                {
                    var bounds = visualInstance.GetRendererBounds ();
                    var centerScaled = new Vector3 (bounds.center.x * scale.x, bounds.center.y * scale.y, bounds.center.z * scale.z);
                    position -= rotationQt * centerScaled;
                }
            }
            
            // Reason for patching: avoid polluting the instance name with visual key, which might block return
            // t.name = visualKey;
            t.parent = visualHolder;
            t.localPosition = (visualInstance.customTransform ? visualInstance.customPosition : Vector3.zero) + position;
            t.localRotation = (visualInstance.customTransform ? Quaternion.Euler (visualInstance.customRotation) : Quaternion.identity) * Quaternion.Euler (rotation);
            t.localScale = scale;

            // visualInstance.liveryOffset = 0;
            
            if (visualInstance.headlightOverrideTransform != null && view.lightManager != null && view.lightManager.headHolder != null)
                view.lightManager.headHolder.transform.position = visualInstance.headlightOverrideTransform.position;

            if (visualInstance.podOverrideTransform != null && string.Equals (socket, LoadoutSockets.corePart))
            {
                if (view.podBone != null)
                {
                    view.podBone.position = visualInstance.podOverrideTransform.position;
                    view.podBone.rotation = visualInstance.podOverrideTransform.rotation;
                }
            }

            if (visualInstance.attachmentsLanding != null && visualInstance.attachmentsLanding.Count > 0)
            {
                var backParent = visualInstance.attachmentsLanding[0].holder;
                if (view.backBone != null && backParent != null)
                {
                    view.backBone.position = backParent.position;
                    view.backBone.rotation = backParent.rotation;
                }
            }
            
            if (hardpointGroupLink.visualsAffectBody && visualIndex.IsValidIndex (hardpointGroupLink.renderersBase))
            {
                var rendererBase = hardpointGroupLink.renderersBase[visualIndex];
                bool baseVisible = !visualInstance.includesBase && !baseHidden;
                rendererBase.gameObject.SetActive (baseVisible);
            }
            
            if (activated && activationLinks != null && visualInstance.activationLinks != null && visualInstance.activationLinks.Count > 0)
            {
                for (int i = 0, iLimit = visualInstance.activationLinks.Count; i < iLimit; ++i)
                {
                    var activationLink = visualInstance.activationLinks[i];
                    if (activationLinks == null)
                        continue;

                    if (activationLinks.Contains (activationLink))
                        continue;
                    
                    activationLinks.Add (activationLink);
                }
            }
        }
    }
}