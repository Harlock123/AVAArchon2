namespace AVAArchon2.Engine;

public sealed partial class GameEngine
{
    /// <summary>Each side's cursor on the board (Order's, then Chaos's).</summary>
    public (int X, int Y)[] Cursors { get; } = [(1, 4), (7, 4)];
    public (int X, int Y) Cursor => Cursors[(int)Turn];

    /// <summary>The piece picked up this turn, if any.</summary>
    public Piece? Selected { get; private set; }
    /// <summary>The squares lit up: where the picked piece may go, or what a spell may touch.</summary>
    public HashSet<(int X, int Y)> Lit { get; private set; } = [];

    // The spellbook and its targets.
    public int SpellIndex { get; private set; }
    public Spell? Casting { get; private set; }
    public Piece? FirstTarget { get; private set; }
    /// <summary>The adept casting.</summary>
    public Piece? Caster { get; private set; }

    // A piece on its way.
    public Piece? Mover { get; private set; }
    public (int X, int Y) MoveFrom { get; private set; }
    public (int X, int Y) MoveTo { get; private set; }
    public double MoveSeconds { get; private set; }

    // A fight: who started it, whom, where, and the arena.
    public Piece? Attacker { get; private set; }
    public Side AttackerSide { get; private set; }
    public Piece? Defender { get; private set; }
    public (int X, int Y) Battleground { get; private set; }
    public Fight? Fight { get; private set; }
    private Brain? _orderBrain, _chaosBrain;
    public string Outcome { get; private set; } = "";

    private Plan? _plan;
    private Action? _afterCast;

