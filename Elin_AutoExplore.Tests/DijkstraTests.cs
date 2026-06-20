using Elin_AutoExplore.Flood;
using Xunit;

public class DijkstraTests
{
    // FakeFloodGrid parses an ASCII map: '.' walkable, '#' wall, 'S' start.
    [Fact]
    public void FloodsOpenRoomAndOrdersByDistance()
    {
        var grid = FakeFloodGrid.Parse(new[]
        {
            "S..",
            "...",
            "...",
        });
        var d = new Dijkstra();
        d.Run(grid, grid.StartX, grid.StartZ);

        Assert.True(d.Result.TryDistance(grid.Key(0, 0), out int d00)); Assert.Equal(0, d00);
        Assert.True(d.Result.TryDistance(grid.Key(1, 0), out int d10)); Assert.Equal(10, d10); // cardinal
        Assert.True(d.Result.TryDistance(grid.Key(1, 1), out int d11)); Assert.Equal(14, d11); // diagonal

        // Ordered is nondecreasing.
        int prev = -1;
        foreach (int k in d.Result.Ordered)
        {
            d.Result.TryDistance(k, out int dist);
            Assert.True(dist >= prev);
            prev = dist;
        }
    }

    [Fact]
    public void WallSplitsReachability()
    {
        var grid = FakeFloodGrid.Parse(new[]
        {
            "S#.",
            ".#.",
            ".#.",
        });
        var d = new Dijkstra();
        d.Run(grid, grid.StartX, grid.StartZ);
        Assert.True(d.Result.IsReached(grid.Key(0, 2)));   // left column reachable
        Assert.False(d.Result.IsReached(grid.Key(2, 0)));  // right column sealed off
    }

    [Fact]
    public void NoDiagonalCornerCutting()
    {
        // Walls at (1,0) and (0,1) must block the (0,0)->(1,1) diagonal.
        var grid = FakeFloodGrid.Parse(new[]
        {
            "S#",
            "#.",
        });
        var d = new Dijkstra();
        d.Run(grid, grid.StartX, grid.StartZ);
        Assert.False(d.Result.IsReached(grid.Key(1, 1)));
    }

    [Fact]
    public void EarlyExitStopsAtGoal()
    {
        var grid = FakeFloodGrid.Parse(new[]
        {
            "S....",
            ".....",
            ".....",
            ".....",
            ".....",
        });
        var d = new Dijkstra();
        int goalKey = grid.Key(4, 0);
        int hit = d.Run(grid, grid.StartX, grid.StartZ, (key, dist) => key == goalKey);
        Assert.Equal(goalKey, hit);
        Assert.True(d.Result.IsReached(goalKey));
    }
}
