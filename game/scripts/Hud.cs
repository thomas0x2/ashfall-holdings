using System.Text;
using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>Status bar, event log, settler bio card, and controls help. Built in code to keep scenes trivial.</summary>
public partial class Hud : CanvasLayer
{
    private Label _status = null!;
    private RichTextLabel _log = null!;
    private Label _bio = null!;
    private PanelContainer _bioPanel = null!;
    private Label _help = null!;
    private int _lastLogCount = -1;

    public override void _Ready()
    {
        Layer = 20;

        _status = new Label();
        var statusPanel = Panel(_status);
        statusPanel.Position = new Vector2(16, 16);
        AddChild(statusPanel);

        _log = new RichTextLabel { BbcodeEnabled = true, ScrollFollowing = true, FitContent = false, CustomMinimumSize = new Vector2(560, 170) };
        _log.AddThemeFontSizeOverride("normal_font_size", 14);
        var logPanel = Panel(_log);
        AddChild(logPanel);
        logPanel.GrowVertical = Control.GrowDirection.Begin;
        logPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 16);

        _bio = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(360, 0) };
        _bioPanel = Panel(_bio);
        AddChild(_bioPanel);
        _bioPanel.GrowHorizontal = Control.GrowDirection.Begin;
        _bioPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight, Control.LayoutPresetMode.Minsize, 16);
        _bioPanel.Visible = false;

        _help = new Label
        {
            Text = "Space pause  ·  1 2 3 speed  ·  B build (cycle)  ·  Esc cancel  ·  Click settler for bio  ·  WASD / wheel camera  ·  P toggle paint",
        };
        _help.AddThemeFontSizeOverride("font_size", 13);
        _help.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 0.7f, 0.8f));
        _help.HorizontalAlignment = HorizontalAlignment.Right;
        AddChild(_help);
        _help.GrowHorizontal = Control.GrowDirection.Begin;
        _help.GrowVertical = Control.GrowDirection.Begin;
        _help.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight, Control.LayoutPresetMode.Minsize, 12);
    }

    private static PanelContainer Panel(Control content)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.13f, 0.10f, 0.08f, 0.86f),
            BorderColor = new Color(0.55f, 0.42f, 0.26f, 0.9f),
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        content.AddThemeColorOverride("font_color", new Color(0.92f, 0.87f, 0.76f));
        panel.AddChild(content);
        return panel;
    }

    public void Refresh(Simulation sim, bool paused, int speed, BuildingType? buildMode, Settler? selected)
    {
        var sb = new StringBuilder();
        string clock = $"{sim.Hour:00}:{sim.Minute:00}";
        string speedText = paused ? "PAUSED" : new string('>', speed);
        sb.Append($"{sim.SettlementName}  ·  Day {sim.Day}, {clock}{(sim.IsNight ? " (night)" : "")}  ·  {speedText}\n");
        foreach (var r in Enum.GetValues<Resource>())
            sb.Append($"{r}: {sim.Stockpile[r]}    ");
        int alive = sim.Settlers.Count(s => !s.IsDead);
        sb.Append($"\nSettlers: {alive}");
        if (buildMode is { } b)
            sb.Append($"\nBuilding: {b.Name} ({string.Join(", ", b.Cost.Select(kv => $"{kv.Value} {kv.Key}"))}). {b.Description}");
        _status.Text = sb.ToString();

        if (sim.Log.Entries.Count != _lastLogCount)
        {
            _lastLogCount = sim.Log.Entries.Count;
            var lb = new StringBuilder();
            foreach (var e in sim.Log.Recent(40))
            {
                string color = e.Kind switch
                {
                    LogKind.Good => "#a9c27a",
                    LogKind.Warning => "#e0b45c",
                    LogKind.Bad => "#e07a5f",
                    _ => "#e8dcc2",
                };
                long day = e.Tick / Simulation.TicksPerDay + 1;
                long hour = e.Tick % Simulation.TicksPerDay / Simulation.TicksPerHour;
                lb.Append($"[color=#9b8b70]D{day} {hour:00}h[/color] [color={color}]{e.Text}[/color]\n");
            }
            _log.Text = lb.ToString();
        }

        _bioPanel.Visible = selected is not null;
        if (selected is { } s)
            _bio.Text = Bio(s);
    }

    private static string Bio(Settler s)
    {
        var sb = new StringBuilder();
        sb.Append($"{s.DisplayName}, {s.Age}\n");
        sb.Append($"Doing: {Describe(s.Activity)}   Mood: {s.Mood}\n");
        sb.Append($"Hunger {s.Hunger:0}   Rest {s.Rest:0}   Health {s.Health:0}\n\n");
        sb.Append($"Childhood: {s.Childhood}\n\n");
        sb.Append($"Before the end: {s.Adulthood}\n\n");
        sb.Append("Traits:\n");
        foreach (var t in s.Traits) sb.Append($"  {t.Name}: {t.Description}\n");
        sb.Append("\nSkills: ");
        sb.Append(string.Join(", ", s.Skills.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
        return sb.ToString();
    }

    private static string Describe(SettlerActivity a) => a switch
    {
        SettlerActivity.Idle => "loitering with intent",
        SettlerActivity.WalkingToDeposit => "heading out to scavenge",
        SettlerActivity.Gathering => "gathering",
        SettlerActivity.Hauling => "hauling loot home",
        SettlerActivity.WalkingToEat => "going to eat",
        SettlerActivity.Eating => "eating, loudly",
        SettlerActivity.Sleeping => "asleep",
        _ => a.ToString(),
    };
}
