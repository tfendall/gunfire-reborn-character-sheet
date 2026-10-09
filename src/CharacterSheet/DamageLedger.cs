// Cumulative captured damage. Source and element are separate partitions:
// a fire weapon hit is one weapon hit and one fire hit, never counted twice
// in either partition's total. No time window or idle-time decay.
internal sealed class DamageLedger
{
    internal double Total;
    internal readonly Dictionary<string, double> Sources = new();
    internal readonly Dictionary<string, double> Elements = new();
    internal readonly Dictionary<(string Source, string Element), double> Cells = new();
    internal void Clear() { Total = 0; Sources.Clear(); Elements.Clear(); Cells.Clear(); }
    internal void Add(long damage, int type, int action, int extra = 0, bool companion = false)
    {
        if (damage <= 0) return;
        string source = companion ? "Companions" : (type & 0xF00000) switch {
            0x800000 => action >= 30000 && action <= 65500 ? "Weapon hits" : "Weapon effects",
            // Explicit server extra-damage markers from the local enum. These
            // identify specific effects; action IDs alone cannot identify casts.
            0x400000 => (extra & 0xFF0) switch {
                0x10 => "Secondary skill · Fatal Bloom",
                0xD0 => "Seasonal · Magic Wand",
                0x160 => "Seasonal effects",
                0x170 => "Seasonal · Laser Turret",
                0x180 => "Seasonal · Tornado",
                0x190 => "Seasonal · Music Boom",
                _ => "Skills (combined)"
            },
            0x200000 => (type & 0xFF0000) switch { 0x220000 => "Burning", 0x210000 => "Bleeding", _ => "Damage over time" },
            _ => "Other damage"
        };
        string element = (type & 0x1F00) switch {
            0x100 => "Lightning", 0x200 => "Corrosion", 0x400 => "Fire",
            0x800 => "Normal", 0x1000 => "True damage", _ => "Other element"
        };
        Cells[(source, element)] = Cells.GetValueOrDefault((source, element)) + damage;
        Total += damage;
        Sources[source] = Sources.GetValueOrDefault(source) + damage;
        Elements[element] = Elements.GetValueOrDefault(element) + damage;
    }
    internal double Share(double damage) => Total == 0 ? 0 : 100 * damage / Total;
}
