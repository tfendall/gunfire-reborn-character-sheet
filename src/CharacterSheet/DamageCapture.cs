using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using DYPublic.Duonet;

internal static class DamageCapture
{
    private readonly record struct Incoming(double Time, int Attacker, int Action, int Victim, int Type, long Damage, int Abnormal, int BreakShield, int Weaked, int Lucky, int Extra, int Weapon);
    private static readonly ConcurrentQueue<Incoming> incoming = new();
    private static readonly CombatWindow window = new();
    internal static readonly DamageLedger Ledger = new();
    private static readonly Queue<DamageSample> dps = new();
    private static readonly Stopwatch clock = Stopwatch.StartNew();
    private static double nextSummary, dpsTotal;
    private static double nextHealth;
    private static int dpsDrops;
    private static int queued, dropped, sequence;
    private static double started;
    internal static string Status = "Damage observer waiting for F8 in a run.";
    internal static string Summary = "No local damage events yet.";
    internal static double? WeaponLsc, SkillLsc;
    internal static int WeaponSamples, SkillSamples;
    internal static double WeaponDps, SkillDps, AverageWeaponHit, AverageSkillHit;
    private static double weaponDpsTotal, skillDpsTotal;
    internal static void Enqueue(s2cnetwar_GS2CWStatusHPClass data)
    {
        // Copy primitives only; no logging or game-object lookups inside the hook.
        if (!Panel.Capture || !Panel.RunActive || data.iAttack != Panel.LocalPlayerId || Panel.LocalPlayerId == 0) return;
        if (System.Threading.Interlocked.Increment(ref queued) > 8192)
        {
            System.Threading.Interlocked.Decrement(ref queued);
            System.Threading.Interlocked.Increment(ref dropped);
            return;
        }
        incoming.Enqueue(new(clock.Elapsed.TotalSeconds, data.iAttack, data.iActNum, data.iVictim, data.iType,
            data.iHP, data.iAbnormal, data.iBreakShield, data.iWeaked, data.iLuckyHitEff, data.iExInfo, Panel.CurrentWeaponId));
    }
    internal static void Reset()
    {
        for (int i = 0; i < 8192 && incoming.TryDequeue(out _); i++) System.Threading.Interlocked.Decrement(ref queued);
        window.Clear(); dps.Clear(); dpsTotal = 0; started = clock.Elapsed.TotalSeconds; nextSummary = 0;
        Ledger.Clear();
        Summary = "No local damage events yet.";
        WeaponLsc = SkillLsc = null; WeaponSamples = SkillSamples = 0;
        WeaponDps = SkillDps = AverageWeaponHit = AverageSkillHit = weaponDpsTotal = skillDpsTotal = 0;
        TraceStore.Marker("combat-window-reset");
    }
    internal static void Drain()
    {
        double now = clock.Elapsed.TotalSeconds;
        long frameStart = Stopwatch.GetTimestamp();
        for (int i = 0; i < 128 && (Stopwatch.GetTimestamp() - frameStart) < Stopwatch.Frequency / 1000 && incoming.TryDequeue(out var hit); i++)
        {
            System.Threading.Interlocked.Decrement(ref queued);
            if (hit.Attacker != Panel.LocalPlayerId || hit.Time < started) continue;
            Ledger.Add(hit.Damage, hit.Type, hit.Action, hit.Extra);
            // Native GetHurtPartStr tests the low nibble for 1 to mark weak hits.
            bool weak = (hit.Type & 15) == 1;
            // DamageSource class and client action range come from this build's
            // ServerDefine / SkillManager constants. Actionless damage can carry
            // the weapon flag too; retain it in DPS, not in the weapon LSC roll.
            bool weaponHit = (hit.Type & 0xF00000) == 0x800000 && hit.Action >= 30000 && hit.Action <= 65500;
            bool skillHit = (hit.Type & 0xF00000) == 0x400000;
            var sample = new DamageSample(hit.Time, hit.Damage, hit.Lucky, weak, weaponHit, skillHit);
            window.Add(sample);
            if (hit.Damage > 0)
            {
                dps.Enqueue(sample); dpsTotal += hit.Damage;
                AddDps(sample, 1);
                if (dps.Count > 8192) { var removed = dps.Dequeue(); dpsTotal -= removed.RawDamage; AddDps(removed, -1); dpsDrops++; }
            }
            // Preserve flags raw. Their native text helpers and skill lookups are
            // intentionally kept out of per-hit processing; poll skills separately.
            TraceStore.Write("damage-event", FormattableString.Invariant($"seq={++sequence} mono={hit.Time:F3} attacker={hit.Attacker} action={hit.Action} victim={hit.Victim} type={hit.Type} hpRaw={hit.Damage} damageDisplayUnits={hit.Damage} luckyEff={hit.Lucky} weak={weak} weakRaw={hit.Weaked} abnormal={hit.Abnormal} breakShield={hit.BreakShield} extraRaw={hit.Extra} weaponAtReceipt={hit.Weapon}"));
        }
        if (now < nextSummary) return;
        nextSummary = now + 0.25;
        while (dps.Count > 0 && now - dps.Peek().Time > 30) { var removed = dps.Dequeue(); dpsTotal -= removed.RawDamage; AddDps(removed, -1); }
        if (Panel.Capture && now >= nextHealth)
        {
            nextHealth = now + 5;
            TraceStore.Write("capture-health", $"damageQueuePending={queued} damageQueueDrops={dropped} dpsCapDrops={dpsDrops} processed={sequence} {TraceStore.Health}");
        }
        var result = window.Measure(now);
        var skills = window.MeasureSkills(now);
        WeaponLsc = result.Lsc; WeaponSamples = result.Count;
        SkillLsc = skills.Lsc; SkillSamples = skills.Count;
        double seconds = Math.Min(30, Math.Max(1, now - started));
        WeaponDps = Math.Max(0, weaponDpsTotal) / seconds; SkillDps = Math.Max(0, skillDpsTotal) / seconds;
        var weaponHits = window.Samples.Where(x => x.WeaponHit).ToArray();
        var skillHits = window.Samples.Where(x => x.SkillHit).ToArray();
        AverageWeaponHit = weaponHits.Length == 0 ? 0 : weaponHits.Average(x => (double)x.RawDamage);
        AverageSkillHit = skillHits.Length == 0 ? 0 : skillHits.Average(x => (double)x.RawDamage);
        if (window.Samples.Count == 0) { Summary = "No recent local damage events."; return; }
        string estimate = result.Lsc.HasValue ? result.Lsc.Value.ToString("F1", CultureInfo.InvariantCulture) + "%" : "unavailable";
        Summary = FormattableString.Invariant($"Observed WEAPON LSC: {estimate} ({result.Count} identified weapon events)\nWindow: last 500 damage events / 10 min; mapping PROVISIONAL\nExcluded from LSC: {window.Samples.Count - result.Count} skill/other/unmapped events\nWeak-point rate (all events): {result.WeakRate:F1}% · events: {window.Samples.Count}\nLocal damage DPS (30s, includes idle): {Math.Max(0, dpsTotal) / seconds:F1}\nDamage queue drops: {dropped} · DPS cap drops: {dpsDrops}");
    }
    private static void AddDps(DamageSample sample, int direction)
    {
        if (sample.WeaponHit) weaponDpsTotal += direction * (double)sample.RawDamage;
        if (sample.SkillHit) skillDpsTotal += direction * (double)sample.RawDamage;
    }
}

[HarmonyPatch(typeof(FollowTextManager), nameof(FollowTextManager.WStatusHP))]
public static class DamageEventProbe
{
    public static void Prefix(s2cnetwar_GS2CWStatusHPClass data)
    {
        try { DamageCapture.Enqueue(data); } catch { }
    }
}
