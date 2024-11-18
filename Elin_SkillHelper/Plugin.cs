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
        if (EInput.isInputFieldActive) return;
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
        if (Input.GetKeyDown(KeyCode.P))
        {
            this.Logger.LogInfo("P key pressed");
            var bestPoint = this.FindBestLocationForPerformance();
            var ai = new AI_Goto(bestPoint, 0);
            this.Logger.LogInfo("bestPoint: " + bestPoint);
            ELayer.pc.SetAIImmediate(ai);
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

    private Point FindBestLocationForPerformance()
    {
        var map = ELayer._map;
        var bestPoint = map.GetCenterPos();
        double bestScore = 0;
        map.ForeachPoint(point =>
        {
            var charas = point.ListWitnesses(ELayer.pc, 4, WitnessType.music);
            charas = charas
                .Where(chara => chara.interest > 0)
                .Where(chara => !(EClass._zone is Zone_Music && (chara.IsPCFaction || chara.IsPCFactionMinion)))
                .ToList();
            if (!charas.Any())
                return;
            var score = charas.Average(chara => chara.LV) * charas.Count;
            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point.Copy();
                this.Logger.LogInfo($"BestScore: {bestScore}, BestPoint: {bestPoint}");
            }
        });
        return bestPoint;
    }


}