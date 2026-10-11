# Scoreboard damage diagnostics (research 0.13.15)

Enable `CaptureScoreboardDamage = true` under `[Diagnostics]` and restart with the research build. The observer installs after normal player/weapon readiness. Status-effect diagnostics can remain enabled independently. Neither observer changes damage totals or game behavior.

The new prefix observes `TeamWarData.AddTeamerDMG`, the scoreboard update path downstream of `s2cnetwar.GS2CTeamDamage`. Local inspection shows AddTeamerDMG assigns the supplied damage/seasonal values to its dictionaries; it does not add them to previous values. Observe this one point rather than treating both methods as independent damage events.

`scoreboard-diagnostic` records contain receipt UTC, sequence, player, reset epoch, reported total and seasonal damage, copied raw category/detail/value triples, and capture totals/queue depths at receipt and at logging time. Category meanings remain unverified. Do not sum categories or add seasonal damage to the reported total without verifying their relationship.

The hook copies only callback-supplied numeric values. Category copying has a 1 ms cooperative budget, at most 64 groups and 128 cells. Availability and completeness are separate. Native exceptions preserve the headline values and mark the breakdown incomplete. No native dictionaries survive the callback. A managed queue holds at most 128 reports and drains at most 16 per update, reporting overflow. Reports from a previous local player or reset epoch are discarded.

The two feeds need not be synchronized: a receipt gap can include queued hits; a drain gap can include hits newer than the server snapshot. Compare repeated, settled reports rather than assuming every instantaneous difference is loss. A manual reset clears our counter but not the server's cumulative total, so avoid resetting during comparison. A first report can include damage before the observer was installed.

## Test

Run a short encounter, then stop attacking and allow updates to settle. Include companion-only attacks and player-only attacks where practical. Preserve the session trace and one scoreboard/widget screenshot. Check for `scoreboard-observer-on`, then `scoreboard-diagnostic` records. A build/test pass does not verify native hook delivery; no reports after a scoreboard update needs investigation rather than treating a missing report as zero.

## Other useful hook candidates

- `StateManager.AddState`, `DelState`, `RefreshState`, `RefreshStateCnt`: state lifecycle, timed expiry refreshes and stack changes, including effects missed by polling. Removal only supplies an instance ID, so ownership/SID needs a previously captured mapping.
- `StateManager.RefreshExtraInfo`: player, state instance, perform/source type and named numeric values. Especially relevant to Firepower/Rapidfire/CritX display formulas. Copy inputs with bounds; never retain its native dictionary.
- `s2cnetaddobject.MakeDeviceAddPacket`, `MakeSummonAddPacket`, and `GS2CPropChange`: companion creation/ownership changes. Useful if damage arrives after an attacker disappears from the live property cache. Must handle deletion, room changes and reused IDs before using cached ownership for credit.
- `s2cnetwar.GS2CImmunity`: explains attacks that produce no HP damage; not an additional damage source.
- `s2cnetwar.GS2CShowDamageInfo`: exposes an integer-list report, but its meaning/coverage is not verified. Inspect its consumer before installing a hook or interpreting values.

Only the scoreboard observer is newly installed by this build. Prefer targeted hooks with primitive queues over enabling every combat hook, especially given prior native stability problems.


## Damage audit (research 0.13.24)

The same `CaptureScoreboardDamage` setting now enables three complementary observations:

- Every processed rejection is assigned a reason and included in exact count/raw-damage sums, grouped by attacker, resolved owner, action, type and extra flags. `damage-uncredited` details include victim, packet correlation ID and a unique rejection number. Native owner lookups still happen at drain time, not in packet callbacks. Null, zero, other-owner and lookup-error outcomes remain distinct. These include enemy/teammate hits and are not automatically missing local damage.
- `s2cnetwar.GS2CWStatusHP` prefix/finalizer brackets packet delivery. A thread-local nested context matches the complete payload with `FollowTextManager.WStatusHP`. Counts distinguish arrival, matching callback, duplicate callback, payload mismatch, unscoped callback and packet without a callback. Check `damage-packet-observer-on` to verify installation; native delivery must still be verified in a live run. Only the existing callback feeds the ledger.
- Each local scoreboard report cuts an atomic managed audit interval. `damage-reconciliation` records game/captured/gap increases, pending depths, outcome totals, overflow and trace health. `damage-reconciliation-buckets` contains numbered chunks of at most 32 detailed buckets, avoiding the trace record length limit. The summary declares the expected chunk count. Missing chunks or trace queue drops mean incomplete evidence.

