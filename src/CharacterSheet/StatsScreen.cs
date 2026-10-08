using UnityEngine;

internal static class StatsScreen
{
    internal static bool PackageOpen;
    internal static bool MenuBusy;
    private static float resumeReadsAfter;
    internal static bool ReadsAllowed => !MenuBusy && Time.unscaledTime >= resumeReadsAfter;
    private static bool matrixView = true;
    private static readonly HashSet<string> expandedSources = new() { "Weapon damage" };
    private static float measuredDamageHeight = 400;
    private static readonly HashSet<string> expandedBonuses = new();
    private static readonly Dictionary<string,string> chosenTiles = new();
    private static List<BuildContribution>? sheetSource;
    private static SheetBonus[] sheet = Array.Empty<SheetBonus>();
    private static GUIStyle? rowButton, valueStyle, detailValue, buildLabel, buildValue, badgeValue;
    private static GUIStyle? label, heading, number, small, button;
    private static Vector2 scroll;
    private static Rect panelBounds;
    private static float measuredBuildHeight = 360;
    private static GUIStyle? matrixNumber, detailHeading, elementLabel, legendLabel;
    private static GUIStyle? damageAmount, damageShare, columnHeading, columnValue;
    private static GameObject? inputCanvas;
    private static RectTransform? inputArea;
    private static readonly PanelSafety safety = new();
    internal static void SuspendForLoading()
    {
        PackageOpen = false; safety.SetOpen(false); DisableInput();
    }
    internal static void Poll()
    {
        bool shown = LoadingGuard.DisplayReady && Panel.LocalPlayerId != 0 && Panel.RunActive && Input.GetKey(KeyCode.C);
        if (shown != PackageOpen) TraceStore.Marker(shown ? "c-stats-open" : "c-stats-close");
        PackageOpen = shown;
        // Core Unity input only. No game UI-manager or scene-manager reads.
        bool busy = Cursor.visible && !shown;
        if (busy || busy != MenuBusy) resumeReadsAfter = Time.unscaledTime + 0.75f;
        if (busy != MenuBusy) TraceStore.Marker("collection-reads-" + (busy ? "paused" : "resume-pending") + " cursor-visible=" + Cursor.visible);
        MenuBusy = busy;
        safety.SetOpen(shown);
        UpdateInputSurface();
        GameItemArtwork.Poll();
    }
    private static void UpdateInputSurface()
    {
        GameObject? candidate = null;
        GameObject? child = null;
        try
        {
            if (!safety.CanBlockInput || !Cursor.visible) { inputCanvas?.SetActive(false); return; }
            if (inputCanvas == null)
            {
                CrashDiagnostics.Begin("stats-input-surface-create");
                // A transparent UGUI surface prevents clicks on the IMGUI panel
                // from also selecting/dragging inventory items behind it.
                candidate = new GameObject("Gunfire LSC input surface");
                candidate.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(candidate);
                var canvas = candidate.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32760;
                candidate.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                child = new GameObject("Input area", new Il2CppSystem.Type[] { Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>() });
                child.transform.SetParent(candidate.transform, false);
                var rect = child.GetComponent<RectTransform>();
                var image = child.AddComponent<UnityEngine.UI.Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                if (rect == null) throw new InvalidOperationException("Input-area RectTransform unavailable.");
                // Publish only after every component has initialized successfully.
                inputArea = rect;
                inputCanvas = candidate;
                candidate = null; child = null;
                CrashDiagnostics.Complete("stats-input-surface-create");
            }
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 3f);
            inputArea!.anchorMin = inputArea.anchorMax = new Vector2(1, 1);
            inputArea.pivot = new Vector2(1, 1);
            inputArea.anchoredPosition = new Vector2(panelBounds.xMax - Screen.width, -panelBounds.y);
            inputArea.sizeDelta = new Vector2(panelBounds.width, panelBounds.height);
            inputCanvas.SetActive(true);
        }
        catch (Exception ex)
        {
            safety.Fail();
            DisableInput();
            DestroyQuietly(child);
            DestroyQuietly(candidate);
            Dispose();
            CrashDiagnostics.Error("stats-input-surface", ex);
            TraceStore.Write("stats-input-surface-error", ex.Message + "; disabled until package screen is reopened");
        }
    }
    private static void DisableInput()
    {
        try { inputCanvas?.SetActive(false); } catch { }
    }
    private static void DestroyQuietly(GameObject? obj)
    {
        try { if (obj != null) { obj.SetActive(false); UnityEngine.Object.Destroy(obj); } } catch { }
    }
    internal static void DrawSucceeded()
    {
        safety.DrawSucceeded();
        if (safety.CanBlockInput && inputCanvas == null) UpdateInputSurface();
    }
    internal static void DrawFailed(Exception error)
    {
        safety.Fail(); DisableInput();
        // Clear partially created style state too, so reopening can retry cleanly.
        label = heading = number = small = button = null;
        TraceStore.Write("stats-screen-draw-error", error.Message + "; input disabled until package screen is reopened");
    }
    internal static void Dispose()
    {
        DisableInput();
        DestroyQuietly(inputCanvas);
        inputCanvas = null; inputArea = null;
        StatsTheme.Dispose();
        SourceIcons.Dispose();
        GameItemArtwork.Clear();
        label = heading = number = small = button = null;
    }
    private static string Percent(double? value) => value.HasValue ? value.Value.ToString("F1") + "%" : "--";
    internal static void Draw()
    {
        if (!safety.CanDraw) return;
        CrashDiagnostics.Begin("stats-panel-render");
        float s = Mathf.Clamp(Screen.height / 1080f, 0.65f, 3f);
        float width = PanelWidth(s), x = Screen.width - width - 18 * s, y = 95 * s;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            heading = new GUIStyle(label) { fontStyle = FontStyle.Normal };
            number = new GUIStyle(heading);
            small = new GUIStyle(label);
            button = new GUIStyle();
            StatsTheme.Apply(label, heading, number, small, button);
            rowButton = new GUIStyle();
            valueStyle = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            valueStyle.normal.textColor = StatsTheme.Gold;
            detailValue = new GUIStyle(small) { alignment = TextAnchor.UpperRight, wordWrap = false };
            valueStyle.wordWrap = false;
            buildLabel = new GUIStyle(label) { alignment=TextAnchor.MiddleLeft };
            buildValue = new GUIStyle(valueStyle) { alignment=TextAnchor.MiddleRight };
            badgeValue = new GUIStyle(detailValue ?? small) { alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset(0,0,0,0) };
            matrixNumber = new GUIStyle(detailValue);
            matrixNumber.normal.textColor = StatsTheme.BrightText;
            damageAmount = new GUIStyle(matrixNumber);
            damageAmount.normal.textColor = StatsTheme.MutedText;
            damageShare = new GUIStyle(valueStyle);
            damageShare.normal.textColor = StatsTheme.BrightText;
            columnHeading = new GUIStyle(small) { wordWrap=false };
            columnHeading.normal.textColor = StatsTheme.Gold;
            columnValue = new GUIStyle(matrixNumber);
            columnValue.normal.textColor = StatsTheme.Gold;
            elementLabel = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, padding=new RectOffset(0,0,0,0), margin=new RectOffset(0,0,0,0), contentOffset=Vector2.zero };
            elementLabel.normal.textColor = StatsTheme.BrightText;
            legendLabel = new GUIStyle(elementLabel) { wordWrap = false };
            legendLabel.normal.textColor = StatsTheme.MutedText;
            detailHeading = new GUIStyle(small);
            detailHeading.normal.textColor = StatsTheme.Gold;
            foreach (var style in new[] { number, valueStyle, detailValue, matrixNumber, buildValue, badgeValue, damageAmount, damageShare, columnValue }) StatsTheme.Numeric(style!);
        }
        label.fontSize = (int)(17 * s); heading!.fontSize = (int)(18 * s);
        number!.fontSize = (int)(30 * s); small!.fontSize = (int)(14 * s); button!.fontSize = (int)(13 * s);
        valueStyle!.fontSize = (int)(20 * s);
        buildLabel!.fontSize=(int)(15*s);buildValue!.fontSize=(int)(17*s);
        badgeValue!.fontSize=(int)(12*s);
        detailValue!.fontSize = (int)(14 * s);
        matrixNumber!.fontSize = (int)(14 * s);
        detailHeading!.fontSize = (int)(14 * s);
        elementLabel!.fontSize = (int)(13 * s);
        damageAmount!.fontSize=(int)(14*s);damageShare!.fontSize=(int)(20*s);
        columnHeading!.fontSize=(int)(11*s);columnValue!.fontSize=(int)(11*s);
        Color oldColor = GUI.color; int oldDepth = GUI.depth;
        GUI.depth = -100;
        try
        {
            float desired = matrixView ? MatrixHeight(s) : measuredBuildHeight + 150 * s;
            float height = Math.Min(Math.Max(340*s,desired), Screen.height - y - 150*s);
            panelBounds = new Rect(x,y,width,height);
            GUI.color = new Color(0.43f, 0.39f, 0.27f, 1);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = new Color(0.095f,0.105f,0.10f,1);
            GUI.DrawTexture(new Rect(x + 1, y + 1, width - 2, height - 2), Texture2D.whiteTexture);
            GUI.color = StatsTheme.Gold;
            GUI.DrawTexture(new Rect(x, y, width, 3 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.14f,0.15f,0.13f,1);
            GUI.DrawTexture(new Rect(x + 1, y + 1, width - 2, 48 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 20 * s, y + 18 * s, width - (matrixView ? 120 : 150) * s, 28 * s), "BUILD OVERVIEW", heading);
            GUI.color = new Color(0.95f, 0.79f, 0.44f, 1);

            GUI.color = Color.white;

            if(matrixView)
            {
                if (GUI.Button(new Rect(x+width-78*s,y+14*s,58*s,28*s),"Reset",button!) && Cursor.visible) Panel.ResetStats();
            }
            else if(GUI.Button(new Rect(x+width-118*s,y+14*s,98*s,28*s),"Collapse all",button!) && Cursor.visible)expandedBonuses.Clear();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled;
            try
            {
                if (GUI.Toggle(new Rect(x + 20 * s, y + 65 * s, (width - 50 * s) / 2, 34 * s), matrixView, "Damage", button) && Cursor.visible && !matrixView)
                { matrixView = true; scroll = Vector2.zero; }
                if (GUI.Toggle(new Rect(x + 30 * s + (width - 50 * s) / 2, y + 65 * s, (width - 50 * s) / 2, 34 * s), !matrixView, "Build", button) && Cursor.visible && matrixView)
                { matrixView = false; scroll = Vector2.zero; }
                if (matrixView)
                {
                    GUI.Label(new Rect(x+20*s,y+120*s,width-40*s,48*s),Compact(DamageCapture.Ledger.Total)+" DMG",number!);
                    DrawMatrix(x,y+180*s,width,height-205*s,s);
                }
                else DrawBuild(x, y + 125 * s, width, height - 150 * s, s);
            }
            finally { GUI.enabled = wasEnabled; }
            CrashDiagnostics.Complete("stats-panel-render");
        }
        finally { GUI.color = oldColor; GUI.depth = oldDepth; }
    }
    private static string CleanName(LscRow row)
    {
        if (row.Source == "Weapon") return "Equipped weapon";
        return System.Text.RegularExpressions.Regex.Replace(row.Name, @" · slot \d+", "");
    }
    private static void DrawBuild(float x, float y, float width, float height, float s)
    {
        if (!ReferenceEquals(sheetSource, BuildBonuses.Rows))
        { sheetSource = BuildBonuses.Rows; sheet = BuildSheetModel.Stable(sheetSource); }
        float contentWidth = width - 62 * s;
        // Lay out the same entries before painting, using actual wrapped text
        // heights. This keeps longer ability names/contributors from clipping.
        var blocks = new List<(float Y, float H, string Name, string Value, string Key, bool Section, bool Detail)>();
        float cursor = 0;
        var tileSets=new Dictionary<string,ContributorTile[]>();
        void Section(string text)
        {
            if(blocks.Count>0)cursor+=6*s;
            blocks.Add((cursor,26*s,text,"","",true,false));cursor+=26*s;
        }
        void Detail(string text, string value)
        {
            float h = Math.Max(22 * s, small!.CalcHeight(new GUIContent(text), contentWidth - 2*PanelSpacing.ContentInset*s - PanelSpacing.NumberWidth*s) + PanelSpacing.DetailGap*s);
            blocks.Add((cursor,h,text,value,"",false,true)); cursor += h;
        }
        void Bonus(string key, string text, string value, BuildContribution[] items)
        {
            var contributors=ContributorTiles.Create(key,items,BaseLsc.Rows,ModifierRuntime.Findings,Panel.CurrentWeaponSid);
            tileSets[key]=contributors;
            float nameWidth=contentWidth-(PanelSpacing.RowInset+PanelSpacing.BuildValueWidth+PanelSpacing.BadgeGap+PanelSpacing.BadgeWidth+12+28)*s;
            float h=Math.Max(32*s,buildLabel!.CalcHeight(new GUIContent(text),nameWidth)+8*s);
            blocks.Add((cursor,h,text,value,key,false,false)); cursor += h;
            if (!expandedBonuses.Contains(key)) return;
            int backgroundIndex=blocks.Count;float expandedTop=cursor;blocks.Add((cursor,0,"","","expanded",false,true));cursor+=PanelSpacing.ExpandedPadding*s;
            if(key=="lsc")
            {
                const string note="Rolling average of recent weapon hits. Changes as damage is dealt.";
                float noteHeight=small!.CalcHeight(new GUIContent(note),contentWidth-2*PanelSpacing.ContentInset*s)+12*s;
                blocks.Add((cursor,noteHeight,note,"","body",false,true));cursor+=noteHeight;
            }
            var tiles=contributors;
            if(tiles.Length==0)Detail("No contributors","");
            else
            {
                int cols=PanelSpacing.Columns(contentWidth/s);
                int rows=(tiles.Length+cols-1)/cols;
                blocks.Add((cursor,rows*58*s,"","","tiles:"+key,false,true));cursor+=rows*58*s+16*s;
                string selected=chosenTiles.GetValueOrDefault(key,"");
                var tile=tiles.FirstOrDefault(t=>t.Source+":"+t.Name==selected)??tiles[0];
                float titleHeight=Math.Max(26*s,label!.CalcHeight(new GUIContent(tile.Name),contentWidth-2*PanelSpacing.ContentInset*s-PanelSpacing.NumberWidth*s)+6*s);
                blocks.Add((cursor,titleHeight,tile.Name,tile.Value,"title",false,true));cursor+=titleHeight;
                blocks.Add((cursor,22*s,tile.Source,"","source",false,true));cursor+=28*s;
                if(tile.Description.Length>0)
                {
                    float bodyHeight=small!.CalcHeight(new GUIContent(tile.Description),contentWidth-2*PanelSpacing.ContentInset*s)+16*s;
                    blocks.Add((cursor,bodyHeight,tile.Description,"","body",false,true));cursor+=bodyHeight;
                }
            }
            cursor+=PanelSpacing.ExpandedPadding*s;
            blocks[backgroundIndex]=(expandedTop,cursor-expandedTop,"","","expanded",false,true);
            cursor+=PanelSpacing.RowGap*s;
        }
        var displayRows=new[]{new SheetBonus("lsc","WEAPON & LUCK","Lucky Shot Chance",Percent(DamageCapture.WeaponLsc),Array.Empty<BuildContribution>())}.Concat(sheet);
        foreach(var family in displayRows.GroupBy(BuildSheetModel.Family).OrderBy(group=>BuildSheetModel.FamilyRank(group.Key)).ThenBy(group=>group.Key))
        {
            Section(family.Key);
            foreach(var bonus in family)
                if(bonus.Key=="Critical Multiplier (multiplier)")
                    Bonus(bonus.Key,"CritX",BuildBonuses.CritX.HasValue ? BuildBonuses.CritX.Value.ToString("0.##")+"×" : "—",bonus.Contributors);
                else Bonus(bonus.Key,bonus.Name,bonus.Value,bonus.Contributors);
        }
        measuredBuildHeight = cursor;
        scroll = GUI.BeginScrollView(new Rect(x + 20 * s,y,width - 40 * s,height),scroll,
            new Rect(0,0,contentWidth,Math.Max(height,cursor)));
        try
        {
            foreach (var block in blocks)
            {
                if (block.H == 0) { Rule(new Rect(0,block.Y,contentWidth,s)); continue; }
                if (block.Section)
                {
                    Rule(new Rect(0,block.Y,contentWidth,s));
                    GUI.Label(new Rect(0,block.Y+4*s,contentWidth-90*s,24*s),block.Name,detailHeading!);
                    continue;
                }
                if(block.Key=="expanded") { Fill(new Rect(0,block.Y,contentWidth,block.H),new Color(0.075f,0.085f,0.08f,1));continue; }
                if(block.Key.StartsWith("tiles:",StringComparison.Ordinal))
                {
                    string metric=block.Key[6..];
                    var values=sheet.FirstOrDefault(row=>row.Key==metric)?.Contributors??Array.Empty<BuildContribution>();
                    var tiles=tileSets[metric];
                    int cols=PanelSpacing.Columns(contentWidth/s);
                    string selected=chosenTiles.GetValueOrDefault(metric,tiles.Length>0 ? tiles[0].Source+":"+tiles[0].Name : "");
                    if(tiles.Length>0 && !tiles.Any(tile=>tile.Source+":"+tile.Name==selected))selected=tiles[0].Source+":"+tiles[0].Name;
                    for(int i=0;i<tiles.Length;i++)
                    {
                        var tile=tiles[i];var rect=new Rect(PanelSpacing.ContentInset*s+(i%cols)*56*s,block.Y+(i/cols)*58*s,46*s,46*s);
                        Fill(rect,selected==tile.Source+":"+tile.Name ? new Color(0.78f,0.68f,0.40f,1) : new Color(0.42f,0.39f,0.28f,1));
                        Fill(new Rect(rect.x+2*s,rect.y+2*s,rect.width-4*s,rect.height-4*s),new Color(0.08f,0.13f,0.13f,1));
                        if(!GameItemArtwork.Draw(new Rect(rect.x+3*s,rect.y+3*s,40*s,40*s),tile) && SourceIcons.Supports(tile.Source))GUI.DrawTexture(new Rect(rect.x+7*s,rect.y+7*s,32*s,32*s),SourceIcons.Get(tile.Source));
                        if(GUI.Button(rect,new GUIContent("",null,tile.Name),rowButton!) && Cursor.visible)chosenTiles[metric]=tile.Source+":"+tile.Name;
                    }
                    continue;
                }
                if (block.Detail)
                {
                    if(block.Key=="body") { GUI.Label(new Rect(PanelSpacing.ContentInset*s,block.Y,contentWidth-2*PanelSpacing.ContentInset*s,block.H),block.Name,small!);continue; }
                    if(block.Key=="title")
                    {
                        GUI.Label(new Rect(PanelSpacing.ContentInset*s,block.Y,contentWidth-2*PanelSpacing.ContentInset*s-PanelSpacing.NumberWidth*s,block.H),block.Name,label!);
                        GUI.Label(new Rect(contentWidth-(PanelSpacing.ContentInset+PanelSpacing.NumberWidth)*s,block.Y,PanelSpacing.NumberWidth*s,28*s),block.Value,detailValue!);continue;
                    }
                    if(block.Key=="source") {GUI.Label(new Rect(PanelSpacing.ContentInset*s,block.Y,contentWidth-2*PanelSpacing.ContentInset*s,block.H),block.Name,small!);continue;}
                    string text=block.Name;float indent=PanelSpacing.ContentInset*s;
                    int separator=text.IndexOf(" · ",StringComparison.Ordinal);
                    if(separator>0 && SourceIcons.Supports(text[..separator]))
                    {
                        string source=text[..separator];
                        GUI.Label(new Rect(18*s,block.Y+2*s,28*s,28*s),new GUIContent("",SourceIcons.Get(source),source));
                        text=text[(separator+3)..];indent=58*s;
                    }
                    GUI.Label(new Rect(indent,block.Y,contentWidth-115*s-indent,block.H),text,block.Value.Length == 0 ? detailHeading! : small!);
                    GUI.Label(new Rect(contentWidth-(PanelSpacing.ContentInset+PanelSpacing.NumberWidth)*s,block.Y,PanelSpacing.NumberWidth*s,28*s),block.Value,detailValue!);
                    continue;
                }
                bool expanded=expandedBonuses.Contains(block.Key);
                if(expanded)GUI.DrawTexture(new Rect(0,block.Y,contentWidth,block.H),StatsTheme.Row);
                if (GUI.Button(new Rect(0,block.Y,contentWidth,block.H),"",rowButton!) && Cursor.visible)
                { if (!expandedBonuses.Add(block.Key)) expandedBonuses.Remove(block.Key); }
                int count=tileSets[block.Key].Length;
                GUI.Label(new Rect(8*s,block.Y+(block.H-26*s)/2,12*s,26*s),expanded ? "−" : "›",detailHeading!);
                float valueWidth=PanelSpacing.BuildValueWidth*s;
                float valueLeft=contentWidth-PanelSpacing.RowInset*s-valueWidth;
                float badgeLeft=valueLeft-(PanelSpacing.BadgeGap+PanelSpacing.BadgeWidth)*s;
                GUI.Label(new Rect(28*s,block.Y+4*s,badgeLeft-40*s,block.H-8*s),block.Name,buildLabel!);
                if(count>0)
                {
                    var badge=new Rect(badgeLeft,block.Y+(block.H-20*s)/2,PanelSpacing.BadgeWidth*s,20*s);
                    GUI.DrawTexture(badge,StatsTheme.Badge);
                    GUI.Label(badge,new GUIContent(count.ToString(),null,count+" contributors"),badgeValue!);
                }
                GUI.Label(new Rect(valueLeft,block.Y,valueWidth,block.H),block.Value,buildValue!);



            }
        }
        finally { GUI.EndScrollView(); }
    }
    private static void Rule(Rect rect) { }
    private static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
    private static string BonusValue(string metric,double value) => BuildSheetModel.Value(metric,value);
    private static float PanelWidth(float scale) => Math.Min(420 * scale, Screen.width * 0.215f);
    private static float MatrixHeight(float s) => measuredDamageHeight + 205*s;
    private static readonly Dictionary<string,Color> elementColors=new() {
        ["Normal"]=new(0.77f,0.73f,0.62f,1),["Fire"]=new(0.94f,0.62f,0.42f,1),
        ["Lightning"]=new(0.60f,0.74f,0.87f,1),["Corrosion"]=new(0.67f,0.79f,0.45f,1),["Other"]=new(0.66f,0.63f,0.71f,1)
    };
    private static void DrawMatrix(float x,float y,float width,float height,float s)
    {
        var sources=DamageTabModel.Create(DamageCapture.Ledger);
        float cw=width-62*s, inset=14*s, inner=cw-2*inset;
        // Keep the single-line legend pinned below the scrolling sources.
        legendLabel!.fontSize=Math.Max(1,(int)(11*s));
        float LegendWidth()=>DamageTabModel.Elements.Sum(name=>legendLabel!.CalcSize(new GUIContent(name)).x+12*s)+4*6*s;
        while(LegendWidth()>cw && legendLabel.fontSize>1)legendLabel.fontSize--;
        float cursor=0;
        var sections=new List<(SourceDamage Data,float Y,float Header,float End)>();
        foreach(var source in sources)
        {
            float header=Math.Max(42*s,label!.CalcHeight(new GUIContent(source.Name),cw-164*s)+8*s);
            float end=cursor+header+38*s;
            if(expandedSources.Contains(source.Name))end+=(32+source.Elements.Length*38+(source.Name is "Other damage" or "Damage over time" ? 32+source.Contributions.Length*28 : 0))*s;
            end+=22*s;sections.Add((source,cursor,header,end));cursor=end;
        }
        if(sources.Length==0)cursor+=40*s;
        measuredDamageHeight=cursor+32*s;
        float scrollHeight=Math.Max(1,height-32*s);
        scroll=GUI.BeginScrollView(new Rect(x+20*s,y,width-40*s,scrollHeight),scroll,new Rect(0,0,cw,Math.Max(scrollHeight,cursor)));
        try
        {
            foreach(var section in sections)
            {
                var source=section.Data;float ry=section.Y;
                if(GUI.Button(new Rect(0,ry,cw,section.Header),"",rowButton!) && Cursor.visible)
                {if(!expandedSources.Add(source.Name))expandedSources.Remove(source.Name);}
                GUI.Label(new Rect(0,ry,12*s,26*s),expandedSources.Contains(source.Name) ? "▾" : "▸",detailHeading!);
                GUI.Label(new Rect(16*s,ry,cw-164*s,section.Header),source.Name,label!);
                GUI.Label(new Rect(cw-145*s,ry+4*s,66*s,26*s),MatrixValue(source.Amount),damageAmount!);
                GUI.Label(new Rect(cw-74*s,ry,74*s,32*s),source.RunPercent.ToString("0.#")+"%",damageShare!);
                float barY=ry+section.Header;
                Fill(new Rect(inset,barY,inner,19*s),new Color(0.18f,0.20f,0.15f,1));
                float offset=0;
                foreach(var element in source.Elements)
                {
                    float segment=(float)Math.Clamp(element.RunFraction,0,1)*inner;
                    Fill(new Rect(inset+offset,barY,segment,19*s),elementColors[element.Element]);offset+=segment;
                }
                if(expandedSources.Contains(source.Name))
                {
                    float ey=barY+36*s;
                    GUI.Label(new Rect(inset,ey,inner-140*s,24*s),"ELEMENT",columnHeading!);
                    GUI.Label(new Rect(cw-inset-140*s,ey,70*s,24*s),"DAMAGE",columnValue!);
                    GUI.Label(new Rect(cw-inset-65*s,ey,65*s,24*s),"%",columnValue!);ey+=32*s;
                    foreach(var element in source.Elements)
                    {
                        DrawElementLabel(new Rect(inset,ey,inner-140*s,28*s),element.Element,elementLabel!,8*s,s);
                        GUI.Label(new Rect(cw-inset-140*s,ey,70*s,28*s),MatrixValue(element.Amount),damageAmount!);
                        GUI.Label(new Rect(cw-inset-65*s,ey,65*s,28*s),element.SourcePercent.ToString("0.#")+"%",matrixNumber!);ey+=38*s;
                    }
                }
                if(expandedSources.Contains(source.Name) && source.Name is "Other damage" or "Damage over time")
                {
                    float detailY=barY+(68+source.Elements.Length*38)*s;
                    GUI.Label(new Rect(inset,detailY,inner,24*s),"SOURCES",detailHeading!);detailY+=28*s;
                    foreach(var contribution in source.Contributions)
                    {
                        GUI.Label(new Rect(inset,detailY,inner-85*s,26*s),contribution.Name,small!);
                        GUI.Label(new Rect(cw-inset-80*s,detailY,80*s,26*s),MatrixValue(contribution.Amount),damageAmount!);detailY+=28*s;
                    }
                }
                Fill(new Rect(0,section.End-8*s,cw,s),new Color(0.28f,0.26f,0.18f,1));
            }
            if(sources.Length==0)GUI.Label(new Rect(0,0,cw,36*s),"Damage appears as you fight.",small!);
        }
        finally{GUI.EndScrollView();}
        float legendX=x+20*s,legendY=y+height-24*s;
        foreach(string element in DamageTabModel.Elements)
        {
            float textWidth=legendLabel!.CalcSize(new GUIContent(element)).x;
            DrawElementLabel(new Rect(legendX,legendY,textWidth+12*s,24*s),element,legendLabel!,6*s,s);
            legendX+=textWidth+18*s;
        }

    }
    private static void DrawElementLabel(Rect row,string element,GUIStyle style,float square,float scale)
    {
        // One shared line box for both the glyphs and their color swatch.
        float textHeight=style.CalcSize(new GUIContent(element)).y;
        float center=row.y+row.height/2;
        Fill(new Rect(row.x,center-square/2,square,square),elementColors[element]);
        float indent=square+6*scale;
        GUI.Label(new Rect(row.x+indent,center-textHeight/2,row.width-indent,textHeight),element,style);
    }
    private static string ElementName(string name)=>name switch {"True damage"=>"True", "Other element"=>"Other", _=>name};
    private static string MatrixValue(double value)=>value>=1e6 ? (value/1e6).ToString("0.#")+"M" : value>=1e3 ? (value/1e3).ToString("0.#")+"K" : value.ToString("0");
    private static string Compact(double value)
        => value >= 1e9 ? (value / 1e9).ToString("0.##") + "B" : value >= 1e6 ? (value / 1e6).ToString("0.##") + "M" : value >= 1e3 ? (value / 1e3).ToString("0.##") + "K" : value.ToString("0");
}
