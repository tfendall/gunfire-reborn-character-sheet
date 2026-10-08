using UnityEngine;
// Use the game's sprite loader to read its atlas artwork. One request per 250ms
// while the stats screen is open; never load assets inside rendering or combat.
internal static class GameItemArtwork
{
    private static readonly Dictionary<string,bool> resolved=new();
    private static readonly Queue<(string Key,string Path)> pending=new();
    private static readonly HashSet<string> requested=new();
    private static readonly Dictionary<string,IntPtr> roots=new();
    private static readonly Dictionary<string,Texture2D> artwork=new();
    private static float next;
    private static bool suspended;
    internal static void Clear()
    {
        foreach(var image in artwork.Values) { try { if(image!=null)UnityEngine.Object.Destroy(image); } catch { } }
        foreach(var root in roots.Values)if(root!=IntPtr.Zero)Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_free(root);
        roots.Clear();artwork.Clear();resolved.Clear();requested.Clear();pending.Clear();
    }
    private static string? Default(string source)
    {
        string? path=source switch {
            "Scroll"=>"Res/Atlas/Common/Common/img_blackrelic",
            "Blessing"=>"Res/Atlas/Benediction/Benediction/13533",
            "Ascension"=>"Res/Atlas/Talent3/Talent3/2216",

            _=>null
        };
        if(path==null)return null;
        string key="default:"+source;
        if(resolved.TryGetValue(key,out bool valid))return valid ? key : null;
        if(requested.Add(key))pending.Enqueue((key,path));
        return null;
    }
    private static readonly Dictionary<int,int> heroAtlases=new(){{20,2},{21,1},{22,3},{23,4},{27,5},{29,6},{30,7},{31,8},{32,9},{33,10},{35,11},{36,12},{37,13},{38,14}};
    internal static string? Get(ContributorTile item)
    {
        if(item.IconId==0)return null;
        string key=item.IconSource+":"+item.IconId;
        if(resolved.TryGetValue(key,out bool valid))return valid ? key : null;
        if(item.IconSource=="gem" && !GemArtworkIds.TryGet(item.IconId,out _))return null;
        if(!requested.Add(key))return null;
        if(item.IconSource=="gem")
        {
            // Resolve configuration IDs only in the throttled artwork poll.
            GemArtworkIds.TryGet(item.IconId,out int gemItem);
            pending.Enqueue((key,"gem:"+gemItem));return null;
        }
        string? atlas=item.IconSource switch {
            "weapon"=>"Weaponicon",
            "blessing"=>"Benediction",
            "scroll"=>"Relic",
            "ascension"=>heroAtlases.TryGetValue(item.IconId/100,out int hero) ? "Talent"+hero : null,
            _=>null
        };
        if(atlas==null){resolved[key]=false;return null;}
        pending.Enqueue((key,$"Res/Atlas/{atlas}/{atlas}/{item.IconId}"));
        return null;
    }
    internal static void Poll()
    {
        if(!LoadingGuard.DisplayReady)
        {if(!suspended){Clear();suspended=true;}return;}
        suspended=false;
        if(!LoadingGuard.DisplayReady || !StatsScreen.PackageOpen || Time.unscaledTime<next || pending.Count==0)return;
        next=Time.unscaledTime+0.25f;var item=pending.Dequeue();
        try
        {
            CrashDiagnostics.Reading("item-artwork");
            string path=item.Path;
            if(path.StartsWith("gem:",StringComparison.Ordinal))
            {
                // Use tables already initialized by the game, never force a
                // singleton startup or query live season/run-state objects.
                var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                var gemTable=typeof(DataHelper.S8GemItemData).BaseType?.GetProperty("s_instance",flags)?.GetValue(null) as DataHelper.S8GemItemData;
                var iconTable=typeof(DataHelper.IconData).BaseType?.GetProperty("s_instance",flags)?.GetValue(null) as DataHelper.IconData;
                int gemItem=int.Parse(path[4..]);
                if(gemTable==null || iconTable==null)throw new InvalidOperationException($"Soul gem icon tables unavailable: {item.Key}; item={gemItem}; gemTable={gemTable!=null}; iconTable={iconTable!=null}");
                var gem=gemTable.GetGemData(gemItem);
                if(gem==null)throw new InvalidOperationException($"Soul gem item definition unavailable: {item.Key}; item={gemItem}");
                int iconId=gem.IconID;
                path=iconTable.GetIconData(iconId)?.IconPath??"";
                if(string.IsNullOrEmpty(path))throw new InvalidOperationException($"Soul gem icon path unavailable: {item.Key}; item={gemItem}; icon={iconId}");
                TraceStore.Write("gem-artwork-mapping",$"ability={item.Key}; item={gemItem}; icon={iconId}; path={path}");
            }
            var sprite=GameCoder.Engine.DYResourceManager.LoadSubSpriteInAtlas(path);
            string name=sprite!=null ? sprite.name : "not available";
            if(sprite!=null)
            {
                var image=Copy(sprite);
                IntPtr handle=Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_new(image.Pointer,false);
                artwork[item.Key]=image;roots[item.Key]=handle;resolved[item.Key]=true;
            }
            else resolved[item.Key]=false;
            TraceStore.Write("item-artwork-"+item.Key,path+" => "+name+"; ownedBitmap="+resolved[item.Key]);
            CrashDiagnostics.ReadDone("item-artwork");
        }
        catch(Exception error){resolved[item.Key]=false;CrashDiagnostics.Error("item-artwork",error);}
    }
    private static Texture2D Copy(Sprite sprite)
    {
        IntPtr spriteRoot=Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_new(sprite.Pointer,false);
        IntPtr textureRoot=IntPtr.Zero;
        RenderTexture? target=null;Texture2D? image=null;
        var previous=RenderTexture.active;
        try
        {
            var atlas=sprite.texture;if(atlas==null)throw new InvalidOperationException("Atlas texture unavailable.");
            textureRoot=Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_new(atlas.Pointer,false);
            var region=sprite.textureRect;
            target=RenderTexture.GetTemporary(96,96,0,RenderTextureFormat.ARGB32);
            Graphics.Blit(atlas,target,new Vector2(region.width/atlas.width,region.height/atlas.height),new Vector2(region.x/atlas.width,region.y/atlas.height));
            RenderTexture.active=target;
            image=new Texture2D(96,96,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp };
            image.ReadPixels(new Rect(0,0,96,96),0,0,false);image.Apply(false,false);
            return image;
        }
        catch { if(image!=null)UnityEngine.Object.Destroy(image);throw; }
        finally
        {
            RenderTexture.active=previous;
            if(target!=null)RenderTexture.ReleaseTemporary(target);
            if(spriteRoot!=IntPtr.Zero)Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_free(spriteRoot);
            if(textureRoot!=IntPtr.Zero)Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_free(textureRoot);
        }
    }
    internal static bool Draw(Rect rect,ContributorTile item)
    {
        string? key=Get(item)??Default(item.Source);if(key==null)return false;
        try
        {
            if(!artwork.TryGetValue(key,out var image))return false;
            // Reconstruct the managed proxy from our strong native root. The
            // game's temporary clone/wrapper never participates in rendering.
            var pointer=Il2CppInterop.Runtime.IL2CPP.il2cpp_gchandle_get_target(roots[key]);
            if(pointer==IntPtr.Zero)throw new InvalidOperationException("Owned icon native root lost.");
            var proxy=new Texture2D(pointer);
            GUI.DrawTexture(rect,proxy);return true;
        }
        catch(Exception error){TraceStore.Write("item-artwork-draw-"+key,error.ToString());return false;}
    }
}
