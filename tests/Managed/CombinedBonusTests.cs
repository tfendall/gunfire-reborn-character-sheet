internal static class CombinedBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
        var owners=new[]{new CoverageFinding("gem",51788,1,"Weapon Fodder",ModifierCoverage.Partial,"","Soul Jade"),
            new CoverageFinding("gem",51788,2,"Weapon Fodder",ModifierCoverage.Partial,"","Soul Jade"),
            new CoverageFinding("gem",51788,2,"Weapon Fodder",ModifierCoverage.Partial,"","Soul Jade")};
        var baseline=owners.Select(o=>new BuildContribution("Weapon DMG buffs",o.Category,o.Name+" (Lv "+o.Level+")",o.Level==1 ? 25 : 40,
            SourceKey:o.Source,SourceId:o.Id,SourceLevel:o.Level)).ToArray();
        var state=new EffectiveBonusRow(1,"Weapon Fodder","+120% Weapon DMG.",10,true,"gem",51788);
        var retained=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state with {Text="+55% Weapon DMG."}},Array.Empty<CoverageFinding>(),10).Single();
        check(retained.Value==55 && retained.Name.EndsWith(" (retained status)") && retained.SourceLevel==0,
            "explicit live source bonus survives selling the final copy without inventing an equipped level");
        var retainedWindow=new BonusCombatWindow();
        for(int i=0;i<=30;i++)retainedWindow.Observe(i,i,HeadlineBonuses.Observe(new[]{retained},Array.Empty<CoverageFinding>()),true);
        check(HeadlineBonuses.Value("Weapon DMG buffs",retainedWindow.Snapshot())=="+55%","retained bonus contributes to the combat average without inventory ownership");
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},Array.Empty<CoverageFinding>(),16).Count==0 &&
            EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state with {Source="",SourceId=0}},Array.Empty<CoverageFinding>(),10).Count==0,
            "retained bonus still requires fresh state and explicit source identity");
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state,state with {Id=2}},Array.Empty<CoverageFinding>(),10).Count==0,
            "conflicting retained states remain unresolved");
        var retainedTile=ContributorTiles.Create("Weapon DMG buffs",new[]{retained},Array.Empty<LscRow>(),Array.Empty<CoverageFinding>()).Single();
        check(retainedTile.IconId==51788 && retainedTile.Description.Contains("no longer equipped"),"retained contribution has source artwork identity and honest attribution");
        var merged=EffectiveContributionMerge.Merge(baseline,new[]{state},owners,10);
        check(merged.Count==1 && merged[0].Value==120 && merged[0].SourceLevel==0 && merged[0].Name.Contains("combined · 3 copies"),
            "mixed-level duplicate gems replace 105 catalogue points with one captured 120-point combined contribution");
        check(HeadlineBonuses.Observe(merged,owners).Single(r=>r.Metric=="Weapon DMG buffs") is {Value:120,Complete:true},
            "combined contribution resolves the headline for all owned copies without multiplying it");
        var tile=ContributorTiles.Create("Weapon DMG buffs",merged,Array.Empty<LscRow>(),owners).Single();
        check(tile.Value=="+120%" && tile.Description.Contains("individual shares are unknown"),"combined attribution is one tile with no invented per-copy split");
        var combinedHistory=new[]{new BonusWindowResult("gem:51788:0:Weapon DMG buffs",merged[0].Name,"Weapon DMG buffs",120,120,100,100,30),
            new BonusWindowResult("gem:51788:1:Weapon DMG buffs","Weapon Fodder (Lv 1)","Weapon DMG buffs",null,null,null,0,30)};
        check(EffectAveragePresentation.Apply("Weapon DMG buffs",tile,combinedHistory).Value=="+120%",
            "combined tile selects combined history even while old per-copy unknown history remains in the window");
        var window=new BonusCombatWindow();
        window.Observe(0,0,HeadlineBonuses.Observe(merged,owners),true);
        window.Observe(1,1,HeadlineBonuses.Observe(merged,owners),true);
        check(HeadlineBonuses.Value("Weapon DMG buffs",window.Snapshot())=="+120%","captured combined value reaches averaged headline");
        check(EffectiveContributionMerge.Merge(baseline,new[]{state,state with {Id=2,Text="+90% Weapon DMG."}},owners,10).Sum(r=>r.Value)==105,
            "competing live states remain ambiguous rather than summed or selected");
        check(EffectiveContributionMerge.Merge(baseline,new[]{state with {Source="",SourceId=0}},owners,10).Sum(r=>r.Value)==105,
            "shared name alone cannot establish a combined bonus");
        check(EffectiveContributionMerge.Merge(baseline,new[]{state},owners,16).Sum(r=>r.Value)==105,
            "stale combined reading cannot replace current catalogue values");
        var other=baseline.Append(new BuildContribution("Weapon DMG buffs","Soul Jade","Different source",24,SourceKey:"gem",SourceId:51763,SourceLevel:1));
        check(EffectiveContributionMerge.Merge(other,new[]{state},owners,10).Sum(r=>r.Value)==144,"replacement preserves unrelated source contributions");
        var scroll=new CoverageFinding("scroll",5791,1,"Merciless Combo",ModifierCoverage.NeedsState,"","Scroll");
        var scrollOwners=new[]{scroll,scroll};
        var scrollState=new EffectiveBonusRow(2,"Merciless Combo","+75% Weapon DMG.",10,true,"scroll",5791);
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{scrollState},scrollOwners,10).Single().Value==75,
            "combined merge also works for a different source type without an effect ID exception");
        var lifecycle=new InactiveBonusRules();lifecycle.Learn(1190,scrollState,scrollOwners);
        var zero=lifecycle.Observe(Array.Empty<int>(),true,scrollOwners,11);
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),zero,scrollOwners,11).Single().Value==0,
            "learned disappearance preserves combined zero without requiring unique copies");
        check(lifecycle.Observe(Array.Empty<int>(),true,new[]{scroll},12).Length==0,"ownership count changes invalidate learned combined zero");
        var luckOwners=owners.Select(o=>o with {Id=51762,Name="Lucky Shot"}).ToArray();
        var luckRows=luckOwners.Select(o=>new LscRow("Soul Jade","Lucky Shot (Lv "+o.Level+")",12,"listed")).ToArray();
        var luckState=state with {Name="Lucky Shot",SourceId=51762,Text="+60% Lucky Shot Chance."};
        var luck=EffectiveContributionMerge.MergeLuck(luckRows,new[]{luckState},luckOwners,10);
        check(luck.Count==1 && luck[0].Amount==60 && luck[0].Name.Contains("combined"),"combined Lucky Shot replaces all duplicate listed amounts once");
    }
}
