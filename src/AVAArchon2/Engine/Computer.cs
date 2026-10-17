namespace AVAArchon2.Engine;

/// <summary>What the computer decides to do with its turn.</summary>
public abstract record Plan;
public sealed record MovePlan(Piece Piece, int X, int Y) : Plan;
/// <summary>A spell, the adept casting it, and its target (a piece, or a square).</summary>
public sealed record SpellPlan(Spell Spell, Piece Caster, Piece? Target = null, int X = -1, int Y = -1) : Plan;

/// <summary>
/// The computer's general. It weighs every move it has: a fight it should win, a power point taken or
/// held, a piece pulled back out of danger, a step toward the enemy. Often a spell is worth more than any
/// move: an elemental called onto a power point or against a foe it should beat, a demon against a piece
/// worth the mana, an adept healed, a strong enemy weakened, imprisoned or banished. The easier the
/// setting, the more its judgement wanders.
/// </summary>
public sealed class Computer(Difficulty difficulty, Random rng)
{
    public Difficulty Difficulty => difficulty;

    /// <summary>A piece's worth: how often it beats the other side's pieces, and for a leader, its spells too.</summary>
    public static double Value(PieceType t) => Values.GetValueOrDefault(t.Kind, 1.0);

    private static readonly Dictionary<Kind, double> Values = PieceType.All.Values.ToDictionary(t => t.Kind, t =>
    {
        // (Against everything the other side could field, as if this were an Order piece.)
        var foes = PieceType.All.Values.Where(o => o.Side == Side.Chaos || o.Elemental);
        double wins = foes.Average(o => OddsTable.OrderWins.GetValueOrDefault((Mirror(t.Kind), Mirror(o.Kind)), 0.5));
        return (0.5 + 2.5 * wins) * (t.Adept ? 2.6 : 1.0);
    });

    /// <summary>The table is of Order's kinds against Chaos's: a Chaos kind on Order's side is looked up as its Order twin.</summary>
    private static Kind Mirror(Kind k) => k switch { Kind.Sorcerer => Kind.Magus, Kind.Fiend => Kind.Seraph, Kind.Behemoth => Kind.Colossus, _ => k };
    private static Kind MirrorChaos(Kind k) => k switch { Kind.Magus => Kind.Sorcerer, Kind.Seraph => Kind.Fiend, Kind.Colossus => Kind.Behemoth, _ => k };

    /// <summary>
    /// The chance that a (fighting for <paramref name="sideA"/>) beats b, with their lives now (squares'
    /// bonuses included): the measured odds at full life, shifted by how much life each has.
    /// </summary>
    public static double Odds(PieceType a, Side sideA, double lifeA, PieceType b, double lifeB)
    {
        double p = sideA == Side.Order
            ? OddsTable.OrderWins.GetValueOrDefault((Mirror(a.Kind), MirrorChaos(b.Kind)), 0.5)
            : 1 - OddsTable.OrderWins.GetValueOrDefault((Mirror(b.Kind), MirrorChaos(a.Kind)), 0.5);
        p = Math.Clamp(p, 0.02, 0.98);
        double logit = Math.Log(p / (1 - p)) + 2.5 * Math.Log(lifeA / a.Life / (lifeB / b.Life));
        return 1 / (1 + Math.Exp(-logit));
    }

    public static double Odds(Board board, Piece attacker, Piece defender, int x, int y) =>
        Odds(attacker.Type, attacker.Side, attacker.Life + board.Bonus(attacker.Type, x, y), defender.Type, defender.Life + board.Bonus(defender.Type, x, y));

    private double Noise => difficulty switch { Difficulty.Easy => 0.9, Difficulty.Normal => 0.3, _ => 0.08 };

