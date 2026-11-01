namespace Ashfall.Sim;

/// <summary>A tile coordinate on the settlement grid.</summary>
public readonly record struct GridPos(int X, int Y)
{
    private static readonly GridPos[] Offsets4 =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    public IEnumerable<GridPos> Neighbors4()
    {
        foreach (var o in Offsets4)
            yield return new GridPos(X + o.X, Y + o.Y);
    }

    public int ManhattanTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

    public override string ToString() => $"({X},{Y})";
}
