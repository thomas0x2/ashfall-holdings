namespace Ashfall.Sim;

/// <summary>Generates settlers with names, backstories, traits, and skills from a seeded RNG.</summary>
public sealed class SettlerFactory
{
    private readonly Random _rng;
    private int _nextId = 1;
    private readonly HashSet<string> _usedNames = new();

    public SettlerFactory(int seed) => _rng = new Random(seed);

    public static readonly IReadOnlyList<Trait> AllTraits = new List<Trait>
    {
        new("Iron Stomach", "Once ate a raincoat on a dare. Would do it again.", HungerRate: 0.8),
        new("Glutton", "Treats every meal like it's the last one, which, to be fair, it might be.", HungerRate: 1.35, MoodOffset: 3),
        new("Workaholic", "Hasn't taken a day off since the world ended. Or before.", WorkSpeed: 1.25, FatigueRate: 1.2) { ExcludesTag = "work" },
        new("Lazy", "Believes the apocalypse is a good excuse. It is not.", WorkSpeed: 0.8, FatigueRate: 0.85) { ExcludesTag = "work" },
        new("Night Owl", "Sleeps less, complains more.", FatigueRate: 0.8, MoodOffset: -3),
        new("Optimist", "Thinks things are looking up. Things are mostly ash.", MoodOffset: 10) { ExcludesTag = "outlook" },
        new("Doomsayer", "Predicted the end of the world. Will not stop bringing it up.", MoodOffset: -10) { ExcludesTag = "outlook" },
        new("Magpie", "Can smell copper wire through two feet of rubble.", Specialty: Resource.Scrap, SpecialtyBonus: 1.5),
        new("Green Thumb", "Coaxes vegetables out of soil that should legally be classified as gravel.", Specialty: Resource.Food, SpecialtyBonus: 1.5),
        new("Dowser", "Finds water with a bent coat hanger. Nobody knows how. Nobody asks.", Specialty: Resource.Water, SpecialtyBonus: 1.5),
        new("Former Lumberjack", "Says 'timber' every single time.", Specialty: Resource.Wood, SpecialtyBonus: 1.5),
        new("Hypochondriac", "Has diagnosed themselves with everything. Was right twice.", Specialty: Resource.Medicine, SpecialtyBonus: 1.4, MoodOffset: -4),
    };

    private static readonly string[] FirstNames =
    {
        "Mara", "Dez", "Otto", "Juniper", "Silas", "Wren", "Bram", "Ines", "Cato", "Lupe",
        "Hollis", "Nadia", "Ezra", "Tamsin", "Gus", "Odile", "Rook", "Priya", "Fenn", "Agnes",
    };

    private static readonly string[] LastNames =
    {
        "Calloway", "Vance", "Okafor", "Brandt", "Moreau", "Kettle", "Ashby", "Novak", "Pruitt", "Sato",
        "Greaves", "Lindqvist", "Marsh", "Quill", "Dunmore", "Ferro", "Hale", "Iwu", "Rask", "Thorne",
    };

    private static readonly string[] Nicknames =
    {
        "Two-Socks", "Tin Can", "Doc", "Lucky", "Biscuit", "Mayor", "Sparks", "Gravel", "Pockets", "Saint",
    };

    private static readonly (string Text, Skill Bonus)[] Childhoods =
    {
        ("Grew up in the rubble. Thinks 'before' is a story adults made up to feel sad.", Skill.Scavenging),
        ("Raised in a highway tollbooth by a family that never once opened the barrier.", Skill.Social),
        ("Born in a shopping cart convoy. Still steers slightly to the left.", Skill.Scavenging),
        ("Grew up on a rooftop farm, tending tomatoes and grudges.", Skill.Farming),
        ("Spent childhood in a scout troop that took 'be prepared' extremely personally.", Skill.Shooting),
        ("Was a hospital volunteer who mostly stole lollipops. Learned some things anyway.", Skill.Medicine),
        ("Built treehouses. Several are still standing, unlike the houses.", Skill.Construction),
    };

    private static readonly (string Text, Skill Bonus)[] Adulthoods =
    {
        ("Insurance claims adjuster. Saw the end of the world coming and denied it anyway.", Skill.Social),
        ("Mall Santa. Still flinches whenever a child approaches.", Skill.Social),
        ("Bunker timeshare salesperson. Business was booming until it very literally was.", Skill.Social),
        ("Competitive eater. Years of training have finally paid off, in a sense.", Skill.Farming),
        ("Middle manager. Keeps scheduling meetings that nobody attends.", Skill.Construction),
        ("Demolition contractor. Feels partly responsible for the skyline, but only partly.", Skill.Construction),
        ("Veterinarian. Treats people the same way, and honestly the results are fine.", Skill.Medicine),
        ("Paintball referee. Has strong opinions on what counts as a hit.", Skill.Shooting),
        ("Scrapyard owner. The apocalypse barely changed the inventory.", Skill.Scavenging),
        ("Community garden treasurer. Fled with the seed fund.", Skill.Farming),
    };

    public Settler Create(GridPos position)
    {
        string first, last;
        int guard = 0;
        do
        {
            first = Pick(FirstNames);
            last = Pick(LastNames);
        } while (!_usedNames.Add($"{first} {last}") && ++guard < 100);

        var childhood = Pick(Childhoods);
        var adulthood = Pick(Adulthoods);

        var skills = new Dictionary<Skill, int>();
        foreach (var s in Enum.GetValues<Skill>())
            skills[s] = _rng.Next(0, 7);
        skills[childhood.Bonus] = Math.Min(20, skills[childhood.Bonus] + 3 + _rng.Next(0, 3));
        skills[adulthood.Bonus] = Math.Min(20, skills[adulthood.Bonus] + 5 + _rng.Next(0, 5));

        return new Settler
        {
            Id = _nextId++,
            FirstName = first,
            LastName = last,
            Nickname = _rng.NextDouble() < 0.3 ? Pick(Nicknames) : null,
            Age = 18 + _rng.Next(0, 45),
            Childhood = childhood.Text,
            Adulthood = adulthood.Text,
            Traits = PickTraits(2 + _rng.Next(0, 2)),
            Skills = skills,
            Position = position,
            Hunger = 60 + _rng.Next(0, 40),
            Rest = 60 + _rng.Next(0, 40),
        };
    }

    private List<Trait> PickTraits(int count)
    {
        var chosen = new List<Trait>();
        var pool = AllTraits.OrderBy(_ => _rng.Next()).ToList();
        foreach (var t in pool)
        {
            if (chosen.Count >= count) break;
            if (t.ExcludesTag is { } tag && chosen.Any(c => c.ExcludesTag == tag)) continue;
            if (t.Specialty is not null && chosen.Any(c => c.Specialty is not null)) continue;
            chosen.Add(t);
        }
        return chosen;
    }

    private T Pick<T>(IReadOnlyList<T> items) => items[_rng.Next(items.Count)];
}
