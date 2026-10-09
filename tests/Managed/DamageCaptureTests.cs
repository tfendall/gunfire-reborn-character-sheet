using DYPublic.Duonet;

// Exercise the real enqueue/drain path, replacing only the native game boundary.
internal static class DamageCaptureTests
{
    internal static void Run(Action<bool,string> check)
    {
        Panel.Capture = Panel.RunActive = LoadingGuard.CanRead = true;
        Panel.LocalPlayerId = 10;
        DamageCapture.Reset();
        NewObjectCache.Owners[20] = new() { Owner = 10 }; // Local companion.
        NewObjectCache.Owners[30] = new() { Owner = 11 }; // Teammate companion.
        NewObjectCache.Owners[40] = new() { Owner = 0 }; // Enemy.
        void Hit(int attacker, long damage, int type = 0x800400) => DamageCapture.Enqueue(new()
        { iAttack=attacker, iHP=damage, iType=type, iActNum=30001, iLuckyHitEff=3 });
        void Drain() { for(int i=0;i<20;i++) DamageCapture.Drain(); }
        Hit(10,100); Hit(20,250); Hit(20,50,0x400100);
        Hit(11,900); Hit(30,900); Hit(40,900); Hit(99,900);
        Drain();
        check(DamageCapture.Ledger.Total==400 && DamageCapture.Ledger.Sources["Companions"]==300,
            "capture credits direct player and owned companion hits once, excluding teammate/enemy/unknown attackers");
        var rows=DamageTabModel.Create(DamageCapture.Ledger);
        var companion=rows.Single(x=>x.Name=="Companion damage");
        check(companion.Amount==300 && companion.RunPercent==75 && companion.Elements.Length==2 && rows.Sum(x=>x.Amount)==400,
            "companion damage preserves elements and source percentages without inflating player weapon damage");
        Thread.Sleep(260); Drain();
        check(DamageCapture.WeaponSamples==1 && DamageCapture.SkillSamples==0 && DamageCapture.AverageWeaponHit==100,
            "companion weapon/skill flags do not pollute the player's Lucky Shot samples or average hit");
        check(TraceStore.Events.Any(x=>x.Contains("credit=Companion owner=10")),
            "credited companion trace records retain server owner for in-game verification");

        DamageCapture.Reset(); NewObjectCache.Reads=0; LoadingGuard.CanRead=false;
        Hit(20,250); Drain();
        check(NewObjectCache.Reads==0 && DamageCapture.Ledger.Total==0,
            "ownership reads are deferred while the native loading guard is closed");
        LoadingGuard.CanRead=true; Drain();
        check(DamageCapture.Ledger.Total==250, "deferred companion hit is credited when reads resume");
        Hit(20,100); DamageCapture.Reset(); Drain();
        check(DamageCapture.Ledger.Total==0, "reset discards pending companion events");

        Hit(20,100); Panel.LocalPlayerId=11; Drain();
        check(DamageCapture.Ledger.Total==0, "queued events cannot transfer credit when the local player changes");
        Panel.LocalPlayerId=10; NewObjectCache.Owners[20]=new() { Owner=11 };
        Hit(20,100); Drain();
        check(DamageCapture.Ledger.Total==0, "ownership is rechecked across drains rather than retained for reused IDs");
        NewObjectCache.ThrowOnRead=true; Hit(20,100); Drain(); NewObjectCache.ThrowOnRead=false;
        check(DamageCapture.Ledger.Total==0, "failed ownership lookup fails closed without interrupting damage processing");
        Panel.Capture=false; Hit(10,100); Panel.Capture=true;
        Hit(10,0); Hit(10,-50); Drain();
        check(DamageCapture.Ledger.Total==0, "disabled capture and non-damage events do not add damage");
        check(DamageOwnership.Classify(0,10,10,10)==DamageCredit.None && DamageOwnership.Classify(20,0,0,0)==DamageCredit.None,
            "zero attacker/player identifiers cannot grant ownership credit");
    }
}

internal static class Panel
{
    internal static bool Capture, RunActive;
    internal static int LocalPlayerId, CurrentWeaponId=123;
}
internal static class LoadingGuard { internal static bool CanRead; }
internal sealed class PlayerProp { internal int Owner; }
internal static class NewObjectCache
{
    internal static readonly Dictionary<int,PlayerProp> Owners = new();
    internal static int Reads;
    internal static bool ThrowOnRead;
    internal static PlayerProp? GetPlayerProp(int id)
    {
        Reads++;
        if(ThrowOnRead)throw new InvalidOperationException("Simulated native lookup failure");
        return Owners.GetValueOrDefault(id);
    }
}
internal static class TraceStore
{
    internal static readonly List<string> Events = new();
    internal static string Health => "test";
    internal static void Marker(string message) { }
    internal static void Write(string kind,string message) => Events.Add(kind+" "+message);
}
public static class FollowTextManager { public static void WStatusHP(s2cnetwar_GS2CWStatusHPClass data) { } }
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute { internal HarmonyPatch(Type type,string method) { } }
}
namespace DYPublic.Duonet
{
    public struct s2cnetwar_GS2CWStatusHPClass
    {
        public int iAttack,iActNum,iVictim,iType,iAbnormal,iBreakShield,iWeaked,iLuckyHitEff,iExInfo;
        public long iHP;
    }
}
