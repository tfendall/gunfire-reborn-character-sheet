using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using UnityEngine;
using HarmonyLib;
using SkillBolt;
using System.Runtime.CompilerServices;

[BepInPlugin("local.gunfire.statsdiagnostic", "Gunfire Stats Diagnostic", Plugin.ModVersion)]
public class Plugin : BasePlugin
{
    internal const string ModVersion = "0.13.12";
    public override void Load()
    {
        Panel.Logger = Log;
        AddComponent<Panel>();
        TraceStore.Start(Log);
        CrashDiagnostics.Install();
        Panel.AutoCapture = Config.Bind("Stats", "AutomaticCapture", true, "Capture stats during runs without showing the F8 diagnostic panel. Stats appear on the C/package screen.").Value;
        // Compatibility test: no new UI/lifecycle code is reached at bootstrap.
        // F8 after a valid player/weapon enables the advanced path for this launch.
        Panel.Capture = false;
        Panel.ObserverName = Config.Bind("Diagnostics", "Observer", "AttackProbe", "One observer per launch: AttackProbe, PacketProbe, CacheProbe, FixedProbe, or ScaleProbe. F10 enables it during the run.").Value;
        Log.LogInfo("Automatic stats start after player/weapon readiness has been stable for two seconds. Open C for stats; F8 only shows diagnostics.");
        CrashDiagnostics.Complete("plugin-load");
    }
}

public class Panel : MonoBehaviour
{
    public Panel(IntPtr pointer) : base(pointer) { }
    internal static ManualLogSource? Logger;
    private bool visible;
    internal static volatile bool Capture;
    internal static bool AutoCapture = true;
    internal static volatile int LocalPlayerId;
    internal static volatile int CurrentWeaponId;
    internal static int CurrentWeaponSid;
    internal static volatile bool RunActive;


    private bool gameReady;
    private bool featuresEnabled;
    private float readySince = -1;
    private bool damageAttempted;
    private float nextInventory;
    internal static string ObserverName = "AttackProbe";
    private string observerStatus = "Experimental observer OFF — F10 optional";
    private bool observerAttempted;
    private float nextPoll;
    private string snapshot = "Waiting for a solo run.";
    private string previous = "";
    private bool errorLogged;
    private float nextPanelText;
    private string panelText = "";

