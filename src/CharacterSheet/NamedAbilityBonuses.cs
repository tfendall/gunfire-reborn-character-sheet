using System.Globalization;
using System.Text.RegularExpressions;

// Ability roles, not per-ascension exceptions. Restrict projection to hero
// ascensions: seasonal Energy Orb effects are not Crown Prince's primary skill.
internal static class NamedAbilityBonuses
{
    private static readonly Dictionary<string,string> damageTextCache=new(StringComparer.Ordinal);
    private static readonly Dictionary<string,string> roles=new(StringComparer.OrdinalIgnoreCase)
    {
        ["Soul Strike"]="Primary",["Energy Orb"]="Primary",["Leap"]="Primary",
        ["Flying Sword"]="Primary",["Spiritual Flame"]="Primary",
        ["Cleave"]="Secondary",["Essence Chain"]="Secondary",["Striking Punch"]="Secondary",
        ["Fatal Bloom"]="Secondary",["Blazing Meteor"]="Secondary",
        ["Smoke Grenade"]="Secondary",["Explosive Grenade"]="Secondary",["Chain Lightning"]="Secondary"
    };
    internal static string DamageText(string text)
    {
        if(damageTextCache.TryGetValue(text,out var cached))return cached;
        string original=text;
        foreach(var pair in roles.OrderByDescending(p=>p.Key.Length))
        {
            string ability=@"\[?"+Regex.Escape(pair.Key)+@"\]?";
            text=Regex.Replace(text,@"\bBase (?:DMG|Damage) of "+ability+@"\b",pair.Value+" Skill Base DMG",RegexOptions.IgnoreCase);
            text=Regex.Replace(text,@"(?<![\w])"+ability+@"\s+(?<base>base\s+)?(?:DMG|Damage)\b",
                m=>pair.Value+" Skill "+(m.Groups["base"].Success ? "Base " : "")+"DMG",RegexOptions.IgnoreCase);
        }
        if(damageTextCache.Count>=1024)damageTextCache.Clear();
        damageTextCache[original]=text;
        return text;
    }
    internal static ModifierDefinition Project(string source,ModifierDefinition definition)
    {
        if(source!="ascension")return definition;
        var rules=definition.Rules.Select(rule=>
        {
            if(rule.Layer is not (ModifierLayer.Base or ModifierLayer.Additive) || rule.Unit!=ModifierUnit.Percent)return rule;
            string normalized=DamageText(rule.Metric);
            if(normalized==rule.Metric)return rule;
            if(normalized is "Primary Skill DMG" or "Secondary Skill DMG" or "Primary Skill Base DMG" or "Secondary Skill Base DMG")
                return rule with {Metric=normalized+" buffs"};
            return rule;
        }).ToArray();
        if(rules.Length>0)return definition with {Rules=rules};
        // Only a whole unconditional damage sentence establishes a new static rule.
        // Mixed or conditional descriptions retain their unresolved coverage.
        string text=DamageText(definition.Description);
        var match=Regex.Match(text,@"^\+(?<amount>\d+(?:\.\d+)?)% (?<metric>(?:Primary|Secondary) Skill (?:Base )?DMG)\.?$",RegexOptions.IgnoreCase);
        if(!match.Success || text==definition.Description)return definition;
        string metric=match.Groups["metric"].Value;
        double amount=double.Parse(match.Groups["amount"].Value,CultureInfo.InvariantCulture);
        return definition with {Review="calculated",Rules=new[]{new ModifierRule(metric+" buffs",amount,ModifierUnit.Percent,
            metric.Contains("Base") ? ModifierLayer.Base : ModifierLayer.Additive,"Named ability",ModifierCondition.Always)}};
    }
}
