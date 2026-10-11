using System.Text.Json;

internal static class ScoreboardSampling
{
    internal static bool Enabled, ObserverReady;
    private static readonly ScoreboardSamplingState state=new();
    private static int requestedPlayer, requestedEpoch, requestedScene;
    private static bool failed;
    internal static void Tick()
    {
        if(!Enabled)return;
        double now=DamageCapture.Now;
        if(state.CheckTimeout(now))
            if(TraceStore.Enabled)TraceStore.Write("scoreboard-sampling",JsonSerializer.Serialize(new {kind="timeout",request=state.Pending,time=now,
                detail="Automatic requests suspended until a local response arrives; no retry."}));
        bool eligible=!failed && ObserverReady && ScoreboardDiagnostic.Enabled && Panel.Capture && Panel.RunActive &&
            Panel.LocalPlayerId!=0 && LoadingGuard.Ready;
        long request=state.Tick(now,eligible);
        if(request==0)return;
        requestedPlayer=Panel.LocalPlayerId;requestedEpoch=DamageCapture.Epoch;requestedScene=LoadingGuard.SceneId;
        if(TraceStore.Enabled)TraceStore.Write("scoreboard-sampling",JsonSerializer.Serialize(new {kind="request",request,time=now,
            player=requestedPlayer,epoch=requestedEpoch,scene=requestedScene,captured=DamageCapture.Ledger.Total,
            pendingHits=DamageCapture.Pending}));
        try { s2cnetwar.C2GSTeamDamage(); }
        catch(Exception ex)
        {
            failed=true;
            CrashDiagnostics.Error("scoreboard-sampling-request",ex);
        }
    }
    internal static void Response(int player,double damage)
    {
        if(!Enabled || player!=Panel.LocalPlayerId || player==0)return;
        double now=DamageCapture.Now, sent=state.SentAt;
        long request=state.Response(now);
        // The protocol has no request token. A C-screen response can overlap ours.
        if(TraceStore.Enabled)TraceStore.Write("scoreboard-sampling",JsonSerializer.Serialize(new {kind="response",request,time=now,player,
            reported=damage,elapsed=request==0?(double?)null:now-sent,
            sameContext=request!=0 && requestedPlayer==player && requestedEpoch==DamageCapture.Epoch && requestedScene==LoadingGuard.SceneId,
            association="first local report after request, not a protocol acknowledgement"}));
    }
}
