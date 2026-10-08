using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SkillBolt;
using BulletChange;
using Item;
using System.Reflection;

internal static class TraceStore
{
    private static readonly object gate = new();
    private static TraceSink? sink;
    private static ManualLogSource? log;
    private static readonly Dictionary<string,string> last = new();
    internal static string Latest = "No observed attack data yet. Open F8 and fire.";
    internal static void Start(ManualLogSource logger)
    {
        log = logger;
        var dir = Path.Combine(Paths.BepInExRootPath, "StatsDiagnostic");
        var stem = Path.Combine(dir, "trace-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        var path = stem + ".jsonl";
        sink = new TraceSink(stem);
        Marker("session-start");
        log.LogInfo("Trace file: " + path);
    }
    internal static void Marker(string marker) => Write("marker", marker, true);
    internal static string Health => sink == null ? "Trace writer unavailable" : sink.Error.Length > 0 ? "Trace writer failed: " + sink.Error : "Trace queue drops: " + sink.Drops;
    internal static void Stop() => sink?.Dispose();
    internal static bool Local(int id)
    {
        try { return Panel.Capture && Panel.LocalPlayerId != 0 && Panel.LocalPlayerId == id; }
        catch { return false; }
    }
    internal static void Write(string kind, string detail, bool force = false)
    {
        try
        {
            lock (gate)
            {
                if (sink == null || (!force && !Panel.Capture)) return;
                if (detail.Length > 16384) detail = detail[..16384] + " [trace length limit]";
                if (!force && last.TryGetValue(kind, out var old) && old == detail) return;
                if (!sink.TryWrite("{\"utc\":\"" + DateTime.UtcNow.ToString("O") + "\",\"kind\":\"" + Escape(kind) + "\",\"detail\":\"" + Escape(detail) + "\"}")) return;
                if (last.Count >= 1024) last.Clear();
                last[kind] = detail;
                if (kind != "marker") Latest = kind + ": " + (detail.Length > 650 ? detail[..650] : detail);
            }
        }
        catch (Exception ex) { log?.LogWarning("Trace write failed: " + ex.Message); }
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    internal static string Dict(Il2CppSystem.Collections.Generic.Dictionary<string,int>? dict)
    {
        if (dict == null) return "null";
        var values = new List<string>();
        foreach (var pair in dict) { if (!ReadBudget.Step()) { values.Add("[budget limit]"); break; } values.Add(pair.Key + "=" + pair.Value); if (values.Count >= 64) break; }
        values.Sort(StringComparer.Ordinal);
        return string.Join(",", values);
    }
    internal static string Collect(SkillCollectData? data)
        => data == null ? "null" : "extra{" + Dict(data.ExtraInfo) + "} base{" + Dict(data.BaseInfo) + "} collect{" + Dict(data.CollectData) + "} info{" + ObjectDict(data.CollectInfo) + "}";
    internal static string ObjectDict(Il2CppSystem.Collections.Generic.Dictionary<string,Il2CppSystem.Object>? dict)
    {
        if (dict == null) return "null";
        var values = new List<string>();
        foreach (var pair in dict)
        {
            if (!ReadBudget.Step()) { values.Add("[budget limit]"); break; }
            string value = Boxed(pair.Value);
            if (value.Length > 100) value = value[..100];
            values.Add(pair.Key + "=" + value);
            if (values.Count >= 64) break;
        }
        values.Sort(StringComparer.Ordinal);
        return string.Join(",", values);
    }
    internal static string Skill(CSkillBase skill)
        => $"act={skill.ActNum} weapon={skill.WeaponSID}/{skill.Weapon} perform={skill.Perform} victim={skill.Victim} weaponSkill={skill.isWeaponSkill} charge={skill.CustomData?.ChargeLevel} custom{{{Dict(skill.CustomData?.CustomArgs)}}} " + Collect(skill.CollectData);
    internal static string Boxed(Il2CppSystem.Object? value, string? knownType = null)
    {
        if (value == null) return "null";
        string type = knownType ?? value.GetIl2CppType().FullName;
        return type switch
        {
            "System.Int32" => value.Unbox<int>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            "System.Int64" => value.Unbox<long>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            "System.Single" => value.Unbox<float>().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "System.Double" => value.Unbox<double>().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            "System.Boolean" => value.Unbox<bool>().ToString(),
            "System.String" => value.ToString(),
            _ => "[unmapped " + type + "]"
        };
    }
}

// This is a cached-property calculation, NOT proven to be final hit resolution.
[HarmonyPatch(typeof(CSkillBase), "CalculatePropCacheAddition")]
public static class CacheProbe
{
    public static void Postfix(CSkillBase __instance, string propName, int __result, bool isFloatRes)
    {
        try { if (TraceStore.Local(__instance.AttID)) TraceStore.Write("cached-property", $"act={__instance.ActNum} weapon={__instance.WeaponSID} {propName}={__result} float={isFloatRes}"); } catch { }
    }
}

[HarmonyPatch]
public static class FixedProbe
{
    public static MethodBase TargetMethod() => typeof(CSkillBase).GetMethods().Single(m => m.Name == "AddBulletChangeFixedArg" && m.GetParameters().Length == 3);
    public static void Postfix(CSkillBase __instance, string propName, int value, int __result)
    {
        try { if (TraceStore.Local(__instance.AttID)) TraceStore.Write("fixed-snapshot", $"act={__instance.ActNum} {propName} input={value} result={__result}"); } catch { }
    }
}

[HarmonyPatch]
public static class ScaleProbe
{
    public static MethodBase TargetMethod() => typeof(CSkillBase).GetMethods().Single(m => m.Name == "AddBulletChangePerTenThousandArg" && m.GetParameters().Length == 3);
    public static void Postfix(CSkillBase __instance, string propName, int value, int __result)
    {
        try { if (TraceStore.Local(__instance.AttID)) TraceStore.Write("scaled-snapshot", $"act={__instance.ActNum} {propName} input={value} result={__result}"); } catch { }
    }
}

[HarmonyPatch(typeof(CWeaponPerformance), nameof(CWeaponPerformance.SingleAttack))]
public static class AttackProbe
{
    public static void Prefix(CWeaponPerformance __instance, SkillCollectData final)
    {
        try { if (TraceStore.Local(__instance.OwnerID)) TraceStore.Write("attack-observed", $"weapon={__instance.Sid}/{__instance.ObjectID}"); } catch { }
    }
}

[HarmonyPatch(typeof(CSkillCollectDataMgr), nameof(CSkillCollectDataMgr.PacketBulletChange))]
public static class PacketProbe
{
    public static void Prefix(CSkillBase skillbase)
    {
        try { if (skillbase != null && TraceStore.Local(skillbase.AttID)) TraceStore.Write("outgoing-skill-observed", $"act={skillbase.ActNum} perform={skillbase.Perform} weapon={skillbase.Weapon}"); } catch { }
    }
}
