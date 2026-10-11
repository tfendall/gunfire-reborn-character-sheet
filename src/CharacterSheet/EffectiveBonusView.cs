internal sealed record EffectiveBonusRow(int Id,string Name,string Text,double Checked,bool Resolved,string Source="",int SourceId=0);

// Managed UI snapshot only; rendering never dereferences game objects.
internal sealed class EffectiveBonusView
{
    private readonly Dictionary<int,EffectiveBonusRow> rows=new();
    internal void Set(int id,string name,string text,double now,bool resolved=true,string source="",int sourceId=0)
        => rows[id]=new(id,name,text,now,resolved,source,sourceId);
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
