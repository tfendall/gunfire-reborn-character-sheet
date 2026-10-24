internal sealed record BonusObservation(string Key,string Name,string Metric,double? Value,bool Complete=true);
internal sealed record BonusWindowResult(string Key,string Name,string Metric,double? Average,double? Peak,double? Uptime,double Coverage,double Seconds,string Basis="time",int Hits=0,bool Partial=false);

// Integrate prior observations over elapsed combat time, never backfill a new value.
internal sealed class BonusCombatWindow
{
    private sealed record Segment(double Start,double End,BonusObservation[] Values);
    private readonly Queue<Segment> segments=new();
    private BonusObservation[] previous=Array.Empty<BonusObservation>();
    private double last=double.NaN, combat, previousHit=double.NegativeInfinity;
    private bool previousValid;
    internal double Seconds=>Math.Min(30,combat);
    internal void Clear(){segments.Clear();previous=Array.Empty<BonusObservation>();last=double.NaN;combat=0;previousValid=false;previousHit=double.NegativeInfinity;}
    internal void Observe(double now,double lastWeaponHit,IEnumerable<BonusObservation> values,bool valid)
    {
        if(!double.IsFinite(now))return;
        if(!double.IsNaN(last) && now<last)Clear();
        if(!double.IsNaN(last))
        {
            double priorEnd=previousHit+2;
            double duration=Math.Max(0,Math.Min(now,priorEnd)-last);
            if(lastWeaponHit>previousHit)
                duration+=Math.Max(0,Math.Min(now,lastWeaponHit+2)-Math.Max(Math.Max(last,lastWeaponHit),priorEnd));
            if(duration>0)
            {
                var observed=previousValid && valid && now-last<=1 ? previous : previous.Select(v=>v with {Value=null}).ToArray();
                segments.Enqueue(new(combat,combat+duration,observed));combat+=duration;
                while(segments.Count>0 && segments.Peek().End<=combat-30)segments.Dequeue();
                // Very sparse hits can leave tiny fragments. Eviction reduces coverage.
                while(segments.Count>2048)segments.Dequeue();
            }
        }
        previous=values.ToArray();previousValid=valid;last=now;previousHit=lastWeaponHit;
    }
    internal BonusWindowResult[] Snapshot()
    {
        double start=Math.Max(0,combat-30),seconds=Seconds;
        if(seconds<=0)return Array.Empty<BonusWindowResult>();
        var names=segments.SelectMany(s=>s.Values).GroupBy(v=>v.Key).Select(g=>g.Last());
        return names.Select(item=>
        {
            double known=0,weighted=0,active=0;double? peak=null;bool partial=false;
            foreach(var segment in segments)
            {
                double duration=segment.End-Math.Max(start,segment.Start);
                var observation=segment.Values.SingleOrDefault(v=>v.Key==item.Key);
                var value=observation?.Value;
                if(observation?.Complete==false)partial=true;
                if(!value.HasValue || !double.IsFinite(value.Value))continue;
                known+=duration;weighted+=duration*value.Value;if(value.Value!=0)active+=duration;
                peak=peak.HasValue ? Math.Max(peak.Value,value.Value) : value;
            }
            return new BonusWindowResult(item.Key,item.Name,item.Metric,known>0?weighted/known:null,peak,known>0?100*active/known:null,100*known/seconds,seconds,Partial:partial);
        }).OrderBy(r=>r.Name).ThenBy(r=>r.Metric).ToArray();
    }
}
