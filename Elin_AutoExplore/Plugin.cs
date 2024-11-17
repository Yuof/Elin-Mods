using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Elin_AutoExplore;

[BepInPlugin("yuof.elin.autoExplore.mod", "Elin AutoExplorer", "1.1.0.0")]
public class Plugin : BaseUnityPlugin
{
    private Chara playerCharacter => ELayer.pc;

    private Point currentPos => this.playerCharacter.pos;
    private bool isEnable = false;
    private State state = State.Idle;

    private ConfigEntry<KeyCode> activationKey;
    private ConfigEntry<bool> useMeditation;
    private ConfigEntry<int> minMP;
    private ConfigEntry<int> minHP;
    private ConfigEntry<bool> handleTraps;
    private ConfigEntry<bool> handleFighting;
    private ConfigEntry<bool> handleHarvestables;
    private ConfigEntry<bool> handleMineables;


    private void Awake()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.PatchAll();
        this.activationKey = this.Config.Bind("General", "ActivationKey", KeyCode.L, "Key to start and stop autoexplore.");
        this.handleTraps = this.Config.Bind("Toggles", "HandleTraps", true, "Should autoexplore disarm traps?");
        this.useMeditation = this.Config.Bind("Toggles", "UseMeditation", true, "Should autoexplore meditate for HP/MP regen?");
        this.handleFighting = this.Config.Bind("Toggles", "HandleFighting", true, "Should autoexplore fight enemies?");
        this.handleHarvestables = this.Config.Bind("Toggles", "HandleHarvestables", false, "Should autoexplore harvest?");
        this.handleMineables = this.Config.Bind("Toggles", "HandleMineables", false, "Should autoexplore mine?");

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
        if (Input.GetKeyDown(this.activationKey.Value) && !EInput.isInputFieldActive)
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

        if (this.InventoryIsFull())
        {
            this.Logger.LogWarning("Inventory is full. Stopping autoExplore.");
            this.isEnable = false;
            Msg.Say("returnOverweight");
            return;
        }


        if (this.playerCharacter.ai.status == AIAct.Status.Fail && this.state != State.Starting)
        {
            this.Logger.LogWarning("Current AIAct failed!. " + this.playerCharacter.ai.Name);
            var currentAi = this.playerCharacter.ai;
            var userCanceled = HookUserInteraction.UserCanceledAiActs.Contains(currentAi);
            //this.Logger.LogWarning("UserCanceledAiActs: " + HookUserInteraction.UserCanceledAiActs.Count);

            if (userCanceled)
            {
                this.Logger.LogWarning("User canceled AIAct. Stopping autoExplore.");
                this.isEnable = false;
                return;
            }

            if (!currentAi.IsMoveAI)
            {
                this.Logger.LogWarning("Non-move AIAct failed. Stopping autoExplore.");
                //this.Logger.LogWarning("AIAct: " + currentAi.child.Name);
                //this.Logger.LogWarning("AIAct: " + currentAi.child.IsMoveAI);
                //this.isEnable = false;
                //return;
            }
        }

        if (this.playerCharacter.IsDeadOrSleeping)
            return;

        if (!this.playerCharacter.ai.IsRunning)
        {
            //this.Logger.LogInfo("AIAct is not running.");
            this.state = State.Idle;
        }
        if (this.playerCharacter.ai.IsIdle)
        {
            //this.Logger.LogInfo("AIAct is idle.");
            this.state = State.Idle;
        }

        var enemies = this.FindVisibleEnemies();
        var shouldRest = this.useMeditation.Value && this.ShouldRest();

