using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;

namespace Elin_AutoExplore
{
    [HarmonyPatch(typeof(AM_Adv), nameof(AM_Adv.TryCancelInteraction))]
    public static class HookUserInteraction
    {
        public static readonly List<AIAct> UserCanceledAiActs = [];

        [HarmonyPostfix]
        static public void PostFix(AM_Adv __instance, bool __result)
        {
            if (__result == true)
            {
                UserCanceledAiActs.Add(EClass.pc.ai);
            }
        }
    }
}
