using AVAArchon2.Engine;
using AVAArchon2.Views;

namespace AVAArchon2.Tests;

public sealed class GameplayTests
{
    private static readonly Controls Fire = new(0, 0, true);
    private static readonly Controls Back = new(0, 0, false, true);

    /// <summary>A game with people on both sides, at the start.</summary>
    private static GameEngine TwoPlayers(int seed = 7)
    {
        var g = new GameEngine(seed) { Silent = true, Players = Players.Two };
        g.StartGame();
        return g;
    }

    /// <summary>Taps a control for one step, then lets go.</summary>
    private static void Tap(GameEngine g, Controls c, bool chaos = false)
    {
        if (chaos) g.Tick(default, c); else g.Tick(c, default);
        g.Tick(default, default);
    }

    /// <summary>Walks a side's cursor to a square, a tap at a time.</summary>
    private static void CursorTo(GameEngine g, int x, int y, bool chaos = false)
    {
        for (int i = 0; i < 20 && g.Cursor != (x, y); i++)
        {
            var (cx, cy) = g.Cursor;
            Tap(g, new Controls(Math.Sign(x - cx), Math.Sign(y - cy)), chaos);
        }
        Assert.Equal((x, y), g.Cursor);
    }

    private static void Until(GameEngine g, Func<bool> done, int limit = 60 * 30)
    {
        for (int i = 0; i < limit && !done(); i++) g.Tick(default, default);
        Assert.True(done());
    }

    [Fact]
    public void Four_adepts_a_side_on_the_home_edges_and_the_rings_of_the_elements()
    {
        var b = Board.Start();
        Assert.Equal(4, b.Adepts(Side.Order).Count());
        Assert.Equal(4, b.Adepts(Side.Chaos).Count());
        Assert.All(b.Living(Side.Order), p => Assert.Equal((0, Kind.Magus), (p.X, p.Kind)));
        Assert.All(b.Living(Side.Chaos), p => Assert.Equal((8, Kind.Sorcerer), (p.X, p.Kind)));
        Assert.Equal(Element.Fire, Board.ElementAt(0, 3));
        Assert.Equal(Element.Water, Board.ElementAt(1, 4));
        Assert.Equal(Element.Air, Board.ElementAt(6, 5));
        Assert.Equal(Element.Earth, Board.ElementAt(3, 5));
        Assert.Equal(Element.Void, Board.ElementAt(4, 4));
        Assert.Equal(6, Board.PowerPoints.Length);
        Assert.All(Board.PowerPoints, pp => Assert.Null(b.At(pp.X, pp.Y)));
        Assert.Equal([Board.Mana0, Board.Mana0], b.Mana);
    }

    [Fact]
    public void An_elemental_is_strongest_on_its_own_ring_and_an_adept_on_a_power_point()
    {
        var b = new Board();
        var fire = PieceType.All[Kind.FireElemental];
        Assert.Equal(Board.ElementBonus, b.Bonus(fire, 0, 0));
        Assert.Equal(0, b.Bonus(fire, 4, 4));
        Assert.Equal(Board.ElementBonus, b.Bonus(PieceType.All[Kind.EarthElemental], 3, 4));
        Assert.Equal(4, b.Bonus(PieceType.All[Kind.Magus], 4, 0));
        Assert.Equal(0, b.Bonus(PieceType.All[Kind.Magus], 3, 0));
        Assert.Equal(0, b.Bonus(PieceType.All[Kind.Seraph], 0, 0));
    }

    [Fact]
    public void Mana_grows_each_turn_and_faster_for_each_power_point_held()
    {
        var b = Board.Start();
        b.EndTurn(Side.Order);
        Assert.Equal(Board.Mana0 + Board.ManaIncome, b.ManaOf(Side.Order));
        b.At(0, 1)!.X = 4;
        b.Pieces[0].Y = 0;
        b.Pieces.First(p => p.Side == Side.Order && p.Y == 3).X = 1;
        b.Pieces.First(p => p.Side == Side.Order && p.X == 1).Y = 4;
        Assert.Equal(2, b.PointsHeld(Side.Order));
        b.EndTurn(Side.Order);
        Assert.Equal(Board.Mana0 + 2 * Board.ManaIncome + 2 * Board.ManaPerPoint, b.ManaOf(Side.Order));
        for (int i = 0; i < 100; i++) b.EndTurn(Side.Order);
        Assert.Equal(Board.MaxMana, b.ManaOf(Side.Order));
    }

