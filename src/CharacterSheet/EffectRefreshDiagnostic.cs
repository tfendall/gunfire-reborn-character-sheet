using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

// Data-pattern coverage; descriptions and arithmetic are supplied by the game.
// No HUD mutation, shared tooltip parameter dictionary, or gameplay-rule replay.
internal static class EffectRefreshDiagnostic
{
    internal static bool Enabled;
    internal static bool ShowInBuild;
    internal static readonly EffectiveBonusView View=new();
    internal static double Now=>clock.Elapsed.TotalSeconds;
    private static readonly Stopwatch clock=Stopwatch.StartNew();
    private static readonly Dictionary<int,string> fingerprints=new();
    private static readonly Dictionary<int,string> unresolved=new();
    private static readonly PropertyInfo? tableInstance=typeof(DataHelper.StateData).BaseType?
        .GetProperty("s_instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
    private static double next;
    private static int player,epoch,scene,afterId;
    internal static void Tick()
    {
        if(!Enabled && !ShowInBuild)return;
        int currentScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if(player!=Panel.LocalPlayerId || epoch!=DamageCapture.Epoch || scene!=currentScene)
        {
            View.Clear();fingerprints.Clear();unresolved.Clear();afterId=0;player=Panel.LocalPlayerId;epoch=DamageCapture.Epoch;scene=currentScene;
            TraceStore.Write("effect-refresh-reset",$"player={player} epoch={epoch} scene={scene}");
        }
        if(!Panel.RunActive || player==0){View.Clear();fingerprints.Clear();return;}
        if(!Panel.Capture || !LoadingGuard.CanRead || !StatsScreen.ReadsAllowed || clock.Elapsed.TotalSeconds<next)return;
        next=clock.Elapsed.TotalSeconds+0.5;
        var caches=StateManager.StateCacheDict;
        if(caches==null || !caches.ContainsKey(player))return;
        var states=caches[player]?.StateDict;
        var table=tableInstance?.GetValue(null) as DataHelper.StateData;
        if(states==null || table==null)return;
        var seen=new HashSet<int>();bool complete=true;
        foreach(var entry in states)
        {
            if(seen.Count>=512){complete=false;break;}
            seen.Add(entry.Key);
        }
        if(!complete)TraceStore.Write("effect-refresh-limit","server state enumeration capped at 512; coverage incomplete");
        // Remove vanished effects on every complete scan, before the rotating work batch.
        if(complete)foreach(var row in View.Snapshot())if(!seen.Contains(row.Id))View.Remove(row.Id);
        foreach(int id in EffectExpression.Batch(seen,afterId))
        {
            afterId=id;
            if(!states.ContainsKey(id))continue;
            var state=states[id];
            if(state==null)continue;
            string displayName="";
            try
            {
                var definition=table.GetStateData(state.SID);
                displayName=definition?.Name??"";
                if(!state.CanShow || displayName.StartsWith("#",StringComparison.Ordinal))displayName="";
                if(displayName.Length==0)View.Remove(id);
                // These native tooltip branches override generic inputs/formatting.
                // They remain explicit unsupported cases, not an item inclusion list.
                if(state.SID is 1668 or 8037)throw new InvalidOperationException("special-tooltip-variable-producer");
                if(definition?.StateDescDic==null || definition.StateDescDic.Count!=1)
                    throw new InvalidOperationException("unsupported-description-selection");
                string description="";
                foreach(var item in definition.StateDescDic)
                {
                    if(item.Value==null || (item.Value.FormulaRange!=null && item.Value.FormulaRange.Count!=0))
                        throw new InvalidOperationException("unsupported-formula-range");
                    if(item.Value.Level!=0 || item.Value.Tier!=0)
                        throw new InvalidOperationException("unsupported-description-level-or-tier");
                    description=item.Value.Desc;
                }
                if(string.IsNullOrWhiteSpace(description))throw new InvalidOperationException("no-description");
                var extra=StateManager.GetStateExtraInfo(id);
                var values=new SortedDictionary<string,long>(StringComparer.Ordinal);
                if(extra?.extraDict!=null)foreach(var item in extra.extraDict)
                {
                    if(values.Count>=32)throw new InvalidOperationException("extra-data-limit");
                    values[item.Key]=item.Value;
                }
                // Use only this state's current data. Missing dependencies stay unknown.
                values["n"]=state.Count;
                if(values.TryGetValue("ExcessiveDam",out long damage))values["dam"]=damage;
                if(values.TryGetValue("SrcLV",out long level))values["lvl"]=level;
                else if(extra!=null && ((int)extra.PerfromType | 0x4000)==0x4004)
                    values["lvl"]=Talent.TalentManager.GetTalentCurlevel(extra.typesid);
                EffectExpression.Validate(description,values);
                string fingerprint=JsonSerializer.Serialize(new {state.SID,description,values});
                if(fingerprints.TryGetValue(id,out string? old) && old==fingerprint &&
                    (displayName.Length==0 || View.Snapshot().Any(row=>row.Id==id && row.Resolved)))
                {if(displayName.Length>0)View.Touch(id,Now);continue;}
                var native=new Il2CppSystem.Collections.Generic.Dictionary<string,long>();
                foreach(var item in values)native.Add(item.Key,item.Value);
                var calculations=new List<object>();
                long started=Stopwatch.GetTimestamp();
                string previous=InvestigationScope.Push($"diagnostic-refresh state={id} sid={state.SID}");
                string result;
                try
                {
                    result=EffectExpression.Render(description,values,expression=>
                    {
                        string answer=Evaluate.OperateWithParam(expression,native,double.MinValue,double.MaxValue);
                        if(Enabled)calculations.Add(new {expression,answer});return answer;
                    });
                }
                finally{InvestigationScope.Restore(previous);}
                if(Enabled)TraceStore.Write("effect-refresh-result",JsonSerializer.Serialize(new {utc=DateTime.UtcNow,player,epoch,scene,
                    stateId=id,sid=state.SID,name=definition.Name,description,inputs=values,result,calculations,
                    durationMs=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency,source=calculations.Count==0?"game-static-description":"diagnostic-game-evaluator"}));
                fingerprints[id]=fingerprint;unresolved.Remove(id);
                if(displayName.Length>0)View.Set(id,displayName,result,Now,true,
                    extra==null?"":((int)extra.PerfromType & ~0x4000) switch {4=>"ascension",5=>"scroll",18=>"gem",_=>""},extra?.typesid??0);
            }
            catch(Exception ex)
            {
                fingerprints.Remove(id);
                if(displayName.Length>0)View.Set(id,displayName,"",Now,false);
                string reason=$"sid={state.SID} reason={ex.GetBaseException().Message}";
                if(!unresolved.TryGetValue(id,out string? oldReason) || oldReason!=reason)
                    TraceStore.Write("effect-refresh-unresolved",$"player={player} epoch={epoch} state={id} "+reason);
                unresolved[id]=reason;
            }
        }
        if(complete)foreach(int removed in fingerprints.Keys.Concat(unresolved.Keys).Distinct().Where(id=>!seen.Contains(id)).ToArray())
        {
            fingerprints.Remove(removed);unresolved.Remove(removed);View.Remove(removed);
            TraceStore.Write("effect-refresh-removed",$"player={player} epoch={epoch} state={removed}");
        }
    }
}
