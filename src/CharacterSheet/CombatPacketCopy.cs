using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Collections.Concurrent;

// Only traverses packet DTO members and known collection shapes. Never follows
// Unity/game entity objects, evaluates properties, or retains native references.
internal sealed class CombatPacketCopy
{
    internal readonly Dictionary<string,string> Fields=new();
    internal bool Truncated;
    internal int Errors;
    private readonly long start=Stopwatch.GetTimestamp();
    private static readonly ConcurrentDictionary<Type,MemberInfo[]> members=new();
    private bool Budget(int depth)
    {
        if(depth>5 || Fields.Count>=80 || Stopwatch.GetTimestamp()-start>Stopwatch.Frequency/500)
        {Truncated=true;return false;}return true;
    }
    private void Put(string path,string value)
    {
        if(Fields.Count>=80){Truncated=true;return;}
        if(value.Length>160){value=value[..160];Truncated=true;}
        if(path.Length>120){path=path[..120];Truncated=true;}
        Fields[path]=value;
    }
    internal void Copy(string path,object? value,int depth=0)
    {
        if(!Budget(depth))return;
        try
        {
            if(value==null){Put(path,"null");return;}
            var type=value.GetType();
            if(value is string text){Put(path,text);return;}
            if(type.IsPrimitive || type.IsEnum || value is decimal){Put(path,Convert.ToString(value,CultureInfo.InvariantCulture)??"");return;}
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,Il2CppSystem.Object> map)
            {
                Put(path+".$count",map.Count.ToString());
                var copied=new HashSet<string>();
                foreach(string key in CombatEvidenceModel.IdentityKeys)
                    if(Budget(depth) && map.TryGetValue(key,out var item)){Copy(path+"."+key,item,depth+1);copied.Add(key);}
                foreach(var pair in map)
                {if(!Budget(depth))break;if(copied.Add(pair.Key))Copy(path+"."+pair.Key,pair.Value,depth+1);}
                return;
            }
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,int> ints)
            {
                Put(path+".$count",ints.Count.ToString());
                foreach(var pair in ints){if(!Budget(depth))break;Copy(path+"."+pair.Key,pair.Value,depth+1);}return;
            }
            if(value is Il2CppSystem.Collections.Generic.Dictionary<string,long> longs)
            {
                Put(path+".$count",longs.Count.ToString());
                foreach(var pair in longs){if(!Budget(depth))break;Copy(path+"."+pair.Key,pair.Value,depth+1);}return;
            }
            if(value is Il2CppSystem.Collections.Generic.Dictionary<int,int> integerMap)
            {
                Put(path+".$count",integerMap.Count.ToString());
                foreach(var pair in integerMap){if(!Budget(depth))break;Copy(path+"."+pair.Key,pair.Value,depth+1);}return;
            }
            if(value is Il2CppSystem.Collections.Generic.Dictionary<int,long> numbers)
            {
                Put(path+".$count",numbers.Count.ToString());
                foreach(var pair in numbers){if(!Budget(depth))break;Copy(path+"."+pair.Key,pair.Value,depth+1);}return;
            }
            if(value is Il2CppSystem.Collections.Generic.Dictionary<int,Il2CppSystem.Collections.Generic.Dictionary<int,long>> groups)
            {
                Put(path+".$count",groups.Count.ToString());
                foreach(var pair in groups){if(!Budget(depth))break;Copy(path+"."+pair.Key,pair.Value,depth+1);}return;
            }
            if(value is Il2CppSystem.Collections.Generic.List<int> integers)
            {
                int count=integers.Count;Put(path+".$count",count.ToString());
                for(int i=0;i<Math.Min(count,24) && Budget(depth);i++)Copy(path+"["+i+"]",integers[i],depth+1);
                if(count>24)Truncated=true;
                return;
            }
            if(value is Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.List<int>> rows)
            {
                int count=rows.Count;Put(path+".$count",count.ToString());
                for(int i=0;i<Math.Min(count,24) && Budget(depth);i++)Copy(path+"["+i+"]",rows[i],depth+1);
                if(count>24)Truncated=true;
                return;
            }
            if(type.IsGenericType && type.GetGenericTypeDefinition().FullName=="Il2CppSystem.Collections.Generic.List`1")
            {
                // Do not invoke reflected native generic getters. The C-screen
                // crash was inside the team-report callback's list-copy path.
                Truncated=true;Put(path,"[native list requires typed adapter]");return;
            }
            // Generated IL2CPP structs expose packet fields as managed properties.
            if((type.Namespace=="DYPublic.Duonet" && type.Name.StartsWith("s2cnet")) ||
                type.DeclaringType?.Namespace=="DYPublic.Duonet")
            {
                foreach(var m in members.GetOrAdd(type,t=>t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                    .Where(m=>m is FieldInfo || m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length==0)
                    .OrderBy(m=>m.Name.StartsWith("dProp")?1:0).ToArray()))
                {
                    if(!Budget(depth))break;
                    try{Copy(path+"."+m.Name,m is FieldInfo f?f.GetValue(value):((PropertyInfo)m).GetValue(value),depth+1);}
                    catch{Errors++;}
                }
                return;
            }
            if(value is Il2CppSystem.Object native)
            {
                string name=native.GetIl2CppType().FullName;
                string nativeText=TraceStore.Boxed(native,name);
                if(nativeText.StartsWith("[unmapped"))Truncated=true;
                Put(path,nativeText);return;
            }
            Truncated=true;
            Put(path,"[unsupported "+type.FullName+"]");
        }
        catch{Errors++;Put(path,"[copy-error]");}
    }
}
