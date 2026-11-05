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

**"Main.tscn can't be opened" / missing dependency `Main.cs`:** you're almost certainly on the standard Godot download, which can't read C# scripts. Download the **.NET** build of the same version (it's a separate download on the Godot site, labelled ".NET"), then import the project again. The standard build also warns that the project uses the unsupported "C#" feature.

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
| Tab | Show or hide the settler roster |

## Interface

- **Top bar:** settlement, day and time, every resource with its change over the last in-game hour (hover for targets), speed buttons, and a `?` with all controls.
- **Settler roster** (left): each settler's activity, hunger, rest, health, and mood. Click a row to select them and jump the camera there.
- **Settler card** (right): backstory, traits, skills, and live needs for the selected settler.
- **Build bar** (bottom): every building with its cost. Unaffordable ones are greyed out; hover for details.
- **Chronicle** (bottom left): the event log.

## Art direction

Everything should feel oil-painted. For now that comes from two things: hand-varied earthy colours with loose "brush dabs" on every tile (`game/scripts/MapView.cs`), and a full-screen Kuwahara filter with canvas weave and warm varnish (`game/shaders/oil_paint.gdshader`). Real painted art can replace the placeholder shapes later without touching the simulation.

## Roadmap

1. Grid and camera, terrain and deposits, building placement (this scaffold)
2. Settlers live: needs, jobs, pathfinding, gathering (this scaffold, basic version)
3. Personality: relationships, mood breakdowns
4. Event director and raids (real-time with pause combat)
5. Second settlement and caravan trade routes
6. AI factions with territory