    /// <param name="quietTurns">Turns since the last fight: the longer the quiet, the bolder the computer gets.</param>
    public Plan Choose(Board board, Side side, int quietTurns = 0)
    {
        double caution = Math.Max(0.4, 1 - quietTurns / 50.0);
        var enemies = board.Living(Board.Other(side)).ToList();
        double mine = board.Living(side).Sum(p => Value(p.Type)), theirs = enemies.Sum(p => Value(p.Type));
        // Closing in: worth more when ahead, or when nothing has happened for a while.
        double closing = (mine >= theirs * 0.9 ? 0.06 : 0.02) + quietTurns * 0.004;
        int Nearest(int x, int y) => enemies.Count == 0 ? 0 : enemies.Min(e => Math.Max(Math.Abs(e.X - x), Math.Abs(e.Y - y)));
        Plan? best = null;
        double bestScore = double.MinValue;
        var enemy = Board.Other(side);
        int heldBefore = Board.PowerPoints.Count(pp => board.At(pp.X, pp.Y)?.Side == side);
        int enemyHeld = Board.PowerPoints.Count(pp => board.At(pp.X, pp.Y)?.Side == enemy);
        var reach = Reach(board, enemy);

        foreach (var p in board.Living(side).ToList())
        {
            var moves = board.Moves(p);
            if (moves.Count == 0) continue;
            double here = Danger(board, p, p.X, p.Y, reach) * caution;
            int near = Nearest(p.X, p.Y);
            bool onPoint = Board.IsPowerPoint(p.X, p.Y);
            foreach (var (x, y) in moves)
            {
                double score;
                var target = board.At(x, y);
                bool point = Board.IsPowerPoint(x, y);
                if (target is not null)
                {
                    double win = Odds(board, p, target, x, y);
                    score = win * Value(target.Type) - (1 - win) * Value(p.Type) * caution;
                    if (point) score += win * PointWorth(heldBefore + (onPoint ? 0 : 1), enemyHeld, true);
                }
                else
                {
                    score = here - Danger(board, p, x, y, reach) * caution + closing * (near - Nearest(x, y));
                    if (point && !onPoint) score += PointWorth(heldBefore + 1, enemyHeld, false);
                    // Edge toward the middle, and toward the enemy.
                    double towards = side == Side.Order ? x - p.X : p.X - x;
                    score += 0.03 * towards + 0.02 * (Math.Abs(p.Y - 4) - Math.Abs(y - 4));
                }
                if (onPoint && !point) score -= PointWorth(heldBefore, enemyHeld, false) * 0.8;
                score += (rng.NextDouble() - 0.5) * Noise;
                if (score > bestScore) { bestScore = score; best = new MovePlan(p, x, y); }
            }
        }

        var spell = BestSpell(board, side, reach, out double spellScore);
        if (spell is not null && (best is null || spellScore > bestScore + 0.15)) return spell;
        return best ?? (Plan?)spell ?? new MovePlan(board.Living(side).First(), board.Living(side).First().X, board.Living(side).First().Y);
    }

    /// <summary>How much a power point matters: more the more you hold, and everything if it's the last.</summary>
    private static double PointWorth(int heldAfter, int enemyHeld, bool takenFromEnemy)
    {
        if (heldAfter >= Board.PowerPoints.Length) return 50;
        double w = 0.6 + 0.25 * heldAfter;
        if (takenFromEnemy && enemyHeld >= Board.PowerPoints.Length - 1) w += 8;
        return w;
    }

    /// <summary>Where each enemy piece could go next turn.</summary>
    public static Dictionary<Piece, HashSet<(int X, int Y)>> Reach(Board board, Side enemy) =>
        board.Living(enemy).ToDictionary(q => q, board.Moves);

    /// <summary>The worst an enemy that can reach (x, y) next turn might do to this piece there.</summary>
    public static double Danger(Board board, Piece p, int x, int y, Dictionary<Piece, HashSet<(int X, int Y)>> reach)
    {
        double worst = 0;
        foreach (var (q, moves) in reach)
        {
            if (!moves.Contains((x, y))) continue;
            double win = Odds(q.Type, q.Side, q.Life + board.Bonus(q.Type, x, y), p.Type, p.Life + board.Bonus(p.Type, x, y));
            worst = Math.Max(worst, win * Value(p.Type) - (1 - win) * Value(q.Type));
        }
        return Math.Max(0, worst) * 0.85;
    }

