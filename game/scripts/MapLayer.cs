using Ashfall.Sim;
using Godot;

namespace Ashfall.Game;

/// <summary>
/// Creates one <see cref="MapView"/> per 16x16 block of tiles. Each block is its own canvas item,
/// so Godot skips the ones off screen and a change only re-records the block it touched.
/// </summary>
public partial class MapLayer : Node2D
{
    public const int ChunkSize = 16;

    public void Init(Simulation sim)
    {
        int chunksX = (sim.Map.Width + ChunkSize - 1) / ChunkSize;
        int chunksY = (sim.Map.Height + ChunkSize - 1) / ChunkSize;
        var chunks = new MapView[chunksX, chunksY];

        // Back to front, so raised things in a nearer block overlap the block behind it.
        for (int sum = 0; sum <= chunksX + chunksY - 2; sum++)
            for (int cx = Math.Max(0, sum - chunksY + 1); cx <= Math.Min(sum, chunksX - 1); cx++)
            {
                var chunk = new MapView { Name = $"Chunk{cx}_{sum - cx}" };
                AddChild(chunk);
                chunk.Init(sim, cx, sum - cx);
                chunks[cx, sum - cx] = chunk;
            }

        sim.TileChanged += p => chunks[p.X / ChunkSize, p.Y / ChunkSize].QueueRedraw();
    }
}
