// Ownership snapshots are committed by source, atomically. No game objects,
// reflection, native calls or disk IO in this class.
internal sealed record BuffMeasurement(int Count, double RemainingSeconds, bool Immortal);
internal static class ModifierRuntime
{
    private static readonly Dictionary<string,List<EquippedModifier>> owners = new();
    private static List<EquippedModifier> pending = new();
    private static readonly Dictionary<string,(Dictionary<int,BuffMeasurement> Values,long Time,bool Complete)> buffs = new();
    private static readonly Dictionary<string,(Dictionary<string,double> Values,long Time)> resources = new();
    private static int weaponId;
    private static int? magazine;
    private static long magazineTime;
    internal static List<BuildContribution> Rows = new();
    internal static List<LscRow> Luck = new();
    internal static CoverageFinding[] Findings = Array.Empty<CoverageFinding>();
    internal static long Now => Environment.TickCount64;
    internal static void Reset() { owners.Clear();pending=new();buffs.Clear();resources.Clear();weaponId=0;magazine=null;Rows=new();Luck=new();Findings=Array.Empty<CoverageFinding>(); }
    internal static void Begin() => pending=new();
    internal static void Add(string source,int id,int level,string category) => pending.Add(new(source,id,level,category));
    internal static void Commit(string source) { owners[source]=pending;Rebuild(); }
    internal static void Abort(string source) { owners.Remove(source);Rebuild(); }
    internal static void Weapon(int id) { if(weaponId==id)return;weaponId=id;magazine=null;owners.Remove("weapon-inscriptions");Rebuild(); }
    internal static void Magazine(int id,int? ammo) { if(id!=weaponId)return;magazine=ammo;magazineTime=Now;Rebuild(); }
    internal static void Buffs(string channel,Dictionary<int,BuffMeasurement>? snapshot,bool complete=true)
    {
        if(snapshot==null)buffs.Remove(channel);else buffs[channel]=(snapshot,Now,complete);
        Rebuild();
    }
    internal static void Resources(string channel,Dictionary<string,double>? snapshot)
    { if(snapshot==null)resources.Remove(channel);else resources[channel]=(snapshot,Now);Rebuild(); }
    internal static double? Resource(string key,long? at=null)
    {
        long now=at??Now;
        foreach(var snapshot in resources.Values)
            if(now-snapshot.Time<=5000 && snapshot.Values.TryGetValue(key,out double value) && double.IsFinite(value))return value;
        return null;
    }
    internal static int? Stacks(ModifierRule rule,long? at = null)
    {
        long now=at??Now;int max=0;bool complete=false;
        // Server states are the authoritative source for reviewed stack rules.
        // Client-only mirrors are captured but not mixed with unrelated IDs.
        if(buffs.TryGetValue("server",out var snapshot) && now-snapshot.Time<=5000)
        {
            complete=snapshot.Complete;bool found=false;
            foreach(int id in rule.StateIds??Array.Empty<int>())
                if(snapshot.Values.TryGetValue(id,out var value))
                { found=true;if(value.Immortal || value.RemainingSeconds>(now-snapshot.Time)/1000.0)max=Math.Max(max,rule.Condition==ModifierCondition.BuffPresence ? 1 : value.Count); }
            if(found)return max;
        }
        return complete ? max : null;
    }
    internal static void Refresh() => Rebuild();
    private static void Rebuild()
    {
        var rows=new List<BuildContribution>();var luck=new List<LscRow>();var findings=new List<CoverageFinding>();
        int? ammo=Now-magazineTime<=5000 ? magazine : null;
        foreach(var item in owners.Values.SelectMany(group=>group))
        {
            ModifierCatalog.Entries.TryGetValue((item.Source,item.Id,item.Level),out var definition);
            if(definition==null){findings.Add(ModifierEvaluator.Coverage(item,null,ammo) with { Category=item.Category });continue;}
            bool unknown=false;
            foreach(var rule in definition.Rules)
            {
                double? value=ModifierEvaluator.Value(rule,ammo,rule.Condition is ModifierCondition.BuffStacks or ModifierCondition.BuffPresence or ModifierCondition.BuffResource ? Stacks(rule) : null,
                    rule.Resource.Length>0 ? Resource(rule.Resource) : null);
                if(!value.HasValue)unknown=true;
                string name=definition.Name+" (Lv "+item.Level+")";
                if(rule.Condition==ModifierCondition.Magazine && ammo.HasValue) name+=" · "+ammo+" rounds";
                if(rule.Metric=="Lucky Shot Chance")
                {
                    // Reviewed legacy Lucky Shot rows own their caps and base
                    // contributions; never count the same effect twice.
                    if(rule.Condition!=ModifierCondition.Always || !LscCatalog.Entries.TryGetValue((item.Source,item.Id,item.Level),out var legacy) || !legacy.Flat.HasValue)
                        luck.Add(new(item.Category,name,value,rule.Requirement));
                }
                else if(value.HasValue)rows.Add(new(rule.Metric,item.Category,name,value.Value,rule.Layer,rule.Unit,rule.Scope,rule.Condition));
            }
            var finding=ModifierEvaluator.Coverage(item,definition,ammo) with { Category=item.Category };
            if(unknown)finding=finding with { Status=ModifierCoverage.NeedsState,Reason="Runtime measurement missing/stale; excluded from totals. "+definition.Description };
            else if(definition.Rules.Length>0 && finding.Status==ModifierCoverage.NeedsState)finding=finding with { Status=definition.Review=="partial" ? ModifierCoverage.Partial : ModifierCoverage.Calculated,Reason="Runtime resource/stack rule evaluated from a fresh snapshot." };
            findings.Add(finding);
        }
        // Preserve list identity when values do not change, so the UI's
        // cached grouping does not rebuild on every unchanged snapshot.
        if(!Rows.SequenceEqual(rows))Rows=rows;
        if(!Luck.SequenceEqual(luck))Luck=luck;
        if(!Findings.SequenceEqual(findings))Findings=findings.ToArray();
    }
}
