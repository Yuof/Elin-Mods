using BepInEx;
using Elin_SkillHelper;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TSBase;
using UnityEngine;

namespace ExampleMod;

[BepInPlugin("yuof.elin.skillhelper.mod", "Elin SkillHelper", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Start()
    {
        var harmony = new Harmony("yuof.elin.skillhelper.mod");
        harmony.PatchAll();
    }


    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            this.Logger.LogInfo("O key pressed");
            //TSUI.LoadCustomUI<SkillHelperWindow, SkillHelperWindow.Args>(new SkillHelperWindow.Args());
            var shearables = this.FindShearables();
            if (!shearables.Any())
            {
                ELayer.pc.Say("invalidAction");
                return;
            }
            shearables = shearables.OrderBy(chara => ELayer.pc.Dist(chara)).ToList();
            this.WieldShears();
            var chara = shearables.First();
            ELayer.pc.SetAIImmediate(new AI_Shear { target = chara });
        }
    }

    private List<Chara> FindShearables()
    {
        var map = ELayer._map;
        //Logger.LogMessage("CurrentFov.Count: " + currentFov.Count);
        var allChars = map.charas.ToList();
        var shearables = allChars.Where(chara => chara.CanBeSheared()).ToList();
        //Logger.LogMessage("currentCharas.Count: " + currentCharas.Count);
        return shearables;
    }

    private void WieldShears()
    {
        var pc = ELayer.pc;
        //find shears
        var shears = pc.things.Where(thing => thing.trait as TraitToolShears != null).FirstOrDefault();
        if (shears != null) {
            ELayer.player.EquipTool(shears);
        }
    }


}