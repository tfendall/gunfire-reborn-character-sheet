static void Check(bool condition, string test) { if (!condition) throw new Exception(test); Console.WriteLine("PASS " + test); }
DamageCaptureTests.Run(Check);
var normal = new CombatWindow();
for (int i = 0; i < 100; i++) normal.Add(new(i, 221, i < 60 ? 1 : 2, i < 20));
var a = normal.Measure(100);
Check(a.Count == 100 && a.Lsc == 40 && a.WeakRate == 20 && a.Damage == 22100, "40% estimate, weak frequency, integer damage units; identical hits retained");
var mixed = new CombatWindow();
for (int i = 0; i < 100; i++) mixed.Add(new(i, 1, i % 2 == 0 ? 2 : 3, false));
Check(mixed.Measure(100).Lsc == 150, "150% estimate across Lucky Shot tiers");
mixed.Add(new(100, 1, 0, false)); mixed.Add(new(101, 1, 1, false, false)); mixed.Add(new(102, -1, 4, false));
Check(mixed.Measure(102).Count == 100 && mixed.Measure(102).Lsc == 150 && mixed.Samples.Count == 102, "unmapped tiers excluded; non-damage ignored");
var nona = new CombatWindow();
for (int i = 0; i < 326; i++) nona.Add(new(i, 100, 1, false, false));
for (int i = 0; i < 62; i++) nona.Add(new(326 + i, 100, 3, false));
for (int i = 0; i < 100; i++) nona.Add(new(388 + i, 100, 4, false));
for (int i = 0; i < 12; i++) nona.Add(new(488 + i, 100, 5, false));
Check(nona.Measure(499).Count == 174 && Math.Abs(nona.Measure(499).Lsc!.Value - 271.2643678160919) < 0.000001, "Nona regression: exclude skill events and include fifth-tier damage multiplier");
var skillWindow = new CombatWindow();
skillWindow.Add(new(1, 100, 1, false, false, true));
skillWindow.Add(new(2, 100, 2, false, false, true));
skillWindow.Add(new(3, 100, 4, false, true, false));
Check(skillWindow.MeasureSkills(3).Count == 2 && skillWindow.MeasureSkills(3).Lsc == 50 && skillWindow.Measure(3).Lsc == 300, "skill and weapon outcome estimates stay separate");
var baseRows = new List<LscRow> { new("weapon", "weapon", 20, ""), new("blessing", "Field Proficiency", 100, "") };
foreach (int level in new[] { 2, 2, 1 })
{
    var gem = LscCatalog.Entries[("gem", 51762, level)];
    baseRows.Add(new("gem", gem.Name, gem.Flat, gem.Description));
}
var conditional = LscCatalog.Entries[("ascension", 3315, 2)];
baseRows.Add(new("ascension", conditional.Name, conditional.Flat, conditional.Description));
Check(LscTotals.Known(baseRows) == 168 && conditional.Flat == null, "base accounts for gem levels; conditional Nona ascension excluded");
var capped = LscCatalog.Entries[("scroll", 5829, 1)];
baseRows.Add(new("scroll", capped.Name, capped.Flat, capped.Description, capped.Cap));
Check(LscTotals.Known(baseRows) == 150, "known-base subtotal honors reviewed scroll cap");
BuildBonuses.Reset(); BuildBonuses.Begin();
BuildBonuses.Add("gem", 51759, 1, "Soul Jade"); BuildBonuses.Add("gem", 51759, 1, "Soul Jade"); BuildBonuses.Commit("gems");
BuildBonuses.Begin(); BuildBonuses.Add("scroll", 5827, 1, "Scroll"); BuildBonuses.Commit("scrolls");
Check(BuildBonuses.Totals().Single(x => x.Metric == "Skill DMG buffs").Value == 152, "stacked base skill bonuses across gems and scrolls");
BuildBonuses.Begin(); BuildBonuses.Add("ascension", 5201, 1, "Ascension"); BuildBonuses.Commit("ascensions");
Check(!BuildBonuses.Totals().Any(x => x.Metric.Contains("Total") || x.Metric.Contains("Final")) && BuildBonuses.Rows.Any(x => x.Metric == "Final Skill DMG" && x.Value == -50), "total/final layers and penalties stay separate");
Check(!BuildCatalog.Entries.ContainsKey(("scroll",5849,1)) && !BuildCatalog.Entries.ContainsKey(("blessing",13540,1)), "history-dependent and specific-skill bonuses not counted as general flat buffs");
Check(BuildCatalog.Entries[("ascension",3207,3)].Bonuses.Single().Metric == "Falling Star Base DMG" && BuildCatalog.Entries[("ascension",3207,3)].Bonuses.Single().Value == 600, "ability-specific base damage stays separate from general skill buffs");
Check(BuildCatalog.Entries[("ascension",2907,1)].Bonuses.Single().Metric == "Striking Punch Base DMG" && BuildCatalog.Entries[("ascension",2907,1)].Bonuses.Single().Value == 100, "Striking Punch ascension contributes its actual base bonus");
BuildBonuses.Begin(); BuildBonuses.Add("ascension",2904,1,"Ascension"); BuildBonuses.Commit("targets");
Check(BuildBonuses.Notes.Any(n => n.Section == "Target effects" && n.Text.Contains("+25%") && n.Text.Contains("+50%")) && !BuildBonuses.Rows.Any(r => r.Name.Contains(BuildNotesCatalog.Entries[("ascension",2904,1)].Name)), "target-dependent amplification displayed without becoming a global damage bonus");
BuildBonuses.Begin(); BuildBonuses.Add("gem",51870,1,"Soul Pendant"); BuildBonuses.Commit("pendant");
Check(BuildBonuses.Notes.Any(n => n.Section == "Triggered effects" && n.Text.Contains("two True Damage hits")), "equipped pendant trigger mechanics remain visible");
BuildBonuses.Begin(); BuildBonuses.Add("gem",51766,1,"Soul Jade"); BuildBonuses.Commit("other");
Check(BuildBonuses.Notes.Any(n => n.Name.StartsWith(BuildNotesCatalog.Entries[("gem",51766,1)].Name)), "unmapped build-related item is not silently omitted");
BuildBonuses.Abort("gems");
Check(BuildBonuses.Totals().Single(x => x.Metric == "Skill DMG buffs").Value == 120, "failed source cannot retain stale general damage bonuses");
var runs = new RunBoundary();
var playerWeaponTile=ContributorTiles.Create("lsc",Array.Empty<BuildContribution>(),new[]{new LscRow("Weapon","Equipped weapon + static inscriptions",50,"Inscription bonuses are included in this property; not added twice.")},Array.Empty<CoverageFinding>()).Single();
Check(playerWeaponTile.Name=="Equipped weapon" && playerWeaponTile.Value=="+50%" && playerWeaponTile.Description=="", "weapon contributor uses player-facing wording without implementation notes");
Check(PanelSpacing.HeaderHeight(24,false)==56 && PanelSpacing.HeaderHeight(40,false)==64 && PanelSpacing.HeaderHeight(24,true)==80, "shared header dimensions reserve consistent padding and only the required wrapped height");
Check(PanelSpacing.RowGap==8 && PanelSpacing.ExpandedPadding==16 && PanelSpacing.ContentInset==16 && PanelSpacing.RowInset==14, "mock spacing is encoded in the implementation instead of inherited oversized gaps");
Check(PanelSpacing.Columns(358)==6 && PanelSpacing.Columns(300)==4, "contributor tile wrapping reserves both content insets and icon gaps");
var tileFinding=new CoverageFinding("scroll",5817,2,"Bullet Light",ModifierCoverage.Partial,"","Scroll");
var tileList=ContributorTiles.Create("lsc",Array.Empty<BuildContribution>(),new[]{new LscRow("Scroll","Bullet Light (Lv 2)",25,"")},new[]{tileFinding});
Check(tileList.Length==1 && tileList[0].Value=="+25%" && tileList[0].Description.Contains("Lucky Shot"), "contributor tiles merge measured value and conditional description without duplicating an item");
var unresolvedTiles=ContributorTiles.Create("lsc",Array.Empty<BuildContribution>(),Array.Empty<LscRow>(),new[]{new CoverageFinding("blessing",13721,1,"Skilled Trick",ModifierCoverage.NeedsState,"","Blessing")});
Check(unresolvedTiles.Single().Value=="—" && unresolvedTiles.Single().Description.Contains("Weapon Skill"), "unmeasured contributor remains available as a selectable explanatory tile");
var stableEmpty=BuildSheetModel.Stable(Array.Empty<BuildContribution>());
var stablePopulated=BuildSheetModel.Stable(new[]{new BuildContribution("Weapon DMG buffs","Scroll","Example",75)});
Check(BuildSheetModel.MainMetrics.All(key=>stableEmpty.Any(row=>row.Key==key) && stablePopulated.Any(row=>row.Key==key)), "main modifier rows are present consistently across empty and populated builds");
Check(stablePopulated.Single(row=>row.Key=="Weapon DMG buffs").Value=="+75%" && stableEmpty.Single(row=>row.Key=="Weapon DMG buffs").Value=="+0%", "stable rows retain actual totals and zero known modifiers");
Check(BuildSheetModel.Relevant("lsc","Gain Lucky Shot Chance per consumed ammo") && BuildSheetModel.Relevant("Next Weapon Skill DMG","Next Weapon Skill DMG +6%") && !BuildSheetModel.Relevant("Weapon DMG buffs","Next Weapon Skill DMG +6%"), "conditional contributor descriptions attach to their relevant stat without merging weapon skill and weapon damage");
var attributed=new DamageLedger();attributed.Add(100,0x400800,58,17);attributed.Add(50,0x400400,101,353);attributed.Add(25,0x400800,999,1);
Check(attributed.Sources["Secondary skill · Fatal Bloom"]==100 && attributed.Sources["Seasonal effects"]==50 && attributed.Sources["Skills (combined)"]==25 && attributed.Total==attributed.Cells.Values.Sum(), "explicit server markers separate known ability/seasonal effects and preserve unresolved skill damage and totals");
var weaponSkillRule=new ModifierRule("Next Weapon Skill DMG",6,ModifierUnit.Percent,ModifierLayer.Additive,"Weapon Skill",ModifierCondition.BuffResource,StateIds:new[]{39940},Resource:"pendant:MaxEnergy",ResourceStep:10,PerResource:1.2,StackLimit:8);
Check(Math.Abs(ModifierEvaluator.Value(weaponSkillRule,null,8,60)!.Value-105.6)<1e-9 && ModifierEvaluator.Value(weaponSkillRule,null,0,null)==0, "resource-scaled stack bonus matches game tooltip and clears on consumption");
Check(ModifierEvaluator.Value(weaponSkillRule,null,8,null)==null && ModifierEvaluator.Value(weaponSkillRule,null,null,60)==null, "compound formula requires both fresh inputs rather than inventing a bonus");
ModifierRuntime.Reset();ModifierRuntime.Begin();ModifierRuntime.Add("gem",51807,1,"Soul Jade");ModifierRuntime.Commit("gems");
ModifierRuntime.Resources("pendant",new(){{"pendant:MaxEnergy",60}});ModifierRuntime.Buffs("server",new(){{39940,new(8,0,true)}},false);
Check(Math.Abs(ModifierRuntime.Rows.Single().Value-105.6)<1e-9, "partial snapshot can resolve a present measured state without treating absent states as zero");
ModifierRuntime.Buffs("server",new(),false);Check(ModifierRuntime.Rows.Count==0, "absent state in partial snapshot remains unknown");
ModifierRuntime.Begin();ModifierRuntime.Add("gem",51781,1,"Soul Jade");ModifierRuntime.Commit("other");
Check(ModifierRuntime.Findings.Single(f=>f.Id==51781).Status!=ModifierCoverage.Calculated, "unmapped definition cannot be falsely reported as calculated");
Check(ModifierCatalog.Entries.Keys.Count==ModifierCatalog.Entries.Keys.Distinct().Count() && ModifierCatalog.Entries.Values.All(d=>d.Rules.All(r=>double.IsFinite(r.Amount) && (!r.Cap.HasValue || r.Cap.Value>=0))), "whole modifier catalogue has unique identities and valid finite rules");
Check(BuildCatalog.Entries.All(pair=>ModifierCatalog.Entries.TryGetValue(pair.Key,out var model) && pair.Value.Bonuses.All(b=>model.Rules.Any(r=>r.Metric==b.Metric && r.Amount==b.Value))), "all reviewed legacy damage mappings survive migration into the shared engine");
Check(ModifierCatalog.Entries[("ascension",3101,1)].Rules.Single().Scope=="Ability: Blazing Meteor" && ModifierCatalog.Entries[("ascension",3102,1)].Rules.Single().Scope.StartsWith("Target:"), "ability and target applicability are represented explicitly in modifier rules");
Check(ModifierCatalog.Entries.Where(pair=>pair.Key.Source=="ascension").Count()==758, "coverage includes every audited hero ascension level rather than only mapped effects");
Check(ModifierCatalog.Entries[("ascension",3007,1)].Rules.Single().Amount==150 && ModifierCatalog.Entries[("ascension",3601,1)].Rules.Single().Amount==200, "strict shared grammar covers previously missing named abilities across heroes");
Check(ModifierCatalog.Entries[("scroll",5848,1)].Rules.Length==0 && ModifierCatalog.Entries[("blessing",13540,1)].Rules.Length==0, "history-changing crit bonus and ambiguously scoped Iron Wing bonus remain unresolved");
var unknownRule=new ModifierRule("Skill DMG buffs",200,ModifierUnit.Percent,ModifierLayer.Additive,"Skill",ModifierCondition.Unresolved);
Check(ModifierEvaluator.Value(unknownRule,null)==null, "unknown condition stays unknown instead of producing zero or a made-up total");
var stackRule=ModifierCatalog.Entries[("blessing",13721,1)].Rules.Single();
Check(ModifierEvaluator.Value(stackRule,null,4)==80 && ModifierEvaluator.Value(stackRule,null,99)==200 && ModifierEvaluator.Value(stackRule,null)==null, "shared buff evaluator applies stack multiplier and cap while preserving missing state");
ModifierRuntime.Reset();ModifierRuntime.Begin();ModifierRuntime.Add("blessing",13721,1,"Blessing");ModifierRuntime.Commit("blessings");
Check(ModifierRuntime.Luck.Single().Amount==null && ModifierRuntime.Findings.Single().Status==ModifierCoverage.NeedsState, "equipped stack effect is diagnosed when the state snapshot is missing");
ModifierRuntime.Buffs("server",new Dictionary<int,BuffMeasurement>{{1646,new(4,10,false)}});
Check(ModifierRuntime.Luck.Single().Amount==80 && ModifierRuntime.Findings.Single().Status!=ModifierCoverage.NeedsState, "primitive authoritative buff snapshot resolves equipped Lucky Shot stacks");
Check(ModifierRuntime.Stacks(stackRule,ModifierRuntime.Now+6000)==null, "stale buff snapshot becomes unknown rather than remaining active");
ModifierRuntime.Buffs("server",new Dictionary<int,BuffMeasurement>{{1646,new(4,0.1,false)}});
Check(ModifierRuntime.Stacks(stackRule,ModifierRuntime.Now+1000)==0, "cached timed buff expires without another native read");
ModifierRuntime.Buffs("server",new Dictionary<int,BuffMeasurement>());
Check(ModifierRuntime.Luck.Single().Amount==0, "complete fresh snapshot distinguishes inactive zero stacks from unavailable state");
ModifierRuntime.Buffs("server",null);
Check(ModifierRuntime.Luck.Single().Amount==null, "failed or partial snapshot invalidates previously measured stack bonus");
ModifierRuntime.Begin();ModifierRuntime.Add("gem",51762,1,"Soul Jade");ModifierRuntime.Commit("gems");
Check(ModifierRuntime.Luck.Count==1, "legacy Lucky Shot base and new engine never double-count the same jade");
ModifierRuntime.Begin();ModifierRuntime.Add("ascension",999999,1,"Ascension");ModifierRuntime.Commit("new-unknown");
Check(ModifierRuntime.Findings.Any(f=>f.Status==ModifierCoverage.MissingDefinition), "new game definitions missing from the catalogue are reported automatically");
ModifierRuntime.Abort("new-unknown");Check(!ModifierRuntime.Findings.Any(f=>f.Id==999999), "failed or removed ownership source cannot leave stale coverage entries");
var unitSheet=BuildSheetModel.Create(new[]{new BuildContribution("Critical Multiplier (multiplier)","Scroll","Hawkeye",2),new BuildContribution("Maximum HP (flat)","Ascension","Survival Instinct",20)});
Check(unitSheet.Any(row=>row.Value=="+2×") && unitSheet.Any(row=>row.Value=="+20" && row.Section=="RECOVERY & DEFENSE"), "different units and defense grouping survive the build presentation model");
BuildBonuses.Reset();BuildBonuses.Begin();BuildBonuses.Add("scroll",5703,1,"Scroll");BuildBonuses.Commit("scrolls");
Check(!BuildBonuses.Totals().Any() && BuildSheetModel.Create(BuildBonuses.Rows).Single().Section=="INDEPENDENT MODIFIERS", "shield doubling is an independent factor rather than an additive percentage subtotal");
var sheetExample = BuildSheetModel.Create(new[] {
    new BuildContribution("Skill DMG buffs","Scroll","Spirit Bible",120),
    new BuildContribution("Skill DMG buffs","Soul Jade","Spirit Boost",36),
    new BuildContribution("Chain Lightning Bounces (flat)","Ascension","Tesla Coil",2),
    new BuildContribution("Weapon DMG after empty reload (6s)","Scroll","Reload scroll",40),
    new BuildContribution("Final DMG","Scroll","A",20),
    new BuildContribution("Final DMG","Scroll","B",30),
});
Check(sheetExample.Single(row=>row.Key=="Skill DMG buffs").Value=="+156%" && sheetExample.Single(row=>row.Key=="Skill DMG buffs").Contributors.Length==2, "approved sheet sums same-scope bonuses and preserves contributor expansion");
Check(sheetExample.Any(row=>row.Section=="CHAIN LIGHTNING" && row.Name=="Bounces" && row.Value=="+2"), "ability modifiers group under named ability and preserve flat units");
Check(sheetExample.Any(row=>row.Section=="CONDITIONAL BONUSES" && row.Name.Contains("empty reload (6s)")), "conditional sheet rows retain trigger and duration");
Check(sheetExample.Count(row=>row.Section=="INDEPENDENT MODIFIERS")==2 && sheetExample.Select(row=>row.Key).Distinct().Count()==sheetExample.Length, "independent damage factors remain separate with unique expander keys");
Check(BuildCatalog.Entries[("ascension",2111,1)].Bonuses.Single().Metric.Contains("vs enemies") && BuildCatalog.Entries[("ascension",2111,1)].Bonuses.Single().Value == 30, "elemental target damage retains target scope rather than becoming a general bonus");
Check(BuildCatalog.Entries[("scroll",5876,1)].Bonuses.Single().Metric.Contains("empty reload (6s)") && BuildCatalog.Entries[("ascension",2109,3)].Bonuses.Single().Metric.Contains("elemental hit"), "timed bonuses preserve trigger and duration in their metric");
Check(BuildCatalog.Entries[("gem",51792,1)].Bonuses.Single().Value == 50 && BuildCatalog.Entries[("scroll",5703,1)].Bonuses.Single().Value == 100, "current run double-cast chance and doubled shield are mapped in their own categories");
Check(BuildCatalog.Entries[("ascension",2125,1)].Bonuses.Single().Value == 40 && BuildCatalog.Entries[("ascension",2126,2)].Bonuses.Single().Value == 80, "element damage grammar includes both prefix and suffix percentage definitions");
Check(BuildCatalog.Entries[("ascension",2105,3)].Bonuses.Single().Metric == "Energy Orb DMG" && !BuildCatalog.Entries.ContainsKey(("ascension",3312,3)), "named attack bonus is scoped; Iron Wing damage is not counted as general player damage");
Check(BuildCatalog.Entries[("ascension",2316,1)].Bonuses.All(b => b.Metric.EndsWith("(flat)") && b.Value == 2), "secondary ability capacity and bounces use flat units");
BuildBonuses.Reset(); BuildBonuses.SetWeapon(42);
BuildBonuses.Begin(); BuildBonuses.Add("blessing",13533,1,"Blessing"); BuildBonuses.Commit("blessings");
Check(!BuildBonuses.Totals().Any(), "ammo-dependent blessing excluded until magazine sample is available");
BuildBonuses.SetMagazine(42,20);
Check(BuildBonuses.Totals().Single().Value == 100, "First Strike derives current bonus from remaining magazine rounds");
BuildBonuses.Begin(); BuildBonuses.Add("gem",51763,1,"Soul Jade"); BuildBonuses.Commit("gems");
Check(BuildBonuses.Totals().Single().Value == 124, "dynamic weapon bonus adds to static weapon bonus without double counting");
BuildBonuses.SetMagazine(42,5); Check(BuildBonuses.Totals().Single().Value == 49, "magazine depletion updates dynamic subtotal");
BuildBonuses.SetWeapon(43); BuildBonuses.SetMagazine(42,99);
Check(BuildBonuses.Totals().Single().Value == 24, "weapon switch clears ammo and rejects old weapon samples");
BuildBonuses.SetMagazine(43,10); BuildBonuses.Abort("blessings");
Check(BuildBonuses.Totals().Single().Value == 24, "removed or failed blessing source removes dynamic bonus");
Check(!runs.Observe(false, 0) && runs.Observe(true, 100), "first run boundary detected");
Check(!runs.Observe(true, 100) && !runs.Observe(true, 100), "same run/room transitions preserve samples");
Check(!runs.Observe(false, 100) && runs.Observe(true, 100), "new run detected even if player and battle IDs are reused");
Check(runs.Observe(true, 101), "battle number change catches missed menu transition");
var delayed = new RunBoundary();
Check(delayed.Observe(true, 0) && !delayed.Observe(true, 500), "late battle ID assignment does not reset twice");
var uiSafety = new PanelSafety();
uiSafety.SetOpen(true);
Check(uiSafety.CanDraw && !uiSafety.CanBlockInput, "input never blocked before first successful draw");
uiSafety.DrawSucceeded();
Check(uiSafety.CanBlockInput, "successful panel can block clicks behind it");
uiSafety.Fail(); uiSafety.DrawSucceeded(); uiSafety.SetOpen(true);
Check(!uiSafety.CanDraw && !uiSafety.CanBlockInput, "render/init failure stays disabled across polls and draw callbacks");
uiSafety.SetOpen(false); uiSafety.SetOpen(true);
Check(uiSafety.CanDraw && !uiSafety.CanBlockInput, "reopening retries with input initially disabled");
var world = new WorldReadiness();
Check(!world.Observe(1,true,false,false,false,0) && world.Observe(1,true,false,false,false,2), "world reads require two seconds of settled gameplay");
Check(world.Observe(1,true,true,false,true,3) && world.Observe(1,true,true,true,false,4), "normal C screen preserves readiness");
Check(!world.Observe(2,false,true,false,false,5) && !world.Observe(2,false,true,false,true,6), "C cannot make an unloading scene ready");
Check(!world.Observe(2,true,true,false,false,7) && !world.Observe(2,true,true,false,true,8), "C cannot reopen stats while loading cursor is active");
Check(!world.Observe(2,true,false,false,false,9) && world.Observe(2,true,false,false,false,11), "reads resume after transition settles");
var ledger = new DamageLedger();
var displayWorld = new WorldReadiness();
displayWorld.Observe(1,true,false,false,false,0); displayWorld.Observe(1,true,false,false,false,2);
Check(displayWorld.DisplayReady, "cached panel becomes available after initial scene readiness");
displayWorld.Observe(1,true,true,false,false,3);
Check(displayWorld.DisplayReady && !displayWorld.Ready, "merchant cursor pauses native reads without delaying cached panel visibility");
displayWorld.Observe(1,true,false,false,true,3.1);
Check(displayWorld.DisplayReady && !displayWorld.Ready, "C immediately after a menu displays cached stats without bypassing read cooldown");
displayWorld.Observe(2,false,false,true,true,4);
Check(!displayWorld.DisplayReady && !displayWorld.Ready, "scene transition still suspends panel and native reads");
ledger.Add(100, 0x800400, 30001); ledger.Add(50, 0x400800, 10); ledger.Add(25, 0x220400, 0);
Check(ledger.Total == 175 && ledger.Sources.Values.Sum() == 175 && ledger.Elements.Values.Sum() == 175, "damage source and element partitions never double count");
Check(ledger.Cells.Values.Sum() == 175 && ledger.Cells[("Weapon hits", "Fire")] == 100 && ledger.Cells[("Burning", "Fire")] == 25, "matrix preserves source and element dimensions without double counting");
Check(ledger.Sources.All(source => ledger.Cells.Where(cell => cell.Key.Source == source.Key).Sum(cell => cell.Value) == source.Value) && ledger.Elements.All(element => ledger.Cells.Where(cell => cell.Key.Element == element.Key).Sum(cell => cell.Value) == element.Value), "matrix rows and columns match ledger partitions");
Check(Math.Abs(ledger.Share(ledger.Sources["Weapon hits"]) - 100.0 / 175 * 100) < 0.00001 && ledger.Total == 175, "cumulative shares do not decay during idle time");
ledger.Add(0, 0x800800, 30001); ledger.Add(-100, 0x800800, 30001);
Check(ledger.Total == 175, "cumulative ledger ignores non-damage events");
ledger.Clear(); Check(ledger.Total == 0 && ledger.Sources.Count == 0 && ledger.Elements.Count == 0, "manual reset clears all damage totals");
Check(ledger.Cells.Count == 0, "reset clears matrix cells");
var bounded = new CombatWindow { MaxHits = 3, MaxAge = 10 };
var barLedger = new DamageLedger();
barLedger.Add(32000,0x800800,30001); barLedger.Add(18000,0x800400,30001);
barLedger.Add(6000,0x800100,30001); barLedger.Add(4000,0x800200,1);
barLedger.Add(30000,0x400400,1); barLedger.Add(2000,0x400100,1,0x170);
barLedger.Add(7000,0x201000,0); barLedger.Add(1000,0,0);
var bars = DamageTabModel.Create(barLedger);
Check(bars.Select(b=>b.Name).SequenceEqual(new[]{"Weapon damage","Skill damage","Damage over time","Other damage"}) && bars.Select(b=>b.Amount).SequenceEqual(new double[]{60000,32000,7000,1000}), "damage tab separates known damage over time from unidentified damage without losing damage");
Check(bars.Sum(b=>b.Amount)==barLedger.Total && Math.Abs(bars.Sum(b=>b.RunPercent)-100)<0.00001, "damage tab source amounts and percentages conserve the run total");
Check(Math.Abs(bars[0].Elements.Sum(e=>e.RunFraction)-0.6)<0.00001 && Math.Abs(bars[1].Elements.Sum(e=>e.RunFraction)-0.32)<0.00001, "bar filled lengths use the run total rather than normalizing every source to full width");
Check(Math.Abs(bars[0].Elements[0].SourcePercent-100.0*32/60)<0.00001 && bars.All(b=>Math.Abs(b.Elements.Sum(e=>e.SourcePercent)-100)<0.00001), "expanded element percentages use each source as their denominator");
Check(bars.Skip(2).All(b=>b.Elements.Length==1 && b.Elements[0].Element=="Other") && bars.Skip(2).Sum(b=>b.Elements[0].Amount)==8000, "true and unknown elements remain Other without obscuring their distinct damage sources");
Check(DamageTabModel.Create(new DamageLedger()).Length==0, "empty damage tab avoids undefined percentages");
for (int i = 0; i < 5; i++) bounded.Add(new(i, 1, 1, false));
Check(bounded.Samples.Count == 3 && bounded.Samples.Peek().Time == 2, "rolling event limit");
Check(bounded.Measure(13).Count == 2 && bounded.Measure(15).Lsc == null, "time window expires during idle");
bounded.Add(new(16, 1, 2, false)); bounded.Clear();
Check(bounded.Measure(16).Count == 0, "reset removes previous build samples");
ReadBudget.Begin(3, 1000);
Check(ReadBudget.Step() && ReadBudget.Step() && ReadBudget.Step() && !ReadBudget.Step(), "shared traversal node limit");
var testDir = Path.Combine(Path.GetTempPath(), "GunfireStatsSink-" + Guid.NewGuid());
Directory.CreateDirectory(testDir);
try
{
    using (var sink = new TraceSink(Path.Combine(testDir, "trace")))
    {
        for (int i = 0; i < 10020; i++)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!sink.TryWrite(i.ToString()))
            {
                if (timeout.Elapsed.TotalSeconds > 5) throw new Exception("Trace worker stalled: " + sink.Error);
                Thread.Sleep(1);
            }
        }
        Check(sink.Error == "", "background writer accepts burst without producer blocking");
    }
    // Dispose only waits 100ms in production; give the background flush time in this test.
    Check(SpinWait.SpinUntil(() => File.Exists(Path.Combine(testDir, "trace-part1.jsonl")) && File.ReadAllLines(Path.Combine(testDir, "trace-part1.jsonl")).Length == 20, 5000), "background writer flushes and rotates");
    Check(File.ReadAllLines(Path.Combine(testDir, "trace.jsonl")).Length == 10000, "rotation preserves 10,000-record boundary");
    File.WriteAllText(Path.Combine(testDir, "blocked"), "not a directory");
    using var broken = new TraceSink(Path.Combine(testDir, "blocked", "trace"));
    Check(SpinWait.SpinUntil(() => broken.Error.Length > 0, 5000) && !broken.TryWrite("x"), "disk failure disables sink without throwing into game code");
}
finally { Directory.Delete(testDir, true); }

