internal static class AttributionGapTests
{
    internal static void Run(Action<bool,string> check)
    {
        var fodder=new CoverageFinding("gem",51809,2,"Movement Speed Fodder",ModifierCoverage.Partial,"","Soul Jade");
        var baseline=new[]{new BuildContribution("Movement Speed","Soul Jade","Movement Speed Fodder (Lv 2)",15,
            SourceKey:"gem",SourceId:51809,SourceLevel:2)};
        var speed=new EffectiveBonusRow(28809,"Movement Speed Fodder","Movement Speed +34%.",10,true,"gem",51809,StateSid:39988);
        foreach(double value in new[]{34d,40d,31d,22d})
        {
            var merged=EffectiveContributionMerge.Merge(baseline,new[]{speed with {Text=$"Movement Speed +{value}%."}},new[]{fodder},10);
            check(merged.Count==1 && merged[0].Value==value && merged[0].SourceId==51809 && merged[0].Condition==ModifierCondition.BuffPresence,
                $"Movement Speed Fodder live {value} replaces catalogue 15 exactly once");
        }
        var retained=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{speed},Array.Empty<CoverageFinding>(),10).Single();
        check(retained.Value==34 && retained.SourceLevel==0,"explicit speed source supports retained bonus after final jade is removed");
        check(EffectiveContributionMerge.Merge(baseline,new[]{speed},new[]{fodder},16).Single().Value==15,
            "stale movement observation cannot replace catalogue contribution");
        check(!EffectiveContributionMerge.Parse("+100% current Movement Speed.").Any() &&
            !EffectiveContributionMerge.Parse("+20% Movement Speed while moving.").Any(),
            "movement parser rejects current-speed and conditional scopes");

        var cube=new CoverageFinding("scroll",5725,1,"Elemental Cube",ModifierCoverage.NeedsFormula,"","Scroll");
        var effects=new[]{
            new EffectiveBonusRow(1,"Elemental Cube: Fire","+50% Fire DMG.",10,true,StateSid:1096),
            new EffectiveBonusRow(2,"Elemental Cube: Lightning","+50% Lightning DMG.",10,true,StateSid:1097),
            new EffectiveBonusRow(3,"Elemental Cube: Corrosion","+50% Corrosion DMG.",10,true,StateSid:1098)};
        foreach(var effect in effects)
        {
            var row=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{effect},new[]{cube},10).Single();
            check(row.SourceId==5725 && row.Value==50 && row.Metric==EffectiveContributionMerge.Parse(effect.Text).Single().Metric,
                effect.Name+" maps to owned Cube and retains element scope");
        }
        var pair=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),effects.Take(2),new[]{cube with {Level=2}},10);
        check(pair.Count==2 && pair.All(r=>r.SourceId==5725 && r.SourceLevel==2) && pair.Select(r=>r.Metric).Distinct().Count()==2,
            "two active Cube elements stay separate rather than becoming general elemental damage");
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),effects,Array.Empty<CoverageFinding>(),10).Count==0 &&
            EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),effects,new[]{cube,cube},10).Count==0,
            "Cube fallback requires one owned source and cannot invent retained ownership or copy shares");
        check(EffectiveContributionMerge.Owners(effects[0] with {StateSid=0},new[]{cube}).Length==0 &&
            EffectiveContributionMerge.Owners(effects[0] with {Source="scroll",SourceId=5807},new[]{cube}).Length==0,
            "Cube mapping requires verified state definition and never overrides explicit identity");
        var inactive=new InactiveBonusRules();
        inactive.Learn(1096,effects[0],new[]{cube});inactive.Learn(1097,effects[1],new[]{cube});
        var zeros=inactive.Observe(new[]{1097},true,new[]{cube},11);
        var switched=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{effects[1] with {Checked=11}}.Concat(zeros),new[]{cube},11);
        check(switched.Single(r=>r.Metric=="Fire DMG buffs").Value==0 && switched.Single(r=>r.Metric=="Lightning DMG buffs").Value==50,
            "Cube reload clears previous element without zeroing the active element");
        check(inactive.Observe(Array.Empty<int>(),false,new[]{cube},11).Length==0 &&
            EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),effects,new[]{cube},16).Count==0,
            "Cube absence requires complete scan and active contributions expire");

        var field=new CoverageFinding("scroll",5807,1,"Field Support",ModifierCoverage.NeedsState,"","Scroll");
        var unknown=new EffectiveBonusRow(4,"Field Support","",10,false,StateSid:1236);
        inactive.Learn(1236,unknown,new[]{field});
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{unknown},new[]{field},10).Count==0 &&
            inactive.Observe(Array.Empty<int>(),true,new[]{field},11).Length==0,
            "unresolved Field Support supplies neither a contribution nor learned inactive zero");
    }
}
