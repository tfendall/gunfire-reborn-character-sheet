using System.Reflection;
using System.Collections.Concurrent;
using System.Text.Json;
using HarmonyLib;

internal static class CombatEvidence
{
    internal static bool Enabled;
    private static readonly CombatEvidenceModel model=new();
    private static readonly ConcurrentDictionary<MethodBase,Hook> hooks=new();
    private static double nextHealth;
    private sealed class Hook
    {
        internal string Name="",Group="";
        internal string[] Parameters=Array.Empty<string>();
        internal long Calls,Errors,Truncated,Suppressed,Filtered;
        internal readonly CombatCaptureGate Gate=new();
    }
    internal static void Install()
    {
        if(!Enabled)return;
        // GS2CWarStatus shares RVA 0x6b02d0 with 6,143 unrelated methods in
        // the inspected game build. Never patch it: it is not a distinct feed.
        TraceStore.Marker("combat-hook-excluded s2cnetwar.GS2CWarStatus shared-native-stub");
        CombatCallbackJournal.Start();
        var harmony=new Harmony("local.gunfire.statsdiagnostic.combat-evidence");
        void Add(Type type,string name,string group)
        {
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)
                .Where(m=>m.Name==name && !m.IsGenericMethod).ToArray();
            if(methods.Length==0)TraceStore.Marker("combat-hook-missing "+type.Name+"."+name);
            foreach(var method in methods)
            {
                try
                {
                    hooks[method]=new Hook{Name=type.Name+"."+name,Group=group,Parameters=method.GetParameters().Select(p=>p.Name??"arg").ToArray()};
                    var prefix=typeof(CombatTypedPrefixes).GetMethod(type.Name+"_"+name,BindingFlags.Static|BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("No typed prefix");
                    var expected=method.GetParameters();var actual=prefix.GetParameters().Skip(1).ToArray();
                    if(expected.Length!=actual.Length || expected.Where((p,i)=>p.ParameterType!=actual[i].ParameterType).Any())
                        throw new InvalidOperationException("Typed prefix signature mismatch");
                    harmony.Patch(method,prefix:new HarmonyMethod(prefix),
                        finalizer:new HarmonyMethod(typeof(CombatEvidence),nameof(Finished)));
                    TraceStore.Marker("combat-hook-on "+type.Name+"."+name);
                }
                catch(Exception ex){hooks.TryRemove(method,out _);TraceStore.Marker("combat-hook-failed "+type.Name+"."+name+" "+ex.GetBaseException().Message);}
            }
        }
        foreach(string name in new[]{"MakeHeroAddPacket","MakeMonsterAddPacket","MakeSummonAddPacket","MakeDeviceAddPacket","MakeBuildAddPacket","MakeServantAddPacket","MakePetAddPacket","MakeNpcAddPacket"})
            Add(typeof(s2cnetaddobject),name,"birth");
        foreach(string name in new[]{"GS2CPropChange","GS2CPetPropChange","ExtHeroProp"})Add(typeof(s2cnetaddobject),name,"property");
        Add(typeof(s2cnetaddobject),"MapMonsterAndPart","parts");
        Add(typeof(NewObjectCache),"UpdatePlayerProp","property");
        Add(typeof(NewObjectCache),"RemovePlayerProp","remove");
        Add(typeof(NewObjectCache),"ClearAllNH","clear");
        Add(typeof(NewPlayerManager),"DelPlayer","remove");
        Add(typeof(NewPlayerManager),"ClearAll","clear");
        foreach(string name in new[]{"GS2CWStatusHP","GS2CDie","GS2CRealDie","GS2CRelife","GS2CImmunity","GS2CReceivedDamRecord","GS2CStruckMonster","GS2CBreakShield","GS2CBreakArmor","GS2CShowDetailDam","GS2CShowDamageInfo","GS2CJumpFigure","GS2CDoubleHPBarUI","GS2CMonsterKillLine","GS2CHeroKillLine","GS2CMonsterKillLineAddition","GS2CNotifyStartSkill","GS2CMonsterAdditionInfo","GS2CLevelNodeLoadOk"})
            Add(typeof(s2cnetwar),name,"combat");
        foreach(string name in new[]{"GS2CStateAdd","GS2CStateDel","GS2CStateRefresh","GS2CStateRefreshCnt","GS2CStateRefreshExtraInfo"})
            Add(typeof(s2cnetstate),name,"state");
        foreach(string name in new[]{"C2FSSkillStart","C2FSSkillTrigger","C2FSSkillThroughInfo"})
            Add(typeof(s2cwarskill),name,"outgoing");
        Add(typeof(SkillBolt.CartoonData),"PacketCartoon","outgoing");
        // Incoming team report supplements the existing local scoreboard hook.
        TraceStore.Marker("combat-feed-on TeamWarData.AddTeamerDMG forwarded; GS2CTeamDamage packet hook excluded");
    }
    internal static void Capture(MethodBase __originalMethod,object?[] __args)
    {
        if(!Enabled || !hooks.TryGetValue(__originalMethod,out var hook))return;
        CombatCallbackJournal.Enter(hook.Name);
        Interlocked.Increment(ref hook.Calls);
        try
        {
            double time=DamageCapture.Now;
            // Refuse work before allocating/copying when a feed floods or the
            // consumer falls behind. This cannot make an unsafe detour safe;
            // shared native stubs must be excluded before installation.
            if(!hook.Gate.Accept(time) || model.Pending>=3500)
            {Interlocked.Increment(ref hook.Suppressed);return;}
            var copy=new CombatPacketCopy();
            for(int i=0;i<Math.Min(__args.Length,hook.Parameters.Length);i++)copy.Copy(hook.Parameters[i],__args[i]);
            CombatCallbackJournal.Phase("copied "+hook.Name);
            if(copy.Truncated)Interlocked.Increment(ref hook.Truncated);
            Interlocked.Add(ref hook.Errors,copy.Errors);
            model.Add(time,Panel.LocalPlayerId,DamageCapture.Epoch,LoadingGuard.SceneId,hook.Name,hook.Group,copy.Fields,copy.Truncated,copy.Errors);
        }
        catch{Interlocked.Increment(ref hook.Errors);}
    }
    internal static void CaptureOutgoing(MethodBase method,Action<CombatPacketCopy> read,Func<bool>? eligible=null)
    {
        if(!Enabled || !hooks.TryGetValue(method,out var hook))return;
        CombatCallbackJournal.Enter(hook.Name);Interlocked.Increment(ref hook.Calls);
        try
        {
            // Ignore idle trigger polls before allocating payloads or consuming
            // the observation rate budget. Still expose their count in health.
            if(eligible!=null && !eligible()) {Interlocked.Increment(ref hook.Filtered);return;}
            double time=DamageCapture.Now;
            if(!hook.Gate.Accept(time) || model.Pending>=3500)
            {Interlocked.Increment(ref hook.Suppressed);return;}
            var copy=new CombatPacketCopy();read(copy);
            CombatCallbackJournal.Phase("copied "+hook.Name);
            if(copy.Truncated)Interlocked.Increment(ref hook.Truncated);
            Interlocked.Add(ref hook.Errors,copy.Errors);
            model.Add(time,Panel.LocalPlayerId,DamageCapture.Epoch,LoadingGuard.SceneId,hook.Name,hook.Group,copy.Fields,copy.Truncated,copy.Errors);
        }
        catch {Interlocked.Increment(ref hook.Errors);}
    }
    internal static void Scoreboard(int player,double damage,double seasonal)
    {
        if(!Enabled)return;
        model.Add(DamageCapture.Now,Panel.LocalPlayerId,DamageCapture.Epoch,LoadingGuard.SceneId,
            "TeamWarData.AddTeamerDMG","scoreboard",new() {
                ["tid"]=player.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["dmg"]=damage.ToString("R",System.Globalization.CultureInfo.InvariantCulture),
                ["seasonDMG"]=seasonal.ToString("R",System.Globalization.CultureInfo.InvariantCulture)
            });
    }
    private static void Finished(MethodBase __originalMethod)
    {
        if(hooks.TryGetValue(__originalMethod,out var hook))CombatCallbackJournal.Exit(hook.Name);
    }
    internal static void Tick()
    {
        if(!Enabled)return;
        foreach(var evidence in model.Drain())
        {
            var fields=evidence.Event.Fields.ToArray();
            int chunks=Math.Max(1,(fields.Length+7)/8);
            for(int i=0;i<chunks;i++)
            {
                string payload=JsonSerializer.Serialize(new {
                    evidence.Event.Sequence,evidence.Event.Time,evidence.Event.Player,evidence.Event.Epoch,evidence.Event.Scene,
                    evidence.Event.Hook,evidence.Event.Group,evidence.Event.Truncated,evidence.Event.CopyErrors,
                    chunk=i,chunks,fields=fields.Skip(i*8).Take(8).ToDictionary(p=>p.Key,p=>p.Value),
                    subject=i==0?evidence.Subject:null,attacker=i==0?evidence.Attacker:null,victim=i==0?evidence.Victim:null,
                    deltas=i==0?evidence.PropertyDeltas:null,evidence.QueueDrops,evidence.EntityEvictions
                });
                if(payload.Length>15000)
                    payload=JsonSerializer.Serialize(new {
                        evidence.Event.Sequence,evidence.Event.Time,evidence.Event.Player,evidence.Event.Epoch,evidence.Event.Scene,
                        evidence.Event.Hook,evidence.Event.Group,Truncated=true,evidence.Event.CopyErrors,
                        chunk=i,chunks,fields=fields.Skip(i*8).Take(8).ToDictionary(p=>p.Key,p=>p.Value),
                        contextOmittedForLength=true,evidence.QueueDrops,evidence.EntityEvictions
                    });
                TraceStore.Write("combat-evidence",payload,true);
            }
        }
        if(DamageCapture.Now<nextHealth)return;
        nextHealth=DamageCapture.Now+5;
        // Separate records keep every hook's counters below the trace length cap.
        foreach(var hook in hooks.Values)
            TraceStore.Write("combat-hook-health",JsonSerializer.Serialize(new {time=DamageCapture.Now,hook.Name,hook.Group,
                calls=Interlocked.Read(ref hook.Calls),filtered=Interlocked.Read(ref hook.Filtered),suppressed=Interlocked.Read(ref hook.Suppressed),errors=Interlocked.Read(ref hook.Errors),truncated=Interlocked.Read(ref hook.Truncated),
                pending=model.Pending,drops=model.Drops,traceHealth=TraceStore.Health}),true);
    }
}
