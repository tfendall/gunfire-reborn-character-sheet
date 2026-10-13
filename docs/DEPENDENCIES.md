# Dependency compatibility

The dependency upgrade was checked on October 10–11, 2026, against Windows Steam game build 25361614. The current baseline uses a locally modified BepInEx loader, HarmonyX 2.16.1, and .NET 10.0.12. The mod compiles against regenerated interop assemblies with no warnings or errors. Standalone platform/managed-detour/native-trampoline checks pass, and bounded Steam startup tests load both production plugins. A temporary in-game probe successfully patches, invokes, and unpatches a native IL2CPP getter; it is removed after the test, and the game is automatically closed.

The user's earlier short gameplay check on October 11 confirmed the C panel and updating totals on the preceding stock-788/.NET-6 baseline. On October 13 the user confirmed that the upgraded dependencies are working during gameplay. Long-session stability has not been established.

| Component | Previous version | Current baseline |
|---|---|---|
| BepInEx Unity IL2CPP Windows x64 | 6.0.0-be.697 / 5362580 | Local 6.0.0-gunfire-net10.1, based on upstream 788 / 5b766a3 |
| Il2CppInterop | 1.4.6-ci.426 | 1.5.3, supplied by build 788 |
| Unity Doorstop | 4.3.0 | 4.5.0, supplied by build 788 |
| Cpp2IL | development.972 | development.1452, supplied by build 788 |
| Embedded .NET runtime | 6.0.7 | 10.0.12 |
| HarmonyX | 2.10.2 | 2.16.1 |
| MonoMod RuntimeDetour / Utils | 22.05.01.01 | 25.3.4 / 25.0.12 |
| MonoMod Core | Not used by the old stack | 1.3.4, including its .NET 10 implementation |
| EmberCloak distribution | 1.2.2 | 1.2.2, current Nexus distribution |
| Restored metadata | Steam build 25361614 | Same; current Nexus download and exact game match |
| Developer SDK | .NET 8 in the development guide | .NET 10.0.401 |
| Managed test target | net8.0 | net10.0 |

The mod and custom loader now target `net10.0`. The developer SDK and game runtime are separate installations.

## Loader changes

Simply replacing HarmonyX and its NuGet dependencies in stock BepInEx failed with `TypeLoadException`: the loader references `MonoMod.Utils.PlatformHelper`, which the newer MonoMod no longer supplies. HarmonyX 2.11.0 already moved to MonoMod 25. Stock BepInEx also failed with an access violation under the embedded .NET 10 runtime.

The local patch updates the four IL2CPP loader projects to .NET 10 and the new HarmonyX/MonoMod versions. It supplies a BepInEx-owned platform compatibility facade, uses `RuntimeInformation.ProcessArchitecture` in place of PE inspection of CoreCLR's native core library, replaces removed delegate/library helpers with supported .NET APIs, and replaces the old `IDetour`/`DetourHelper.GenerateNativeProxy` dependency with the loader's explicit native-detour contract and a native `calli` trampoline. The existing Dobby native provider and export-thunk handling are retained.

This is our modified Windows IL2CPP variant, not an official upstream BepInEx release. Other plugins and operating systems have not been certified. Version 0.13.38 supplies a prebuilt LoaderOverlay ZIP and a separate LoaderSource ZIP, so installation does not require compilation.

## Building the custom loader

From this repository, with Git and the .NET 10 SDK installed:

```powershell
./scripts/Build-Loader.ps1
```

The script clones upstream commit `5b766a3b7f6c164d4798924a93f3acf4db769d06`, applies `dependencies/bepinex-net10.patch`, and builds the local `6.0.0-gunfire-net10.1` variant into `artifacts/custom-loader`. It verifies the official build-788 archive before extracting its unchanged `dobby.dll`. It records source/patch provenance and output hashes in `build-info.json`, and preserves the upstream LGPL-2.1 license. The custom build reports some existing upstream XML-documentation and unused-variable warnings; the Character Sheet build is clean.

Close the game and preserve the previous loader/runtime/plugins before installing. Install the complete official build-788 archive as described in INSTALL.md, then copy the custom build's `*.dll`, `*.xml`, and `*.config` files into `BepInEx/core`, overwriting matching files. Install the .NET 10.0.12 runtime as described in INSTALL.md, preserving the additional BepInEx support assemblies. Keep the Unity library archive cached before launching through Steam.

Rebuilding can change file hashes; the installer verifies the exact checked baseline and labels differing builds unverified. Do not change the manifest merely to suppress that warning. Our Setup and ModOnly ZIPs include neither the custom loader nor third-party prerequisites. The separate LoaderOverlay ZIP supplies the changed loader/dependency files over official build 788; LoaderSource ZIP supplies the corresponding modified source tree, patch, build recipe, notices, and package provenance. It excludes Unity/game reference binaries, which the build recipe obtains from pinned upstream.

Standalone loader checks:

```powershell
& "$env:USERPROFILE/.dotnet/dotnet.exe" run --project tests/LoaderCompatibility/LoaderCompatibility.csproj -c Release
```

Bounded native integration check (automatically closes its game launch and removes its temporary probe):

```powershell
./scripts/Test-LoaderStartup.ps1 -GameDir 'YOUR_GAME_FOLDER' -NativeProbe
```

## Runtime and first-launch preparation

BepInEx's `dotnet` directory contains both the Microsoft runtime and additional BepInEx logging/dependency-injection assemblies. A runtime replacement must preserve those extra assemblies. Follow INSTALL.md; the compatibility manifest verifies the custom loader support files and .NET 10 runtime separately.

Build 788 downloads Unity base libraries when it regenerates interop references. On this installation that download caused an access violation in `WinHttpOpen`, with Doorstop's `winhttp.dll` loaded in the game. Downloading the official `2018.4.36.zip` outside the game process and placing it in `BepInEx/unity-libs` allowed generation to complete. The archive's SHA-256 is `9562fb2033e84677b9f3f713926318ff3d068f31aa539e03930d62d19b1a9b02`.

## Official sources

- [BepInEx bleeding-edge builds](https://builds.bepinex.dev/projects/bepinex_be)
- [Il2CppInterop releases](https://github.com/BepInEx/Il2CppInterop/releases)
- [HarmonyX releases](https://github.com/BepInEx/HarmonyX/releases)
- [HarmonyX 2.11.0 dependency transition](https://github.com/BepInEx/HarmonyX/releases/tag/v2.11.0)
- [Unity Doorstop releases](https://github.com/NeighTools/UnityDoorstop/releases)
- [.NET 6 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 10 release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
- [EmberCloak](https://www.nexusmods.com/gunfirereborn/mods/8)
- [Restored metadata files](https://www.nexusmods.com/gunfirereborn/mods/4?tab=files)
