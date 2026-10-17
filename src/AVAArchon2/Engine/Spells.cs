namespace AVAArchon2.Engine;

/// <summary>
/// The adepts' spells: what each costs, what it does, and what it may touch. Any adept may cast, so long as
/// its side has the mana and it isn't imprisoned; no spell may touch a piece standing on a power point.
/// </summary>
public static class Spells
{
    public const int ImprisonTurns = 3;
    /// <summary>How far from its adept an elemental may be called.</summary>
    public const int SummonRange = 2;

    public static readonly Spell[] All = Enum.GetValues<Spell>();

    public static int Cost(Spell s) => s switch
    {
        Spell.Summon => 20,
        Spell.Demon => 50,
        Spell.Heal => 12,
        Spell.Weaken => 14,
        Spell.Imprison => 20,
        _ => 30,
    };

    public static string Name(Spell s) => s switch
    {
        Spell.Summon => "Summon Elemental",
        Spell.Demon => "Call a Demon",
        _ => s.ToString(),
    };

    public static string About(Spell s) => s switch
    {
        Spell.Summon => $"Call the elemental of the ring this adept stands on, to a square within {SummonRange}. Onto an enemy, it fights.",
        Spell.Demon => "Call one of your demons against any enemy elemental or demon on the board.",
        Spell.Heal => "Heal one of your badly hurt pieces (more than half its life gone) of every wound.",
        Spell.Weaken => "Take half the life from an enemy piece that isn't already weakened.",
        Spell.Imprison => $"Hold an enemy elemental or demon where it stands for {ImprisonTurns} of its turns.",
        Spell.Banish => "Send an enemy elemental or demon back where it came from.",
        _ => "",
    };

    /// <summary>Whether an adept can cast a spell now: the mana, and something to cast on.</summary>
    public static bool CanCast(Board b, Side side, Spell s, Piece? caster) =>
        caster is { Alive: true, Imprisoned: 0 } && caster.Type.Adept && caster.Side == side && b.ManaOf(side) >= Cost(s) && Targets(b, side, s, caster).Count > 0;

    public static bool CanCastAny(Board b, Side side, Piece? caster) => All.Any(s => CanCast(b, side, s, caster));

    /// <summary>Whether any of a side's adepts can cast anything.</summary>
    public static bool CanCastAny(Board b, Side side) => b.Adepts(side).Any(a => CanCastAny(b, side, a));

    private static bool Touchable(Piece p) => !Board.IsPowerPoint(p.X, p.Y);

    /// <summary>The squares the spell may be cast on.</summary>
    public static HashSet<(int X, int Y)> Targets(Board b, Side side, Spell s, Piece caster)
    {
        var enemy = Board.Other(side);
        if (s == Spell.Summon)
        {
            var set = new HashSet<(int, int)>();
            for (int y = caster.Y - SummonRange; y <= caster.Y + SummonRange; y++)
                for (int x = caster.X - SummonRange; x <= caster.X + SummonRange; x++)
                {
                    if (!Board.OnBoard(x, y)) continue;
                    var o = b.At(x, y);
                    if (o is null || o.Side == enemy && Touchable(o)) set.Add((x, y));
                }
            return set;
        }
        IEnumerable<Piece> pieces = s switch
        {
            // (Adepts are shielded from demons: only elementals and demons can be set upon from afar.)
            Spell.Demon => b.Living(enemy).Where(p => Touchable(p) && !p.Type.Adept),
            // (Healing is for the badly hurt, and a weakened piece can't be weakened again.)
            Spell.Heal => b.Living(side).Where(p => Touchable(p) && p.Life * 2 < p.Type.Life),
            Spell.Weaken => b.Living(enemy).Where(p => Touchable(p) && p.Life > p.Type.Life / 2),
            Spell.Imprison => b.Living(enemy).Where(p => Touchable(p) && p.Imprisoned == 0 && !p.Type.Adept),
            Spell.Banish => b.Living(enemy).Where(p => Touchable(p) && !p.Type.Adept),
            _ => [],
        };
        return pieces.Select(p => (p.X, p.Y)).ToHashSet();
    }

    /// <summary>Which demon answers a side's call: the one fitter for the foe (the flier against a flier).</summary>
    public static Kind DemonFor(Side side, Piece foe)
    {
        var (quick, heavy) = side == Side.Order ? (Kind.Seraph, Kind.Colossus) : (Kind.Fiend, Kind.Behemoth);
        return foe.Type.Life >= 26 ? heavy : quick;
    }
}
