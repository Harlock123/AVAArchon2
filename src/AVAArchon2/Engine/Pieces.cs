namespace AVAArchon2.Engine;

/// <summary>The controls one player has: a stick (eight ways), fire, and a second button.</summary>
public record struct Controls(int Dx, int Dy, bool Fire = false, bool Alt = false);

/// <summary>The two powers at war: Order starts on the left, Chaos on the right.</summary>
public enum Side { Order, Chaos }

public enum Kind
{
    // The adepts: four on each side, who move a little and cast the spells.
    Magus, Sorcerer,
    // The elementals, called by either side's adepts: each strongest on its own ring of the board.
    FireElemental, WaterElemental, AirElemental, EarthElemental,
    // The demons: Order's two, then Chaos's two.
    Seraph, Colossus, Fiend, Behemoth,
}

/// <summary>The four elements, and the void at the board's heart.</summary>
public enum Element { Fire, Water, Air, Earth, Void }

/// <summary>How a piece fights in the arena.</summary>
public enum Attack
{
    /// <summary>A blow at close range, in the direction it faces.</summary>
    Melee,
    /// <summary>Missiles, in the direction it faces.</summary>
    Shot,
    /// <summary>A burst all around it; it can't move while the burst lasts.</summary>
    Aura,
}

/// <summary>
/// What a kind of piece is. <see cref="Move"/> is how far it goes on the board; fliers may pass over
/// other pieces, walkers may not. In the arena: <see cref="Life"/>, <see cref="Speed"/> in pixels a
/// second, and its weapon: <see cref="Damage"/> per hit, <see cref="Reload"/> seconds between attacks,
/// and for missiles their speed, size and how many fly at once (in a fan). An elemental belongs to
/// <see cref="Element"/>; a demon is called from afar.
/// </summary>
public sealed record PieceType(
    Kind Kind, string Name, Side Side, int Move, bool Flies, int Life, double Speed,
    Attack Attack, int Damage, double Reload, double Reach = 0, double ShotSpeed = 0, double ShotSize = 0, int Shots = 1,
    string Weapon = "", string About = "", Element? Element = null, bool Demon = false)
{
    public bool Adept => Kind is Kind.Magus or Kind.Sorcerer;
    /// <summary>The adepts are the ones who cast.</summary>
    public bool Leader => Adept;
    public bool Elemental => Element is not null;

    /// <summary>A rough worth in battle, for the computer's judgement: how long it lasts times how hard it hits.</summary>
    public double Power => Life * (Damage * Shots / Reload) * (Attack == Attack.Shot ? 1.15 : 1.0) * (0.6 + Speed / 400);

    public static readonly IReadOnlyDictionary<Kind, PieceType> All = new Dictionary<Kind, PieceType>
    {
        [Kind.Magus] = new(Kind.Magus, "Magus", Side.Order, 2, true, 16, 160, Attack.Shot, 7, 1.0, ShotSpeed: 470, ShotSize: 6,
            Weapon: "bolts of light", About: "An adept of Order: moves a little, and casts the spells."),
        [Kind.Sorcerer] = new(Kind.Sorcerer, "Sorcerer", Side.Chaos, 2, true, 16, 160, Attack.Shot, 7, 1.0, ShotSpeed: 470, ShotSize: 6,
            Weapon: "bolts of shadow", About: "An adept of Chaos: moves a little, and casts the spells."),
        [Kind.FireElemental] = new(Kind.FireElemental, "Fire Elemental", Side.Order, 4, true, 20, 185, Attack.Aura, 12, 1.4, Reach: 125,
            Weapon: "a ring of fire", About: "Burns all around it. Strongest on the ring of Fire.", Element: Engine.Element.Fire),
        [Kind.WaterElemental] = new(Kind.WaterElemental, "Water Elemental", Side.Order, 3, false, 30, 115, Attack.Melee, 12, 0.8, Reach: 58,
            Weapon: "a crushing wave", About: "Slow and heavy. Strongest on the ring of Water.", Element: Engine.Element.Water),
        [Kind.AirElemental] = new(Kind.AirElemental, "Air Elemental", Side.Order, 5, true, 16, 240, Attack.Shot, 6, 0.55, ShotSpeed: 520, ShotSize: 5,
            Weapon: "whirlwinds", About: "Quick, and hard to hit. Strongest on the ring of Air.", Element: Engine.Element.Air),
        [Kind.EarthElemental] = new(Kind.EarthElemental, "Earth Elemental", Side.Order, 2, false, 36, 80, Attack.Shot, 15, 1.7, ShotSpeed: 230, ShotSize: 12,
            Weapon: "boulders", About: "Slow, very tough, and throws boulders. Strongest on the ring of Earth.", Element: Engine.Element.Earth),
        [Kind.Seraph] = new(Kind.Seraph, "Seraph", Side.Order, 4, true, 26, 190, Attack.Shot, 9, 1.2, ShotSpeed: 480, ShotSize: 6, Shots: 3,
            Weapon: "holy fire", About: "A demon of Order, called to strike anywhere on the board.", Demon: true),
        [Kind.Colossus] = new(Kind.Colossus, "Colossus", Side.Order, 3, false, 40, 85, Attack.Shot, 14, 1.5, ShotSpeed: 260, ShotSize: 12,
            Weapon: "hurled stone", About: "A demon of Order: a walking statue that hurls stone.", Demon: true),
        [Kind.Fiend] = new(Kind.Fiend, "Fiend", Side.Chaos, 4, true, 26, 190, Attack.Shot, 9, 1.2, ShotSpeed: 480, ShotSize: 6, Shots: 3,
            Weapon: "hellfire", About: "A demon of Chaos, called to strike anywhere on the board.", Demon: true),
        [Kind.Behemoth] = new(Kind.Behemoth, "Behemoth", Side.Chaos, 3, false, 40, 85, Attack.Shot, 14, 1.5, ShotSpeed: 260, ShotSize: 12,
            Weapon: "hurled brimstone", About: "A demon of Chaos: huge, slow, and throws brimstone.", Demon: true),
    };

    public static Kind ElementalOf(Element e) => e switch
    {
        Engine.Element.Fire => Kind.FireElemental,
        Engine.Element.Water => Kind.WaterElemental,
        Engine.Element.Air => Kind.AirElemental,
        _ => Kind.EarthElemental,
    };
}

/// <summary>A piece on the board (or fallen).</summary>
public sealed class Piece
{
    public required int Id { get; init; }
    public required PieceType Type { get; init; }
    public required Side Side { get; init; }
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Its life now; wounds heal a little each turn.</summary>
    public int Life { get; set; }
    public bool Alive { get; set; } = true;
    /// <summary>Turns of its own side it still can't move, from the Imprison spell.</summary>
    public int Imprisoned { get; set; }

    public Kind Kind => Type.Kind;
    public string Name => Type.Name;
    public bool Wounded => Life < Type.Life;
    public override string ToString() => $"{Side} {Name} at {X},{Y} ({Life}/{Type.Life})";
}
