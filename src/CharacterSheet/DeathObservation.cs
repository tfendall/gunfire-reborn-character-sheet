// Receipt-time evidence only: final damage is not assumed to be extra damage.
internal sealed record DeathHit(double Time, long Packet, DamageIdentity Hit);
internal sealed record DeathSample(long Sequence, int Player, int Epoch, double Time,
    int Attacker, int Victim, int FinalDamage, DeathHit[] RecentHits, int CandidateCount,
    long HistoryEvictions, long QueueDrops);
internal static class DeathObservation
{
    private static readonly object gate = new();
    private static readonly Queue<DeathHit> hits = new();
    private static readonly Queue<DeathSample> deaths = new();
    private static int player, epoch;
    private static long sequence, evictions, drops;
    private static void Context(int currentPlayer, int currentEpoch)
    {
        if(player==currentPlayer && epoch==currentEpoch)return;
        hits.Clear(); deaths.Clear(); evictions=0; drops=0;
        player=currentPlayer; epoch=currentEpoch;
    }
    internal static void Hit(int currentPlayer,int currentEpoch,double time,long packet,DamageIdentity hit)
    {
        lock(gate)
        {
            Context(currentPlayer,currentEpoch);
            while(hits.Count>0 && time-hits.Peek().Time>10)hits.Dequeue();
            if(hits.Count>=1024) { hits.Dequeue(); evictions++; }
            hits.Enqueue(new(time,packet,hit));
        }
    }
    internal static void Death(int currentPlayer,int currentEpoch,double time,int attacker,int victim,int finalDamage)
    {
        lock(gate)
        {
            Context(currentPlayer,currentEpoch);
            if(deaths.Count>=128) { drops++; return; }
            var candidates=hits.Where(h=>h.Hit.Victim==victim && time>=h.Time && time-h.Time<=10).ToArray();
            deaths.Enqueue(new(++sequence,player,epoch,time,attacker,victim,finalDamage,
                candidates.TakeLast(8).ToArray(),candidates.Length,evictions,drops));
        }
    }
    internal static DeathSample[] Drain(int currentPlayer,int currentEpoch)
    {
        lock(gate)
        {
            Context(currentPlayer,currentEpoch);
            var result=new List<DeathSample>();
            while(result.Count<16 && deaths.Count>0)result.Add(deaths.Dequeue() with {QueueDrops=drops});
            return result.ToArray();
        }
    }
}
