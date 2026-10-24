# Character Sheet 0.13.49 (experimental)

Build bonuses now include combat-window averages and contributor breakdowns, with clearer treatment of observed values, retained bonuses, duplicate items and unresolved effects. This release includes the development work since 0.13.38.

## Changes

- Show 30-second combat averages with observation coverage and known-subtotal indicators. Separate primary/secondary skill damage and base bonuses; normalize supported named hero abilities.
- Replace catalogue amounts with fresh source-level contributions without counting duplicate copies twice. Preserve explicitly identified retained bonuses after equipment changes.
- Clear learned inactive contributions only after complete status scans; keep incomplete, stale and ambiguous observations unresolved.
- Capture Movement Speed Fodder's live bonus and map Elemental Cube's verified states to its owned scroll while keeping elements separate.
- Recognize Resolute's unqualified damage-dealt bonus. Attribute the observed normal Convergent Paths skill branch, preserving its weapon penalty and skill bonus; overlapping branches and enhanced variants remain unresolved.
- Keep Field Support unresolved instead of interpreting its unverified input as a zero bonus.
- Cache build contributor data between sampled updates and bound named-ability text caching.
- Add offline attribution coverage reports and 23 captured replay samples, with source file/line provenance and explicit limitations on historical matching.

## Downloads

- **Setup ZIP**: guided mod install/update/uninstall and prerequisite checks.
- **ModOnly ZIP**: manual mod update; choose this or Setup.
- **LoaderOverlay ZIP**: unchanged custom loader/dependency baseline, for installations that still need it.
- **LoaderSource ZIP**: corresponding modified loader source, build recipe and license notices.
- **SHA256SUMS.txt**: checksums for all four ZIPs.

If the custom loader and .NET 10.0.12 runtime from 0.13.38 are already installed, update only the mod. Follow the included INSTALL.md for prerequisites. Game binaries, runtime, EmberCloak and restored metadata are not redistributed.

## Validation and limitations

Managed tests (including captured replay cases), extractor tests and installer tests pass. The mod compiles against the installed game with no warnings or errors. The loader baseline is unchanged from 0.13.38; its earlier startup/native-hook and gameplay validation does not establish fresh validation of this release.

The newest attribution fixes have not yet been validated in a new gameplay run. Modifier coverage remains partial. Replay tests validate interpretation of captured native answers, not native hook delivery or the game's arithmetic. Historical display matches do not establish exact timing, ownership or each item's share of final damage. Field Support, overlapping Convergent Paths branches and unverified scopes remain unresolved. Long-session stability and frame-time overhead are not fully characterized.

Baseline: Windows Steam x64 game build 25361614; modified BepInEx 6.0.0-gunfire-net10.1 based on upstream 788; HarmonyX 2.16.1; embedded .NET 10.0.12. Other game builds and plugin combinations are unverified.
