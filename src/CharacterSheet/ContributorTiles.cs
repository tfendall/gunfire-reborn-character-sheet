internal sealed record ContributorTile(string Source,string Name,string Value,string Description,string IconSource="",int IconId=0);
internal static class ContributorTiles
{
    internal static ContributorTile[] Create(string metric,IEnumerable<BuildContribution> values,IEnumerable<LscRow> luck,IEnumerable<CoverageFinding> findings,int weaponSid=0)
    {
        var tiles=new List<ContributorTile>();
        if(metric=="lsc")
            foreach(var row in luck.Where(row=>row.Source!="Weapon" || row.Amount.GetValueOrDefault()!=0))
                tiles.Add(row.Source=="Weapon"
                    ? new("Weapon inscription","Weapon inscription",row.Amount.HasValue ? "+"+row.Amount.Value.ToString("0.#")+"%" : "—","")
                    : new(row.Source,row.Name,row.Amount.HasValue ? "+"+row.Amount.Value.ToString("0.#")+"%" : "—",row.Note));
        else foreach(var row in values)tiles.Add(new(row.Source,row.Name,BuildSheetModel.Value(row.Metric,row.Value),""));
        foreach(var finding in findings)
        {
            if(!ModifierCatalog.Entries.TryGetValue((finding.Source,finding.Id,finding.Level),out var definition))continue;
            int existing=tiles.FindIndex(tile=>tile.Source==finding.Category && tile.Name.StartsWith(definition.Name,StringComparison.Ordinal));
            if(existing>=0){tiles[existing]=tiles[existing] with {Description=definition.Description,IconSource=finding.Source,IconId=finding.Id};continue;}
            if(finding.Status==ModifierCoverage.NoNumericModifier || !BuildSheetModel.Relevant(metric,definition.Description))continue;
            string source=finding.Category.Length>0 ? finding.Category : finding.Source;
            string name=finding.Source=="inscription" ? "Weapon inscription" : definition.Name+" (Lv "+finding.Level+")";
            tiles.Add(new(source,name,"—",definition.Description,finding.Source,finding.Id));
        }
        var weaponTiles=tiles.Where(tile=>tile.Source is "Weapon" or "Weapon inscription" || tile.IconSource=="inscription").ToArray();
        if(weaponTiles.Length>0)
        {
            int index=tiles.FindIndex(tile=>weaponTiles.Contains(tile));
            tiles.RemoveAll(tile=>weaponTiles.Contains(tile));
            // The weapon property already contains its static inscriptions.
            // Keep that measured subtotal once, and retain every relevant condition.
            string value=weaponTiles.FirstOrDefault(tile=>tile.Value!="—")?.Value??"—";
            string description=string.Join("\n\n",weaponTiles.Select(tile=>tile.Description).Where(text=>text.Length>0).Distinct());
            tiles.Insert(index,new("Weapon","Equipped weapon",value,description,"weapon",weaponSid));
        }
        return tiles.Distinct().ToArray();
    }
}
