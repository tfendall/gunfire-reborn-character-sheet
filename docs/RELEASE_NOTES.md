# Experimental Character Sheet release

Windows Steam x64; tested with game build 25361614, BepInEx 6.0.0-be.697, EmberCloak distribution 1.2.2, and matching restored metadata.

Includes cumulative damage by source/element, observed Lucky Shot Chance, grouped build modifiers, and contributor details on the C screen.

0.13.13 adds damage credit for attackers whose server `PlayerProp.Owner` matches the local player. These hits appear under **Companion damage**, retain their element breakdown, and do not affect player weapon/skill Lucky Shot estimates. This addresses the player-ID-only filter that excluded separately attributed companion attacks. Nona/Iron Wing validation in-game is still required; this does not establish parity with the game's scoreboard for every damage source. Previously missed damage cannot be recovered.

Download the guided Setup ZIP for prerequisite detection and install/uninstall, or the ModOnly ZIP for manual installation. Prerequisites are separate official downloads; links and steps are in INSTALL.md. Neither package bundles game files or third-party prerequisite binaries.

Known limitations: intermittent native startup crashes remain under investigation; modifier coverage and damage attribution are partial; some item artwork may still use fallbacks. Observed Lucky Shot Chance uses a rolling damage-event window. Soul Pendant damage is not yet a separate headline source. Compatibility with other game/dependency versions is unverified.
