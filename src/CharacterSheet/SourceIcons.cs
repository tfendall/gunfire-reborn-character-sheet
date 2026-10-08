using UnityEngine;
// Small locally drawn source symbols; no game asset discovery or native UI reads.
internal static class SourceIcons
{
    private static readonly Dictionary<string,Texture2D> icons=new();
    internal static bool Supports(string source)=>source is "Weapon" or "Weapon inscription" or "Scroll" or "Soul Jade" or "Soul Pendant" or "Ascension" or "Blessing";
    internal static Texture2D Get(string source)
    {
        if(icons.TryGetValue(source,out var icon) && icon!=null)return icon;
        icon=new Texture2D(16,16);
        icon.hideFlags=HideFlags.HideAndDontSave;
        icon.filterMode=FilterMode.Point;
        Color ink=source switch {"Soul Jade"=>new(0.30f,0.80f,0.72f),"Soul Pendant"=>new(0.70f,0.53f,0.88f),"Blessing"=>new(0.25f,0.75f,0.85f),_=>StatsTheme.Gold};
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)
        {
            bool on=source switch {
                "Soul Jade"=>Math.Abs(x-7)+Math.Abs(y-7)<=6,
                "Blessing"=>(x-7)*(x-7)+(y-7)*(y-7)<=36 && (x-7)*(x-7)+(y-7)*(y-7)>=15,
                "Ascension"=>Math.Abs(x-7)==Math.Abs(y-10) && y>=4 && y<=10 || x>=6&&x<=8&&y>=2&&y<=9,
                "Weapon"=>y>=8&&y<=11&&x>=2&&x<=13 || x>=4&&x<=7&&y>=3&&y<=8,
                "Weapon inscription"=>Math.Abs(x-y)<=1&&x>=3&&x<=12 || y==2&&x>=2&&x<=11,
                "Soul Pendant"=>Math.Abs(x-7)+Math.Abs(y-6)<=5 || x==7&&y>=10&&y<=14,
                _=>x>=3&&x<=12&&y>=2&&y<=13&&(x==3||x==12||y==2||y==13||y==6||y==9)
            };
            icon.SetPixel(x,y,on ? ink : Color.clear);
        }
        icon.Apply();icons[source]=icon;return icon;
    }
    internal static void Dispose(){foreach(var icon in icons.Values)UnityEngine.Object.Destroy(icon);icons.Clear();}
}
