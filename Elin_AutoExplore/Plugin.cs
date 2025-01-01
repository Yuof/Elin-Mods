using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Elin_AutoExplore;

[BepInPlugin("yuof.elin.autoExplore.mod", "Elin AutoExplorer", "1.2.0.1")]
public class Plugin : BaseUnityPlugin
{
    private Chara playerCharacter => ELayer.pc;
    private Point currentPos => this.playerCharacter.pos;
    private MapBounds currentBounds => ELayer._map.bounds;
    private bool isEnable = false;
    private State state = State.Idle;

    public AutoExplorerConfig AutoExplorerConfig { get; private set; } = null!;
    public static Plugin Instance { get; private set; } = null!;
    public  IgnoreList IgnoreList { get; private set; } = null!;

    private AIActionFinder actionFinder = null!;

    private void Awake()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.PatchAll();
        Instance = this;
        this.AutoExplorerConfig = new AutoExplorerConfig(this.Config);
        this.IgnoreList = new IgnoreList(this.AutoExplorerConfig.GatheringRestrictionList, AutoExplorerConfig.MiningRestrictionList);
        this.actionFinder = new AIActionFinder();

        //this.Logger.LogInfo("AutoExplorer loaded.");
        //this.Logger.LogInfo(harmony.GetPatchedMethods().Select(@base => @base.Name.ToString()).Join());
    }

    private void Unload()
    {
        var harmony = new Harmony("yuof.elin.autoExplore.mod");
        harmony.UnpatchSelf();
    }

    public void Update()
    {
        this.HandleInput();

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

        if (this.Drowning())
        {
            this.Logger.LogWarning("Drowning. Stopping autoExplore.");
            this.isEnable = false;
            Msg.Say("regionAbortMove");
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
        var shouldRest = this.AutoExplorerConfig.UseMeditation.Value && this.ShouldRest();
        var shouldEat = this.AutoExplorerConfig.HandleHunger.Value != AutoExplorerConfig.HungerMode.Ignore && this.IsHungry();

        var trap = this.AutoExplorerConfig.HandleTraps.Value ? this.actionFinder.FindTrap() : null;

        switch (this.state)
        {
            case State.Exploring:
                if (shouldEat) { this.HandleFood(); break; }
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
                if (shouldEat) { this.HandleFood(); break; }
                if (shouldRest) { this.HandleResting(); break; }
                if (trap != null) { this.HandleTrap(trap); break; }
                var actions = this.actionFinder.FindPotentialActions();
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

        if (this.AutoExplorerConfig.ToggleHarvestingAndMiningMode.Value.IsDown())
        {
            this.Logger.LogInfo("Toggle Harvesting and Mining mode key pressed");
            this.AutoExplorerConfig.SetNextMode();
            return;
        }

        if (Input.GetKeyDown(this.AutoExplorerConfig.ActivationKey.Value))
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

        if (Input.GetKeyDown(this.AutoExplorerConfig.GoDownKey.Value))
        {
            this.Logger.LogInfo("Go down key pressed");
            var stairs = this.actionFinder.FindStairs();
            if (stairs != null)
            {
                this.playerCharacter.SetAIImmediate(stairs);
                this.state = State.Exploring;
            }
        }
        if (Input.GetKeyDown(this.AutoExplorerConfig.GoUpKey.Value))
        {
            this.Logger.LogInfo("Go up key pressed");
            var stairs = this.actionFinder.FindStairs(false);
            if (stairs != null)
            {
                this.playerCharacter.SetAIImmediate(stairs);
                this.state = State.Exploring;
            }
        }

        //this.Logger.LogInfo("Current key: " + Input.inputString);
    }

    private bool ShouldRest()
    {
        return
            !this.currentPos.cell.CanSuffocate()
               && (
                   this.playerCharacter.hp * 100 < this.playerCharacter.MaxHP * this.AutoExplorerConfig.MinHP.Value
                   || this.playerCharacter.mana.value * 100 < this.playerCharacter.mana.max * this.AutoExplorerConfig.MinMP.Value
                   || this.playerCharacter.stamina.value <= 0
                   );
    }

    private bool InventoryIsFull()
    {
        return this.playerCharacter.burden.GetPhase() >= 3;
    }

    private bool Drowning()
    {
        if (!this.playerCharacter.HasCondition<ConSuffocation>())
            return false;
        return this.playerCharacter.GetCondition<ConSuffocation>().value >= 100;
    }

    private bool IsHungry()
    {
        //this.Logger.LogInfo("Hunger: " + this.playerCharacter.hunger.value);
        return this.playerCharacter.hunger.GetPhase() >= 3;
    }

    private void HandleFood()
    {
        if (this.AutoExplorerConfig.HandleHunger.Value == AutoExplorerConfig.HungerMode.StopAutoExplore
            || this.playerCharacter.things.Find((Thing a) => this.playerCharacter.CanEat(a)) == null)
        {
            this.Logger.LogWarning("Hungry. Stopping autoExplore.");
            this.isEnable = false;
            Msg.Say("regionAbortMove");
            return;
        }
        this.playerCharacter.InstantEat();
    }

    private void HandleCombat(List<Chara> enemies)
    {
        if (this.AutoExplorerConfig.HandleFighting.Value == false)
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
        var allChars = map.charas.ToList();
        var currentCharas = allChars.Where(chara => currentFov.Contains(chara.pos)).ToList();
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
