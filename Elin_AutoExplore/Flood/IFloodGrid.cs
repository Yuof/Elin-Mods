namespace Elin_AutoExplore.Flood
{
    /// <summary>
    /// Abstracts a walkable grid for the Dijkstra flood. Implemented by MapFloodGrid (game)
    /// and FakeFloodGrid (tests). All passability/corner rules live in CanEnter so the
    /// flood algorithm stays game-agnostic and testable.
    /// </summary>
    public interface IFloodGrid
    {
        int Size { get; }

        /// <summary>
        /// True iff a walker may step from (x,z) into the neighbour in direction `dir`
        /// (0..7, see Directions). Implementations must enforce bounds, the cardinal
        /// height-gate, the destination blocked-check, and the diagonal no-corner-cutting rule.
        /// </summary>
        bool CanEnter(int x, int z, int dir);
    }
}
