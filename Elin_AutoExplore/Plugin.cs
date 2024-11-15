using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExampleMod;

[BepInPlugin("yuof.elin.autoExplore.mod", "Elin AutoExplorer", "1.0.1.0")]
public class Plugin : BaseUnityPlugin
{
    private Zone currentZone;
    private Point currentPos => this.playerCharacter.pos;
    private Chara playerCharacter => ELayer.pc;
    private bool isEnable = false;
    private State state = State.Idle;
    private ConfigEntry<bool> useMeditation;
    private ConfigEntry<int> minMP;
    private ConfigEntry<int> minHP;
    private ConfigEntry<bool> handleTraps;
    private ConfigEntry<KeyCode> activationKey;
    private ConfigEntry<bool> handleFighting;

    private void Awake()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.PatchAll();
        this.activationKey = this.Config.Bind("General", "ActivationKey", KeyCode.L, "Key to start and stop autoexplore.");
        this.handleTraps = this.Config.Bind("Toggles", "HandleTraps", true, "Should autoexplore disarm traps?");
        this.useMeditation = this.Config.Bind("Toggles", "UseMeditation", true, "Should autoexplore meditate for HP/MP regen?");
        this.handleFighting = this.Config.Bind("Toggles", "HandleFighting", true, "Should autoexplore fight enemies?");
        this.minMP = this.Config.Bind("Regen", "minMP", 90, "Percentage of MP to start meditation.");
        this.minHP = this.Config.Bind("Regen", "minHP", 100, "Percentage of HP to start meditation.");
    }
    private void Unload()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.UnpatchSelf();
    }

    public void Update()
    {
        if (Input.GetKeyDown(this.activationKey.Value))
        {
            this.Logger.LogInfo("L key pressed");
            if (this.isEnable)
            {
                this.isEnable = false;
                this.state = State.Starting;
                this.Logger.LogInfo("Auto explore disabled.");
            }
            else
            {
                this.isEnable = true;
                this.state = State.Starting;
                this.Logger.LogInfo("Auto explore enabled.");
            }
        }

        if (!this.isEnable) return;

        if (!EClass.core.IsGameStarted
                || this.playerCharacter == null
                || ELayer._zone.IsPlayerFaction
                || this.playerCharacter.isDead)
        {
            this.isEnable = false;
            return;
        }
        if (this.playerCharacter.ai.status == AIAct.Status.Fail && !this.playerCharacter.ai.IsMoveAI && this.state != State.Starting)
        {
            this.Logger.LogWarning("Current AIAct failed!. " + this.playerCharacter.ai.Name);
            this.isEnable = false;
            return;
        }

        if (!this.playerCharacter.ai.IsRunning) this.state = State.Idle;
        if (this.playerCharacter.ai.IsIdle) this.state = State.Idle;
        var enemies = this.FindVisibleEnemies();
        var unexplored = this.FindUnexploredPoints();
        var loot = this.FindLoot();
        var points = unexplored.Concat(loot).OrderBy(p => p.Distance(this.currentPos)).ToList();
        var trap = this.handleTraps.Value ? this.FindTrap() : null;
        var shouldRest = this.useMeditation.Value && this.ShouldRest();

        switch (this.state)
        {
            case State.Exploring:
                if (shouldRest) { this.HandleResting(); break; }
                break;
            case State.Combat:
                break;
            case State.Resting:
                if (enemies.Any()) { this.HandleCombat(enemies); break; }
                break;
            case State.Starting:
            case State.Idle:
                if (enemies.Any()) { this.HandleCombat(enemies); break; }
                if (shouldRest) { this.HandleResting(); break; }
                if (trap != null) { this.HandleTrap(trap); break; }
                if (points.Any()) { this.HandleMovement(points); break; }
                this.Logger.LogMessage("Nothing to do.");
                this.isEnable = false;
                break;
            default:
                break;
        }
        this.Logger.LogInfo("Current State is " + this.state);
    }

    private List<Point> FindUnexploredPoints()
    {
        var map = ELayer._map;

        var cells = map.cells;
        var points = new List<Point>();
        map.ForeachPoint(point =>
        {
            if (!point.IsSeen && !point.IsBlocked && this.IsPointReachable(point))
            {
                
                points.Add(new Point(point));
            }
        });

        points = points.OrderBy(p => p.Distance(this.currentPos)).ToList();
        //Logger.LogInfo($"Found {points.Count} hidden points.");
        return points;
    }

    private List<Point> FindLoot()
    {
        var map = ELayer._map;

        var cells = map.cells;
        var points = new List<Point>();
        map.ForeachPoint(point =>
        {
            if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
            {
                var things = point.Things;
                foreach ( var thing in things )
                {
                    //Logger.LogInfo(thing.ToString() + " | " +thing.GetRootCard().placeState + " | " + this.CanPick(thing));
                    if (thing.GetRootCard().placeState == PlaceState.roaming && this.CanPick(thing) && !thing.isNPCProperty)
                    {
                        points.Add(new Point(point));
                        //Logger.LogInfo(thing.ToString() + " | " + thing.GetRootCard().placeState + " | " + this.CanPick(thing)+ " | " + thing.isNPCProperty + " | " + thing.ExistsOnMap);
                    }

                }
            }
        });

        points = points.OrderBy(p => p.Distance(this.currentPos)).ToList();
        this.Logger.LogInfo($"Found {points.Count} loot points.");
        return points;
    }

    private Card? FindTrap()
    {
        var map = ELayer._map;
        var cells = map.cells;
        var currentFov = this.playerCharacter.fov.ListPoints();
        currentFov = currentFov.OrderBy(p => p.Distance(this.currentPos)).ToList();
        foreach (var point in currentFov)
        {
            if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
            {
                var things = point.Things;
                foreach (var thing in things)
                {
                    //Logger.LogInfo(thing.ToString() + " | " + thing.GetRootCard().placeState + " | " + this.CanPick(thing));
                    if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitTrap)
                    {
                        var trait = thing.trait as TraitTrap;
                        if (trait.CanDisarmTrap)
                            return thing.GetRootCard();

                    }
                }
            }
        }
        return null;
    }

    private bool ShouldRest()
    {
        return
               this.playerCharacter.hp * 100 < this.playerCharacter.MaxHP * this.minHP.Value
            || this.playerCharacter.mana.value * 100 < this.playerCharacter.mana.max * this.minMP.Value
            ;
    }

    private bool IsPointReachable(Point point)
    {
        var path = new PathProgress();
        path.RequestPathImmediate(this.currentPos, point, 0, false);
        return path.HasPath;

    }

    private void HandleCombat(List<Chara> enemies)
    {
        var nearestEnemy = enemies.OrderBy(enemy => enemy.pos.Distance(this.currentPos)).First();
        this.Logger.LogMessage($"Current enemies: {enemies.Count()}");
        EClass.pc.SetAIImmediate(new GoalAutoCombat(nearestEnemy));
        this.state = State.Combat;
    }

    private List<Chara> FindVisibleEnemies()
    {
        if (this.handleFighting.Value == false) return [];
        var map = ELayer._map;
        var currentFov = this.playerCharacter.fov.ListPoints();
        //Logger.LogMessage("CurrentFov.Count: " + currentFov.Count);
        var allChars = map.charas.ToList();
        var currentCharas = allChars.Where(chara => currentFov.Contains(chara.pos)).ToList();
        //Logger.LogMessage("currentCharas.Count: " + currentCharas.Count);
        //check for enemies
        var enemies = currentCharas.FindAll(chara => chara.hostility == Hostility.Enemy);
        return enemies;
    }

    private void HandleMovement(List<Point> points)
    {
        var point = points[0];
        this.Logger.LogInfo($"Moving to point {point}");
        var Ai = new AI_Goto(point, 0, false, false);
        this.playerCharacter.SetAIImmediate(Ai);
        this.state = State.Exploring;
    }

    private void HandleTrap(Card trap)
    {
        this.Logger.LogMessage("Handling trap " + trap.Name );
        if (this.currentPos.Distance(trap.pos) == 1)
            (trap.trait as TraitTrap).TryDisarmTrap(this.playerCharacter);
        else
        {
            var Ai = new AI_Goto(trap, 1, false, false);
            this.playerCharacter.SetAIImmediate(Ai);
        }
        this.state = State.Exploring;
    }

    private void HandleResting()
    {
        this.Logger.LogMessage("Resting");
        var canSleep = this.playerCharacter.CanSleep();
        if (!canSleep)
        {
            var Ai = new AI_Meditate();
            this.playerCharacter.SetAIImmediate(Ai);
        }
        else
        {
            var action = new HotItemActionSleep();
            action.Perform();
        }
        this.state = State.Resting;
    }

    private bool CanPick(Thing thing)
    {
        if (thing.isDestroyed)
        {
            return false;
        }

        Card rootCard = thing.GetRootCard();
        if (rootCard.isDestroyed)
        {
            return false;
        }

        if (this.playerCharacter.things.IsFull(thing))
        {
            return false;
        }

        return true;
    }



    private enum State
    {
        Starting,
        Idle,
        Exploring,
        Combat,
        Resting,
        Finished
    }
}