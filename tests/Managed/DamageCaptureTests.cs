using DYPublic.Duonet;

// Exercise the real enqueue/drain path, replacing only the native game boundary.
internal static class DamageCaptureTests
{
    internal static void Run(Action<bool,string> check)
    {
        Panel.Capture = Panel.RunActive = LoadingGuard.CanRead = true;
        Panel.LocalPlayerId = 10;
        DamageCapture.Reset();
        DamageAudit.Enabled=true;
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
        var audit=DamageAudit.Take();
        check(audit.Buckets.Where(x=>x.Key.Outcome.StartsWith("accepted-")).Sum(x=>x.RawDamage)==400 &&
            audit.Buckets.Where(x=>x.Key.Outcome.StartsWith("rejected-")).Sum(x=>x.RawDamage)==3600 &&
            audit.Buckets.Where(x=>x.Key.Outcome.StartsWith("rejected-")).Sum(x=>x.Count)==4,
            "actual capture path audits every accepted and rejected hit without changing credit");
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
        DamageCapture.Reset();
        Hit(10,99,0x10008000);Hit(10,63,0x10008000);
        Hit(10,100,0x20002000);Hit(20,100,0x10004000);Drain();
        check(DamageCapture.Ledger.Total==0 && DamageCapture.WeaponSamples==0 && DamageCapture.SkillSamples==0,
            "perform and scene recovery never enter damage totals or samples, including companion recovery");
        check(DamageAudit.Take().Buckets.Where(x=>x.Key.Outcome=="recovery-excluded").Sum(x=>x.RawDamage)==362,"recovery exclusions retain exact audit sums");
        Hit(10,80,0x808020);Hit(10,90,0x401000);Drain();
        check(DamageCapture.Ledger.Total==170,"ordinary shield damage and true damage survive recovery filtering");
        DamageCapture.Reset();
        void Execution(int attacker=10,int type=4096,long amount=999999,int extra=1,int lucky=1,int action=30436,int abnormal=0,int shield=0,int weak=0)
            => DamageCapture.Enqueue(new(){iAttack=attacker,iVictim=100,iHP=amount,iType=type,iExInfo=extra,iLuckyHitEff=lucky,iActNum=action,iAbnormal=abnormal,iBreakShield=shield,iWeaked=weak});
        Execution();Drain();
        check(DamageCapture.ExecutionMarkers==1 && DamageCapture.Ledger.Total==0 && DamageCapture.Summary=="No recent local damage events.",
            "execution marker does not enter ledger or combat sample window");
        check(TraceStore.Events.Any(x=>x.StartsWith("execution-marker ") && x.Contains("hpRaw=999999 damageCredited=0") && x.Contains("victim=100")),
            "excluded execution retains raw amount and correlation identity in diagnostic log");
        check(DamageAudit.Take().Buckets.Single(x=>x.Key.Outcome=="execution-excluded").RawDamage==999999,
            "execution exclusion remains measurable in scoreboard audit intervals");
        Hit(10,167);Hit(10,3696,0x400800);Drain();
        check(DamageCapture.Ledger.Total==3863,"execution regression preserves ordinary damage around the killing hit");
        DamageCapture.Reset();
        Execution(type:0x801000);Execution(type:0x401000);Execution(amount:1000);Execution(extra:0);
        Execution(lucky:2);Execution(action:0);Execution(abnormal:1);Execution(shield:1);Execution(weak:1);Drain();
        check(DamageCapture.ExecutionMarkers==0 && DamageCapture.Ledger.Total==8*999999L+1000,
            "large weapon and skill hits, ordinary true damage, and near-match flags remain counted");
        DamageCapture.Reset();Execution(attacker:11);Drain();
        check(DamageCapture.ExecutionMarkers==0 && DamageCapture.Ledger.Total==0,"unowned executions cannot count as local executions");
        Execution();DamageCapture.Reset();Drain();
        check(DamageCapture.ExecutionMarkers==0 && DamageCapture.Ledger.Total==0,"reset clears execution counts and pending markers");
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
