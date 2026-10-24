internal static class HeadlineBonuses
{
    internal static bool Supports(string metric)=>BuildSheetModel.MainMetrics.Contains(metric) && metric!="Critical Multiplier (multiplier)";
    internal static BonusObservation[] Observe(IEnumerable<BuildContribution> source,IEnumerable<CoverageFinding> findings)
    {
        var rows=source.ToArray();var owners=findings.ToArray();
        return BuildSheetModel.MainMetrics.Where(Supports).Select(metric=>
        {
            var included=rows.Where(r=>r.Metric==metric && r.Layer!=ModifierLayer.Independent && r.Condition!=ModifierCondition.Scoped && !r.Scope.StartsWith("Target:")).ToArray();
            bool partial=owners.Any(owner=>
            {
                if(!ModifierCatalog.Entries.TryGetValue((owner.Source,owner.Id,owner.Level),out var definition))return true;
                string description=definition.Description;
                // A primary-only bonus is not a missing general Skill Damage contribution.
                if(metric=="Skill DMG buffs")description=description.Replace("Primary Skill","Primary Ability").Replace("Secondary Skill","Secondary Ability");
                if(!BuildSheetModel.Relevant(metric,description,owner.Source))return false;
                return owner.Status!=ModifierCoverage.Calculated && !included.Any(r=>r.SourceKey==owner.Source && r.SourceId==owner.Id && r.Condition==ModifierCondition.BuffPresence);
            });
            return new BonusObservation("total:"+metric,BuildSheetModel.Name(metric),metric,
                included.Length==0 && (partial || owners.Length==0) ? null : included.Sum(r=>r.Value),!partial);
        }).ToArray();
    }
    internal static BonusWindowResult? Find(string metric,IEnumerable<BonusWindowResult> history)=>history.FirstOrDefault(r=>r.Key=="total:"+metric);
    internal static string Value(string metric,IEnumerable<BonusWindowResult> history)
    {
        var row=Find(metric,history);
        return row?.Average is double value ? BuildSheetModel.Value(metric,value)+(row.Partial || row.Coverage<99.5 ? "*" : "") : "—";
    }
    internal static string Detail(string metric,IEnumerable<BonusWindowResult> history)
    {
        var row=Find(metric,history);
        return row==null ? "No combat observations yet." : $"{row.Seconds:0.#}s combat average · {row.Coverage:0}% of time observed"+
            (row.Partial ? "\n* Known subtotal; unresolved effects are excluded." : row.Coverage<99.5 ? "\n* Some combat time was not observed." : "")+
            (metric.Contains("Skill Base DMG") ? "\nBase bonus; separate from additive skill damage." : "");
    }
}

