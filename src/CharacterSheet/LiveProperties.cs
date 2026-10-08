using System.Reflection;
using System.Globalization;

internal static class LiveProperties
{
    private static readonly PropertyInfo? props = typeof(PropObject).GetProperty("m_PropDict", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    internal static string Summary = "Waiting for capture.";
    internal static void Sample(PlayerProp? player, NewItemProp? weapon)
    {
        try
        {
            var candidates = new List<string>();
            ReadBudget.Begin(512, 2);
            Read("player", player, candidates);
            Read("weapon", weapon, candidates);
            Summary = candidates.Count == 0 ? "No luck-related property key exposed." : string.Join("\n", candidates.Take(6));
        }
        catch (Exception ex)
        {
            Summary = "Property inspection unavailable; see trace.";
            TraceStore.Write("property-error", ex.Message);
        }
    }
    private static void Read(string source, PropObject? obj, List<string> candidates)
    {
        if (obj == null || props == null) return;
        var dict = props.GetValue(obj) as Il2CppSystem.Collections.Generic.Dictionary<string, PropItem>;
        if (dict == null) return;
        var values = new List<string>();
        foreach (var entry in dict)
        {
            if (!ReadBudget.Step()) { values.Add("[budget limit]"); break; }
            var item = entry.Value;
            if (item == null) continue;
            int type = (int)item.PropValueType;
            // Numeric types only: omit text, identity strings and complex objects.
            if (type != 0 && type != 3 && type != 4 && type != 8 && type != 9 && type != 11) continue;
            // Use native typed getters: Object.ToString() on these boxed values
            // produced object-header data rather than the numeric payload.
            string value;
            try
            {
                // A PropType tag may also wrap a list (e.g. weapon attack speed).
                // Check the actual boxed type before decoding instead of guessing
                // the payload layout from the tag alone.
                var boxed = item.Value;
                if (boxed == null) continue;
                string boxedType = boxed.GetIl2CppType().FullName;
                if (boxedType != "System.Int32" && boxedType != "System.Int64" && boxedType != "System.Single" && boxedType != "System.Double")
                {
                    TraceStore.Write("property-schema-" + source + "-" + entry.Key, "tag=" + type + " boxed=" + boxedType);
                    continue;
                }
                value = TraceStore.Boxed(boxed, boxedType);
                if (source == "weapon" && entry.Key == "DebuffProb" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double chance))
                    BuildBonuses.WeaponElementChance = chance / 100;
                if (obj is NewItemProp weapon)
                {
                    int? expected = entry.Key switch { "LuckyHit" => weapon.LuckyHit, "CrazyEff" => weapon.CrazyEff, _ => null };
                    if (expected.HasValue && value != expected.Value.ToString(CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Typed value " + value + " differs from weapon getter " + expected.Value);
                }
            }
            catch (Exception ex)
            {
                TraceStore.Write("property-decode-error", source + ": " + entry.Key + " [type " + type + "] " + ex.Message);
                continue;
            }
            string detail = entry.Key + "=" + value + " [type " + type + "]";
            values.Add(detail);
            if (entry.Key.Contains("luck", StringComparison.OrdinalIgnoreCase)) candidates.Add(source + ": " + detail);
            if (values.Count >= 256) break;
        }
        values.Sort(StringComparer.Ordinal);
        TraceStore.Write("live-" + source + "-properties", "object=" + obj.ObjectID + " " + string.Join("; ", values));
    }
}
