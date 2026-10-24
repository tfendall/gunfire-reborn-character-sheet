using System.Text.Json;

internal static class TraceReplayTests
{
    internal static void Run(Action<bool,string> check)
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","attribution-20261017.json")));
        var records=fixture.RootElement.GetProperty("records").EnumerateArray().ToArray();
        check(records.Length>=15,"captured replay fixture contains multiple effect families");
        foreach(var record in records)
        {
            var d=record.GetProperty("data");int sid=d.GetProperty("sid").GetInt32();
            string result=d.GetProperty("result").GetString()!;
            check(record.GetProperty("evidence").GetProperty("line").GetInt32()>0 && record.GetProperty("evidence").GetProperty("file").GetString()!.StartsWith("trace-20261017-040723-282"),
                $"replay {sid} preserves trace provenance");
            if(EffectiveBonusView.ResolutionBlock(sid)!=null)
            {
                check(sid==1236 && result=="+0% Weapon DMG." && AttributionCoverageReport.Classify(sid,result)=="unverified-field-support-inputs",
                    "historical Field Support zero remains blocked by current policy");
                continue;
            }
            var inputs=d.GetProperty("inputs").EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.GetInt64());
            var calls=new Queue<JsonElement>(d.GetProperty("calculations").EnumerateArray());
            string rendered=EffectExpression.Render(d.GetProperty("description").GetString()!,inputs,expression=>
            {
                var call=calls.Dequeue();
                if(call.GetProperty("expression").GetString()!=expression)throw new Exception("Replay requested a different native expression");
                return call.GetProperty("answer").GetString()!;
            });
            check(rendered==result && calls.Count==0,$"captured {sid} native answers reproduce exact rendered text");
            var values=EffectiveContributionMerge.Parse(rendered).ToArray();
            if(sid is 1108 or 1818)
            {
                ReplayNewBranches(check,d,rendered,sid);continue;
            }
            if(sid is 39884 or 39879)
            {
                check(values.Length==0 && AttributionCoverageReport.Classify(sid,rendered)=="unsupported or conditional scope",
                    "captured final-damage and this-shot scopes cannot enter additive totals");continue;
            }
            // These are explicit controlled contexts, not inferred historical inventory.
            bool speed=sid==39988,cube=sid is 1096 or 1097 or 1098;
            string source=speed?"gem":"scroll";int item=speed?51809:cube?5725:5871;
            string name=speed?"Movement Speed Fodder":cube?"Elemental Cube":"Undivided Attention";
            var owner=new CoverageFinding(source,item,1,name,ModifierCoverage.Partial,"controlled replay context",speed?"Soul Jade":"Scroll");
            var state=new EffectiveBonusRow(d.GetProperty("stateId").GetInt32(),d.GetProperty("name").GetString()!,rendered,10,true,
                speed?source:"",speed?item:0,StateSid:sid);
            var rows=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner},10);
            string metric=speed?"Movement Speed":sid==1096?"Fire DMG buffs":sid==1097?"Lightning DMG buffs":sid==1098?"Corrosion DMG buffs":sid==33069?"Skill DMG buffs":"Weapon DMG buffs";
            double expected=speed?inputs["ST39988_AddRatio"]/100d:cube?50:inputs["n"]*30;
            check(rows.Count==1 && rows[0].Metric==metric && rows[0].Value==expected && rows[0].SourceId==item,
                $"replayed {sid} attributes {expected} to correct source and metric");
            check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner},16).Count==0,
                $"replayed {sid} expires without fresh evidence");
        }
        var fieldOwner=new CoverageFinding("scroll",5807,1,"Field Support",ModifierCoverage.NeedsState,"","Scroll");
        var field=new EffectiveBonusRow(1,"Field Support","",10,false,StateSid:1236);
        var inactive=new InactiveBonusRules();inactive.Learn(1236,field,new[]{fieldOwner});
        check(inactive.Observe(Array.Empty<int>(),true,new[]{fieldOwner},11).Length==0,"blocked replay cannot learn a Field Support inactive zero");
    }
    private static void ReplayNewBranches(Action<bool,string> check,JsonElement data,string text,int sid)
    {
        bool paths=sid==1818;
        var owner=new CoverageFinding("scroll",paths?5865:5712,1,paths?"Convergent Paths":"Resolute",ModifierCoverage.NeedsState,"controlled replay context","Scroll");
        var state=new EffectiveBonusRow(data.GetProperty("stateId").GetInt32(),data.GetProperty("name").GetString()!,text,10,true,StateSid:sid);
        var rows=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner},10);
        check(rows.Count==2 && rows.All(r=>r.SourceId==owner.Id) &&
            rows.Single(r=>r.Metric==(paths?"Weapon DMG buffs":"Movement Speed")).Value==(paths?-20:50) &&
            rows.Single(r=>r.Metric==(paths?"Skill DMG buffs":"All DMG buffs")).Value==50,
            $"captured {owner.Name} preserves both signed, separately scoped contributions");
        check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},Array.Empty<CoverageFinding>(),10).Count==0 &&
            EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner,owner},10).Count==0 &&
            EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner},16).Count==0,
            $"{owner.Name} needs unique owned source and fresh observation");
        var inactive=new InactiveBonusRules();inactive.Learn(sid,state,new[]{owner});
        var zeros=inactive.Observe(Array.Empty<int>(),true,new[]{owner},11);
        var cleared=EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),zeros,new[]{owner},11);
        check(cleared.Count==2 && cleared.All(r=>r.Value==0),$"{owner.Name} complete absence clears both components including penalty");
        check(inactive.Observe(Array.Empty<int>(),false,new[]{owner},11).Length==0,$"{owner.Name} incomplete scan cannot invent inactive zero");
        if(paths)
        {
            var other=state with {Id=state.Id+1,StateSid=1817,Name="Convergent Paths - Weapon",Resolved=false,Text=""};
            check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state,other},new[]{owner},10).Count==0,
                "unresolved overlapping Paths branch blocks single-branch attribution");
            check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state,other with {Resolved=true,Text="+40% Weapon DMG, -25% Skill DMG."}},new[]{owner},10).Count==0,
                "resolved overlapping Paths branches are not summed");
            check(EffectiveContributionMerge.Merge(Array.Empty<BuildContribution>(),new[]{state},new[]{owner with {Level=2}},10).Count==0,
                "unverified enhanced Paths variant remains unresolved");
            check(EffectiveContributionMerge.Owners(state with {StateSid=0},new[]{owner}).Length==0 &&
                EffectiveContributionMerge.Owners(state with {Source="scroll",SourceId=1},new[]{owner}).Length==0,
                "Paths mapping requires exact verified state and respects explicit source identity");
        }
        else check(!EffectiveContributionMerge.Parse("+50% DMG Dealt by Iron Wing.").Any() &&
            !EffectiveContributionMerge.Parse("+50% DMG Dealt against burning enemies.").Any() &&
            !EffectiveContributionMerge.Parse("+50% Final DMG Dealt.").Any() &&
            !EffectiveContributionMerge.Parse("+50% DMG taken.").Any(),
            "DMG Dealt alias cannot promote companion, target, final or incoming damage");
    }
}
