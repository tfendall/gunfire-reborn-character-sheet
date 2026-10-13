# Local BepInEx .NET 10 variant

`bepinex-net10.patch` modifies upstream BepInEx commit `5b766a3b7f6c164d4798924a93f3acf4db769d06` (official build 788) for this project's experimental Windows IL2CPP dependency baseline. The source is available from [BepInEx/BepInEx](https://github.com/BepInEx/BepInEx/tree/5b766a3b7f6c164d4798924a93f3acf4db769d06); the modification follows the upstream LGPL-2.1 license in `BepInEx-LICENSE.txt`.

Build with `scripts/Build-Loader.ps1`. It pins the source revision, applies this patch, upgrades HarmonyX/MonoMod, targets .NET 10, and produces source/patch/hash provenance. See `docs/DEPENDENCIES.md` for changes, installation, validation, and compatibility limits. This local variant is not an official BepInEx release.

No upstream or game DLL is committed here. Setup and ModOnly packages do not include this loader. Release 0.13.38 provides a separate LoaderOverlay ZIP and corresponding LoaderSource ZIP, including the modified source tree, pinned revision, patch, license notices, and build recipe. Installation needs no compiler; source rebuilds require Git and the .NET 10 SDK. Game/Unity reference binaries are excluded from the source ZIP and fetched from pinned upstream only when rebuilding.
