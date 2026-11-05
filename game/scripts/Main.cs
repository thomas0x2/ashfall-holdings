using Ashfall.Sim;
using Godot;
using Resource = Ashfall.Sim.Resource;

namespace Ashfall.Game;

/// <summary>
/// Root of the game scene. Owns the simulation, advances it in real time (with pause and
/// three speeds), and routes input to building and selection.
/// </summary>
public partial class Main : Node2D
{
    [Export] public int Seed = 1337;
    [Export] public int MapSize = 128;

    /// <summary>Simulation ticks (in-game minutes) per real second at speeds 1, 2, 3.</summary>
    private static readonly int[] TicksPerSecond = { 0, 15, 60, 240 };
    private const int MaxTicksPerFrame = 600;

    private Simulation _sim = null!;
    private MapLayer _map = null!;
    private SettlerView _settlers = null!;
    private CameraRig _camera = null!;
    private Hud _hud = null!;
    private ShaderMaterial _paint = null!;

    private bool _paused;
    private int _speed = 1;
    private double _tickAccumulator;
    private double _hudTimer;
    private BuildingType? _buildMode;
    private Settler? _selected;

    private BuildingType? BuildMode => _buildMode;

    public override void _Ready()
    {
        _sim = new Simulation(Seed, MapSize);

        _map = new MapLayer { Name = "Map" };
        AddChild(_map);
        _map.Init(_sim);

        _settlers = new SettlerView { Name = "Settlers" };
        AddChild(_settlers);
        _settlers.Init(_sim);

        _camera = new CameraRig { Name = "Camera", Position = Iso.ToScreen(_sim.Camp) };
        AddChild(_camera);
        _camera.MakeCurrent();

        AddChild(CreatePaintLayer());

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);
        _hud.Init(_sim);
        _hud.BuildPicked += type => { _buildMode = type; if (type is not null) _selected = null; };
        _hud.SpeedPicked += speed => { if (speed == 0) _paused = true; else SetSpeed(speed); };
        _hud.SettlerPicked += Select;
    }

    /// <summary>A full-screen pass that turns the flat render into something that looks oil-painted.</summary>
    private CanvasLayer CreatePaintLayer()
    {
        var layer = new CanvasLayer { Name = "OilPaint", Layer = 10 };
        _paint = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/oil_paint.gdshader") };
        var rect = new ColorRect { Material = _paint, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        return layer;
    }

    public override void _Process(double delta)
    {
        if (!_paused)
        {
            _tickAccumulator += delta * TicksPerSecond[_speed];
            int ticks = 0;
            while (_tickAccumulator >= 1 && ticks < MaxTicksPerFrame)
            {
                _sim.Tick();
                _tickAccumulator -= 1;
                ticks++;
            }
            if (ticks == MaxTicksPerFrame) _tickAccumulator = 0;
        }

        _settlers.Hovered = Iso.ToGrid(GetGlobalMousePosition());
        _settlers.BuildPreview = BuildMode;
        _settlers.Selected = _selected;
        // Text layout is expensive; ten updates a second reads as live.
        _hudTimer -= delta;
        if (_hudTimer <= 0)
        {
            _hudTimer = 0.1;
            _hud.Refresh(_paused, _speed, BuildMode, _selected);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.Space: _paused = !_paused; break;
                case Key.Key1: SetSpeed(1); break;
                case Key.Key2: SetSpeed(2); break;
                case Key.Key3: SetSpeed(3); break;
                case Key.B: CycleBuildMode(); break;
                case Key.Tab: _hud.ToggleRoster(); break;
                case Key.Escape: _buildMode = null; _selected = null; break;
                case Key.P:
                    bool on = (bool)_paint.GetShaderParameter("enabled");
                    _paint.SetShaderParameter("enabled", !on);
                    break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            var tile = Iso.ToGrid(GetGlobalMousePosition());
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (BuildMode is { } type)
                {
                    var result = _sim.TryPlace(type, tile);
                    if (result != PlaceResult.Ok)
                        _sim.Log.Add(_sim.CurrentTick, LogKind.Warning, RejectLine(type, result));
                }
                else
                {
                    _selected = _settlers.SettlerAt(tile);
                }
                _hudTimer = 0;
                GetViewport().SetInputAsHandled();
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _buildMode = null;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void CycleBuildMode()
    {
        var all = BuildingCatalog.All;
        int i = _buildMode is null ? -1 : all.ToList().IndexOf(_buildMode);
        _buildMode = all[(i + 1) % all.Count];
        _selected = null;
    }

    /// <summary>Select a settler (or nothing) and bring them into view.</summary>
    private void Select(Settler? s)
    {
        _selected = s;
        _hudTimer = 0;
        if (s is not null)
        {
            _buildMode = null;
            _camera.Position = Iso.ToScreen(s.Position);
        }
    }

    private void SetSpeed(int speed)
    {
        _speed = speed;
        _paused = false;
    }

    private static string RejectLine(BuildingType type, PlaceResult result) => result switch
    {
        PlaceResult.CantAfford => $"Can't afford a {type.Name}. Wishing harder does not count as a resource.",
        PlaceResult.BadTerrain => $"A {type.Name} can't go on water or rock. Physics remains undefeated.",
        PlaceResult.Occupied => "Something is already there. Possibly someone.",
        PlaceResult.BlocksCamp => "That's the campfire. People are attached to it.",
        PlaceResult.OutOfBounds => "That's off the map. Nobody has been there and come back.",
        _ => "Can't build there.",
    };
}
