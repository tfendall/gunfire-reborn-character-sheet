# Install Character Sheet

Experimental Windows Steam x64 release. The tested game build is 25361614; compatibility with other builds is not established. Intermittent native startup crashes remain under investigation.

## Guided install

1. Download and extract `CharacterSheet-VERSION-Setup.zip` anywhere outside the game folder.
2. Close Gunfire Reborn. Run `Install.cmd`. The guided installer uses Windows PowerShell and Windows Forms already included with Windows; it does not require a development SDK.
3. Select the game folder. Steam libraries are detected automatically, or use **Browse**. This folder must contain `Gunfire Reborn.exe` and `GameAssembly.dll`.
4. Click **Check installation**. Select a missing prerequisite and click **Open selected prerequisite page**. Install it using the instructions below, then check again.
5. Verified prerequisites are reused without changes. A different version is marked unverified, rather than assumed compatible. Missing prerequisites always block installation; the explicit unverified-version option is for users testing other installed versions.
6. Optionally select your original vanilla metadata backup. Keep a backup when manually installing restored metadata. The installer stores its own copy outside the game directory for uninstall.
7. Click **Install / update mod**, then launch the game. Hold C to view the panel. While holding C, right-click to use the game's cursor and interact with the panel.

The first BepInEx launch may take longer while interop assemblies are generated. The installer does not start the game or alter Windows crash-dump settings.

## Prerequisites

Install all prerequisites before launching with the mod:

| Component | Tested version | Official download and destination |
|---|---|---|
| BepInEx | Unity.IL2CPP-win-x64, 6.0.0-be.697, commit 5362580 | [BepInEx builds](https://builds.bepinex.dev/projects/bepinex_be). Extract the complete archive into the game root, including `dotnet`, `winhttp.dll`, `doorstop_config.ini`, and `BepInEx`. |
| Restored metadata | Steam build 25361614 | [Global Metadata Restored](https://www.nexusmods.com/gunfirereborn/mods/4). Back up the original `Gunfire Reborn_Data/il2cpp_data/Metadata/global-metadata.dat` outside the game folder, then replace it with the restored file for your exact game build. |
| EmberCloak | Distribution 1.2.2 | [EmberCloak](https://www.nexusmods.com/gunfirereborn/mods/8). Put both `EmberCloak.dll` and `EmberCloak.Native.dll` in `BepInEx/plugins/EmberCloak`. |

EmberCloak's assembly/plugin version can differ from its download version. The installer uses known file hashes to distinguish the tested package. It also checks the game assembly and restored metadata, not just filenames.

Nexus downloads require the author's download flow and possibly a Nexus account. Prerequisites are not included in our release assets. See [BepInEx's IL2CPP instructions](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html).

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
