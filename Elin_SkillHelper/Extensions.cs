public static class Extensions
{
    public static Point GetDestinationPoint(this AIAct act)
    {
        if (act is TaskPoint taskPoint)
        {
            return taskPoint.pos;
        }
        else if (act is AI_TargetThing thing)
        {
            return thing.target.pos;
        }
        else
        {
            return act.GetDestination();
        }
    }

    public static int RealDistance(this Point a, Point b)
    {
        var path = new PathProgress();
        path.RequestPathImmediate(a, b, 0, false);
        if (path.nodes.Count == 0)
        {
            return int.MaxValue;
        }
        return path.nodes.Count;
    }
}