    public void Update()
    {
        LoadingGuard.Poll();
        if (Input.GetKeyDown(KeyCode.F8))
        {
            visible = !visible;
            if (!featuresEnabled && gameReady && LocalPlayerId != 0 && CurrentWeaponId != 0)
            {
                featuresEnabled = true;
                TraceStore.Marker("advanced-features-enabled-after-player-ready");
            }
            Capture = featuresEnabled && (AutoCapture || visible);
            TraceStore.Marker(visible ? "diagnostics-open" : "diagnostics-close");
        }
        if (Input.GetKeyDown(KeyCode.F9)) TraceStore.Marker("manual-test-marker");
        if (Input.GetKeyDown(KeyCode.F7)) ResetStats();
        if (!featuresEnabled && AutoCapture && gameReady && readySince >= 0 && Time.unscaledTime - readySince >= 2)
        {
            featuresEnabled = Capture = true;
            CrashDiagnostics.Begin("automatic-feature-enable");
            TraceStore.Marker("advanced-features-enabled-automatically");
            CrashDiagnostics.Complete("automatic-feature-enable");
        }
        if (featuresEnabled)
        {
            try { TickAdvanced(); }
            catch (Exception ex) { CrashDiagnostics.Error("advanced-update", ex); DisableAdvanced(ex); featuresEnabled = false; Capture = false; readySince = -1; }
        }
        try { DamageCapture.Drain(); }
        catch (Exception ex) { DamageCapture.Status = "Damage processing failed: " + ex.Message; CrashDiagnostics.Error("damage-processing", ex); }
        TickPlayer();
    }
    // Keep these native type dependencies behind a method boundary, not just
    // visibility checks inside StatsScreen.Draw/Poll.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TickAdvanced()
    {
        CrashDiagnostics.Begin("stats-screen-poll");
        StatsScreen.Poll();
        CrashDiagnostics.Complete("stats-screen-poll");
    }
    private void TickPlayer()
    {
        if (!LoadingGuard.CanRead) return;
        if (Input.GetKeyDown(KeyCode.F10) && !observerAttempted)
        {
            observerAttempted = true;
            try
            {
                Type type = ObserverName switch { "AttackProbe" => typeof(AttackProbe), "PacketProbe" => typeof(PacketProbe), "CacheProbe" => typeof(CacheProbe), "FixedProbe" => typeof(FixedProbe), "ScaleProbe" => typeof(ScaleProbe), _ => throw new InvalidOperationException("Unknown observer: " + ObserverName) };
                new Harmony("local.gunfire.statsdiagnostic.single").CreateClassProcessor(type).Patch();
                observerStatus = "Observer ON: " + ObserverName;
                Capture = visible = true;
                Logger?.LogInfo(observerStatus);
                TraceStore.Marker(observerStatus);
            }
            catch (Exception ex) { observerStatus = "Observer failed — see log"; Logger?.LogError(ex); }
        }
        if (Capture && RunActive && StatsScreen.ReadsAllowed && Time.unscaledTime >= nextInventory)
        {
            nextInventory = Time.unscaledTime + 0.1f;
            try
            {
                var player = AimAssistCtrl.HeroObj;
                if (player != null && player.ObjectID == LocalPlayerId)
                    InventoryCapture.Sample(player, CurrentWeaponId == 0 ? null : ItemPropCache.GetPropByItem(CurrentWeaponId));
            }
            catch (Exception ex) { TraceStore.Write("inventory-poll-error", ex.Message); }
        }
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.5f;
        try
        {
            CrashDiagnostics.Reading("player-lookup");
            var hero = AimAssistCtrl.HeroObj;
            CrashDiagnostics.ReadDone("player-lookup");
            if (hero == null) { RunActive = false; LocalPlayerId = CurrentWeaponId = 0; gameReady = false; readySince = -1; snapshot = "Waiting for player / solo run."; }
            else
            {
                if (LocalPlayerId != hero.ObjectID)
                {
                    if (LocalPlayerId != 0)
                    {
                        DamageCapture.Reset(); BaseLsc.Reset();
                        TraceStore.Marker("local-player-changed");
                    }
                    LocalPlayerId = hero.ObjectID;
                }
                CrashDiagnostics.Reading("current-weapon-id");
                int id = hero.PlayerCom == null ? 0 : hero.PlayerCom.CurWeaponID;
                CrashDiagnostics.ReadDone("current-weapon-id");
                CurrentWeaponId = id;
                // Player/weapon presence only; no game scene or run-state calls.
                RunActive = id != 0;
                if (id == 0) { gameReady = false; readySince = -1; }
                // Do not initialize scene/UI manager classes during bootstrap.
                // The original proven polling path only touches these after a
                // local player with a weapon has been created by the game.
                if (!gameReady && id != 0)
                {
                    gameReady = true;
                    readySince = Time.unscaledTime;
                    TraceStore.Marker("game-readiness-confirmed");
                }
                if (Capture && RunActive && !damageAttempted)
                {
                    damageAttempted = true;
                    try
                    {
                        CrashDiagnostics.Begin("damage-hook-install");
                        new Harmony("local.gunfire.statsdiagnostic.damage").CreateClassProcessor(typeof(DamageEventProbe)).Patch();
                        CrashDiagnostics.Complete("damage-hook-install");
                        DamageCapture.Status = "Damage observer ON";
                        Logger?.LogInfo(DamageCapture.Status);
                        TraceStore.Marker("damage-observer-on");
                    }
                    catch (Exception ex) { DamageCapture.Status = "Damage observer FAILED; see log"; CrashDiagnostics.Error("damage-hook-install", ex); }
                }
                CrashDiagnostics.Reading("weapon-property-lookup");
                var weapon = id == 0 ? null : ItemPropCache.GetPropByItem(id);
                string source = "ItemPropCache";
                if (weapon == null && id != 0 && hero.PreBulletCom != null)
                {
                    var performance = hero.PreBulletCom.ReturnWeapon(id);
                    if (performance != null) weapon = performance.WeaponAttr;
                    source = "PreBulletCom.WeaponAttr";
                }
                CrashDiagnostics.ReadDone("weapon-property-lookup");
                CurrentWeaponSid = weapon == null ? 0 : weapon.SID;
                snapshot = weapon == null
                    ? $"Player: {hero.ObjectID}\nCurrent item: {id}\nWeapon properties unavailable."
                    : $"Player: {hero.ObjectID}\nCurrent weapon: {id} ({source})\nWeapon SID: {CurrentWeaponSid}  Level: {weapon.Grade}\nLuckyHit RAW (unverified): {weapon.LuckyHit}\nCrazyEff RAW (unverified): {weapon.CrazyEff}\nAtt RAW (unverified): {weapon.Att}";
                if (Capture && RunActive)
                {
                    if (StatsScreen.ReadsAllowed)
                    {
                        CrashDiagnostics.Reading("numeric-properties");
                        LiveProperties.Sample(hero.playerProp, weapon);
                        CrashDiagnostics.ReadDone("numeric-properties");
                        BaseLsc.Sample(hero, weapon);
                    }
                }
            }
            if (snapshot != previous)
            {
                Logger?.LogInfo(snapshot.Replace("\n", " | "));
                previous = snapshot;
            }
        }
        catch (Exception ex)
        {
            snapshot = "Could not read properties. See BepInEx log.";
            if (!errorLogged) { Logger?.LogError(ex); errorLogged = true; }
        }
    }

