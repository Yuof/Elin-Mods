using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Elin_AutoExplore;

[BepInPlugin("yuof.elin.autoExplore.mod", "Elin AutoExplorer", "1.5.0.0")]
public class Plugin : BaseUnityPlugin
{
    private Chara playerCharacter => ELayer.pc;
    private Point currentPos => this.playerCharacter.pos;
    private MapBounds currentBounds => ELayer._map.bounds;
    private bool isEnable = false;
    private State state = State.Idle;

    // Item the player was holding when auto-eating started (AI_Eat stows it to eat); restored afterwards.
    private Card? heldBeforeEat;
    private bool eatDisplacedHeld;

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

        this.RestoreHeldAfterEating();

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
                // Disarm a trap we walked up to mid-route (only when nothing to fight), so the path past it
                // isn't blocked by the game refusing to step onto the trap. Combat keeps its own state/case.
                if (trap != null && !enemies.Any()) { this.HandleTrap(trap); break; }
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
                if (this.AutoExplorerConfig.HandleChests.Value && this.TryHandleAdjacentChest()) break;
                var actions = this.actionFinder.FindPotentialActions();
                if (actions.Any()) { this.HandleActions(actions); break; }
                if (this.AutoExplorerConfig.HandleChests.Value && this.TryHandleImpossibleChest()) break;
                if (this.AutoExplorerConfig.StopOnUnusableShrine.Value && this.TryHandleUnusableShrine()) break;
                if (this.AutoExplorerConfig.AutoDescend.Value && this.TryDescend()) break;
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

    // Used by AIActionFinder to emit profiling lines when AutoExplorerConfig.LogPerformance is on.
    internal void LogPerf(string message) => this.Logger.LogMessage(message);

    private bool ShouldRest()
    {
        var wouldSuffocate = this.currentPos.cell.CanSuffocate() && !this.playerCharacter.HasElement(1252);
        return
            !wouldSuffocate
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
            || this.playerCharacter.things.Find((Thing a) => this.playerCharacter.CanEat(a) && !a.c_isImportant) == null)
        {
            this.Logger.LogWarning("Hungry. Stopping autoExplore.");
            this.isEnable = false;
            Msg.Say("daFood");
            return;
        }

        var food = this.playerCharacter.things.Find((Thing a) => this.playerCharacter.CanEat(a, true) && !a.c_isImportant, true);
        if (food == null)
        {
            food = this.playerCharacter.things.Find((Thing a) => this.playerCharacter.CanEat(a, false) && !a.c_isImportant, true);
        }
        if (food == null)
            return;

        // AI_Eat holds the food in hand (HoldCard), which stows whatever the player was holding (e.g. a
        // pickaxe) back into the pack and leaves the hand empty once eating finishes. Remember it so
        // RestoreHeldAfterEating can put it back, while keeping AI_Eat's proper eating animation/duration.
        this.heldBeforeEat = (this.playerCharacter.held != null && this.playerCharacter.held != food)
            ? this.playerCharacter.held
            : null;
        this.eatDisplacedHeld = false;

