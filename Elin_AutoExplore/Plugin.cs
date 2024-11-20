using BepInEx;
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

    private AutoExplorerConfig config;



    private void Awake()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.PatchAll();
        this.config = new AutoExplorerConfig(this.Config);
    }
    private void Unload()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.UnpatchSelf();
    }

    public void Update()
    {
        HandleInput();

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
        var shouldRest = this.config.UseMeditation.Value && this.ShouldRest();

        var trap = this.config.HandleTraps.Value ? this.FindTrap() : null;

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

    private void HandleInput()
    {
        if (EInput.isInputFieldActive) return;

        if (Input.GetKeyDown(this.config.ActivationKey.Value))
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

        if (Input.GetKeyDown(this.config.GoDownKey.Value))
        {
            this.Logger.LogInfo("Go down key pressed");
            var stairs = this.FindStairs();
            if (stairs != null)
            {
                this.playerCharacter.SetAIImmediate(stairs);
                this.state = State.Exploring;

            }
        }
        if (Input.GetKeyDown(this.config.GoUpKey.Value))
        {
            this.Logger.LogInfo("Go up key pressed");
            var stairs = this.FindStairs(false);
            if (stairs != null)
            {
                this.playerCharacter.SetAIImmediate(stairs);
                this.state = State.Exploring;
            }
        }

        //this.Logger.LogInfo("Current key: " + Input.inputString);
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

        //Logger.LogInfo($"Found {tasks.Count} hidden points.");
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

        //this.Logger.LogInfo($"Found {tasks.Count} loot points.");
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
        if (!this.config.HandleHarvestables.Value) return tasks;
        map.ForeachPoint(point =>
        {
            //this.Logger.LogInfo("Checking point " + point);
            if (!point.IsInBounds || (!point.HasObj && !point.HasThing))
                return;
            var task = TaskHarvest.TryGetAct(ELayer.pc, point);
            if (task != null)
            {
                if (!this.IsPointReachable(point, 1))
                    return;
                task.SetTarget(this.playerCharacter);
                if (!task.IsTooHard)
                    tasks.Add(task);
                //this.Logger.LogInfo(task.ToString() + " " + task.target + task.TargetType + task.IsTooHard);
            }
        });
        //this.Logger.LogInfo($"Found {tasks.Count} harvestables.");
        return tasks;
    }

    private List<AIAct> FindMineables()
    {
        var map = ELayer._map;
        var tasks = new List<AIAct>();
        if (!this.config.HandleMineables.Value) return tasks;
        map.ForeachPoint(point =>
        {
            if (!point.IsInBounds)
                return;
            var canMine = TaskMine.CanMine(point, this.playerCharacter.Tool);
            if (canMine)
            {
                // this.Logger.LogInfo($"Can mine {point}");
                if (!this.IsPointReachable(point, 1))
                    return;
                var task = new TaskMine() { pos = point.Copy() };
                task.SetTarget(this.playerCharacter);
                if (!task.IsTooHard)
                    tasks.Add(task);
                // this.Logger.LogInfo($"{task.ToString()} {task.GetDestinationPoint()} {task.IsTooHard}(lvl{task.reqLv})");
            }
        });
        //this.Logger.LogInfo($"Found {tasks.Count} mineables.");
        return tasks;
    }

    private AIAct? FindStairs(bool down = true)
    {
        var map = ELayer._map;
        AIAct? action = null;
        map.ForeachPoint(point =>
        {
            if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
            {
                var things = point.Things;
                foreach (var thing in things)
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitStairs)
                    {
                        var traitStairs = thing.trait as TraitStairs;
                        if (traitStairs?.IsDownstairs == down)
                            action = new AI_Goto(point, 0);
                    }
                }
            }
        });
        return action;
    }

    private bool ShouldRest()
    {
        return
               this.playerCharacter.hp * 100 < this.playerCharacter.MaxHP * this.config.MinHP.Value
            || this.playerCharacter.mana.value * 100 < this.playerCharacter.mana.max * this.config.MinMP.Value
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
        if (this.config.HandleFighting.Value == false)
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