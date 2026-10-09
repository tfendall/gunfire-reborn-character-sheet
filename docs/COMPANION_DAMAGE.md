# Companion damage verification

The 0.13.13 capture path keeps the existing `FollowTextManager.WStatusHP` hook. It queues positive damage with the local player ID at receipt, then resolves non-player attackers through `NewObjectCache.GetPlayerProp(attacker).Owner` during guarded main-thread draining. An explicit owner match credits **Companion damage**. Unknown owners and other players' attackers are excluded. `ClientOwner` and team/side membership are not used to grant credit.

Local inspection of the supported game build found that `s2cnetwar.GS2CWStatusHP` forwards the packet unchanged to `FollowTextManager.WStatusHP` (RVA 0x6CF8F8), and that `s2cnetaddobject.MakeDeviceAddPacket` reads `PlayerProp.Owner` (RVA 0x6BB8DA). This supports the ownership-based fix, but does not replace an observed Iron Wing event from a live run.

Ownership reads wait for the existing loading/menu guard. Primitive results are reused only within one drain batch; no game objects or owner mappings are retained between batches. Unavailable ownership fails closed. Very short-lived attackers removed before draining may therefore remain unattributed. The queue and drain retain their existing capacity/time limits; capture-health logs report drops.

## Live check

1. Restart with 0.13.13 and record the game and mod totals.
2. Let Iron Wing attack without firing. Check that **Companion damage** and the mod total increase; compare the changes against the game's total.
3. Shoot separately and confirm player weapon damage still increases normally.
4. If available, check melee and turret modes, a room transition, and a teammate's companion (which must not count for the local player).

New `damage-event` records include `credit=Companion`, `owner`, and `localPlayer`. `capture-health` includes companion/rejected event counts and ownership errors. A throttled `damage-uncredited` record samples rejected attackers for diagnosis. Preserve the session's `BepInEx/StatsDiagnostic/trace-*.jsonl` files if the counters still disagree.

Managed tests exercise the actual enqueue/drain code against a stubbed native property cache, including attribution, source totals, Lucky Shot isolation, guarded reads, resets, player changes, owner changes, missing properties, and lookup failures. They cannot verify native hook delivery or Iron Wing's runtime owner value.
