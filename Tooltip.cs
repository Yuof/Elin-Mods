using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace Elin_UI
{
    [HarmonyPatch]
    public class Tooltip
    {

        [HarmonyPatch(typeof(Thing), "WriteNote")]
        [HarmonyPostfix]
        public static void Thing_WriteNote(Thing __instance, UINote n, Action<UINote> onWriteNote = null, IInspect.NoteMode mode = IInspect.NoteMode.Default, Recipe recipe = null)
        {
            foreach (var element2 in __instance.elements.dict.Values)
            {
                n.AddText(string.Concat(new string[]
                {
                    element2.source.alias,
                    "/",
                    element2.Value.ToString(),
                    "/",
                    element2.vBase.ToString(),
                    "/",
                    element2.vSource.ToString()
                }), FontColor.DontChange);
            }

            n.AddText(__instance.GetValue().ToString());
            n.AddText(__instance.GetEquipValue().ToString());
            n.Build();
        }
    }
}
