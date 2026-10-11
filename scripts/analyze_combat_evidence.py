"""Read one trace session and its rotated parts; never modifies game files.

Usage: python scripts/analyze_combat_evidence.py path/to/trace-...jsonl
The report distinguishes absent evidence from complete, matched observations.
"""
import argparse
import collections
import json
from pathlib import Path


def nearby_hits(death, hits):
    victim = death['fields'].get('data.iVictim')
    return [h for h in hits.get(victim, [])
            if h['Scene'] == death['Scene'] and h['Epoch'] == death['Epoch']
            and -10 <= h['Time'] - death['Time'] <= 2
            and (not h.get('victim') or not death.get('victim')
                 or h['victim']['Generation'] == death['victim']['Generation'])]


def report(path):
    stem = path.stem.split('-part')[0]
    events, health, scoreboard = {}, {}, []
    malformed = 0
    failures = []
    for file in path.parent.glob(stem + '*.jsonl'):
        with file.open(encoding='utf-8-sig', errors='replace') as source:
            for line in source:
                try:
                    row = json.loads(line)
                    kind = row.get('kind')
                    if kind == 'marker' and ('combat-hook-failed' in row['detail'] or 'combat-hook-missing' in row['detail']):
                        failures.append(row['detail'])
                    if kind not in ('combat-evidence', 'combat-hook-health', 'scoreboard-diagnostic'):
                        continue
                    value = json.loads(row['detail'])
                    if kind == 'combat-evidence':
                        key = value['Sequence']
                        events.setdefault(key, {})[value['chunk']] = value
                    elif kind == 'combat-hook-health':
                        if value['time'] >= health.get(value['Name'], {}).get('time', -1):
                            health[value['Name']] = value
                    else:
                        scoreboard.append(value)
                except (ValueError, KeyError):
                    malformed += 1
    assembled, incomplete = [], 0
    for chunks in events.values():
        head = chunks.get(0)
        if not head or set(chunks) != set(range(head['chunks'])):
            incomplete += 1
            continue
        row = dict(head)
        row['fields'] = {k: v for chunk in chunks.values() for k, v in chunk['fields'].items()}
        assembled.append(row)
    assembled.sort(key=lambda r: r['Sequence'])
    hits = collections.defaultdict(list)
    deaths = []
    for row in assembled:
        if row['Hook'].endswith('.GS2CWStatusHP'):
            fields = row['fields']
            try:
                if int(fields['data.iHP']) > 0 and not int(fields['data.iType']) & 0x30000000:
                    hits[fields['data.iVictim']].append(row)
            except (KeyError, ValueError):
                pass
        if row['Hook'].endswith('.GS2CDie'):
            deaths.append(row)
    print('Session:', stem)
    if not events:
        print('No broad combat-evidence records found; this run cannot validate bundle delivery.')
    print('Records:', len(assembled), 'Incomplete chunks:', incomplete, 'Malformed records:', malformed)
    print('Truncated copies:', sum(r['Truncated'] for r in assembled))
    for row in sorted(health.values(), key=lambda r: r['Name']):
        print(f"{row['Name']}: calls={row['calls']} errors={row['errors']} truncated={row['truncated']} suppressed={row.get('suppressed', 0)}")
    for failure in failures:
        print('INSTALLATION:', failure)
    if health:
        print('Queue drops:', max(h['drops'] for h in health.values()))
        print('Trace health:', max(health.values(), key=lambda r:r['time'])['traceHealth'])
    if scoreboard:
        last = max(scoreboard, key=lambda r:r['Sequence'])
        print('Latest scoreboard:', last['reportedTotal'], 'Captured:', last['CapturedAtReceipt'], 'Gap:', last['gapAtReceipt'])
    unresolved = []
    for death in deaths:
        candidates = nearby_hits(death, hits)
        if not candidates:
            victim = death.get('victim')
            unresolved.append(dict(sequence=death['Sequence'], time=death['Time'],
                attacker=death['fields'].get('data.iAttack'), victim=death['fields'].get('data.iVictim'),
                origin=victim.get('Origin') if victim else 'unknown',
                properties=victim.get('Properties') if victim else {}, final=death['fields'].get('data.iFinalDam')))
    print('Deaths:', len(deaths), 'Without hit in [-10s,+2s]:', len(unresolved))
    print('Unmatched deaths are candidates, not proof of missing damage; inspect lifecycle and loss counters.')
    for item in unresolved[:40]:
        print(json.dumps(item, ensure_ascii=True))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('trace', type=Path)
    args = parser.parse_args()
    report(args.trace)
