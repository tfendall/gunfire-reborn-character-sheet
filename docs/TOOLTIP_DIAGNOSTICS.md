# Tooltip calculation observations

Research build 0.13.16 adds opt-in observers controlled by
`Diagnostics.CaptureTooltipCalculations` (default false). They observe calls the
game makes; they do not invoke tooltip calculations or alter their results.

The following PCWP_State_logic methods are patched independently with postfixes:

- GetValue: state identity, count, and returned value.
- GetDescWithExtraInfo: description, bounded extra-data fields, and returned text.
- GetStateLevel: extra-data fields and returned level.
- OperateDescByFormula: description, bounded formula definitions, and returned text.
- OperateDesc: input description and returned text.
- SpecialOperateDesc: formula, relic type, bounds, and returned text.
- UpdateDescObj: count, client flag, and displayed name/description.
- OnEnterState and OnExitState: tooltip lifecycle context.

Observations are limited to the local player's UI context. Individual hook
installation successes/failures are logged. `tooltip-observation` records include
sequence, UTC time, method, player, state instance, SID, inputs, and result.
Helper methods use the UI's current effect identity; GetValue supplies explicit
identity. Treat helper identity as context until correlated with actual hovers.

The managed queue holds at most 256 records and drains at most 16 per frame.
Input/result strings are capped at 2,048/4,096 characters, with truncation marked.
Extra-data and formula enumeration are capped. Repeated calls remain visible to
measure update frequency. `tooltip-health` reports dropped records and errors.

## In-game verification

Enable CaptureTooltipCalculations alongside CaptureStatusDetails and
CaptureScoreboardDamage in the installed BepInEx config, then restart the game
with the research DLL. Hover known effects such as Armed Caster, Ninja's
Discipline, Demon Soul Surge, Rapidfire Upgrade, and CritX. Record their visible
values, switch weapons or change stacks, and hover again. Compare observations
with active-status-detail snapshots and the visible tooltip.

Managed tests validate ordering, repeated observations, string limits, queue
limits, and drain limits. Compilation does not establish that native hooks fire
or that every tooltip uses these methods; that requires in-game verification.
An observed tooltip result is an effect-specific value, not automatically a
general additive bonus. Scope, stacking, and duplicate contributions still need
validation before using these records to change displayed build totals.