        var ai = new AI_Eat() { target = food };
        this.playerCharacter.SetAIImmediate(ai);
        this.state = State.Resting;
    }

    // AI_Eat takes the held item out of the hand to eat. Once eating has finished, put it back so the
    // player keeps holding their tool (e.g. a pickaxe between mining actions).
    private void RestoreHeldAfterEating()
    {
        if (this.heldBeforeEat == null)
            return;

        // Wait until eating has actually taken the item out of the hand before watching for completion,
        // so we don't give up during the frame between setting the AI and it starting to run.
        if (!this.eatDisplacedHeld)
        {
            if (this.playerCharacter.held != this.heldBeforeEat)
                this.eatDisplacedHeld = true;
            return;
        }

        if (this.playerCharacter.ai.IsRunning)
            return; // still eating

        var tool = this.heldBeforeEat;
        this.heldBeforeEat = null;
        this.eatDisplacedHeld = false;
        if (!tool.isDestroyed && tool.GetRootCard() == this.playerCharacter)
            this.playerCharacter.HoldCard(tool);
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

    // Acts on a chest the moment we're standing next to it (the explore loop walks us adjacent via
    // FindChestApproaches). Lockpicks pickable locks, takes loot from open ones. Unpickable locks are
    // ignored here and dealt with by TryHandleImpossibleChest once the floor is otherwise done.
    private bool TryHandleAdjacentChest()
    {
        var chest = this.actionFinder.FindChests()
            .Where(c => this.currentPos.Distance(c.pos) <= 1)
            .OrderBy(c => this.currentPos.Distance(c.pos))
            .FirstOrDefault();
        if (chest == null)
            return false;

        if (chest.c_lockLv > 0)
        {
            if (!this.actionFinder.CanPickLock(chest))
                return false; // can't pick it; handled at end of floor

            // AI_OpenLock walks to the chest and picks it; it retries until it opens.
            this.Logger.LogMessage("Lockpicking " + chest.Name);
            this.playerCharacter.SetAIImmediate(new AI_OpenLock { target = chest });
            this.state = State.Exploring;
            return true;
        }

        this.LootChest(chest);
        this.state = State.Starting;
        return true;
    }

    // Once the floor is otherwise cleared, walk to any chest whose lock we can never pick, make one real
    // attempt so the player sees the in-game "too hard" message, then stop.
    private bool TryHandleImpossibleChest()
    {
        var chest = this.actionFinder.FindChests()
            .Where(c => c.c_lockLv > 0 && !this.actionFinder.CanPickLock(c))
            .OrderBy(c => this.currentPos.RealDistance(c.pos, this.playerCharacter))
            .FirstOrDefault();
        if (chest == null)
            return false;

        if (this.currentPos.Distance(chest.pos) > 1)
        {
            this.playerCharacter.SetAIImmediate(new AI_Goto(chest.pos, 1));
            this.state = State.Exploring;
            return true;
        }

        this.Logger.LogWarning("Chest lock too hard to pick. Stopping autoExplore.");
        chest.trait.TryOpenLock(this.playerCharacter, true);
        this.isEnable = false;
        return true;
    }

    // Once the floor is otherwise cleared, walk to the nearest shrine we can't auto-use (material/armor)
    // and stop next to it so the player can use it manually.
    private bool TryHandleUnusableShrine()
    {
        var shrine = this.actionFinder.FindUnusableShrines()
            .OrderBy(s => this.currentPos.RealDistance(s.pos, this.playerCharacter))
            .FirstOrDefault();
        if (shrine == null)
            return false;

        if (this.currentPos.Distance(shrine.pos) > 1)
        {
            this.playerCharacter.SetAIImmediate(new AI_Goto(shrine.pos, 1));
            this.state = State.Exploring;
            return true;
        }

        this.Logger.LogWarning("Reached a shrine we can't auto-use. Stopping autoExplore.");
        Msg.SayRaw(shrine.Name);
        this.isEnable = false;
        return true;
    }

    private void LootChest(Card chest)
    {
        this.Logger.LogMessage("Looting " + chest.Name);
        foreach (var thing in chest.things.ToList())
        {
            if (this.actionFinder.CanPick(thing, true))
                this.playerCharacter.Pick(thing, false);
        }
    }

    private bool TryDescend()
    {
        // A "Blocked" staircase (TraitStairsLocked) must be unlocked first. The game only allows this
        // once the floor's exit lock is cleared (i.e. the boss/lord is dead), exposed via CanUnlockExit.
        var locked = this.actionFinder.FindLockedStairs();
        if (locked != null)
        {
            if (!ELayer._zone.CanUnlockExit)
                return false; // Boss/lord not defeated yet; we can't open it.

            if (this.currentPos.Distance(locked.pos) <= 1)
            {
                this.Logger.LogMessage("Unlocking blocked staircase.");
                (locked.trait as TraitStairsLocked)!.OnUse(this.playerCharacter);
                this.state = State.Starting; // A real downstairs now exists; re-scan next cycle.
                return true;
            }

            this.playerCharacter.SetAIImmediate(new AI_Goto(locked.pos, 1));
            this.state = State.Exploring;
            return true;
        }

        var stairs = this.actionFinder.FindDownStairs();
        if (stairs != null)
        {
            // Don't descend while a Nefia boss is still alive: MoveZone would pop a confirmation dialog
            // that interrupts automation.
            if (ELayer._zone.IsNefia && ELayer._zone.Boss != null)
                return false;

            if (this.currentPos.Equals(stairs.pos))
            {
                this.Logger.LogMessage("Descending to next floor.");
                (stairs.trait as TraitStairs)!.MoveZone();
                this.state = State.Starting;
                return true;
            }

            this.playerCharacter.SetAIImmediate(new AI_Goto(stairs.pos, 0));
            this.state = State.Exploring;
            return true;
        }

        return false;
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
