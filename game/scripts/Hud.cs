using System.Text;
using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>
/// The whole game UI: top bar (clock, resources, speed), settler roster, settler detail card,
/// building picker, and event log. Built in code; Main listens to its events.
/// </summary>
public partial class Hud : CanvasLayer
{
    public event Action<BuildingType?>? BuildPicked;
    /// <summary>0 pauses, 1 to 3 set the speed.</summary>
    public event Action<int>? SpeedPicked;
    public event Action<Settler?>? SettlerPicked;

    private const float TopBarHeight = 52;
    private const float Margin = 12;

    private Simulation _sim = null!;
    private Control _root = null!;

    // Top bar.
    private Label _clock = null!;
    private Label _fps = null!;
    private readonly Dictionary<Resource, Label> _resourceLabels = new();
    private readonly Dictionary<Resource, Control> _resourceChips = new();
    private readonly Dictionary<Resource, int> _stockAnHourAgo = new();
    private readonly Dictionary<Resource, int> _perHour = new();
    private long _lastSnapshotTick = long.MinValue;
    private readonly Button[] _speedButtons = new Button[4];

    // Settler roster.
    private PanelContainer _rosterPanel = null!;
    private VBoxContainer _rosterList = null!;
    private Label _rosterHeader = null!;
    private Button _rosterToggle = null!;
    private readonly List<RosterRow> _rows = new();
    private bool _rosterOpen = true;

    // Settler detail.
    private PanelContainer _detailPanel = null!;
    private VBoxContainer _detailBody = null!;
    private Settler? _detailFor;
    private Label _detailActivity = null!;
    private readonly List<(ProgressBar Bar, Label Value, Func<Settler, double> Get)> _detailBars = new();

    // Build bar.
    private PanelContainer _buildPanel = null!;
    private Label _buildHint = null!;
    private readonly Dictionary<BuildingType, Button> _buildButtons = new();

    // Event log.
    private PanelContainer _logPanel = null!;
    private RichTextLabel _log = null!;
    private Button _logToggle = null!;
    private int _lastLogCount = -1;
    private bool _logOpen = true;

    private sealed class RosterRow
    {
        public required Settler Settler;
        public required Button Button;
        public required Label Name;
        public required Label Activity;
        public required ProgressBar Hunger;
        public required ProgressBar Rest;
        public required ProgressBar Health;
        public required Label Mood;
    }

    public void Init(Simulation sim)
    {
        _sim = sim;
        Layer = 20;

        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Create() };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        BuildTopBar();
        BuildRoster();
        BuildDetail();
        BuildBuildBar();
        BuildLog();

        foreach (var r in Enum.GetValues<Resource>())
            _perHour[r] = 0;

