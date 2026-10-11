// UI reconciliation only; raw hit accounting and the diagnostic audit stay untouched.
internal sealed class AuthoritativeDamage
{
    internal static readonly AuthoritativeDamage Current=new();
    internal readonly DamageLedger View = new();
    internal bool Available { get; private set; }
    private bool baselinePending;
    private double baseline, lastReported;
    private Dictionary<int,double>? seasonalBaseline = new();
    private readonly Dictionary<(string Source,string Element),double> offsets=new();
    internal void Reset(bool manual)
    {
        Available=false;View.Clear();offsets.Clear();baseline=0;lastReported=0;
        baselinePending=manual;seasonalBaseline=new();
    }
    internal void Observe(double reported, DamageLedger captured, Dictionary<int,double>? seasonal = null)
    {
        if(!double.IsFinite(reported) || reported<0)return;
        if(Available && reported<lastReported) { Reset(false); baselinePending=true; }
        if(baselinePending)
        {
            baseline=reported;offsets.Clear();
            // A zero scoreboard proves every seasonal baseline is zero even
            // before an orb is equipped. A nonzero unknown baseline stays unsafe.
            seasonalBaseline=seasonal==null ? (reported==0 ? new() : null) : new(seasonal);
            foreach(var cell in captured.Cells)offsets[cell.Key]=cell.Value;
            baselinePending=false;
        }
        lastReported=reported;Available=true;View.Clear();
        foreach(var cell in captured.Cells)
        {
            double value=cell.Value-offsets.GetValueOrDefault(cell.Key);
            if(value>0)View.Cells[cell.Key]=value;
        }
        // Replace the entire seasonal partition: packet markers are shared across
        // seasonal effects and cannot safely identify only the orb's overlap.
        // A missing/incomplete reset snapshot disables replacement for this window.
        if(seasonal!=null && seasonalBaseline!=null && seasonalBaseline.All(x=>seasonal.GetValueOrDefault(x.Key)>=x.Value))
        {
            foreach(var key in View.Cells.Keys.Where(k=>k.Source.StartsWith("Seasonal")).ToArray())View.Cells.Remove(key);
            foreach(var cell in seasonal)
            {
                double value=cell.Value-seasonalBaseline.GetValueOrDefault(cell.Key);
                if(value>0)View.Cells[(cell.Key==11 ? "Seasonal · POLE MONARCH Orb" : $"Seasonal · Effect {cell.Key}","Unknown")]=value;
            }
        }
        double attributed=View.Cells.Values.Sum();
        View.Total=Math.Max(0,reported-baseline);
        double difference=View.Total-attributed;
        // Never silently rescale or discard captured hits when they exceed the report.
        if(difference!=0)View.Cells[(difference>0?"Unattributed damage":"Reconciliation adjustment","Unknown")]=difference;
    }
}
