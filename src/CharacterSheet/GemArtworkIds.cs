using System.Text.RegularExpressions;
// Map ability identities to gem items from the existing inventory snapshot.
// This adds no native object reads and assumes no arithmetic ID relationship.
internal static class GemArtworkIds
{
    private static readonly Dictionary<int,int> items=new();
    private static readonly Regex equipped=new(@"S8GemUnit\{[^{}]*\bsid=(\d+),[^{}]*abilility=S8Ability\{sid=(\d+),",RegexOptions.Compiled);
    internal static void Observe(string snapshot)
    {
        foreach(Match match in equipped.Matches(snapshot))
            if(int.TryParse(match.Groups[1].Value,out int item) && int.TryParse(match.Groups[2].Value,out int ability))items[ability]=item;
    }
    internal static bool TryGet(int ability,out int item)=>items.TryGetValue(ability,out item);
    internal static void Clear()=>items.Clear();
}
