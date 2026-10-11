using System.Collections.Concurrent;

internal sealed record ScoreboardCell(int Category, int Detail, long Value);
internal sealed record ScoreboardSample(long Sequence, string Utc, int Player, int Epoch,
    double Reported, double Seasonal, double CapturedAtReceipt, int PendingAtReceipt,
    bool BreakdownAvailable, bool BreakdownComplete, ScoreboardCell[] Cells, DamageAuditSnapshot? Audit = null);
internal static class ScoreboardDiagnosticModel
{
    private static readonly ConcurrentQueue<ScoreboardSample> queue = new();
    private static int pending, dropped;
    internal static int Drops => System.Threading.Volatile.Read(ref dropped);
    internal static bool Add(ScoreboardSample sample)
    {
        if (!double.IsFinite(sample.Reported) || !double.IsFinite(sample.Seasonal)) return false;
        if (System.Threading.Interlocked.Increment(ref pending) > 128)
        {
            System.Threading.Interlocked.Decrement(ref pending);
            System.Threading.Interlocked.Increment(ref dropped); return false;
        }
        queue.Enqueue(sample); return true;
    }
    internal static ScoreboardSample[] Drain(int player, int epoch)
    {
        var result = new List<ScoreboardSample>();
        for (int i=0; i<16 && queue.TryDequeue(out var sample); i++)
        {
            System.Threading.Interlocked.Decrement(ref pending);
            if (sample.Player==player && player!=0 && sample.Epoch==epoch) result.Add(sample);
        }
        return result.ToArray();
    }
}
