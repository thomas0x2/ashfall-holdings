namespace Ashfall.Sim;

/// <summary>
/// One settlement's simulation. Advance it with <see cref="Tick"/>; one tick is one in-game minute.
/// The renderer reads state from here and never mutates it except through public commands.
/// </summary>
public sealed class Simulation
{
    public const int TicksPerHour = 60;
    public const int TicksPerDay = 24 * TicksPerHour;

    // Tuning. Kept here so balancing passes touch one place.
    public const double HungerPerTick = 100.0 / (TicksPerDay * 1.2);
    public const double FatiguePerTick = 100.0 / (TicksPerHour * 20);
    public const double SleepRecoveryPerTick = 0.2;
    public const double StarvationDamagePerTick = 0.05;
    public const int TicksPerStep = 3;
    public const double WorkPerUnit = 15;
    public const int CarryCapacity = 5;
    public const int MealTicks = 20;
    public const int JobSearchRetryTicks = 30;
    public const int MaxPathNodes = 6000;

    /// <summary>Stock levels settlers try to keep. They stop gathering a resource at twice its target.</summary>
    public static IReadOnlyDictionary<Resource, int> StockTargets => Targets;

    private static readonly Dictionary<Resource, int> Targets = new()
    {
        [Resource.Food] = 30,
        [Resource.Water] = 30,
        [Resource.Wood] = 40,
        [Resource.Scrap] = 40,
        [Resource.Medicine] = 10,
    };

    public WorldMap Map { get; }
    public Stockpile Stockpile { get; } = new();
    public EventLog Log { get; } = new();
    public string SettlementName { get; }
    public GridPos Camp { get; }
    public long CurrentTick { get; private set; }

    private readonly List<Settler> _settlers = new();
    private readonly List<Building> _buildings = new();
    private readonly List<GridPos> _deposits;
    private readonly HashSet<GridPos> _reserved = new();
    // Deposits no path reaches. Nothing ever opens terrain up, so once unreachable, always unreachable.
    private readonly HashSet<GridPos> _unreachable = new();
    private int _nextBuildingId = 1;

    public IReadOnlyList<Settler> Settlers => _settlers;
    public IReadOnlyList<Building> Buildings => _buildings;

    public int Day => (int)(CurrentTick / TicksPerDay) + 1;
    public int Hour => (int)(CurrentTick % TicksPerDay / TicksPerHour);
    public int Minute => (int)(CurrentTick % TicksPerHour);
    public bool IsNight => Hour >= 22 || Hour < 6;

    /// <summary>Raised for each tile that changes in a way the renderer should redraw (building placed, deposit gone).</summary>
    public event Action<GridPos>? TileChanged;

    public Simulation(int seed, int mapSize = 128, int settlerCount = 5, string settlementName = "First Light")
    {
        Map = WorldMap.Generate(mapSize, seed);
        Camp = Map.Center;
        SettlementName = settlementName;
        CurrentTick = 6 * TicksPerHour; // Dawn of day 1.

        _deposits = Map.AllPositions().Where(p => Map[p].Deposit is not null).ToList();

        Stockpile.Add(Resource.Food, 12);
        Stockpile.Add(Resource.Water, 12);
        Stockpile.Add(Resource.Wood, 10);
        Stockpile.Add(Resource.Scrap, 6);

        var factory = new SettlerFactory(seed);
        var spots = Camp.Neighbors4().Prepend(Camp).Concat(new[] { Camp + new GridPos(1, 1), Camp + new GridPos(-1, -1), Camp + new GridPos(1, -1) }).ToList();
        for (int i = 0; i < settlerCount; i++)
        {
            var s = factory.Create(spots[i % spots.Count]);
            _settlers.Add(s);
        }

        Log.Add(CurrentTick, LogKind.Info,
            $"{settlerCount} survivors arrive at what will generously be called {SettlementName}.");
        var loudest = _settlers.OrderBy(s => s.Mood).First();
        Log.Add(CurrentTick, LogKind.Info, $"{loudest.ShortName} has already started complaining.");
    }

    // ---------------------------------------------------------------- commands

    public PlaceResult CanPlace(BuildingType type, GridPos origin)
    {
        var b = new Building { Type = type, Origin = origin };
        foreach (var p in b.Footprint())
        {
            if (!Map.InBounds(p)) return PlaceResult.OutOfBounds;
            ref var t = ref Map[p];
            if (t.Terrain is Terrain.Water or Terrain.Rock) return PlaceResult.BadTerrain;
            if (t.BuildingId >= 0 || t.Deposit is not null) return PlaceResult.Occupied;
            if (p == Camp) return PlaceResult.BlocksCamp;
            if (type.BlocksMovement && _settlers.Any(s => !s.IsDead && s.Position == p)) return PlaceResult.Occupied;
        }
        return Stockpile.Has(type.Cost) ? PlaceResult.Ok : PlaceResult.CantAfford;
    }