    private SpellPlan? BestSpell(Board board, Side side, Dictionary<Piece, HashSet<(int X, int Y)>> reach, out double score)
    {
        score = double.MinValue;
        SpellPlan? best = null;
        double bestScore = double.MinValue;
        void Consider(SpellPlan plan, double s)
        {
            // Mana spent is mana gone: a little caution with it.
            s -= Spells.Cost(plan.Spell) * 0.008;
            s += (rng.NextDouble() - 0.5) * Noise;
            if (s > bestScore) { bestScore = s; best = plan; }
        }
        var enemy = Board.Other(side);
        int mana = board.ManaOf(side);
        int held = board.PointsHeld(side);
        foreach (var adept in board.Adepts(side).Where(a => a.Imprisoned == 0))
        {
            // An elemental: onto an empty power point near, or against a foe it should beat.
            if (Spells.CanCast(board, side, Spell.Summon, adept))
            {
                var element = Board.ElementAt(adept.X, adept.Y);
                var kind = PieceType.All[PieceType.ElementalOf(element == Element.Void ? Element.Earth : element)];
                foreach (var (x, y) in Spells.Targets(board, side, Spell.Summon, adept))
                {
                    var foe = board.At(x, y);
                    if (foe is null)
                    {
                        if (Board.IsPowerPoint(x, y)) Consider(new SpellPlan(Spell.Summon, adept, null, x, y), (held + 1 >= Board.PowerPoints.Length ? 50 : 0.9 + 0.25 * held) - Danger(board, new Piece { Id = -1, Type = kind, Side = side, Life = kind.Life }, x, y, reach));
                        else if (mana > 120) Consider(new SpellPlan(Spell.Summon, adept, null, x, y), 0.25 - Danger(board, new Piece { Id = -1, Type = kind, Side = side, Life = kind.Life }, x, y, reach));
                        continue;
                    }
                    double win = Odds(kind, side, kind.Life + board.Bonus(kind, x, y), foe.Type, foe.Life + board.Bonus(foe.Type, x, y));
                    if (win > 0.55) Consider(new SpellPlan(Spell.Summon, adept, foe, x, y), win * Value(foe.Type) - (1 - win) * 0.8 + 0.3);
                }
            }
            if (Spells.CanCast(board, side, Spell.Demon, adept))
                foreach (var foe in board.Living(enemy).Where(f => !Board.IsPowerPoint(f.X, f.Y) && !f.Type.Adept))
                {
                    var demon = PieceType.All[Spells.DemonFor(side, foe)];
                    double win = Odds(demon, side, demon.Life, foe.Type, foe.Life + board.Bonus(foe.Type, foe.X, foe.Y));
                    if (win > 0.6) Consider(new SpellPlan(Spell.Demon, adept, foe, foe.X, foe.Y), win * Value(foe.Type) - (1 - win) * 0.5);
                }
            if (Spells.CanCast(board, side, Spell.Heal, adept))
                foreach (var p in board.Living(side).Where(p => p.Life * 2 < p.Type.Life && !Board.IsPowerPoint(p.X, p.Y)))
                {
                    double missing = 1 - (double)p.Life / p.Type.Life;
                    Consider(new SpellPlan(Spell.Heal, adept, p, p.X, p.Y), Value(p.Type) * missing * 0.6 - 0.2);
                }
            if (Spells.CanCast(board, side, Spell.Weaken, adept))
                foreach (var foe in board.Living(enemy).Where(f => !Board.IsPowerPoint(f.X, f.Y) && f.Life > f.Type.Life / 2 && f.Life > 10 && !f.Type.Adept))
                    Consider(new SpellPlan(Spell.Weaken, adept, foe, foe.X, foe.Y), Value(foe.Type) * 0.18 - 0.25);
            if (Spells.CanCast(board, side, Spell.Imprison, adept))
                foreach (var foe in board.Living(enemy).Where(f => !Board.IsPowerPoint(f.X, f.Y) && f.Imprisoned == 0 && !f.Type.Adept && Value(f.Type) > 1.6))
                    Consider(new SpellPlan(Spell.Imprison, adept, foe, foe.X, foe.Y), Value(foe.Type) * 0.2 - 0.3);
            if (Spells.CanCast(board, side, Spell.Banish, adept))
                foreach (var foe in board.Living(enemy).Where(f => !Board.IsPowerPoint(f.X, f.Y) && !f.Type.Adept))
                    Consider(new SpellPlan(Spell.Banish, adept, foe, foe.X, foe.Y), Value(foe.Type) * 0.8 - 0.4);
        }
        score = bestScore;
        return best;
    }
}
