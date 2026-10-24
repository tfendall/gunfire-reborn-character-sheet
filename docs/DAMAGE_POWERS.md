# What powers this?

## Current layout — development 0.13.48

Build contributor tiles and sheet data are cached across GUI events until sampled
rows/history, ownership findings, or weapon identity changes. Named-ability text
normalization has a bounded 1024-entry cache to avoid repeating regex work for
every metric and contributor in large builds. Live Act 3 performance still needs
verification; the change removes repeated managed work without new native hooks.

A fresh resolved status with explicit gem/scroll/blessing source ID can contribute
after inventory ownership disappears. It must have one catalogue source name;
independent modifiers and competing live readings remain excluded. It is labelled
retained status with no invented item level, and stale reads still expire. This
does not retroactively change prior combat samples: current status values can
differ from the last 30 combat seconds, particularly after shopping or selling.

### Previous development 0.13.47

Primary Skill Damage and Secondary Skill Damage each have one section heading,
with separate Additive bonus and Base bonus rows beneath it. Each row retains
its own average, coverage and expandable contributor breakdown. This is a
presentation change; the underlying metrics and calculations remain separate.

### Previous development 0.13.46

NamedAbilityBonuses supplies a shared ability-role table for hero ascensions.
Soul Strike, Energy Orb, Leap, Flying Sword and Spiritual Flame map to primary;
Cleave, Essence Chain, Striking Punch, Fatal Bloom, Blazing Meteor, Smoke Grenade,
Explosive Grenade and Chain Lightning map to secondary. Unknown abilities remain
unmapped; companion/subcomponent scopes and seasonal skills are not inferred.
Seasonal Energy Orb shares a label with a hero ability and is deliberately excluded.

Existing reviewed additive/base damage rules are projected into separate role
metrics while preserving their layer, scope, and conditions. Whole unconditional
named-damage sentences can establish a static rule through shared grammar; this
resolves all three Turn into Reality levels without an ascension-ID exception.
Live descriptions and contributor/coverage matching use the same role table.
Primary/Secondary Skill Base Damage have separate headline rows and histories;
their values are never summed into additive Primary/Secondary Skill Damage.
Mixed, conditional, independent and delivery clauses are not promoted into a
plain additive bonus. Tests exercise static capture through the 30-second window,
live base/additive values, source collisions, and multiple heroes.

### Previous development 0.13.45

A single fresh, resolved reading with an explicit source ID now replaces the
matching catalogue metric across all owned copies and levels. It contributes
once to the headline and appears as one combined contributor, with individual
shares explicitly unknown. The old Skill Fodder ID exception is removed.
Name-only duplicate matches and competing readings for the same source/metric
remain ambiguous; weapon properties and independent multipliers retain their
existing exclusions. Combined history uses source level zero as a group identity.
Lucky Shot duplicate rows are likewise replaced once. Learned inactive zeros
retain the whole ownership count/level signature and expire if it changes.

Tests replay the mixed-level Weapon Fodder case: 25 + 40 + 40 catalogue points
are replaced by one observed 120, not 120 per copy or 120 plus the catalogue.
Additional tests cover another source type, Lucky Shot, conflict/staleness,
ownership changes, and combined contributor presentation.

### Previous development 0.13.44

The shared formula renderer accepts finite numeric results with a trailing `%`,
preserving that unit through nested expressions. Captured Hail of Bullets calls
return `30%`; the previous scalar-only validation rejected this valid result.
Arithmetic still comes from the game evaluator, without effect-specific formulas.

Inactive tracking now learns status SID, unique source/level, and parsed metrics
from successfully resolved, displayable statuses. A subsequent complete scan
with that SID absent supplies zero for those metrics, expiring after one second
unless renewed. The three-effect whitelist is removed. No inactive history is
invented before the first successful observation; conditional/unresolved text,
ambiguous owners, changed ownership/level, and incomplete scans remain unknown.
Scene/run resets clear learned mappings. Multiple state contributions that cannot
be uniquely merged remain unresolved rather than being summed speculatively.

Coverage is labelled “of time observed” (or “hits observed”), with unresolved
effects separately excluded from the known subtotal. This is observation coverage,
not the percentage of a build's effects that the tracker understands.

### Previous development 0.13.43

Fully Equipped and Last Stand now parse the captured prefix-style combined
weapon/skill values. Formatting tags in Damage Boost are stripped, and its
reported All DMG value has a separate All Damage headline; stacking with
weapon/skill modifiers is not inferred. Sentence-level parsing preserves
conditional guards while allowing Fully Equipped's separate recovery sentence.

Fresh complete state scans establish inactive zero for the reviewed Oppressor,
Fully Equipped, and Last Stand identities while their unique owner is present.
Those zeros expire after one second without renewal. Incomplete scans and
ambiguous ownership remain unknown. Persistent Damage Boost must report its
own zero. Regression coverage verifies six active seconds at +30% followed by
24 inactive seconds produces a +6% headline with 100% coverage.

