using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;

var platform = Assembly.Load("BepInEx.Preloader.Core").GetType("BepInEx.Preloader.Core.PlatformUtils", true)!;
platform.GetMethod("SetPlatform", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null);
if (!BepInEx.PlatformHelper.Is(BepInEx.Platform.Windows)) throw new Exception("Platform detection failed");
Console.WriteLine("PASS .NET 10 loader platform initialization");
var target = typeof(Probe).GetMethod(nameof(Probe.Value))!;
var harmony = new Harmony("gunfire.loader.net10.smoke");
harmony.Patch(target, postfix: new HarmonyMethod(typeof(Probe).GetMethod(nameof(Probe.Postfix))!));
if (Probe.Value(4) != 15) throw new Exception("Harmony patch did not run");
harmony.UnpatchSelf();
if (Probe.Value(4) != 5) throw new Exception("Harmony unpatch did not restore original");
Console.WriteLine("PASS HarmonyX 2.16.1 managed detour and restoration on .NET 10");
var library = Assembly.Load("BepInEx.Unity.IL2CPP");
var generator = library.GetType("BepInEx.Unity.IL2CPP.Hook.NativeTrampoline", true)!.GetMethod("Generate", BindingFlags.Static | BindingFlags.Public)!;
NativeFunction original = value => value * 3;
var pointer = Marshal.GetFunctionPointerForDelegate(original);
var proxy = (MethodInfo)generator.Invoke(null, new object[] { pointer, typeof(NativeFunction).GetMethod("Invoke")! })!;
var callable = proxy.CreateDelegate<NativeFunction>();
if (callable(7) != 21) throw new Exception("Native trampoline returned wrong result");
GC.KeepAlive(original);
Console.WriteLine("PASS native trampoline calling convention and signature");

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int NativeFunction(int value);
static class Probe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Value(int input) => input + 1;
    public static void Postfix(ref int __result) => __result += 10;
}
