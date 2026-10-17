namespace AVAArchon2.Engine;

/// <summary>The adepts' spells. Any adept may cast any of them, so long as its side has the mana.</summary>
public enum Spell { Summon, Demon, Heal, Weaken, Imprison, Banish }

/// <summary>
/// The 9×9 board: four rings, one for each element, round the void at its heart; the six power points;
/// each side's mana; and the pieces. Order starts on the left, Chaos on the right.
/// </summary>
public sealed class Board
{
    public const int Size = 9;
    /// <summary>A piece on a power point heals this much a turn (others heal 1), and spells can't touch it.</summary>
    public const int PowerPointHealing = 5;
    /// <summary>The strength an elemental draws from its own ring: extra life in the arena.</summary>
    public const int ElementBonus = 8;
    /// <summary>The mana each side starts with, and the most it can hold.</summary>
    public const int Mana0 = 60, MaxMana = 200;
    /// <summary>Mana gained at the end of each of a side's turns, and more for each power point it holds.</summary>
    public const int ManaIncome = 5, ManaPerPoint = 4;

    /// <summary>Two on the ring of Fire, one on the ring of Water near each side, two on the ring of Earth.</summary>
    public static readonly (int X, int Y)[] PowerPoints = [(4, 0), (4, 8), (1, 4), (7, 4), (4, 3), (4, 5)];

    public List<Piece> Pieces { get; } = [];
    public int[] Mana { get; } = [Mana0, Mana0];

    /// <summary>Which element's ring a square lies on: Fire at the edge, then Water, Air and Earth, and the void in the middle.</summary>
    public static Element ElementAt(int x, int y) => Math.Max(Math.Abs(x - 4), Math.Abs(y - 4)) switch
    {
        4 => Element.Fire,
        3 => Element.Water,
        2 => Element.Air,
        1 => Element.Earth,
        _ => Element.Void,
    };

    public static bool IsPowerPoint(int x, int y) => Array.IndexOf(PowerPoints, (x, y)) >= 0;
    public static bool OnBoard(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

    /// <summary>The extra life a piece fights with on a square: an elemental on its own ring, an adept on a power point.</summary>
    public int Bonus(PieceType t, int x, int y) =>
        t.Element is { } e && e == ElementAt(x, y) ? ElementBonus : t.Adept && IsPowerPoint(x, y) ? 4 : 0;

    public Piece? At(int x, int y) => Pieces.FirstOrDefault(p => p.Alive && p.X == x && p.Y == y);
    public IEnumerable<Piece> Living(Side side) => Pieces.Where(p => p.Alive && p.Side == side);
    public IEnumerable<Piece> Fallen(Side side) => Pieces.Where(p => !p.Alive && p.Side == side);
    public IEnumerable<Piece> Adepts(Side side) => Living(side).Where(p => p.Type.Adept);
    public Piece? Leader(Side side) => Adepts(side).FirstOrDefault();
    public int ManaOf(Side side) => Mana[(int)side];

    /// <summary>The four adepts of each side drawn up on their home edges.</summary>
    public static Board Start()
    {
        var b = new Board();
        foreach (int y in new[] { 1, 3, 5, 7 })
        {
            b.Add(Kind.Magus, 0, y);
            b.Add(Kind.Sorcerer, 8, y);
        }
        return b;
    }

    public Piece Add(Kind kind, int x, int y, Side? side = null)
    {
        var t = PieceType.All[kind];
        var p = new Piece { Id = Pieces.Count, Type = t, Side = side ?? t.Side, X = x, Y = y, Life = t.Life };
        Pieces.Add(p);
        return p;
    }

    /// <summary>
    /// Where a piece can go this turn. Walkers step up to their move in any of eight directions through
    /// empty squares; fliers go anywhere within their move. Either may end on an enemy, to fight it.
    /// </summary>
    public HashSet<(int X, int Y)> Moves(Piece p)
    {
        var result = new HashSet<(int, int)>();
        if (!p.Alive || p.Imprisoned > 0 || !OnBoard(p.X, p.Y)) return result;
        int range = p.Type.Move;
        if (p.Type.Flies)
        {
            for (int y = p.Y - range; y <= p.Y + range; y++)
                for (int x = p.X - range; x <= p.X + range; x++)
                {
                    if (!OnBoard(x, y) || (x, y) == (p.X, p.Y)) continue;
                    var o = At(x, y);
                    if (o is null || o.Side != p.Side) result.Add((x, y));
                }
            return result;
        }
        var frontier = new List<(int, int)> { (p.X, p.Y) };
        var seen = new HashSet<(int, int)> { (p.X, p.Y) };
        for (int step = 0; step < range; step++)
        {
            var next = new List<(int, int)>();
            foreach (var (fx, fy) in frontier)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = fx + dx, y = fy + dy;
                        if ((dx, dy) == (0, 0) || !OnBoard(x, y) || !seen.Add((x, y))) continue;
                        var o = At(x, y);
                        if (o is null) { result.Add((x, y)); next.Add((x, y)); }
                        else if (o.Side != p.Side) result.Add((x, y));
                    }
            frontier = next;
        }
        return result;
    }

    /// <summary>The side holding all six power points, if any.</summary>
    public Side? HoldsAllPowerPoints()
    {
        Side? holder = null;
        foreach (var (x, y) in PowerPoints)
        {
            var p = At(x, y);
            if (p is null || (holder is { } h && h != p.Side)) return null;
            holder = p.Side;
        }
        return holder;
    }

    public int PointsHeld(Side side) => PowerPoints.Count(pp => At(pp.X, pp.Y)?.Side == side);

    /// <summary>The end of a side's turn: its wounded heal, its prisoners count down, and its mana grows.</summary>
    public void EndTurn(Side side)
    {
        foreach (var p in Living(side))
        {
            if (p.Wounded) p.Life = Math.Min(p.Type.Life, p.Life + (IsPowerPoint(p.X, p.Y) ? PowerPointHealing : 1));
            if (p.Imprisoned > 0) p.Imprisoned--;
        }
        Mana[(int)side] = Math.Min(MaxMana, Mana[(int)side] + ManaIncome + ManaPerPoint * PointsHeld(side));
    }

    public static Side Other(Side s) => s == Side.Order ? Side.Chaos : Side.Order;
}