Groups are bounded at 512 per interval. Additional identities enter explicit per-outcome overflow buckets, preserving counts and exact decimal raw sums while losing identity detail. Packet stages and processing outcomes describe overlapping observations: **do not sum all outcomes as damage**. Execution exclusions are separate, and nonpositive callbacks, queue drops and stale queued hits are visible. No audit counter changes displayed damage or credits execution markers.

The first report/player change/reset/server-total decrease establishes a baseline rather than inventing a delta. An audit serial gap marks an incomplete interval after report queue loss. Arrival and processing can straddle scoreboard boundaries; use pending depths and settled successive reports. A scoreboard snapshot remains a server cumulative report, not a per-hit credit receipt. State reset discards earlier audit buckets; it does not reset the server counter.

Validation: managed tests cover nested/identical packets, changed payloads, unmatched callbacks, duplicate callbacks, reset, concurrency, overflow conservation and the real capture path's accepted/rejected/execution accounting. Native hook delivery and performance require the next in-game run.


## Recovery filter and deaths (research 0.13.25)

Positive WStatusHP amounts with either CURESource bit (perform 0x10000000 or scene 0x20000000) are recovery, not damage. They remain in packet matching and `recovery-excluded` audit buckets, but never enter the damage queue, ledger, DPS or Lucky Shot window. The filter is always active, including when diagnostics are disabled. Damage-use shield/armor flags alone do not exclude ordinary damage.

With CaptureScoreboardDamage enabled, `death-observer-on` confirms installation of a prefix on s2cnetwar.GS2CDie. `death-observation` retains the raw attacker, victim and iFinalDam plus the latest eight positive non-recovery callbacks for that victim within the preceding ten seconds. Candidate records include full raw flags, attacker, monotonic receipt time and packet ID; they are not necessarily credited local hits. CandidateCount reports truncation. The 1,024-hit history and 128-death queue report evictions/drops; each update logs at most 16 deaths. Player/reset changes clear context. Temporal matching alone cannot rule out reused victim IDs or establish causality; no-match also does not prove missing damage. Later-arriving hit callbacks remain in the normal trace for follow-up correlation. This observer never adds iFinalDam to the ledger.

Native GS2CDie uses iFinalDam for damage taken when the local player is the victim. Its relationship to outgoing scoreboard credit is unverified. Do not assume it is additional damage or sum it with ordinary hits. Live delivery requires verification after restart.


## Broad combat evidence bundle (research 0.13.26)

`CaptureCombatEvidence` defaults to the existing `CaptureScoreboardDamage` setting on first creation. It is independent thereafter and requires restart. It installs before run readiness, copying only callback inputs, so initial entity births are observable. It records nonlocal as well as local entities, including while the ordinary stats capture is not yet ready. It does not send requests, alter game state, or add any new source to the damage ledger.

Coverage is organized by information needed, rather than one hypothesized scroll:

- Identity/lifecycle: hero, monster, summon, device, build/destructible, servant, pet and NPC creation; monster/body-part mappings; property cache removal, player removal and world clears.
- State: inbound state add/delete, duration/stack refresh and extra data, including target states and companion states. Existing local effective-bonus diagnostics remain available alongside these.
- Health/ownership: incoming general/pet property changes and the applied NewObjectCache.UpdatePlayerProp path. Prioritized fields include SID, Name, Owner, ClientOwner, Side, HP/HPMax, Shield/ShieldMax and Armor/ArmorMax. All remaining dictionary entries are copied within bounds. Packet-level owner fields retain their own names and never grant credit.
- Combat: raw WStatusHP, normal/real death, revive, immunity, struck target, shield/armor break, detailed damage, GM single damage, jump-figure notifications, shared HP bars, kill-line updates, skill-start notifications, monster additional info, war status and level-loaded notifications.
- Accounting: incoming team damage, plus the existing local scoreboard snapshots, packet matching, rejection/recovery/execution buckets and death diagnostics.

