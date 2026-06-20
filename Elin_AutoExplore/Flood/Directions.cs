namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// The 8 movement directions in PathFinder order. Cardinal indices 0..3 line up with
    /// Cell.weights: 0=Front(z-1), 1=Right(x+1), 2=Back(z+1), 3=Left(x-1).
    /// Diagonals carry the two cardinal gate components they depend on.
    /// </summary>
    public static class Directions
    {
        // dx, dz per direction.
        public static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        public static readonly int[] DZ = { -1, 0, 1, 0, -1, 1, 1, -1 };

        // For diagonals (index 4..7): the two SOURCE cardinal gate indices (0..3) that must be open
        // (height steps from the source cell toward each orthogonal intermediate cell).
        // Cardinals (0..3) reference themselves in GateA and -1 in GateB.
        public static readonly int[] GateA = { 0, 1, 2, 3, 0, 2, 2, 0 };
        public static readonly int[] GateB = { -1, -1, -1, -1, 1, 1, 3, 3 };

        // For diagonals: the two DESTINATION cardinal gate indices the game also checks — the height
        // steps from each orthogonal intermediate cell into the destination, read on the dest cell.
        // Derived from PathFinder._FindPath: dest vertical gate = (dz<0)?2:0, dest horizontal = (dx>0)?3:1.
        // Cardinals don't use these (-1).
        public static readonly int[] DestGateV = { -1, -1, -1, -1, 2, 0, 0, 2 };
        public static readonly int[] DestGateH = { -1, -1, -1, -1, 3, 3, 1, 1 };

        public const int Count = 8;

        public static bool IsDiagonal(int dir) => dir >= 4;

        // Octile step costs: cardinal 10, diagonal 14 (~10*sqrt(2)).
        public static readonly int[] Cost = { 10, 10, 10, 10, 14, 14, 14, 14 };
    }
}
