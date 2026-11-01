using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>
/// Draws terrain, deposits, and buildings. Redrawn only when the map changes, because it is
/// thousands of polygons; per-frame things (settlers, hover) live on other nodes.
/// </summary>
public partial class MapView : Node2D
{
    private Simulation _sim = null!;

    public void Init(Simulation sim)
    {
        _sim = sim;
        sim.MapChanged += QueueRedraw;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_sim is null) return;
        var map = _sim.Map;

        // Draw back to front so raised things (rocks, buildings) overlap correctly.
        for (int sum = 0; sum <= map.Width + map.Height - 2; sum++)
        {
            for (int x = Math.Max(0, sum - map.Height + 1); x <= Math.Min(sum, map.Width - 1); x++)
            {
                var p = new GridPos(x, sum - x);
                ref var tile = ref map[p];
                DrawGround(p, tile.Terrain);
                if (tile.Deposit is { } d) DrawDeposit(p, d);
            }
        }

        foreach (var b in _sim.Buildings.OrderBy(b => b.Origin.X + b.Origin.Y))
            DrawBuilding(b);

        DrawCamp();
    }

    private void DrawGround(GridPos p, Terrain t)
    {
        var baseColor = Palette.Vary(Palette.For(t), p, 3, 0.16f);
        DrawColoredPolygon(Iso.Diamond(p), baseColor);

        // Two or three loose "dabs" per tile: the shader smears these into brushwork.
        var c = Iso.ToScreen(p);
        for (int i = 0; i < 3; i++)
        {
            float hx = (float)ValueNoise.Hash(p.X, p.Y, 100 + i) - 0.5f;
            float hy = (float)ValueNoise.Hash(p.X, p.Y, 200 + i) - 0.5f;
            var center = c + new Vector2(hx * 34f, hy * 16f);
            var dab = Palette.Vary(baseColor, p, 300 + i, 0.35f);
            dab.A = 0.55f;
            float len = 6f + (float)ValueNoise.Hash(p.X, p.Y, 400 + i) * 8f;
            DrawColoredPolygon(new[]
            {
                center + new Vector2(-len, -1.5f), center + new Vector2(0, -3f),
                center + new Vector2(len, 1.5f), center + new Vector2(0, 3f),
            }, dab);
        }

        if (t == Terrain.Rock)
            DrawBlock(p, 0.35f, 14f, Palette.Vary(Palette.Rock, p, 9, 0.2f));
        else if (t == Terrain.Water && ValueNoise.Hash(p.X, p.Y, 55) < 0.3)
            DrawLine(c + new Vector2(-10, 2), c + new Vector2(12, -1), new Color(0.55f, 0.65f, 0.65f, 0.35f), 2f);
    }

    /// <summary>A raised box over part of a tile. <paramref name="halfSize"/> is in tiles.</summary>
    private void DrawBlock(GridPos p, float halfSize, float height, Color color)
    {
        var a = Iso.ToScreen(p.X - halfSize, p.Y - halfSize);
        var b = Iso.ToScreen(p.X + halfSize, p.Y - halfSize);
        var c = Iso.ToScreen(p.X + halfSize, p.Y + halfSize);
        var d = Iso.ToScreen(p.X - halfSize, p.Y + halfSize);
        DrawBox(a, b, c, d, height, color);
    }

    private void DrawBox(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float height, Color color)
    {
        var up = new Vector2(0, -height);
        DrawColoredPolygon(new[] { d, c, c + up, d + up }, color.Darkened(0.25f));
        DrawColoredPolygon(new[] { c, b, b + up, c + up }, color.Darkened(0.4f));
        DrawColoredPolygon(new[] { a + up, b + up, c + up, d + up }, color.Lightened(0.08f));
        DrawPolyline(new[] { a + up, b + up, c + up, d + up, a + up }, Palette.Ink with { A = 0.35f }, 1f);
    }

    private void DrawDeposit(GridPos p, Resource r)
    {
        var c = Iso.ToScreen(p);
        switch (r)
        {
            case Resource.Scrap:
                DrawColoredPolygon(new[] { c + new Vector2(-14, 2), c + new Vector2(-4, -8), c + new Vector2(8, -4), c + new Vector2(14, 4), c + new Vector2(0, 8) }, Palette.Vary(Palette.Rust, p, 21, 0.2f));
                DrawColoredPolygon(new[] { c + new Vector2(-6, -2), c + new Vector2(2, -12), c + new Vector2(8, -6), c + new Vector2(2, 0) }, Palette.Rubble.Lightened(0.15f));
                DrawLine(c + new Vector2(-10, -4), c + new Vector2(6, -14), Palette.Ink with { A = 0.6f }, 1.5f);
                break;
            case Resource.Wood:
                DrawLine(c, c + new Vector2(0, -30), Palette.DeadWood, 3f);
                DrawLine(c + new Vector2(0, -18), c + new Vector2(-10, -28), Palette.DeadWood, 2f);
                DrawLine(c + new Vector2(0, -22), c + new Vector2(9, -32), Palette.DeadWood, 2f);
                DrawLine(c + new Vector2(0, -12), c + new Vector2(7, -17), Palette.DeadWood, 1.5f);
                break;
            case Resource.Food:
                DrawCircle(c + new Vector2(-6, -2), 5f, Palette.Vary(Palette.Sage, p, 31, 0.2f));
                DrawCircle(c + new Vector2(5, -4), 6f, Palette.Vary(Palette.Sage, p, 32, 0.25f));
                DrawCircle(c + new Vector2(0, 3), 4.5f, Palette.Vary(Palette.Sage.Darkened(0.2f), p, 33, 0.2f));
                DrawCircle(c + new Vector2(3, -6), 1.8f, new Color(0.85f, 0.7f, 0.3f));
                break;
            case Resource.Water:
                DrawColoredPolygon(Ellipse(c, 14f, 6f), Palette.SpringBlue.Darkened(0.2f));
                DrawColoredPolygon(Ellipse(c + new Vector2(-3, -1), 7f, 2.5f), Palette.SpringBlue.Lightened(0.25f) with { A = 0.7f });
                break;
            case Resource.Medicine:
                DrawBlock(p, 0.15f, 8f, Palette.Bone);
                DrawLine(c + new Vector2(-3, -9), c + new Vector2(3, -9), Palette.Blood, 2f);
                DrawLine(c + new Vector2(0, -12), c + new Vector2(0, -6), Palette.Blood, 2f);
                break;
        }
    }

    private void DrawBuilding(Building b)
    {
        float x0 = b.Origin.X - 0.5f, y0 = b.Origin.Y - 0.5f;
        float x1 = x0 + b.Type.Width, y1 = y0 + b.Type.Height;
        var a = Iso.ToScreen(x0, y0);
        var bb = Iso.ToScreen(x1, y0);
        var c = Iso.ToScreen(x1, y1);
        var d = Iso.ToScreen(x0, y1);

        switch (b.Type.Id)
        {
            case "garden_plot":
                DrawColoredPolygon(new[] { a, bb, c, d }, Palette.Dirt.Darkened(0.25f));
                for (int i = 1; i < 4; i++)
                {
                    float t = i / 4f;
                    DrawLine(a.Lerp(d, t), bb.Lerp(c, t), Palette.Sage, 3f);
                }
                break;
            case "shelter":
                DrawBox(a, bb, c, d, 26f, new Color(0.48f, 0.38f, 0.28f));
                // Corrugated roof patch, slightly askew.
                var up = new Vector2(0, -26);
                DrawColoredPolygon(new[] { a.Lerp(bb, 0.1f) + up, bb + up, bb.Lerp(c, 0.6f) + up, a.Lerp(d, 0.5f) + up }, Palette.Rust with { A = 0.8f });
                break;
            case "rain_collector":
                DrawBox(a.Lerp(c, 0.2f), bb.Lerp(d, 0.2f), c.Lerp(a, 0.2f), d.Lerp(bb, 0.2f), 18f, new Color(0.3f, 0.36f, 0.4f));
                DrawColoredPolygon(new[] { a + new Vector2(0, -24), bb + new Vector2(0, -28), c + new Vector2(0, -24), d + new Vector2(0, -20) }, Palette.SpringBlue with { A = 0.6f });
                break;
            case "storage_crate":
                DrawBox(a.Lerp(c, 0.15f), bb.Lerp(d, 0.15f), c.Lerp(a, 0.15f), d.Lerp(bb, 0.15f), 16f, new Color(0.6f, 0.46f, 0.3f));
                break;
            case "barricade":
                DrawBox(a, bb, c, d, 14f, Palette.Rust.Darkened(0.15f));
                break;
            default:
                DrawBox(a, bb, c, d, 16f, Palette.Rubble);
                break;
        }
    }

    private void DrawCamp()
    {
        var c = Iso.ToScreen(_sim.Camp);
        // A campfire ring and the stockpile tarp everyone hauls to.
        DrawColoredPolygon(Ellipse(c, 14f, 7f), Palette.Ink with { A = 0.5f });
        DrawCircle(c + new Vector2(0, -3), 5f, new Color(0.95f, 0.55f, 0.2f));
        DrawCircle(c + new Vector2(1, -6), 3f, new Color(1f, 0.82f, 0.4f));
    }

    private static Vector2[] Ellipse(Vector2 c, float rx, float ry, int segments = 14)
    {
        var pts = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Tau * i / segments;
            pts[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
        }
        return pts;
    }
}
