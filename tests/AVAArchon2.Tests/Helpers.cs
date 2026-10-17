using AVAArchon2.Engine;

namespace AVAArchon2.Tests;

internal static class Ext
{
    public static void Idle(this GameEngine g, int ticks)
    {
        for (int i = 0; i < ticks; i++) g.Tick(default, default);
    }

    /// <summary>Fights a whole arena battle between two computer brains; returns the fight.</summary>
    public static Fight Brawl(Kind order, Kind chaos, int seed, Difficulty skill = Difficulty.Normal, int orderBonus = 0, int chaosBonus = 0)
    {
        var rng = new Random(seed);
        // (A summoned elemental fights for whichever side called it.)
        var a = new Piece { Id = 0, Type = PieceType.All[order], Side = Side.Order, Life = PieceType.All[order].Life };
        var b = new Piece { Id = 1, Type = PieceType.All[chaos], Side = Side.Chaos, Life = PieceType.All[chaos].Life };
        var f = new Fight(Fight.Champion(a, orderBonus), Fight.Champion(b, chaosBonus), 3, (4, 4), rng, _ => { });
        var ba = new Brain(skill, rng);
        var bb = new Brain(skill, rng);
        for (int i = 0; i < 60 * 120 && !f.Over; i++)
            f.Update(GameEngine.Step, ba.Think(f, f.Order, GameEngine.Step), bb.Think(f, f.Chaos, GameEngine.Step));
        return f;
    }
}
