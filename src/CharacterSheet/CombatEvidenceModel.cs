using System.Globalization;

internal sealed record CombatEvidenceRecord(long Sequence, double Time, int Player, int Epoch, int Scene,
    string Hook, string Group, Dictionary<string,string> Fields, bool Truncated, int CopyErrors);
internal sealed record CombatEntity(int Id, long Generation, int Scene, string Origin,
    Dictionary<string,string> Properties);
internal sealed record CombatEvidenceResult(CombatEvidenceRecord Event, CombatEntity? Subject,
    CombatEntity? Attacker, CombatEntity? Victim, Dictionary<string,decimal> PropertyDeltas,
    long QueueDrops, long EntityEvictions);

// All evidence is observational. Entity metadata must never grant damage credit.
internal sealed class CombatEvidenceModel
{
    private readonly object gate=new();
    private readonly Queue<CombatEvidenceRecord> queue=new();
    private readonly Dictionary<int,CombatEntity> entities=new();
    private long sequence,drops,seenDrops,evictions,generation;
    internal long Drops {get {lock(gate)return drops;}}
    internal int Pending {get {lock(gate)return queue.Count;}}
    internal static readonly string[] IdentityKeys={"SID","Sid","ID","Name","Owner","ClientOwner","Side","Type","FightType","HP","HPMax","Shield","ShieldMax","Armor","ArmorMax"};
    internal void Add(double time,int player,int epoch,int scene,string hook,string group,
        Dictionary<string,string> fields,bool truncated=false,int errors=0)
    {
        lock(gate)
        {
            long id=++sequence;
            if(queue.Count>=4096){drops++;return;}
            queue.Enqueue(new(id,time,player,epoch,scene,hook,group,new(fields),truncated,errors));
        }
    }
    private CombatEntity? Get(int id,int scene) => entities.TryGetValue(id,out var value) && value.Scene==scene ? value : null;
    internal CombatEvidenceResult[] Drain(int maximum=64)
    {
        lock(gate)
        {
            // Losing lifecycle/property events invalidates inferred continuity.
            if(seenDrops!=drops){entities.Clear();seenDrops=drops;}
            var result=new List<CombatEvidenceResult>();
            while(result.Count<maximum && queue.Count>0)
            {
                var e=queue.Dequeue();
                int Number(params string[] keys)
                {
                    foreach(var key in keys)
                        if(e.Fields.TryGetValue(key,out var s) && int.TryParse(s,out int n))return n;
                    return 0;
                }
                int id=Number("data.iMonster","data.iSummon","data.iDevice","data.iObstacle","data.iPet","data.iHero","data.iServant","data.iNpc","data.iTarget","pid");
                int attacker=Number("data.iAttack"),victim=Number("data.iVictim");
                var delta=new Dictionary<string,decimal>();
                if(e.Group=="clear")entities.Clear();
                var old=Get(id,e.Scene);
                var subject=old;
                if(id!=0 && (e.Group=="birth" || e.Group=="property"))
                {
                    var props=e.Group=="birth" || old==null ? new Dictionary<string,string>() : new(old.Properties);
                    foreach(string key in IdentityKeys)
                    {
                        string? value=null;
                        if(e.Fields.TryGetValue("data.dPropInfo."+key,out var a))value=a;
                        if(e.Fields.GetValueOrDefault("attrname")==key)value=e.Fields.GetValueOrDefault("attrValue");
                        if(value==null)continue;
                        if(e.Group!="birth" && props.TryGetValue(key,out var before) &&
                            decimal.TryParse(before,NumberStyles.Float,CultureInfo.InvariantCulture,out var b) &&
                            decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var v))delta[key]=v-b;
                        props[key]=value;
                    }
                    if(e.Group=="birth")
                        foreach(string key in new[]{"iType","iOwner","iClientOwner","Prefab"})
                            if(e.Fields.TryGetValue("data."+key,out var raw))props["packet."+key]=raw;
                    subject=new(id,e.Group=="birth" || old==null?++generation:old.Generation,e.Scene,
                        e.Group=="birth"?e.Hook:old?.Origin??"property-only",props);
                    if(!entities.ContainsKey(id) && entities.Count>=2048){entities.Remove(entities.Keys.First());evictions++;}
                    entities[id]=subject;
                }
                result.Add(new(e,subject,Get(attacker,e.Scene),Get(victim,e.Scene),delta,drops,evictions));
                if(e.Group=="remove")entities.Remove(id);
            }
            return result.ToArray();
        }
    }
}

// Limits callback copying, not merely the size of the downstream queue.
internal sealed class CombatCaptureGate
{
    private readonly object gate=new();
    private double start=double.NegativeInfinity;
    private int calls;
    internal bool Accept(double time)
    {
        lock(gate)
        {
            if(time<start || time-start>=1){start=time;calls=0;}
            return ++calls<=400;
        }
    }
}