    public PlaceResult TryPlace(BuildingType type, GridPos origin)
    {
        var result = CanPlace(type, origin);
        if (result != PlaceResult.Ok) return result;

        Stockpile.TryPay(type.Cost);
        var building = new Building { Id = _nextBuildingId++, Type = type, Origin = origin };
        _buildings.Add(building);
        foreach (var p in building.Footprint())
        {
            ref var t = ref Map[p];
            t.BuildingId = building.Id;
            t.Blocked = type.BlocksMovement;
            TileChanged?.Invoke(p);
        }

        Log.Add(CurrentTick, LogKind.Good, $"Built a {type.Name}. {type.Description}");
        return PlaceResult.Ok;
    }

    // ---------------------------------------------------------------- simulation

    public void Tick()
    {
        CurrentTick++;
        if (CurrentTick % TicksPerDay == 6 * TicksPerHour)
            AnnounceDay();

        foreach (var b in _buildings)
            TickBuilding(b);

        foreach (var s in _settlers)
            if (!s.IsDead)
                TickSettler(s);
    }

    public void Advance(int ticks)
    {
        for (int i = 0; i < ticks; i++) Tick();
    }

    private void AnnounceDay()
    {
        int alive = _settlers.Count(s => !s.IsDead);
        string flavor = alive == _settlers.Count
            ? "Everyone is still alive, which counts as a good day."
            : $"{alive} still standing. The others are now part of the scenery.";
        Log.Add(CurrentTick, LogKind.Info, $"Day {Day} in {SettlementName}. {flavor}");
    }

    private void TickBuilding(Building b)
    {
        if (b.Type.Produces is not { } r) return;
        if (++b.ProduceTimer < b.Type.ProduceEveryTicks) return;
        b.ProduceTimer = 0;
        Stockpile.Add(r, b.Type.ProduceAmount);
    }

    private void TickSettler(Settler s)
    {
        // Needs.
        s.Hunger = Math.Max(0, s.Hunger - HungerPerTick * s.TraitProduct(t => t.HungerRate));
        if (s.Activity != SettlerActivity.Sleeping)
            s.Rest = Math.Max(0, s.Rest - FatiguePerTick * s.TraitProduct(t => t.FatigueRate));

        if (s.Hunger <= 0)
        {
            s.Health -= StarvationDamagePerTick;
            if (s.IsDead)
            {
                Release(s);
                Log.Add(CurrentTick, LogKind.Bad,
                    $"{s.DisplayName} has starved. Their last words were a strongly worded complaint about the menu.");
                return;
            }
        }

        switch (s.Activity)
        {
            case SettlerActivity.Idle:
                Decide(s);
                break;
            case SettlerActivity.WalkingToDeposit:
                if (Step(s, out bool arrived) && arrived) BeginGather(s);
                break;
            case SettlerActivity.Gathering:
                Gather(s);
                break;
            case SettlerActivity.Hauling:
                if (Step(s, out bool atCamp) && atCamp) Unload(s);
                break;
            case SettlerActivity.WalkingToEat:
                if (Step(s, out bool atFood) && atFood) { s.Activity = SettlerActivity.Eating; s.WorkProgress = 0; }
                break;
            case SettlerActivity.Eating:
                if (++s.WorkProgress >= MealTicks) Eat(s);
                break;
            case SettlerActivity.Sleeping:
                Sleep(s);
                break;
        }
    }

    private void Decide(Settler s)
    {
        if (s.Hunger < 30 && Stockpile[Resource.Food] > 0)
        {
            if (TryWalkTo(s, Camp)) s.Activity = SettlerActivity.WalkingToEat;
            return;
        }

        if (s.Rest < 20 || (IsNight && s.Rest < 70))
        {
            s.Activity = SettlerActivity.Sleeping;
            return;
        }

        if (s.JobSearchCooldown > 0) s.JobSearchCooldown--;
        else if (TryStartGatherJob(s)) return;
        else s.JobSearchCooldown = JobSearchRetryTicks;

        // Nothing to do: wander one step so the colony doesn't look frozen.
        if (CurrentTick % 30 == s.Id % 30)
        {
            var options = s.Position.Neighbors4()
                .Where(p => Map.IsWalkable(p) && p.ManhattanTo(Camp) <= 4)
                .OrderBy(p => ValueNoise.Hash(p.X, p.Y, (int)CurrentTick))
                .ToList();
            if (options.Count > 0) s.Position = options[0];
        }
    }

    private bool TryStartGatherJob(Settler s)
    {
        // Gather whatever is furthest below target, leaning toward what this settler is good at.
        var order = Enum.GetValues<Resource>()
            .OrderBy(r => Stockpile[r] / (double)Targets[r] - s.GatherSpeed(r) * 0.15)
            .ToList();

        foreach (var r in order)
        {
            if (Stockpile[r] >= Targets[r] * 2) continue;

            var candidates = _deposits
                .Where(p => Map[p].Deposit == r && Map[p].DepositAmount > 0 && !_reserved.Contains(p) && !_unreachable.Contains(p))
                .OrderBy(p => p.ManhattanTo(s.Position))
                .Take(4);

            foreach (var target in candidates)
            {
                if (!TryWalkTo(s, target))
                {
                    _unreachable.Add(target);
                    continue;
                }
                _reserved.Add(target);
                s.TargetDeposit = target;
                s.Activity = SettlerActivity.WalkingToDeposit;
                return true;
            }
        }
        return false;
    }

