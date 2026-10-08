internal static class CrashDiagnostics
{
    private static readonly HashSet<string> entered = new(), completed = new();
    private static string latest = "plugin-load";
    internal static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Error("unhandled-managed-exception", args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) => Error("unobserved-task-exception", args.Exception);
        Begin("plugin-load");
        TraceStore.Write("session-environment", "plugin=" + Plugin.ModVersion
            + "; pid=" + Environment.ProcessId + "; runtime=" + Environment.Version + "; os=" + Environment.OSVersion
            + "; executable=" + Environment.ProcessPath, true);
    }
    internal static void Begin(string phase)
    {
        if (!entered.Add(phase)) return;
        latest = phase;
        Panel.Logger?.LogInfo("START " + phase);
        TraceStore.Write("checkpoint", "START " + phase, true);
    }
    internal static void Complete(string phase)
    {
        if (!completed.Add(phase)) return;
        Panel.Logger?.LogInfo("OK " + phase);
        TraceStore.Write("checkpoint", "OK " + phase, true);
    }
    internal static void Reading(string source)
    {
        latest = source;
        TraceStore.Write("checkpoint", "READ " + source, true);
    }
    internal static void ReadDone(string source) => TraceStore.Write("checkpoint", "DONE " + source, true);
    internal static void Error(string phase, Exception error)
    {
        try
        {
            string detail = phase + " (latest startup phase: " + latest + ") " + error;
            Panel.Logger?.LogError(detail);
            TraceStore.Write("managed-exception", detail, true);
        }
        catch { }
    }
}
