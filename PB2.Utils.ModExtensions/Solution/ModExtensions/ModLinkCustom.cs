using UnityEngine;
using HarmonyLib;
using PhantomBrigade.Mods;

namespace ModExtensions
{
    public class ModLinkCustom : ModLink
    {
        public static ModLinkCustom ins;
        
        public override void OnLoadStart()
        {
            ins = this;
            Debug.Log ($"OnLoadStart");
        }

        public override void OnLoad (Harmony harmonyInstance)
        {
            base.OnLoad (harmonyInstance);
            Debug.Log ($"OnLoad | Mod: {modID} | Index: {modIndexPreload} | Path: {modPath}");
        }
    }
}