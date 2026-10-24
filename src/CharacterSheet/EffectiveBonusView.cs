internal sealed record EffectiveBonusRow(int Id,string Name,string Text,double Checked,bool Resolved,string Source="",int SourceId=0,double MaxAge=5,int StateSid=0);

// Managed UI snapshot only; rendering never dereferences game objects.
internal sealed class EffectiveBonusView
{
    internal static string? ResolutionBlock(int sid)=>sid==1236 ? "unverified-field-support-inputs" : null;
    // Verified in the October 13 run: state 32724 is the owned Oppressor blessing's damage boost.
    internal static (string Source,int Id) SourceFor(int sid,string source,int id)
        => source.Length==0 && sid==32724 ? ("blessing",13514) : (source,id);
    private readonly Dictionary<int,EffectiveBonusRow> rows=new();
    internal void Set(int id,string name,string text,double now,bool resolved=true,string source="",int sourceId=0,double maxAge=5,int stateSid=0)
        => rows[id]=new(id,name,text,now,resolved,source,sourceId,maxAge,stateSid);
    internal void Touch(int id,double now)
    {if(rows.TryGetValue(id,out var row))rows[id]=row with {Checked=now};}
    internal void Remove(int id)=>rows.Remove(id);
    internal void Clear()=>rows.Clear();
    internal EffectiveBonusRow[] Snapshot()=>rows.Values.OrderBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.Id).ToArray();
    internal static string Display(EffectiveBonusRow row,double now)
    {
        if(!row.Resolved)return "Current value unavailable.";
        double age=Math.Max(0,now-row.Checked);
        return age<=2 ? row.Text : row.Text+"\nLast checked "+Math.Floor(age).ToString("0")+"s ago.";
    }
}
