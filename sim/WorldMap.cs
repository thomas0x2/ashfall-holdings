namespace Ashfall.Sim;

public struct Tile
{
    public Terrain Terrain;
    /// <summary>The resource that can be gathered here, if any.</summary>
    public Resource? Deposit;
    /// <summary>Units left in the deposit. Zero means exhausted.</summary>
    public int DepositAmount;
    /// <summary>Id of the building on this tile, or -1.</summary>
    public int BuildingId;
    /// <summary>True while a building here blocks movement.</summary>
    public bool Blocked;
}

/// <summary>The local settlement map: a square grid of tiles.</summary>
public sealed class WorldMap
{
    public int Width { get; }
    public int Height { get; }
    public GridPos Center => new(Width / 2, Height / 2);

    private readonly Tile[] _tiles;

    public WorldMap(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width * height];
        for (int i = 0; i < _tiles.Length; i++) _tiles[i].BuildingId = -1;
    }

    public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public ref Tile this[GridPos p] => ref _tiles[p.Y * Width + p.X];

    public bool IsWalkable(GridPos p)
    {
        if (!InBounds(p)) return false;
        ref var t = ref this[p];
        return t.Terrain is not (Terrain.Water or Terrain.Rock) && !t.Blocked;
    }

    public IEnumerable<GridPos> AllPositions()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return new GridPos(x, y);
    }

    /// <summary>
    /// Generate terrain and resource deposits. The area around the centre is kept clear
    /// so the starting settlers always have room to build.
    /// </summary>
    public static WorldMap Generate(int size, int seed)
    {
        var map = new WorldMap(size, size);
        var height = new ValueNoise(seed);
        var ruin = new ValueNoise(seed ^ 0x5A5A5A);
        var center = map.Center;
        const int clearRadius = 6;

        foreach (var p in map.AllPositions())
        {
            double h = height.Fractal(p.X, p.Y, 4, 0.045);
            double r = ruin.Fractal(p.X, p.Y, 3, 0.08);
            ref var t = ref map[p];

            t.Terrain = h switch
            {
                < 0.30 => Terrain.Water,
                < 0.36 => Terrain.Dirt,
                > 0.74 => Terrain.Rock,
                _ => r > 0.62 ? Terrain.Rubble : (r < 0.40 ? Terrain.Dirt : Terrain.Scrub),
            };

            if (p.ManhattanTo(center) <= clearRadius && t.Terrain is Terrain.Water or Terrain.Rock)
                t.Terrain = Terrain.Dirt;
        }

        // Scatter deposits. Each resource prefers a terrain, which is what makes sites differ.
        foreach (var p in map.AllPositions())
        {
            ref var t = ref map[p];
            if (p.ManhattanTo(center) <= 2) continue;
            double roll = ValueNoise.Hash(p.X, p.Y, seed + 77);
            double near = ValueNoise.Hash(p.X, p.Y, seed + 91);

            Resource? deposit = t.Terrain switch
            {
                Terrain.Rubble when roll < 0.10 => Resource.Scrap,
                Terrain.Rubble when roll < 0.115 => Resource.Medicine,
                Terrain.Scrub when roll < 0.07 => Resource.Wood,
                Terrain.Scrub when roll < 0.10 => Resource.Food,
                Terrain.Dirt when roll < 0.03 && map.TouchesWater(p) => Resource.Water,
                Terrain.Dirt when roll < 0.015 => Resource.Water,
                _ => null,
            };

            if (deposit is { } d)
            {
                t.Deposit = d;
                t.DepositAmount = d switch
                {
                    Resource.Medicine => 10 + (int)(near * 10),
                    Resource.Water => 60 + (int)(near * 60),
                    _ => 25 + (int)(near * 35),
                };
            }
        }

        map.GuaranteeStarterDeposits(seed);
        return map;
    }

    private bool TouchesWater(GridPos p) =>
        p.Neighbors4().Any(n => InBounds(n) && this[n].Terrain == Terrain.Water);

    /// <summary>Make sure every basic resource exists within walking distance of the start.</summary>
    private void GuaranteeStarterDeposits(int seed)
    {
        var basics = new[] { Resource.Food, Resource.Water, Resource.Scrap, Resource.Wood };
        int i = 0;
        foreach (var r in basics)
        {
            bool present = AllPositions().Any(p => p.ManhattanTo(Center) <= 14 && this[p].Deposit == r);
            if (present) { i++; continue; }

            // Place one at a fixed ring offset around the centre.
            var offset = (i % 4) switch
            {
                0 => new GridPos(5, 2),
                1 => new GridPos(-4, 5),
                2 => new GridPos(-5, -3),
                _ => new GridPos(3, -5),
            };
            var p2 = Center + offset;
            ref var t = ref this[p2];
            t.Terrain = r == Resource.Scrap ? Terrain.Rubble : (r == Resource.Water ? Terrain.Dirt : Terrain.Scrub);
            t.Deposit = r;
            t.DepositAmount = 40 + (int)(ValueNoise.Hash(p2.X, p2.Y, seed) * 20);
            i++;
        }
    }
}
