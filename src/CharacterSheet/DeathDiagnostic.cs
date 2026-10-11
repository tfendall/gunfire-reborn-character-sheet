using DYPublic.Duonet;
using HarmonyLib;
using System.Text.Json;

internal static class DeathDiagnostic
{
    internal static void Drain()
    {
        foreach(var sample in DeathObservation.Drain(Panel.LocalPlayerId,DamageCapture.Epoch))
            TraceStore.Write("death-observation",JsonSerializer.Serialize(new {
                sample, candidateWindowSeconds=10, candidatesAreRawCallbacks=true,
                sameAttackerCandidates=sample.RecentHits.Count(h=>h.Hit.Attacker==sample.Attacker),
                damageCredited=0
            }));
    }
}

[HarmonyPatch(typeof(s2cnetwar), nameof(s2cnetwar.GS2CDie))]
internal static class DeathEventProbe
{
    internal static void Prefix(s2cnetwar_GS2CDieClass data)
    {
        try
        {
            if(DamageAudit.Enabled && Panel.Capture && Panel.RunActive && Panel.LocalPlayerId!=0)
                DeathObservation.Death(Panel.LocalPlayerId,DamageCapture.Epoch,DamageCapture.Now,
                    data.iAttack,data.iVictim,data.iFinalDam);
        }
        catch { }
    }
}
