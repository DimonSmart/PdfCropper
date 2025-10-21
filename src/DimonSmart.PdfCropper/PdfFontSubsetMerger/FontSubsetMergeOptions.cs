using System;
using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public sealed class FontSubsetMergeOptions
{
    public static FontSubsetMergeOptions CreateDefault() => new();

    public bool IncludeFormXObjects { get; init; } = true;

    public bool IncludeAnnotations { get; init; } = true;

    public ISet<string> SupportedFontSubtypes { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        PdfName.Type0.GetValue(),
        PdfName.Type1.GetValue(),
        PdfName.TrueType.GetValue(),
        PdfName.CIDFontType0.GetValue(),
        PdfName.CIDFontType2.GetValue()
    };

    public bool IsSupportedFontSubtype(string? subtype)
    {
        if (string.IsNullOrWhiteSpace(subtype))
        {
            return true;
        }

        return SupportedFontSubtypes.Contains(subtype);
    }
}
