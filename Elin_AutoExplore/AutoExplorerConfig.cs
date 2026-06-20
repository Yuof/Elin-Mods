using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Elin_AutoExplore
{
    public class AutoExplorerConfig
    {
        public AutoExplorerConfig(ConfigFile config)
        {
            this.ActivationKey = config.Bind("General", "ActivationKey", KeyCode.L, "Key to start and stop autoexplore.");
            this.ToggleHarvestingAndMiningMode = config.Bind("General", "ToggleHarvestingAndMiningMode", new KeyboardShortcut(KeyCode.L, KeyCode.LeftShift), "Key to toggle between just exploring, harvesting and mining mode.");
            this.GoDownKey = config.Bind("General", "GoDownKey", KeyCode.Comma, "Key to move to stairs down.");
            this.GoUpKey = config.Bind("General", "GoUpKey", KeyCode.Period, "Key to move to stairs up.");
            this.HandleTraps = config.Bind("Toggles", "HandleTraps", true, "Should autoexplore disarm traps?");
            this.UseMeditation = config.Bind("Toggles", "UseMeditation", true, "Should autoexplore meditate for HP/MP regen?");
            this.HandleFighting = config.Bind("Toggles", "HandleFighting", true, "Should autoexplore fight enemies?");
            this.HandleHarvestables = config.Bind("Toggles", "HandleHarvestables", false, "Should autoexplore harvest?");
            this.HandleMineables = config.Bind("Toggles", "HandleMineables", false, "Should autoexplore mine?");
            this.HandleShrines = config.Bind("Toggles", "HandleShrines", true, "Should autoexplore use shrines?");
            this.StopOnUnusableShrine = config.Bind("Toggles", "StopOnUnusableShrine", false, "Should autoexplore stop next to material/armor shrines (which it can't auto-use) so you can use them manually?");
            this.HandleStatues = config.Bind("Toggles", "HandleStatues", false, "Should autoexplore use god statues? (includes one-time effects like Jure's rebirth)");
            this.HandleChests = config.Bind("Toggles", "HandleChests", false, "Should autoexplore lockpick and loot containers (e.g. boss chests)?");
            this.AutoDescend = config.Bind("Toggles", "AutoDescend", false, "Should autoexplore descend to the next floor when the current floor is cleared?");
            this.HandleHunger = config.Bind("Toggles", "HandleHunger", HungerMode.AutoEat, "Should autoexplore eat food?");

            this.MinMP = config.Bind("Regen", "minMP", 90, "Percentage of MP to start meditation.");
            this.MinHP = config.Bind("Regen", "minHP", 100, "Percentage of HP to start meditation.");

            this.LogPerformance = config.Bind("Debug", "LogPerformance", false, "Log reachability-flood and decision-cycle timings to the BepInEx console (for profiling on large maps).");
            this.UseEarlyExitFlood = config.Bind("Performance", "UseEarlyExitFlood", true, "Stop the reachability flood at the nearest actionable target instead of flooding the whole map (much faster on large maps). Disable only if you suspect it is missing reachable targets.");

            this.GatheringRestrictionList = config.Bind("Restrictions", "GatheringRestrictionList", "wreck,chemicals,wall frame,debris,moss grass,a vine,pebble,stalagmite,chunk", "Comma separated list of things to ignore when harvesting.");
            this.MiningRestrictionList = config.Bind("Restrictions", "MiningRestrictionList", "metal block(copper),soil block(soil),soil block covered with vines (soil)", "Comma separated list of things to ignore when mining.");
        }


        public ConfigEntry<KeyCode> ActivationKey { get; set; }
        public ConfigEntry<KeyboardShortcut> ToggleHarvestingAndMiningMode { get; set; }
        public ConfigEntry<KeyCode> GoDownKey { get; set; }
        public ConfigEntry<KeyCode> GoUpKey { get; set; }
        public ConfigEntry<KeyboardShortcut> MenuKey { get; set; }
        public ConfigEntry<bool> UseMeditation { get; set; }
        public ConfigEntry<int> MinMP { get; set; }
        public ConfigEntry<int> MinHP { get; set; }
        public ConfigEntry<bool> HandleTraps { get; set; }
        public ConfigEntry<bool> HandleFighting { get; set; }
        public ConfigEntry<bool> HandleHarvestables { get; set; }
        public ConfigEntry<bool> HandleMineables { get; set; }
        public ConfigEntry<bool> HandleShrines { get; set; }
        public ConfigEntry<bool> StopOnUnusableShrine { get; set; }
        public ConfigEntry<bool> HandleStatues { get; set; }
        public ConfigEntry<bool> HandleChests { get; set; }
        public ConfigEntry<bool> AutoDescend { get; set; }
        public ConfigEntry<bool> LogPerformance { get; set; }
        public ConfigEntry<bool> UseEarlyExitFlood { get; set; }
        public ConfigEntry<string> GatheringRestrictionList { get; set; }
        public ConfigEntry<string> MiningRestrictionList { get; set; }
        public ConfigEntry<HungerMode> HandleHunger { get; set; }

        private Mode GetMode()
        {
            if (this.HandleHarvestables.Value && this.HandleMineables.Value)
            {
                return Mode.HarvestAndMine;
            }
            else if (this.HandleHarvestables.Value)
            {
                return Mode.Harvest;
            }
            else if (this.HandleMineables.Value)
            {
                return Mode.Mine;
            }
            else
            {
                return Mode.Explore;
            }
        }

        public void SetNextMode()
        {
            switch (this.GetMode())
            {
                case Mode.Explore:
                    this.SetMode(Mode.Harvest);
                    ELayer.pc.TalkRaw(Translations.GetTranslation(Translations.HarvestingMode));
                    break;
                case Mode.Harvest:
                    this.SetMode(Mode.Mine);
                    ELayer.pc.TalkRaw(Translations.GetTranslation(Translations.MiningMode));
                    break;
                case Mode.Mine:
                    this.SetMode(Mode.HarvestAndMine);
                    ELayer.pc.TalkRaw(Translations.GetTranslation(Translations.HarvestingAndMiningMode));
                    break;
                case Mode.HarvestAndMine:
                    this.SetMode(Mode.Explore);
                    ELayer.pc.TalkRaw(Translations.GetTranslation(Translations.ExploringMode));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetMode(Mode mode)
        {
            switch (mode)
            {
                case Mode.Explore:
                    this.HandleHarvestables.Value = false;
                    this.HandleMineables.Value = false;
                    break;
                case Mode.Harvest:
                    this.HandleHarvestables.Value = true;
                    this.HandleMineables.Value = false;
                    break;
                case Mode.Mine:
                    this.HandleHarvestables.Value = false;
                    this.HandleMineables.Value = true;
                    break;
                case Mode.HarvestAndMine:
                    this.HandleHarvestables.Value = true;
                    this.HandleMineables.Value = true;
                    break;
            }
        }

        public enum Mode
        {
            Explore,
            Harvest,
            Mine,
            HarvestAndMine,
        }

        public enum HungerMode
        {
            Ignore,
            AutoEat,
            StopAutoExplore,
        }
    }
}
