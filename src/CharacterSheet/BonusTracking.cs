using System.Text.Json;

// Main-thread snapshots only. Rendering and window calculations do no native reads.
internal static class BonusTracking
{
    internal static readonly BonusCombatWindow Window=new();
    internal static List<BuildContribution> Rows=new();
    internal static List<LscRow> Luck=new();
    internal static readonly HitBonusWindow HitWindow=new();
    internal static BonusWindowResult[] Averages=Array.Empty<BonusWindowResult>();
    private static bool priorHawkeye,priorValid;
    private static double priorTime;
    private static int player,epoch,weapon;
    private static double next,nextLog,windowStart;
    internal static void Tick()
    {
        double now=DamageCapture.Now;
        if(player!=Panel.LocalPlayerId || epoch!=DamageCapture.Epoch || weapon!=Panel.CurrentWeaponId || !Panel.RunActive)
        {Window.Clear();HitWindow.Clear();Averages=Array.Empty<BonusWindowResult>();priorValid=false;Rows=new();Luck=new();player=Panel.LocalPlayerId;epoch=DamageCapture.Epoch;weapon=Panel.CurrentWeaponId;next=nextLog=0;windowStart=now;}
        if(!Panel.RunActive || now<next)return;
        next=now+0.25;
        var effects=EffectRefreshDiagnostic.ShowInBuild ? EffectRefreshDiagnostic.View.Snapshot() : Array.Empty<EffectiveBonusRow>();
        Rows=EffectiveContributionMerge.Merge(BuildBonuses.Rows,effects,ModifierRuntime.Findings,EffectRefreshDiagnostic.Now).Where(r=>r.Metric!="Lucky Shot Chance").ToList();
        Luck=EffectiveContributionMerge.MergeLuck(BaseLsc.Rows,effects,ModifierRuntime.Findings,EffectRefreshDiagnostic.Now);
        bool valid=Panel.Capture && LoadingGuard.CanRead && StatsScreen.ReadsAllowed;
        while(DamageCapture.BonusHits.TryDequeue(out var hit))
            if(hit.Weapon==weapon && hit.Time>=windowStart)
                // Weak-point hits establish the critical branch. A non-weak hit may
                // still crit through another effect; do not infer the -50% branch.
                HitWindow.Add(hit.Time,priorValid && valid && hit.Time>=priorTime && hit.Time-priorTime<=1 && DamageCapture.BonusHitDrops==0 && hit.Weakpoint ? true : null,priorHawkeye);
        bool hawkeye=ModifierRuntime.Findings.Count(f=>f.Source=="scroll" && f.Id==5720 && f.Level==1)==1;
        priorHawkeye=hawkeye;priorValid=valid;priorTime=now;
        var observations=new List<BonusObservation>();
        foreach(var group in Rows.Where(r=>r.Metric!="Lucky Shot Chance")
            .GroupBy(r=>r.SourceId!=0 ? $"{r.SourceKey}:{r.SourceId}:{r.SourceLevel}:{r.Metric}" : $"{r.Source}:{r.Name}:{r.Metric}"))
        {
            var row=group.First();
            bool applicable=row.Condition!=ModifierCondition.Scoped && row.Scope!="Element" && !row.Scope.StartsWith("Target:") &&
                !new[]{"Fire DMG buffs","Lightning DMG buffs","Corrosion DMG buffs","Elemental DMG buffs","Explosion DMG buffs"}.Contains(row.Metric);
            if(row.Condition!=ModifierCondition.BuffPresence && ModifierRuntime.Findings.Any(f=>f.Source==row.SourceKey && f.Id==row.SourceId && f.Level==row.SourceLevel && f.Status!=ModifierCoverage.Calculated))applicable=false;
            observations.Add(new(group.Key,row.Name,row.Metric,applicable && (group.Count()==1 || row.Layer==ModifierLayer.Additive) ? group.Sum(r=>r.Value) : null));
        }
        foreach(var group in Luck.GroupBy(r=>r.Source+":"+r.Name))
        {
            var row=group.First();
            // A partial item's static Lucky Shot amount is a floor, not its observed total.
            bool partial=ModifierRuntime.Findings.Any(f=>f.Category==row.Source && row.Name.StartsWith(f.Name+" ",StringComparison.Ordinal) && f.Status!=ModifierCoverage.Calculated);
            double? value=group.Count()==1 && (!partial || row.Note.StartsWith("Live observed contribution.")) ? row.Amount : null;
            observations.Add(new("luck:"+group.Key,row.Name,"Lucky Shot Chance",value));
        }
        observations.AddRange(HeadlineBonuses.Observe(Rows,ModifierRuntime.Findings));
        Window.Observe(now,DamageCapture.LastCombatHit>=windowStart ? DamageCapture.LastCombatHit : double.NegativeInfinity,observations,valid);
        Averages=Window.Snapshot();
        if(hawkeye)Averages=Averages.Append(HitWindow.Snapshot(now)).ToArray();
        if(now>=nextLog)
        {
            nextLog=now+2;
            // Exact post-merge values used by the UI, including coverage and trace health.
            var snapshot=Averages;
            // Small chunks remain below the trace record length limit in large builds.
            int chunks=Math.Max(1,Math.Max((Rows.Count+3)/4,Math.Max((Luck.Count+3)/4,(snapshot.Length+3)/4)));
            for(int chunk=0;chunk<chunks;chunk++)
                TraceStore.Write("displayed-bonus-snapshot",JsonSerializer.Serialize(new {time=now,player,epoch,weapon,valid,chunk,chunks,
                    rows=Rows.Skip(chunk*4).Take(4),luck=Luck.Skip(chunk*4).Take(4),window=snapshot.Skip(chunk*4).Take(4),traceHealth=TraceStore.Health}));
        }
    }
}