        var trap = this.handleTraps.Value ? this.FindTrap() : null;

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
                var actions = this.FindPotentialActions();
                if (actions.Any()) { this.HandleActions(actions); break; }
                this.Logger.LogMessage("Nothing to do.");
                Msg.Say("noTargetFound");
                this.isEnable = false;
                break;
            default:
                break;
        }
        //this.Logger.LogInfo("Current State is " + this.state);
    }

    private List<AIAct> FindPotentialActions()
    {
        var unexplored = this.FindUnexploredPoints();
        var loot = this.FindLoot();
        var harvestables = this.FindHarvestables();
        var mineables = this.FindMineables();
        var actions = unexplored.Concat(loot)
                                .Concat(harvestables)
                                .Concat(mineables)
                                .OrderBy(p => this.currentPos.RealDistance(p.GetDestinationPoint()))
                                .ToList();
        //this.Logger.LogInfo($"Found {actions.Count} potential actions.");
        return actions;
    }

    private List<AIAct> FindUnexploredPoints()
    {
        var map = ELayer._map;

        var cells = map.cells;
        var tasks = new List<AIAct>();
        map.ForeachPoint(point =>
        {
            if (!point.IsSeen && !point.IsBlocked && this.IsPointReachable(point))
            {
                tasks.Add(new AI_Goto(point, 1));
            }
        });

        //Logger.LogInfo($"Found {points.Count} hidden points.");
        return tasks;
    }

    private List<AIAct> FindLoot()
    {
        var map = ELayer._map;
        var tasks = new List<AIAct>();
        map.ForeachPoint(point =>
        {
            if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
            {
                var things = point.Things;
                foreach (var thing in things)
                {
                    //Logger.LogInfo(thing.ToString() + " | " +thing.GetRootCard().placeState + " | " + this.CanPick(thing));
                    if (thing.GetRootCard().placeState == PlaceState.roaming && this.CanPick(thing) && !thing.isNPCProperty)
                    {
                        tasks.Add(new AI_Goto(point, 0));
                        //Logger.LogInfo(thing.ToString() + " | " + thing.GetRootCard().placeState + " | " + this.CanPick(thing)+ " | " + thing.isNPCProperty + " | " + thing.ExistsOnMap);
                    }

                }
            }
        });

        //this.Logger.LogInfo($"Found {points.Count} loot points.");
        return tasks;
    }

    private Card? FindTrap()
    {
        var map = ELayer._map;
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
                        if (trait!.CanDisarmTrap)
                            return thing.GetRootCard();

                    }
                }
            }
        }
        return null;
    }

    private List<AIAct> FindHarvestables()
    {
        var map = ELayer._map;
        var tasks = new List<AIAct>();
        if (!this.handleHarvestables.Value) return tasks;
        map.ForeachPoint(point =>
        {
            //this.Logger.LogInfo("Checking point " + point);
            if (!this.IsPointReachable(point, 1)) return;
            var task = TaskHarvest.TryGetAct(ELayer.pc, point);
            if (task != null)
            {
                task.SetTarget(this.playerCharacter);
                if (!task.IsTooHard)
                    tasks.Add(task);
                //this.Logger.LogInfo(task.ToString() + " " + task.target + task.TargetType);
            }
        });
        return tasks;
    }

    private List<AIAct> FindMineables()
    {
        var map = ELayer._map;
        var tasks = new List<AIAct>();
        if (!this.handleMineables.Value) return tasks;
        map.ForeachPoint(point =>
        {
            if (!this.IsPointReachable(point, 1)) return;
            var canMine = TaskMine.CanMine(point, this.playerCharacter.Tool);
            if (canMine)
            {
                var task = new TaskMine() { pos = point.Copy() };
                task.SetTarget(this.playerCharacter);
                if (!task.IsTooHard)
                    tasks.Add(task);
                //this.Logger.LogInfo($"{task.ToString()} {task.GetDestinationPoint()} {task.IsTooHard}(lvl{task.reqLv})");
            }
        });
        return tasks;
    }

    private bool ShouldRest()
    {
        return
               this.playerCharacter.hp * 100 < this.playerCharacter.MaxHP * this.minHP.Value
            || this.playerCharacter.mana.value * 100 < this.playerCharacter.mana.max * this.minMP.Value
            || this.playerCharacter.stamina.value <= 0
            ;
    }

    private bool InventoryIsFull()
    {
        return this.playerCharacter.burden.GetPhase() == 3;
    }

    public bool IsPointReachable(Point point, int distance = 0)
    {
        var path = new PathProgress();
        path.RequestPathImmediate(this.currentPos, point, distance, false);
        return path.HasPath;

    }

    private void HandleCombat(List<Chara> enemies)
    {
        if (this.handleFighting.Value == false)
        {
            this.state = State.Finished;
            this.isEnable = false;
            return;
        }
        var nearestEnemy = enemies.OrderBy(enemy => enemy.pos.Distance(this.currentPos)).First();
        this.Logger.LogMessage($"Current enemies: {enemies.Count()}");
        EClass.pc.SetAIImmediate(new GoalAutoCombat(nearestEnemy));
        this.state = State.Combat;
    }

    private List<Chara> FindVisibleEnemies()
    {
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

    private void HandleActions(List<AIAct> acts)
    {
        var ai = acts.First();
        this.Logger.LogInfo($"Doing action {ai.Name}/(move:{ai.IsMoveAI}) to point {ai.GetDestinationPoint()}");
        this.playerCharacter.SetAIImmediate(ai);
        this.state = State.Exploring;
    }

    private void HandleTrap(Card trap)
    {
        this.Logger.LogMessage("Handling trap " + trap.Name);
        if (this.currentPos.Distance(trap.pos) == 1)
            (trap.trait as TraitTrap)!.TryDisarmTrap(this.playerCharacter);
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
        var hasBed = this.playerCharacter.things.Find<TraitBed>() != null;
        var canSleep = this.playerCharacter.CanSleep() && hasBed;
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