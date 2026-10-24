// Stream one session and its numbered parts; never load the full run into memory.
// Usage: node scripts/extract_attribution_evidence.cjs <base.jsonl> <output.json>
const fs = require('fs'), path = require('path'), readline = require('readline');
async function extract(base, output) {
  const dir = path.dirname(base), stem = path.basename(base, '.jsonl');
  if (/-part\d+$/.test(stem)) throw Error('Pass the base session filename, not a part.');
  const pattern = new RegExp('^' + stem.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '(?:-part(\\d+))?\\.jsonl$');
  const files = fs.readdirSync(dir).filter(f => pattern.test(f)).sort((a,b) => Number(pattern.exec(a)[1]||0)-Number(pattern.exec(b)[1]||0));
  if (!files.length) throw Error('No session files found.');
  const out = {session:stem, files, missingParts:[], malformed:0, incompleteSnapshots:0, completeSnapshots:0,
    traceDrops:0, first:null, last:null, effects:{}, details:{}, unresolved:{}, displayed:[], fixtures:[]};
  const parts = files.map(f => Number(pattern.exec(f)[1]||0));
  for(let i=0;i<=Math.max(...parts);i++) if(!parts.includes(i)) out.missingParts.push(i);
  const rows = new Map(), fixtureKeys = new Set(); let snapshot=null;
  function finishSnapshot() {
    if(!snapshot) return;
    if(snapshot.parts.size!==snapshot.chunks) out.incompleteSnapshots++;
    else {
      out.completeSnapshots++;
      for(const r of snapshot.rows) {
        const k=JSON.stringify([r.SourceKey,r.SourceId,r.Metric,r.Value]);
        if(!rows.has(k)) rows.set(k,{...r,occurrences:0,evidence:snapshot.evidence});
        rows.get(k).occurrences++;
      }
    }
  }
  const selected = new Set([39988,1096,1097,1098,1236,33069,33070,39884,39879,1108,1818]);
  for(const file of files) {
    let lineNumber=0;
    for await(const line of readline.createInterface({input:fs.createReadStream(path.join(dir,file)),crlfDelay:Infinity})) {
      lineNumber++; let o;
      try { o=JSON.parse(line); } catch { out.malformed++; continue; }
      out.first??=o.utc; out.last=o.utc;
      const evidence={file,line:lineNumber,utc:o.utc};
      try {
        if(o.kind==='capture-health') out.traceDrops=Math.max(out.traceDrops,Number(/Trace queue drops: (\d+)/.exec(o.detail)?.[1]||0));
        if(o.kind==='effect-refresh-result') {
          const d=JSON.parse(o.detail), e=out.effects[d.sid]??={name:d.name,count:0,variants:{}};
          e.count++; const v=e.variants[d.result]??={count:0,data:d,evidence}; v.count++;
          const key=d.sid+':'+d.result;
          // Bounded, real formula samples; expectations are asserted independently in C#.
          if(selected.has(d.sid)&&!fixtureKeys.has(key)) {
            fixtureKeys.add(key);out.fixtures.push({kind:o.kind,data:d,evidence});
          }
        }
        if(o.kind==='active-status-detail') {
          const d=JSON.parse(o.detail), list=out.details[d.sid]??=[];
          const key=JSON.stringify([d.performType,d.typeSid,d.attacker,d.extraAvailable]);
          if(!list.some(x=>x.key===key)) list.push({key,data:d,evidence});
        }
        if(o.kind==='effect-refresh-unresolved') {
          const m=/sid=(\d+) reason=(.*)/.exec(o.detail);
          if(m) {const e=out.unresolved[m[1]]??={count:0,reasons:{},evidence};e.count++;e.reasons[m[2]]=(e.reasons[m[2]]||0)+1;}
        }
        if(o.kind==='displayed-bonus-snapshot') {
          const d=JSON.parse(o.detail), key=JSON.stringify([d.time,d.player,d.epoch,d.weapon]);
          if(snapshot?.key!==key) { finishSnapshot();snapshot={key,chunks:d.chunks,parts:new Set(),rows:[],evidence}; }
          if(snapshot.parts.has(d.chunk)||d.chunks!==snapshot.chunks||d.chunk<0||d.chunk>=d.chunks) {snapshot.chunks=Infinity;}
          snapshot.parts.add(d.chunk);snapshot.rows.push(...d.rows);
        }
      } catch {out.malformed++;}
    }
  }
  finishSnapshot();out.displayed=[...rows.values()];
  fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,JSON.stringify(out,null,2)+'\n');
  console.log(JSON.stringify({files:files.length,effects:Object.keys(out.effects).length,completeSnapshots:out.completeSnapshots,incompleteSnapshots:out.incompleteSnapshots,traceDrops:out.traceDrops}));
  return out;
}
if(require.main===module) {
  if(process.argv.length!==4) {console.error('Usage: node extract_attribution_evidence.cjs <base.jsonl> <output.json>');process.exitCode=1;}
  else extract(process.argv[2],process.argv[3]).catch(e=>{console.error(e);process.exitCode=1;});
}
module.exports={extract};
