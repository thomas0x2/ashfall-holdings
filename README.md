# Ashfall Holdings

A darkly humorous post-apocalyptic settlement sim. Lead a handful of survivors into a ruined valley, build a settlement on an isometric grid, and keep people with very specific pasts alive while the wasteland tests them. Later: several settlements linked by caravan routes, and rival factions that raid.

Design doc: https://claude.ai/artifact/CYfqbn2iow3FYnxcC46TsA

## Layout

| Path | What it is |
| --- | --- |
| `sim/` | Engine-free C# simulation: map generation, pathfinding, settlers, needs, jobs, buildings, event log. No Godot references allowed. |
| `tests/Ashfall.Sim.Tests/` | xUnit tests that run the simulation headless. |
| `game/` | The Godot 4 project. Renders the simulation and handles input. |

Keeping the rules in `sim/` means the game can be tested and balanced by fast-forwarding thousands of in-game days without opening the editor.

## Running it

Requirements: [Godot 4.7 .NET edition](https://godotengine.org/download) and the .NET 8 SDK.

1. Open Godot, choose **Import**, and pick `game/project.godot`.
2. Press **Build** (hammer icon, top right), then **Run** (F5).

Tests, from the repo root:

```sh
dotnet test tests/Ashfall.Sim.Tests
```

## Controls

| Input | Action |
| --- | --- |
| Space | Pause / resume |
| 1, 2, 3 | Game speed |
| B | Enter build mode / cycle building type |
| Left click | Place building, or select a settler to read their bio |
| Right click, Esc | Cancel build mode |
| WASD, arrows, middle drag | Pan camera |
| Mouse wheel | Zoom |
| P | Toggle the oil-paint filter |

## Art direction

Everything should feel oil-painted. For now that comes from two things: hand-varied earthy colours with loose "brush dabs" on every tile (`game/scripts/MapView.cs`), and a full-screen Kuwahara filter with canvas weave and warm varnish (`game/shaders/oil_paint.gdshader`). Real painted art can replace the placeholder shapes later without touching the simulation.

## Roadmap

1. Grid and camera, terrain and deposits, building placement (this scaffold)
2. Settlers live: needs, jobs, pathfinding, gathering (this scaffold, basic version)
3. Personality: relationships, mood breakdowns
4. Event director and raids (real-time with pause combat)
5. Second settlement and caravan trade routes
6. AI factions with territory