`combat-hook-on/missing/failed` records form the installation manifest. `combat-hook-health` includes even zero-call hooks, copy errors, truncations, pending records, queue drops and trace-writer health. Hook failure is isolated; the rest still install. No delivery is assumed merely because a hook installed. `GS2CShowDamageInfo` routes to GMData_Event/SingleDamage; availability and numeric meaning remain unverified. HP changes can reflect healing, regeneration, multiple attackers, or server corrections: they are evidence, not attributable damage.

Every `combat-evidence` event has a session sequence, monotonic time shared with damage capture, cached Unity scene handle, local player/reset epoch, hook and primitive field paths. Payloads have numbered chunks of eight fields; missing chunks indicate incomplete evidence. Entity snapshots attach birth kind, identity properties and generation; property updates produce numeric before/after deltas. Repeated updates from packet and cache layers are deliberately separate observations and must not be summed. IDs are invalidated on removal/world clear, get a new generation at each birth, and are not joined across differing scene handles. Scene handles are cached by the frame poll, so load-boundary packets may have a preceding handle: raw lifecycle records remain available for offline inspection. Unknown entities remain unknown, never inferred to be enemies. Death and later-arriving hit events can be correlated offline by victim/time; the narrow death snapshot alone is not exhaustive.

Bounds: 80 fields, 160 characters per value, 120 per field path, recursion depth five, list cap 24, cooperative two-millisecond copy budget per callback; unsupported values and limits mark a partial copy. Only packet DTO members and known dictionaries/lists are traversed; no arbitrary entity getters are invoked. A managed queue holds 4,096 events, drains at most 64 per frame, and tracks drops. The derived entity cache holds 2,048 entries with eviction counts; loss invalidates cached continuity. Oversized enrichment is omitted with an explicit flag rather than allowing TraceStore to truncate JSON. Records may span multiple lines, all outside the native callback. Increased diagnostic overhead and native hook delivery require a live run to measure.

Offline review: `python scripts/analyze_combat_evidence.py <trace-session.jsonl>` loads that session's rotated files, verifies chunks, reports hook coverage/losses, the latest scoreboard gap, and deaths without hit packets in the preceding ten seconds or following two seconds. It does not assume such deaths are missing enemy damage. Compare origin/type, health updates, ownership, all damage feeds and state changes in the same interval. This bundle is intended to let a single run test these competing explanations; data that the server never transmits cannot be recovered through client observers.


## Crash follow-up, 0.13.29

0.13.26 incorrectly patched GS2CWarStatus, a shared native stub (6,144 aliases at RVA 0x6b02d0). It is permanently excluded. The remaining original targets were audited as unique addresses for this game build. Flood gating precedes copying; suppression is reported per hook.

The 0.13.28 crash breadcrumb while opening C identified an active GS2CTeamDamage callback. The exact native failing instruction remains unavailable. 0.13.29 removes that redundant packet hook, forwarding scalar scoreboard values for all players from the existing typed TeamWarData.AddTeamerDMG observer instead. Local category snapshots still use the existing bounded scoreboard copier. Generic reflected list getters are removed: only explicitly typed integer lists/nested integer lists are traversed; other native lists produce a partial-copy marker pending a typed adapter. The rest of the broad identity/health/state/combat evidence remains enabled. Typed hook signatures are validated before patching. File-backed callback journals record active hooks and read phases, including a completed-copy marker, independently of the asynchronous trace writer. No C-screen stability claim is made until live verification.
