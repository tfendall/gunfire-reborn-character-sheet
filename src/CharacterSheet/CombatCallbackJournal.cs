using System.IO.MemoryMappedFiles;
using System.Text;

// File-backed breadcrumbs survive termination without waiting for TraceSink.
// No game objects/values are touched. Bounded per-thread callback stacks.
internal static class CombatCallbackJournal
{
    private static MemoryMappedFile? map;
    private static MemoryMappedViewAccessor? view;
    private static readonly object gate=new();
    private static readonly Dictionary<int,int> slots=new();
    [ThreadStatic] private static List<string>? stack;
    internal static void Start()
    {
        try
        {
            string folder=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)! ,"BepInEx","StatsDiagnostic");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,$"callback-journal-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt");
            using(var file=File.Create(path))file.SetLength(32768);
            map=MemoryMappedFile.CreateFromFile(path,FileMode.Open,null,32768);
            view=map.CreateViewAccessor();
            TraceStore.Marker("combat-callback-journal "+path);
        }
        catch(Exception ex){TraceStore.Marker("combat-journal-unavailable "+ex.GetType().Name);}
    }
    internal static void Enter(string name)
    {
        try { (stack??=new()).Add(name);Write("entered "+name); }catch { }
    }
    internal static void Exit(string name)
    {
        try
        {
            if(stack is {Count:>0})stack.RemoveAt(stack.Count-1);
            Write("returned "+name);
        }
        catch { }
    }
    internal static void Phase(string phase)
    {
        try { Write("phase "+phase); } catch { }
    }
    private static void Write(string stage)
    {
        if(view==null)return;
        lock(gate)
        {
            int thread=Environment.CurrentManagedThreadId;
            if(!slots.TryGetValue(thread,out int slot))
            {if(slots.Count>=16)return;slot=slots.Count;slots[thread]=slot;}
            string text=$"utc={DateTime.UtcNow:O} thread={thread} {stage} active="+string.Join(" > ",stack?.TakeLast(8)??Enumerable.Empty<string>());
            if(text.Length>1800)text=text[..1800];
            byte[] bytes=Encoding.ASCII.GetBytes(text.PadRight(2047)+'\n');
            view.WriteArray(slot*2048,bytes,0,bytes.Length);
        }
    }
}
