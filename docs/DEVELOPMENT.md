# Build and validate

Install the .NET 8 SDK. The mod targets .NET 6 because the tested BepInEx runtime is .NET 6.0.7; the test harness runs on .NET 8.

Prepare an owned Windows Steam game installation using the prerequisites in INSTALL.md. Launch once with BepInEx to generate `BepInEx/interop/Assembly-CSharp.dll` and the other reference assemblies.

```powershell
./scripts/Test.ps1
./scripts/Build.ps1 -GameDir 'D:\SteamLibrary\steamapps\common\Gunfire Reborn'
./scripts/Package.ps1
```

All scripts accept a custom SDK executable through `-Dotnet` where applicable. `Build.ps1` passes the game folder to MSBuild; no developer-specific path is in the project. Direct builds can use `-p:GameDir=...` or `GUNFIRE_GAME_DIR`.

`artifacts/mod` contains the build. `dist/VERSION` contains guided and mod-only ZIPs plus SHA-256 checksums. Package only the plugin DLL, installer, manifest, and documentation. Never package game DLLs, interop assemblies, restored metadata, EmberCloak, log files, dumps, or local credentials.

Source catalogs are checked in so a build needs no extraction tools or translation-session database. `Plugin.ModVersion` is the release version. Before a release, run managed/installer tests, compile against the supported game, and validate the UI in-game. A clean build cannot verify native stability or asset availability.

## GitHub workflows

Workflow templates are in `docs/workflows/*.yml.example`. They are not active yet: the current GitHub OAuth login lacks the `workflow` permission. After granting that permission, copy them into `.github/workflows/` and drop the `.example` suffix. The hosted Windows test workflow needs no game installation. The manual release workflow needs a trusted Windows self-hosted runner labeled `gunfire-build`, .NET 8, GitHub CLI, and `GUNFIRE_GAME_DIR` pointing to a prepared game. It creates an experimental **draft** release; review its artifacts and notes before publishing. Never run untrusted pull-request code on that runner.

For a local release, build and package, then upload the three files in `dist/VERSION` to an experimental draft release. Private-repository releases require repository access.

## Compatibility updates

`installer/compatibility.json` records tested game and dependency hashes. It contains no binary payloads. Review changes against official packages and an in-game smoke test; do not update it just to silence an unverified-version message. Metadata/game updates require a new compatibility entry and validation.

The installer is intentionally inspectable PowerShell 5.1 code, with a Windows Forms UI and no automatic prerequisite downloads. Backend tests use temporary fake installations and never modify a real game.
