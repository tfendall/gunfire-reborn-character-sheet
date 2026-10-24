const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {extract}=require('../scripts/extract_attribution_evidence.cjs');
(async()=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'attribution-test-'));
  try {
    const line=(kind,detail)=>JSON.stringify({utc:'2026-10-17T04:00:00Z',kind,detail:typeof detail==='string'?detail:JSON.stringify(detail)});
    const row={SourceKey:'scroll',SourceId:5725,Metric:'Fire DMG buffs',Value:50};
    const snap=(time,chunk,chunks)=>line('displayed-bonus-snapshot',{time,chunk,chunks,player:1,epoch:0,weapon:1,rows:[row]});
    fs.writeFileSync(path.join(dir,'trace-test.jsonl'),[snap(1,0,2),snap(2,0,2),line('capture-health','Trace queue drops: 12'),'bad json'].join('\n')+'\n');
    fs.writeFileSync(path.join(dir,'trace-test-part2.jsonl'),[snap(2,1,2),snap(3,0,2),snap(3,0,2),snap(3,1,2)].join('\n')+'\n');
    fs.writeFileSync(path.join(dir,'trace-test-other.jsonl'),snap(4,0,1));
    const a=await extract(path.join(dir,'trace-test.jsonl'),path.join(dir,'result.json'));
    assert.equal(a.files.length,2);assert.deepEqual(a.missingParts,[1]);assert.equal(a.malformed,1);assert.equal(a.traceDrops,12);
    assert.equal(a.completeSnapshots,1);assert.equal(a.incompleteSnapshots,2);
    assert.equal(a.displayed.length,1);assert.equal(a.displayed[0].occurrences,2);
    console.log('PASS extractor excludes incomplete/duplicate chunks, joins cross-file chunks, reports loss and isolates session');
  } finally {fs.rmSync(dir,{recursive:true,force:true});}
})().catch(e=>{console.error(e);process.exitCode=1;});
