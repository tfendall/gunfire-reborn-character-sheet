using UnityEngine;

// Presentation only. One bounded font discovery when the settled C panel opens;
// no game managers, prefab traversal, combat hooks or repeated asset scans.
internal static class StatsTheme
{
    internal static readonly Color Gold = new(0.83f, 0.77f, 0.56f, 1);
    internal static readonly Color Ink = new(0.055f, 0.073f, 0.085f, 1);
    internal static readonly Color Text = new(0.91f, 0.89f, 0.82f, 1);
    internal static readonly Color BrightText = new(0.97f, 0.95f, 0.88f, 1);
    internal static readonly Color MutedText = new(0.66f, 0.71f, 0.72f, 1);
    private static bool fontAttempted;
    private static Font? gameFont, bodyFont, serifFont;
    private static Texture2D? idle, hover, active;
    private static Texture2D? row, badge;
    internal static Texture2D Badge
    {
        get
        {
            if(badge!=null)return badge;
            badge=new Texture2D(48,40,TextureFormat.RGBA32,false) {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            try
            {
                for(int y=0;y<40;y++)for(int x=0;x<48;x++)
                {
                    float dx=Math.Abs(x+0.5f-24)-17,dy=Math.Abs(y+0.5f-20)-13;
                    float distance=MathF.Sqrt(Math.Max(dx,0)*Math.Max(dx,0)+Math.Max(dy,0)*Math.Max(dy,0))+Math.Min(Math.Max(dx,dy),0)-7;
                    var color=distance < -2 ? new Color(0.16f,0.18f,0.13f,1) : new Color(0.44f,0.40f,0.25f,1);
                    color.a=Math.Clamp(0.5f-distance,0,1);badge.SetPixel(x,y,color);
                }
                badge.Apply();return badge;
            }
            catch {UnityEngine.Object.Destroy(badge);badge=null;throw;}
        }
    }
    internal static Texture2D Row
    {
        get
        {
            if(row!=null)return row;
            row=new Texture2D(64,1) { hideFlags=HideFlags.HideAndDontSave };
            try
            {
                for(int x=0;x<64;x++)row.SetPixel(x,0,Color.Lerp(new Color(0.27f,0.25f,0.20f,1),new Color(0.16f,0.17f,0.14f,1),x/63f));
                row.Apply();return row;
            }
            catch { UnityEngine.Object.Destroy(row);row=null;throw; }
        }
    }
    internal static void Numeric(GUIStyle style)
    {
        if (bodyFont != null) style.font = bodyFont;
        style.fontStyle = FontStyle.Normal;
        style.wordWrap = false;
    }

    internal static void Apply(GUIStyle label, GUIStyle heading, GUIStyle number, GUIStyle small, GUIStyle button)
    {
        if (!fontAttempted)
        {
            fontAttempted = true;
            try
            {
                CrashDiagnostics.Begin("stats-font-discovery");
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                var names = new List<string>();
                for (int i = 0; i < Math.Min(fonts.Length, 64); i++)
                {
                    var font = fonts[i];
                    if (font == null) continue;
                    string name = font.name;
                    names.Add(name);
                    if (font.dynamic && name == "Roboto-Regular" && font.HasCharacter('A')) bodyFont = font;
                    if (font.dynamic && name == "NotoSerifSC-Medium" && font.HasCharacter('A')) serifFont = font;
                    // Dynamic source fonts work with IMGUI's requested sizes.
                    // TMP-only static atlases need a different text renderer.
                    if (gameFont == null && font.dynamic && name.Contains("Art_font", StringComparison.OrdinalIgnoreCase) && font.HasCharacter('A')) gameFont = font;
                }
                TraceStore.Write("stats-font", "loaded=" + string.Join(",", names) + "; numbers=" + (bodyFont != null ? bodyFont.name : "fallback") + "; labels=" + (serifFont!=null ? serifFont.name : gameFont!=null ? gameFont.name : "Unity fallback"));
                CrashDiagnostics.Complete("stats-font-discovery");
            }
            catch (Exception ex) { CrashDiagnostics.Error("stats-font-discovery", ex); }
        }
        foreach (var style in new[] { label, heading, number, small, button })
        {
            if (serifFont != null) style.font = serifFont;
            else if (gameFont != null) style.font = gameFont;
            else if (bodyFont != null) style.font = bodyFont;
            style.normal.textColor = Text;
        }
        if (serifFont != null) { heading.font = serifFont; number.font = serifFont; }
        else if (gameFont != null) { heading.font = gameFont; number.font = gameFont; }
        heading.fontStyle = FontStyle.Normal;
        number.fontStyle = FontStyle.Normal;
        heading.normal.textColor = Gold;
        number.normal.textColor = Gold;
        small.normal.textColor = new Color(0.71f, 0.73f, 0.70f, 1);
        idle ??= Fill(new Color(0.15f, 0.16f, 0.13f, 1));
        hover ??= Fill(new Color(0.24f, 0.25f, 0.21f, 1));
        active ??= Fill(new Color(0.31f, 0.28f, 0.20f, 1));
        button.normal.background = idle;
        button.hover.background = hover; button.hover.textColor = Gold;
        button.active.background = active; button.active.textColor = Gold;
        button.onNormal.background = active; button.onNormal.textColor = Gold;
        button.focused.background = hover; button.focused.textColor = Gold;
        button.border = new RectOffset(0, 0, 0, 0);
        button.fontStyle = FontStyle.Normal;
        button.alignment = TextAnchor.MiddleCenter;
        button.onHover.background = active; button.onHover.textColor = Gold;
        button.onActive.background = active; button.onActive.textColor = Gold;
        button.onFocused.background = active; button.onFocused.textColor = Gold;
    }
    private static Texture2D Fill(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.hideFlags = HideFlags.HideAndDontSave;
        try { texture.SetPixel(0, 0, color); texture.Apply(); return texture; }
        catch { UnityEngine.Object.Destroy(texture); throw; }
    }
    internal static void Dispose()
    {
        foreach (var texture in new[] { idle, hover, active, row, badge })
            if (texture != null) UnityEngine.Object.Destroy(texture);
        idle = hover = active = row = badge = null;
    }
}
