using System;

namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// Minimal binary min-heap keyed by int priority. netstandard2.0 has no PriorityQueue.
    /// Stores (key, priority) pairs; pops the lowest priority first.
    /// </summary>
    public sealed class MinHeap
    {
        private int[] _keys;
        private int[] _prio;
        private int _count;

        public MinHeap(int capacity = 256)
        {
            if (capacity < 1) capacity = 1;
            _keys = new int[capacity];
            _prio = new int[capacity];
            _count = 0;
        }

        public int Count => _count;

        public void Clear() => _count = 0;

        public void Push(int key, int priority)
        {
            if (_count == _keys.Length)
            {
                Array.Resize(ref _keys, _keys.Length * 2);
                Array.Resize(ref _prio, _prio.Length * 2);
            }
            int i = _count++;
            _keys[i] = key;
            _prio[i] = priority;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_prio[parent] <= _prio[i]) break;
                Swap(parent, i);
                i = parent;
            }
        }

        public bool TryPop(out int key, out int priority)
        {
            if (_count == 0)
            {
                key = 0; priority = 0;
                return false;
            }
            key = _keys[0];
            priority = _prio[0];
            _count--;
            if (_count > 0)
            {
                _keys[0] = _keys[_count];
                _prio[0] = _prio[_count];
                SiftDown(0);
            }
            return true;
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                if (l < _count && _prio[l] < _prio[smallest]) smallest = l;
                if (r < _count && _prio[r] < _prio[smallest]) smallest = r;
                if (smallest == i) break;
                Swap(smallest, i);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            int kt = _keys[a], pt = _prio[a];
            _keys[a] = _keys[b]; _prio[a] = _prio[b];
            _keys[b] = kt; _prio[b] = pt;
        }
    }
}
