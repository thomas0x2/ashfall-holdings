using Ashfall.Sim;
using Godot;

namespace Ashfall.Game;

/// <summary>Grid to screen conversion for a 2:1 isometric projection.</summary>
public static class Iso
{
    public const float TileW = 64f;
    public const float TileH = 32f;
    private const float HalfW = TileW / 2f;
    private const float HalfH = TileH / 2f;

    public static Vector2 ToScreen(float x, float y) => new((x - y) * HalfW, (x + y) * HalfH);

    public static Vector2 ToScreen(GridPos p) => ToScreen(p.X, p.Y);

    public static GridPos ToGrid(Vector2 world)
    {
        float fx = (world.X / HalfW + world.Y / HalfH) / 2f;
        float fy = (world.Y / HalfH - world.X / HalfW) / 2f;
        return new GridPos(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy));
    }

    /// <summary>The four corners of a tile's diamond: top, right, bottom, left.</summary>
    public static Vector2[] Diamond(GridPos p, float inset = 0f)
    {
        var c = ToScreen(p);
        float w = HalfW - inset * 2f, h = HalfH - inset;
        return new[]
        {
            c + new Vector2(0, -h),
            c + new Vector2(w, 0),
            c + new Vector2(0, h),
            c + new Vector2(-w, 0),
        };
    }
}