    private void ToBoard()
    {
        Phase = Phase.Board;
        PhaseTime = 0;
        Selected = null;
        Lit = [];
        Casting = null;
        FirstTarget = null;
        Mover = null;
        Fight = null;
        Attacker = Defender = null;
        _plan = null;
    }

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }

    private bool Human(Side side) => State == GameState.Playing && !IsComputer(side);

    private void RunTurn(double dt)
    {
        switch (Phase)
        {
            case Phase.Board:
                if (Human(Turn)) BoardControls(); else ComputerTurn();
                break;
            case Phase.Spellbook:
                if (Human(Turn)) SpellbookControls();
                break;
            case Phase.Target:
                if (Human(Turn)) TargetControls();
                break;
            case Phase.Moving:
                if (PhaseTime >= MoveSeconds) Arrive();
                break;
            case Phase.Cast:
                if (PhaseTime >= 1.0)
                {
                    var then = _afterCast;
                    _afterCast = null;
                    if (then is not null) then(); else EndTurn();
                }
                break;
            case Phase.Clash:
                if (PhaseTime >= 1.5) OpenArena();
                break;
            case Phase.Fight:
                RunFight(dt);
                break;
            case Phase.Aftermath:
                if (PhaseTime >= 1.8) SettleFight();
                break;
        }
    }

    // ───────────────────────────── a human's turn ─────────────────────────────

    private void MoveCursor(Pad pad)
    {
        var (sx, sy) = pad.Step;
        if (sx == 0 && sy == 0) return;
        ref var c = ref Cursors[(int)Turn];
        var n = (Math.Clamp(c.X + sx, 0, Board.Size - 1), Math.Clamp(c.Y + sy, 0, Board.Size - 1));
        if (n != c) { c = n; Play(Sfx.Cursor); }
    }

    private void BoardControls()
    {
        var pad = PadFor(Turn);
        MoveCursor(pad);
        if (pad.AltEdge && Selected is not null) { Selected = null; Lit = []; Play(Sfx.Cancel); return; }
        if (!pad.FireEdge) return;
        var (x, y) = Cursor;
        var here = Board.At(x, y);
        if (Selected is null || here?.Side == Turn && here != Selected)
        {
            if (here is null || here.Side != Turn) { Play(Sfx.Buzz); return; }
            Pick(here);
            return;
        }
        if (here == Selected)
        {
            if (Selected.Type.Adept && Spells.CanCastAny(Board, Turn, Selected)) OpenSpellbook();
            else { Selected = null; Lit = []; Play(Sfx.Cancel); }
            return;
        }
        if (Lit.Contains((x, y))) BeginMove(Selected, x, y);
        else Play(Sfx.Buzz);
    }

    /// <summary>Picks up a piece, lighting the squares it can reach.</summary>
    public bool Pick(Piece p)
    {
        if (p.Imprisoned > 0)
        {
            Say($"The {p.Name} is imprisoned for {p.Imprisoned} more {(p.Imprisoned == 1 ? "turn" : "turns")}");
            Play(Sfx.Buzz);
            return false;
        }
        var moves = Board.Moves(p);
        if (moves.Count == 0 && !(p.Type.Adept && Spells.CanCastAny(Board, Turn, p)))
        {
            Say($"The {p.Name} has nowhere to go");
            Play(Sfx.Buzz);
            return false;
        }
        Selected = p;
        Lit = moves;
        Play(Sfx.Select);
        if (p.Type.Adept && Spells.CanCastAny(Board, Turn, p) && Human(Turn)) Say("Fire on the adept again to open its spellbook", 2.5);
        return true;
    }

    private void OpenSpellbook()
    {
        SetPhase(Phase.Spellbook);
        Caster = Selected;
        Message = null;
        SpellIndex = Array.FindIndex(Spells.All, s => Spells.CanCast(Board, Turn, s, Caster));
        if (SpellIndex < 0) SpellIndex = 0;
        Play(Sfx.Spell);
    }

    private void SpellbookControls()
    {
        var pad = PadFor(Turn);
        var (_, sy) = pad.Step;
        if (sy != 0) { SpellIndex = (SpellIndex + sy + Spells.All.Length) % Spells.All.Length; Play(Sfx.Menu); }
        if (pad.AltEdge) { SetPhase(Phase.Board); Play(Sfx.Cancel); return; }
        if (!pad.FireEdge) return;
        var spell = Spells.All[SpellIndex];
        if (!Spells.CanCast(Board, Turn, spell, Caster)) { Play(Sfx.Buzz); return; }
        BeginSpell(spell);
    }

    /// <summary>Starts casting: every spell asks whom, or where.</summary>
    public void BeginSpell(Spell spell)
    {
        Casting = spell;
        FirstTarget = null;
        Caster ??= Selected;
        Lit = Spells.Targets(Board, Turn, spell, Caster!);
        SetPhase(Phase.Target);
        Play(Sfx.Select);
    }

    private void TargetControls()
    {
        var pad = PadFor(Turn);
        MoveCursor(pad);
        if (pad.AltEdge)
        {
            FirstTarget = null;
            Lit = Board.Moves(Selected!);
            SetPhase(Phase.Spellbook);
            Play(Sfx.Cancel);
            return;
        }
        if (!pad.FireEdge) return;
        var (x, y) = Cursor;
        if (!Lit.Contains((x, y))) { Play(Sfx.Buzz); return; }
        Target(x, y);
    }

    /// <summary>A target chosen for the spell being cast.</summary>
    public void Target(int x, int y) => Cast(Casting!.Value, Caster!, Board.At(x, y), x, y);

    /// <summary>For tests: an adept of the side to move opens its spellbook and starts a spell.</summary>
    internal void BeginSpellFor(Side side, Spell spell, Piece? adept = null)
    {
        if (side != Turn) throw new InvalidOperationException($"It's {Turn}'s turn");
        Selected = Caster = adept ?? Board.Adepts(side).First();
        BeginSpell(spell);
    }

    /// <summary>For tests: sends the picked piece to a square it can reach.</summary>
    internal void MoveSelected(int x, int y)
    {
        if (Selected is null || !Lit.Contains((x, y))) throw new InvalidOperationException($"Can't move to {x},{y}");
        BeginMove(Selected, x, y);
    }

    internal void SetQuiet(int turns) => QuietTurns = turns;

    // ───────────────────────────── the computer's turn ─────────────────────────────

    private void ComputerTurn()
    {
        var ai = Ai(Turn) ?? new Computer(Difficulty.Normal, _rng);
        double pace = State == GameState.Attract ? 0.8 : 1.0;
        if (_plan is null)
        {
            if (PhaseTime < 0.45 * pace) return;
            _plan = ai.Choose(Board, Turn, QuietTurns);
            var who = _plan switch
            {
                MovePlan m => m.Piece,
                SpellPlan sp => sp.Caster,
                _ => null,
            };
            if (who is not null)
            {
                Cursors[(int)Turn] = (who.X, who.Y);
                Selected = who;
                Lit = _plan is MovePlan ? Board.Moves(who) : [];
                Play(Sfx.Select);
            }
            PhaseTime = 0;
            return;
        }
        if (PhaseTime < 0.8 * pace) return;
        switch (_plan)
        {
            case MovePlan m:
                Cursors[(int)Turn] = (m.X, m.Y);
                BeginMove(m.Piece, m.X, m.Y);
                break;
            case SpellPlan s:
                CastPlan(s);
                break;
        }
    }

    private void CastPlan(SpellPlan s)
    {
        Casting = s.Spell;
        Caster = s.Caster;
        Cast(s.Spell, s.Caster, s.Target, s.X, s.Y);
    }

    // ───────────────────────────── moving ─────────────────────────────

    private void BeginMove(Piece p, int x, int y)
    {
        Mover = p;
        MoveFrom = (p.X, p.Y);
        MoveTo = (x, y);
        double d = Math.Sqrt((x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y));
        MoveSeconds = 0.15 + d * (p.Type.Flies ? 0.11 : 0.14);
        Lit = [];
        SetPhase(Phase.Moving);
        Play(p.Type.Flies ? Sfx.Fly : Sfx.Step);
    }

    /// <summary>Where the moving piece is drawn now, in squares.</summary>
    public (double X, double Y) MoverAt
    {
        get
        {
            double t = Math.Clamp(PhaseTime / Math.Max(0.01, MoveSeconds), 0, 1);
            t = t * t * (3 - 2 * t);
            return (MoveFrom.X + (MoveTo.X - MoveFrom.X) * t, MoveFrom.Y + (MoveTo.Y - MoveFrom.Y) * t);
        }
    }

    private void Arrive()
    {
        var p = Mover!;
        var (x, y) = MoveTo;
        var there = Board.At(x, y);
        if (there is not null && there.Side != p.Side)
        {
            Challenge(p, p.Side, there);
            return;
        }
        p.X = x;
        p.Y = y;
        Mover = null;
        if (Board.IsPowerPoint(x, y)) Play(Sfx.PowerPoint);
        EndTurn();
    }

    // ───────────────────────────── spells ─────────────────────────────

    /// <summary>
    /// Casts a spell (already chosen and targeted), paying its mana. Most end the turn; an elemental called onto
    /// an enemy, or a demon, leads to a fight.
    /// </summary>
    private void Cast(Spell spell, Piece caster, Piece? target, int x, int y)
    {
        Board.Mana[(int)Turn] -= Spells.Cost(spell);
        string who = caster.Name;
        Casting = spell;
        Caster = caster;
        Lit = [];
        _afterCast = null;
        switch (spell)
        {
            case Spell.Summon:
            {
                var element = Board.ElementAt(caster.X, caster.Y);
                if (element == Element.Void) element = Element.Earth;
                var kind = PieceType.ElementalOf(element);
                if (target is not null && target.Side != Turn)
                {
                    // Called straight into battle: it stands off the board until it wins the square.
                    var e = Board.Add(kind, -10, -10, Turn);
                    Say($"The {who} calls a {e.Name} against the {target.Name}");
                    _afterCast = () => { Selected = null; Challenge(e, Turn, target); };
                }
                else
                {
                    var e = Board.Add(kind, x, y, Turn);
                    Say($"The {who} calls a {e.Name}");
                    target = e;
                }
                break;
            }
            case Spell.Demon:
            {
                var d = Board.Add(Spells.DemonFor(Turn, target!), -10, -10, Turn);
                Say($"The {who} calls the {d.Name} against the {target!.Name}");
                _afterCast = () => { Selected = null; Challenge(d, Turn, target); };
                break;
            }
            case Spell.Heal:
                target!.Life = target.Type.Life;
                Say($"The {who} heals the {target.Name}");
                break;
            case Spell.Weaken:
                target!.Life = Math.Max(1, target.Life / 2);
                Say($"The {who} weakens the {target.Name}");
                break;
            case Spell.Imprison:
                target!.Imprisoned = Spells.ImprisonTurns;
                Say($"The {who} imprisons the {target.Name}");
                break;
            case Spell.Banish:
                target!.Alive = false;
                Say($"The {who} banishes the {target.Name}");
                break;
        }
        SpellTarget = target;
        SetPhase(Phase.Cast);
        Play(Sfx.Spell);
    }

    /// <summary>The piece the last spell touched, for the sparkle.</summary>
    public Piece? SpellTarget { get; private set; }

    // ───────────────────────────── fights ─────────────────────────────

    private void Challenge(Piece? attacker, Side side, Piece defender)
    {
        Attacker = attacker;
        AttackerSide = side;
        Defender = defender;
        Battleground = (defender.X, defender.Y);
        Say($"The {attacker?.Name} challenges the {defender.Name}", 1.6);
        SetPhase(Phase.Clash);
        Play(Sfx.Clash);
    }

    private void OpenArena()
    {
        var (x, y) = Battleground;
        var attacker = Fight.Champion(Attacker!, Board.Bonus(Attacker!.Type, x, y));
        var defender = Fight.Champion(Defender!, Board.Bonus(Defender!.Type, x, y));
        var order = AttackerSide == Side.Order ? attacker : defender;
        var chaos = AttackerSide == Side.Order ? defender : attacker;
        Fight = new Fight(order, chaos, (int)Board.ElementAt(x, y), (x, y), _rng, Play);
        // Each computer fights at its own skill; an elemental at its caller's (or the game's).
        Difficulty Skill(Side s) => Ai(s)?.Difficulty ?? (State == GameState.Attract ? Difficulty.Normal : Difficulty);
        _orderBrain = !Human(Side.Order) || order.Computer ? new Brain(Skill(Side.Order), _rng) : null;
        _chaosBrain = !Human(Side.Chaos) || chaos.Computer ? new Brain(Skill(Side.Chaos), _rng) : null;
        order.Computer = _orderBrain is not null;
        chaos.Computer = _chaosBrain is not null;
        SetPhase(Phase.Fight);
    }

    private void RunFight(double dt)
    {
        var f = Fight!;
        var order = _orderBrain?.Think(f, f.Order, dt) ?? PadFor(Side.Order).Now;
        var chaos = _chaosBrain?.Think(f, f.Chaos, dt) ?? PadFor(Side.Chaos).Now;
        f.Update(dt, order, chaos);
        if (!f.Over) return;
        Outcome = f.Order.Dead && f.Chaos.Dead ? "Both fall!" : $"The {(f.Order.Dead ? f.Chaos : f.Order).Type.Name} wins";
        SetPhase(Phase.Aftermath);
    }

    /// <summary>The fight's result on the board: the fallen leave it, the survivor keeps its wounds, and a winning attacker takes the square.</summary>
    private void SettleFight()
    {
        var f = Fight!;
        foreach (var fighter in new[] { f.Order, f.Chaos })
        {
            if (fighter.Piece is not { } p) continue;
            if (fighter.Dead) p.Alive = false;
            else p.Life = Math.Clamp((int)Math.Ceiling(fighter.Life) - fighter.Bonus, 1, p.Type.Life);
        }
        if (Attacker is { Alive: true } a && !Defender!.Alive)
        {
            (a.X, a.Y) = Battleground;
            if (Board.IsPowerPoint(a.X, a.Y)) Play(Sfx.PowerPoint);
        }
        QuietTurns = -1;
        EndTurn();
    }

    // ───────────────────────────── the end of a turn ─────────────────────────────

    private void EndTurn()
    {
        Board.EndTurn(Turn);
        QuietTurns++;
        ToBoard();
        if (CheckEnd()) return;
        Turn = Board.Other(Turn);
        if (Turn == Side.Order) Round++;
        // A side that can do nothing at all (everything imprisoned, no spells) passes.
        if (!Board.Living(Turn).Any(p => Board.Moves(p).Count > 0) && !Spells.CanCastAny(Board, Turn))
        {
            if (++_passes >= 2) { GameOver(null, "Neither side can move: the war ends in a truce"); return; }
            Say($"{Turn} can't move, and passes");
            EndTurn();
            return;
        }
        _passes = 0;
    }

    private int _passes;

    private bool CheckEnd()
    {
        if (Board.HoldsAllPowerPoints() is { } holder)
        {
            GameOver(holder, $"{holder} holds all six power points");
            return true;
        }
        bool orderLeft = Board.Adepts(Side.Order).Any(), chaosLeft = Board.Adepts(Side.Chaos).Any();
        if (!orderLeft || !chaosLeft)
        {
            Side? w = orderLeft ? Side.Order : chaosLeft ? Side.Chaos : null;
            GameOver(w, w is { } s ? $"{s} has destroyed every enemy adept" : "Every adept is destroyed");
            return true;
        }
        if (QuietTurns >= QuietTurnsForDraw)
        {
            GameOver(null, $"{QuietTurnsForDraw} turns without a fight: the war ends in a truce");
            return true;
        }
        return false;
    }
}
