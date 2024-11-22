using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Elin_UI
{
    [HarmonyPatch]
    public class RosterButton
    {
        private static Dictionary<int, Image> secondBars = new();

        [HarmonyPatch(typeof(ButtonRoster), "SetChara")]
        [HarmonyPostfix]
        public static void SetChara(ButtonRoster __instance, Chara c)
        {
            //Plugin.Log.LogInfo("SetChara");
            __instance.roster.extra.showHP = false;
            //Plugin.Log.LogInfo($"Instance Id in SetChara {__instance.GetInstanceID()}");
            var secondBar = UnityEngine.Object.Instantiate(__instance.barMood);
            secondBars[__instance.GetInstanceID()] = secondBar;
            secondBar.transform.SetParent(__instance.transform);
            __instance.rect.sizeDelta = new Vector2(0f, 40f);
            var hpPos = __instance.barMood.transform.localPosition;
            //Plugin.Log.LogInfo($"HP Pos: {hpPos}");
            secondBar.transform.SetAsLastSibling();
            secondBar.transform.localPosition = new Vector3(4f, -24f, 0f);
            __instance.RebuildLayout();
        }

        [HarmonyPatch(typeof(ButtonRoster), "Refresh")]
        [HarmonyPostfix]
        public static void Refresh(ButtonRoster __instance)
        {
            //Plugin.Log.LogInfo("Refresh");
            float num = Mathf.Clamp((float)__instance.chara.mana.value / (float)__instance.chara.mana.max, 0f, 1f);
            //Plugin.Log.LogInfo($"Instance Id in Refresh {__instance.GetInstanceID()}");
            //Plugin.Log.LogInfo($"Mana value: {__instance.chara.mana.value}, Mana max: {__instance.chara.mana.max}, Num: {num}");
            if (secondBars.ContainsKey(__instance.GetInstanceID()))
            {
                var secondbar = secondBars[__instance.GetInstanceID()];
                secondbar.Rect().localScale = new Vector3(num, 1f, 1f);
                secondbar.color = Color.blue;
            }
        }
    }
}
