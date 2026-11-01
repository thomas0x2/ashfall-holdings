# Ashfall Holdings: notes for Claude

- Original game. Use genre conventions only; never copy names, lore, assets, or specific mechanics from existing games.
- Tone: darkly humorous. Log lines, trait descriptions, and backstories should be dry and funny, never cruel to real groups.
- Art: should feel oil-painted (earthy palette, brushwork, canvas, varnish).
- Combat: real-time with pause.
- All game rules go in `sim/` (no Godot references). `game/` only renders and handles input.
- Before pushing: `dotnet test tests/Ashfall.Sim.Tests` and `dotnet build game/Ashfall.csproj`.
