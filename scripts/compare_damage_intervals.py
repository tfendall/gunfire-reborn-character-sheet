"""Offline scoreboard interval audit. Reads traces; never changes game state."""
import bisect
import collections
import csv
import json
import re
from pathlib import Path

ROOT = Path('C:/Program Files (x86)/Steam/steamapps/common/Gunfire Reborn/BepInEx/StatsDiagnostic')
OUT = Path('artifacts/interval-comparison')
OUT.mkdir(parents=True, exist_ok=True)

def analyze(stem):
    scores, hits, changes, health, reconciliations = [], [], [], [], {}
    for path in ROOT.glob(stem + '*.jsonl'):
        # Snapshot length so an ongoing game cannot extend this read indefinitely.
        with path.open('rb') as stream:
            data = stream.read(path.stat().st_size)
        for line in data.splitlines():
            try:
                row = json.loads(line)
                kind, detail = row['kind'], row['detail']
                if kind == 'scoreboard-diagnostic':
                    scores.append(json.loads(detail))
                elif kind == 'damage-event':
                    hit = dict(re.findall(r'(\w+)=(\S*)', detail))
                    hit['utc'] = row['utc']
                    hits.append(hit)
                elif kind == 'damage-reconciliation':
                    d = json.loads(detail)
                    reconciliations[d['Sequence']] = d
                elif kind in ('inventory-scrolls','inventory-ascensions','inventory-blessings','inventory-season8','execution-marker'):
                    changes.append(row)
                elif kind == 'capture-health':
                    health.append(row)
            except (ValueError, KeyError):
                continue
    scores.sort(key=lambda d:d['Sequence'])
    hits.sort(key=lambda d:int(d['seq']))
    changes.sort(key=lambda d:d['utc'])
    health.sort(key=lambda d:d['utc'])
    result=[]
    # Cumulative damage provides exact event boundaries, independent of asynchronous log timestamps.
    cumulative=[0]
    for h in hits:
        cumulative.append(cumulative[-1]+int(h['damageDisplayUnits']))
    for before,after in zip(scores,scores[1:]):
        if before['Epoch'] != after['Epoch'] or after['CapturedAtReceipt']<before['CapturedAtReceipt']:
            continue
        a,b = before['CapturedAtReceipt'],after['CapturedAtReceipt']
        ia,ib = bisect.bisect_left(cumulative,a),bisect.bisect_left(cumulative,b)
        exact = ia<len(cumulative) and ib<len(cumulative) and cumulative[ia]==a and cumulative[ib]==b
        hs = hits[ia:ib] if exact else []
        groups=collections.Counter()
        for h in hs:
            amt=int(h['damageDisplayUnits']); typ=int(h['type']); extra=int(h.get('extraRaw',0))
            groups['credit:'+h.get('credit','unknown')]+=amt
            groups['type:'+str(typ)]+=amt
            groups['extra:'+str(extra)]+=amt
            groups['element:'+str(typ&7936)]+=amt
            if typ&0x30000000:groups['recovery']+=amt
        d=dict(seq=after['Sequence'],start=before['Utc'],end=after['Utc'],game=after['reportedTotal']-before['reportedTotal'],captured=b-a,
               gap=after['gapAtReceipt']-before['gapAtReceipt'],totalGap=after['gapAtReceipt'],hits=len(hs),exact=exact,
               pendingBefore=before['PendingAtReceipt'],pendingAfter=after['PendingAtReceipt'],groups=dict(groups),
               changes=[r for r in changes if before['Utc']<r['utc']<=after['Utc']],
               audit=reconciliations.get(after['Sequence']))
        result.append(d)
    output=dict(stem=stem,scores=scores,intervals=result,hitCount=len(hits),hitSum=cumulative[-1],health=health)
    (OUT/(stem+'.json')).write_text(json.dumps(output,indent=2),encoding='utf-8')
    with (OUT/(stem+'.csv')).open('w',newline='',encoding='utf-8') as f:
        fields=['seq','start','end','game','captured','gap','totalGap','hits','exact','pendingBefore','pendingAfter']
        w=csv.DictWriter(f,fieldnames=fields,extrasaction='ignore');w.writeheader();w.writerows(result)
    print(stem,'scores',len(scores),'intervals',len(result),'exact',sum(r['exact'] for r in result),'final',scores[-1])
    print('Largest positive gap intervals:')
    for d in sorted(result,key=lambda r:r['gap'],reverse=True)[:12]:
        print({k:d[k] for k in ('seq','start','end','game','captured','gap','hits','exact')},'groups', {k:v for k,v in d['groups'].items() if k.startswith(('credit:','extra:'))})
    return output

if __name__=='__main__':
    for stem in ['trace-20261010-041956-848','trace-20261011-040055-165']:
        analyze(stem)
