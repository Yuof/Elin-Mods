using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Elin_UIExtensions;

[BepInPlugin("yuof.elin.uiExtensions.mod", "Elin UI Extensions", "1.0.0.1")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Log;

    private void Start()
    {
        Log = Logger;
        var harmony = new Harmony("yuof.elin.uiExtensions.mod");
        harmony.PatchAll();
    }
}