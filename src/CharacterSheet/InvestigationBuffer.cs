using System.Collections.Concurrent;

internal sealed record InvestigationSample(long Sequence, string Utc, int Player, int Epoch,
    string Method, string Context, string Input, string Result, bool Truncated);

// Only managed snapshots cross from callbacks to the writer/polling pump.
internal sealed class InvestigationBuffer
{
    private readonly ConcurrentQueue<InvestigationSample> queue = new();
    private int pending, drops;
    private long sequence;
    internal int Drops => Volatile.Read(ref drops);
    internal int Pending => Volatile.Read(ref pending);
    internal bool Add(int player, int epoch, string method, string context, string input, string result)
    {
        if (Interlocked.Increment(ref pending) > 1024)
        { Interlocked.Decrement(ref pending); Interlocked.Increment(ref drops); return false; }
        bool clipped = input.Length > 6000 || result.Length > 2000 || context.Length > 1000;
        queue.Enqueue(new(Interlocked.Increment(ref sequence), DateTime.UtcNow.ToString("O"),
            player, epoch, method, context[..Math.Min(context.Length,1000)],
            input[..Math.Min(input.Length,6000)], result[..Math.Min(result.Length,2000)], clipped));
        return true;
    }
    internal InvestigationSample[] Drain(int player, int epoch, out int stale)
    {
        stale=0;
        var output=new List<InvestigationSample>();
        for(int i=0;i<32 && queue.TryDequeue(out var sample);i++)
        {
            Interlocked.Decrement(ref pending);
            if(sample.Player!=player || sample.Epoch!=epoch) { stale++; continue; }
            output.Add(sample);
        }
        return output.ToArray();
    }
}

internal sealed class InvestigationScope
{
    [ThreadStatic] private static string? current;
    internal static string Current => current ?? "";
    internal static string Push(string value) { string old=Current; current=value; return old; }
    internal static void Restore(string value) => current=value;
}

internal sealed class InvestigationDirtyProperties
{
    private readonly Dictionary<(int,string),long> pending=new();
    internal int Drops { get; private set; }
    internal void Mark(int id,string key,long timestamp)
    {
        if(id==0 || key.Length==0)return;
        var pair=(id,key);
        if(pending.ContainsKey(pair))return; // Preserve oldest notification for latency.
        if(pending.Count>=256){Drops++;return;}
        pending[pair]=timestamp;
    }
    internal (int Id,string Key,long Timestamp)[] Drain()
    {
        var batch=pending.Take(8).Select(p=>(p.Key.Item1,p.Key.Item2,p.Value)).ToArray();
        foreach(var p in batch)pending.Remove((p.Item1,p.Item2));
        return batch;
    }
    internal void Clear()=>pending.Clear();
}
