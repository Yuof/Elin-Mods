using HarmonyLib;

namespace Elin_AutoExplore
{
    [HarmonyPatch]
    public class AutoExploreConfigUi
    {
        public const string Name = "AutoExplore Settings";

        [HarmonyPatch(typeof(ActPlan), "ShowContextMenu")]
        [HarmonyPrefix]
        public static void Prefix(ActPlan __instance)
        {
            if (__instance.pos.Equals(EClass.pc.pos))
            {
                var config = Plugin.Instance.AutoExplorerConfig;
                var dynamicAct = new DynamicAct(Translations.GetTranslation(Name), () =>
                {
                    //ELayer.pc.TalkRaw("buliding settings");
                    var menu = EClass.ui.CreateContextMenu();
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleFighting)), config.HandleFighting.Value,
                        val => config.HandleFighting.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleHarvestables)), config.HandleHarvestables.Value,
                        val => config.HandleHarvestables.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleMineables)), config.HandleMineables.Value,
                        val => config.HandleMineables.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleTraps)), config.HandleTraps.Value,
                        val => config.HandleTraps.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleShrines)), config.HandleShrines.Value,
                        val => config.HandleShrines.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.StopOnUnusableShrine)), config.StopOnUnusableShrine.Value,
                        val => config.StopOnUnusableShrine.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleStatues)), config.HandleStatues.Value,
                        val => config.HandleStatues.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.HandleChests)), config.HandleChests.Value,
                        val => config.HandleChests.Value = val);
                    menu.AddToggle(Translations.GetTranslation(nameof(config.AutoDescend)), config.AutoDescend.Value,
                        val => config.AutoDescend.Value = val);
                    menu.AddSlider(Translations.GetTranslation(nameof(config.HandleHunger)),
                        val => ((AutoExplorerConfig.HungerMode)(int)val).ToString(),
                        (float)config.HandleHunger.Value,
                        val => config.HandleHunger.Value = (AutoExplorerConfig.HungerMode)(int)val,
                        0,
                        2,
                        true,
                        false);

                    menu.AddSeparator();
                    menu.AddToggle(Translations.GetTranslation(nameof(config.UseMeditation)), config.UseMeditation.Value,
                        val => config.UseMeditation.Value = val);
                    menu.AddSlider(Translations.GetTranslation(nameof(AutoExplorerConfig.MinMP)),
                        val => val.ToString(), config.MinMP.Value, val => config.MinMP.Value = (int)val, 0, 100, true, false);
                    menu.AddSlider(Translations.GetTranslation(nameof(AutoExplorerConfig.MinHP)),
                        val => val.ToString(), config.MinHP.Value, val => config.MinHP.Value = (int)val, 0, 100, true, false);
                    menu.Show();
                    return false;
                });
                __instance.list.Add(new ActPlan.Item() { act = dynamicAct });
            }
        }

        [HarmonyPatch(typeof(ActPlan.Item), "Perform")]
        [HarmonyPrefix]
        public static bool Prefix(ActPlan.Item __instance)
        {
            if (__instance.act is DynamicAct act && act.id == Translations.GetTranslation(Name))
            {
                act.Perform();
                return false;
            }

            return true;
        }
    }
}