        GetViewport().SizeChanged += DockAll;
        Callable.From(DockAll).CallDeferred();
    }

    // ------------------------------------------------------------------ layout

    private void DockAll()
    {
        Dock(_rosterPanel, Control.LayoutPreset.TopLeft, new Vector2(Margin, TopBarHeight + Margin));
        Dock(_detailPanel, Control.LayoutPreset.TopRight, new Vector2(-Margin, TopBarHeight + Margin));
        Dock(_buildPanel, Control.LayoutPreset.CenterBottom, new Vector2(0, -Margin));
        Dock(_logPanel, Control.LayoutPreset.BottomLeft, new Vector2(Margin, -Margin));
    }

    /// <summary>Snap a panel to its corner at its current minimum size, so it grows and shrinks with its content.</summary>
    private static void Dock(Control c, Control.LayoutPreset preset, Vector2 offset)
    {
        c.SetAnchorsAndOffsetsPreset(preset, Control.LayoutPresetMode.Minsize);
        c.OffsetLeft += offset.X;
        c.OffsetRight += offset.X;
        c.OffsetTop += offset.Y;
        c.OffsetBottom += offset.Y;
    }

    private void Redock() => Callable.From(DockAll).CallDeferred();

    // ------------------------------------------------------------------ top bar

    private void BuildTopBar()
    {
        var bar = new PanelContainer { Name = "TopBar" };
        bar.AddThemeStyleboxOverride("panel", UiTheme.Box(Palette.UiPanel, Palette.UiBorder, 0, 12));
        _root.AddChild(bar);
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        bar.OffsetBottom = TopBarHeight;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        bar.AddChild(row);

        var title = new VBoxContainer();
        title.AddThemeConstantOverride("separation", -2);
        title.AddChild(UiTheme.Text(_sim.SettlementName, 17, Palette.UiAccent));
        _clock = UiTheme.Text("", 13, Palette.UiMuted);
        title.AddChild(_clock);
        row.AddChild(title);

        row.AddChild(new VSeparator());

        foreach (var r in Enum.GetValues<Resource>())
        {
            var chip = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Stop };
            chip.AddThemeConstantOverride("separation", 6);
            var swatch = new ColorRect { Color = Palette.For(r), CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            chip.AddChild(swatch);
            var label = UiTheme.Text("", 15);
            chip.AddChild(label);
            row.AddChild(chip);
            _resourceLabels[r] = label;
            _resourceChips[r] = chip;
        }

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });

        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 4);
        var group = new ButtonGroup();
        string[] names = { "Pause", "1x", "2x", "3x" };
        string[] tips = { "Pause (Space)", "Normal speed (1)", "Fast (2)", "Very fast (3)" };
        for (int i = 0; i < 4; i++)
        {
            int speed = i;
            var b = UiTheme.Button(names[i], tips[i]);
            b.ToggleMode = true;
            b.ButtonGroup = group;
            b.CustomMinimumSize = new Vector2(i == 0 ? 64 : 42, 30);
            b.Pressed += () => SpeedPicked?.Invoke(speed);
            speeds.AddChild(b);
            _speedButtons[i] = b;
        }
        row.AddChild(speeds);

        var help = UiTheme.Button("?",
            "Space  pause / resume\n1 2 3  game speed\nB  cycle buildings\nLeft click  build, or select a settler\nRight click / Esc  cancel\nTab  show or hide the roster\nWASD, arrows, middle drag  pan\nMouse wheel  zoom\nP  toggle the paint filter");
        help.CustomMinimumSize = new Vector2(30, 30);
        row.AddChild(help);

        _fps = UiTheme.Text("", 12, Palette.UiMuted);
        _fps.CustomMinimumSize = new Vector2(56, 0);
        _fps.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(_fps);
    }

    // ------------------------------------------------------------------ roster

    private void BuildRoster()
    {
        _rosterPanel = new PanelContainer { Name = "Roster" };
        _root.AddChild(_rosterPanel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _rosterPanel.AddChild(col);

        var header = new HBoxContainer();
        _rosterHeader = UiTheme.Text("Settlers", 16, Palette.UiAccent);
        _rosterHeader.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_rosterHeader);
        _rosterToggle = UiTheme.Button("Hide", "Show or hide the roster (Tab)");
        _rosterToggle.Pressed += ToggleRoster;
        header.AddChild(_rosterToggle);
        col.AddChild(header);

        _rosterList = new VBoxContainer();
        _rosterList.AddThemeConstantOverride("separation", 4);
        col.AddChild(_rosterList);

        foreach (var s in _sim.Settlers)
            AddRosterRow(s);
    }

    private void AddRosterRow(Settler s)
    {
        var button = UiTheme.Button("");
        button.ToggleMode = true;
        button.CustomMinimumSize = new Vector2(300, 54);
        button.TooltipText = $"{s.DisplayName}\n{s.Adulthood}";
        button.Pressed += () => SettlerPicked?.Invoke(button.ButtonPressed ? s : null);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", side is "left" or "right" ? 10 : 6);
        button.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        margin.AddChild(col);

        var top = new HBoxContainer();
        var name = UiTheme.Text(s.ShortName, 14);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(name);
        var mood = UiTheme.Text("", 12, Palette.UiMuted);
        top.AddChild(mood);
        col.AddChild(top);

        var activity = UiTheme.Text("", 12, Palette.UiMuted);
        col.AddChild(activity);

        var bars = new HBoxContainer();
        bars.AddThemeConstantOverride("separation", 6);
        var hunger = UiTheme.Bar(Palette.Sage, 84, 6);
        var rest = UiTheme.Bar(Palette.SpringBlue, 84, 6);
        var health = UiTheme.Bar(Palette.Blood.Lightened(0.15f), 84, 6);
        bars.AddChild(hunger);
        bars.AddChild(rest);
        bars.AddChild(health);
        col.AddChild(bars);

        UiTheme.PassMouse(margin);
        _rosterList.AddChild(button);
        _rows.Add(new RosterRow { Settler = s, Button = button, Name = name, Activity = activity, Hunger = hunger, Rest = rest, Health = health, Mood = mood });
    }

    public void ToggleRoster()
    {
        _rosterOpen = !_rosterOpen;
        _rosterList.Visible = _rosterOpen;
        _rosterToggle.Text = _rosterOpen ? "Hide" : "Show";
        Redock();
    }

    // ------------------------------------------------------------------ detail card

    private void BuildDetail()
    {
        _detailPanel = new PanelContainer { Name = "Detail", Visible = false };
        _root.AddChild(_detailPanel);
        _detailBody = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        _detailBody.AddThemeConstantOverride("separation", 6);
        _detailPanel.AddChild(_detailBody);
    }

    private void RebuildDetail(Settler s)
    {
        foreach (var child in _detailBody.GetChildren())
        {
            _detailBody.RemoveChild(child);
            child.QueueFree();
        }
        _detailBars.Clear();

        var header = new HBoxContainer();
        var name = UiTheme.Text(s.DisplayName, 18, Palette.UiAccent);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        header.AddChild(name);
        var close = UiTheme.Button("Close", "Deselect (Esc)");
        close.Pressed += () => SettlerPicked?.Invoke(null);
        header.AddChild(close);
        _detailBody.AddChild(header);

        _detailActivity = UiTheme.Text("", 13, Palette.UiMuted);
        _detailBody.AddChild(_detailActivity);

        AddDetailBar("Hunger", Palette.Sage, 100, x => x.Hunger);
        AddDetailBar("Rest", Palette.SpringBlue, 100, x => x.Rest);
        AddDetailBar("Health", Palette.Blood.Lightened(0.15f), 100, x => x.Health);
        AddDetailBar("Mood", Palette.UiAccent, 100, x => x.Mood);

        _detailBody.AddChild(new HSeparator());
        AddParagraph("Childhood", s.Childhood);
        AddParagraph("Before the end", s.Adulthood);

        _detailBody.AddChild(new HSeparator());
        _detailBody.AddChild(UiTheme.Text("Traits", 14, Palette.UiAccent));
        foreach (var t in s.Traits)
            AddParagraph(t.Name, t.Description, 13);

        _detailBody.AddChild(new HSeparator());
        _detailBody.AddChild(UiTheme.Text("Skills", 14, Palette.UiAccent));
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var (skill, level) in s.Skills.OrderByDescending(kv => kv.Value))
        {
            var label = UiTheme.Text(skill.ToString(), 13);
            label.CustomMinimumSize = new Vector2(100, 0);
            grid.AddChild(label);
            var bar = UiTheme.Bar(Palette.UiAccent, 200, 8, 20);
            bar.Value = level;
            grid.AddChild(bar);
            grid.AddChild(UiTheme.Text(level.ToString(), 12, Palette.UiMuted));
        }
        _detailBody.AddChild(grid);
    }

    private void AddDetailBar(string label, Color color, double max, Func<Settler, double> get)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var l = UiTheme.Text(label, 13);
        l.CustomMinimumSize = new Vector2(60, 0);
        row.AddChild(l);
        var bar = UiTheme.Bar(color, 240, 10, max);
        bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(bar);
        var value = UiTheme.Text("", 12, Palette.UiMuted);
        value.CustomMinimumSize = new Vector2(30, 0);
        value.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(value);
        _detailBody.AddChild(row);
        _detailBars.Add((bar, value, get));
    }

    private void AddParagraph(string heading, string text, int size = 13)
    {
        var l = new Label
        {
            Text = $"{heading}: {text}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontSizeOverride("font_size", size);
        _detailBody.AddChild(l);
    }

    // ------------------------------------------------------------------ build bar

    private void BuildBuildBar()
    {
        _buildPanel = new PanelContainer { Name = "BuildBar" };
        _root.AddChild(_buildPanel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        _buildPanel.AddChild(col);

        _buildHint = UiTheme.Text("Build", 13, Palette.UiMuted);
        _buildHint.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_buildHint);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        col.AddChild(row);

        var group = new ButtonGroup { AllowUnpress = true };
        foreach (var type in BuildingCatalog.All)
        {
            var b = UiTheme.Button($"{type.Name}\n{CostText(type)}", type.Description);
            b.ToggleMode = true;
            b.ButtonGroup = group;
            b.CustomMinimumSize = new Vector2(124, 52);
            b.Pressed += () => BuildPicked?.Invoke(b.ButtonPressed ? type : null);
            row.AddChild(b);
            _buildButtons[type] = b;
        }
    }

    private static string CostText(BuildingType type) =>
        string.Join("  ", type.Cost.Select(kv => $"{kv.Value} {kv.Key}"));

    // ------------------------------------------------------------------ log and help

    private void BuildLog()
    {
        _logPanel = new PanelContainer { Name = "Log" };
        _root.AddChild(_logPanel);
        var col = new VBoxContainer();
        _logPanel.AddChild(col);

        var header = new HBoxContainer();
        var title = UiTheme.Text("Chronicle", 14, Palette.UiAccent);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        _logToggle = UiTheme.Button("Hide");
        _logToggle.Pressed += () =>
        {
            _logOpen = !_logOpen;
            _log.Visible = _logOpen;
            _logToggle.Text = _logOpen ? "Hide" : "Show";
            Redock();
        };
        header.AddChild(_logToggle);
        col.AddChild(header);

        _log = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            CustomMinimumSize = new Vector2(420, 150),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        col.AddChild(_log);
    }

    // ------------------------------------------------------------------ refresh

    public void Refresh(bool paused, int speed, BuildingType? buildMode, Settler? selected)
    {
        var sim = _sim;
        _clock.Text = $"Day {sim.Day}, {sim.Hour:00}:{sim.Minute:00}{(sim.IsNight ? "  night" : "")}";
        _fps.Text = $"{Engine.GetFramesPerSecond():0} fps";

        RefreshResources();

        int active = paused ? 0 : speed;
        for (int i = 0; i < 4; i++) _speedButtons[i].SetPressedNoSignal(i == active);

        RefreshRoster(selected);
        RefreshDetail(selected);
        RefreshBuildBar(buildMode);
        RefreshLog();
    }

    private void RefreshResources()
    {
        var sim = _sim;
        if (sim.CurrentTick - _lastSnapshotTick >= Simulation.TicksPerHour)
        {
            foreach (var r in Enum.GetValues<Resource>())
            {
                if (_stockAnHourAgo.TryGetValue(r, out var old)) _perHour[r] = sim.Stockpile[r] - old;
                _stockAnHourAgo[r] = sim.Stockpile[r];
            }
            _lastSnapshotTick = sim.CurrentTick;
        }

        foreach (var r in Enum.GetValues<Resource>())
        {
            int stock = sim.Stockpile[r];
            int target = Simulation.StockTargets[r];
            int trend = _perHour[r];
            string trendText = trend == 0 ? "" : trend > 0 ? $" +{trend}" : $" {trend}";
            _resourceLabels[r].Text = $"{r} {stock}{trendText}";
            var color = stock == 0 ? Palette.UiBad : stock < target / 2 ? Palette.UiWarn : Palette.UiText;
            _resourceLabels[r].AddThemeColorOverride("font_color", color);
            _resourceChips[r].TooltipText = $"{r}: {stock} in store, {trendText.Trim() switch { "" => "no change", var t => t }} in the last hour.\nSettlers aim to keep {target} and stop gathering at {target * 2}.";
        }
    }

    private void RefreshRoster(Settler? selected)
    {
        int alive = _sim.Settlers.Count(s => !s.IsDead);
        _rosterHeader.Text = $"Settlers  {alive}/{_sim.Settlers.Count}";
        if (!_rosterOpen) return;

        foreach (var row in _rows)
        {
            var s = row.Settler;
            row.Button.SetPressedNoSignal(s == selected);
            if (s.IsDead)
            {
                row.Activity.Text = "Deceased. Still technically a resident.";
                row.Button.Modulate = new Color(1, 1, 1, 0.5f);
                row.Hunger.Value = row.Rest.Value = row.Health.Value = 0;
                row.Mood.Text = "";
                continue;
            }
            row.Activity.Text = Describe(s.Activity);
            row.Hunger.Value = s.Hunger;
            row.Rest.Value = s.Rest;
            row.Health.Value = s.Health;
            row.Mood.Text = $"mood {s.Mood}";
            row.Mood.AddThemeColorOverride("font_color", s.Mood < 35 ? Palette.UiBad : s.Mood < 55 ? Palette.UiWarn : Palette.UiMuted);
            row.Name.AddThemeColorOverride("font_color", s.Hunger < 25 ? Palette.UiWarn : Palette.UiText);
        }
    }

    private void RefreshDetail(Settler? selected)
    {
        if (selected != _detailFor)
        {
            _detailFor = selected;
            _detailPanel.Visible = selected is not null;
            if (selected is not null) RebuildDetail(selected);
            Redock();
        }
        if (selected is null) return;

        _detailActivity.Text = selected.IsDead
            ? "Deceased."
            : $"{selected.Age} years old · {Describe(selected.Activity)}" + (selected.Carrying is { } c ? $" · carrying {selected.CarryAmount} {c}" : "");
        foreach (var (bar, value, get) in _detailBars)
        {
            double v = get(selected);
            bar.Value = v;
            value.Text = $"{v:0}";
        }
    }

    private void RefreshBuildBar(BuildingType? buildMode)
    {
        foreach (var (type, button) in _buildButtons)
        {
            bool affordable = _sim.Stockpile.Has(type.Cost);
            button.SetPressedNoSignal(type == buildMode);
            button.Disabled = !affordable && type != buildMode;
            button.TooltipText = affordable ? type.Description : $"{type.Description}\nNeeds {CostText(type)}.";
        }

        _buildHint.Text = buildMode is { } b
            ? $"Placing {b.Name}: left click to build, right click or Esc to stop"
            : "Build";
        _buildHint.AddThemeColorOverride("font_color", buildMode is null ? Palette.UiMuted : Palette.UiAccent);
    }

    private void RefreshLog()
    {
        if (_sim.Log.Entries.Count == _lastLogCount) return;
        _lastLogCount = _sim.Log.Entries.Count;

        var sb = new StringBuilder();
        foreach (var e in _sim.Log.Recent(40))
        {
            string color = e.Kind switch
            {
                LogKind.Good => Palette.UiGood.ToHtml(false),
                LogKind.Warning => Palette.UiWarn.ToHtml(false),
                LogKind.Bad => Palette.UiBad.ToHtml(false),
                _ => Palette.UiText.ToHtml(false),
            };
            long day = e.Tick / Simulation.TicksPerDay + 1;
            long hour = e.Tick % Simulation.TicksPerDay / Simulation.TicksPerHour;
            sb.Append($"[color=#{Palette.UiMuted.ToHtml(false)}]D{day} {hour:00}h[/color]  [color=#{color}]{e.Text}[/color]\n");
        }
        _log.Text = sb.ToString();
    }

    private static string Describe(SettlerActivity a) => a switch
    {
        SettlerActivity.Idle => "Loitering with intent",
        SettlerActivity.WalkingToDeposit => "Heading out to scavenge",
        SettlerActivity.Gathering => "Gathering",
        SettlerActivity.Hauling => "Hauling loot home",
        SettlerActivity.WalkingToEat => "Going to eat",
        SettlerActivity.Eating => "Eating, loudly",
        SettlerActivity.Sleeping => "Asleep",
        _ => a.ToString(),
    };
}
