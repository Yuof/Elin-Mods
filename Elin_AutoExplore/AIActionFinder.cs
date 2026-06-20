using System.Collections.Generic;
using System.Linq;
using Elin_AutoExplore.Flood;
using UnityEngine;

namespace Elin_AutoExplore
{
    public class AIActionFinder
    {
        private Chara playerCharacter => ELayer.pc;
        private Point currentPos => this.playerCharacter.pos;
        private MapBounds currentBounds => ELayer._map.bounds;

        private AutoExplorerConfig config = Plugin.Instance.AutoExplorerConfig;

        // One reachability flood replaces the old per-tile A* reachability checks. It is recomputed at
        // most once per frame (cached by Time.frameCount): every Find* call within a single Update reuses
        // the same result, and it refreshes next frame after the character has moved/acted.
        private readonly Dijkstra _flood = new Dijkstra();
        private int _floodFrame = -1;

        // Reusable dedup stamp for early-exit approach-target evaluation, so each candidate cell is
        // classified once per cycle without a per-flood HashSet (version bump = clear).
        private int[] _approachStamp = System.Array.Empty<int>();
        private int _approachVersion;

        private bool MarkApproach(int idx)
        {
            if (this._approachStamp[idx] == this._approachVersion) return false;
            this._approachStamp[idx] = this._approachVersion;
            return true;
        }

        private void RefreshFlood()
        {
            if (this._floodFrame == Time.frameCount) return;
            var map = ELayer._map;
            var sw = this.config.LogPerformance.Value ? System.Diagnostics.Stopwatch.StartNew() : null;
            this._flood.Run(new MapFloodGrid(map, this.playerCharacter), this.currentPos.x, this.currentPos.z);
            if (sw != null)
            {
                sw.Stop();
                Plugin.Instance.LogPerf($"[perf] flood: {sw.Elapsed.TotalMilliseconds:F3} ms, {this._flood.Result.Ordered.Count} reachable, map {map.Size}x{map.Size} = {map.Size * map.Size} cells");
            }
            this._floodFrame = Time.frameCount;
        }

        private int Key(Point p) => p.x + p.z * ELayer._map.Size;

        // Shortest octile step-distance the flood found to a point. For a blocked target (mineable wall,
        // statue, locked stairs) the point itself is unreachable, so fall back to the nearest reachable
        // neighbour plus one cardinal step. int.MaxValue means unreachable.
        private int FloodDistance(Point p)
        {
            this.RefreshFlood();
            if (this._flood.Result.TryDistance(this.Key(p), out int d)) return d;
            int best = int.MaxValue;
            p.ForeachNeighbor(nb =>
            {
                if (nb.IsInBounds && this._flood.Result.TryDistance(this.Key(nb), out int nd) && nd < best)
                    best = nd;
            });
            // +14 (diagonal cost) is a safe upper bound for the final approach step so a blocked target
            // whose nearest reachable neighbour is diagonal isn't ordered earlier than its true cost.
            return best == int.MaxValue ? int.MaxValue : best + 14;
        }

        public List<AIAct> FindPotentialActions()
        {
            if (this.config.UseEarlyExitFlood.Value)
                return this.FindNearestActionEarlyExit();

            var sw = this.config.LogPerformance.Value ? System.Diagnostics.Stopwatch.StartNew() : null;
            var unexplored = this.FindUnexploredPoints();
            var loot = this.FindLoot();
            var harvestables = this.FindHarvestables();
            var mineables = this.FindMineables();
            var shrines = this.FindShrines();
            var statues = this.FindStatues();
            var chests = this.FindChestApproaches();
            var actions = unexplored.Concat(loot)
                                    .Concat(harvestables)
                                    .Concat(mineables)
                                    .Concat(shrines)
                                    .Concat(statues)
                                    .Concat(chests)
                                    .OrderBy(p => this.FloodDistance(p.GetDestinationPoint()))
                                    .ToList();
            if (sw != null)
            {
                sw.Stop();
                Plugin.Instance.LogPerf($"[perf] decision cycle: {sw.Elapsed.TotalMilliseconds:F3} ms (incl. flood), {actions.Count} candidates");
            }
            return actions;
        }

