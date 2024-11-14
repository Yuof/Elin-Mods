using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ExampleMod;

[BepInPlugin("yuof.elin.uiExtension.mod", "Elin UI Extension", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Start()
    {
        var harmony = new Harmony("yuof.elin.uiExtension.mod");
        harmony.PatchAll();
    }
}