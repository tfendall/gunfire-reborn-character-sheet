using System.Globalization;
using System.Text.RegularExpressions;

// Accept only whole, unqualified clauses with a known metric. No substring
// matching that could promote companion/target/final/next-hit damage to general damage.
internal static class EffectiveContributionMerge
{
    private static readonly Dictionary<string,string> metrics=new(StringComparer.OrdinalIgnoreCase)
    {
        ["Weapon DMG"]="Weapon DMG buffs",["Skill DMG"]="Skill DMG buffs",
        ["Primary Skill DMG"]="Primary Skill DMG buffs",["Secondary Skill DMG"]="Secondary Skill DMG buffs",
        ["Next Weapon Skill DMG"]="Next Weapon Skill DMG",
        ["Elemental DMG"]="Elemental DMG buffs",["Fire DMG"]="Fire DMG buffs",
        ["Lightning DMG"]="Lightning DMG buffs",["Corrosion DMG"]="Corrosion DMG buffs",
        ["Explosion DMG"]="Explosion DMG buffs",["Rate of Fire"]="Rate of Fire"
    };
    internal static IEnumerable<(string Metric,double Value)> Parse(string text)
    {
        if(Regex.IsMatch(text,@"\b(if|when|while|against|versus|per|every|final|total)\b",RegexOptions.IgnoreCase))yield break;
        foreach(string part in text.Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries))
        {
            string clause=part.Trim().TrimEnd('.');
            var match=Regex.Match(clause,@"^(?<value>[+-]\d+(?:\.\d+)?)% (?<metric>[A-Za-z ]+)$");
            if(!match.Success)match=Regex.Match(clause,@"^(?<metric>[A-Za-z ]+) (?<value>[+-]\d+(?:\.\d+)?)%$");
            if(match.Success && metrics.TryGetValue(match.Groups["metric"].Value,out string? metric) &&
                double.TryParse(match.Groups["value"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) && double.IsFinite(value))
                yield return (metric,value);
        }
    }
    internal static List<BuildContribution> Merge(IEnumerable<BuildContribution> original,IEnumerable<EffectiveBonusRow> effects,
        IEnumerable<CoverageFinding> findings,double now)
    {
        var rows=original.ToList();var owners=findings.ToArray();
        // Multiple state instances of one source are ambiguous, not automatically additive.
        var live=effects.Where(e=>e.Resolved && now>=e.Checked && now-e.Checked<=5).ToArray();
        var replacements=new List<(CoverageFinding Owner,string Metric,double Value)>();
        foreach(var effect in live)
        {
            var matches=owners.Where(f=>effect.Source.Length>0 && effect.SourceId!=0
                ? f.Source==effect.Source && f.Id==effect.SourceId
                : f.Name==effect.Name).ToArray();
            if(matches.Length!=1)continue; // Require unique owned-source attribution.
            var owner=matches[0];
            if(owner.Source is "weapon" or "inscription")continue; // Already represented by weapon properties.
            foreach(var value in Parse(effect.Text))replacements.Add((owner,value.Metric,value.Value));
        }
        foreach(var group in replacements.GroupBy(x=>(x.Owner.Source,x.Owner.Id,x.Metric)))
        {
            if(group.Count()!=1)continue;
            var replacement=group.Single();var owner=replacement.Owner;
            if(!ModifierCatalog.Entries.TryGetValue((owner.Source,owner.Id,owner.Level),out var definition))continue;
            var matching=rows.Where(r=>r.Metric==replacement.Metric && r.Source==owner.Category &&
                (r.Name==definition.Name || r.Name==definition.Name+" (Lv "+owner.Level+")")).ToArray();
            if(matching.Any(r=>r.Layer==ModifierLayer.Independent) || definition.Rules.Any(r=>r.Metric==replacement.Metric && r.Layer==ModifierLayer.Independent))continue;
            rows.RemoveAll(r=>matching.Contains(r));
            rows.Add(new(replacement.Metric,owner.Category,definition.Name+" (Lv "+owner.Level+")",replacement.Value,
                ModifierLayer.Additive,ModifierUnit.Percent,replacement.Metric,ModifierCondition.BuffPresence));
        }
        return rows;
    }
}