        // Early-exit alternative to FindPotentialActions: flood outward from the player and stop at the
        // nearest actionable cell instead of flooding the whole map and scanning every tile. Mirrors the
        // per-category logic of the full-scan path. Gated by UseEarlyExitFlood (experimental).
        private List<AIAct> FindNearestActionEarlyExit()
        {
            var map = ELayer._map;
            int size = map.Size;
            var pc = this.playerCharacter;
            // Blocked-target categories (mine/harvest/statue/chest) are the only reason to inspect a settled
            // cell's neighbours. When none are enabled (the common case) we skip the neighbour scan entirely
            // and the per-cell cost drops to a single ClassifyOnTile.
            bool approachModes = this.config.HandleMineables.Value || this.config.HandleHarvestables.Value
                               || this.config.HandleStatues.Value || this.config.HandleChests.Value;
            if (approachModes)
            {
                int n = size * size;
                if (this._approachStamp.Length != n) this._approachStamp = new int[n];
                this._approachVersion++;
            }
            var sw = this.config.LogPerformance.Value ? System.Diagnostics.Stopwatch.StartNew() : null;
            AIAct? found = null;
            int settled = 0;
            this._flood.Run(new MapFloodGrid(map, pc), pc.pos.x, pc.pos.z, (key, dist) =>
            {
                settled++;
                var c = new Point(key % size, key / size);

                // dist-0: the player stands on c.
                var onTile = this.ClassifyOnTile(c);
                if (onTile != null) { found = onTile; return true; }

                if (!approachModes) return false;

                // dist-1: the player stands on c and acts on c or a neighbour (e.g. a mineable wall).
                var here = this.ClassifyApproach(c);
                if (here != null) { found = here; return true; }

                for (int dir = 0; dir < Directions.Count; dir++)
                {
                    int nx = c.x + Directions.DX[dir];
                    int nz = c.z + Directions.DZ[dir];
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    var a = this.ClassifyApproach(new Point(nx, nz));
                    if (a != null) { found = a; return true; }
                }
                return false;
            });
            if (sw != null)
            {
                sw.Stop();
                Plugin.Instance.LogPerf($"[perf] early-exit decision: {sw.Elapsed.TotalMilliseconds:F3} ms, settled {settled} cells, found={(found != null)}");
            }
            return found != null ? new List<AIAct> { found } : new List<AIAct>();
        }

