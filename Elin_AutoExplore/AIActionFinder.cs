using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Elin_AutoExplore
{
    public class AIActionFinder
    {
        private Chara playerCharacter => ELayer.pc;
        private Point currentPos => this.playerCharacter.pos;
        private MapBounds currentBounds => ELayer._map.bounds;

        private AutoExplorerConfig config = Plugin.Instance.AutoExplorerConfig;

        public List<AIAct> FindPotentialActions()
        {
            var unexplored = this.FindUnexploredPoints();
            var loot = this.FindLoot();
            var harvestables = this.FindHarvestables();
            var mineables = this.FindMineables();
            var shrines = this.FindShrines();
            var statues = this.FindStatues();
            var actions = unexplored.Concat(loot)
                                    .Concat(harvestables)
                                    .Concat(mineables)
                                    .Concat(shrines)
                                    .Concat(statues)
                                    .OrderBy(p => this.currentPos.RealDistance(p.GetDestinationPoint(), this.playerCharacter))
                                    .ToList();
            return actions;
        }

        public List<AIAct> FindUnexploredPoints()
        {
            var tasks = new List<AIAct>();
            this.currentBounds.ForeachPoint(point =>
            {
                if (!point.IsSeen && !point.IsBlocked && this.IsPointReachable(point))
                {
                    tasks.Add(new AI_Goto(point, 1));
                }
            });
            return tasks;
        }

        public List<AIAct> FindLoot()
        {
            var tasks = new List<AIAct>();
            this.currentBounds.ForeachPoint(point =>
            {
                if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
                {
                    var things = point.Things;
                    foreach (var thing in things)
                    {
                        if (thing.GetRootCard().placeState == PlaceState.roaming && this.CanPick(thing))
                        {
                            tasks.Add(new AI_Goto(point, 0));
                        }
                    }
                }
            });
            return tasks;
        }

        public List<AIAct> FindHarvestables()
        {
            var tasks = new List<AIAct>();
            if (!this.config.HandleHarvestables.Value) return tasks;
            this.currentBounds.ForeachPoint(point =>
            {
                if (!point.IsInBounds || (!point.HasObj && !point.HasThing))
                    return;
                var task = TaskHarvest.TryGetAct(ELayer.pc, point);
                if (task != null)
                {
                    var targetName = task.IsObj ? task.pos.cell.sourceObj.GetName() : task.target.Name;
                    if (Plugin.Instance.IgnoreList.IsIgnoredFromGathering(targetName))
                        return;
                    if (!this.IsPointReachable(point, 1))
                        return;
                    task.SetTarget(this.playerCharacter);
                    if (!task.IsTooHard)
                        tasks.Add(task);
                }
            });
            return tasks;
        }

        public List<AIAct> FindMineables()
        {
            var tasks = new List<AIAct>();
            if (!this.config.HandleMineables.Value) return tasks;
            this.currentBounds.ForeachPoint(point =>
            {
                if (!point.IsInBounds)
                    return;
                var canMine = TaskMine.CanMine(point, this.playerCharacter.Tool);
                if (canMine)
                {
                    if (Plugin.Instance.IgnoreList.IsIgnoredFromMining(point.cell.GetBlockName()))
                        return;
                    if (!this.IsPointReachable(point, 1))
                        return;
                    var task = new TaskMine() { pos = point.Copy() };
                    task.SetTarget(this.playerCharacter);
                    if (!task.IsTooHard)
                        tasks.Add(task);
                }
            });
            return tasks;
        }

        public AIAct? FindStairs(bool down = true)
        {
            AIAct? action = null;
            this.currentBounds.ForeachPoint(point =>
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

        public Thing? FindDownStairs()
        {
            Thing? result = null;
            this.currentBounds.ForeachPoint(point =>
            {
                if (result != null || point.IsHidden || !point.HasThing || !this.CanReach(point))
                    return;
                foreach (var thing in point.Things)
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed
                        && thing.trait is TraitStairs stairs && stairs.IsDownstairs)
                    {
                        result = thing;
                        return;
                    }
                }
            });
            return result;
        }

        public Thing? FindLockedStairs()
        {
            // A "Blocked" staircase is a TraitStairsLocked item, not a TraitStairs. It may sit on a
            // blocked cell, so we don't filter on point.IsBlocked and approach it from an adjacent tile.
            Thing? result = null;
            this.currentBounds.ForeachPoint(point =>
            {
                if (result != null || point.IsHidden || !point.HasThing || !this.CanReach(point, 1))
                    return;
                foreach (var thing in point.Things)
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed
                        && thing.trait is TraitStairsLocked)
                    {
                        result = thing;
                        return;
                    }
                }
            });
            return result;
        }

        public List<AIAct> FindShrines()
        {
            var tasks = new List<AIAct>();
            if (!this.config.HandleShrines.Value) return tasks;
            this.currentBounds.ForeachPoint(point =>
            {
                if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
                {
                    var things = point.Things.ToArray();
                    foreach (var thing in things)
                    {
                        if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitShrine trait)
                        {
                            if (!trait.CanUse(this.playerCharacter))
                                continue;
                            if (trait.Shrine.id == "material" || trait.Shrine.id == "armor")
                                continue;
                            if (point.Distance(this.currentPos) > 1)
                            {
                                tasks.Add(new AI_Goto(point, 1));
                                continue;
                            }
                            //Logger.LogInfo("Using shrine " + trait.Shrine.id);
                            trait.OnUse(this.playerCharacter);
                        }
                    }
                }
            });
            return tasks;
        }

        public List<AIAct> FindStatues()
        {
            var tasks = new List<AIAct>();
            if (!this.config.HandleStatues.Value) return tasks;
            this.currentBounds.ForeachPoint(point =>
            {
                // Statues block their own cell, so we can't path onto it; only require a reachable
                // adjacent tile (CanReach with distance 1) and don't filter on point.IsBlocked.
                if (!point.IsHidden && point.HasThing && this.CanReach(point, 1))
                {
                    var things = point.Things.ToArray();
                    foreach (var thing in things)
                    {
                        // God statues only become usable when active (gold material); CanUse also blocks
                        // re-use (isOn=false after use) and use in the player's home zone.
                        if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitGodStatue trait)
                        {
                            if (!trait.CanUse(this.playerCharacter))
                                continue;
                            if (point.Distance(this.currentPos) > 1)
                            {
                                tasks.Add(new AI_Goto(point, 1));
                                continue;
                            }
                            trait.OnUse(this.playerCharacter);
                        }
                    }
                }
            });
            return tasks;
        }

        public Card? FindTrap()
        {
            var currentFov = this.playerCharacter.fov.ListPoints();
            currentFov = currentFov.OrderBy(p => p.Distance(this.currentPos)).ToList();
            foreach (var point in currentFov)
            {
                if (!point.IsHidden && !point.IsBlocked && point.HasThing && this.IsPointReachable(point))
                {
                    var things = point.Things;
                    foreach (var thing in things)
                    {
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

        public bool IsPointReachable(Point point, int distance = 0)
        {
            var path = new PathProgress() { walker = this.playerCharacter } ;
            path.RequestPathImmediate(this.currentPos, point, distance, false);
            return path.HasPath;
        }

        // A zero-length path (we're already at/within the target distance) produces no nodes, so
        // PathProgress.HasPath is false. Treat already being within range as reachable.
        public bool CanReach(Point point, int distance = 0)
        {
            return this.currentPos.Distance(point) <= distance || this.IsPointReachable(point, distance);
        }

        public bool CanPick(Thing thing)
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

            if (thing.isNPCProperty)
            {
                return false;
            }

            if (thing.ignoreAutoPick)
            {
                return false;
            }

            if (thing.isThing && thing.placeState == PlaceState.roaming && !thing.ignoreAutoPick)
            {
                if (EClass.core.config.game.advancedMenu)
                {
                    Window.SaveData dataPick = EClass.player.dataPick;
                    ContainerFlag containerFlag = thing.category.GetRoot().id.ToEnum<ContainerFlag>(true);
                    if (containerFlag == ContainerFlag.none)
                    {
                        containerFlag = ContainerFlag.other;
                    }
                    if ((!dataPick.noRotten || !thing.IsDecayed) && (!dataPick.onlyRottable || thing.trait.Decay != 0))
                    {
                        if (dataPick.userFilter)
                        {
                            Window.SaveData.FilterResult filterResult = dataPick.IsFilterPass(thing.GetName(NameStyle.Full, 1));
                            if (filterResult == Window.SaveData.FilterResult.Block)
                            {
                                return false;
                            }
                            if (filterResult == Window.SaveData.FilterResult.PassWithoutFurtherTest)
                            {
                                return true;
                            }
                        }
                        if (dataPick.advDistribution)
                        {
                            using HashSet<int>.Enumerator enumerator4 = dataPick.cats.GetEnumerator();
                            while (enumerator4.MoveNext())
                            {
                                int num4 = enumerator4.Current;
                                if (thing.category.uid == num4)
                                {
                                    return true;
                                }
                            }
                            return false;
                        }
                        if (!dataPick.flag.HasFlag(containerFlag))
                        {
                            return true;
                        }
                    }
                }
            }

            return true;
        }
    }
}
