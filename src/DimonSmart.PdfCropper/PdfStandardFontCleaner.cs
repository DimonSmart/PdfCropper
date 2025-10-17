using System;
using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

internal static class PdfStandardFontCleaner
{
    private static readonly HashSet<string> StandardFontNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Courier",
        "Courier-Bold",
        "Courier-Oblique",
        "Courier-BoldOblique",
        "Helvetica",
        "Helvetica-Bold",
        "Helvetica-Oblique",
        "Helvetica-BoldOblique",
        "Times-Roman",
        "Times-Bold",
        "Times-Italic",
        "Times-BoldItalic",
        "Symbol",
        "ZapfDingbats"
    };

    public static void RemoveEmbeddedStandardFonts(PdfDocument pdfDocument)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        var pageCount = pdfDocument.GetNumberOfPages();
        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            var page = pdfDocument.GetPage(pageIndex);
            var resources = page.GetResources();
            var fonts = resources?.GetResource(PdfName.Font) as PdfDictionary;
            if (fonts == null)
            {
                continue;
            }

            foreach (var fontName in fonts.KeySet())
            {
                var fontDictionary = fonts.GetAsDictionary(fontName);
                if (fontDictionary == null)
                {
                    continue;
                }

                var baseFont = fontDictionary.GetAsName(PdfName.BaseFont);
                var fontNameValue = baseFont?.GetValue();
                if (string.IsNullOrEmpty(fontNameValue))
                {
                    continue;
                }

                if (!IsStandardFont(fontNameValue))
                {
                    continue;
                }

                var descriptor = fontDictionary.GetAsDictionary(PdfName.FontDescriptor);
                if (descriptor == null)
                {
                    continue;
                }

                descriptor.Remove(PdfName.FontFile);
                descriptor.Remove(PdfName.FontFile2);
                descriptor.Remove(PdfName.FontFile3);

                if (descriptor.Size() == 0)
                {
                    fontDictionary.Remove(PdfName.FontDescriptor);
                }
            }
        }
    }

    private static bool IsStandardFont(string fontName)
    {
        if (StandardFontNames.Contains(fontName))
        {
            return true;
        }

        var plusIndex = fontName.IndexOf('+');
        if (plusIndex > 0 && plusIndex < fontName.Length - 1)
        {
            var stripped = fontName[(plusIndex + 1)..];
            return StandardFontNames.Contains(stripped);
        }

        return false;
    }
}
