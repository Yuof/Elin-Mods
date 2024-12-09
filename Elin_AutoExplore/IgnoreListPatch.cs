using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace Elin_AutoExplore
{
    [HarmonyPatch]
    public class IgnoreListPatch
    {
        private static List<string> actNames = new List<string> { "Remove from Gathering Ignore List", "Add to Gathering Ignore List", "Remove from Mining Ignore List", "Add to Mining Ignore List" };

        [HarmonyPatch(typeof(ActPlan), "GetAction")]
        [HarmonyPrefix]
        public static void Prefix(ActPlan __instance)
        {
            //Console.WriteLine(__instance.input);
            if (!EInput.isShiftDown)
            {
                return;
            }
            //Console.WriteLine($"Checking for harvest and mine tasks at {__instance.pos}");
            var harvestTask = TaskHarvest.TryGetAct(ELayer.pc, __instance.pos);
            if (harvestTask != null)
            {
                //Console.WriteLine($"Harvest task found at {__instance.pos.cell.sourceObj.GetName()}");
                var targetName = harvestTask.IsObj ? __instance.pos.cell.sourceObj.GetName() : harvestTask.target.Name;
                var ignored = Plugin.Instance.IgnoreList.IsIgnoredFromGathering(targetName);
                var actionName = ignored ? "Remove from Gathering Ignore List" : "Add to Gathering Ignore List";
                var dynamicAct = new DynamicAct($"{actionName}", () =>
                {
                    if (ignored)
                    {
                        Plugin.Instance.IgnoreList.RemoveFromGatheringIgnoreList(targetName);
                        //Console.WriteLine($"Removed {targetName} from gathering ignore list.");
                    }
                    else
                    {
                        Plugin.Instance.IgnoreList.AddToGatheringIgnoreList(targetName);
                        //Console.WriteLine($"Added {targetName} to gathering ignore list.");
                    }
                    return true;
                });
                __instance.list.Add(new ActPlan.Item() { act = dynamicAct });
            }

            var canMine = TaskMine.CanMine(__instance.pos, ELayer.pc.Tool);
            if (canMine)
            {
                //Console.WriteLine($"Mine task found at {__instance.pos.cell.GetBlockName()}");
                var targetName = __instance.pos.cell.GetBlockName();
                var ignored = Plugin.Instance.IgnoreList.IsIgnoredFromMining(targetName);
                var actionName = ignored ? "Remove from Mining Ignore List" : "Add to Mining Ignore List";
                var dynamicAct = new DynamicAct($"{actionName}", () =>
                {
                    if (ignored)
                    {
                        Plugin.Instance.IgnoreList.RemoveFromMiningIgnoreList(targetName);
                        //Console.WriteLine($"Removed {targetName} from mining ignore list.");
                    }
                    else
                    {
                        Plugin.Instance.IgnoreList.AddToMiningIgnoreList(targetName);
                        //Console.WriteLine($"Added {targetName} to mining ignore list.");
                    }
                    return true;
                });
                __instance.list.Add(new ActPlan.Item() { act = dynamicAct });
            }
        }

        [HarmonyPatch(typeof(ActPlan.Item), "Perform")]
        [HarmonyPrefix]
        public static bool Prefix(ActPlan.Item __instance)
        {
            if (__instance.act is DynamicAct act && actNames.Contains(act.id))
            {
                act.Perform();
                return false;
            }

            return true;
        }
    }
}
