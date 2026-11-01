using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>
/// Earthy, slightly desaturated oil colours: ochres, umbers, sage, rust, and a murky teal.
/// The post-process shader adds brushwork and varnish on top of these.
/// </summary>
public static class Palette
{
    public static readonly Color Scrub = new(0.56f, 0.53f, 0.31f);
    public static readonly Color Dirt = new(0.53f, 0.41f, 0.28f);
    public static readonly Color Rubble = new(0.49f, 0.47f, 0.44f);
    public static readonly Color Water = new(0.21f, 0.32f, 0.35f);
    public static readonly Color Rock = new(0.35f, 0.31f, 0.29f);

    public static readonly Color Rust = new(0.62f, 0.33f, 0.18f);
    public static readonly Color DeadWood = new(0.30f, 0.22f, 0.16f);
    public static readonly Color Sage = new(0.47f, 0.56f, 0.33f);
    public static readonly Color SpringBlue = new(0.36f, 0.55f, 0.60f);
    public static readonly Color Bone = new(0.90f, 0.86f, 0.76f);
    public static readonly Color Blood = new(0.66f, 0.16f, 0.13f);
    public static readonly Color Ink = new(0.14f, 0.11f, 0.09f);

    public static readonly Color[] SettlerColors =
    {
        new(0.70f, 0.30f, 0.22f), new(0.25f, 0.42f, 0.55f), new(0.78f, 0.62f, 0.25f),
        new(0.38f, 0.52f, 0.36f), new(0.55f, 0.35f, 0.52f), new(0.80f, 0.50f, 0.32f),
        new(0.30f, 0.30f, 0.38f), new(0.62f, 0.58f, 0.50f),
    };

    public static Color For(Terrain t) => t switch
    {
        Terrain.Scrub => Scrub,
        Terrain.Dirt => Dirt,
        Terrain.Rubble => Rubble,
        Terrain.Water => Water,
        Terrain.Rock => Rock,
        _ => Dirt,
    };

    /// <summary>Deterministic per-tile variation so the ground looks hand-painted, not tiled.</summary>
    public static Color Vary(Color c, GridPos p, int salt, float amount)
    {
        float n = (float)ValueNoise.Hash(p.X, p.Y, salt) - 0.5f;
        float warm = (float)ValueNoise.Hash(p.X, p.Y, salt + 17) - 0.5f;
        return new Color(
            Mathf.Clamp(c.R * (1 + n * amount) + warm * amount * 0.15f, 0, 1),
            Mathf.Clamp(c.G * (1 + n * amount), 0, 1),
            Mathf.Clamp(c.B * (1 + n * amount) - warm * amount * 0.15f, 0, 1),
            c.A);
    }
}
