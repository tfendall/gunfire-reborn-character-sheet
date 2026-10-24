// Learn resolved, unqualified status bonuses with an identified source group.
// Absence describes this status's contribution, not arbitrary catalogue conditions.
internal sealed class InactiveBonusRules
{
    private sealed record Learned(EffectiveBonusRow Zero,string Ownership);
    private static string Signature(IEnumerable<CoverageFinding> owners)=>string.Join(",",owners.Select(o=>o.Level).OrderBy(l=>l));
    private readonly Dictionary<int,Learned> learned=new();
    internal void Clear()=>learned.Clear();
    internal void Forget(int sid)=>learned.Remove(sid);
    internal void Learn(int sid,EffectiveBonusRow active,IEnumerable<CoverageFinding> findings)
    {
        var owners=EffectiveContributionMerge.Owners(active,findings);
        if(!active.Resolved || sid<=0 || owners.Length==0 || owners[0].Source is "weapon" or "inscription")
        {Forget(sid);return;}
        var values=EffectiveContributionMerge.Parse(owners[0].Source=="ascension" ? NamedAbilityBonuses.DamageText(active.Text) : active.Text).ToArray();
        if(values.Length==0 || values.Select(v=>v.Metric).Distinct().Count()!=values.Length)
        {Forget(sid);return;}
        if(learned.Count>=512 && !learned.ContainsKey(sid))return;
        var owner=owners[0];
        string zero=string.Join(", ",values.Select(v=>EffectiveContributionMerge.ZeroText(v.Metric)))+".";
        learned[sid]=new(new(-sid,active.Name,zero,active.Checked,true,owner.Source,owner.Id,1,active.StateSid),Signature(owners));
    }
    internal EffectiveBonusRow[] Observe(IEnumerable<int> present,bool complete,IEnumerable<CoverageFinding> findings,double now)
    {
        var owners=findings.ToArray();
        foreach(var pair in learned.ToArray())
            if(Signature(owners.Where(f=>f.Source==pair.Value.Zero.Source && f.Id==pair.Value.Zero.SourceId))!=pair.Value.Ownership)
                learned.Remove(pair.Key);
        if(!complete)return Array.Empty<EffectiveBonusRow>();
        var sids=present.ToHashSet();
        return learned.Where(p=>!sids.Contains(p.Key)).Select(p=>p.Value.Zero with {Checked=now}).ToArray();
    }
}
