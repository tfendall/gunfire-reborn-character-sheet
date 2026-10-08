// Shared design dimensions, in reference pixels. Geometry tests use these same
// numbers; both build and damage views must use the same insets and gaps.
internal static class PanelSpacing
{
    internal const float RowInset = 14, ContentInset = 16, RowGap = 8;
    internal const float HeaderPadding = 12, ExpandedPadding = 16, DetailGap = 6;
    internal const float IconSize = 46, IconGap = 10, NumberWidth = 100;
    internal static float HeaderHeight(float textHeight,bool breakdown) => Math.Max(56,textHeight+HeaderPadding*2)+(breakdown ? 24 : 0);
    internal static int Columns(float width) => Math.Max(1,(int)Math.Floor((width-ContentInset*2+IconGap)/(IconSize+IconGap)));
}
