namespace Ashfall.Sim;

/// <summary>A* over the 4-connected grid.</summary>
public static class Pathfinder
{
    /// <summary>
    /// Returns the steps from <paramref name="start"/> (exclusive) to <paramref name="goal"/> (inclusive),
    /// or null when there is no route. An empty list means start == goal.
    /// </summary>
    public static List<GridPos>? FindPath(WorldMap map, GridPos start, GridPos goal, int maxExpanded = 20000)
    {
        if (start == goal) return new List<GridPos>();
        if (!map.IsWalkable(goal)) return null;

        var open = new PriorityQueue<GridPos, int>();
        var cameFrom = new Dictionary<GridPos, GridPos>();
        var cost = new Dictionary<GridPos, int> { [start] = 0 };
        open.Enqueue(start, start.ManhattanTo(goal));
        int expanded = 0;

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal) return Rebuild(cameFrom, start, goal);
            if (++expanded > maxExpanded) return null;

            int g = cost[current];
            foreach (var next in current.Neighbors4())
            {
                if (!map.IsWalkable(next)) continue;
                int ng = g + 1;
                if (cost.TryGetValue(next, out var old) && old <= ng) continue;
                cost[next] = ng;
                cameFrom[next] = current;
                open.Enqueue(next, ng + next.ManhattanTo(goal));
            }
        }
        return null;
    }

    private static List<GridPos> Rebuild(Dictionary<GridPos, GridPos> cameFrom, GridPos start, GridPos goal)
    {
        var path = new List<GridPos>();
        var p = goal;
        while (p != start)
        {
            path.Add(p);
            p = cameFrom[p];
        }
        path.Reverse();
        return path;
    }
}
