using System.Text.RegularExpressions;

// Presentation only: damage drivers are not a partition of observed damage.
// Profiles select reviewed rules; description matching only suggests effects to review.
internal sealed record DamagePowerProfile(string Name, Func<BuildContribution,bool> Includes, Func<string,bool> MayRelate);
internal sealed record DamagePowerEntry(string Section, string Name, string Text,string Metric="",string SourceKey="",int SourceId=0);
internal static class DamagePowerModel
{
    internal const string WeaponKey = "powers:weapon";
    internal const string Introduction = "Current build, not a reconstruction of earlier hits. Values describe bonuses, not each item's share of damage. Coverage is partial; separate damage, crit, speed and conditional bonuses are not added into one multiplier.";
    internal static readonly DamagePowerProfile Weapon = new("Weapon damage", WeaponRule,
        MayPowerWeapon);

    private static bool MayPowerWeapon(string text)
    {
        // Remove defensive phrases before finding possible offensive clauses.
        string offense=Regex.Replace(text,@"(?:DMG|damage) (?:taken|resistance|reduction)","defense",RegexOptions.IgnoreCase);
        return Regex.IsMatch(offense,@"weapon(?! skill)|\bDMG\b|damage|crit|lucky|\brof\b|rate of fire|projectile|ammo|magazine|reload",RegexOptions.IgnoreCase);
    }

    private static bool WeaponRule(BuildContribution row)
    {
        if(row.Scope.StartsWith("Ability:") || row.Scope.Contains("Skill") || row.Metric.Contains("Skill") ||
            row.Metric.Contains("Companion") || row.Layer is ModifierLayer.Defense or ModifierLayer.Resource) return false;
        return row.Scope=="Weapon" || ElementMetric(row.Metric) || row.Metric is "Rate of Fire" or "Critical Chance" or "Critical Multiplier (multiplier)" ||
            ((row.Scope is "General" or "Element" || row.Scope.StartsWith("Target:")) && row.Metric.Contains("DMG")) ||
            row.Metric=="Weapon DMG buffs";
    }
    private static bool ElementMetric(string metric)=>metric is "Elemental DMG buffs" or "Fire DMG buffs" or "Lightning DMG buffs" or "Corrosion DMG buffs" or "Explosion DMG buffs";
    internal static DamagePowerEntry[] Create(DamagePowerProfile profile, IEnumerable<BuildContribution> contributions,
        IEnumerable<CoverageFinding> findings, IEnumerable<LscRow>? luck = null)
    {
        var result=new List<DamagePowerEntry>();
        var owners=findings.ToArray();
        var values=contributions.ToArray();
        foreach(var row in values.Where(r=>r.Metric!="Lucky Shot Chance" && profile.Includes(r)))
        {
            var owner=owners.Where(f=>row.SourceId!=0
                ? f.Source==row.SourceKey && f.Id==row.SourceId && f.Level==row.SourceLevel
                : f.Category==row.Source && row.Name==f.Name+" (Lv "+f.Level+")").ToArray();
            bool scoped=row.Condition==ModifierCondition.Scoped || row.Scope=="Element" || ElementMetric(row.Metric) || row.Scope.StartsWith("Target:");
            string section=scoped ? "CONDITIONAL BONUSES" : row.Layer==ModifierLayer.Independent ? "INDEPENDENT MULTIPLIERS" :
                row.Metric.StartsWith("Critical") || row.Metric=="Rate of Fire" ? "CRITS & ATTACK RATE" : "DAMAGE BONUSES";
            string evidence=scoped ? "Conditional — applicability not measured" : row.Condition is ModifierCondition.Always ? "Calculated" : "Calculated from captured state";
            string text=BuildSheetModel.Value(row.Metric,row.Value)+" "+BuildSheetModel.Name(row.Metric)+"\n"+row.Source+" · "+evidence;
            if(row.Scope.Length>0)text+="\nApplies to: "+row.Scope;
            if(row.Layer==ModifierLayer.Independent)text+="\nSeparate multiplier; not part of the additive subtotal.";
            if(owner.Length==1 && ModifierCatalog.Entries.TryGetValue((owner[0].Source,owner[0].Id,owner[0].Level),out var definition))
                text+="\n"+definition.Description;
            result.Add(new(section,row.Name,text,row.Metric,row.SourceKey,row.SourceId));
        }
        if(luck!=null)
            foreach(var row in luck)
                result.Add(new("CRITS & ATTACK RATE",row.Name,
                    (row.Amount.HasValue ? BuildSheetModel.Value("Lucky Shot Chance",row.Amount.Value)+" Lucky Shot Chance" : "Lucky Shot Chance — unresolved")+
                    "\n"+row.Source+" · "+(row.Source=="Weapon" ? "Measured weapon component (includes static inscriptions)" : "Listed contribution; caps and conditions may apply")+
                    (row.Cap.HasValue ? "\nLucky Shot cap: "+row.Cap.Value.ToString("0.#")+"%" : "")+
                    (row.Note.Length>0 ? "\n"+row.Note : ""),"Lucky Shot Chance"));
        foreach(var owner in owners)
        {
            if(!ModifierCatalog.Entries.TryGetValue((owner.Source,owner.Id,owner.Level),out var definition))
            {
                result.Add(new("EFFECTS TO REVIEW",owner.Name,"Unresolved — missing definition; relevance to "+profile.Name.ToLowerInvariant()+" is unknown."));
                continue;
            }
            bool hasRule=definition.Rules.Any(r=>profile.Includes(new(r.Metric,owner.Category,definition.Name,r.Amount,r.Layer,r.Unit,r.Scope,r.Condition)));
            if(!hasRule && !profile.MayRelate(definition.Description))continue;
            // Even calculated inscriptions may already be embedded in weapon properties.
            if(owner.Status==ModifierCoverage.Calculated && owner.Source!="inscription")continue;
            string reason=owner.Source=="inscription" ? "Inscription effect — activation and separate contribution unresolved; may already be included in weapon properties." :
                values.Any(r=>r.SourceKey==owner.Source && r.SourceId==owner.Id && r.Condition==ModifierCondition.BuffPresence && profile.Includes(r)) ? "Live bonus shown above; other clauses are not quantified." :
                owner.Status==ModifierCoverage.Partial ? (hasRule ? "Partial — represented values appear above; remaining clauses are not calculated." : "Partial catalogue coverage; contribution to this damage source is unresolved.") :
                owner.Status==ModifierCoverage.NoNumericModifier ? "Trigger or utility effect — not quantified as a damage bonus." :
                "Unresolved — current value or applicability unavailable.";
            result.Add(new(hasRule ? "UNRESOLVED EFFECTS" : "EFFECTS TO REVIEW",definition.Name+" (Lv "+owner.Level+")",
                owner.Category+" · "+reason+(hasRule ? "" : "\nPossible interaction; relevance to this damage source is not verified.")+"\n"+definition.Description));
        }
        return result.OrderBy(e=>Rank(e.Section)).ToArray();
    }
    private static int Rank(string section)=>section switch {"DAMAGE BONUSES"=>0,"INDEPENDENT MULTIPLIERS"=>1,"CRITS & ATTACK RATE"=>2,"CONDITIONAL BONUSES"=>3,"UNRESOLVED EFFECTS"=>4,_=>5};
}