var inscriptionExamples=ModifierCatalog.Entries.Where(d=>d.Key.Item1=="inscription" && BuildSheetModel.Relevant("lsc",d.Value.Description)).Take(2).ToArray();
var weaponContributors=ContributorTiles.Create("lsc",Array.Empty<BuildContribution>(),new[]{new LscRow("Weapon","Equipped weapon",20,"")},inscriptionExamples.Select(d=>new CoverageFinding(d.Key.Item1,d.Key.Item2,d.Key.Item3,d.Value.Name,ModifierCoverage.NeedsState,"","Weapon inscription")),1510);
Check(weaponContributors.Length==1 && weaponContributors[0].IconSource=="weapon" && weaponContributors[0].IconId==1510 && weaponContributors[0].Value=="+20%", "multiple weapon inscriptions use one held-weapon tile without adding the property twice");
Check(inscriptionExamples.All(d=>weaponContributors[0].Description.Contains(d.Value.Description)), "one weapon tile retains all contributing inscription descriptions");
Check(bars[2].Contributions.Sum(c=>c.Amount)==7000 && bars[2].Contributions.Any(c=>c.Name=="Damage over time") && bars[3].Contributions.Single().Name=="Unidentified effects", "damage over time is labeled separately while genuinely unidentified effects remain Other");
Check(BuildSheetModel.Stable(Array.Empty<BuildContribution>()).Single(row=>row.Key=="Rate of Fire") is {Section:"ATTACK & MOVEMENT",Value:"+0%"}, "Rate of Fire stays visible even without known bonuses");
var fireRate=BuildSheetModel.Stable(new[]{new BuildContribution("Rate of Fire","Soul Jade","Rapidfire Upgrade (Lv 1)",5),new BuildContribution("Rate of Fire","Scroll","Example",20)}).Single(row=>row.Key=="Rate of Fire");
Check(fireRate.Value=="+25%" && fireRate.Contributors.Length==2 && BuildSheetModel.Relevant("Rate of Fire","+10% RoF for 5s"), "Rate of Fire sums known bonuses and attaches timed RoF descriptions");
var jadeTile=ContributorTiles.Create("Rate of Fire",new[]{new BuildContribution("Rate of Fire","Soul Jade","Rapidfire Upgrade (Lv 1)",5)},Array.Empty<LscRow>(),new[]{new CoverageFinding("gem",51771,1,"Rapidfire Upgrade",ModifierCoverage.Partial,"","Soul Jade")}).Single();
Check(jadeTile.IconSource=="gem" && jadeTile.IconId==51771 && jadeTile.Value=="+5%", "soul gem contributors retain their identity for icon configuration lookup");
GemArtworkIds.Observe("S8GemItemDict=playerScoped:[31:S8GemUnit{id=31,sid=2071,quality=1,pos=3,abilility=S8Ability{sid=51771,level=1,pos=0}},32:S8GemUnit{id=32,sid=999,quality=2,pos=4,abilility=S8Ability{sid=51758,level=2,pos=0}}]");
Check(GemArtworkIds.TryGet(51771,out var gemItemId) && gemItemId==2071 && GemArtworkIds.TryGet(51758,out gemItemId) && gemItemId==999, "gem artwork resolves captured item identity rather than passing ability IDs or assuming an offset");
GemArtworkIds.Observe("[budget limit]");
Check(!GemArtworkIds.TryGet(51800,out _) && GemArtworkIds.TryGet(51771,out _), "incomplete inventory cannot invent gem artwork identities");
var groupedBuild=BuildSheetModel.Stable(Array.Empty<BuildContribution>()).GroupBy(BuildSheetModel.Family).OrderBy(group=>BuildSheetModel.FamilyRank(group.Key)).ToArray();
Check(groupedBuild.Select(group=>group.Key).SequenceEqual(new[]{"WEAPON & LUCK","SKILLS","ELEMENTS","ATTACK & MOVEMENT"}) && groupedBuild.Sum(group=>group.Count())==BuildSheetModel.MainMetrics.Length, "compact build families keep every main modifier visible in a stable order");
Check(groupedBuild.Single(group=>group.Key=="SKILLS").Count()==4 && groupedBuild.Single(group=>group.Key=="ELEMENTS").Any(row=>row.Key=="Explosion DMG buffs"), "compact group counts preserve skill scopes and explosion damage");
