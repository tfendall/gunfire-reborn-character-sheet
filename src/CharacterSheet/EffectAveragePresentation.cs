using System.Text.RegularExpressions;

internal static class EffectAveragePresentation
{
    private static string Normalize(string name)=>Regex.Replace(Regex.Replace(name,@" · slot \d+",""),@" · \d+ rounds$","");
    internal static BonusWindowResult? Find(string metric,string name,string source,int id,IEnumerable<BonusWindowResult> history)
    {
        if(metric=="lsc")metric="Lucky Shot Chance";
        // Independent rows carry a display index; history retains the underlying metric.
        metric=Regex.Replace(metric,@":\d+$","");
        if((name.Contains(" (combined · ") || name.EndsWith(" (retained status)")) && source.Length>0 && id!=0 && metric!="Lucky Shot Chance")
            return history.FirstOrDefault(r=>r.Key==source+":"+id+":0:"+metric);
        var matches=history.Where(r=>!r.Key.StartsWith("total:") && r.Metric==metric && (source=="weapon" && metric=="Lucky Shot Chance"
            ? r.Key.StartsWith("luck:Weapon:") : source.Length>0 && id!=0 && metric!="Lucky Shot Chance"
                ? r.Key.StartsWith(source+":"+id+":") : Normalize(r.Name)==Normalize(name))).ToArray();
        return matches.Length==1 ? matches[0] : null;
    }
    internal static string Summary(BonusWindowResult? result)=>result==null ? "30s average unknown · No measured contribution" :
        "30s "+(result.Basis=="hits" ? "hit-weighted" : "combat-time")+" average · "+result.Coverage.ToString("0")+"%"+
        (result.Basis=="hits" ? " of "+result.Hits+" hits observed" : " of time observed over "+result.Seconds.ToString("0.0")+"s");
    internal static ContributorTile Apply(string metric,ContributorTile tile,IEnumerable<BonusWindowResult> history)
    {
        var result=Find(metric,tile.Name,tile.IconSource,tile.IconId,history);
        string value=result?.Average is double avg ? BuildSheetModel.Value(result.Metric,avg) : "—";
        return tile with {Value=value,Description=Summary(result)+"\n"+
            "Current / listed value: "+tile.Value+"\n\n"+tile.Description};
    }
}
