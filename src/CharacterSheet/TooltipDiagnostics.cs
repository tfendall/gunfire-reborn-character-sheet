using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using UIScript;
using FormulaMap = Il2CppSystem.Collections.Generic.Dictionary<int, DataHelper.SFormulaRangeSub>;

internal static class TooltipDiagnostics
{
    internal static bool Enabled;
    private static readonly TooltipObservationBuffer buffer=new();
    private static int errors;
    private static double nextHealth;
    private static readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
    internal static void Install()
    {
        var harmony=new Harmony("local.gunfire.statsdiagnostic.tooltips");
        foreach(string name in new[]{"GetValue","GetDescWithExtraInfo","GetStateLevel","OperateDescByFormula",
            "OperateDesc","SpecialOperateDesc","UpdateDescObj","OnEnterState","OnExitState"})
        {
            try
            {
                var target=AccessTools.Method(typeof(PCWP_State_logic),name) ?? throw new MissingMethodException(name);
                var observer=AccessTools.Method(typeof(TooltipDiagnostics),name+"Postfix") ?? throw new MissingMethodException(name+"Postfix");
                harmony.Patch(target,postfix:new HarmonyMethod(observer));
                TraceStore.Marker("tooltip-hook-on "+name);
            }
            catch(Exception ex) { TraceStore.Marker("tooltip-hook-failed "+name+" "+ex.GetBaseException().Message); }
        }
    }
    private static void Record(PCWP_State_logic ui,string method,Func<string> input,string? result,
        int? state=null,int? sid=null)
    {
        if(!Enabled || !Panel.Capture)return;
        try
        {
            int player=ui.HeroID;
            if(player==0 || player!=Panel.LocalPlayerId)return;
            buffer.Add(method,player,state??ui.curStateId,sid??ui.curStateSid,input(),result);
        }
        catch { System.Threading.Interlocked.Increment(ref errors); }
    }
    private static string Extra(StateExtraInfo? info)
    {
        if(info==null)return "extra=unavailable";
        var values=new List<string>();
        if(info.extraDict!=null)foreach(var entry in info.extraDict)
        {if(values.Count>=16){values.Add("[truncated]");break;}values.Add(entry.Key+"="+entry.Value);}
        return $"extraState={info.stateId} performType={info.PerfromType} typeSid={info.typesid} "+string.Join(";",values);
    }
    // Positional argument names avoid depending on generated wrapper parameter
    // names. Postfixes only observe; none return a replacement or use ref results.
    private static void GetValuePostfix(PCWP_State_logic __instance,int __0,int __1,int __2,string __result)
        => Record(__instance,"GetValue",()=>"count="+__2,__result,__0,__1);
    private static void GetDescWithExtraInfoPostfix(PCWP_State_logic __instance,string __0,StateExtraInfo __1,string __result)
        => Record(__instance,"GetDescWithExtraInfo",()=>__0+" | "+Extra(__1),__result);
    private static void GetStateLevelPostfix(PCWP_State_logic __instance,StateExtraInfo __0,int __result)
        => Record(__instance,"GetStateLevel",()=>Extra(__0),__result.ToString());
    private static void OperateDescPostfix(PCWP_State_logic __instance,string __0,string __result)
        => Record(__instance,"OperateDesc",()=>__0,__result);
    private static void OperateDescByFormulaPostfix(PCWP_State_logic __instance,string __0,FormulaMap __1,string __result)
        => Record(__instance,"OperateDescByFormula",()=>
        {
            var parts=new List<string>{__0};
            if(__1!=null)foreach(var entry in __1)
            {
                if(parts.Count>=5){parts.Add("[truncated]");break;}
                if(entry.Value!=null)parts.Add($"{entry.Key}: {entry.Value.Formula} min={entry.Value.MinValue} max={entry.Value.MaxValue}");
            }
            return string.Join(" | ",parts);
        },__result);
    private static void SpecialOperateDescPostfix(PCWP_State_logic __instance,string __0,int __1,double __2,double __3,string __result)
        => Record(__instance,"SpecialOperateDesc",()=>FormattableString.Invariant($"formula={__0} relicType={__1} min={__2} max={__3}"),__result);
    private static void UpdateDescObjPostfix(PCWP_State_logic __instance,int __0,bool __1)
        => Record(__instance,"UpdateDescObj",()=>"count="+__0+" client="+__1,ReadText(__instance));
    private static string ReadText(PCWP_State_logic ui)
    {
        if(!Enabled || !Panel.Capture)return "";
        try { return (ui.descName?.text??"")+" | "+(ui.descDesc?.text??""); }
        catch { System.Threading.Interlocked.Increment(ref errors);return "[text unavailable]"; }
    }
    private static void OnEnterStatePostfix(PCWP_State_logic __instance,int __1,bool __2)
        => Record(__instance,"OnEnterState",()=>"client="+__2,"",__1);
    private static void OnExitStatePostfix(PCWP_State_logic __instance)
        => Record(__instance,"OnExitState",()=>"","");
    internal static void Drain()
    {
        foreach(var item in buffer.Drain())
            TraceStore.Write("tooltip-observation",JsonSerializer.Serialize(item));
        if(!Enabled || !Panel.Capture || clock.Elapsed.TotalSeconds<nextHealth)return;
        nextHealth=clock.Elapsed.TotalSeconds+5;
        TraceStore.Write("tooltip-health",$"queueDrops={buffer.Drops} copyErrors={errors}");
    }
}
