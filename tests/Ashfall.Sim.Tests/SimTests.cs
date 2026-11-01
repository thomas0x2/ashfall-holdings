using Ashfall.Sim;

namespace Ashfall.Sim.Tests;

public class MapTests
{
    [Fact]
    public void SameSeedGivesSameMap()
    {
        var a = WorldMap.Generate(64, 42);
        var b = WorldMap.Generate(64, 42);
        foreach (var p in a.AllPositions())
        {
            Assert.Equal(a[p].Terrain, b[p].Terrain);
            Assert.Equal(a[p].Deposit, b[p].Deposit);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1234)]
    public void StartAreaIsWalkableAndHasBasicResourcesNearby(int seed)
    {
        var map = WorldMap.Generate(128, seed);
        foreach (var p in map.AllPositions().Where(p => p.ManhattanTo(map.Center) <= 6))
            Assert.True(map.IsWalkable(p), $"tile {p} near camp should be walkable");

        foreach (var r in new[] { Resource.Food, Resource.Water, Resource.Scrap, Resource.Wood })
            Assert.Contains(map.AllPositions(), p => p.ManhattanTo(map.Center) <= 14 && map[p].Deposit == r);
    }
}

public class PathfinderTests
{
    [Fact]
    public void FindsShortestPathOnOpenGround()
    {
        var map = new WorldMap(10, 10);
        var path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(3, 4));
        Assert.NotNull(path);
        Assert.Equal(7, path!.Count);
        Assert.Equal(new GridPos(3, 4), path[^1]);
    }

    [Fact]
    public void RoutesAroundWallsAndFailsWhenSealed()
    {
        var map = new WorldMap(5, 5);
        for (int y = 0; y < 4; y++) map[new GridPos(2, y)].Terrain = Terrain.Rock;

        var around = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(4, 0));
        Assert.NotNull(around);
        Assert.Contains(new GridPos(2, 4), around!);

        map[new GridPos(2, 4)].Terrain = Terrain.Water;
        Assert.Null(Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(4, 0)));
    }
}

public class SettlerTests
{
    [Fact]
    public void SettlersAreDeterministicAndDistinct()
    {
        var a = new SettlerFactory(99);
        var b = new SettlerFactory(99);
        var listA = Enumerable.Range(0, 8).Select(_ => a.Create(default)).ToList();
        var listB = Enumerable.Range(0, 8).Select(_ => b.Create(default)).ToList();

        Assert.Equal(listA.Select(s => s.DisplayName), listB.Select(s => s.DisplayName));
        Assert.Equal(8, listA.Select(s => $"{s.FirstName} {s.LastName}").Distinct().Count());
    }

    [Fact]
    public void EverySettlerHasABackstoryAndCompatibleTraits()
    {
        var f = new SettlerFactory(5);
        for (int i = 0; i < 50; i++)
        {
            var s = f.Create(default);
            Assert.False(string.IsNullOrWhiteSpace(s.Childhood));
            Assert.False(string.IsNullOrWhiteSpace(s.Adulthood));
            Assert.InRange(s.Traits.Count, 2, 3);
            Assert.True(s.Traits.Count(t => t.Specialty is not null) <= 1);
            var tags = s.Traits.Where(t => t.ExcludesTag is not null).Select(t => t.ExcludesTag).ToList();
            Assert.Equal(tags.Count, tags.Distinct().Count());
        }
    }
}

public class SimulationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2024)]
    public void ColonySurvivesThreeDaysAndGathers(int seed)
    {
        var sim = new Simulation(seed);
        int startScrap = sim.Stockpile[Resource.Scrap];
        int startWood = sim.Stockpile[Resource.Wood];

        sim.Advance(Simulation.TicksPerDay * 3);

        Assert.All(sim.Settlers, s => Assert.False(s.IsDead, $"{s.DisplayName} died"));
        Assert.True(sim.Stockpile[Resource.Scrap] + sim.Stockpile[Resource.Wood] > startScrap + startWood,
            "settlers should have gathered building materials");
        Assert.Contains(sim.Log.Entries, e => e.Text.StartsWith("Day 3"));
    }

    [Fact]
    public void SettlersSleepAtNight()
    {
        var sim = new Simulation(3);
        // Advance to 01:00 on day 2.
        sim.Advance((int)(Simulation.TicksPerDay + Simulation.TicksPerHour - sim.CurrentTick));
        Assert.Contains(sim.Settlers, s => s.Activity == SettlerActivity.Sleeping);
    }

    [Fact]
    public void PlacingABuildingCostsResourcesAndBlocksTiles()
    {
        var sim = new Simulation(11);
        sim.Stockpile.Add(Resource.Wood, 20);
        sim.Stockpile.Add(Resource.Scrap, 20);
        var origin = FindFreeSpot(sim, BuildingCatalog.Shelter);
        int wood = sim.Stockpile[Resource.Wood];

        Assert.Equal(PlaceResult.Ok, sim.TryPlace(BuildingCatalog.Shelter, origin));
        Assert.Equal(wood - 10, sim.Stockpile[Resource.Wood]);
        Assert.False(sim.Map.IsWalkable(origin));
        Assert.Equal(PlaceResult.Occupied, sim.TryPlace(BuildingCatalog.Shelter, origin));
    }

    [Fact]
    public void CannotBuildOnCampOrWithoutResources()
    {
        var sim = new Simulation(11);
        Assert.Equal(PlaceResult.BlocksCamp, sim.CanPlace(BuildingCatalog.GardenPlot, sim.Camp));

        while (sim.Stockpile.TryTake(Resource.Scrap, 1)) { }
        var origin = FindFreeSpot(sim, BuildingCatalog.RainCollector);
        Assert.Equal(PlaceResult.CantAfford, sim.TryPlace(BuildingCatalog.RainCollector, origin));
    }

    [Fact]
    public void ProductionBuildingsProduce()
    {
        var sim = new Simulation(8);
        sim.Stockpile.Add(Resource.Wood, 10);
        var origin = FindFreeSpot(sim, BuildingCatalog.GardenPlot);
        Assert.Equal(PlaceResult.Ok, sim.TryPlace(BuildingCatalog.GardenPlot, origin));

        var garden = sim.Buildings.Single();
        int before = sim.Stockpile[Resource.Food];
        // Settlers also eat, so just check the building's own output arrives.
        sim.Advance(BuildingCatalog.GardenPlot.ProduceEveryTicks);
        Assert.Equal(0, garden.ProduceTimer);
        Assert.True(sim.Stockpile[Resource.Food] >= before - sim.Settlers.Count + 1);
    }

    private static GridPos FindFreeSpot(Simulation sim, BuildingType type)
    {
        for (int r = 3; r < 20; r++)
            foreach (var p in sim.Map.AllPositions().Where(p => p.ManhattanTo(sim.Camp) == r))
            {
                var res = sim.CanPlace(type, p);
                if (res is PlaceResult.Ok or PlaceResult.CantAfford)
                {
                    // CantAfford means the spot itself is fine.
                    var probe = new Building { Type = type, Origin = p };
                    if (probe.Footprint().All(f => sim.Map.InBounds(f) && sim.Map[f].BuildingId < 0 && sim.Map[f].Deposit is null && sim.Map.IsWalkable(f) && sim.Settlers.All(s => s.Position != f)))
                        return p;
                }
            }
        throw new InvalidOperationException("no free spot");
    }
}