    [Fact]
    public void Walkers_go_round_pieces_while_fliers_pass_over_them()
    {
        var b = new Board();
        var water = b.Add(Kind.WaterElemental, 4, 4, Side.Order);
        var air = b.Add(Kind.AirElemental, 4, 6, Side.Order);
        for (int y = 3; y <= 5; y++) b.Add(Kind.Magus, 5, y);
        // The walker must go round the wall of adepts: three steps get it to (6, 3), not straight through to (6, 4).
        Assert.Contains((6, 3), b.Moves(water));
        Assert.DoesNotContain((6, 4), b.Moves(water));
        Assert.Contains((8, 6), b.Moves(air));
        var earth = b.Add(Kind.EarthElemental, 0, 0, Side.Chaos);
        Assert.DoesNotContain((3, 0), b.Moves(earth));
        Assert.Contains((2, 0), b.Moves(earth));
    }

    [Fact]
    public void An_adept_summons_the_elemental_of_its_ring_onto_a_square_near_it()
    {
        var g = TwoPlayers();
        var magus = g.Board.At(0, 3)!;
        g.BeginSpellFor(Side.Order, Spell.Summon, magus);
        Assert.Contains((2, 3), g.Lit);
        Assert.DoesNotContain((3, 3), g.Lit);
        g.Target(2, 3);
        Until(g, () => g.Turn == Side.Chaos);
        var e = g.Board.At(2, 3)!;
        Assert.Equal(Kind.FireElemental, e.Kind);
        Assert.Equal(Side.Order, e.Side);
        Assert.Equal(Board.Mana0 - Spells.Cost(Spell.Summon) + Board.ManaIncome, g.Board.ManaOf(Side.Order));
    }

    [Fact]
    public void Summoned_onto_an_enemy_the_elemental_fights_for_the_square()
    {
        var g = TwoPlayers();
        var magus = g.Board.At(0, 3)!;
        var foe = g.Board.At(8, 1)!;
        foe.X = 2; foe.Y = 2;
        g.BeginSpellFor(Side.Order, Spell.Summon, magus);
        Assert.Contains((2, 2), g.Lit);
        g.Target(2, 2);
        Until(g, () => g.Phase == Phase.Fight);
        Assert.Equal(Kind.FireElemental, g.Fight!.Order.Type.Kind);
        Assert.Same(foe, g.Fight.Chaos.Piece);
        g.Fight.Chaos.Life = 0;
        Until(g, () => g.Turn == Side.Chaos, 60 * 60);
        Assert.False(foe.Alive);
        Assert.Equal(Kind.FireElemental, g.Board.At(2, 2)!.Kind);
    }

    [Fact]
    public void A_demon_strikes_an_elemental_anywhere_on_the_board_but_never_an_adept()
    {
        var g = TwoPlayers();
        g.Board.Mana[0] = 100;
        var foe = g.Board.Add(Kind.EarthElemental, 8, 8, Side.Chaos);
        g.BeginSpellFor(Side.Order, Spell.Demon);
        Assert.Contains((8, 8), g.Lit);
        Assert.DoesNotContain((8, 7), g.Lit);
        g.Target(8, 8);
        Until(g, () => g.Phase == Phase.Fight);
        Assert.True(g.Fight!.Order.Type.Demon);
        Assert.Equal(Side.Order, g.Fight.Order.Side);
        Assert.Same(foe, g.Fight.Chaos.Piece);
        Assert.Equal(100 - Spells.Cost(Spell.Demon), g.Board.ManaOf(Side.Order));
    }

