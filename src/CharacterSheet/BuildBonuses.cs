internal sealed record BuildContribution(string Metric, string Source, string Name, double Value,
    ModifierLayer? Layer = null, ModifierUnit? Unit = null, string Scope = "", ModifierCondition Condition = ModifierCondition.Always);
internal sealed record BuildNote(string Source, string Name, string Section, string Text);
internal static class BuildBonuses
{
    private static readonly Dictionary<string, List<BuildNote>> noteGroups = new();
    private static List<BuildNote> pendingNotes = new();
    internal static List<BuildNote> Notes = new();
    internal static List<BuildContribution> Rows = new();
    internal static double? WeaponElementChance, CritX, WeaponDamage;
    internal static void Reset() { noteGroups.Clear(); Notes = new(); pendingNotes = new(); ModifierRuntime.Reset(); Rows = new(); WeaponElementChance = CritX = WeaponDamage = null; }
    internal static void SetWeapon(int id) { ModifierRuntime.Weapon(id); Rows=ModifierRuntime.Rows; }
    internal static void SetMagazine(int id,int? ammo) { ModifierRuntime.Magazine(id,ammo);Rows=ModifierRuntime.Rows; }
    internal static void Refresh() { ModifierRuntime.Refresh();Rows=ModifierRuntime.Rows; }
    internal static void Begin() { ModifierRuntime.Begin();pendingNotes=new(); }
    internal static void Add(string source, int id, int level, string category)
    {
        ModifierRuntime.Add(source,id,level,category);
        if (BuildNotesCatalog.Entries.TryGetValue((source,id,level),out var note))
            pendingNotes.Add(new(category,note.Name + " (Lv " + level + ")",note.Section,note.Text));
    }
    internal static void Commit(string source)
    {
        ModifierRuntime.Commit(source);
        noteGroups[source] = pendingNotes;
        Notes = noteGroups.Values.SelectMany(x => x).ToList();
        Rows=ModifierRuntime.Rows;
    }
    internal static void Abort(string source) { ModifierRuntime.Abort(source);noteGroups.Remove(source);Notes=noteGroups.Values.SelectMany(x=>x).ToList();Rows=ModifierRuntime.Rows; }
    internal static IEnumerable<(string Metric, double Value)> Totals()
        => Rows.Where(x => x.Layer != ModifierLayer.Independent && !x.Metric.Contains("Total") && !x.Metric.Contains("Final"))
            .GroupBy(x => x.Metric).Select(x => (x.Key, x.Sum(y => y.Value)));
}