    private bool TryWalkTo(Settler s, GridPos goal)
    {
        var path = Pathfinder.FindPath(Map, s.Position, goal, MaxPathNodes);
        if (path is null) return false;
        s.Path.Clear();
        foreach (var p in path) s.Path.Enqueue(p);
        s.MoveCooldown = 0;
        return true;
    }

    /// <summary>Move along the current path. Returns false if the path broke and the settler went idle.</summary>
    private bool Step(Settler s, out bool arrived)
    {
        arrived = false;
        if (s.Path.Count == 0) { arrived = true; return true; }
        if (++s.MoveCooldown < TicksPerStep) return true;
        s.MoveCooldown = 0;

        var next = s.Path.Peek();
        if (!Map.IsWalkable(next))
        {
            // Something was built in the way. Re-plan to the same goal.
            var goal = s.Path.Last();
            if (!TryWalkTo(s, goal)) { Release(s); s.Activity = SettlerActivity.Idle; return false; }
            return true;
        }

        s.Position = s.Path.Dequeue();
        arrived = s.Path.Count == 0;
        return true;
    }

    private void BeginGather(Settler s)
    {
        s.Activity = SettlerActivity.Gathering;
        s.WorkProgress = 0;
        s.CarryAmount = 0;
        s.Carrying = s.TargetDeposit is { } p ? Map[p].Deposit : null;
    }

    private void Gather(Settler s)
    {
        if (s.TargetDeposit is not { } p || s.Carrying is not { } r)
        {
            Release(s);
            s.Activity = SettlerActivity.Idle;
            return;
        }

        ref var tile = ref Map[p];
        s.WorkProgress += s.GatherSpeed(r);
        while (s.WorkProgress >= WorkPerUnit && tile.DepositAmount > 0 && s.CarryAmount < CarryCapacity)
        {
            s.WorkProgress -= WorkPerUnit;
            tile.DepositAmount--;
            s.CarryAmount++;
        }

        bool exhausted = tile.DepositAmount <= 0;
        if (exhausted)
        {
            tile.Deposit = null;
            _deposits.Remove(p);
            Log.Add(CurrentTick, LogKind.Warning, DepletedLine(s, r));
            TileChanged?.Invoke(p);
        }

        if (s.CarryAmount >= CarryCapacity || exhausted)
        {
            _reserved.Remove(p);
            s.TargetDeposit = null;
            if (TryWalkTo(s, Camp)) s.Activity = SettlerActivity.Hauling;
            else { s.Carrying = null; s.CarryAmount = 0; s.Activity = SettlerActivity.Idle; }
        }
    }

    private static string DepletedLine(Settler s, Resource r) => r switch
    {
        Resource.Scrap => $"{s.ShortName} has picked a scrap pile clean. Somewhere, a toaster rests in peace.",
        Resource.Wood => $"{s.ShortName} felled the last dead tree in that patch. It did not put up a fight.",
        Resource.Food => $"{s.ShortName} harvested the last of the wild tubers. They tasted like regret and dirt.",
        Resource.Water => $"{s.ShortName} drained a murky spring. Nobody mention what was at the bottom.",
        Resource.Medicine => $"{s.ShortName} emptied an old pharmacy shelf. Most of it was expired. Most.",
        _ => $"{s.ShortName} exhausted a deposit.",
    };

    private void Unload(Settler s)
    {
        if (s.Carrying is { } r && s.CarryAmount > 0)
            Stockpile.Add(r, s.CarryAmount);
        s.Carrying = null;
        s.CarryAmount = 0;
        s.Activity = SettlerActivity.Idle;
    }

    private void Eat(Settler s)
    {
        s.WorkProgress = 0;
        s.Activity = SettlerActivity.Idle;
        if (!Stockpile.TryTake(Resource.Food, 1))
        {
            Log.Add(CurrentTick, LogKind.Warning, $"{s.ShortName} found the food store empty and stared at it for a while.");
            return;
        }

        if (Stockpile.TryTake(Resource.Water, 1))
            s.Hunger = Math.Min(100, s.Hunger + 50);
        else
        {
            s.Hunger = Math.Min(100, s.Hunger + 25);
            Log.Add(CurrentTick, LogKind.Warning, $"{s.ShortName} ate a dry meal with no water. Their review: one star.");
        }
    }

    private void Sleep(Settler s)
    {
        double rate = SleepRecoveryPerTick * (_buildings.Any(b => b.Type == BuildingCatalog.Shelter) ? 1.5 : 1.0);
        s.Rest = Math.Min(100, s.Rest + rate);
        bool rested = s.Rest >= 99.5 || (!IsNight && s.Rest >= 85);
        if (rested || s.Hunger < 15) s.Activity = SettlerActivity.Idle;
    }

    private void Release(Settler s)
    {
        if (s.TargetDeposit is { } p) _reserved.Remove(p);
        s.TargetDeposit = null;
        s.Path.Clear();
    }
}
