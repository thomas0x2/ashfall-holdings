namespace Ashfall.Sim;

public enum Resource
{
    Food,
    Water,
    Scrap,
    Wood,
    Medicine,
}

public enum Terrain
{
    /// <summary>Dry grass and weeds. Walkable.</summary>
    Scrub,
    /// <summary>Bare packed earth. Walkable.</summary>
    Dirt,
    /// <summary>Broken concrete and old foundations. Walkable, slower in later milestones.</summary>
    Rubble,
    /// <summary>Standing water. Not walkable.</summary>
    Water,
    /// <summary>Rock outcrops. Not walkable.</summary>
    Rock,
}

/// <summary>Resource counts held by a settlement.</summary>
public sealed class Stockpile
{
    private readonly Dictionary<Resource, int> _amounts = new();

    public Stockpile()
    {
        foreach (var r in Enum.GetValues<Resource>())
            _amounts[r] = 0;
    }

    public int this[Resource r] => _amounts[r];

    public void Add(Resource r, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _amounts[r] += amount;
    }

    public bool TryTake(Resource r, int amount)
    {
        if (_amounts[r] < amount) return false;
        _amounts[r] -= amount;
        return true;
    }

    public bool Has(IReadOnlyDictionary<Resource, int> cost) =>
        cost.All(kv => _amounts[kv.Key] >= kv.Value);

    public bool TryPay(IReadOnlyDictionary<Resource, int> cost)
    {
        if (!Has(cost)) return false;
        foreach (var (r, n) in cost) _amounts[r] -= n;
        return true;
    }

    public IReadOnlyDictionary<Resource, int> Snapshot() => new Dictionary<Resource, int>(_amounts);
}
