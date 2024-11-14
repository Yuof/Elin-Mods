using System;
using System.Collections.Generic;
using System.Linq;
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
            //foreach (var element2 in __instance.elements.dict.Values)
            //{
            //    n.AddText(string.Concat(new string[]
            //    {
            //        element2.source.alias,
            //        "/",
            //        element2.Value.ToString(),
            //        "/",
            //        element2.vBase.ToString(),
            //        "/",
            //        element2.vSource.ToString()
            //    }), FontColor.DontChange);
            //}

            int totalQuality = __instance.GetTotalQuality(true);
            int totalQuality2 = __instance.GetTotalQuality(false);
            var quality = "_quality".lang(__instance.GetTotalQuality(true).ToString() ?? "", null, null, null, null) + ((totalQuality == totalQuality2) ? "" : (" (" + totalQuality2.ToString() + ")"));

            var value = __instance.GetValue().FormatNumber();
            var equipValue = __instance.GetEquipValue().FormatNumber();
            var valueString = $"It is worth {value}.";
            if (__instance.GetEquipValue() != __instance.GetValue())
            {
                valueString = $"{valueString} (EQ:{equipValue})";
            }

            if (__instance.GetValue() != 0)
            {
                n.AddText(valueString);
            }
            if (__instance.sourceCard.tag.Length != 0)
            {
                n.AddText("It has special tags: " + string.Concat(__instance.sourceCard.tag));
            }
            // tourism value
            if (__instance.HasTag(CTAG.tourism))
            {
                var tourismValue = CalcTourismPrice(__instance);
                if (tourismValue != __instance.GetValue())
                {
                    n.AddText("It has " + tourismValue.FormatNumber() + " tourism value.");
                }
            }

            n.Build();
        }

        private static int CalcTourismPrice(Thing thing)
        {
            int num = thing.GetPrice(CurrencyType.Money, false, PriceType.Default, null);
            if (thing.noSell)
            {
                num /= 50;
            }
            if (thing.HasTag(CTAG.tourism) && thing.trait is TraitFigure)
            {
                num *= 2;
            }

            return num;
        }
    }
}
