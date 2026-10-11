using System.Reflection;
using System.Text.Json;

// Diagnostic evidence only: state display formulas are not assumed to be
// additive combat modifiers. Never invoke the UI's formula evaluator here.
internal static class StatusEffectProbe
{
    internal static bool Enabled;
    private static int player, afterId;
    private static readonly PropertyInfo? tableInstance = typeof(DataHelper.StateData).BaseType?
        .GetProperty("s_instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    internal static string Sample(int playerId)
    {
        if (!Enabled) return "disabled";
        if (player != playerId) { player = playerId; afterId = 0; }
        // Use the existing server cache and already-initialized configuration
        // table; do not force singleton/UI initialization or retain game objects.
        var caches = StateManager.StateCacheDict;
        if (caches == null || !caches.ContainsKey(playerId)) return "no server cache";
        var states = caches[playerId]?.StateDict;
        if (states == null) return "no server states";
        var ids = new List<int>();
        bool complete = true;
        foreach (var entry in states)
        {
            if (!ReadBudget.Step() || ids.Count >= 128) { complete = false; break; }
            ids.Add(entry.Key);
        }
        // Process one instance per inventory cycle, rotating fairly across
        // instances with the same SID. The regular stack reader remains separate.
        int id = StatusProbeSelection.Next(ids, afterId);
        if (id == 0) return complete ? "no server states" : "state enumeration incomplete";
        if (!ReadBudget.Step()) return "[budget limit]";
        var state = states[id];
        if (state == null) { afterId=id;return "state disappeared"; }
        var table = tableInstance?.GetValue(null) as DataHelper.StateData;
        var definition = table?.GetStateData(state.SID);
        var extra = StateManager.GetStateExtraInfo(id);
        var detail = new Dictionary<string,object?>
        {
            ["player"]=playerId, ["stateId"]=id, ["sid"]=state.SID,
            ["attacker"]=state.Attacker, ["count"]=state.Count, ["viewCount"]=state.ViewCount,
            ["maxCount"]=state.MaxCount, ["remainingSeconds"]=state.RemainTime,
            ["immortal"]=state.IsImmortal, ["canShow"]=state.CanShow,
            ["hasCountView"]=state.HasCountView, ["enumerationComplete"]=complete,
            ["definitionAvailable"]=definition!=null, ["extraAvailable"]=extra!=null
        };
        bool detailComplete=true;
        string Text(string? value)
        {
            if(value==null)return "";
            if(value.Length<=2048)return value;
            detailComplete=false;return value[..2048]+" [truncated]";
        }
        if (definition != null)
        {
            detail["name"]=Text(definition.Name);
            detail["attribute"]=Text(definition.AttName);
            detail["valueFormula"]=Text(definition.ValueFormula);
            detail["stateType"]=Text(definition.StateType);
            detail["judgeType"]=Text(definition.JudgeType);
            detail["behaviorId"]=definition.BehaviorID;
            detail["countBehavior"]=definition.StateCountBehav;
            var descriptions = new List<object>();
            var entries=definition.StateDescDic;
            if(entries!=null)foreach(var entry in entries)
            {
                if(!ReadBudget.Step() || descriptions.Count>=4) { detailComplete=false;break; }
                var description=entry.Value;
                if(description==null)continue;
                var formulas=new List<object>();
                if(description.FormulaRange!=null)foreach(var formula in description.FormulaRange)
                {
                    if(!ReadBudget.Step() || formulas.Count>=4) { detailComplete=false;break; }
                    if(formula.Value!=null)formulas.Add(new { key=formula.Key,formula=Text(formula.Value.Formula),min=Text(formula.Value.MinValue),max=Text(formula.Value.MaxValue) });
                }
                descriptions.Add(new { key=entry.Key,level=description.Level,tier=description.Tier,text=Text(description.Desc),formulas });
            }
            detail["descriptions"]=descriptions;
        }
        if(extra!=null)
        {
            detail["extraStateId"]=extra.stateId;
            detail["performType"]=extra.PerfromType;
            detail["typeSid"]=extra.typesid;
            var values=new SortedDictionary<string,long>(StringComparer.Ordinal);
            if(extra.extraDict!=null)foreach(var entry in extra.extraDict)
            {
                if(!ReadBudget.Step() || values.Count>=32) { detailComplete=false;break; }
                values[Text(entry.Key)]=entry.Value;
            }
            detail["extraValues"]=values;
        }
        detail["detailComplete"]=detailComplete;
        string json=JsonSerializer.Serialize(detail);
        // Stay below TraceStore's per-record truncation limit, retaining valid
        // nested JSON even when a definition contains unusually large text.
        if(json.Length>14000)
        {
            detail.Remove("descriptions");detail["detailComplete"]=false;
            json=JsonSerializer.Serialize(detail);
        }
        if(json.Length>14000)
        {
            detail.Remove("extraValues");
            json=JsonSerializer.Serialize(detail);
        }
        TraceStore.Write("active-status-detail",json);
        afterId=id;
        return $"sid={state.SID} state={id} definition={definition!=null} extra={extra!=null} complete={detailComplete && complete}";
    }
}
