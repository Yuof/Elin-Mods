using Elin_AutoExplore.Flood;
using Xunit;

public class MinHeapTests
{
    [Fact]
    public void PopsInPriorityOrder()
    {
        var h = new MinHeap(2);
        h.Push(100, 5);
        h.Push(200, 1);
        h.Push(300, 3);
        Assert.True(h.TryPop(out int k1, out int p1)); Assert.Equal(200, k1); Assert.Equal(1, p1);
        Assert.True(h.TryPop(out int k2, out _)); Assert.Equal(300, k2);
        Assert.True(h.TryPop(out int k3, out _)); Assert.Equal(100, k3);
        Assert.False(h.TryPop(out _, out _));
    }

    [Fact]
    public void GrowsBeyondInitialCapacity()
    {
        var h = new MinHeap(1);
        for (int i = 10; i >= 1; i--) h.Push(i, i);
        for (int i = 1; i <= 10; i++)
        {
            Assert.True(h.TryPop(out int k, out _));
            Assert.Equal(i, k);
        }
    }
}
