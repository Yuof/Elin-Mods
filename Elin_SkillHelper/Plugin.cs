using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExampleMod;

[BepInPlugin("yuof.elin.skillhelper.mod", "Elin SkillHelper", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    public SkillHelperMode mode = SkillHelperMode.None;

    private void Start()
    {
        var harmony = new Harmony("yuof.elin.skillhelper.mod");
        harmony.PatchAll();
    }


    public void Update()
    {
        if (EInput.isInputFieldActive) return;
        if (Input.GetKeyDown(KeyCode.I))
        {
            this.Logger.LogInfo("I key pressed");
            this.mode = SkillHelperMode.None;
        }
        if (!EClass.core.IsGameStarted
        || ELayer.pc == null
        || ELayer.pc.isDead
        || !ELayer.pc.ai.IsIdle
        || WidgetCurrentTool.dirty)
        {
            return;
        }

        if (this.mode != SkillHelperMode.None && this.ShouldStop())
        {
            ELayer.pc.TalkRaw("Low Stamina");
            this.mode = SkillHelperMode.None;
            return;
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            this.Logger.LogInfo("O key pressed");
            //TSUI.LoadCustomUI<SkillHelperWindow, SkillHelperWindow.Args>(new SkillHelperWindow.Args());
            this.mode = SkillHelperMode.Shear;
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            this.Logger.LogInfo("P key pressed");
            this.mode = SkillHelperMode.Performance;
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            this.Logger.LogInfo("K key pressed");
            this.mode = SkillHelperMode.Water;
        }
        if (Input.GetKeyDown(KeyCode.U))
        {
            this.Logger.LogInfo("U key pressed");
            this.mode = SkillHelperMode.Lockpick;
        }

        switch (this.mode)
        {
            case SkillHelperMode.Shear:
                this.HandleShearing();
                break;
            case SkillHelperMode.Water:
                this.HandleWatering();
                break;
            case SkillHelperMode.Performance:
                this.HandlePerformance();
                break;
            case SkillHelperMode.Lockpick:
                this.HandleLockPicking();
                break;
        }
    }

    private void HandleWatering()
    {
        // if can is empty, refill it
        var acts = new List<AIAct>();
        var waterCan = this.WieldWaterCan();
        if (waterCan != null) {
                Logger.LogInfo("WaterCan charges: " + waterCan.owner.c_charges);
                if (waterCan.owner.c_charges == 0)
                {
                    ELayer._map.bounds.ForeachPoint(point =>
                    {
                        if (ActDrawWater.HasWaterSource(point))
                        {
                            Logger.LogInfo("Found water source at: " + point);
                            if (point.RealDistance(ELayer.pc.pos) > 1)
                            {
                                Logger.LogInfo("Going to water source");
                                acts.Add(new AI_Goto(point, 1));
                                return;
                            }
                            var act = new ActDrawWater() { waterCan = waterCan };
                            act.Perform();
                            acts = this.FindWaterAbles();
                        }
                    });
                }
                else
                {
                    acts = this.FindWaterAbles();
                }
        }
                
        if (!acts.Any())
        {
            ELayer.pc.Say("invalidAction");
            this.mode = SkillHelperMode.None;
            return;
        }
        var act = acts.OrderBy(act => act.GetDestinationPoint().RealDistance(ELayer.pc.pos)).First();
        ELayer.pc.SetAIImmediate(act);
    }

    private void HandlePerformance()
    {
        var bestPoint = this.FindBestLocationForPerformance();
        Logger.LogInfo("bestPoint: " + bestPoint);
        Logger.LogInfo("pc.pos: " + ELayer.pc.pos);
        if (bestPoint != null && ELayer.pc.pos.Distance(bestPoint) == 0)
        {
            ELayer.pc.TalkRaw("Starting music.");
            // equip instrument
            var instrument = ELayer.pc.things.Where(thing => thing.trait as TraitToolMusic != null).FirstOrDefault();
            if (instrument == null)
            {
                ELayer.pc.Say("invalidAction");
                this.mode = SkillHelperMode.None;
                return;
            }
            else
            {
                ELayer.player.EquipTool(instrument);
                ELayer.pc.SetAIImmediate(new AI_PlayMusic() { tool = instrument });
                this.mode = SkillHelperMode.None;
                return;
            }
        }
        var ai = new AI_Goto(bestPoint, 0);
        this.Logger.LogInfo("bestPoint: " + bestPoint);
        ELayer.pc.SetAIImmediate(ai);
    }

    private void HandleShearing()
    {
        var shearables = this.FindShearables();
        if (!shearables.Any())
        {
            ELayer.pc.Say("invalidAction");
            this.mode = SkillHelperMode.None;
            return;
        }
        var chara = shearables.OrderBy(chara => ELayer.pc.pos.RealDistance(chara.pos)).First();
        this.WieldShears();
        ELayer.pc.SetAIImmediate(new AI_Shear { target = chara });
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

    private List<AIAct> FindWaterAbles()
    {
        var map = ELayer._map;
        var list = new List<AIAct>();
        map.bounds.ForeachPoint(point =>
        {

            if (TaskWater.ShouldWater(point))
            {
                var act = new TaskWater() { dest = point.Copy(), owner = ELayer.pc };
                list.Add(act);
            }
        });
        return list;
    }

    private TraitToolWaterCan? WieldWaterCan()
    {
        var pc = ELayer.pc;
        //find water can
        var waterCan = pc.things.Where(thing => thing.trait as TraitToolWaterCan != null).FirstOrDefault();
        if (waterCan != null)
        {
            ELayer.player.EquipTool(waterCan);
        }
        return waterCan?.trait as TraitToolWaterCan;
    }

    private Point FindBestLocationForPerformance()
    {
        var map = ELayer._map;
        var bestPoint = map.GetCenterPos();
        double bestScore = 0;
        map.bounds.ForeachPoint(point =>
        {
            var charas = point.ListWitnesses(ELayer.pc, 4, WitnessType.music);
            charas = charas
                .Where(chara => chara.interest > 0)
                .Where(chara => !(EClass._zone is Zone_Music && (chara.IsPCFaction || chara.IsPCFactionMinion || chara.IsPCParty)))
                .ToList();
            if (!charas.Any())
                return;
            var score = charas.Average(chara => chara.LV) * charas.Count;
            var distance = point.Distance(ELayer.pc.pos);
            if (distance > 10)
                score -= distance;
            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = point.Copy();
                this.Logger.LogInfo($"BestScore: {bestScore}, BestPoint: {bestPoint} Distance: {distance}");
            }
        });
        return bestPoint;
    }

    public void OnGUI()
    {
        if (this.mode != SkillHelperMode.None)
        {
            GUI.Label(new Rect(10, 10, 100, 20), "SkillHelperMode: " + this.mode);
        }
    }

    public void HandleLockPicking()
    {
        var acts = new List<AIAct>();
        ELayer._map.bounds.ForeachPoint(point =>
        {
            var container = point.ListThings<TraitContainer>();
            if (container.Count > 0)
            {
                foreach (var c in container)
                {
                    if (c.c_lockLv > 0)
                    {
                        acts.Add(new AI_OpenLock() { target = c.Thing });
                    }
                }
            }
        });


        if (!acts.Any())
        {
            ELayer.pc.Say("invalidAction");
            this.mode = SkillHelperMode.None;
            return;
        }
        var act = acts.OrderBy(act => act.GetDestinationPoint().RealDistance(ELayer.pc.pos)).First();
        ELayer.pc.SetAIImmediate(act);
    }

    public bool ShouldStop()
    {
        return ELayer.pc.stamina.value < -10;
    }



    public enum SkillHelperMode
    {
        None,
        Shear,
        Water,
        Performance,
        Lockpick,
    }
}