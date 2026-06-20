namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// IFloodGrid over the live game map. Reproduces the A*'s per-step passability:
    /// cardinal = source height-gate (Cell.weights[dir]) + dest not IsPathBlocked;
    /// diagonal = both component height-gates + both orthogonal cells not blocked + dest not blocked.
    /// </summary>
    public sealed class MapFloodGrid : IFloodGrid
    {
        private readonly Map _map;
        private readonly Chara _walker;
        private readonly bool _checkDestGates;
        private readonly int _size;
        // Per-cell IsPathBlocked result, computed lazily and cached so each cell's (virtual) IsPathBlocked
        // runs once per flood instead of ~8-16x as a neighbour/destination of other cells.
        // 0 = unknown, 1 = free, 2 = blocked.
        private readonly byte[] _blockedCache;
        private const PathManager.MoveType Move = PathManager.MoveType.Default;

        // checkDestGates: include the destination-side diagonal height gates the game's A* enforces.
        // Defaults true (correct). Pass false only to reproduce the old (incomplete) rule for comparison.
        public MapFloodGrid(Map map, Chara walker, bool checkDestGates = true)
        {
            _map = map;
            _walker = walker;
            _checkDestGates = checkDestGates;
            _size = map.Size;
            _blockedCache = new byte[_size * _size];
        }

        public int Size => _size;

        private bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < _size && z < _size;

        private bool Blocked(int x, int z)
        {
            if (x < 0 || z < 0 || x >= _size || z >= _size) return true;
            int idx = x + z * _size;
            byte c = _blockedCache[idx];
            if (c == 0)
            {
                c = _map.cells[x, z].IsPathBlocked(_walker, Move) ? (byte)2 : (byte)1;
                _blockedCache[idx] = c;
            }
            return c == 2;
        }

        public bool CanEnter(int x, int z, int dir)
        {
            int nx = x + Directions.DX[dir];
            int nz = z + Directions.DZ[dir];
            if (!InBounds(nx, nz)) return false;

            Cell src = _map.cells[x, z];

            if (!Directions.IsDiagonal(dir))
            {
                if (src.weights[dir] == 0) return false;            // height step too large
                return !Blocked(nx, nz);                            // dest walkable
            }

            int ga = Directions.GateA[dir];
            int gb = Directions.GateB[dir];
            if (src.weights[ga] == 0 || src.weights[gb] == 0) return false; // source height gates
            // No corner cutting: both orthogonal intermediate cells must be passable.
            if (Blocked(x + Directions.DX[dir], z)) return false;
            if (Blocked(x, z + Directions.DZ[dir])) return false;
            // Destination height gates: the game also checks the step from each intermediate cell into
            // the destination (read on the dest cell). Skipping these marks height-blocked diagonals as
            // reachable on terrain with elevation changes.
            if (this._checkDestGates)
            {
                Cell dest = _map.cells[nx, nz];
                if (dest.weights[Directions.DestGateV[dir]] == 0 || dest.weights[Directions.DestGateH[dir]] == 0)
                    return false;
            }
            return !Blocked(nx, nz);
        }
    }
}
