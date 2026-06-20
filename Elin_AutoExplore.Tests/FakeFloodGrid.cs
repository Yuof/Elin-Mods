using Elin_AutoExplore.Flood;

/// <summary>
/// Builds an IFloodGrid from ASCII rows: '.' walkable, '#' wall/blocked, 'S' start (walkable).
/// Row 0 is z=0. Mirrors the game rule: cardinal open iff dest not blocked (height gates all
/// open in the fake); diagonal open iff dest + both orthogonal cells not blocked.
/// </summary>
public sealed class FakeFloodGrid : IFloodGrid
{
    private readonly bool[,] _blocked; // [x,z]
    public int Size { get; }
    public int StartX { get; private set; }
    public int StartZ { get; private set; }

    private FakeFloodGrid(int size) { Size = size; _blocked = new bool[size, size]; }

    public int Key(int x, int z) => x + z * Size;

    public static FakeFloodGrid Parse(string[] rows)
    {
        int size = rows.Length; // assume square for tests
        var g = new FakeFloodGrid(size);
        for (int z = 0; z < size; z++)
            for (int x = 0; x < size; x++)
            {
                char c = rows[z][x];
                g._blocked[x, z] = c == '#';
                if (c == 'S') { g.StartX = x; g.StartZ = z; }
            }
        return g;
    }

    private bool InBounds(int x, int z) => x >= 0 && z >= 0 && x < Size && z < Size;
    private bool Blocked(int x, int z) => !InBounds(x, z) || _blocked[x, z];

    public bool CanEnter(int x, int z, int dir)
    {
        int nx = x + Directions.DX[dir];
        int nz = z + Directions.DZ[dir];
        if (!InBounds(nx, nz) || Blocked(nx, nz)) return false;
        if (!Directions.IsDiagonal(dir)) return true; // height gates all open in fake
        // No corner cutting: both orthogonal cells of the diagonal must be open.
        bool orthoXOpen = !Blocked(x + Directions.DX[dir], z);
        bool orthoZOpen = !Blocked(x, z + Directions.DZ[dir]);
        return orthoXOpen && orthoZOpen;
    }
}
