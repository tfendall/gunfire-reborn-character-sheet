using UnityEngine;
using UnityEngine.SceneManagement;

internal static class LoadingGuard
{
    private static readonly WorldReadiness state = new();
    internal static bool Ready;
    internal static bool DisplayReady;
    internal static void Poll()
    {
        try
        {
            // Unity engine scene identity/loading flag, not GameSceneManager or
            // game run-state APIs. Do not inspect any game-owned objects here.
            var scene = SceneManager.GetActiveScene();
            bool loaded = scene.IsValid() && scene.isLoaded;
            Ready = state.Observe(scene.handle, loaded, Cursor.visible, StatsScreen.PackageOpen, Input.GetKey(KeyCode.C), Time.unscaledTime);
            // Cached UI can display immediately once this scene has settled.
            // Menu cursor transitions still restart the live-read cooldown.
            DisplayReady = state.DisplayReady;
            if (!DisplayReady) StatsScreen.SuspendForLoading();
        }
        catch (Exception ex) { Ready = DisplayReady = false; StatsScreen.SuspendForLoading(); CrashDiagnostics.Error("unity-loading-guard", ex); }
    }
    internal static bool CanRead => Ready && !Cursor.visible;
}
