using HarmonyLib;
using DYPublic.Duonet;

[HarmonyPatch(typeof(s2cnetwar), nameof(s2cnetwar.GS2CWStatusHP))]
internal static class DamagePacketProbe
{
    internal static void Prefix(s2cnetwar_GS2CWStatusHPClass data, out DamageAudit.Packet? __state)
    {
        __state=null;
        try
        {
            if(Panel.Capture && Panel.RunActive && Panel.LocalPlayerId!=0)
                __state=DamageAudit.Begin(DamageCapture.Identity(data));
        }
        catch { }
    }
    // Finalizer restores nested/thread-local correlation even when the game throws.
    internal static void Finalizer(DamageAudit.Packet? __state)
    {
        try { DamageAudit.End(__state); } catch { }
    }
}
