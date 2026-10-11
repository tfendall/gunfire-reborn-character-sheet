internal static class InvestigationTests
{
    internal static void Run(Action<bool,string> check)
    {
        var orbView=new AuthoritativeDamage();var orbLedger=new DamageLedger();
        orbLedger.Add(100,0x800800,30001);
        orbLedger.Add(20,0x400800,30001,208);
        orbLedger.Add(30,0x400400,30001,353);
        orbView.Observe(200,orbLedger,new(){{11,80}});
        check(orbView.View.Cells[("Seasonal · POLE MONARCH Orb","Unknown")]==80 &&
            orbView.View.Cells[("Unattributed damage","Unknown")]==20 && orbView.View.Cells.Values.Sum()==200 &&
            !orbView.View.Cells.Keys.Any(k=>k.Source=="Seasonal · Magic Wand") && orbLedger.Total==150,
            "server orb subtotal replaces overlapping packets without changing total or raw audit");
        orbView.Observe(230,orbLedger,new(){{11,80},{14,30}});
        check(orbView.View.Cells[("Seasonal · Effect 14","Unknown")]==30 &&
            orbView.View.Cells[("Seasonal · POLE MONARCH Orb","Unknown")]==80,
            "other seasonal entries do not get mislabeled as orb damage");
        orbView.Observe(230,orbLedger);
        check(!orbView.View.Cells.Keys.Any(k=>k.Source.Contains("Orb")) && orbView.View.Cells.Values.Sum()==230,
            "unavailable or rejected breakdown falls back without retaining stale orb credits");
        orbView.Reset(true);orbView.Observe(230,orbLedger,new(){{11,80},{14,30}});
        orbLedger.Add(5,0x400800,30001,208);
        orbView.Observe(250,orbLedger,new(){{11,100},{14,30}});
        check(orbView.View.Total==20 && orbView.View.Cells.Count==1 &&
            orbView.View.Cells[("Seasonal · POLE MONARCH Orb","Unknown")]==20,
            "manual reset subtracts both seasonal and captured baselines");
        orbView.Observe(0,orbLedger,new());
        orbLedger.Add(2,0x400800,30001,353);orbView.Observe(10,orbLedger,new(){{11,10}});
        check(orbView.View.Total==10 && orbView.View.Cells.Values.Sum()==10 &&
            orbView.View.Cells[("Seasonal · POLE MONARCH Orb","Unknown")]==10,
            "new run cannot carry prior seasonal credits across a scoreboard reset");
        orbView.Reset(true);orbView.Observe(10,orbLedger);
        orbView.Observe(20,orbLedger,new(){{11,20}});
        check(!orbView.View.Cells.Keys.Any(k=>k.Source.Contains("Orb")),
            "missing seasonal baseline cannot reintroduce damage from before reset");
        orbView.Reset(true);orbLedger.Clear();orbView.Observe(0,orbLedger);
        orbLedger.Add(2,0x400800,30001,208);orbView.Observe(10,orbLedger,new(){{11,10}});
        check(orbView.View.Cells[("Seasonal · POLE MONARCH Orb","Unknown")]==10 && orbView.View.Cells.Count==1,
            "zero-score reset before equipping orb preserves later authoritative seasonal attribution");
        var sampler=new ScoreboardSamplingState();
        var authority=new AuthoritativeDamage();var ledger=new DamageLedger();
        ledger.Add(80,0x800800,30001);authority.Observe(100,ledger);
        check(authority.View.Total==100 && authority.View.Cells.Values.Sum()==100 && authority.View.Cells[("Unattributed damage","Unknown")]==20,
            "authoritative total includes a separate exact unattributed remainder");
        var displayRows=DamageTabModel.Create(authority.View);
        check(displayRows.Single(r=>r.Name=="Unattributed damage").RunPercent==20 &&
            displayRows.Single(r=>r.Name=="Weapon damage").RunPercent==80 &&
            displayRows.Single(r=>r.Name=="Unattributed damage").Elements.Single().Element=="Unknown",
            "unattributed row stays separate and all percentages use the server denominator");
        ledger.Add(30,0x800800,30001);authority.Observe(100,ledger);
        check(authority.View.Total==100 && authority.View.Cells[("Reconciliation adjustment","Unknown")]==-10,
            "captured excess is explicit rather than silently clamped or scaled");
        authority.Reset(true);ledger.Clear();ledger.Add(5,0x800800,30001);authority.Observe(115,ledger);
        check(authority.View.Total==0 && authority.View.Cells.Count==0,"manual reset anchors to the next fresh server report");
        ledger.Add(7,0x800800,30001);authority.Observe(125,ledger);
        check(authority.View.Total==10 && authority.View.Cells[("Unattributed damage","Unknown")]==3 && authority.View.Cells.Values.Sum()==10,
            "reset subtracts matching source and server baselines");
        authority.Observe(double.NaN,ledger);
        check(authority.View.Total==10,"invalid server values cannot poison the display");
        check(sampler.Tick(0,false)==0 && sampler.Tick(1,true)==0 && sampler.Tick(2,true)==0,
            "scoreboard sampling waits for stable eligibility");
        check(sampler.Tick(3,true)==1 && sampler.Tick(4,true)==0,"only one automatic scoreboard request is outstanding");
        check(!sampler.CheckTimeout(12) && sampler.CheckTimeout(13) && !sampler.CheckTimeout(14) && sampler.Tick(30,true)==0,
            "scoreboard timeout logs once and does not retry an unanswered request");
        check(sampler.Response(31)==1 && sampler.Tick(31.5,true)==0 && sampler.Tick(32,true)==2,
            "late scoreboard response resumes bounded sampling");
        sampler.Tick(33,false);
        check(sampler.Tick(40,true)==0 && sampler.Tick(43,true)==0,
            "loading does not discard an outstanding request and create duplicates");
        check(sampler.Response(44)==2 && sampler.Tick(45,true)==3,"response unblocks request after loading");
        sampler.Response(46);sampler.Response(46.5);
        check(sampler.Tick(47,true)==0 && sampler.Tick(47.5,true)==4,"natural reports also debounce automatic requests");
        DamageAudit.Enabled=true;
        DamageAudit.Reset();
        var hit=new DamageIdentity(10,30001,20,4096,999999,0,0,0,1,1);
        var packet=DamageAudit.Begin(hit);
        check(DamageAudit.Observe(hit)==packet!.Id,"packet correlation matches the full payload in the current call");
        var nested=DamageAudit.Begin(hit);
        check(DamageAudit.Observe(hit)==nested!.Id && nested.Id!=packet.Id,"identical nested packets retain distinct identities");
        DamageAudit.End(nested);
        check(DamageAudit.Observe(hit)==packet.Id,"nested packet completion restores parent correlation");
        DamageAudit.End(packet);
        var missing=DamageAudit.Begin(hit);DamageAudit.End(missing);
        DamageAudit.Observe(hit);
        var audit=DamageAudit.Take();
        check(audit.Buckets.Single(x=>x.Key.Outcome=="packet-arrival").Count==3 &&
            audit.Buckets.Single(x=>x.Key.Outcome=="packet-matched").Count==2 &&
            audit.Buckets.Single(x=>x.Key.Outcome=="packet-duplicate-callback").Count==1 &&
            audit.Buckets.Single(x=>x.Key.Outcome=="packet-without-callback").Count==1 &&
            audit.Buckets.Single(x=>x.Key.Outcome=="callback-without-packet").Count==1,
            "packet audit distinguishes missing, repeated, and unscoped callbacks without adding ledger damage");
        check(DamageAudit.Take().Buckets.Length==0,"scoreboard cuts consume each audit interval once");
        for(int i=0;i<600;i++)DamageAudit.Record("rejected-owner-unavailable",hit with {Attacker=i});
        audit=DamageAudit.Take();
        check(audit.Buckets.Length==513 && audit.OverflowEvents==88 && audit.Buckets.Sum(x=>x.Count)==600 &&
            audit.Buckets.Sum(x=>x.RawDamage)==600m*999999,"bounded rejection groups preserve every count and exact raw sum in explicit overflow");
        packet=DamageAudit.Begin(hit);DamageAudit.Reset();DamageAudit.End(packet);
        check(DamageAudit.Take().Buckets.Length==0,"a packet spanning reset cannot leak unmatched counts into the new epoch");
        packet=DamageAudit.Begin(hit);
        check(DamageAudit.Observe(hit with {Damage=1})==0,"changed callback payload is not falsely paired");
        DamageAudit.End(packet);DamageAudit.Take();
        Parallel.For(0,1000,i=>DamageAudit.Record("accepted-player",hit));
        check(DamageAudit.Take().Buckets.Single().Count==1000,"concurrent accounting preserves all hits");
        DamageAudit.Enabled=false;
        DeathObservation.Hit(10,1,1,42,hit);
        DeathObservation.Hit(10,1,2,43,hit with {Victim=99});
        DeathObservation.Death(10,1,3,10,20,800);
        var death=DeathObservation.Drain(10,1).Single();
        check(death.FinalDamage==800 && death.RecentHits.Single().Packet==42 && death.CandidateCount==1,
            "death correlation retains final amount and matches only the victim without crediting damage");
        DeathObservation.Death(10,1,20,10,20,800);
        check(DeathObservation.Drain(10,1).Single().RecentHits.Length==0,"expired hits cannot explain a later death");
        for(int i=0;i<12;i++)DeathObservation.Hit(10,1,21+i*.01,i,hit);
        DeathObservation.Death(10,1,22,10,20,800);
        death=DeathObservation.Drain(10,1).Single();
        check(death.CandidateCount==12 && death.RecentHits.Length==8 && death.RecentHits[0].Packet==4,
            "death candidates keep latest eight and report omitted candidates");
        DeathObservation.Death(10,1,22,10,20,800);
        check(DeathObservation.Drain(10,2).Length==0,"reset removes queued deaths and old hit context");
        DeathObservation.Death(11,2,22,11,20,800);
        check(DeathObservation.Drain(11,2).Single().RecentHits.Length==0,"player change cannot reuse another player's hit context");
        for(int i=0;i<1030;i++)DeathObservation.Hit(11,2,23,i,hit);
        for(int i=0;i<130;i++)DeathObservation.Death(11,2,24,11,20,800);
        var batch=DeathObservation.Drain(11,2);
        check(batch.Length==16 && batch[0].HistoryEvictions==6 && batch[0].QueueDrops==2,
            "death history and queues are bounded and report evictions and overflow");
        DeathObservation.Drain(0,3);
        var evidence=new CombatEvidenceModel();
        var fields=new Dictionary<string,string>{{"data.iMonster","20"},{"data.dPropInfo.HP","100"},{"data.dPropInfo.SID","42"},{"data.dPropInfo.Owner","0"}};
        evidence.Add(1,0,0,1,"spawn","birth",fields);fields["data.dPropInfo.HP"]="999";
        evidence.Add(2,10,0,1,"prop","property",new(){{"data.iTarget","20"},{"data.dPropInfo.HP","60"}});
        evidence.Add(3,10,0,1,"death","combat",new(){{"data.iAttack","10"},{"data.iVictim","20"}});
        var evidenceRows=evidence.Drain();
        check(evidenceRows[1].PropertyDeltas["HP"]==-40 && evidenceRows[2].Victim!.Properties["HP"]=="60" && evidenceRows[2].Victim!.Origin=="spawn",
            "combat evidence links pre-readiness identities, property deltas and deaths using copied values");
        check(evidenceRows[0].Subject!.Properties["HP"]=="100","later health updates cannot rewrite earlier evidence snapshots");
        evidence.Add(4,10,0,2,"death","combat",new(){{"data.iVictim","20"}});
        check(evidence.Drain().Single().Victim==null,"scene mismatch prevents stale victim identity attribution");
        evidence.Add(5,10,0,1,"remove","remove",new(){{"pid","20"}});
        evidence.Add(6,10,0,1,"death","combat",new(){{"data.iVictim","20"}});
        evidenceRows=evidence.Drain();
        check(evidenceRows[0].Subject!=null && evidenceRows[1].Victim==null,"removal retains final identity evidence then invalidates the object");
        evidence.Add(7,10,0,1,"spawn","birth",new(){{"data.iMonster","20"},{"data.dPropInfo.HP","200"}});
        check(evidence.Drain().Single().Subject!.Generation!=evidenceRows[0].Subject!.Generation,"reused object IDs start a new entity generation");
        evidence.Add(8,10,0,1,"cache","property",new(){{"pid","20"},{"attrname","HP"},{"attrValue","150"}});
        check(evidence.Drain().Single().PropertyDeltas["HP"]==-50,"cached property update path exposes health deltas too");
        evidence.Add(9,10,0,1,"clear","clear",new());
        evidence.Add(10,10,0,1,"death","combat",new(){{"data.iVictim","20"}});
        check(evidence.Drain().Last().Victim==null,"world cache clears invalidate entity metadata");
        for(int i=0;i<4100;i++)evidence.Add(i,10,0,1,"raw","combat",new());
        check(evidence.Pending==4096 && evidence.Drops==4 && evidence.Drain().Length==64,"broad capture has bounded queues and visible loss counters");
        var floodGate=new CombatCaptureGate();
        check(Enumerable.Range(0,10000).Count(_=>floodGate.Accept(1))==400,"flood protection caps copying before snapshot allocation");
        check(floodGate.Accept(2) && floodGate.Accept(0),"flood gate recovers in the next window and on clock reset");
        var q=new InvestigationBuffer();
        q.Add(1,2,"m","c","same","same");q.Add(1,2,"m","c","same","same");
        q.Add(2,2,"m","c",""," ");q.Add(1,1,"m","c",""," ");
        var records=q.Drain(1,2,out int stale);
        check(records.Length==2 && records[0].Sequence!=records[1].Sequence && stale==2 && q.Pending==0,
            "observers retain repeated events and discard other players or old epochs");
        q.Add(1,2,"m",new string('c',1100),new string('i',7000),new string('r',3000));
        var clipped=q.Drain(1,2,out _).Single();
        check(clipped.Truncated && clipped.Context.Length==1000 && clipped.Input.Length==6000 && clipped.Result.Length==2000,
            "observer payloads are bounded with explicit clipping");
        Parallel.For(0,1200,i=>q.Add(1,2,"m","c","",""));
        check(q.Pending==1024 && q.Drops==176,"concurrent observer bursts respect capacity and report loss");
        check(q.Drain(1,2,out _).Length==32 && q.Pending==992,"observer writer drains bounded batches");
        var original=InvestigationScope.Push("skill");
        var parent=InvestigationScope.Push("formula");
        try { throw new InvalidOperationException(); }
        catch(InvalidOperationException) { }
        finally { InvestigationScope.Restore(parent); }
        check(InvestigationScope.Current=="skill","nested formula context restores its caller after exceptions");
        string other="unset";
        var thread=new Thread(()=>{other=InvestigationScope.Current;InvestigationScope.Push("other");});
        thread.Start();thread.Join();
        check(other=="" && InvestigationScope.Current=="skill","calculation context cannot cross threads");
        InvestigationScope.Restore(original);
        var dirty=new InvestigationDirtyProperties();
        dirty.Mark(1,"SkillDamage",10);dirty.Mark(1,"SkillDamage",20);
        dirty.Mark(0,"SkillDamage",0);dirty.Mark(1,"",0);
        var changed=dirty.Drain();
        check(changed.Length==1 && changed[0].Timestamp==10,"property notifications coalesce without losing oldest latency");
        for(int i=1;i<=300;i++)dirty.Mark(i,"Damage",i);
        check(dirty.Drops==44 && dirty.Drain().Length==8,"property refreshes have bounded capacity and batches");
        dirty.Clear();
        check(dirty.Drain().Length==0,"scene changes clear pending property reads");
        var inputs=new Dictionary<string,long>{{"n",0},{"dam",500},{"lvl",1}};
        var expressions=new List<string>();
        string rendered=EffectExpression.Render("+((n+dam)/100)% Rate of Fire.",inputs,expression=>
        {expressions.Add(expression);return expression=="(n+dam)"?"500":"5";});
        check(rendered=="+5% Rate of Fire." && expressions.SequenceEqual(new[]{"(n+dam)","(500/100)"}),
            "nested description expressions delegate each calculation to the evaluator");
        check(EffectExpression.Render("+(n*1)% speed, +(lvl*1*n)% damage.",inputs,_=>"100")=="+100% speed, +100% damage.",
            "multiple placeholders preserve their labels and units");
        check(EffectExpression.Render("Stores Weapon DMG as (dam).",inputs,_=>"16460")=="Stores Weapon DMG as 16460.",
            "stored damage remains an amount rather than a percentage");
        bool Reject(string text,Func<string,string> evaluator)
        {try{EffectExpression.Render(text,inputs,evaluator);return false;}catch(InvalidOperationException){return true;}}
        check(Reject("(unknown)",_=>throw new Exception("must not evaluate")),"missing variables cannot silently evaluate as zero");
        check(Reject("((n)",_=>"1") && Reject("(n))",_=>"1"),"malformed descriptions fail closed");
        check(Reject("(n)",_=>"NaN") && Reject("(n)",_=>"Infinity") && Reject("(n)",_=>"unresolved"),"nonfinite or unresolved game results cannot become bonuses");
        check(Reject(new string('(',10)+"n"+new string(')',10),_=>"1"),"expression nesting is bounded");
        var namedInputs=new Dictionary<string,long>{{"ST39903_DamRatio",2500}};
        string? received=null;
        check(EffectExpression.Render("+(ST39903_DamRatio/100)% Skill DMG.",namedInputs,e=>{received=e;return "25";})=="+25% Skill DMG." && received=="(ST39903_DamRatio/100)",
            "named state variables reach the evaluator without per-effect arithmetic");
        check(Reject("+(ST39903_DamRatio/100)% Skill DMG.",_=>throw new Exception("must not evaluate")),
            "a missing named field cannot reuse another effect's parameters");
        var swiftInputs=new Dictionary<string,long>{{"n",2},{"dam",1600}};
        check(EffectExpression.Render("+(n*dam/100)% Movement Speed.",swiftInputs,e=>{received=e;return "32";})=="+32% Movement Speed." && received=="(n*dam/100)",
            "Swiftstep stack and stored-value expression delegates unchanged");
        check(EffectExpression.Render("+(n) free Dash charges.",swiftInputs,_=>"2")=="+2 free Dash charges." &&
            EffectExpression.Render("(dam) DMG stored.",swiftInputs,_=>"1600")=="1600 DMG stored.",
            "dash charges and stored damage retain distinct nonpercentage units");
        var firstBatch=EffectExpression.Batch(Enumerable.Range(1,19),0);
        var secondBatch=EffectExpression.Batch(Enumerable.Range(1,19),firstBatch.Last());
        var thirdBatch=EffectExpression.Batch(Enumerable.Range(1,19),secondBatch.Last());
        check(firstBatch.Concat(secondBatch).Concat(thirdBatch).Distinct().Count()==19 && thirdBatch.Length==8,
            "bounded refresh rotation eventually visits every effect rather than starving later states");
        check(EffectExpression.Batch(new[]{2,2,7,20},9).SequenceEqual(new[]{20,2,7}),
            "refresh cursor tolerates removed states and wraps with unique IDs");
        bool InvalidTemplate(string text)
        {try{EffectExpression.Validate(text,inputs);return false;}catch(InvalidOperationException){return true;}}
        check(InvalidTemplate("+(n)% damage, +(missing)% speed."),"preflight rejects a later missing dependency before native evaluation");
        check(InvalidTemplate("+{n}% damage") && InvalidTemplate("+[n]% damage"),"unknown template syntax is unresolved rather than returned as a resolved value");
        var view=new EffectiveBonusView();
        view.Set(1,"Companion bonus","+275% DMG dealt by Iron Wing, +22% Attack Speed.",10);
        view.Set(2,"Stored damage","48780 DMG stored.",10);
        check(view.Snapshot().Length==2 && EffectiveBonusView.Display(view.Snapshot()[0],11).Contains("Iron Wing"),
            "effective UI retains scope and amount wording instead of aggregating percentages");
        check(EffectiveBonusView.Display(view.Snapshot()[0],15).Contains("Last checked 5s ago"),
            "paused or delayed reads are visibly marked as older observations");
        view.Touch(1,15);
        check(!EffectiveBonusView.Display(view.Snapshot()[0],15).Contains("Last checked"),
            "unchanged verified inputs renew UI freshness");
        view.Set(1,"Companion bonus","",16,false);
        check(EffectiveBonusView.Display(view.Snapshot()[0],16)=="Current value unavailable.",
            "failed resolution replaces stale numeric UI with unavailable");
        view.Remove(1);check(view.Snapshot().Length==1,"removed effects disappear from UI");
        view.Clear();check(view.Snapshot().Length==0,"run changes clear effective UI snapshots");
        var rapidOwner=new CoverageFinding("gem",51771,1,"Rapidfire Upgrade",ModifierCoverage.Partial,"","Soul Jade");
        var paceOwner=new CoverageFinding("ascension",3316,1,"Battle Pace",ModifierCoverage.NeedsState,"","Ascension");
        var staticRows=new[]{new BuildContribution("Rate of Fire","Soul Jade","Rapidfire Upgrade (Lv 1)",5),
            new BuildContribution("Rate of Fire","Scroll","Other",20)};
        var liveRows=new[]{new EffectiveBonusRow(1,"Rapidfire Upgrade","+11% Rate of Fire.",10,true,"gem",51771),
            new EffectiveBonusRow(2,"Battle Pace","+100% current Movement Speed, +100% Weapon DMG.",10,true,"ascension",3316)};
        var merged=EffectiveContributionMerge.Merge(staticRows,liveRows,new[]{rapidOwner,paceOwner},11);
        check(merged.Where(r=>r.Metric=="Rate of Fire").Sum(r=>r.Value)==31 && merged.Count(r=>r.Name.StartsWith("Rapidfire"))==1,
            "live bonus replaces static contributor once and updates aggregate without double counting");
        check(merged.Single(r=>r.Metric=="Weapon DMG buffs").Value==100 &&
            ContributorTiles.Create("Weapon DMG buffs",merged.Where(r=>r.Metric=="Weapon DMG buffs"),Array.Empty<LscRow>(),new[]{paceOwner}).Single().Value=="+100%",
            "formerly dashed contributor receives live amount and contributes to matching header");
        check(EffectiveContributionMerge.Merge(staticRows,liveRows,new[]{rapidOwner,paceOwner},16).Count==2,
            "stale live values fall back to original known contributions");
        check(!EffectiveContributionMerge.Parse("+275% DMG dealt by Iron Wing, +22% Attack Speed, Movement Speed, and Attack Range.").Any() &&
            !EffectiveContributionMerge.Parse("+50% Final Weapon DMG.").Any() &&
            !EffectiveContributionMerge.Parse("+50% Weapon DMG against burning enemies.").Any(),
            "companion, independent and target-scoped percentages cannot inflate general totals");
        check(EffectiveContributionMerge.Parse("+12% Primary Skill DMG.").Single().Metric=="Primary Skill DMG buffs" &&
            !EffectiveContributionMerge.Parse("48780 DMG stored.").Any(),
            "primary scope remains separate and stored damage is not a bonus");
        check(EffectiveContributionMerge.Merge(staticRows,new[]{liveRows[0],liveRows[0] with {Id=3}},new[]{rapidOwner},11).Sum(r=>r.Value)==25,
            "ambiguous multiple states are not summed or double counted");
        var zero=EffectiveContributionMerge.Merge(staticRows,new[]{liveRows[0] with {Text="+0% Rate of Fire."}},new[]{rapidOwner},11);
        check(zero.Single(r=>r.Name.StartsWith("Rapidfire")).Value==0,"live zero replaces a positive baseline without falling back");
    }
}
