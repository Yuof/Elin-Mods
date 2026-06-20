using System;

namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// Weighted (octile) Dijkstra reachability flood over an IFloodGrid. Reuses a MinHeap and a
    /// FloodResult buffer to avoid per-cycle allocations.
    ///
    /// Two modes:
    ///  - Run(...) with goal == null: full flood (settles every reachable cell).
    ///  - Run(...) with a goal predicate: early-exit — stops once goal(key, distance) returns true
    ///    for a just-settled cell. The flood is left valid up to that point.
    ///
    /// Both modes populate `Result` (Ordered + distances) identically up to the stopping point.
    /// </summary>
    public sealed class Dijkstra
    {
        private readonly MinHeap _heap = new MinHeap();
        public readonly FloodResult Result = new FloodResult();

        /// <param name="goal">Optional early-exit predicate over (key, distance) of a settled cell.</param>
        /// <returns>The settled key that satisfied `goal`, or -1 (full flood / goal never met).</returns>
        public int Run(IFloodGrid grid, int startX, int startZ, Func<int, int, bool>? goal = null)
        {
            int size = grid.Size;
            Result.BeginRun(size);
            _heap.Clear();

            int startKey = startX + startZ * size;
            _heap.Push(startKey, 0);

            while (_heap.TryPop(out int key, out int dist))
            {
                if (Result.IsReached(key)) continue; // stale heap entry (already settled at <= dist)
                Result.Settle(key, dist);

                if (goal != null && goal(key, dist))
                    return key;

                int x = key % size;
                int z = key / size;
                for (int dir = 0; dir < Directions.Count; dir++)
                {
                    if (!grid.CanEnter(x, z, dir)) continue;
                    int nx = x + Directions.DX[dir];
                    int nz = z + Directions.DZ[dir];
                    int nkey = nx + nz * size;
                    if (Result.IsReached(nkey)) continue;
                    _heap.Push(nkey, dist + Directions.Cost[dir]);
                }
            }
            return -1;
        }
    }
}
