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
        ["Primary Skill Base DMG"]="Primary Skill Base DMG buffs",["Secondary Skill Base DMG"]="Secondary Skill Base DMG buffs",
        ["Next Weapon Skill DMG"]="Next Weapon Skill DMG",
        ["Elemental DMG"]="Elemental DMG buffs",["Fire DMG"]="Fire DMG buffs",
        ["Lightning DMG"]="Lightning DMG buffs",["Corrosion DMG"]="Corrosion DMG buffs",
        ["Explosion DMG"]="Explosion DMG buffs",["Rate of Fire"]="Rate of Fire",
        ["Movement Speed"]="Movement Speed",
        ["DMG Dealt"]="All DMG buffs",
        ["Lucky Shot Chance"]="Lucky Shot Chance",["All DMG Dealt"]="All DMG buffs"
    };
    internal static string ZeroText(string metric)=>"+0% "+metrics.First(p=>p.Value==metric).Key;
    internal static CoverageFinding[] Owners(EffectiveBonusRow effect,IEnumerable<CoverageFinding> findings)
    {
        bool identified=effect.Source.Length>0 && effect.SourceId!=0;
        if(!identified && effect.StateSid==1818)
        {
            var paths=findings.Where(f=>f.Source=="scroll" && f.Id==5865).ToArray();
            return paths.Length==1 && paths[0].Level==1 ? paths : Array.Empty<CoverageFinding>();
        }
        // October 16 trace: Cube's element-specific states lack source extras.
        // Match the verified definitions only to an owned Cube; this fallback
        // cannot invent a retained item or override explicit game identity.
        if(!identified && effect.StateSid is 1096 or 1097 or 1098)
        {
            var cubes=findings.Where(f=>f.Source=="scroll" && f.Id==5725).ToArray();
            return cubes.Length==1 ? cubes : Array.Empty<CoverageFinding>();
        }
        var matches=findings.Where(f=>identified ? f.Source==effect.Source && f.Id==effect.SourceId : f.Name==effect.Name).ToArray();
        // Combining copies requires the game's source ID, never just a shared label.
        return !identified && matches.Length!=1 ? Array.Empty<CoverageFinding>() : matches;
    }
    internal static IEnumerable<(string Metric,double Value)> Parse(string text)
    {
        text=Regex.Replace(text,@"</?b>","",RegexOptions.IgnoreCase);
        foreach(string sentence in Regex.Split(text,@"(?<=[.!?])\s+"))
        {
        if(Regex.IsMatch(sentence,@"\b(if|when|while|against|versus|per|every|final|total)\b",RegexOptions.IgnoreCase))continue;
        foreach(string part in sentence.Split(new[]{',',';','\n'},StringSplitOptions.RemoveEmptyEntries))
        {
            string clause=Regex.Replace(part.Trim().TrimEnd('.'),@"^Gain\s+","",RegexOptions.IgnoreCase);
            var paired=Regex.Match(clause,@"^Weapon DMG and Skill DMG (?<value>[+-]\d+(?:\.\d+)?)%$",RegexOptions.IgnoreCase);
            if(!paired.Success)paired=Regex.Match(clause,@"^(?<value>[+-]\d+(?:\.\d+)?)% Weapon DMG and Skill DMG$",RegexOptions.IgnoreCase);
            if(paired.Success && double.TryParse(paired.Groups["value"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out double both) && double.IsFinite(both))
            {yield return ("Weapon DMG buffs",both);yield return ("Skill DMG buffs",both);continue;}
            var match=Regex.Match(clause,@"^(?<value>[+-]\d+(?:\.\d+)?)% (?<metric>[A-Za-z ]+)$");
            if(!match.Success)match=Regex.Match(clause,@"^(?<metric>[A-Za-z ]+) (?<value>[+-]\d+(?:\.\d+)?)%$");
            if(match.Success && metrics.TryGetValue(match.Groups["metric"].Value,out string? metric) &&
                double.TryParse(match.Groups["value"].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) && double.IsFinite(value))
                yield return (metric,value);
        }
        }
    }
    internal static List<BuildContribution> Merge(IEnumerable<BuildContribution> original,IEnumerable<EffectiveBonusRow> effects,
        IEnumerable<CoverageFinding> findings,double now)
    {
        var rows=original.ToList();var owners=findings.ToArray();
        // Multiple state instances of one source are ambiguous, not automatically additive.
        var fresh=effects.Where(e=>now>=e.Checked && now-e.Checked<=e.MaxAge).ToArray();
        var live=fresh.Where(e=>e.Resolved).ToArray();
        var replacements=new List<(CoverageFinding[] Owners,string Metric,double Value)>();
        foreach(var effect in live)
        {
            var matches=Owners(effect,owners);
            // Retained bonuses can outlive inventory ownership. Require explicit
            // game source identity and a known catalogue source; never infer from name.
            if(matches.Length==0 && effect.Id>0 && effect.Source is "gem" or "scroll" or "blessing" && effect.SourceId!=0)
            {
                var known=ModifierCatalog.Entries.Where(p=>p.Key.Source==effect.Source && p.Key.Id==effect.SourceId).Select(p=>p.Value.Name).Distinct().ToArray();
                if(known.Length==1)
                    matches=new[]{new CoverageFinding(effect.Source,effect.SourceId,0,known[0],ModifierCoverage.Calculated,"Live retained status",
                        effect.Source=="gem" ? "Soul Jade" : effect.Source=="scroll" ? "Scroll" : "Blessing")};
            }
            if(matches.Length==0)continue;
            var owner=matches[0];
            // Only the captured normal skill branch is verified. Overlapping
            // branches change both the bonus and penalty; never infer that mix.
            if(owner.Source=="scroll" && owner.Id==5865 &&
                (owner.Level!=1 || effect.StateSid!=1818 || fresh.Any(e=>e.Id!=effect.Id && e.Id>0 &&
                    (e.Source=="scroll" && e.SourceId==5865 || e.Name.StartsWith("Convergent Paths",StringComparison.Ordinal)))))continue;
            if(owner.Source is "weapon" or "inscription")continue; // Already represented by weapon properties.
            foreach(var value in Parse(owner.Source=="ascension" ? NamedAbilityBonuses.DamageText(effect.Text) : effect.Text))replacements.Add((matches,value.Metric,value.Value));
        }
        foreach(var group in replacements.GroupBy(x=>(x.Owners[0].Source,x.Owners[0].Id,x.Metric)))
        {
            if(group.Count()!=1)continue;
            var replacement=group.Single();var owner=replacement.Owners[0];
            bool retained=owner.Level==0;
            var definitions=retained ? ModifierCatalog.Entries.Where(p=>p.Key.Source==owner.Source && p.Key.Id==owner.Id).Select(p=>(ModifierDefinition?)p.Value).ToArray() :
                replacement.Owners.Select(o=>ModifierCatalog.Entries.GetValueOrDefault((o.Source,o.Id,o.Level))).ToArray();
            if(definitions.Any(d=>d==null))continue;
            var matching=rows.Where(r=>r.Metric==replacement.Metric &&
                (r.SourceId!=0 ? r.SourceKey==owner.Source && r.SourceId==owner.Id :
                r.Source==owner.Category && replacement.Owners.Any(o=>r.Name==o.Name || r.Name==o.Name+" (Lv "+o.Level+")"))).ToArray();
            if(matching.Any(r=>r.Layer==ModifierLayer.Independent) || definitions.Any(d=>d!.Rules.Any(r=>r.Metric==replacement.Metric && r.Layer==ModifierLayer.Independent)))continue;
            rows.RemoveAll(r=>matching.Contains(r));
            bool combined=replacement.Owners.Length>1;
            string name=definitions[0]!.Name+(retained ? " (retained status)" : combined ? " (combined · "+replacement.Owners.Length+" copies)" : " (Lv "+owner.Level+")");
            rows.Add(new(replacement.Metric,owner.Category,name,replacement.Value,
                replacement.Metric.Contains("Skill Base DMG") ? ModifierLayer.Base : ModifierLayer.Additive,ModifierUnit.Percent,replacement.Metric,ModifierCondition.BuffPresence,owner.Source,owner.Id,combined ? 0 : owner.Level));
        }
        return rows;
    }
    internal static List<LscRow> MergeLuck(IEnumerable<LscRow> original,IEnumerable<EffectiveBonusRow> effects,IEnumerable<CoverageFinding> findings,double now)
    {
        var result=original.ToList();var owners=findings.ToArray();
        foreach(var value in Merge(Array.Empty<BuildContribution>(),effects,owners,now).Where(r=>r.Metric=="Lucky Shot Chance"))
        {
            var names=owners.Where(o=>o.Source==value.SourceKey && o.Id==value.SourceId).Select(o=>o.Name+" (Lv "+o.Level+")").ToHashSet();
            var matches=result.Where(r=>r.Source==value.Source && names.Contains(Regex.Replace(r.Name,@" · slot \d+",""))).ToArray();
            if(matches.Length==0 || (value.SourceLevel!=0 && matches.Length!=1))continue;
            int index=result.IndexOf(matches[0]);
            result.RemoveAll(r=>matches.Contains(r));
            result.Insert(index,matches[0] with {Name=value.SourceLevel==0 ? value.Name : matches[0].Name,Amount=value.Value,Note="Live observed contribution. "+(value.SourceLevel==0 ? "Combined across copies; individual shares unknown. " : "")+matches[0].Note});
        }
        return result;
    }
}
