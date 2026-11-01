using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>Draws settlers every frame, gliding between grid tiles, plus hover and build previews.</summary>
public partial class SettlerView : Node2D
{
    private Simulation _sim = null!;
    private readonly Dictionary<int, Vector2> _drawPos = new();

    public Settler? Selected { get; set; }
    public GridPos? Hovered { get; set; }
    public BuildingType? BuildPreview { get; set; }

    public void Init(Simulation sim) => _sim = sim;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_sim is null) return;
        DrawHover();

        var font = ThemeDB.FallbackFont;
        var dt = (float)GetProcessDeltaTime();

        foreach (var s in _sim.Settlers.OrderBy(s => s.Position.X + s.Position.Y))
        {
            var target = Iso.ToScreen(s.Position);
            var pos = _drawPos.TryGetValue(s.Id, out var prev) ? prev.Lerp(target, Mathf.Min(1f, dt * 8f)) : target;
            _drawPos[s.Id] = pos;

            if (s.IsDead)
            {
                // A small cairn marks where they fell.
                DrawCircle(pos + new Vector2(-4, -2), 4f, Palette.Rubble);
                DrawCircle(pos + new Vector2(3, -1), 3.5f, Palette.Rubble.Darkened(0.2f));
                DrawCircle(pos + new Vector2(0, -6), 3f, Palette.Rubble.Lightened(0.1f));
                continue;
            }

            var color = Palette.SettlerColors[(s.Id - 1) % Palette.SettlerColors.Length];
            bool asleep = s.Activity == SettlerActivity.Sleeping;

            DrawSetTransform(pos, 0, new Vector2(1f, 0.45f));
            DrawCircle(Vector2.Zero, 10f, Palette.Ink with { A = 0.35f });
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);

            if (s == Selected)
            {
                DrawSetTransform(pos, 0, new Vector2(1f, 0.5f));
                DrawArc(Vector2.Zero, 15f, 0, Mathf.Tau, 24, Palette.Bone, 2f);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }

            if (asleep)
            {
                DrawColoredPolygon(new[] { pos + new Vector2(-12, -2), pos + new Vector2(10, -6), pos + new Vector2(12, 0), pos + new Vector2(-10, 4) }, color.Darkened(0.2f));
                DrawCircle(pos + new Vector2(-12, -3), 4.5f, Palette.Bone.Darkened(0.15f));
                DrawString(font, pos + new Vector2(4, -14), "z", HorizontalAlignment.Left, -1, 12, Palette.Bone);
            }
            else
            {
                DrawColoredPolygon(new[] { pos + new Vector2(-6, 0), pos + new Vector2(-5, -18), pos + new Vector2(5, -18), pos + new Vector2(6, 0) }, color);
                DrawCircle(pos + new Vector2(0, -23), 5.5f, Palette.Bone.Darkened(0.1f));
                if (s.Carrying is { } r)
                    DrawRect(new Rect2(pos + new Vector2(4, -14), new Vector2(7, 7)), CarryColor(r));
            }

            if (s == Selected || s.Hunger < 25)
            {
                var label = s.Hunger < 25 ? $"{s.ShortName} (hungry)" : s.ShortName;
                DrawString(font, pos + new Vector2(-60, -34), label, HorizontalAlignment.Center, 120, 13, Palette.Bone);
            }
        }
    }

    private void DrawHover()
    {
        if (Hovered is not { } h || !_sim.Map.InBounds(h)) return;

        if (BuildPreview is { } type)
        {
            var result = _sim.CanPlace(type, h);
            var tint = result == PlaceResult.Ok ? new Color(0.5f, 0.85f, 0.4f, 0.45f) : new Color(0.9f, 0.3f, 0.2f, 0.45f);
            for (int dy = 0; dy < type.Height; dy++)
                for (int dx = 0; dx < type.Width; dx++)
                    DrawColoredPolygon(Iso.Diamond(new GridPos(h.X + dx, h.Y + dy), 1f), tint);
            return;
        }

        var d = Iso.Diamond(h, 1f);
        DrawPolyline(new[] { d[0], d[1], d[2], d[3], d[0] }, Palette.Bone with { A = 0.7f }, 1.5f);
    }

    private static Color CarryColor(Resource r) => r switch
    {
        Resource.Scrap => Palette.Rust,
        Resource.Wood => Palette.DeadWood.Lightened(0.2f),
        Resource.Food => Palette.Sage,
        Resource.Water => Palette.SpringBlue,
        Resource.Medicine => Palette.Bone,
        _ => Palette.Bone,
    };

    public Settler? SettlerAt(GridPos p) =>
        _sim.Settlers.Where(s => !s.IsDead).FirstOrDefault(s => s.Position == p)
        ?? _sim.Settlers.Where(s => !s.IsDead).FirstOrDefault(s => s.Position.ManhattanTo(p) == 1);
}
