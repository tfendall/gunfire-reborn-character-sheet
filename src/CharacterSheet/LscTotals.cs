internal sealed record LscRow(string Source, string Name, double? Amount, string Note, double? Cap = null);
internal static class LscTotals
{
    internal static double Known(IEnumerable<LscRow> rows)
    {
        double total = 0, cap = double.PositiveInfinity;
        foreach (var row in rows)
        {
            total += row.Amount ?? 0;
            if (row.Cap.HasValue) cap = Math.Min(cap, row.Cap.Value);
        }
        return Math.Min(total, cap);
    }
}
