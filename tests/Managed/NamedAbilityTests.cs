internal static class NamedAbilityTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach(var level in new[]{1,2,3})
        {
            ModifierRuntime.Reset();ModifierRuntime.Begin();ModifierRuntime.Add("ascension",3002,level,"Ascension");ModifierRuntime.Commit("ascensions");
            double expected=level==1 ? 70 : level==2 ? 140 : 280;
            var row=ModifierRuntime.Rows.Single();
            check(row.Metric=="Primary Skill DMG buffs" && row.Value==expected && row.Layer==ModifierLayer.Additive,
                "Turn into Reality level "+level+" reaches additive primary damage through shared named grammar");
            var window=new BonusCombatWindow();
            for(int i=0;i<=30;i++)window.Observe(i,i,HeadlineBonuses.Observe(ModifierRuntime.Rows,ModifierRuntime.Findings),true);
            check(HeadlineBonuses.Value("Primary Skill DMG buffs",window.Snapshot())=="+"+expected+"%",
                "named primary damage reaches the 30-second headline");
            check(ContributorTiles.Create("Primary Skill DMG buffs",ModifierRuntime.Rows,Array.Empty<LscRow>(),ModifierRuntime.Findings).Single().Name.Contains("Turn into Reality"),
                "named primary bonus appears in primary contributor breakdown");
        }
        ModifierRuntime.Reset();ModifierRuntime.Begin();
        ModifierRuntime.Add("ascension",3002,1,"Ascension");ModifierRuntime.Add("ascension",3007,1,"Ascension");ModifierRuntime.Commit("ascensions");
        var secondary=ModifierRuntime.Rows.Single(r=>r.Metric=="Secondary Skill Base DMG buffs");
        check(secondary.Value==150 && secondary.Layer==ModifierLayer.Base,"Essence Chain base bonus maps to secondary base damage without changing layer");
        var totals=HeadlineBonuses.Observe(ModifierRuntime.Rows,ModifierRuntime.Findings);
        check(totals.Single(r=>r.Metric=="Primary Skill DMG buffs").Value==70 && totals.Single(r=>r.Metric=="Secondary Skill Base DMG buffs").Value==150 &&
            totals.Single(r=>r.Metric=="Secondary Skill DMG buffs").Value==0,"base and additive damage stay in separate headline totals");
        check(ModifierCatalog.Entries[("ascension",2702,1)].Rules.Single().Metric=="Primary Skill Base DMG buffs" &&
            ModifierCatalog.Entries[("ascension",2707,1)].Rules.Single().Metric=="Secondary Skill Base DMG buffs",
            "another hero's Flying Sword and Fatal Bloom map to distinct primary and secondary base metrics");
        check(BuildSheetModel.Relevant("Primary Skill DMG buffs","+70% Soul Strike DMG.","ascension") &&
            !BuildSheetModel.Relevant("Primary Skill DMG buffs","+70% Soul Strike DMG.","gem"),"named coverage is source-aware");
        var unknown=new ModifierDefinition("Test","+70% Soul Strike DMG when shields are full.",Array.Empty<ModifierRule>(),"needs-state");
        check(NamedAbilityBonuses.Project("ascension",unknown).Rules.Length==0 && BuildSheetModel.Relevant("Primary Skill DMG buffs",unknown.Description,"ascension"),
            "conditional named damage stays unresolved but is included in primary coverage");
        var seasonal=ModifierCatalog.Entries[("gem",51792,1)].Rules.Single();
        check(seasonal.Scope=="Ability: Energy Orb" && seasonal.Layer==ModifierLayer.Delivery && !seasonal.Metric.StartsWith("Primary"),
            "seasonal orb delivery is not relabeled as hero primary damage");
        var source=new CoverageFinding("ascension",3002,1,"Turn into Reality",ModifierCoverage.Calculated,"","Ascension");
        var live=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{new EffectiveBonusRow(1,"Turn into Reality","+90% Soul Strike DMG.",10,true,"ascension",3002)},new[]{source},10).Single();
        check(live.Metric=="Primary Skill DMG buffs" && live.Value==90,"live named status values use the same ability mapping as static rules");
        var baseOwner=new CoverageFinding("ascension",3007,1,"Shattered Soul",ModifierCoverage.Calculated,"","Ascension");
        var baseLive=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{new EffectiveBonusRow(2,"Shattered Soul","+180% Base DMG of Essence Chain.",10,true,"ascension",3007)},new[]{baseOwner},10).Single();
        check(baseLive.Metric=="Secondary Skill Base DMG buffs" && baseLive.Layer==ModifierLayer.Base && baseLive.Value==180,
            "live named base damage preserves its base layer rather than becoming additive");
        check(NamedAbilityBonuses.Project("ascension",unknown with {Description="+70% Soul Strike Total DMG."}).Rules.Length==0,
            "total damage factors cannot be promoted to additive primary damage");
        ModifierRuntime.Reset();
    }
}
