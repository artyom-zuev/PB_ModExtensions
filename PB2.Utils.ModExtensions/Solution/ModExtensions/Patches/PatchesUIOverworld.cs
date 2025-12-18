using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Entitas;
using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Overworld;

namespace ModExtensions
{
    [HarmonyPatch]
    public class PatchesUIOverworld
    {
        private static IGroup<OverworldEntity> missionEntityGroup = null;
        private static List<OverworldEntity> missionEntityBuffer = new List<OverworldEntity> ();
        private static StringBuilder sb = new StringBuilder ();

        [HarmonyPatch (typeof (CIViewOverworldProcess), "RedrawMissions")]
        [HarmonyPrefix]
        public static bool RedrawMissions (CIViewOverworldProcess __instance)
        {
            var view = __instance;

            // ModUtilities.GetPrivate* methods can throw an exception if a field or method is not found.
            // That means we don't have to write a verbose set of early returns checking every single FieldInfo and MethodInfo for nulls.
            // We can just immediately use everything we get from ModUtilities.GetPrivate* methods and assume it resolved successfully.
            try
            {
                // First, use ModUtilities to fetch dependencies
                var missionInstancesField = ModUtilities.GetPrivateFieldInfo (view, "missionInstances", false, true);
                var missionInstances = missionInstancesField.GetFieldInfoValue<Dictionary<int, CIHelperOverworldSidebarMission>> (view);

                var missionPoolField = ModUtilities.GetPrivateFieldInfo (view, "missionPool", false, true);
                var missionPool = missionPoolField.GetFieldInfoValue<List<CIHelperOverworldSidebarMission>> (view);

                var questKeyLastField = ModUtilities.GetPrivateFieldInfo (view, "questKeyLast", false, true);
                var questKeyLast = questKeyLastField.GetFieldInfoValue<string> (view);

                var baseSpeedMethod = ModUtilities.GetPrivateMethodInfo (view, "GetBaseSpeed", false, true);
                var speedBase = baseSpeedMethod.GetMethodInfoOutput<float> (view, null);

                var layoutRedrawRequestedField = ModUtilities.GetPrivateFieldInfo (view, "layoutRedrawRequested", false, true);

                var onMissionClickMethod = ModUtilities.GetPrivateMethodInfo (view, "OnMissionClick", false, true);
                var onMissionClickSecondaryMethod = ModUtilities.GetPrivateMethodInfo (view, "OnMissionClickSecondary", false, true);
                var onMissionHoverStartMethod = ModUtilities.GetPrivateMethodInfo (view, "OnMissionHoverStart", false, true);
                var onMissionHoverEndMethod = ModUtilities.GetPrivateMethodInfo (view, "OnMissionHoverEnd", false, true);
                var redrawMissionMethod = ModUtilities.GetPrivateMethodInfo (view, "RedrawMission", false, true);
                var refreshColliderListMethod = ModUtilities.GetPrivateMethodInfo (view, "RefreshColliderList", false, true);

                var overworld = Contexts.sharedInstance.overworld;
                if (missionEntityGroup == null)
                {
                    missionEntityGroup = overworld.GetGroup
                    (
                        OverworldMatcher.AllOf
                        (
                            OverworldMatcher.DataLinkPointPreset,
                            OverworldMatcher.CombatDescriptionLink,
                            OverworldMatcher.Position
                        ).NoneOf
                        (
                            OverworldMatcher.Destroyed
                        )
                    );
                }

                var baseOverworld = IDUtility.playerBaseOverworld;
                var posBase = baseOverworld.position.v;

                missionInstances.Clear ();
                UIHelper.PrepareForPoolIteration (ref missionPool, out int poolSizeLast, out int poolInstanceIndex, out int poolInstancesUsed);

                int entityIndex = 0;

                var entitiesQuestLinked = !string.IsNullOrEmpty (questKeyLast) ? overworld.GetEntitiesWithQuestLink (questKeyLast) : null;

                var missionHolder = view.missionWidget.transform;
                var missionPrefab = view.missionPrefab;

                var OnMissionClick = onMissionClickMethod.GetActionFromMethodInfo<int> (view);
                var OnMissionClickSecondary = onMissionClickSecondaryMethod.GetActionFromMethodInfo<int> (view);
                var OnMissionHoverStart = onMissionHoverStartMethod.GetActionFromMethodInfo<int> (view);
                var OnMissionHoverEnd = onMissionHoverEndMethod.GetActionFromMethodInfo<int> (view);

                var RedrawMission = redrawMissionMethod.GetActionFromMethodInfo<CIHelperOverworldSidebarMission, OverworldEntity, float> (view);
                var RefreshColliderList = refreshColliderListMethod.GetActionFromMethodInfo (view);

                // Next, fetch custom settings
                int drawLimit = 3;
                bool drawFallbackAnyCombatDesc = false;
                bool drawOnlyWithDisplayMemory = false;

                if (IDUtility.IsGameLoaded () && IDUtility.playerBasePersistent != null)
                {
                    var basePersistent = IDUtility.playerBasePersistent;
                    if (basePersistent.TryGetMemoryRounded ("mod_ext_missions_list_limit", out var v1))
                        drawLimit = v1;
                    if (basePersistent.TryGetMemoryRounded ("mod_ext_missions_list_unlinked", out var v2) && v2 > 0)
                        drawFallbackAnyCombatDesc = true;
                    if (basePersistent.TryGetMemoryRounded ("mod_ext_missions_list_memory_exclusive", out var v3) && v3 > 0)
                        drawOnlyWithDisplayMemory = true;
                }

                List<OverworldEntity> entitiesFinal = null;
                if (entitiesQuestLinked != null && entitiesQuestLinked.Count > 0)
                {
                    missionEntityBuffer.Clear ();
                    missionEntityBuffer.AddRange (entitiesQuestLinked);
                    entitiesFinal = missionEntityBuffer;
                }
                else if (drawFallbackAnyCombatDesc)
                {
                    var entitiesCombatDesc = missionEntityGroup.GetEntities (missionEntityBuffer);
                    entitiesFinal = entitiesCombatDesc;
                }

                var questsActive = overworld.hasQuestsActive ? overworld.questsActive.s : null;
                int questsCount = questsActive != null ? questsActive.Count : 0;
                bool questsVisible = questsCount > 0;
                HashSet<string> memoryDisplayed = null;

                if (questsActive != null && questsVisible && drawOnlyWithDisplayMemory)
                {
                    if (string.IsNullOrEmpty (questKeyLast) || !questsActive.ContainsKey (questKeyLast))
                        questKeyLast = questsActive.Keys.First ();

                    var questState = questsActive[questKeyLast];
                    var questData = DataMultiLinkerOverworldQuest.GetEntry (questKeyLast, false);
                    var steps = questData?.stepsProc;
                    var stepData = questState != null && steps != null && !string.IsNullOrEmpty (questState.step) && steps.TryGetValue (questState.step, out var s) ? s : null;

                    if (stepData != null)
                    {
                        if (stepData.memoryDisplayed != null && stepData.memoryDisplayed.Count > 0)
                            memoryDisplayed = stepData.memoryDisplayed;
                    }
                }

                if (entitiesFinal != null && entitiesFinal.Count > 0)
                {
                    foreach (var entityOverworld in entitiesFinal)
                    {
                        var entityPersistent = IDUtility.GetLinkedPersistentEntity (entityOverworld);
                        if (entityPersistent == null || entityPersistent.isDestroyed)
                            continue;

                        if (drawOnlyWithDisplayMemory && memoryDisplayed != null)
                        {
                            bool anyMemoryFound = false;
                            foreach (var memoryKey in memoryDisplayed)
                            {
                                if (entityPersistent.TryGetMemoryFloat (memoryKey, out var v))
                                {
                                    anyMemoryFound = true;
                                    break;
                                }
                            }

                            if (!anyMemoryFound)
                                continue;
                        }

                        var instance = UIHelper.GetInstanceFromPool
                        (
                            missionPool,
                            missionPrefab,
                            missionHolder,
                            poolSizeLast,
                            ref poolInstanceIndex,
                            ref poolInstancesUsed
                        );

                        int id = entityOverworld.id.id;
                        missionInstances.Add (id, instance);

                        instance.name = id.ToString ();
                        instance.transform.localPosition = new Vector3 (0f, -entityIndex * view.missionSpacing, 0f);

                        UIHelper.ReplaceCallbackInt (ref instance.button.callbackOnClick, OnMissionClick, id);
                        UIHelper.ReplaceCallbackInt (ref instance.button.callbackOnHoverStart, OnMissionHoverStart, id);
                        UIHelper.ReplaceCallbackInt (ref instance.button.callbackOnHoverEnd, OnMissionHoverEnd, id);
                        UIHelper.ReplaceCallbackInt (ref instance.button.callbackOnClickSecondary, OnMissionClickSecondary, id);

                        sb.Clear ();
                        sb.Append ("Overworld: ");
                        sb.Append (entityOverworld.ToLog ());
                        sb.Append ($"\nPersistent: ");
                        sb.Append (entityPersistent.ToLog ());
                        sb.Append ($"\nIndex: ");
                        sb.Append (entityIndex);

                        instance.button.tooltipUsed = true;
                        instance.button.tooltipOffset = new Vector3 (328f, 0f, 0f);
                        instance.button.tooltipPivot = UIWidget.Pivot.TopLeft;
                        instance.button.tooltipFromLibrary = false;
                        instance.button.tooltipHeader = string.Empty;
                        instance.button.tooltipContent = sb.ToString ();

                        RedrawMission (instance, entityOverworld, speedBase);
                        entityIndex += 1;

                        if (entityIndex >= drawLimit)
                            break;
                    }
                }

                UIHelper.HideUnusedPoolInstances (missionPool, poolInstancesUsed);
                RefreshColliderList ();

                bool missionsVisible = missionInstances.Count > 0;
                view.missionHideable.SetVisible (missionsVisible);

                if (missionsVisible)
                    view.missionWidget.height = entityIndex * view.missionSpacing;

                Debug.Log ($"ModExtensions | RedrawMissions (Patched) | Created instances: {missionInstances.Count} | Limit: {drawLimit} | Any combat site: {drawFallbackAnyCombatDesc} | Draw only with display memory: {drawOnlyWithDisplayMemory} | Memory displayed: {memoryDisplayed.ToStringFormatted ()}");
                layoutRedrawRequestedField.SetValue (view, true);
            }
            catch (Exception e)
            {
                Debug.LogError ($"ModExtensions | RedrawMissions | Skipping patch, exception encountered:\n{e.Message}");
                // Execute original method
                return true;
            }

            // Stop original method from executing
            return false;
        }
    }
}