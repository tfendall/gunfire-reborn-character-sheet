# Character Sheet 0.13.50 (experimental)

Automatic scoreboard polling now waits 10 seconds after each response instead of 1 second. This substantially reduces requests when several players use the mod together, while keeping game-reported damage totals enabled.

## Changes

- Default automatic scoreboard interval: 10 seconds, including the initial eligibility delay.
- New [Stats] ScoreboardRequestIntervalSeconds setting accepts 1-120 seconds (default 10).
- New [Stats] AutomaticScoreboardSampling switch disables automatic requests without disabling receipt of game-supplied damage totals.
- One outstanding request at a time, no timeout retries, and no catch-up bursts after loading. Natural scoreboard responses also postpone the next request.
- All 0.13.49 attribution and build-bonus improvements remain included. Field Support remains unresolved.

## Updating

Existing users of the custom loader/.NET 10 baseline can update using **ModOnly** or **Setup**. The new settings default to slower polling without requiring a config edit. All three players should restart the game after updating. If you customize the settings, place them in [Stats]; the old [Diagnostics] AutomaticScoreboardSampling entry is ignored.

```ini
[Stats]
AutomaticScoreboardSampling = true
ScoreboardRequestIntervalSeconds = 10
```

Research diagnostics remain off by default, but existing local research settings are preserved. Disabling research alone does not disable scoreboard requests.

## Downloads

- **Setup ZIP**: guided mod installation/update/uninstall.
- **ModOnly ZIP**: manual mod update; choose this or Setup.
- **LoaderOverlay ZIP** and **LoaderSource ZIP**: unchanged custom loader baseline and corresponding source/licenses, for installations that need them.
- **SHA256SUMS.txt**: checksums for all four ZIPs.

See included INSTALL.md for prerequisites. No game binaries, runtime, EmberCloak or restored metadata are redistributed.

## Validation and limitations

Managed/replay, extractor and installer tests passed; the mod builds with zero warnings or errors. Three-client pacing tests verify over 85% fewer requests in the simulated interval, with timeout and loading protections intact. Packaged downloads are checksum-verified before publication. The loader is unchanged from 0.13.49.

This reduces scoreboard request traffic; a reduction in multiplayer ping has not yet been established. Game-reported totals refresh less frequently. Actual in-game validation of the new cadence is pending. The previously reported exit freeze is not fixed by this release. Attribution remains partial and Field Support remains unresolved.
