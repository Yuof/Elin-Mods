using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Elin_AutoExplore
{
    internal class AutoExplorerConfig
    {
        public AutoExplorerConfig(ConfigFile config)
        {
            this.ActivationKey = config.Bind("General", "ActivationKey", KeyCode.L, "Key to start and stop autoexplore.");
            this.GoDownKey = config.Bind("General", "GoDownKey", KeyCode.Comma, "Key to move to stairs down.");
            this.GoUpKey = config.Bind("General", "GoUpKey", KeyCode.Period, "Key to move to stairs up.");
            this.HandleTraps = config.Bind("Toggles", "HandleTraps", true, "Should autoexplore disarm traps?");
            this.UseMeditation = config.Bind("Toggles", "UseMeditation", true, "Should autoexplore meditate for HP/MP regen?");
            this.HandleFighting = config.Bind("Toggles", "HandleFighting", true, "Should autoexplore fight enemies?");
            this.HandleHarvestables = config.Bind("Toggles", "HandleHarvestables", false, "Should autoexplore harvest?");
            this.HandleMineables = config.Bind("Toggles", "HandleMineables", false, "Should autoexplore mine?");

            this.MinMP = config.Bind("Regen", "minMP", 90, "Percentage of MP to start meditation.");
            this.MinHP = config.Bind("Regen", "minHP", 100, "Percentage of HP to start meditation.");
        }

        public ConfigEntry<KeyCode> ActivationKey { get; set; }
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
    }
}
