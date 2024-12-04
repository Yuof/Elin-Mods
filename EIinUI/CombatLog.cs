using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Elin_UIExtensions
{
    [HarmonyPatch]
    public class CombatLog
    {
        [HarmonyPatch(typeof(WidgetMainText), "Append", [typeof(string), typeof(Color), typeof(Point)])]
        [HarmonyPrefix]
        public static bool AppendPrefix(WidgetMainText __instance, string s, Color col, Point pos)
        {
            if (s.IsEmpty() || s == " ")
            {
                return false;
            }
            if (MsgBlock.lastBlock != null && MsgBlock.lastText == s)
            {
                Plugin.Log.LogInfo("Last block is not null & MsgBlock.lastText == s");
                return true;
            }

            __instance.box.CreateNewBlock();
            return true;
        }
    }
}