    [Fact]
    public void Heal_weaken_imprison_and_banish_but_adepts_are_never_imprisoned()
    {
        var g = TwoPlayers();
        var b = g.Board;
        b.Mana[0] = b.Mana[1] = 200;
        var magus = b.At(0, 1)!;
        magus.Life = 3;
        g.BeginSpellFor(Side.Order, Spell.Heal, b.At(0, 3));
        g.Target(0, 1);
        Until(g, () => g.Turn == Side.Chaos);
        Assert.Equal(magus.Type.Life, magus.Life);

        g.BeginSpellFor(Side.Chaos, Spell.Weaken);
        g.Target(0, 1);
        Until(g, () => g.Turn == Side.Order);
        Assert.Equal(magus.Type.Life / 2, magus.Life);
        // Weakened, it can't be weakened again.
        Assert.DoesNotContain((0, 1), Spells.Targets(b, Side.Chaos, Spell.Weaken, b.At(8, 1)!));

        var fiend = b.Add(Kind.Fiend, 5, 5, Side.Chaos);
        g.BeginSpellFor(Side.Order, Spell.Imprison);
        Assert.DoesNotContain((8, 1), g.Lit);
        g.Target(5, 5);
        Until(g, () => g.Turn == Side.Chaos);
        Assert.Equal(Spells.ImprisonTurns, fiend.Imprisoned);
        Assert.Empty(b.Moves(fiend));

        var elemental = b.Add(Kind.AirElemental, 3, 3, Side.Order);
        g.BeginSpellFor(Side.Chaos, Spell.Banish);
        Assert.DoesNotContain((0, 1), g.Lit);
        g.Target(3, 3);
        Until(g, () => g.Turn == Side.Order);
        Assert.False(elemental.Alive);
    }

    [Fact]
    public void Spells_cost_mana_and_never_touch_a_power_point()
    {
        var b = Board.Start();
        var magus = b.At(0, 1)!;
        b.Add(Kind.WaterElemental, 5, 5, Side.Chaos);
        b.Mana[0] = Spells.Cost(Spell.Demon) - 1;
        Assert.False(Spells.CanCast(b, Side.Order, Spell.Demon, magus));
        b.Mana[0] = 200;
        Assert.True(Spells.CanCast(b, Side.Order, Spell.Demon, magus));
        var sorcerer = b.At(8, 1)!;
        (sorcerer.X, sorcerer.Y) = (4, 0);
        b.Add(Kind.Fiend, 4, 8, Side.Chaos);
        Assert.DoesNotContain((4, 8), Spells.Targets(b, Side.Order, Spell.Demon, magus));
        Assert.DoesNotContain((4, 0), Spells.Targets(b, Side.Order, Spell.Weaken, magus));
        magus.Imprisoned = 2;
        Assert.False(Spells.CanCastAny(b, Side.Order, magus));
        Assert.Equal(20, Spells.Cost(Spell.Imprison));
    }

    [Fact]
    public void Moving_onto_an_enemy_starts_a_fight_and_the_winner_holds_the_square()
    {
        var g = TwoPlayers();
        var magus = g.Board.At(0, 3)!;
        var sorcerer = g.Board.At(8, 3)!;
        (sorcerer.X, sorcerer.Y) = (2, 3);
        g.Pick(magus);
        g.MoveSelected(2, 3);
        Until(g, () => g.Phase == Phase.Fight);
        var f = g.Fight!;
        Assert.Same(magus, f.Order.Piece);
        Assert.Same(sorcerer, f.Chaos.Piece);
        f.Chaos.Life = 0;
        Until(g, () => g.Turn == Side.Chaos, 60 * 60);
        Assert.False(sorcerer.Alive);
        Assert.Equal((2, 3), (magus.X, magus.Y));
    }

    [Fact]
    public void A_blow_lands_only_within_reach_and_a_solid_pillar_stops_missiles()
    {
        var rng = new Random(3);
        var water = new Piece { Id = 0, Type = PieceType.All[Kind.WaterElemental], Side = Side.Order, Life = 30 };
        var earth = new Piece { Id = 1, Type = PieceType.All[Kind.EarthElemental], Side = Side.Chaos, Life = 30 };
        var f = new Fight(Fight.Champion(water, 0), Fight.Champion(earth, 0), 4, (4, 4), rng, _ => { });
        f.Pillars.Clear();
        f.Update(Fight.Ready + 0.01, default, default);
        f.Order.X = 300; f.Order.Y = 270;
        f.Chaos.X = 300 + 2 * Fighter.Radius + 40; f.Chaos.Y = 270;
        f.Update(GameEngine.Step, new Controls(1, 0, true), default);
        for (int i = 0; i < 15; i++) f.Update(GameEngine.Step, default, default);
        Assert.Equal(30 - 12, f.Chaos.Life);
        f.Chaos.X = 700;
        for (int i = 0; i < 60; i++) f.Update(GameEngine.Step, new Controls(0, 0, true), default);
        Assert.Equal(30 - 12, f.Chaos.Life);
        f.Pillars.Add(new Pillar { X = 500, Y = 270, Phase = Math.PI / 2, Rate = 0 });
        double life = f.Order.Life;
        for (int i = 0; i < 180; i++) f.Update(GameEngine.Step, default, new Controls(-1, 0, true));
        Assert.Equal(life, f.Order.Life);
        f.Pillars.Clear();
        for (int i = 0; i < 180; i++) f.Update(GameEngine.Step, default, new Controls(0, 0, true));
        Assert.True(f.Order.Life < life);
    }

