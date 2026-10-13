# Install Character Sheet

Download [Character Sheet 0.13.38](https://github.com/tfendall/gunfire-reborn-character-sheet/releases/tag/v0.13.38). The repository and downloads are public. Expand **Assets** and choose:

| Asset | Purpose |
|---|---|
| `CharacterSheet-0.13.38-Setup.zip` | Guided mod installation, prerequisite checks, and uninstall. |
| `CharacterSheet-0.13.38-ModOnly.zip` | Manual mod installation; choose this or Setup. |
| `CharacterSheet-0.13.38-LoaderOverlay.zip` | Prebuilt custom loader and updated dependencies; required if you do not already have the verified custom loader. |
| `CharacterSheet-0.13.38-LoaderSource.zip` | Corresponding loader source, build recipe, provenance, and license notices; useful for rebuilding or inspecting it. |
| `SHA256SUMS.txt` | Checksums for all four ZIPs. |

GitHub's automatically generated **Source code** archives are source checkouts, not ready-to-install mod packages.

If your custom loader and .NET 10 runtime already match the verified baseline, update only the mod using Setup or ModOnly. When upgrading from stock BepInEx or .NET 6, close the game and back up `BepInEx/core`, `dotnet`, and your mod DLL before installing the new prerequisites.

Experimental Windows Steam x64 release. The tested game build is 25361614; compatibility with other builds is not established. Intermittent native startup crashes remain under investigation.

## Guided install

1. Download `CharacterSheet-VERSION-Setup.zip` from the [Releases page](https://github.com/tfendall/gunfire-reborn-character-sheet/releases) and extract it anywhere outside the game folder.
2. Close Gunfire Reborn. Run `Install.cmd`. The guided installer uses Windows PowerShell and Windows Forms already included with Windows; it does not require a development SDK.
3. Select the game folder. Steam libraries are detected automatically, or use **Browse**. This folder must contain `Gunfire Reborn.exe` and `GameAssembly.dll`.
4. Click **Check installation**. Select a missing prerequisite and click **Open selected prerequisite page**. Install it using the instructions below, then check again.
5. Verified prerequisites are reused without changes. A different version is marked unverified, rather than assumed compatible. Missing prerequisites always block installation; the explicit unverified-version option is for users testing other installed versions.
6. Optionally select your original vanilla metadata backup. Keep a backup when manually installing restored metadata. The installer stores its own copy outside the game directory for uninstall.
7. Click **Install / update mod**, then launch the game. Hold C to view the panel. While holding C, right-click to use the game's cursor and interact with the panel.

The first BepInEx launch may take longer while interop assemblies are generated. The installer does not start the game or alter Windows crash-dump settings.

## Prerequisites

Install all prerequisites before launching with the mod:

| Component | Verified version | Official download and destination |
|---|---|---|
| BepInEx | Custom `6.0.0-gunfire-net10.1`, based on build 788 / 5b766a3 | Install the complete official [Unity.IL2CPP Windows x64 build 788](https://builds.bepinex.dev/projects/bepinex_be), then extract **LoaderOverlay.zip** from [release 0.13.38](https://github.com/tfendall/gunfire-reborn-character-sheet/releases/tag/v0.13.38) into the game root and overwrite matching files. It supplies the modified loader, HarmonyX 2.16.1, and compatible dependencies. Stock build 788 alone is incompatible. |
| .NET game runtime | 10.0.12 | Download the official [.NET Runtime 10.0.12 Windows x64 binaries ZIP](https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.12/dotnet-runtime-10.0.12-win-x64.zip), extract outside the game folder, and copy the **contents** of `shared/Microsoft.NETCore.App/10.0.12` over the game's `dotnet` folder. Overwrite matching files; preserve the additional BepInEx logging and dependency-injection DLLs. Installing a system runtime alone does not update this embedded runtime. |
| Restored metadata | Steam build 25361614 | [Global Metadata Restored](https://www.nexusmods.com/gunfirereborn/mods/4). Back up the original `Gunfire Reborn_Data/il2cpp_data/Metadata/global-metadata.dat` outside the game folder, then replace it with the restored file for your exact game build. |
| EmberCloak | Distribution 1.2.2 | [EmberCloak](https://www.nexusmods.com/gunfirereborn/mods/8). Put both `EmberCloak.dll` and `EmberCloak.Native.dll` in `BepInEx/plugins/EmberCloak`. |

EmberCloak's assembly/plugin version can differ from its download version. The installer uses known file hashes to distinguish the tested package. It also checks the game assembly and restored metadata, not just filenames.

Before the first launch with build 788, download the [official Unity 2018.4.36 library ZIP](https://unity.bepinex.dev/libraries/2018.4.36.zip) and place the unopened ZIP at `BepInEx/unity-libs/2018.4.36.zip`. The loader uses this archive to regenerate interop assemblies. Without the cached archive, its HTTP download crashed in `WinHttpOpen` on the verified installation. Launch through Steam after installing every prerequisite.

No SDK or compiler is needed for installation. The modified loader is a separate download from our release, with its corresponding source supplied separately. Startup and native patch/unpatch checks passed, and gameplay on the upgraded dependencies was confirmed by the user on October 13. This is still an experimental release; long-session stability has not been established. See [dependency compatibility notes](DEPENDENCIES.md).

Nexus downloads require the author's download flow and possibly a Nexus account. EmberCloak and restored metadata are not included in our release assets. The base BepInEx package, runtime, and Unity library archive come from upstream. See [BepInEx's IL2CPP instructions](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html).

## Manual mod-only install

1. Install the prerequisites above and close the game.
2. Extract `CharacterSheet-VERSION-ModOnly.zip` into your game root. The resulting path is `BepInEx/plugins/GunfireStatsDiagnostic/GunfireStatsDiagnostic.dll`.
3. For updates, replace that DLL while the game is closed.

To remove the manually installed mod, delete that DLL. Its settings, traces, and prerequisites can remain. The guided uninstaller requires an installation record; it does not claim ownership of an unrecorded mod-only installation.

## Uninstall

Reopen the extracted guided installer and select the same game folder:

- **Uninstall mod only** removes our recorded DLL and leaves prerequisites unchanged.
- **Uninstall mod + prerequisites** also removes files matching the tested BepInEx and EmberCloak manifest. It refuses to proceed if another plugin is installed or a prerequisite file has changed. It restores an optional original metadata backup only when the game assembly still matches the backup's recorded build.

Without a suitable original metadata backup, click **Verify game files through Steam** before playing vanilla. Steam restores the game's metadata; removing the loader alone does not establish that the metadata is vanilla. Game updates also require verification instead of restoring an old backup.

Inactive generated caches, logs, settings, and recovery backups are preserved. They are not active mods after the loader is removed. Saves and translation packs are never included in removal. Unexpected files are not deleted. To remove a different loader version, follow its own manual uninstall instructions.

The installer stores records/backups under `%LOCALAPPDATA%/GunfireCharacterSheet/installs`, keyed by the selected game folder. Keep the extracted installer for uninstall and repair. It does not register a Windows Apps entry or install a background service.


## Optional research diagnostics

Normal play needs no diagnostic settings. Authoritative damage totals, seasonal orb attribution,
and effective bonuses remain active with research disabled.

To investigate a problem, close the game and edit
`BepInEx/config/local.gunfire.statsdiagnostic.cfg`. Set `EnableResearch = true`
under `[Diagnostics]`, then select the observers you need. See `DEBUGGING.txt`
in the download for the switches and storage limits. Set `EnableResearch = false`
and restart to return to normal operation. Existing observer settings from earlier
builds cannot enable research while this master switch is false.
