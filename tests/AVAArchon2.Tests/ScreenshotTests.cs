using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAArchon2.Engine;
using AVAArchon2.Views;
using Controls = AVAArchon2.Engine.Controls;

namespace AVAArchon2.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set ARCHON_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.Advance(0.016);
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("ARCHON_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAArchon2-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private static void Ticks(GameEngine g, int n)
    {
        for (int i = 0; i < n; i++) g.Tick(default, default);
    }

    /// <summary>Sends a piece at an enemy (placing it within reach first), and runs to the arena.</summary>
    private static Fight Duel(GameEngine g, Piece attacker, Piece defender, int fromX, int fromY)
    {
        attacker.X = fromX;
        attacker.Y = fromY;
        g.Pick(attacker);
        g.MoveSelected(defender.X, defender.Y);
        for (int i = 0; i < 600 && g.Phase != Phase.Fight; i++) g.Tick(default, default);
        Assert.Equal(Phase.Fight, g.Phase);
        return g.Fight!;
    }

    /// <summary>Lets two computer brains fight it out until the moment is right.</summary>
    private static void Brawl(GameEngine g, Fight f, Func<Fight, bool> moment, int seed)
    {
        var rng = new Random(seed);
        var a = new Brain(Difficulty.Hard, rng);
        var b = new Brain(Difficulty.Normal, rng);
        for (int i = 0; i < 60 * 30 && g.Phase == Phase.Fight; i++)
        {
            g.Tick(a.Think(f, f.Order, GameEngine.Step), b.Think(f, f.Chaos, GameEngine.Step));
            if (f.Time > Fight.Ready + 1 && moment(f)) return;
        }
    }

    [AvaloniaFact]
    public void The_title()
    {
        var (w, c, g) = Open();
        g.Tick(default, default);
        c.Advance(1.3);
        Shot(w, c, "title");
    }

    [AvaloniaFact]
    public void The_board()
    {
        var (w, c, g) = Open(5);
        g.Players = Players.Two;
        g.StartGame();
        Ticks(g, 200);
        // An adept picked up: where it can go lit.
        g.Pick(g.Board.At(0, 3)!);
        g.Cursors[0] = (2, 4);
        Shot(w, c, "board");

        // The spellbook.
        var (w2, c2, g2) = Open(6);
        g2.Players = Players.Two;
        g2.StartGame();
        Ticks(g2, 200);
        g2.Board.Mana[0] = 140;
        g2.Pick(g2.Board.At(0, 5)!);
        g2.Cursors[0] = (0, 5);
        g2.Tick(new Controls(0, 0, true), default);
        g2.Tick(default, default);
        Shot(w2, c2, "spellbook");
    }

    [AvaloniaFact]
    public void A_war_in_progress()
    {
        var (w, c, g) = Open(9);
        g.StartComputerGame(Difficulty.Normal, Difficulty.Normal);
        // Play on until the war is well under way, then stop on a quiet board.
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.Round < 14; i++) g.Tick(default, default);
        for (int i = 0; i < 60 * 30 && g.Phase != Phase.Board; i++) g.Tick(default, default);
        Ticks(g, 20);
        g.Cursors[(int)g.Turn] = (4, 4);
        Shot(w, c, "war");
        // On to the end.
        for (int i = 0; i < 60 * 60 * 30 && g.State == GameState.Playing; i++) g.Tick(default, default);
        Assert.Equal(GameState.Over, g.State);
        Ticks(g, 60 * 4);
        Shot(w, c, "victory");
    }

    [AvaloniaFact]
    public void Fights_in_the_arena()
    {
        // An elemental called against an adept.
        var (w, c, g) = Open(21);
        g.Players = Players.Two;
        g.StartGame();
        var b = g.Board;
        var foe = b.At(8, 3)!;
        (foe.X, foe.Y) = (2, 2);
        g.BeginSpellFor(Side.Order, Spell.Summon, b.At(0, 3));
        g.Target(2, 2);
        for (int i = 0; i < 600 && g.Phase != Phase.Clash; i++) g.Tick(default, default);
        Ticks(g, 30);
        Shot(w, c, "challenge");
        for (int i = 0; i < 600 && g.Phase != Phase.Fight; i++) g.Tick(default, default);
        var f = g.Fight!;
        Brawl(g, f, x => x.Order.Striking is > 0.18 and < 0.3, 3);
        f.Order.Hurt = f.Chaos.Hurt = 0;
        Shot(w, c, "fire");

        // A demon: the Behemoth's brimstone against an earth elemental's boulders.
        var (w2, c2, g2) = Open(22);
        g2.Players = Players.Two;
        g2.StartGame();
        var earth = g2.Board.Add(Kind.EarthElemental, 2, 4, Side.Order);
        earth.Life = earth.Type.Life;
        var behemoth = g2.Board.Add(Kind.Behemoth, 5, 4, Side.Chaos);
        var f2 = Duel(g2, earth, behemoth, 3, 4);
        Brawl(g2, f2, x => x.Missiles.Count(m => m.Owner == x.Order) > 0 && x.Missiles.Count(m => m.Owner == x.Chaos) > 0, 5);
        Shot(w2, c2, "demon");

        // The air elemental's whirlwinds against a seraph's holy fire.
        var (w3, c3, g3) = Open(23);
        g3.Players = Players.Two;
        g3.StartGame();
        var seraph = g3.Board.Add(Kind.Seraph, 2, 2, Side.Order);
        var air = g3.Board.Add(Kind.AirElemental, 6, 6, Side.Chaos);
        var f3 = Duel(g3, seraph, air, 4, 4);
        Brawl(g3, f3, x => x.Missiles.Count(m => m.Owner == x.Order) >= 2 && x.Missiles.Any(m => m.Owner == x.Chaos), 7);
        Shot(w3, c3, "seraph");
    }
}
