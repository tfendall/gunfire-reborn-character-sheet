using Relic;
using Talent;
using Benediction;

internal static class BaseLsc
{
    private static readonly Dictionary<string, List<LscRow>> groups = new();
    private static readonly Dictionary<string, string> errors = new();
    private static int stage, playerId;
    internal static List<LscRow> Rows = new();
    internal static double Total;
    internal static string Status = "Known base only; complete effect coverage is not yet verified.";
    internal static void Reset() { groups.Clear(); errors.Clear(); Rows = new(); Total = 0; stage = 0; BuildBonuses.Reset(); Status = "Refreshing known base for the current run."; }
    internal static void Sample(NewPlayerObject hero, NewItemProp? weapon)
    {
        if (playerId != hero.ObjectID) { Reset(); playerId = hero.ObjectID; }
        BuildBonuses.SetWeapon(weapon?.ObjectID ?? 0);
        groups["weapon"] = new() { new("Weapon", "Equipped weapon + static inscriptions", weapon?.LuckyHit, weapon == null ? "No equipped weapon" : "Inscription bonuses are included in this property; not added twice.") };
        // Reuse the boxed numeric dictionary reader, rather than introducing a
        // new native getter solely for the expanded view.
        if (weapon == null) BuildBonuses.WeaponElementChance = null;
        BuildBonuses.CritX = weapon == null ? null : weapon.CrazyEff / 10000.0;
        BuildBonuses.WeaponDamage = weapon?.Att;
        string source = (stage++ % 4) switch { 0 => "scrolls", 1 => "gems", 2 => "ascensions", _ => "blessings" };
        CrashDiagnostics.Reading("base-" + source);
        ReadBudget.Begin(512, 2);
        BuildBonuses.Begin();
        try
        {
            var rows = new List<LscRow>();
            switch (source)
            {
                case "scrolls":
                    if (RelicManager.m_EquipRelic != null)
                        foreach (var pair in RelicManager.m_EquipRelic)
                        {
                            CheckBudget();
                            var relic = pair.Value;
                            if (relic != null && relic.Pid == hero.ObjectID && !relic.IsForceDisable)
                                AddEffect(rows, "scroll", relic.RelicID, relic.isEnhance ? 2 : 1, "Scroll");
                        }
                    break;
                case "gems":
                    var season = S8Manager._instance;
                    var dict = season?.S8GemItemDict;
                    if (dict != null && dict.ContainsKey(hero.ObjectID))
                        foreach (var pair in dict[hero.ObjectID])
                        {
                            CheckBudget();
                            var gem = pair.Value;
                            // Native isEquiped is an unsigned comparison against
                            // zero: preserve that exact rule without invoking it.
                            if (gem == null || gem.pos == 0 || gem.abilility == null) continue;
                            AddEffect(rows, "gem", gem.abilility.sid, gem.abilility.level, "Soul Jade", "slot " + gem.pos);
                        }
                    var pendants = season?.S8ThirdItemDict;
                    if (pendants != null && pendants.ContainsKey(hero.ObjectID))
                        foreach (var pair in pendants[hero.ObjectID])
                        {
                            CheckBudget();
                            var abilities = pair.Value?.abilityList;
                            if (abilities == null) continue;
                            foreach (var ability in abilities)
                            {
                                CheckBudget();
                                if (ability != null && ability.sid != 0)
                                    BuildBonuses.Add("gem", ability.sid, ability.level, "Soul Pendant");
                            }
                        }
                    break;
                case "ascensions":
                    if (TalentManager.m_TalentDict != null)
                        foreach (var pair in TalentManager.m_TalentDict)
                        {
                            CheckBudget();
                            var talent = pair.Value;
                            if (talent != null) AddEffect(rows, "ascension", talent.TalentID, talent.BaseGrade, "Ascension");
                        }
                    break;
                case "blessings":
                    var blessings = BenedictionManager.SaveBenedictionDict;
                    if (blessings != null && blessings.ContainsKey(hero.ObjectID))
                        foreach (var pair in blessings[hero.ObjectID])
                        {
                            CheckBudget();
                            if (pair.Value != null && pair.Value.BenedictionSID != 13706)
                                AddEffect(rows, "blessing", pair.Value.BenedictionSID, 1, "Blessing");
                        }
                    break;
            }
            groups[source] = rows;
            BuildBonuses.Commit(source);
            errors.Remove(source);
        }
        catch (Exception ex)
        {
            groups.Remove(source); // Do not silently keep a stale contribution.
            BuildBonuses.Abort(source);
            errors[source] = source + " snapshot incomplete";
            TraceStore.Write("base-lsc-error", source + ": " + ex.Message);
        }
        BuildBonuses.Refresh();
        Rows = groups.Values.SelectMany(x => x).Concat(ModifierRuntime.Luck).ToList();
        Total = LscTotals.Known(Rows);
        TraceStore.Write("modifier-coverage-summary", string.Join("; ", ModifierRuntime.Findings.GroupBy(f=>f.Status).Select(group=>group.Key+"="+group.Count())));
        foreach(var finding in ModifierRuntime.Findings)
            TraceStore.Write("modifier-coverage-"+finding.Source+"-"+finding.Id+"-"+finding.Level,
                finding.Name+"="+finding.Status+" · "+finding.Reason);
        TraceStore.Write("build-bonuses", string.Join("; ", BuildBonuses.Rows.Select(row => row.Metric + "=" + row.Value + " (" + row.Source + ":" + row.Name + ")")));
        Status = "Known modifiers plus freshly measured stack effects; unresolved conditions are excluded.";
        if (groups.Count < 5 || errors.Count > 0) Status += " Some sources are still refreshing.";
        TraceStore.Write("base-lsc", "known=" + Total + " " + string.Join("; ", Rows.Select(x => x.Source + ":" + x.Name + "=" + (x.Amount?.ToString() ?? "conditional/unresolved"))));
        CrashDiagnostics.ReadDone("base-" + source);
    }
    private static void CheckBudget()
    {
        if (!ReadBudget.Step()) throw new InvalidOperationException("Read budget reached; retry next sweep.");
    }
    private static void AddEffect(List<LscRow> rows, string source, int id, int level, string category, string suffix = "")
    {
        BuildBonuses.Add(source, id, level, category);
        if (!LscCatalog.Entries.TryGetValue((source, id, level), out var effect)) return;
        if (!effect.Flat.HasValue && ModifierCatalog.Entries.TryGetValue((source,id,level),out var definition) && definition.Rules.Any(rule=>rule.Metric=="Lucky Shot Chance")) return;
        string name = effect.Name + (suffix.Length > 0 ? " · " + suffix : "") + " (Lv " + level + ")";
        rows.Add(new(category, name, effect.Flat, effect.Description, effect.Cap));
    }
}