The separate What powers this text panel has been removed. Existing Build bonus
headlines now show the 30-second combat average of the synchronous metric total.
This is computed from total samples, not by summing contributor averages with
different observation coverage. Expand a normal row for coverage and contributor
tiles; an asterisk marks partial observation or unresolved effect coverage.
Before combat samples exist the headline is a dash. CritX remains a current
weapon property; Lucky Shot remains the separately labelled observed hit estimate.
Both weapon and skill hits advance combat time. Earlier layout descriptions below
describe superseded development iterations.

The latest run shows Skill Fodder state 39903 changing from +25% to +50% with a
second gem. A unique live state for duplicate same-level Skill Fodders therefore
replaces their combined static amount once. Other ambiguous duplicates remain
unresolved. Arcane Surge state 39914 reports +12% Primary Skill DMG in that run;
it belongs to the Primary Skill headline and is not added to general Skill DMG.
General temporary Skill DMG bonuses affect the general headline when observed.

Development build 0.13.41 makes each contributor tile's main value the observed
30-second average, with coverage and weighting in its details. Current/listed
values remain secondary. Section totals still describe current bonuses, not
summed averages with incompatible coverage. The window records other represented
metrics too, over the same weapon-combat period. Partial static catalogue entries
without a live resolved value are unknown for average purposes.

Hit weighting starts conservatively with normal Hawkeye Sight's non-crit weapon
penalty. Confirmed weak-point hits establish the critical branch (zero penalty).
Non-weak-point hits remain unknown because effects can cause other critical hits;
they do not establish the -50% branch. Each accepted hit has equal weight, and
coverage is known-condition hits divided by observed hits. Target burning/shock
conditions have no verified per-hit state mapping and remain unknown. A partial
0% average is an average of known cases only, never proof of zero total impact.
Hit evidence is bounded; queue loss invalidates new condition measurements until
reset, and histories reset with the weapon. No new native hooks were added.

Development build 0.13.39 adds a time-weighted window for each represented
weapon bonus: average, peak, uptime, and coverage over the last 30 combat seconds.
Weapon hits keep combat active for two seconds; longer downtime pauses the
window. Changing weapons, changing player/run, or resetting stats clears it.
Values are sampled about every 250ms. A new sample is never applied backward.
Read pauses and polling gaps longer than one second are unknown intervals.
Average includes observed zero values; uptime is nonzero time divided by observed
time. Coverage is observed time divided by window combat time. During warmup,
the actual duration is shown. Negative bonuses count as active; these statistics
describe represented bonuses, not damage shares or final damage multipliers.

Oppressor state 32724 is linked to owned blessing 13514; its combined weapon/skill
description is parsed as two separate additive bonuses. A complete fresh state
scan with no Oppressor boost records zero while the blessing is owned. Missing
or incomplete scans cannot establish zero. Fresh Lucky Shot gem values replace
the static value once; stale or ambiguous values do not. Static partial Lucky
Shot values remain visible as listed contributions but do not count as observed
totals in the window. Defensive-only text no longer suggests a weapon contributor.

Research logging emits `displayed-bonus-snapshot` every two seconds, with the
post-merge rows, Lucky Shot contributors, window statistics, and trace health.
Large snapshots use numbered chunks; use time/player/epoch/weapon to group them
and require every chunk before treating a snapshot as complete. Shared trace
queue/storage limits still apply and are reported rather than hidden.

The first supported profile is weapon damage. On the Damage tab, expand Weapon
damage and select **What powers this?** Or open **Build > What powers this? >
Weapon damage**. This explains the current captured build, not the modifiers
present during every earlier hit in the run.

Each represented contributor shows its actual calculated amount, source,
applicability, and available catalogue description. Damage bonuses, independent
multipliers, crit/Lucky Shot/attack rate, and conditional effects stay separate.
No combined damage multiplier or per-item damage share is inferred.

Unresolved effects and possible interactions are separate expandable lists.
Their descriptions preserve listed values and triggers, but a listed maximum
is not promoted into an active bonus. Missing state is not zero. Element and
target bonuses remain conditional because the view does not establish that a
particular weapon hit satisfied their requirements. Partial catalogue entries
can have both a calculated contribution and remaining uncalculated clauses.

The weapon Lucky Shot component includes static inscriptions. Individual
inscription descriptions are retained without adding their numbers again.
Other Lucky Shot contributions retain their notes and caps; this view does not
sum them into a final probability. Description matching only suggests effects
to review and does not establish causation or source applicability.

Implementation uses existing managed inventory/modifier snapshots and the
existing fresh effective-bonus merge. There are no new native reads or hooks.
Contributions retain source key, ID, and level. `DamagePowerProfile` selects
rules and potential interactions independently of UI layout, so future skill
and seasonal profiles can reuse the model. POLE MONARCH Orb formula attribution
is not implemented by this change.

Validation: managed tests cover damage-source filtering, actual state-dependent
amounts, missing state, duplicate gems, conditional and independent layers,
weapon/inscription overlap, caps, and another profile. Run `scripts/Test.ps1`
and compile against the supported game installation.

In-game verification still required: open both entry points, inspect a long
build at different UI scales, expand the unresolved lists, and verify a
magazine-dependent bonus updates after firing/reloading. Compare conditional
descriptions with the game's tooltips. Compilation and managed tests cannot
verify the live layout or capture freshness under gameplay conditions.