    public void OnGUI()
    {
        if (featuresEnabled) DrawAdvanced();
        if (!visible || StatsScreen.PackageOpen) return;
        GUI.Box(new Rect(20, 50, 700, 650), "Gunfire Stats Diagnostic 0.13.0 — F8 diagnostics / F9 marker");
        if (Time.unscaledTime >= nextPanelText)
        {
            nextPanelText = Time.unscaledTime + 0.25f;
            panelText = snapshot + "\n\n" + DamageCapture.Status + "\n" + DamageCapture.Summary + "\n\nLive luck-related properties (unverified):\n" + LiveProperties.Summary + "\n\n" + InventoryCapture.Summary + "\nModifier coverage: " + string.Join(" / ",ModifierRuntime.Findings.GroupBy(f=>f.Status).Select(group=>group.Key+" "+group.Count())) + "\n\n" + observerStatus + "\n" + TraceStore.Health + "\nTrace: BepInEx/StatsDiagnostic/";
        }
        GUI.Label(new Rect(35, 85, 670, 600), panelText);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DrawAdvanced()
    {
        try { CrashDiagnostics.Begin("stats-draw-entry"); StatsScreen.Draw(); StatsScreen.DrawSucceeded(); CrashDiagnostics.Complete("stats-draw-entry"); }
        catch (Exception ex) { CrashDiagnostics.Error("stats-draw", ex); StatsScreen.DrawFailed(ex); }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeAdvanced() => StatsScreen.Dispose();
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisableAdvanced(Exception error) => StatsScreen.DrawFailed(error);
    internal static void ResetStats() { DamageCapture.Reset(); BaseLsc.Reset(); TraceStore.Marker("manual-stats-reset"); }
    public void OnDestroy() { Capture = false; if (featuresEnabled) DisposeAdvanced(); TraceStore.Stop(); }
}

// Observe numeric per-shot contributions without changing arguments or results.
[HarmonyPatch(typeof(SkillCollectData), nameof(SkillCollectData.SetCollectData))]
public static class CollectProbe
{
    private static readonly Dictionary<string, int> last = new();
    private static int count;
    public static void Postfix(string reason, int val)
    {
        try
        {
            if (!Panel.Capture || count >= 300) return;
            if (last.TryGetValue(reason, out int previous) && previous == val) return;
            last[reason] = val;
            count++;
            Panel.Logger?.LogInfo($"Shot contribution: {reason} = {val}");
        }
        catch { /* Diagnostic logging must not interrupt the game. */ }
    }
}

[HarmonyPatch(typeof(SkillCollectData), nameof(SkillCollectData.SetBulletBase))]
public static class BaseProbe
{
    public static void Postfix(string ModifyReason, int cost)
        => CollectProbe.Postfix("Base:" + ModifyReason, cost);
}
