using System.Diagnostics;
using System.Text.Json;
using HarmonyLib;
using NativeBreakdown = Il2CppSystem.Collections.Generic.Dictionary<int, Il2CppSystem.Collections.Generic.Dictionary<int, long>>;

internal static class ScoreboardDiagnostic
{
    internal static DamageLedger DisplayLedger => AuthoritativeDamage.Current.Available ? AuthoritativeDamage.Current.View : DamageCapture.Ledger;
    internal static bool Enabled, ResearchEnabled;
    private static long sequence;
    private static int errors;
    internal static void Capture(int player, double damage, double seasonal, NativeBreakdown? breakdown)
    {
        if ((!Enabled && !ResearchEnabled) || !Panel.Capture || Panel.LocalPlayerId==0 || player!=Panel.LocalPlayerId) return;
        // Copy packet-owned numeric data now. Never keep native dictionaries
        // for later, look up objects, serialize, or perform file IO in the hook.
        double captured=DamageCapture.Ledger.Total;
        int pending=DamageCapture.Pending, epoch=DamageCapture.Epoch;
        string utc=DateTime.UtcNow.ToString("O");
        var cells=new List<ScoreboardCell>();
        bool complete=breakdown!=null;
        long start=Stopwatch.GetTimestamp();
        int groups=0;
        try
        {
            if(breakdown!=null)foreach(var group in breakdown)
            {
                if(++groups>64 || Stopwatch.GetTimestamp()-start>Stopwatch.Frequency/1000) { complete=false;break; }
                if(group.Value==null) { complete=false;continue; }
                foreach(var cell in group.Value)
                {
                    if(cells.Count>=128 || Stopwatch.GetTimestamp()-start>Stopwatch.Frequency/1000) { complete=false;break; }
                    cells.Add(new(group.Key,cell.Key,cell.Value));
                }
                if(!complete)break;
            }
        }
        catch { complete=false;System.Threading.Interlocked.Increment(ref errors); }
        ScoreboardDiagnosticModel.Add(new(System.Threading.Interlocked.Increment(ref sequence),utc,player,epoch,
            damage,seasonal,captured,pending,breakdown!=null,complete,cells.ToArray(),ResearchEnabled ? DamageAudit.Take() : null));
    }
    private static Dictionary<int,double>? SeasonalBreakdown(ScoreboardSample sample)
    {
        if(!sample.BreakdownAvailable || !sample.BreakdownComplete || sample.Seasonal<0 || sample.Seasonal>sample.Reported)return null;
        // Category/detail IDs are season-specific. Require this player's S8 orb,
        // not merely an allocated singleton left over from another mode.
        try
        {
            var items=S8Manager._instance?.S8ThirdItemDict;
            if(items==null || !items.ContainsKey(sample.Player))return null;
            bool orb=false;
            foreach(var item in items[sample.Player])if(item.Value?.sid is 1007 or 1052) { orb=true;break; }
            if(!orb)return null;
        }
        catch { return null; }
        var result=new Dictionary<int,double>();
        foreach(var cell in sample.Cells.Where(c=>c.Category==1))
            if(cell.Value<0 || !result.TryAdd(cell.Detail,cell.Value))return null;
        // Only replace the captured partition when the server detail accounts
        // for the whole seasonal subtotal; otherwise retain the raw fallback.
        return result.Values.Sum()==sample.Seasonal ? result : null;
    }
    private static ScoreboardSample? previous;
    internal static void Drain()
    {
        foreach(var sample in ScoreboardDiagnosticModel.Drain(Panel.LocalPlayerId,DamageCapture.Epoch))
        {
            if(Enabled)AuthoritativeDamage.Current.Observe(sample.Reported,DamageCapture.Ledger,SeasonalBreakdown(sample));
            if(!ResearchEnabled)continue;
            TraceStore.Write("scoreboard-diagnostic",JsonSerializer.Serialize(new {
                sample.Sequence,sample.Utc,sample.Player,sample.Epoch,
                reportedTotal=sample.Reported,reportedSeasonal=sample.Seasonal,
                sample.CapturedAtReceipt,sample.PendingAtReceipt,
                capturedAtDrain=DamageCapture.Ledger.Total,pendingAtDrain=DamageCapture.Pending,
                gapAtReceipt=sample.Reported-sample.CapturedAtReceipt,
                gapAtDrain=sample.Reported-DamageCapture.Ledger.Total,
                sample.BreakdownAvailable,sample.BreakdownComplete,sample.Cells,
                diagnosticQueueDrops=ScoreboardDiagnosticModel.Drops,copyErrors=errors
            }));
            bool baseline=previous==null || previous.Player!=sample.Player || previous.Epoch!=sample.Epoch ||
                previous.Reported>sample.Reported || previous.Audit?.Generation!=sample.Audit?.Generation;
            TraceStore.Write("damage-reconciliation",JsonSerializer.Serialize(new {
                sample.Sequence,sample.Utc,sample.Player,sample.Epoch,baseline,
                previousSequence=baseline?(long?)null:previous!.Sequence,
                gameIncrease=baseline?(double?)null:sample.Reported-previous!.Reported,
                capturedIncrease=baseline?(double?)null:sample.CapturedAtReceipt-previous!.CapturedAtReceipt,
                gapIncrease=baseline?(double?)null:(sample.Reported-sample.CapturedAtReceipt)-(previous!.Reported-previous.CapturedAtReceipt),
                sample.PendingAtReceipt, previousPending=baseline?(int?)null:previous!.PendingAtReceipt,
                auditSerial=sample.Audit?.Serial, auditGeneration=sample.Audit?.Generation,
                auditOverflowEvents=sample.Audit?.OverflowEvents,
                bucketChunks=((sample.Audit?.Buckets.Length??0)+31)/32,
                outcomeTotals=sample.Audit?.Buckets.GroupBy(x=>x.Key.Outcome.Replace("-overflow", "")).Select(g=>new {
                    outcome=g.Key,count=g.Sum(x=>x.Count),rawDamage=g.Sum(x=>x.RawDamage)
                }).ToArray(),
                diagnosticQueueDrops=ScoreboardDiagnosticModel.Drops, traceHealth=TraceStore.Health,
                intervalComplete=baseline || (sample.Audit?.Serial==previous!.Audit?.Serial+1)
            }));
            if(sample.Audit is { } audit)
                for(int offset=0;offset<audit.Buckets.Length;offset+=32)
                    TraceStore.Write("damage-reconciliation-buckets",JsonSerializer.Serialize(new {
                        sample.Sequence,sample.Player,sample.Epoch,audit.Serial,chunk=offset/32,
                        buckets=audit.Buckets.Skip(offset).Take(32).ToArray()
                    }));
            previous=sample;
        }
    }
}

// One observation point for the scoreboard feed. Do not also count the network
// packet as a second hit source: these reports can describe damage already seen.
[HarmonyPatch(typeof(TeamWarData), nameof(TeamWarData.AddTeamerDMG))]
public static class ScoreboardDamageProbe
{
    public static void Prefix(int tid, double dmg, double seasonDMG, NativeBreakdown dType)
    {
        try { ScoreboardSampling.Response(tid,dmg); } catch { }
        try { CombatEvidence.Scoreboard(tid,dmg,seasonDMG); } catch { }
        try { ScoreboardDiagnostic.Capture(tid,dmg,seasonDMG,dType); } catch { }
    }
}
