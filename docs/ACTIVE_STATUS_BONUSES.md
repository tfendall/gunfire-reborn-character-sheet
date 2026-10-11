# Active status bonus investigation

Research branch: `codex/active-status-bonuses`. The 0.13.13 release contains only the companion fix. This work prepares a 0.13.14 diagnostic build; it does not add unverified values to build totals.

## Findings from the supported game build

- The existing capture reads server state IDs, counts and durations; only a handful of catalog rules interpret these as bonuses. Client state mirrors are captured separately and are not summed with server states.
- In the October 9 Nona session (`trace-20261009-210444-288` and its continuation), 112 changed server-state snapshots were logged, of which two were budget-limited. The client-state snapshot was empty. Trace deduplication means these are changed records, not every polling attempt.
- Persistent server states with `Count=0` occur in that run. Presence, stack count, and displayed count must remain distinct. Zero stacks cannot by itself establish that an effect is absent.
- The existing numeric-property trace exposes `HPMax=135`, but does not provide a resolved general skill-damage bonus in the inspected snapshots. This supports a resource-based calculation for Enduring Caster after verifying its exact formula and units.
- `StateExtraInfo` has a per-instance numeric dictionary, a source/type ID, and a perform type. `DataHelper.statedataclass` has `AttName`, `ValueFormula`, behavior IDs, visibility flags, and level/tier descriptions with formula ranges.
- The HUD's `UIScript.PCWP_State_logic` exposes `GetDescWithExtraInfo`, `GetStateLevel`, `OperateDescByFormula`, and `GetValue`. Its field layout includes a hero state cache and separate current state SID/instance ID. This is evidence of a richer tooltip calculation path, not proof that every extra number is an additive combat bonus.

## Diagnostic reader

Enable `CaptureStatusDetails = true` in the `[Diagnostics]` section of `BepInEx/config/local.gunfire.statsdiagnostic.cfg` with the research build installed. It defaults off. Restart after changing the setting. The installed 0.13.13 release does not include this reader.

The reader uses the existing guarded, budgeted inventory loop and its formerly disabled live-skills slot. It examines one active server-state instance per full cycle (nominally around 1.6 seconds while reads are allowed), rotating by instance ID. It reads only the existing state cache, already-initialized state table, and state extra-info lookup. It does not initialize UI forms, evaluate game formulas, or retain native object references.

`active-status-detail` trace records contain a JSON object in `detail`, with:

- Instance ID, definition SID, source attacker, actual/display/max counts, time remaining, persistence and display flags.
- Definition name, attribute, value formula, behavior IDs and up to four description/formula entries.
- Extra-info source fields and up to 32 named integer values.
- Separate availability/completeness flags. Missing data and truncation must not be interpreted as zero.

The reader is deliberately diagnostic, not a full simultaneous snapshot. Fast effects can expire between visits. More than 128 active instances or exhausted time budgets can leave it incomplete. These records must not directly drive current totals.

## Next verification

Compare an expanded status tooltip with the corresponding trace record while an effect gains stacks, expires, or changes with HP/ammo. Start with existing mapped effects as controls. Determine whether `extraValues` contain final values or formula inputs, what their units are, and whether formulas are only display formulas. Map source/type IDs to the equipped item's actual level.

Only then add reviewed rules or a restricted formula interpreter. Preserve additive versus independent multipliers, player versus companion scope, and target-specific applicability. Deduplicate contributions already included in measured properties and handle multiple state instances explicitly. Mark unresolved headline totals as partial instead of displaying a misleading complete zero.

For short-lived bonuses, investigate primitive snapshots from state add/refresh/remove events after this polling path has been validated in-game. Do not trade native safety for polling frequency.