    [Fact]
    public void A_ring_of_fire_burns_everything_near_but_roots_its_maker()
    {
        var fire = new Piece { Id = 0, Type = PieceType.All[Kind.FireElemental], Side = Side.Order, Life = 20 };
        var sorcerer = new Piece { Id = 1, Type = PieceType.All[Kind.Sorcerer], Side = Side.Chaos, Life = 10 };
        var f = new Fight(Fight.Champion(fire, 0), Fight.Champion(sorcerer, 0), 4, (4, 4), new Random(1), _ => { });
        f.Update(Fight.Ready + 0.01, default, default);
        f.Order.X = 400; f.Order.Y = 270;
        f.Chaos.X = 400 + 120; f.Chaos.Y = 270;
        f.Update(GameEngine.Step, new Controls(0, 0, true), default);
        double x = f.Order.X;
        for (int i = 0; i < 20; i++) f.Update(GameEngine.Step, new Controls(1, 0), default);
        Assert.Equal(x, f.Order.X);
        Assert.True(f.Chaos.Life <= 0);
        Assert.True(f.Over);
    }

    [Fact]
    public void Wounds_last_but_heal_a_little_each_turn_and_fast_on_a_power_point()
    {
        var b = Board.Start();
        var a = b.At(0, 1)!;
        a.Life = 4;
        var c = b.At(0, 3)!;
        (c.X, c.Y) = (1, 4);
        c.Life = 6;
        b.EndTurn(Side.Order);
        Assert.Equal(5, a.Life);
        Assert.Equal(6 + Board.PowerPointHealing, c.Life);
    }

    [Fact]
    public void Holding_all_six_power_points_wins_and_so_does_destroying_every_enemy_adept()
    {
        var g = TwoPlayers();
        var b = g.Board;
        foreach (var (x, y) in Board.PowerPoints) b.Add(Kind.EarthElemental, x, y, Side.Order);
        var magus = b.At(0, 1)!;
        g.Pick(magus);
        g.MoveSelected(1, 1);
        Until(g, () => g.State == GameState.Over);
        Assert.Equal(Side.Order, g.Winner);

        var h = TwoPlayers(9);
        foreach (var s in h.Board.Adepts(Side.Chaos).ToList()) s.Alive = false;
        h.Board.Add(Kind.Fiend, 8, 8, Side.Chaos);
        h.Pick(h.Board.At(0, 1)!);
        h.MoveSelected(1, 1);
        Until(h, () => h.State == GameState.Over);
        Assert.Equal(Side.Order, h.Winner);
    }

    [Fact]
    public void The_computer_takes_a_fight_it_will_win_and_shuns_one_it_will_lose()
    {
        var b = Board.Start();
        b.Mana[0] = 0;
        var magus = b.At(0, 3)!;
        var sorcerer = b.At(8, 3)!;
        (sorcerer.X, sorcerer.Y) = (2, 3);
        sorcerer.Life = 2;
        var plan = new Computer(Difficulty.Hard, new Random(1)).Choose(b, Side.Order);
        var m = Assert.IsType<MovePlan>(plan);
        Assert.Equal((2, 3), (m.X, m.Y));

        var c = new Board();
        c.Mana[0] = 0;
        c.Add(Kind.Magus, 6, 6);
        c.Add(Kind.Behemoth, 7, 7, Side.Chaos);
        c.Add(Kind.Sorcerer, 8, 0);
        var plan2 = new Computer(Difficulty.Hard, new Random(1)).Choose(c, Side.Order);
        Assert.False(plan2 is MovePlan { X: 7, Y: 7 });
    }

