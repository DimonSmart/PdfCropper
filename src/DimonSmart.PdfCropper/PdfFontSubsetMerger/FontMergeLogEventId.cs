namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public enum FontMergeLogEventId
{
    FontResourcesCollected,
    FontGroupSummary,
    SubsetFontIndexed,
    SubsetFontSkippedDueToUnsupportedSubtype,
    CanonicalFontSplitAcrossGroups,
    SubsetFontsMerged,
    FontCompatibilityRejected,
    GlyphCodesCollected,
    FontClustersSplit,
    SubsetMergePrepared,
    FontWidthsMergeStatus,
    ToUnicodeMergeStatus,
    FontFileMergeStatus,
    FontResourceKeyReplaced,
    TextOperatorUpdated,
    UnusedFontResourceRemoved,
    FontMergeResultSummary
}
