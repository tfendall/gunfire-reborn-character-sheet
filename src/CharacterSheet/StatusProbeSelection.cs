internal static class StatusProbeSelection
{
    // Instance IDs (not SIDs) prevent two sources of the same effect being
    // silently merged by the diagnostic reader. Handle removal and insertion.
    internal static int Next(IEnumerable<int> ids, int after)
    {
        int first=0,next=0;
        foreach(int id in ids)
        {
            if(id<=0)continue;
            if(first==0 || id<first)first=id;
            if(id>after && (next==0 || id<next))next=id;
        }
        return next!=0 ? next : first;
    }
}
