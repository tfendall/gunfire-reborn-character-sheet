# Build and validate

Install the .NET 10 SDK (10.0.401; selected by `global.json`). The mod, locally modified BepInEx loader, and test harness target .NET 10. The game's embedded runtime is .NET 10.0.12. Updating the developer SDK alone does not update that embedded runtime.

Prepare an owned Windows Steam game installation using the prerequisites in INSTALL.md. Launch once with BepInEx to generate `BepInEx/interop/Assembly-CSharp.dll` and the other reference assemblies.

Build the loader first with `./scripts/Build-Loader.ps1`; its pinned upstream source and patch are described in [DEPENDENCIES.md](DEPENDENCIES.md). A stock BepInEx 788 package cannot run this dependency baseline without those changes.

```powershell
./scripts/Test.ps1
./scripts/Build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Gunfire Reborn'
./scripts/Package.ps1
./scripts/Package-Loader.ps1
```

Build and test scripts prefer a user installation at `%USERPROFILE%/.dotnet/dotnet.exe` when present, otherwise `dotnet` on PATH. They accept a custom SDK executable through `-Dotnet`. `Build.ps1` passes the game folder to MSBuild; no developer-specific path is in the project. Direct builds can use `-p:GameDir=...` or `GUNFIRE_GAME_DIR`.

`artifacts/mod` contains the build. `dist/VERSION` contains Setup, ModOnly, LoaderOverlay, LoaderSource, and SHA-256 checksums. Setup and ModOnly contain the plugin DLL, installer where applicable, manifest, and documentation. LoaderOverlay contains changed redistributable loader/dependency files and notices; LoaderSource contains the modified source and build recipe. Never package game DLLs, generated interop assemblies, restored metadata, EmberCloak, log files, dumps, or local credentials.

After building the custom loader, run its standalone compatibility checks with `dotnet run --project tests/LoaderCompatibility/LoaderCompatibility.csproj -c Release`. On this host the user-installed executable is `%USERPROFILE%/.dotnet/dotnet.exe`.

For a bounded in-game loader check, run `./scripts/Test-LoaderStartup.ps1 -GameDir 'YOUR_GAME_FOLDER' -NativeProbe`. It refuses to run while the game is already open, installs a temporary native patch probe, launches through Steam, checks startup for 90 seconds, closes only its own game/crash-handler instances, and removes the probe from the game. Evidence stays under `artifacts/loader-validation`. This verifies native getter patching and restoration, not a complete gameplay session.

Source catalogs are checked in so a build needs no extraction tools or translation-session database. `Plugin.ModVersion` is the release version. Before a release, run managed/installer tests, compile against the supported game, and validate the UI in-game. A clean build cannot verify native stability or asset availability.

## GitHub workflows

Workflow templates are in `docs/workflows/*.yml.example`. They are not active yet: the current GitHub OAuth login lacks the `workflow` permission. After granting that permission, copy them into `.github/workflows/` and drop the `.example` suffix. The hosted Windows test workflow needs no game installation. The manual release workflow needs a trusted Windows self-hosted runner labeled `gunfire-build`, .NET 10 SDK, GitHub CLI, and `GUNFIRE_GAME_DIR` pointing to a prepared game. It creates an experimental **draft** release; review its artifacts and notes before publishing. Never run untrusted pull-request code on that runner.

For a local release, build and package the mod and loader, then upload the four ZIPs and `SHA256SUMS.txt` from `dist/VERSION` to an experimental draft release. Verify the assets before publishing. This repository's releases are public.

## Compatibility updates

`installer/compatibility.json` records tested game and dependency hashes. It contains no binary payloads. Review changes against official packages and an in-game smoke test; do not update it just to silence an unverified-version message. Metadata/game updates require a new compatibility entry and validation.

The installer is intentionally inspectable PowerShell 5.1 code, with a Windows Forms UI and no automatic prerequisite downloads. Backend tests use temporary fake installations and never modify a real game.
