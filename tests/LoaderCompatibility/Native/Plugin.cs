using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

[BepInPlugin("local.gunfire.loadercompat-smoke", "Loader Compatibility Smoke", "0.1.0")]
public class Plugin : BasePlugin
{
    private static int calls;
    private static void AfterFrameCount(ref int __result) => Interlocked.Increment(ref calls);

    public override void Load()
    {
        var harmony = new Harmony("local.gunfire.loadercompat-smoke.patch");
        try
        {
            if (Environment.Version.Major != 10 || typeof(Harmony).Assembly.GetName().Version != new Version(2,16,1,0))
                throw new Exception("Unexpected runtime or HarmonyX version");
            var target = AccessTools.PropertyGetter(typeof(UnityEngine.Time), "frameCount");
            var initial = UnityEngine.Time.frameCount;
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AfterFrameCount), BindingFlags.Static | BindingFlags.NonPublic)));
            var patched = UnityEngine.Time.frameCount;
            if (calls != 1) throw new Exception($"Expected one native getter postfix; saw {calls}");
            harmony.UnpatchSelf();
            var restored = UnityEngine.Time.frameCount;
            if (calls != 1 || patched < initial || restored < patched)
                throw new Exception("Native getter restoration or result failed");
            Log.LogInfo("PASS HarmonyX 2.16.1 native IL2CPP getter patch, invocation and unpatch on .NET 10.0.12");
        }
        catch(Exception error)
        {
            Log.LogError($"FAIL native compatibility smoke: {error}");
        }
        finally
        {
            harmony.UnpatchSelf();
        }
    }
}
