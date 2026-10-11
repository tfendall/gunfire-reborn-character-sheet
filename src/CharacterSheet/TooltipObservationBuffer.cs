using System.Collections.Concurrent;

internal sealed record TooltipObservation(long Sequence, string Utc, string Method, int Player,
    int StateId, int Sid, string Input, string Result, bool Truncated);
internal sealed class TooltipObservationBuffer
{
    private readonly ConcurrentQueue<TooltipObservation> queue = new();
    private int pending, dropped;
    private long sequence;
    internal int Drops => System.Threading.Volatile.Read(ref dropped);
    internal void Add(string method,int player,int stateId,int sid,string? input,string? result)
    {
        if(System.Threading.Interlocked.Increment(ref pending)>256)
        { System.Threading.Interlocked.Decrement(ref pending);System.Threading.Interlocked.Increment(ref dropped);return; }
        input??="";result??="";
        bool truncated=input.Length>2048 || result.Length>4096;
        queue.Enqueue(new(System.Threading.Interlocked.Increment(ref sequence),DateTime.UtcNow.ToString("O"),
            method,player,stateId,sid,input[..Math.Min(2048,input.Length)],result[..Math.Min(4096,result.Length)],truncated));
    }
    internal TooltipObservation[] Drain()
    {
        var result=new List<TooltipObservation>();
        for(int i=0;i<16 && queue.TryDequeue(out var value);i++)
        {System.Threading.Interlocked.Decrement(ref pending);result.Add(value);}
        return result.ToArray();
    }
}
