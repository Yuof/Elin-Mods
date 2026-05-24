using System;

public static class Extensions
{
    public static Point GetDestinationPoint(this AIAct act)
    {
        if (act is TaskPoint taskPoint)
        {
            return taskPoint.pos;
        }
        else
        {
            return act.GetDestination();
        }
    }

    public static int RealDistance(this Point a, Point b, IPathfindWalker walker)
    {
        var path = new PathProgress() { walker = walker };
        path.RequestPathImmediate(a, b, 0, false);
        if (path.nodes.Count == 0)
        {
            return int.MaxValue;
        }
        return path.nodes.Count;
    }
}
