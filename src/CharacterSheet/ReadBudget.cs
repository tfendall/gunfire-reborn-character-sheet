using System.Diagnostics;

internal static class ReadBudget
{
    private static long end;
    private static int remaining;
    internal static void Begin(int nodes = 256, double milliseconds = 1.5)
    {
        remaining = nodes;
        end = Stopwatch.GetTimestamp() + (long)(Stopwatch.Frequency * milliseconds / 1000);
    }
    internal static bool Step() => --remaining >= 0 && Stopwatch.GetTimestamp() < end;
}
