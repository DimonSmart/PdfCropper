namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public enum FontMergeLogEventId
{
    SubsetFontIndexed,
    SubsetFontSkippedDueToUnsupportedSubtype,
    SubsetFontsMerged,
    GlyphCodesCollected,
    FontClustersSplit,
    SubsetMergePrepared,
    FontWidthsMergeStatus,
    ToUnicodeMergeStatus,
    FontFileMergeStatus,
    FontResourceKeyReplaced,
    TextOperatorUpdated,
    UnusedFontResourceRemoved
}
