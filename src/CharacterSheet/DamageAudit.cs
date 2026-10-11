// Managed diagnostic accounting only. Never contributes damage to the ledger.
internal readonly record struct DamageIdentity(int Attacker, int Action, int Victim, int Type,
    long Damage, int Abnormal, int Shield, int Weak, int Lucky, int Extra);
internal readonly record struct DamageAuditKey(string Outcome, int Attacker, int? Owner, int Action, int Type, int Extra);
internal sealed record DamageAuditBucket(DamageAuditKey Key, long Count, decimal RawDamage);
internal sealed record DamageAuditSnapshot(long Serial, int Generation, DamageAuditBucket[] Buckets, long OverflowEvents);
internal static class DamageAudit
{
    internal static bool Enabled;
    private static readonly object gate = new();
    private static readonly Dictionary<DamageAuditKey, DamageAuditBucket> buckets = new();
    private static readonly Dictionary<string, DamageAuditBucket> overflow = new();
    private static long serial, packetSequence, overflowEvents;
    private static int generation;
    [ThreadStatic] private static Packet? active;
    internal sealed class Packet
    {
        internal long Id;
        internal int Generation, Calls;
        internal DamageIdentity Hit;
        internal Packet? Parent;
    }
    internal static void Reset()
    {
        lock(gate) { generation++; buckets.Clear(); overflow.Clear(); overflowEvents=0; }
    }
    internal static void Record(string outcome, DamageIdentity hit, int? owner = null)
    {
        if(!Enabled)return;
        lock(gate)
        {
            var key=new DamageAuditKey(outcome,hit.Attacker,owner,hit.Action,hit.Type,hit.Extra);
            if(!buckets.TryGetValue(key,out var old) && buckets.Count>=512)
            {
                overflowEvents++;
                old=overflow.GetValueOrDefault(outcome);
                overflow[outcome]=new(new(outcome,0,null,0,0,0),(old?.Count??0)+1,(old?.RawDamage??0)+hit.Damage);
            }
            else buckets[key]=new(key,(old?.Count??0)+1,(old?.RawDamage??0)+hit.Damage);
        }
    }
    internal static Packet? Begin(DamageIdentity hit)
    {
        if(!Enabled)return null;
        lock(gate)
        {
            var packet=new Packet{Id=++packetSequence,Generation=generation,Hit=hit,Parent=active};
            active=packet; Record("packet-arrival",hit); return packet;
        }
    }
    internal static long Observe(DamageIdentity hit)
    {
        if(!Enabled)return 0;
        lock(gate)
        {
            if(active is { } packet && packet.Generation==generation && packet.Hit==hit)
            {
                Record(++packet.Calls==1?"packet-matched":"packet-duplicate-callback",hit);
                return packet.Id;
            }
            Record(active==null?"callback-without-packet":"callback-payload-mismatch",hit);
            return 0;
        }
    }
    internal static void End(Packet? packet)
    {
        if(packet==null)return;
        lock(gate)
        {
            try { if(packet.Generation==generation && packet.Calls==0)Record("packet-without-callback",packet.Hit); }
            finally { active=packet.Parent; }
        }
    }
    internal static DamageAuditSnapshot Take()
    {
        lock(gate)
        {
            var result=new DamageAuditSnapshot(++serial,generation,buckets.Values.Concat(overflow.Values.Select(x=>
                x with { Key=x.Key with { Outcome=x.Key.Outcome+"-overflow" } })).ToArray(),overflowEvents);
            buckets.Clear(); overflow.Clear(); overflowEvents=0; return result;
        }
    }
}
