using AVAArchon2.Engine;

namespace AVAArchon2.Tests;

/// <summary>
/// The computer judges fights by a table of how often each piece beats each other one. The table is
/// measured here, by brawls between computer fighters; set ARCHON_ODDS=1 to measure it afresh and
/// rewrite src/AVAArchon2/Engine/OddsTable.cs (after changing any piece's numbers).
/// </summary>
public sealed class OddsTableTests
{
    private static readonly Kind[] Order = [Kind.Magus, Kind.FireElemental, Kind.WaterElemental, Kind.AirElemental, Kind.EarthElemental, Kind.Seraph, Kind.Colossus];
    private static readonly Kind[] Chaos = [Kind.Sorcerer, Kind.FireElemental, Kind.WaterElemental, Kind.AirElemental, Kind.EarthElemental, Kind.Fiend, Kind.Behemoth];

    [Fact]
    public void Every_pairing_of_order_and_chaos_has_odds()
    {
        foreach (var a in Order)
            foreach (var b in Chaos)
            {
                Assert.True(OddsTable.OrderWins.ContainsKey((a, b)), $"{a} vs {b}");
            }
    }

    [Fact]
    public void Measure()
    {
        if (Environment.GetEnvironmentVariable("ARCHON_ODDS") is not "1") return;
        const int fights = 64;
        var sb = new System.Text.StringBuilder();
        sb.Append("// Measured by OddsTableTests.Measure (ARCHON_ODDS=1): how often Order's piece beats Chaos's, at full life\n");
        sb.Append("// on neutral ground, from brawls between computer fighters. Regenerate after changing any piece.\n");
        sb.Append("namespace AVAArchon2.Engine;\n\ninternal static class OddsTable\n{\n");
        sb.Append("    public static readonly Dictionary<(Kind Order, Kind Chaos), double> OrderWins = new()\n    {\n");
        foreach (var a in Order)
            foreach (var b in Chaos)
            {
                double wins = 0;
                for (int s = 0; s < fights; s++)
                {
                    var f = Ext.Brawl(a, b, 1000 + s * 97 + (int)a * 13 + (int)b);
                    wins += f.Order.Dead == f.Chaos.Dead ? 0.5 : f.Chaos.Dead ? 1 : 0;
                }
                sb.Append($"        [(Kind.{a}, Kind.{b})] = {wins / fights:0.###},\n");
            }
        sb.Append("    };\n}\n");
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "AVAArchon2.slnx"))) root = Path.GetDirectoryName(root)!;
        File.WriteAllText(Path.Combine(root, "src", "AVAArchon2", "Engine", "OddsTable.cs"), sb.ToString());
    }
}