    [Fact]
    public void A_hard_fighter_beats_an_easy_one_with_the_same_pieces()
    {
        int hardWins = 0;
        for (int s = 0; s < 24; s++)
        {
            var rng = new Random(s);
            var a = new Piece { Id = 0, Type = PieceType.All[Kind.Magus], Side = Side.Order, Life = 14 };
            var b = new Piece { Id = 1, Type = PieceType.All[Kind.Sorcerer], Side = Side.Chaos, Life = 14 };
            bool hardIsOrder = s % 2 == 0;
            var f = new Fight(Fight.Champion(a, 0), Fight.Champion(b, 0), 4, (4, 4), rng, _ => { });
            var order = new Brain(hardIsOrder ? Difficulty.Hard : Difficulty.Easy, rng);
            var chaos = new Brain(hardIsOrder ? Difficulty.Easy : Difficulty.Hard, rng);
            for (int i = 0; i < 60 * 120 && !f.Over; i++)
                f.Update(GameEngine.Step, order.Think(f, f.Order, GameEngine.Step), chaos.Think(f, f.Chaos, GameEngine.Step));
            var hard = hardIsOrder ? f.Order : f.Chaos;
            if (!hard.Dead) hardWins++;
        }
        Assert.True(hardWins >= 15, $"hard won {hardWins} of 24");
    }

    [Fact]
    public void Computer_generals_fight_a_whole_war_to_a_finish()
    {
        int decided = 0;
        for (int seed = 1; seed <= 6; seed++)
        {
            var g = new GameEngine(seed) { Silent = true };
            g.StartComputerGame();
            for (int i = 0; i < 60 * 60 * 30 && g.State == GameState.Playing; i++) g.Tick(default, default);
            Assert.Equal(GameState.Over, g.State);
            if (g.Winner is not null) decided++;
        }
        Assert.True(decided >= 5);
    }

    [Fact]
    public void A_hard_general_beats_an_easy_one()
    {
        int hard = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var g = new GameEngine(seed) { Silent = true };
            bool hardOrder = seed % 2 == 0;
            g.StartComputerGame(hardOrder ? Difficulty.Hard : Difficulty.Easy, hardOrder ? Difficulty.Easy : Difficulty.Hard);
            for (int i = 0; i < 60 * 60 * 30 && g.State == GameState.Playing; i++) g.Tick(default, default);
            if (g.Winner == (hardOrder ? Side.Order : Side.Chaos)) hard++;
        }
        Assert.True(hard >= 6, $"hard won {hard} of 8");
    }

    [Fact]
    public void The_title_sets_up_the_game_and_idles_into_a_demonstration()
    {
        var g = new GameEngine(1) { Silent = true };
        Assert.Equal(GameState.Title, g.State);
        Assert.Equal("begin", g.TitleRows[g.TitleRow]);
        // Up to the skill row, harder; up to the side row, Chaos.
        Tap(g, new Controls(0, -1));
        Tap(g, new Controls(1, 0));
        Assert.Equal(Difficulty.Hard, g.Difficulty);
        Tap(g, new Controls(0, -1));
        Tap(g, new Controls(1, 0));
        Assert.Equal(Side.Chaos, g.HumanSide);
        g.Tick(default, default, start: true);
        Assert.Equal(GameState.Playing, g.State);
        Assert.True(g.IsComputer(Side.Order));
        Assert.False(g.IsComputer(Side.Chaos));
        // The computer (Order) moves by itself.
        Until(g, () => g.Turn == Side.Chaos, 60 * 60);

        var idle = new GameEngine(2) { Silent = true };
        idle.Idle((int)(GameEngine.TitleIdleSeconds * 60) + 2);
        Assert.Equal(GameState.Attract, idle.State);
        idle.Idle(60 * 20);
        Assert.True(idle.Round > 1 || idle.Phase != Phase.Board);
        idle.Tick(default, default, start: true);
        Assert.Equal(GameState.Title, idle.State);
    }

    [Fact]
    public void Every_piece_has_a_picture_and_every_sound_a_file()
    {
        foreach (var k in Enum.GetValues<Kind>())
        {
            Assert.True(Sprites.All.ContainsKey(k), k.ToString());
            Assert.Equal(16, Sprites.All[k].Length);
            Assert.All(Sprites.All[k], row => Assert.Equal(16, row.Length));
            Assert.All(Sprites.All[k], row => Assert.All(row, ch => Assert.NotEqual(0xFFFF00FFu, Sprites.Colour(ch, 0xFF808080))));
        }
        var dir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(dir, s + ".wav")), s.ToString());
    }
}
