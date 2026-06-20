using System.Collections.Generic;

namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// Holds per-cell distances and the settled cells in distance order. The distance buffer
    /// is reused across cycles; a per-run version stamp avoids clearing Size*Size entries
    /// every cycle (a cell is "reached this run" iff _stamp[i] == _version).
    /// </summary>
    public sealed class FloodResult
    {
        private int _size;
        private int[] _dist = System.Array.Empty<int>();
        private int[] _stamp = System.Array.Empty<int>();
        private int _version;

        /// <summary>Settled cells in nondecreasing-distance (pop) order. Keys are x + z*Size.</summary>
        public readonly List<int> Ordered = new List<int>();

        public int Size => _size;

        public void BeginRun(int size)
        {
            if (_size != size)
            {
                _size = size;
                _dist = new int[size * size];
                _stamp = new int[size * size];
                _version = 0;
            }
            _version++;
            Ordered.Clear();
        }

        public void Settle(int key, int distance)
        {
            _stamp[key] = _version;
            _dist[key] = distance;
            Ordered.Add(key);
        }

        public bool IsReached(int key) => _stamp[key] == _version;

        public bool TryDistance(int key, out int distance)
        {
            if (_stamp[key] == _version) { distance = _dist[key]; return true; }
            distance = int.MaxValue;
            return false;
        }
    }
}
