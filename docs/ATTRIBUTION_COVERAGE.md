# Offline attribution coverage and replay

Requires Node.js and the repository's .NET SDK. No game process or native game evaluator is needed for the report or tests.

From the repository root:

```powershell
node scripts/extract_attribution_evidence.cjs 'PATH/trace-20261017-040723-282.jsonl' artifacts/attribution-evidence-20261017.json
& "$env:USERPROFILE/.dotnet/dotnet.exe" run --project tests/Managed/ManagedTests.csproj -c Release -- --coverage artifacts/attribution-evidence-20261017.json artifacts/attribution-coverage-20261017.html
./scripts/Test.ps1
```

Pass the base session filename. The extractor reads only that session and its numerically ordered parts, reporting missing parts, malformed records and reported trace drops. Numbered display chunks must all be present, with no duplicates, before their rows enter the historical comparison. Files can be processed across rotation boundaries. Analyze a completed capture; a live capture is not a stable input.

The filterable HTML report and adjacent JSON cover the union of resolved and unresolved state definitions observed in the trace. They separate current production-parser eligibility, source evidence, recorded evaluator failures, and historical source/metric/value matches. Unmatched values expose gaps such as Movement Speed Fodder's captured 34/40/31/22 versus the old display's catalogue values. A supported clause does not imply every clause of an effect is supported.

Historical matches are session-wide corroboration, not temporal attribution. The old snapshots do not record which state produced a row, whether it replaced a catalogue value, or why a candidate was rejected. Inventory ownership, competing states and freshness at each merge cannot be established by this report. Name-derived catalogue candidates are explicitly weaker than observed source IDs. This report does not compute effect-completeness percentages, uptime or per-item damage shares, and it does not treat missing data as zero. Missing final trace parts cannot be detected without a session manifest.

## Recorded replay fixtures

`tests/Managed/Fixtures/attribution-20261017.json` contains 23 selected real effect records, with original file, line and UTC. These cover Movement Speed Fodder, all three Cube elements, both Undivided Attention metrics, Resolute, Convergent Paths' skill branch, Field Support, final skill damage and this-shot damage. Fixtures are selected from the extractor's `fixtures` collection; retain provenance when adding samples.

Replay feeds recorded native answers into the production `EffectExpression.Render`, checks the exact expression sequence and rendered result, then exercises production parsing and merging. It validates managed interpretation of captured answers, not the game's arithmetic or native hook delivery. Inventory contexts and expiry checks in the tests are controlled scenarios, explicitly not a reconstructed run timeline. Existing attribution-gap tests cover element switching, duplicate owners, retained bonuses and incomplete scans.

Field Support's historical zero is rejected using the same resolution policy as live refresh. Neither a numeric contribution nor a learned inactive zero is created. The report still shows the historical observation and external originating-entity evidence.

## Further fixes from the October 16 run

Resolute state 1108 (`part46`, line 2844) renders `+50% Movement Speed, +50% DMG Dealt.` The whole, unqualified `DMG Dealt` clause now uses the existing All Damage metric; it is not duplicated into Weapon and Skill Damage. This does not establish its stacking formula with other damage layers. Conditional, final, companion and incoming-damage clauses remain excluded.

Convergent Paths state 1818 (`part140`, line 2074) renders `-20% Weapon DMG, +50% Skill DMG.` The verified mapping requires exactly one owned normal scroll 5865. Fresh overlapping Convergent Paths states, including unresolved states, block this contribution because the scroll's combined-branch behavior changes both the bonus and penalty. Enhanced variants and unobserved branches remain unverified. Learned absence preserves the state definition and clears both components on a complete scan. Replay tests cover these guards, ownership loss/ambiguity, expiry and conditional parsing.

Copycat variants remain deferred: a matching label and rendered value do not establish owned blessing provenance in this run. Missing historical value matches alone are not grounds to alter Firepower Upgrade or Victory Rush; sampling and state ambiguity can also explain them.

Extractor tests cover missing files, malformed JSON, session isolation, incomplete and duplicated chunks, and a complete snapshot spanning files. All run through `scripts/Test.ps1` alongside managed and installer checks. Reports and bulk extracted evidence stay in ignored `artifacts/`; compact replay fixtures are tracked source data.
