// Managed request pacing. No retry while an unanswered request may still exist.
internal sealed class ScoreboardSamplingState
{
    internal long Pending { get; private set; }
    internal double SentAt { get; private set; }
    internal bool TimedOut { get; private set; }
    private long sequence;
    private double next;
    private bool ready;
    internal long Tick(double now, bool eligible)
    {
        if (!eligible) { ready=false; return 0; }
        if (!ready) { ready=true; next=now+2; }
        if(Pending!=0 || now<next)return 0;
        Pending=++sequence; SentAt=now; TimedOut=false;
        return Pending;
    }
    internal bool CheckTimeout(double now)
    {
        if(Pending==0 || TimedOut || now-SentAt<10)return false;
        TimedOut=true; return true;
    }
    internal long Response(double now)
    {
        long pending=Pending;Pending=0;TimedOut=false;next=now+1;
        return pending;
    }
}
