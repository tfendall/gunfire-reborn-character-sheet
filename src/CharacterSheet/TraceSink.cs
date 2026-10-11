using System.Collections.Concurrent;

// Owns all file access on a managed background thread. Producers never wait.
internal sealed class TraceSink : IDisposable
{
    private readonly BlockingCollection<string> queue = new(512);
    private readonly Thread worker;
    private readonly string stem;
    private int drops;
    private readonly long sessionLimit,directoryLimit;
    internal volatile bool CapacityReached;
    internal int Drops => System.Threading.Volatile.Read(ref drops);
    internal volatile string Error = "";
    internal TraceSink(string pathStem,long sessionLimitBytes=64*1024*1024,long directoryLimitBytes=256*1024*1024)
    {
        stem = pathStem;
        sessionLimit=Math.Max(1,sessionLimitBytes);directoryLimit=Math.Max(1,directoryLimitBytes);
        worker = new Thread(Run) { IsBackground = true, Name = "GunfireStats trace writer" };
        worker.Start();
    }
    internal bool TryWrite(string line)
    {
        try
        {
            if (!CapacityReached && Error.Length == 0 && queue.TryAdd(line)) return true;
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
            // Keep old investigations intact. Refuse further research writes at
            // the budget instead of silently deleting earlier evidence.
            long existing=Directory.EnumerateFiles(Path.GetDirectoryName(stem)!,"trace-*.jsonl")
                .Sum(path=>new FileInfo(path).Length);
            long written=0,partBytes=0;
            if(existing>=directoryLimit) {CapacityReached=true;return;}
            StreamWriter Open(string path)=>new(path,false,new System.Text.UTF8Encoding(false)) {NewLine="\n"};
            writer = Open(stem + ".jsonl");
            int part = 0,records=0;
            var lastFlush = System.Diagnostics.Stopwatch.StartNew();
            while (!queue.IsCompleted)
            {
                if (queue.TryTake(out string? line, 100))
                {
                    long bytes=System.Text.Encoding.UTF8.GetByteCount(line)+1L;
                    if(written+bytes>sessionLimit || existing+written+bytes>directoryLimit)
                    {CapacityReached=true;break;}
                    if (records>=10000 || (partBytes>0 && partBytes+bytes>4*1024*1024))
                    {
                        writer.Dispose();
                        writer = Open(stem + "-part" + (++part) + ".jsonl");
                        partBytes = 0;records=0;
                    }
                    writer.WriteLine(line);
                    written+=bytes;partBytes+=bytes;records++;
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
