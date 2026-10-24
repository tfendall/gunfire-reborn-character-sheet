// Pure presentation model. Scoped/timed metrics remain separate, and Total /
// Final modifiers remain individual factors rather than an invented sum.
internal sealed record SheetBonus(string Key, string Section, string Name, string Value, BuildContribution[] Contributors);
internal static class BuildSheetModel
{
    internal static string Value(string metric, double value) => value.ToString("+0.#;-0.#;+0") + (metric.EndsWith("(flat)") ? "" : metric.EndsWith("(multiplier)") ? "×" : metric.EndsWith("(seconds)") ? "s" : "%");
    internal static string Name(string metric) => metric.Replace(" (flat)", "").Replace(" (seconds)", "").Replace(" (multiplier)", "").Replace(" DMG buffs", " Damage").Replace("DMG", "Damage").Replace("[", "").Replace("]", "");
    internal static bool Related(string source, string metric)
    {
        if (metric.Contains("Shield") || metric.Contains("Armor") || metric.Contains("HP") || metric.EndsWith("(flat)")) return false;
        if (source.StartsWith("Weapon"))
            return metric.Contains("Weapon") || metric.Contains("General") || metric.Contains(" vs ") || metric.Contains("Elemental") || metric.Contains("Explosion");
        if (source.StartsWith("Skills") || source.StartsWith("Secondary skill") || source.StartsWith("Seasonal"))
            return !metric.Contains("Weapon") && (metric.Contains("DMG") || metric.Contains("double-cast"));
        return metric.Contains("Elemental") || metric.Contains("Fire") || metric.Contains("Lightning") || metric.Contains("Corrosion");
    }
    internal static SheetBonus[] Create(IEnumerable<BuildContribution> contributions)
    {
        var rows = new List<SheetBonus>();
        foreach (var group in contributions.GroupBy(row => row.Metric))
        {
            string metric = group.Key;
            string section = "DAMAGE BONUSES", name = Name(metric);
            if (metric.EndsWith("(flat)") && !metric.StartsWith("Maximum"))
            {
                string stem = metric[..^7];
                int split = stem.LastIndexOf(' ');
                section = split > 0 ? stem[..split].ToUpperInvariant() : "ABILITY MODIFIERS";
                name = split > 0 ? stem[(split + 1)..] : stem;
            }
            else if (metric.Contains("Shield") || metric.Contains("Armor") || metric.Contains("HP")) section = "RECOVERY & DEFENSE";
            else if (metric.Contains("Resistance")) section = "RECOVERY & DEFENSE";
            else if (metric.Contains("Movement") || metric.Contains("Reload") || metric.Contains("Cooldown") || metric.Contains("Capacity") || metric.Contains("Rate of Fire")) section = "ATTACK & MOVEMENT";
            else if (metric.Contains(" after ") || metric.Contains(" vs ")) section = "CONDITIONAL BONUSES";
            if (group.Any(row=>row.Layer==ModifierLayer.Independent) || metric.Contains("Total") || metric.Contains("Final"))
            {
                int index = 0;
                foreach (var effect in group)
                    rows.Add(new(metric + ":" + index++, "INDEPENDENT MODIFIERS", name + " · " + effect.Name, Value(metric, effect.Value), new[] { effect }));
            }
            else rows.Add(new(metric, section, name, Value(metric, group.Sum(row => row.Value)), group.ToArray()));
        }
        return rows.OrderBy(row => Rank(row.Section)).ThenBy(row => row.Section)
            .ThenBy(row => MetricRank(row.Key)).ThenBy(row => row.Name).ToArray();
    }
    internal static SheetBonus[] Stable(IEnumerable<BuildContribution> contributions)
    {
        var rows=Create(contributions).ToList();
        foreach(string metric in MainMetrics)
            if(!rows.Any(row=>row.Key==metric))rows.Add(new(metric,metric=="Rate of Fire" ? "ATTACK & MOVEMENT" : "DAMAGE BONUSES",Name(metric),Value(metric,0),Array.Empty<BuildContribution>()));
        return rows.OrderBy(row=>Rank(row.Section)).ThenBy(row=>row.Section).ThenBy(row=>Array.IndexOf(MainMetrics,row.Key)>=0 ? Array.IndexOf(MainMetrics,row.Key) : 100).ThenBy(row=>row.Name).ToArray();
    }
    internal static readonly string[] MainMetrics={"Weapon DMG buffs","Skill DMG buffs","Primary Skill DMG buffs","Secondary Skill DMG buffs","Primary Skill Base DMG buffs","Secondary Skill Base DMG buffs","Next Weapon Skill DMG","All DMG buffs","Elemental DMG buffs","Fire DMG buffs","Lightning DMG buffs","Corrosion DMG buffs","Explosion DMG buffs","Critical Chance","Critical Multiplier (multiplier)","Rate of Fire"};
    internal static string Family(SheetBonus row)=>row.Key switch {
        "lsc" or "Weapon DMG buffs" or "Critical Chance" or "Critical Multiplier (multiplier)"=>"WEAPON & LUCK",
        "Skill DMG buffs" or "Next Weapon Skill DMG"=>"SKILLS",
        "Primary Skill DMG buffs" or "Primary Skill Base DMG buffs"=>"PRIMARY SKILL DAMAGE",
        "Secondary Skill DMG buffs" or "Secondary Skill Base DMG buffs"=>"SECONDARY SKILL DAMAGE",
        "Elemental DMG buffs" or "Fire DMG buffs" or "Lightning DMG buffs" or "Corrosion DMG buffs" or "Explosion DMG buffs"=>"ELEMENTS",
        _=>row.Section
    };
    internal static string RowLabel(SheetBonus row)=>row.Key switch {
        "Primary Skill DMG buffs" or "Secondary Skill DMG buffs"=>"Additive bonus",
        "Primary Skill Base DMG buffs" or "Secondary Skill Base DMG buffs"=>"Base bonus",
        _=>row.Name
    };
    internal static int FamilyRank(string family)=>family switch {"WEAPON & LUCK"=>0,"SKILLS"=>1,"PRIMARY SKILL DAMAGE"=>2,"SECONDARY SKILL DAMAGE"=>3,"ELEMENTS"=>4,"ATTACK & MOVEMENT"=>5,"INDEPENDENT MODIFIERS"=>6,"CONDITIONAL BONUSES"=>7,"RECOVERY & DEFENSE"=>8,_=>9};
    internal static bool Relevant(string metric,string description,string source="")
    {
        string text=(source=="ascension" ? NamedAbilityBonuses.DamageText(description) : description).ToLowerInvariant();
        if(metric=="Rate of Fire")return System.Text.RegularExpressions.Regex.IsMatch(text,@"\brof\b|rate of fire|fire rate|firing rate");
        if(metric=="lsc")return text.Contains("lucky shot") || text.Contains("lucky strike");
        if(metric=="All DMG buffs")return text.Contains("all dmg") || text.Contains("all damage");
        if(metric.StartsWith("Critical"))return text.Contains("critx") || text.Contains("critical") || text.Contains("crit hit");
        if(metric=="Next Weapon Skill DMG")return text.Contains("weapon skill") && (text.Contains("dmg") || text.Contains("damage"));
        if(metric=="Weapon DMG buffs")return System.Text.RegularExpressions.Regex.IsMatch(text,@"weapon (?:total )?(?:dmg|damage)|(?:total|final) weapon (?:dmg|damage)");
        if(metric=="Skill DMG buffs")return System.Text.RegularExpressions.Regex.IsMatch(text,@"(?<!weapon )skill (?:dmg|damage)");
        if(metric.StartsWith("Primary") || metric.StartsWith("Secondary"))
            return text.Contains((metric.StartsWith("Primary") ? "primary" : "secondary")+" skill "+(metric.Contains("Base") ? "base " : "")+"dmg") || text.Contains((metric.StartsWith("Primary") ? "primary" : "secondary")+" skill "+(metric.Contains("Base") ? "base " : "")+"damage");
        string token=metric.Split(' ')[0].ToLowerInvariant();
        return text.Contains(token+" dmg") || text.Contains(token+" damage");
    }
    private static int Rank(string section) => section switch { "DAMAGE BONUSES" => 0, "INDEPENDENT MODIFIERS" => 1, "CONDITIONAL BONUSES" => 3, "RECOVERY & DEFENSE" => 4, _ => 2 };
    private static int MetricRank(string metric) => metric switch { "Weapon DMG buffs" => 0, "Skill DMG buffs" => 1, "Elemental DMG buffs" => 2, _ => 3 };
}
