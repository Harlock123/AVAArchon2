using AVAArchon2.Engine;

namespace AVAArchon2.Views;

/// <summary>
/// The pieces: 16×16, one letter per pixel. 'a' is the side's colour, 'b' a darker and 'c' a lighter
/// shade of it; other letters are fixed colours; '.' is clear. All original drawings.
/// </summary>
public static class Sprites
{
    public static readonly Dictionary<char, uint> Fixed = new()
    {
        ['k'] = 0xFF101018, ['w'] = 0xFFF2F0E8, ['s'] = 0xFFE8C0A0, ['g'] = 0xFFA8A8B4, ['G'] = 0xFF62626E,
        ['n'] = 0xFF7A5430, ['y'] = 0xFFFFD840, ['o'] = 0xFFFF8A20, ['r'] = 0xFFE03020, ['e'] = 0xFFFF3030,
        ['h'] = 0xFF7C8C44, ['v'] = 0xFF90F040, ['u'] = 0xFF3070D0, ['l'] = 0xFF90C0F0,
    };

    public static readonly Dictionary<Kind, string[]> All = new()
    {
        [Kind.Magus] =
        [
            "......a.........",
            ".....aaa........",
            "....aaaaa....y..",
            "...aaaaaaa..yyy.",
            "....kskskk...y..",
            "....ksssk....g..",
            "...kcccccck..g..",
            "..kccaaaccckkg..",
            "..kcaaaaacsssg..",
            "..kcaaaaack..g..",
            "..kcaaaaack..g..",
            "..kaaaaaaak..g..",
            ".kaaaaaaaaak.g..",
            ".kbbbbbbbbbk.g..",
            "..kkkkkkkkk..k..",
            "................",
        ],
        [Kind.Sorcerer] =
        [
            "................",
            ".....kkkkk......",
            "....kaaaaak.....",
            "...kaakkkaak....",
            "...kakeekeak....",
            "...kakkkkkak..v.",
            "....kaaaaak..vvv",
            "...kcaaaaack..v.",
            "..kcaabbbaacskk.",
            "..kcaabbbaack...",
            "..kcaabbbaack...",
            "..kcaaaaaaack...",
            ".kcaaaaaaaaack..",
            ".kbbbbbbbbbbbk..",
            "..kkkkkkkkkkk...",
            "................",
        ],
        [Kind.FireElemental] =
        [
            "................",
            ".......y........",
            "......yoy.......",
            ".....yoroy......",
            "....yorrroy.....",
            "...yorryrroy....",
            "..yorykkyrroy...",
            "..yorkwkwkroy...",
            "..yorrkkkrroy...",
            "...yorrrrroy....",
            "..yorroyorroy...",
            ".yor.yoy.yoroy..",
            ".yo..yoy..yoy...",
            ".y....y....y....",
            "................",
            "................",
        ],
        [Kind.WaterElemental] =
        [
            "................",
            "......llll......",
            "....lluuuull....",
            "...luuuuuuuul...",
            "..luukwuukwuul..",
            "..luukkuukkuul..",
            "..luuuuuuuuuul..",
            "...luuukkkuul...",
            "..lluuuuuuuull..",
            ".luuuluuuuluuul.",
            ".luulluuuullull.",
            "luulluuuuuulluul",
            "lulluuuuuuuuullu",
            ".lllllllllllll..",
            "................",
            "................",
        ],
        [Kind.AirElemental] =
        [
            "................",
            "...wwwwwwwwww...",
            "..wggggggggggw..",
            "...wwwwwwwwww...",
            "....gggggggg....",
            ".....wwkwkww....",
            "......gggg......",
            ".....wwwwww.....",
            "......gggg......",
            ".......ww.......",
            "......gg........",
            ".......ww.......",
            "........g.......",
            "................",
            "................",
            "................",
        ],
        [Kind.EarthElemental] =
        [
            "................",
            ".....nnnnnn.....",
            "....nGGGGGGn....",
            "....nGkGGkGn....",
            "....nGGGGGGn....",
            "..nnnnGGGGnnnn..",
            ".nGGGGnnnnGGGGn.",
            ".nGGnGGGGGGnGGn.",
            ".nGn.nGGGGn.nGn.",
            ".nn..nGGGGn..nn.",
            ".....nGGGGn.....",
            "....nGGnnGGn....",
            "....nGn..nGn....",
            "...nGGn..nGGn...",
            "...nnnn..nnnn...",
            "................",
        ],
        [Kind.Seraph] =
        [
            "......yyyy......",
            ".....y....y.....",
            "w.....ksk.....w.",
            "ww....sss....ww.",
            "www..kcck...www.",
            "wwww.kcack.wwww.",
            ".wwwwkcacckwwww.",
            "..wwwkaaackwww..",
            "...wwkaaackww...",
            ".....kaaack.....",
            ".....kaaack.....",
            "....kaaaaack....",
            "....kaaaaack....",
            "....kbbbbbbk....",
            ".....kkkkkk.....",
            "................",
        ],
        [Kind.Colossus] =
        [
            ".....kkkkkk.....",
            "....kGGGGGGk....",
            "....kGkyykGk....",
            "....kGGGGGGk....",
            "..kkkaaaaaakkk..",
            ".kGGkaaaaaakGGk.",
            ".kGGkaccccakGGk.",
            ".kGGkaaaaaakGGkG",
            ".kGGkaaaaaakGGkG",
            "..kkkaaaaaakkkGG",
            "....kbbbbbbk..GG",
            "....kGGkkGGk....",
            "....kGGk.kGGk...",
            "...kGGGk.kGGGk..",
            "...kkkkk.kkkkk..",
            "................",
        ],
        [Kind.Fiend] =
        [
            "..r..........r..",
            "..rr........rr..",
            "...rr.kkkk.rr...",
            "....rkaaaark....",
            ".....kyaayk.....",
            ".....kaaaak.....",
            "..k..kkrrkk..k..",
            ".kak.kaaaak.kak.",
            ".kaakkaaaakkaak.",
            "..kaaaaaaaaaak..",
            "...kaaaaaaaak...",
            ".....kaaaak.....",
            "....kaa..aak....",
            "...kaa....aak...",
            "..kkk......kkk..",
            "................",
        ],
        [Kind.Behemoth] =
        [
            "................",
            "..kkk...........",
            ".kaaak..........",
            "kaykaakkkkkkkk..",
            "kaaaaaaaaaaaaak.",
            ".kraaaaaaaaaaaak",
            "..kaaaaaaaaaaaak",
            "..kaaccccccaaaak",
            "..kaaaaaaaaaaak.",
            "...kbbbbbbbbbk..",
            "...kak.kak.kak..",
            "...kak.kak.kak..",
            "...kak.kak.kak..",
            "..kkkk.kkk.kkkk.",
            "................",
            "................",
        ],
    };

    public static uint Colour(char ch, uint tint) => ch switch
    {
        '.' => 0,
        'a' => tint,
        'b' => Shade(tint, 0.62),
        'c' => Shade(tint, 1.45),
        _ => Fixed.GetValueOrDefault(ch, 0xFFFF00FF),
    };

    public static uint Shade(uint argb, double f)
    {
        uint r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        uint S(uint v) => (uint)Math.Clamp(f >= 1 ? v + (255 - v) * (f - 1) : v * f, 0, 255);
        return (argb & 0xFF000000) | (S(r) << 16) | (S(g) << 8) | S(b);
    }
}
