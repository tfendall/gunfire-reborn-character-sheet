internal static class BonusWindowTests
{
    internal static void Run(Action<bool,string> check)
    {
        BonusObservation[] Value(double? value)=>new[]{new BonusObservation("effect","Effect","Weapon DMG buffs",value)};
        var window=new BonusCombatWindow();
        window.Observe(0,0,Value(100),true);
        window.Observe(1,1,Value(0),true);
        window.Observe(2,2,Value(null),true);
        window.Observe(3,3,Value(200),true);
        window.Observe(4,4,Value(200),true);
        var result=window.Snapshot().Single();
        check(Math.Abs(result.Average!.Value-100)<1e-8 && result.Peak==200 && Math.Abs(result.Uptime!.Value-200.0/3)<1e-8 && result.Coverage==75 && result.Seconds==4,
            "combat window weights prior values by time; missing is not zero; uptime denominator is observed time");
        window.Observe(5,4,Value(200),true);window.Observe(6,4,Value(200),true);
        window.Observe(100,4,Value(200),true);
        check(window.Seconds==6,"combat window includes two-second hit grace but excludes extended idle time");
        window.Observe(101,101,Value(200),true);window.Observe(102,102,Value(200),true);
        check(window.Seconds==7,"new hit after downtime does not backfill idle time");
        window.Clear();window.Observe(0,0,Value(10),true);
        for(int i=1;i<=40;i++)window.Observe(i,i,Value(i<10?10:20),true);
        result=window.Snapshot().Single();
        check(result.Seconds==30 && result.Average==20 && result.Coverage==100,"rolling combat window trims older combat seconds");
        window.Observe(42,42,Value(100),true);
        check(window.Snapshot().Single().Coverage<100,"long polling gaps reduce coverage rather than extending old values");
        window.Clear();window.Observe(0,0,Value(-50),true);window.Observe(1,1,Value(-50),false);
        check(window.Snapshot().Single().Average==null && window.Snapshot().Single().Coverage==0,"paused capture does not fabricate zero bonuses");
        window.Clear();check(window.Snapshot().Length==0,"window reset removes previous run and weapon observations");
        var hits=new HitBonusWindow();
        hits.Add(0,true,true);hits.Add(1,false,true);hits.Add(2,null,true);hits.Add(3,true,false);
        var hitResult=hits.Snapshot(3);
        check(hitResult.Average==-25 && hitResult.Coverage==50 && hitResult.Hits==4 && hitResult.Basis=="hits",
            "hit average gives each verified hit equal weight and excludes unknown conditions and ownership");
        hits.Clear();hits.Add(0,true,true);hits.Add(1,null,true);
        check(hits.Snapshot(1).Average==0 && hits.Snapshot(1).Coverage==50,"non-weak hits remain unknown rather than assuming non-crit penalty");
        for(int i=2;i<40;i++)hits.Add(i,true,true);
        check(hits.Snapshot(40).Coverage==100 && hits.Snapshot(40).Average==0,"hit window evicts old unknown observations after 30 combat seconds");
        var tile=new ContributorTile("Scroll","Hawkeye Sight (Lv 1)","-50%","Conditional penalty","scroll",5720);
        var presented=EffectAveragePresentation.Apply("Weapon DMG buffs",tile,new[]{hitResult});
        check(presented.Value=="-25%" && presented.Description.Contains("50% of 4 hits observed") && presented.Description.Contains("hit-weighted") && presented.Description.Contains("Current / listed value: -50%"),
            "effect tile headline is observed average with coverage and listed value relegated to details");
        var unknown=EffectAveragePresentation.Apply("Weapon DMG buffs",tile,Array.Empty<BonusWindowResult>());
        check(unknown.Value=="—" && unknown.Description.Contains("No measured contribution"),"missing history cannot substitute a listed conditional bonus");
        var critMetric=EffectAveragePresentation.Apply("Critical Multiplier (multiplier)",tile,new[]{hitResult});
        check(critMetric.Value=="—","hit-based weapon penalty cannot overwrite a different metric of the same effect");
        var identity=EffectiveBonusView.SourceFor(32724,"",0);
        check(identity==("blessing",13514) && EffectiveBonusView.SourceFor(32725,"",0)==("",0),"only verified Oppressor state supplies missing owner identity");
        var owner=new CoverageFinding("blessing",13514,1,"Oppressor",ModifierCoverage.NeedsState,"","Blessing");
        var effect=new EffectiveBonusRow(275,"Oppressor - DMG Boost","Weapon DMG and Skill DMG +115%.",10,true,identity.Source,identity.Id);
        var merged=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{effect},new[]{owner},11);
        check(merged.Count==2 && merged.All(r=>r.Value==115) && merged.Any(r=>r.Metric=="Weapon DMG buffs"),"observed Oppressor fixture resolves owner and both bonuses");
        check(!EffectiveContributionMerge.Parse("Weapon DMG and Skill DMG +80% against burning enemies.").Any(),"paired parser does not promote target conditions");
        var luckyOwner=new CoverageFinding("gem",51762,1,"Lucky Shot",ModifierCoverage.Partial,"","Soul Jade");
        var original=new[]{new LscRow("Soul Jade","Lucky Shot · slot 1 (Lv 1)",12,"Temporary bonus",150)};
        var lucky=new EffectiveBonusRow(143,"Lucky Shot","+48% Lucky Shot Chance.",10,true,"gem",51762);
        var luck=EffectiveContributionMerge.MergeLuck(original,new[]{lucky},new[]{luckyOwner},11).Single();
        check(luck.Amount==48 && luck.Cap==150,"live Lucky Shot replaces base amount once while retaining cap");
        check(EffectiveContributionMerge.MergeLuck(original,new[]{lucky},new[]{luckyOwner},16).Single().Amount==12,
            "stale Lucky Shot falls back to labeled static contribution");
        var combinedLuck=EffectiveContributionMerge.MergeLuck(original,new[]{lucky},new[]{luckyOwner,luckyOwner},11).Single();
        check(combinedLuck.Amount==48 && combinedLuck.Name.Contains("combined"),
            "source-identified duplicate gems share one captured bonus without a per-copy assignment");
        check(!DamagePowerModel.Weapon.MayRelate("-50% DMG taken for 2s after hitting an enemy or breaking an object with Leap.") &&
            DamagePowerModel.Weapon.MayRelate("-50% DMG taken, +30% Weapon DMG."),"defensive-only effects are excluded while mixed offensive effects remain discoverable");
    }
}

