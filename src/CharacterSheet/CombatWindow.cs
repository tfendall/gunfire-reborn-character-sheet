// Pure managed aggregation, also compiled by the standalone validation harness.
internal readonly record struct DamageSample(double Time, long RawDamage, int Lucky, bool Weak, bool WeaponHit = true, bool SkillHit = false);
internal sealed class CombatWindow
{
    internal readonly Queue<DamageSample> Samples = new();
    internal int MaxHits = 500;
    internal double MaxAge = 600;
    internal void Clear() => Samples.Clear();
    internal void Add(DamageSample sample)
    {
        if (sample.RawDamage <= 0) return;
        Samples.Enqueue(sample);
        Trim(sample.Time);
    }
    internal void Trim(double now)
    {
        while (Samples.Count > 0 && (Samples.Count > MaxHits || now - Samples.Peek().Time > MaxAge)) Samples.Dequeue();
    }
    internal (int Count, double? Lsc, double WeakRate, double Damage) Measure(double now)
    {
        Trim(now);
        var eligible = Samples.Where(x => x.WeaponHit && x.Lucky >= 1).ToArray();
        return (eligible.Length, eligible.Length == 0 ? null : 100 * eligible.Average(x => x.Lucky - 1),
            Samples.Count == 0 ? 0 : 100.0 * Samples.Count(x => x.Weak) / Samples.Count,
            Samples.Sum(x => (double)x.RawDamage));
    }
    internal (int Count, double? Lsc) MeasureSkills(double now)
    {
        Trim(now);
        var samples = Samples.Where(x => x.SkillHit && x.Lucky >= 1).ToArray();
        return (samples.Length, samples.Length == 0 ? null : 100 * samples.Average(x => x.Lucky - 1));
    }
}
