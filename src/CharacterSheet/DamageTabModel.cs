internal sealed record ElementDamage(string Element,double Amount,double RunFraction,double SourcePercent);
internal sealed record SourceDamage(string Name,double Amount,double RunPercent,ElementDamage[] Elements,DamageContribution[] Contributions);
internal sealed record DamageContribution(string Name,double Amount);
internal static class DamageTabModel
{
    internal static readonly string[] Elements={"Normal","Fire","Lightning","Corrosion","Other"};
    private static string Source(string name)=>name is "Weapon hits" or "Weapon effects" ? "Weapon damage" : name.StartsWith("Skills") || name.StartsWith("Secondary skill") || name.StartsWith("Seasonal") ? "Skill damage" : name is "Burning" or "Bleeding" or "Damage over time" ? "Damage over time" : "Other damage";
    private static string Element(string name)=>Elements.Contains(name) ? name : "Other";
    internal static SourceDamage[] Create(DamageLedger ledger)
    {
        return ledger.Cells.GroupBy(cell=>Source(cell.Key.Source)).OrderBy(group=>group.Key=="Weapon damage" ? 0 : group.Key=="Skill damage" ? 1 : group.Key=="Damage over time" ? 2 : 3)
            .Select(group=>{
                double amount=group.Sum(cell=>cell.Value);
                var elements=group.GroupBy(cell=>Element(cell.Key.Element)).Select(cells=>{
                    double value=cells.Sum(cell=>cell.Value);
                    return new ElementDamage(cells.Key,value,ledger.Total>0 ? value/ledger.Total : 0,amount>0 ? 100*value/amount : 0);
                }).OrderBy(element=>Array.IndexOf(Elements,element.Element)).ToArray();
                var contributions=group.GroupBy(cell=>cell.Key.Source).Select(cells=>new DamageContribution(cells.Key=="Other damage" ? "Unidentified effects" : cells.Key,cells.Sum(cell=>cell.Value))).OrderByDescending(item=>item.Amount).ToArray();
                return new SourceDamage(group.Key,amount,ledger.Share(amount),elements,contributions);
            }).ToArray();
    }
}
