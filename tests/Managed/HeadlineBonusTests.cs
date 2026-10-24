internal static class HeadlineBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
        var hailOwner=new CoverageFinding("ascension",2216,1,"Hail of Bullets",ModifierCoverage.NeedsState,"","Ascension");
        var learned=new InactiveBonusRules();
        var hailState=new EffectiveBonusRow(42,"Hail of Bullets","Weapon DMG +30%, Lucky Shot Chance +20%.",0,true,"ascension",2216);
        check(learned.Observe(Array.Empty<int>(),true,new[]{hailOwner},0).Length==0,"unseen effects cannot be inferred inactive");
        // Arbitrary state ID proves the lifecycle has no per-effect whitelist.
        learned.Learn(987654,hailState,new[]{hailOwner});
        var zeros=learned.Observe(Array.Empty<int>(),true,new[]{hailOwner},1);
        check(zeros.Length==1 && EffectiveContributionMerge.Parse(zeros[0].Text).Count()==2 && EffectiveContributionMerge.Parse(zeros[0].Text).All(v=>v.Value==0),
            "generic learned status disappearance supplies both weapon and luck zeros");
        var hailWindow=new BonusCombatWindow();
        for(int i=0;i<=30;i++)
        {
            var states=i<6 ? new[]{hailState with {Checked=i}} : learned.Observe(Array.Empty<int>(),true,new[]{hailOwner},i);
            hailWindow.Observe(i,i,HeadlineBonuses.Observe(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),states,new[]{hailOwner},i),new[]{hailOwner}),true);
        }
        check(HeadlineBonuses.Value("Weapon DMG buffs",hailWindow.Snapshot())=="+6%","learned Hail-style lifecycle averages active and inactive time into headline");
        check(HeadlineBonuses.Detail("Weapon DMG buffs",hailWindow.Snapshot()).Contains("100% of time observed"),"coverage wording describes time instead of effect completeness");
        check(learned.Observe(new[]{987654},true,new[]{hailOwner},31).Length==0 && learned.Observe(Array.Empty<int>(),false,new[]{hailOwner},31).Length==0,
            "active unresolved status or incomplete scan never becomes absent zero");
        learned.Learn(987654,hailState with {Text="+30% Weapon DMG when magazine is full."},new[]{hailOwner});
        check(learned.Observe(Array.Empty<int>(),true,new[]{hailOwner},32).Length==0,"unresolved conditional description revokes learned zero");
        learned.Learn(987654,hailState,new[]{hailOwner});
        check(learned.Observe(Array.Empty<int>(),true,new[]{hailOwner with {Level=2}},33).Length==0,"ownership level changes invalidate learned state");
        learned.Learn(987654,hailState,new[]{hailOwner});learned.Clear();
        check(learned.Observe(Array.Empty<int>(),true,new[]{hailOwner},34).Length==0,"new run or scene clears learned absence claims");
        var full=EffectiveContributionMerge.Parse("Gain +75% Weapon DMG and Skill DMG. Each weapon shot that hits an enemy restores 1 Armor, up to 12 Armor per second.").ToArray();
        check(full.Length==2 && full.All(r=>r.Value==75),"Fully Equipped parses first sentence without importing recovery conditions");
        var last=EffectiveContributionMerge.Parse("+30% Weapon DMG and Skill DMG, +20% Rate of Fire, +10% DMG Resistance.").ToArray();
        check(last.Length==3 && last.Count(r=>r.Value==30)==2,"Last Stand contributes both damage bonuses and fire rate, excluding defense");
        check(EffectiveContributionMerge.Parse("+60% <b>All DMG</b> Dealt.").Single()==("All DMG buffs",60d),"Damage Boost markup resolves into a separate all-damage metric");
        check(!EffectiveContributionMerge.Parse("Gain +75% Weapon DMG and Skill DMG against burning enemies.").Any(),"conditional suffix remains guarded after parser expansion");
        var lastOwner=new CoverageFinding("ascension",2224,1,"Last Stand",ModifierCoverage.NeedsState,"","Ascension");
        var tracking=new InactiveBonusRules();
        tracking.Learn(32230,new(1,"Last Stand","+30% Weapon DMG and Skill DMG, +20% Rate of Fire.",9,true,"ascension",2224),new[]{lastOwner});
        var inactive=tracking.Observe(Array.Empty<int>(),true,new[]{lastOwner},10);
        check(inactive.Length==1 && EffectiveContributionMerge.Parse(inactive[0].Text).All(r=>r.Value==0),"complete absence supplies all reviewed Last Stand zero components");
        check(tracking.Observe(new[]{32230},true,new[]{lastOwner},10).Length==0 && tracking.Observe(Array.Empty<int>(),false,new[]{lastOwner},10).Length==0,
            "present unresolved states and incomplete scans never imply zero");
        check(tracking.Observe(Array.Empty<int>(),true,Array.Empty<CoverageFinding>(),10).Length==0 &&
            tracking.Observe(Array.Empty<int>(),true,new[]{lastOwner,lastOwner},10).Length==0,"zero requires unique current ownership");
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),inactive,new[]{lastOwner},11.1).Count==0,"inactive zeros expire after one second without refreshed scan evidence");
        tracking.Learn(32230,new(1,"Last Stand","+30% Weapon DMG and Skill DMG, +20% Rate of Fire.",9,true,"ascension",2224),new[]{lastOwner});
        var duty=new BonusCombatWindow();
        for(int i=0;i<=30;i++)
        {
            var state=i<6 ? new[]{new EffectiveBonusRow(1,"Last Stand","+30% Weapon DMG and Skill DMG, +20% Rate of Fire.",i,true,"ascension",2224)} :
                tracking.Observe(Array.Empty<int>(),true,new[]{lastOwner},i);
            duty.Observe(i,i,HeadlineBonuses.Observe(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),state,new[]{lastOwner},i),new[]{lastOwner}),true);
        }
        check(HeadlineBonuses.Value("Skill DMG buffs",duty.Snapshot())=="+6%" && HeadlineBonuses.Value("Rate of Fire",duty.Snapshot())=="+4%" &&
            HeadlineBonuses.Find("Skill DMG buffs",duty.Snapshot())!.Coverage==100,"six active seconds plus 24 confirmed inactive seconds yield full-window +6 damage and +4 fire rate");
        var fodder=new CoverageFinding("gem",51787,1,"Skill Fodder",ModifierCoverage.Partial,"","Soul Jade");
        var original=new[]{new BuildContribution("Skill DMG buffs","Soul Jade","Skill Fodder (Lv 1)",25,ModifierLayer.Additive,ModifierUnit.Percent,"Skill",SourceKey:"gem",SourceId:51787,SourceLevel:1),
            new BuildContribution("Skill DMG buffs","Soul Jade","Skill Fodder (Lv 1)",25,ModifierLayer.Additive,ModifierUnit.Percent,"Skill",SourceKey:"gem",SourceId:51787,SourceLevel:1)};
        var live=new EffectiveBonusRow(165,"Skill Fodder","+50% Skill DMG.",10,true,"gem",51787);
        var combined=EffectiveContributionMerge.Merge(original,new[]{live},new[]{fodder,fodder},11);
        check(combined.Count==1 && combined[0].Value==50,"run fixture: duplicate Skill Fodder state replaces both static gems once");
        var changed=EffectiveContributionMerge.Merge(original,new[]{live with {Text="+65% Skill DMG."}},new[]{fodder,fodder},11);
        check(changed.Single().Value==65,"combined Skill Fodder state carries increases beyond the original static subtotal");
        var arcane=new CoverageFinding("gem",51790,1,"Arcane Surge",ModifierCoverage.NeedsState,"","Soul Jade");
        var scoped=combined.Append(new BuildContribution("Primary Skill DMG buffs","Soul Jade","Arcane Surge (Lv 1)",12,ModifierLayer.Additive,ModifierUnit.Percent,"Primary Skill",ModifierCondition.BuffPresence,"gem",51790,1)).ToArray();
        var observations=HeadlineBonuses.Observe(scoped,new[]{fodder,fodder,arcane});
        check(observations.Single(r=>r.Metric=="Skill DMG buffs").Value==50 && observations.Single(r=>r.Metric=="Primary Skill DMG buffs").Value==12,
            "primary-only Arcane Surge remains separate from the general skill headline");
        var window=new BonusCombatWindow();window.Observe(0,0,observations,true);
        window.Observe(1,1,HeadlineBonuses.Observe(scoped.Append(new BuildContribution("Skill DMG buffs","Scroll","Temporary",100,ModifierLayer.Additive)),new[]{fodder,fodder,arcane}),true);
        window.Observe(2,2,observations,true);
        check(HeadlineBonuses.Value("Skill DMG buffs",window.Snapshot())=="+100%","headline averages synchronous total: +50 base and +100 active for half the window");
        check(HeadlineBonuses.Value("Primary Skill DMG buffs",window.Snapshot())=="+12%","independent scope headline retains its own combat average");
        check(HeadlineBonuses.Value("Skill DMG buffs",Array.Empty<BonusWindowResult>())=="—","headline waits for combat evidence rather than presenting current totals as averages");
        var unknown=HeadlineBonuses.Observe(Array.Empty<BuildContribution>(),new[]{arcane});
        check(unknown.Single(r=>r.Metric=="Primary Skill DMG buffs").Value==null,"unresolved owned primary effect is not a complete zero");
        var tile=new ContributorTile("Soul Jade","Skill Fodder (Lv 1)","+50%","","gem",51787);
        check(EffectAveragePresentation.Apply("Skill DMG buffs",tile,window.Snapshot()).Value=="—","aggregate history cannot be mistaken for an individual contributor's average");
    }
}

