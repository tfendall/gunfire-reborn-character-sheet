internal sealed record WeaponBonusHit(long Sequence,double Time,int Weapon,bool Weakpoint);

// Hit conditions use one vote per accepted weapon hit, never damage weighting.
internal sealed class HitBonusWindow
{
    private sealed record Sample(double Combat, double? Value);
    private readonly Queue<Sample> samples=new();
    private double last=double.NaN,combat;
    private int evicted;
    internal void Clear(){samples.Clear();last=double.NaN;combat=0;evicted=0;}
    internal void Add(double time,bool? critical,bool owned)
    {
        if(!double.IsFinite(time) || (!double.IsNaN(last) && time<last))return;
        if(!double.IsNaN(last))combat+=Math.Min(2,time-last);
        last=time;
        // Reviewed catalogue clause: normal Hawkeye Sight, -50% on non-crit hits.
        samples.Enqueue(new(combat,owned && critical.HasValue ? (critical.Value ? 0 : -50) : null));
        while(samples.Count>0 && samples.Peek().Combat<combat-30)samples.Dequeue();
        if(samples.Count>8192){samples.Dequeue();evicted++;}
    }
    internal BonusWindowResult Snapshot(double now)
    {
        double end=combat+(double.IsNaN(last)?0:Math.Clamp(now-last,0,2));
        var recent=samples.Where(s=>s.Combat>=end-30).ToArray();
        var known=recent.Where(s=>s.Value.HasValue).Select(s=>s.Value!.Value).ToArray();
        return new("scroll:5720:1:Weapon DMG buffs","Hawkeye Sight (Lv 1)","Weapon DMG buffs",
            known.Length>0 && evicted==0 ? known.Average() : null,known.Length>0 ? known.Max() : null,
            known.Length>0 ? 100.0*known.Count(v=>v!=0)/known.Length : null,
            recent.Length>0 && evicted==0 ? 100.0*known.Length/recent.Length : 0,Math.Min(30,end),"hits",recent.Length);
    }
}
