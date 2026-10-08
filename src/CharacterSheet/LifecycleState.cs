// Pure managed policies, validated without loading Unity or game bindings.
internal sealed class RunBoundary
{
    private bool active;
    private int number;
    internal bool Observe(bool inRun, int runNumber)
    {
        if (!inRun) { active = false; return false; }
        bool started = !active || (number > 0 && runNumber > 0 && number != runNumber);
        if (!active || runNumber > 0) number = runNumber;
        active = true;
        return started;
    }
}

internal sealed class PanelSafety
{
    private bool open, failed, drawn;
    internal bool CanDraw => open && !failed;
    internal bool CanBlockInput => CanDraw && drawn;
    internal void SetOpen(bool value)
    {
        if (value != open) { failed = false; drawn = false; }
        open = value;
    }
    internal void DrawSucceeded() { if (CanDraw) drawn = true; }
    internal void Fail() { failed = true; drawn = false; }
}

internal sealed class WorldReadiness
{
    private int handle = int.MinValue;
    private double since = -1;
    internal bool Ready;
    internal bool DisplayReady;
    internal bool Observe(int scene, bool loaded, bool cursor, bool statsOpen, bool pressedC, double now)
    {
        if (scene != handle || !loaded) { handle = scene; since = -1; Ready = DisplayReady = false; }
        if (!loaded) return false;
        if (cursor)
        {
            if (!statsOpen && !pressedC) Ready = false;
            since = -1;
            return Ready;
        }
        if (since < 0) since = now;
        Ready = now - since >= 2;
        if (Ready) DisplayReady = true;
        return Ready;
    }
}
