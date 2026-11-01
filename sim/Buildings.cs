namespace Ashfall.Sim;

public sealed record BuildingType(
    string Id,
    string Name,
    string Description,
    int Width,
    int Height,
    IReadOnlyDictionary<Resource, int> Cost,
    bool BlocksMovement,
    Resource? Produces = null,
    int ProduceAmount = 0,
    int ProduceEveryTicks = 0);

public sealed class Building
{
    public int Id { get; init; }
    public BuildingType Type { get; init; } = null!;
    public GridPos Origin { get; init; }
    internal int ProduceTimer { get; set; }

    public IEnumerable<GridPos> Footprint()
    {
        for (int dy = 0; dy < Type.Height; dy++)
            for (int dx = 0; dx < Type.Width; dx++)
                yield return new GridPos(Origin.X + dx, Origin.Y + dy);
    }
}

public static class BuildingCatalog
{
    public static readonly BuildingType Shelter = new(
        "shelter", "Shelter",
        "Four walls and most of a roof. Settlers sleep better when one exists.",
        2, 2, new Dictionary<Resource, int> { [Resource.Wood] = 10, [Resource.Scrap] = 4 },
        BlocksMovement: true);

    public static readonly BuildingType RainCollector = new(
        "rain_collector", "Rain Collector",
        "A tarp, a barrel, and hope. Produces water slowly.",
        1, 1, new Dictionary<Resource, int> { [Resource.Scrap] = 6 },
        BlocksMovement: true, Produces: Resource.Water, ProduceAmount: 1, ProduceEveryTicks: 120);

    public static readonly BuildingType GardenPlot = new(
        "garden_plot", "Garden Plot",
        "Grows something edible. Usually.",
        2, 2, new Dictionary<Resource, int> { [Resource.Wood] = 6 },
        BlocksMovement: false, Produces: Resource.Food, ProduceAmount: 1, ProduceEveryTicks: 150);

    public static readonly BuildingType StorageCrate = new(
        "storage_crate", "Storage Crate",
        "Holds things. Mostly holds the idea of being organised.",
        1, 1, new Dictionary<Resource, int> { [Resource.Wood] = 4 },
        BlocksMovement: true);

    public static readonly BuildingType Barricade = new(
        "barricade", "Barricade",
        "Car doors welded together. Raiders hate this one trick.",
        1, 1, new Dictionary<Resource, int> { [Resource.Scrap] = 2 },
        BlocksMovement: true);

    public static readonly IReadOnlyList<BuildingType> All = new[]
    {
        Shelter, RainCollector, GardenPlot, StorageCrate, Barricade,
    };
}

public enum PlaceResult
{
    Ok,
    OutOfBounds,
    BadTerrain,
    Occupied,
    BlocksCamp,
    CantAfford,
}