        // dist-0 actions where the player stands on the (reachable, walkable) cell c: explore an unseen
        // tile, pick up loot, or use a shrine (used immediately if already adjacent). Mirrors
        // FindUnexploredPoints / FindLoot / FindShrines.
        private AIAct? ClassifyOnTile(Point c)
        {
            if (!c.IsSeen)
                return new AI_Goto(c, 1);

            if (!c.IsHidden && c.HasThing)
            {
                foreach (var thing in c.Things)
                {
                    if (thing.GetRootCard().placeState == PlaceState.roaming && this.CanPick(thing))
                        return new AI_Goto(c, 0);
                }
            }

            if (this.config.HandleShrines.Value && !c.IsHidden && c.HasThing)
            {
                foreach (var thing in c.Things.ToArray())
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitShrine trait)
                    {
                        if (!trait.CanUse(this.playerCharacter)) continue;
                        if (trait.Shrine.id == "material" || trait.Shrine.id == "armor") continue;
                        if (c.Distance(this.currentPos) > 1) return new AI_Goto(c, 1);
                        trait.OnUse(this.playerCharacter);
                    }
                }
            }
            return null;
        }

        // dist-1 actions where the player stands adjacent to target t (a mineable wall, harvestable,
        // statue, or container; t may be blocked or walkable). Deduped via `done` so a target adjacent to
        // several reachable cells is evaluated once. Mirrors FindMineables / FindHarvestables /
        // FindStatues / FindChestApproaches.
        private AIAct? ClassifyApproach(Point t)
        {
            if (!this.MarkApproach(this.Key(t))) return null;

            if (this.config.HandleMineables.Value && TaskMine.CanMine(t, this.playerCharacter.Tool)
                && !Plugin.Instance.IgnoreList.IsIgnoredFromMining(t.cell.GetBlockName()))
            {
                var task = new TaskMine() { pos = t.Copy() };
                task.SetTarget(this.playerCharacter);
                if (!task.IsTooHard) return task;
            }

            if (this.config.HandleHarvestables.Value && (t.HasObj || t.HasThing))
            {
                var task = TaskHarvest.TryGetAct(ELayer.pc, t);
                if (task != null)
                {
                    var name = task.IsObj ? task.pos.cell.sourceObj.GetName() : task.target.Name;
                    if (!Plugin.Instance.IgnoreList.IsIgnoredFromGathering(name))
                    {
                        task.SetTarget(this.playerCharacter);
                        if (!task.IsTooHard) return task;
                    }
                }
            }

            if (this.config.HandleStatues.Value && !t.IsHidden && t.HasThing)
            {
                foreach (var thing in t.Things.ToArray())
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed && thing.trait is TraitGodStatue trait)
                    {
                        if (!trait.CanUse(this.playerCharacter)) continue;
                        if (t.Distance(this.currentPos) > 1) return new AI_Goto(t, 1);
                        trait.OnUse(this.playerCharacter);
                    }
                }
            }

            if (this.config.HandleChests.Value && !t.IsHidden && t.HasThing)
            {
                foreach (var thing in t.Things.ToArray())
                {
                    if (!this.IsLootableContainer(thing)) continue;
                    if (thing.c_lockLv > 0 && !this.CanPickLock(thing)) continue;
                    if (this.currentPos.Distance(thing.pos) <= 1) continue;
                    // t is the current settled cell (reached, dist 0) or a blocked neighbour (dist 1).
                    var d = this._flood.Result.IsReached(this.Key(t)) ? 0 : 1;
                    return new AI_Goto(thing.pos, d);
                }
            }
            return null;
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

        // All reachable lootable containers (chests, etc.) on the map. A container qualifies when it is
        // still locked (contents unknown) or already holds at least one item we are allowed to pick.
        // NPC-owned containers are skipped so we never commit theft in towns; merchant chests are ignored.
        public List<Thing> FindChests()
        {
            var chests = new List<Thing>();
            if (!this.config.HandleChests.Value) return chests;
            this.currentBounds.ForeachPoint(point =>
            {
                if (point.IsHidden || !point.HasThing || !this.CanReach(point, 1))
                    return;
                foreach (var thing in point.Things.ToArray())
                {
                    if (this.IsLootableContainer(thing))
                        chests.Add(thing);
                }
            });
            return chests;
        }

        private bool IsLootableContainer(Thing thing)
        {
            var placeState = thing.GetRootCard().placeState;
            if (placeState != PlaceState.installed && placeState != PlaceState.roaming)
                return false;
            if (thing.trait is not TraitContainer || thing.trait is TraitChestMerchant)
                return false;
            if (thing.isNPCProperty)
                return false;
            return thing.c_lockLv > 0 || thing.things.Any(t => this.CanPick(t, true));
        }

        // Walk tasks toward openable containers we still need to reach, so they are looted as part of the
        // normal distance-sorted exploration instead of by backtracking at the end of the floor. Chests are
        // walkable, so we path onto them (dist 0) and they sort by real distance like floor loot; the dist-1
        // fallback covers the rare container that blocks its own cell. Adjacent chests are acted on directly
        // by the plugin; unpickable locks are left for the end-of-floor handler.
        public List<AIAct> FindChestApproaches()
        {
            var tasks = new List<AIAct>();
            foreach (var chest in this.FindChests())
            {
                if (chest.c_lockLv > 0 && !this.CanPickLock(chest))
                    continue;
                if (this.currentPos.Distance(chest.pos) <= 1)
                    continue;
                var dist = this.IsPointReachable(chest.pos) ? 0 : 1;
                tasks.Add(new AI_Goto(chest.pos, dist));
            }
            return tasks;
        }

        // Effective lockpicking skill for the player, mirroring Trait.TryOpenLock. A lockpick tool raises
        // it. If this is not strictly greater than the lock level, the lock can never be picked.
        public bool CanPickLock(Thing chest)
        {
            var tool = this.playerCharacter.things.FindBest<TraitLockpick>((Thing t) => -t.c_charges);
            var skill = tool != null
                ? this.playerCharacter.Evalue(280) + 10
                : this.playerCharacter.Evalue(280) / 2 + 2;
            return skill > chest.c_lockLv;
        }

        // Runs every frame from Plugin.Update, so check the cheap trap predicate BEFORE the reachability
        // flood: only pay for the flood when an actual disarmable trap is in view (rare), instead of for
        // every visible tile that merely has a thing on it.
        public Card? FindTrap()
        {
            var currentFov = this.playerCharacter.fov.ListPoints();
            currentFov = currentFov.OrderBy(p => p.Distance(this.currentPos)).ToList();
            foreach (var point in currentFov)
            {
                if (point.IsHidden || point.IsBlocked || !point.HasThing)
                    continue;
                foreach (var thing in point.Things)
                {
                    if (thing.GetRootCard().placeState == PlaceState.installed
                        && thing.trait is TraitTrap trait && trait.CanDisarmTrap
                        && this.IsReachableAStar(point))
                    {
                        return thing.GetRootCard();
                    }
                }
            }
            return null;
        }

        // Reachability now comes from the single per-frame flood instead of a per-call A*. For distance 0
        // the point itself must be reachable; for distance 1 (approach an adjacent tile, used for blocked
        // targets like walls/statues) a reachable 8-neighbour also counts. The flood treats "already there"
        // as reached, so the old zero-length-path workaround is no longer needed.
        public bool IsPointReachable(Point point, int distance = 0)
        {
            this.RefreshFlood();
            if (this._flood.Result.IsReached(this.Key(point))) return true;
            if (distance <= 0) return false;
            bool found = false;
            point.ForeachNeighbor(nb =>
            {
                if (!found && nb.IsInBounds && this._flood.Result.IsReached(this.Key(nb)))
                    found = true;
            });
            return found;
        }

        public bool CanReach(Point point, int distance = 0)
        {
            return this.currentPos.Distance(point) <= distance || this.IsPointReachable(point, distance);
        }

        // A single pooled A* reachability check, for callers that run every frame (FindTrap) and must not
        // trigger the full-map flood. Cheap because it is one bounded query to a nearby (in-FOV) target.
        private bool IsReachableAStar(Point point, int distance = 0)
        {
            if (this.currentPos.Distance(point) <= distance) return true;
            var path = PathManager.Instance.RequestPathImmediate(
                this.currentPos, point, this.playerCharacter, PathManager.MoveType.Default, -1, distance);
            return path.HasPath;
        }

        public bool CanPick(Thing thing, bool fromContainer = false)
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

            if (thing.isThing && (fromContainer || thing.placeState == PlaceState.roaming) && !thing.ignoreAutoPick)
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
