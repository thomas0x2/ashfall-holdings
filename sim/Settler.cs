namespace Ashfall.Sim;

public enum Skill
{
    Scavenging,
    Farming,
    Construction,
    Medicine,
    Shooting,
    Social,
}

/// <summary>A personality trait. Multipliers default to 1 (no effect).</summary>
public sealed record Trait(
    string Name,
    string Description,
    double WorkSpeed = 1.0,
    double HungerRate = 1.0,
    double FatigueRate = 1.0,
    int MoodOffset = 0,
    Resource? Specialty = null,
    double SpecialtyBonus = 1.0)
{
    /// <summary>Traits that can't appear on the same settler.</summary>
    public string? ExcludesTag { get; init; }
}

public enum SettlerActivity
{
    Idle,
    WalkingToDeposit,
    Gathering,
    Hauling,
    WalkingToEat,
    Eating,
    Sleeping,
}

public sealed class Settler
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string? Nickname { get; init; }
    public int Age { get; init; }
    public string Childhood { get; init; } = "";
    public string Adulthood { get; init; } = "";
    public List<Trait> Traits { get; init; } = new();
    public Dictionary<Skill, int> Skills { get; init; } = new();

    public GridPos Position { get; set; }

    /// <summary>100 = full, 0 = starving.</summary>
    public double Hunger { get; set; } = 80;
    /// <summary>100 = well rested, 0 = collapsing.</summary>
    public double Rest { get; set; } = 90;
    public double Health { get; set; } = 100;
    public bool IsDead => Health <= 0;

    public SettlerActivity Activity { get; internal set; } = SettlerActivity.Idle;
    internal Queue<GridPos> Path { get; } = new();
    internal GridPos? TargetDeposit { get; set; }
    public Resource? Carrying { get; internal set; }
    public int CarryAmount { get; internal set; }
    internal double WorkProgress { get; set; }
    internal int MoveCooldown { get; set; }

    public string DisplayName => Nickname is null ? $"{FirstName} {LastName}" : $"{FirstName} \"{Nickname}\" {LastName}";
    public string ShortName => Nickname ?? FirstName;

    public double TraitProduct(Func<Trait, double> pick) => Traits.Aggregate(1.0, (acc, t) => acc * pick(t));

    /// <summary>0 to 100. Driven by needs and traits; will feed breakdowns in milestone 3.</summary>
    public int Mood
    {
        get
        {
            double needs = (Hunger + Rest) / 2.0;
            int traitMood = Traits.Sum(t => t.MoodOffset);
            return (int)Math.Clamp(needs * 0.8 + 10 + traitMood, 0, 100);
        }
    }

    public static Skill SkillFor(Resource r) => r switch
    {
        Resource.Scrap => Skill.Scavenging,
        Resource.Food => Skill.Farming,
        Resource.Water => Skill.Farming,
        Resource.Wood => Skill.Construction,
        Resource.Medicine => Skill.Medicine,
        _ => Skill.Scavenging,
    };

    /// <summary>Work per tick when gathering <paramref name="r"/>. 1.0 is an average settler.</summary>
    public double GatherSpeed(Resource r)
    {
        int skill = Skills.GetValueOrDefault(SkillFor(r));
        double speed = 0.6 + skill * 0.06;
        speed *= TraitProduct(t => t.WorkSpeed);
        speed *= TraitProduct(t => t.Specialty == r ? t.SpecialtyBonus : 1.0);
        if (Rest < 20) speed *= 0.6;
        return speed;
    }
}
