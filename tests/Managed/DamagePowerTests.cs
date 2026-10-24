internal static class DamagePowerTests
{
    internal static void Run(Action<bool,string> check)
    {
        var rows=new[] {
            new BuildContribution("Weapon DMG buffs","Scroll","Weapon bonus",250,ModifierLayer.Additive,ModifierUnit.Percent,"Weapon"),
            new BuildContribution("Final Weapon DMG","Ascension","Multiplier",50,ModifierLayer.Independent,ModifierUnit.Percent,"Weapon"),
            new BuildContribution("Weapon DMG after empty reload (6s)","Scroll","Conditional",40,ModifierLayer.Additive,ModifierUnit.Percent,"Weapon",ModifierCondition.Scoped),
            new BuildContribution("Fire DMG buffs","Scroll","Fire",20,ModifierLayer.Additive,ModifierUnit.Percent,"Element"),
            new BuildContribution("Skill DMG buffs","Scroll","Skill only",100,ModifierLayer.Additive,ModifierUnit.Percent,"Skill"),
            new BuildContribution("Falling Star Base DMG","Ascension","Ability only",250,ModifierLayer.Base,ModifierUnit.Flat,"Ability: Falling Star"),
            new BuildContribution("Rate of Fire","Scroll","Speed",25,ModifierLayer.Delivery,ModifierUnit.Percent,"General")
        };
        var entries=DamagePowerModel.Create(DamagePowerModel.Weapon,rows,Array.Empty<CoverageFinding>());
        check(entries.Length==5 && entries.Single(e=>e.Name=="Weapon bonus").Text.StartsWith("+250% Weapon Damage"),"weapon explanation shows actual bonus and excludes skill/ability-only rules");
        check(entries.Single(e=>e.Name=="Multiplier").Section=="INDEPENDENT MULTIPLIERS" &&
            entries.Where(e=>e.Name is "Conditional" or "Fire").All(e=>e.Section=="CONDITIONAL BONUSES" && e.Text.Contains("applicability not measured")),
            "independent and conditional effects never masquerade as universal additive damage");
        check(entries.Single(e=>e.Name=="Speed").Section=="CRITS & ATTACK RATE","attack rate is separate from per-hit damage");
        var liveElement=DamagePowerModel.Create(DamagePowerModel.Weapon,new[]{new BuildContribution("Fire DMG buffs","Scroll","Live fire",80,ModifierLayer.Additive,ModifierUnit.Percent,"Fire DMG buffs",ModifierCondition.BuffPresence)},Array.Empty<CoverageFinding>());
        check(liveElement.Single().Section=="CONDITIONAL BONUSES","game-evaluated element bonus remains visible and conditional with metric scope");
        BuildBonuses.Reset();BuildBonuses.Begin();
        BuildBonuses.Add("gem",51763,1,"Soul Jade");BuildBonuses.Add("gem",51763,1,"Soul Jade");BuildBonuses.Commit("gems");
        var partial=DamagePowerModel.Create(DamagePowerModel.Weapon,BuildBonuses.Rows,ModifierRuntime.Findings);
        check(partial.Count(e=>e.Section=="DAMAGE BONUSES" && e.Text.Contains("+24%"))==2 && partial.Any(e=>e.Text.Contains("Partial")),
            "duplicate gems retain both numeric contributions and disclose uncalculated clauses");
        check(BuildBonuses.Rows.All(r=>r.SourceKey=="gem" && r.SourceId==51763 && r.SourceLevel==1),"contribution identity survives runtime evaluation");
        BuildBonuses.Reset();BuildBonuses.Begin();BuildBonuses.Add("blessing",13533,1,"Blessing");BuildBonuses.Commit("blessings");
        var missing=DamagePowerModel.Create(DamagePowerModel.Weapon,BuildBonuses.Rows,ModifierRuntime.Findings);
        check(missing.All(e=>e.Section!="DAMAGE BONUSES") && missing.Any(e=>e.Text.Contains("Unresolved")),"missing magazine state is unresolved rather than a zero contribution");
        BuildBonuses.SetWeapon(10);BuildBonuses.SetMagazine(10,50);
        var active=DamagePowerModel.Create(DamagePowerModel.Weapon,BuildBonuses.Rows,ModifierRuntime.Findings);
        check(active.Any(e=>e.Text.Contains("+250% Weapon Damage") && e.Text.Contains("captured state")),"magazine-dependent bonus displays actual calculated amount");
        var inscription=new CoverageFinding("inscription",4801,1,"inscription 4801",ModifierCoverage.NeedsFormula,"","Weapon inscription");
        var weapon=DamagePowerModel.Create(DamagePowerModel.Weapon,Array.Empty<BuildContribution>(),new[]{inscription},new[]{new LscRow("Weapon","Equipped weapon",50,"",100)});
        check(weapon.Count(e=>e.Text.Contains("+50%"))==1 && weapon.Any(e=>e.Text.Contains("already be included")) && weapon.Any(e=>e.Text.Contains("cap: 100%")),
            "weapon component is shown once, inscriptions are unresolved, and caps remain visible");
        var otherProfile=new DamagePowerProfile("Skills",r=>r.Scope=="Skill",_=>false);
        check(DamagePowerModel.Create(otherProfile,rows,Array.Empty<CoverageFinding>()).Single().Name=="Skill only","explanation model supports other damage profiles without weapon-specific rendering");
        BuildBonuses.Reset();
    }
}
