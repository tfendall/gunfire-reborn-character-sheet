using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using SkillBolt;
using UIScript;

internal static class EffectiveDiagnostics
{
    internal static bool PropertiesEnabled, StatesEnabled, CalculationsEnabled;
    internal static bool Enabled => PropertiesEnabled || StatesEnabled || CalculationsEnabled;
    private static readonly InvestigationBuffer buffer=new();
    private static readonly InvestigationDirtyProperties dirty=new();
    private static readonly ConcurrentDictionary<MethodBase,Hook> hooks=new();
    private static readonly ConcurrentDictionary<int,byte> ownedItems=new();
    private static readonly Stopwatch clock=Stopwatch.StartNew();
    private static double nextHealth,nextSurvey;
    private static int surveyObject,surveyOffset,surveyTarget,player,epoch,stale,mainThread,scene,revision;
    private static long surveyErrors, surveyRecords;
    [ThreadStatic] private static bool reading;
    private sealed class Hook
    {
        internal string Group="", Name="";
        internal ParameterInfo[] Parameters=Array.Empty<ParameterInfo>();
        internal long Calls,Accepted,Filtered,Errors;
    }
    internal sealed class Invocation
    {
        internal string Previous="", Context="", Input="";
        internal int Player,Epoch;
        internal bool Scoped,Accepted;
    }
    internal static void Install()
    {
        mainThread=Environment.CurrentManagedThreadId;
        var h=new Harmony("local.gunfire.statsdiagnostic.effective");
        void Add(Type type,string name,string group,int? count=null)
        {
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)
                .Where(m=>m.Name==name && !m.IsGenericMethod && (!count.HasValue || m.GetParameters().Length==count)).ToArray();
            if(methods.Length==0)TraceStore.Marker("effective-hook-missing "+type.FullName+"."+name);
            foreach(var method in methods)
            {
                try
                {
                    var hook=new Hook{Group=group,Name=type.FullName+"."+name+"/"+method.GetParameters().Length,Parameters=method.GetParameters()};
                    hooks[method]=hook;
                    h.Patch(method,prefix:new HarmonyMethod(typeof(EffectiveDiagnostics),nameof(Before)),
                        postfix:new HarmonyMethod(typeof(EffectiveDiagnostics),method.ReturnType==typeof(void)?nameof(AfterVoid):nameof(AfterValue)),
                        finalizer:new HarmonyMethod(typeof(EffectiveDiagnostics),nameof(Finish)));
                    TraceStore.Marker("effective-hook-on "+hook.Name);
                }
                catch(Exception ex){hooks.TryRemove(method,out _);TraceStore.Marker("effective-hook-failed "+type.FullName+"."+name+" "+ex.GetBaseException().Message);}
            }
        }
        if(PropertiesEnabled)
        {
            Add(typeof(PropObject),"UpdatePropValue","property");
            Add(typeof(PropObject),"ExeAllCachedValueChangeEvent","property");
            Add(typeof(PropObject),"ExecPropChangeEvent","property");
            Add(typeof(NewObjectCache),"UpdatePlayerProp","network-property");
            Add(typeof(ItemPropCache),"UpdatePropByItem","network-property");
            Add(typeof(ClientPrepData),"ClientAddPrepDate","client-modifier");
            Add(typeof(ClientPrepData),"ClearProItem","client-modifier");
            Add(typeof(ClientPrepData),"Clear","client-modifier");
            Add(typeof(S8Manager),"RefreshS8GemAttr","season");
            Add(typeof(S8Manager),"RefreshS8ThirdItemAttr","season");
        }
        if(StatesEnabled)
        {
            foreach(string name in new[]{"AddState","DelState","RefreshState","RefreshStateCnt","RefreshExtraInfo","ClearPlayerState","ClearAll"})
                Add(typeof(StateManager),name,"state");
            foreach(string name in new[]{"AddState","DeleteState","ClearAll"})Add(typeof(CtrlSideStateManager),name,"client-state");
        }
        if(CalculationsEnabled)
        {
            Add(typeof(CSkillBase),"CalculatePropCacheAddition","skill");
            Add(typeof(CSkillBase),"AddBulletChangeFixedArg","skill",3);
            Add(typeof(CSkillBase),"AddBulletChangePerTenThousandArg","skill",3);
            Add(typeof(BulletChange.CRuleAction),"BulletChangeCBSetSkillSnapshot","rule");
            Add(typeof(BulletChange.CFormula),"RunFormula","rule");
            foreach(string name in new[]{"UpdateDescObj","GetValue","OperateDesc","OperateDescByFormula","SpecialOperateDesc"})
                Add(typeof(PCWP_State_logic),name,"tooltip-context");
            Add(typeof(Evaluate),"OperateWithParam","evaluate");
            Add(typeof(Evaluate),"EvaluateInfixExpression","evaluate");
            Add(typeof(S9.FormulaCalculator),"CalculateFormula","season-formula");
        }
    }
    private static bool LocalObject(int id)
    {
        if(id==0)return false;
        if(id==Panel.LocalPlayerId || id==Panel.CurrentWeaponId || ownedItems.ContainsKey(id))return true;
        if(!LoadingGuard.CanRead)return false;
        return NewObjectCache.GetPlayerProp(id)?.Owner==Panel.LocalPlayerId;
    }
    private static void Remember(int id)
    {
        if(id==0)return;
        if(ownedItems.Count<128)ownedItems.TryAdd(id,0);
    }
    private static string? Context(Hook hook,object? instance,object[] args)
    {
        if(hook.Group=="evaluate")return InvestigationScope.Current.Length==0?null:InvestigationScope.Current;
        if(instance is PCWP_State_logic ui)
        {
            if(ui.HeroID!=Panel.LocalPlayerId)return null;
            if(hook.Name.Contains(".GetValue/"))return $"state={args[0]} sid={args[1]}";
            return $"state={ui.curStateId} sid={ui.curStateSid}";
        }
        if(instance is CSkillBase skill)
        {
            if(!LocalObject(skill.AttID))return null;
            Remember(skill.Weapon); Remember(skill.AttrObjID);
            if(skill.PerformPropCache!=null)Remember(skill.PerformPropCache.ObjectID);
            return $"attacker={skill.AttID} action={skill.ActNum} weapon={skill.Weapon} perform={skill.Perform} attrObject={skill.AttrObjID}";
        }
        if(instance is PropObject prop)return LocalObject(prop.ObjectID)?$"object={prop.ObjectID}":null;
        if(hook.Group=="rule")
        {
            var hero=args.OfType<NewPlayerObject>().FirstOrDefault();
            if(hero==null || !LocalObject(hero.ObjectID))return null;
            return $"object={hero.ObjectID} "+InvestigationScope.Current;
        }
        if(hook.Group=="client-state")return "local-client-state";
        if(hook.Group=="season-formula")return "season-formula unattributed";
        if(hook.Group=="season")return "season-update unattributed";
        if(args.Length==0)return "global-clear";
        int target=args[0] is int n?n:0;
        if(hook.Group=="state" && hook.Parameters[0].Name=="stateid")
        {
            if(!LoadingGuard.CanRead)return null;
            var owners=StateManager.StateOwnerDict;
            return owners!=null && owners.ContainsKey(target) && LocalObject(owners[target])?$"state={target} object={owners[target]}":null;
        }
        return LocalObject(target)?$"object={target}":null;
    }
    private static void Before(MethodBase __originalMethod,object? __instance,object[] __args,out Invocation? __state)
    {
        __state=null;
        if(!hooks.TryGetValue(__originalMethod,out var hook))return;
        Interlocked.Increment(ref hook.Calls);
        if(Environment.CurrentManagedThreadId!=mainThread || reading || !Panel.Capture || !Panel.RunActive || Panel.LocalPlayerId==0){Interlocked.Increment(ref hook.Filtered);return;}
        try
        {
            string? context=Context(hook,__instance,__args);
            if(context==null){Interlocked.Increment(ref hook.Filtered);return;}
            var state=new Invocation{Player=Panel.LocalPlayerId,Epoch=revision,Context=context};
            __state=state;
            state.Previous=InvestigationScope.Push(context);state.Scoped=true;
            var format=new SnapshotText();
            state.Input=string.Join("; ",__args.Take(12).Select((arg,i)=>hook.Parameters[i].Name+"="+format.Value(arg)));
            state.Accepted=true;Interlocked.Increment(ref hook.Accepted);
        }
        catch {Interlocked.Increment(ref hook.Errors);}
    }
    private static void AfterVoid(MethodBase __originalMethod,object? __instance,object[] __args,Invocation? __state)
        => Complete(__originalMethod,__instance,__args,__state,null);
    private static void AfterValue(MethodBase __originalMethod,object? __instance,object[] __args,object? __result,Invocation? __state)
        => Complete(__originalMethod,__instance,__args,__state,__result);
    private static void Complete(MethodBase method,object? instance,object[] args,Invocation? state,object? result)
    {
        if(state?.Accepted!=true)return;
        try
        {
            var hook=hooks[method];var text=new SnapshotText();
            if(PropertiesEnabled)
            {
                if(instance is PropObject prop && args.Length>0 && args[0] is string propertyKey)
                    dirty.Mark(prop.ObjectID,propertyKey,Stopwatch.GetTimestamp());
                else if(args.Length>1 && args[0] is int objectId)
                {
                    if(hook.Group=="network-property" && args[1] is string keyName)dirty.Mark(objectId,keyName,Stopwatch.GetTimestamp());
                    if(args[1] is ClientPrepDateFormat modifier)dirty.Mark(objectId,modifier.m_keys,Stopwatch.GetTimestamp());
                }
            }
            string output=text.Value(result);
            if(instance is CSkillBase skill && args.Length>0 && args[0] is string key && skill.ItemPropCache!=null)
                output+=" cache="+text.Value(skill.ItemPropCache.GetPropValue<Il2CppSystem.Object>(key,null!));
            if(hook.Group=="state" && method.Name=="RefreshExtraInfo" && LoadingGuard.CanRead && args[1] is int id)
                output+=" committed="+text.Value(StateManager.GetStateExtraInfo(id)?.extraDict);
            for(int i=0;i<hook.Parameters.Length;i++)if(hook.Parameters[i].ParameterType.IsByRef)
                output+=" "+hook.Parameters[i].Name+"="+text.Value(args[i]);
            buffer.Add(state.Player,state.Epoch,hook.Name,state.Context,state.Input,output);
        }
        catch {Interlocked.Increment(ref hooks[method].Errors);}
    }
    private static void Finish(MethodBase __originalMethod,Exception? __exception,Invocation? __state)
    {
        // A void finalizer cannot replace or suppress the game's exception.
        if(__state==null)return;
        try
        {
            if(__exception!=null && hooks.TryGetValue(__originalMethod,out var hook))
            {Interlocked.Increment(ref hook.Errors);buffer.Add(__state.Player,__state.Epoch,hook.Name,__state.Context,__state.Input,"game-threw "+__exception.GetType().Name);}
        }
        finally {if(__state.Scoped)InvestigationScope.Restore(__state.Previous);}
    }
    internal static void Tick()
    {
        if(!Enabled)return;
        int currentScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if(player!=Panel.LocalPlayerId || epoch!=DamageCapture.Epoch || scene!=currentScene)
        {player=Panel.LocalPlayerId;epoch=DamageCapture.Epoch;scene=currentScene;revision++;ownedItems.Clear();dirty.Clear();surveyOffset=surveyObject=surveyTarget=0;}
        var drained=buffer.Drain(player,revision,out int discarded);
        foreach(var sample in drained)TraceStore.Write("effective-observation",JsonSerializer.Serialize(sample));
        stale+=discarded;
        if(PropertiesEnabled && Panel.Capture && Panel.RunActive && LoadingGuard.CanRead && StatsScreen.ReadsAllowed)
        {
            try
            {
                reading=true;
                foreach(var changed in dirty.Drain())
                {
                    PropObject? obj=NewObjectCache.GetPlayerProp(changed.Id);
                    obj??=ItemPropCache.GetPropByItem(changed.Id);
                    ReadProperty(obj,changed.Id,changed.Key,"effective-property-changed",changed.Timestamp);
                }
                if(clock.Elapsed.TotalSeconds>=nextSurvey){nextSurvey=clock.Elapsed.TotalSeconds+0.2;Survey();}
            }
            catch(Exception ex){surveyErrors++;TraceStore.Write("effective-survey-error",ex.GetBaseException().Message);}
            finally{reading=false;}
        }
        if(Panel.Capture && clock.Elapsed.TotalSeconds>=nextHealth)
        {
            nextHealth=clock.Elapsed.TotalSeconds+5;
            TraceStore.Write("effective-health",JsonSerializer.Serialize(new {utc=DateTime.UtcNow,revision,scene,buffer.Pending,buffer.Drops,dirtyDrops=dirty.Drops,stale,surveyErrors,surveyRecords,
                hooks=hooks.Values.Select(h=>new {h.Name,h.Calls,h.Accepted,h.Filtered,h.Errors}).ToArray()}));
        }
    }
    private static readonly PropertyInfo? propertyMap=typeof(PropObject).GetProperty("m_PropDict");
    private static void ReadProperty(PropObject? obj,int id,string key,string method,long timestamp=0)
    {
        string raw="unavailable",value="object-unavailable";int tag=-1;
        try
        {
            if(obj!=null && propertyMap?.GetValue(obj) is Il2CppSystem.Collections.Generic.Dictionary<string,PropItem> map && map.ContainsKey(key))
            {
                var item=map[key];var text=new SnapshotText();tag=(int)item.PropValueType;raw=text.Value(item.Value);
                // Reuse the declared getter (including its game's forecast path).
                // No per-effect arithmetic or fabricated numeric defaults.
                var getter=obj.GetType().GetProperty(key);
                object? resolved=getter!=null && getter.GetIndexParameters().Length==0?getter.GetValue(obj):obj.GetPropValue<Il2CppSystem.Object>(key,null!);
                value=new SnapshotText().Value(resolved);
            }
            else if(obj!=null)value="property-unavailable";
        }
        catch(Exception ex){value="read-error="+ex.GetBaseException().GetType().Name;surveyErrors++;}
        string latency=timestamp==0?"":FormattableString.Invariant($" latencyMs={(Stopwatch.GetTimestamp()-timestamp)*1000.0/Stopwatch.Frequency:F2}");
        buffer.Add(player,revision,method,$"object={id} class={obj?.GetType().Name}"+latency,"key="+key+" tag="+tag+" stored="+raw,value);
        surveyRecords++;
    }
    private static void Survey()
    {
        var ids=new List<int>{Panel.LocalPlayerId,Panel.CurrentWeaponId};
        var hero=NewObjectCache.GetPlayerProp(Panel.LocalPlayerId);
        if(hero!=null && hero.ClientSummonID!=0)ids.Add(hero.ClientSummonID);
        ids.AddRange(ownedItems.Keys.OrderBy(x=>x).Take(128));ids=ids.Distinct().Where(i=>i!=0).ToList();
        if(ids.Count==0)return;
        if(surveyObject>=ids.Count)surveyObject=0;
        int id=ids[surveyObject];
        if(id!=surveyTarget){surveyOffset=0;surveyTarget=id;}
        PropObject? obj=NewObjectCache.GetPlayerProp(id);
        obj??=ItemPropCache.GetPropByItem(id);
        if(obj==null || propertyMap?.GetValue(obj) is not Il2CppSystem.Collections.Generic.Dictionary<string,PropItem> properties)
        {surveyObject++;surveyOffset=0;return;}
        long start=Stopwatch.GetTimestamp();int index=0,read=0;bool finished=true;
        foreach(var pair in properties)
        {
            if(index>=512){TraceStore.Write("effective-survey-limit",$"object={id} property traversal capped at 512");finished=true;break;}
            if(index++<surveyOffset)continue;
            if(read>=12 || Stopwatch.GetTimestamp()-start>Stopwatch.Frequency/500){finished=false;break;}
            ReadProperty(obj,id,pair.Key,"effective-property-survey");
            read++;surveyOffset++;
        }
        if(finished){surveyObject++;surveyOffset=0;}
    }
    private sealed class SnapshotText
    {
        private int entries;
        private readonly long started=Stopwatch.GetTimestamp();
        internal string Value(object? value,int depth=0)
        {
            if(value==null)return "null";
            if(++entries>40 || depth>2 || Stopwatch.GetTimestamp()-started>Stopwatch.Frequency/1000)return "[truncated]";
            if(value is string s)return s.Length>1000?s[..1000]+"[truncated]":s;
            if(value is bool || value is int || value is long || value is float || value is double)
                return Convert.ToString(value,CultureInfo.InvariantCulture)??"";
            if(value is ClientPrepDateFormat f)return $"key={f.m_keys} kind={f.m_clientValueType} value={f.m_value}";
            if(value is BulletChange.CRuleHandler r)return "rule="+r.m_RuleID;
            if(value is NewPlayerObject p)return "object="+p.ObjectID;
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,long> longs)
                return Pairs(LongPairs(longs),depth);
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,int> numbers)
                return Pairs(IntPairs(numbers),depth);
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,double> doubles)
                return Pairs(DoublePairs(doubles),depth);
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,Il2CppSystem.Object> objects)
                return Pairs(ObjectPairs(objects),depth);
            if(value is Il2CppSystem.Collections.Generic.List<int> ints)
            {var items=new List<string>();for(int i=0;i<Math.Min(ints.Count,16);i++)items.Add(Value(ints[i],depth+1));return "["+string.Join(",",items)+(ints.Count>16?",[truncated]":"")+"]";}
            if(value is Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object> list)
            {var items=new List<string>();for(int i=0;i<Math.Min(list.Count,16);i++)items.Add(Value(list[i],depth+1));return "["+string.Join(",",items)+(list.Count>16?",[truncated]":"")+"]";}
            if(value is Il2CppSystem.Object native)
            {
                string type=native.GetIl2CppType().FullName;
                if(type is "System.Int32" or "System.Int64" or "System.Single" or "System.Double" or "System.Boolean" or "System.String")return Value(TraceStore.Boxed(native,type),depth+1);
                if(type.StartsWith("System.Collections.Generic.List`1[System.Int32"))return Value(native.TryCast<Il2CppSystem.Collections.Generic.List<int>>(),depth+1);
                if(type.StartsWith("System.Collections.Generic.List`1[System.Object"))return Value(native.TryCast<Il2CppSystem.Collections.Generic.List<Il2CppSystem.Object>>(),depth+1);
                return "[native "+type+"]";
            }
            return "["+value.GetType().Name+"]";
        }
        private static IEnumerable<KeyValuePair<string,object?>> LongPairs(Il2CppSystem.Collections.Generic.Dictionary<string,long> map)
        {foreach(var x in map)yield return new(x.Key,x.Value);}
        private static IEnumerable<KeyValuePair<string,object?>> IntPairs(Il2CppSystem.Collections.Generic.Dictionary<string,int> map)
        {foreach(var x in map)yield return new(x.Key,x.Value);}
        private static IEnumerable<KeyValuePair<string,object?>> DoublePairs(Il2CppSystem.Collections.Generic.Dictionary<string,double> map)
        {foreach(var x in map)yield return new(x.Key,x.Value);}
        private static IEnumerable<KeyValuePair<string,object?>> ObjectPairs(Il2CppSystem.Collections.Generic.Dictionary<string,Il2CppSystem.Object> map)
        {foreach(var x in map)yield return new(x.Key,x.Value);}
        private string Pairs(IEnumerable<KeyValuePair<string,object?>> pairs,int depth)
        {
            var output=new List<string>();
            foreach(var pair in pairs){if(output.Count>=24 || entries>=40){output.Add("[truncated]");break;}output.Add(Value(pair.Key,depth+1)+":"+Value(pair.Value,depth+1));}
            return "{"+string.Join(",",output)+"}";
        }
    }
}
