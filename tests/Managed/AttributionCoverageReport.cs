using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;

// Offline audit uses the production parser. Historical row matches are session-wide
// corroboration only: old traces do not carry sufficient decision provenance to
// reconstruct exact attribution, ownership, freshness or conflict at every poll.
internal static class AttributionCoverageReport
{
    internal static string Classify(int sid,string text)
    {
        if(EffectiveBonusView.ResolutionBlock(sid) is string blocked)return blocked;
        if(EffectiveContributionMerge.Parse(text).Any())return "supported metric clauses (other clauses may remain unresolved)";
        if(Regex.IsMatch(text,@"\b(final|next|this shot|against|taken|while|when|if)\b",RegexOptions.IgnoreCase))return "unsupported or conditional scope";
        return "no supported metric clause (may be nonnumeric or outside tracked bonuses)";
    }
    internal static void Run(string[] args)
    {
        if(args.Length!=3)throw new ArgumentException("--coverage <extracted-evidence.json> <report.html>");
        using var doc=JsonDocument.Parse(File.ReadAllText(args[1]));var root=doc.RootElement;
        var details=root.GetProperty("details");var unresolved=root.GetProperty("unresolved");
        var displayed=root.GetProperty("displayed").EnumerateArray().ToArray();
        var effects=root.GetProperty("effects");var output=new List<object>();var html=new System.Text.StringBuilder();
        string E(object? value)=>WebUtility.HtmlEncode(value?.ToString()??"");
        html.Append("<!doctype html><meta charset='utf-8'><title>Effect attribution coverage</title><style>body{font:15px system-ui;margin:32px;color:#20252b}table{border-collapse:collapse;width:100%}td,th{padding:10px;border:1px solid #ccd1d7;text-align:left;vertical-align:top}th{background:#edf2f7}code{overflow-wrap:anywhere}input{padding:10px;width:50%}</style>");
        html.Append($"<h1>Effect attribution coverage</h1><p>Session <code>{E(root.GetProperty("session"))}</code></p>");
        html.Append($"<p>{root.GetProperty("files").GetArrayLength()} files; {E(root.GetProperty("traceDrops"))} reported trace drops; {E(root.GetProperty("malformed"))} malformed records; {E(root.GetProperty("incompleteSnapshots"))} incomplete displayed snapshots excluded. Missing parts: {E(root.GetProperty("missingParts"))}.</p>");
        html.Append("<p><strong>Scope:</strong> observed state definitions, not all equipped items or all possible effects. Counts are logged changed evaluations, not uptime. Current parser eligibility is separate from historical display evidence. A matching source/metric/value elsewhere in the session does not prove that this state caused that row, or that it was fresh and owned at that instant. Unsupported clauses are never assumed to be zero. No overall completeness percentage is inferred.</p>");
        html.Append("<p>Priority: resolve missing source evidence for supported metrics; investigate formula-input failures; then implement reviewed scope/layer families. Field Support remains blocked. Staleness, competing states and precise inventory ownership require decision-level provenance; this report does not guess them.</p><input id='filter' placeholder='Filter by name, SID, reason or metric' aria-label='Filter effects'><table><thead><tr><th>Effect</th><th>Current interpretation</th><th>Source evidence</th><th>Historical displayed evidence</th><th>Evaluation failures / sample</th></tr></thead><tbody>");
        var ids=effects.EnumerateObject().Select(p=>p.Name).Concat(unresolved.EnumerateObject().Select(p=>p.Name)).Distinct().OrderBy(int.Parse);
        foreach(string id in ids)
        {
            int sid=int.Parse(id);bool hasEffect=effects.TryGetProperty(id,out var effect);
            var variants=hasEffect ? effect.GetProperty("variants").EnumerateObject().ToArray() : Array.Empty<JsonProperty>();
            string name=hasEffect ? effect.GetProperty("name").GetString()! : "Unresolved state";
            if(!hasEffect && details.TryGetProperty(id,out var nameDetails) && nameDetails.GetArrayLength()>0 &&
                nameDetails[0].GetProperty("data").TryGetProperty("name",out var stateName))name=stateName.GetString()!;
            var reasons=variants.Select(v=>Classify(sid,v.Name)).Distinct().ToArray();
            var metrics=EffectiveBonusView.ResolutionBlock(sid)!=null ? Array.Empty<(string Metric,double Value)>() : variants.SelectMany(v=>EffectiveContributionMerge.Parse(v.Name)).Distinct().ToArray();
            var candidates=new HashSet<(string Source,int Id)>();var provenance=new List<string>();
            if(details.TryGetProperty(id,out var dd))foreach(var item in dd.EnumerateArray())
            {
                var d=item.GetProperty("data");
                if(d.TryGetProperty("typeSid",out var typeSid)&&d.TryGetProperty("performType",out var type))
                {
                    string source=(type.GetInt32() & ~0x4000) switch {4=>"ascension",5=>"scroll",18=>"gem",_=>""};
                    if(source!="")candidates.Add((source,typeSid.GetInt32()));
                }
                if(d.TryGetProperty("attacker",out var attacker)&&d.TryGetProperty("player",out var player)&&attacker.GetInt64()!=player.GetInt64())provenance.Add("External originating entity observed; local ownership unproven");
            }
            if(candidates.Count>0)provenance.Add("Explicit source identity observed (not a time-aligned ownership check)");
            else if(sid is 1096 or 1097 or 1098){candidates.Add(("scroll",5725));provenance.Add("Verified Cube mapping; current merge still requires one owned Cube");}
            else if(sid==32724){candidates.Add(("blessing",13514));provenance.Add("Verified Oppressor mapping");}
            else if(sid==1818){candidates.Add(("scroll",5865));provenance.Add("Verified normal Convergent Paths skill branch; one owned normal scroll and no overlapping branch required");}
            else
            {
                foreach(var entry in ModifierCatalog.Entries.Where(p=>p.Value.Name==name))candidates.Add((entry.Key.Source,entry.Key.Id));
                provenance.Add(candidates.Count==1 ? "Catalogue name candidate only; ownership not established" : "Missing or ambiguous source identity");
            }
            var matched=displayed.Where(r=>candidates.Contains((r.GetProperty("SourceKey").GetString()!,r.GetProperty("SourceId").GetInt32())) &&
                metrics.Any(m=>m.Metric==r.GetProperty("Metric").GetString() && m.Value==r.GetProperty("Value").GetDouble())).ToArray();
            string historical=matched.Length>0 ? $"{matched.Length} distinct source/metric/value matches in complete snapshots; session-wide only" : "No matching source/metric/value in complete snapshots; not proof of absence";
            var unmatched=metrics.Where(m=>!matched.Any(r=>r.GetProperty("Metric").GetString()==m.Metric && r.GetProperty("Value").GetDouble()==m.Value)).ToArray();
            string missingValues=string.Join(", ",unmatched.Take(12).Select(m=>$"{m.Metric}: {m.Value}"))+(unmatched.Length>12 ? $" (+{unmatched.Length-12} more)" : "");
            historical+=$". {unmatched.Length} parsed metric/value pairs have no historical match.";
            string failures=unresolved.TryGetProperty(id,out var u) ? u.GetProperty("reasons").ToString() : "No logged evaluator failure";
            string sample=variants.Length>0 ? variants[0].Value.GetProperty("evidence").ToString() : u.GetProperty("evidence").ToString();
            var interpretations=reasons.Length>0 ? reasons : new[]{"evaluation unresolved"};
            output.Add(new{sid,name,interpretations,sourceEvidence=provenance.Distinct().ToArray(),candidateSources=candidates.Select(c=>new{source=c.Source,id=c.Id}),
                metrics=metrics.Select(m=>new{metric=m.Metric,value=m.Value}),historicalMatches=matched.Length,historical,
                unmatchedValues=unmatched.Select(m=>new{metric=m.Metric,value=m.Value}),failures,sample});
            html.Append($"<tr><td><b>{E(name)}</b><br>SID {sid}<br>{(hasEffect ? effect.GetProperty("count").GetInt32() : 0)} evaluations</td><td>{E(string.Join("; ",interpretations))}<br>{E(string.Join(", ",metrics.Select(m=>m.Metric).Distinct()))}</td><td>{E(string.Join("; ",provenance.Distinct()))}<br>{E(string.Join(", ",candidates))}</td><td>{E(historical)}<details><summary>Unmatched values</summary>{E(missingValues)}</details></td><td>{E(failures)}<details><summary>Evidence location</summary><code>{E(sample)}</code></details></td></tr>");
        }
        html.Append("</tbody></table><script>document.getElementById('filter').oninput=function(){for(const r of document.querySelectorAll('tbody tr'))r.hidden=!r.textContent.toLowerCase().includes(this.value.toLowerCase())}</script>");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);File.WriteAllText(args[2],html.ToString());
        File.WriteAllText(Path.ChangeExtension(args[2],"json"),JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Coverage report: {output.Count} observed state definitions → {args[2]}");
    }
}
