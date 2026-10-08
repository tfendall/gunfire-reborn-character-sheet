using System.Collections.Concurrent;

// Owns all file access on a managed background thread. Producers never wait.
internal sealed class TraceSink : IDisposable
{
    private readonly BlockingCollection<string> queue = new(512);
    private readonly Thread worker;
    private readonly string stem;
    private int drops;
    internal int Drops => System.Threading.Volatile.Read(ref drops);
    internal volatile string Error = "";
    internal TraceSink(string pathStem)
    {
        stem = pathStem;
        worker = new Thread(Run) { IsBackground = true, Name = "GunfireStats trace writer" };
        worker.Start();
    }
    internal bool TryWrite(string line)
    {
        try
        {
            if (Error.Length == 0 && queue.TryAdd(line)) return true;
        }
        catch (InvalidOperationException) { }
        System.Threading.Interlocked.Increment(ref drops);
        return false;
    }
    private void Run()
    {
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(stem)!);
            writer = new StreamWriter(stem + ".jsonl", false);
            int records = 0, part = 0;
            var lastFlush = System.Diagnostics.Stopwatch.StartNew();
            while (!queue.IsCompleted)
            {
                if (queue.TryTake(out string? line, 100))
                {
                    if (records >= 10000)
                    {
                        writer.Dispose();
                        writer = new StreamWriter(stem + "-part" + (++part) + ".jsonl", false);
                        records = 0;
                    }
                    writer.WriteLine(line);
                    records++;
                    if (line.Contains("\"kind\":\"checkpoint\"") || line.Contains("\"kind\":\"managed-exception\"")) writer.Flush();
                }
                if (lastFlush.ElapsedMilliseconds >= 500) { writer.Flush(); lastFlush.Restart(); }
            }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally
        {
            try { writer?.Dispose(); } catch (Exception ex) { Error = ex.Message; }
        }
    }
    public void Dispose()
    {
        queue.CompleteAdding();
        // Only used on teardown; do not wait indefinitely for a stalled disk.
        worker.Join(100);
    }
}
