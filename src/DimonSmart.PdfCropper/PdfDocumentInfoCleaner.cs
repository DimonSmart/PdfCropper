using System;
using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

internal static class PdfDocumentInfoCleaner
{
    public static void Apply(PdfDocument pdfDocument, PdfOptimizationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.ClearDocumentInfo)
        {
            Clear(pdfDocument);
            return;
        }

        RemoveKeys(pdfDocument, settings.DocumentInfoKeysToRemove);
    }

    public static void Clear(PdfDocument pdfDocument)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        var trailer = pdfDocument.GetTrailer();
        if (trailer == null)
        {
            return;
        }

        var infoDictionary = trailer.GetAsDictionary(PdfName.Info);
        infoDictionary?.Clear();
        // Do not remove the Info dictionary from the trailer to prevent issues during PdfDocument.Close()
        // trailer.Remove(PdfName.Info);
    }

    public static void RemoveKeys(PdfDocument pdfDocument, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);
        ArgumentNullException.ThrowIfNull(keys);

        if (keys.Count == 0)
        {
            return;
        }

        var trailer = pdfDocument.GetTrailer();
        var infoDictionary = trailer?.GetAsDictionary(PdfName.Info);
        var info = pdfDocument.GetDocumentInfo();

        if (infoDictionary == null && info == null)
        {
            return;
        }

        var removedAny = false;

        foreach (var key in keys)
        {
            var normalized = key?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            try
            {
                var pdfName = ResolveDocumentInfoName(normalized);
                if (infoDictionary != null && infoDictionary.Remove(pdfName) != null)
                {
                    removedAny = true;
                }
            }
            catch (ArgumentException)
            {
                // ignore invalid names to avoid breaking the pipeline
            }
        }

        if (!removedAny)
        {
            return;
        }

        infoDictionary?.SetModified();
    }

    private static PdfName ResolveDocumentInfoName(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "title" => PdfName.Title,
            "author" => PdfName.Author,
            "subject" => PdfName.Subject,
            "keywords" => PdfName.Keywords,
            "creator" => PdfName.Creator,
            "producer" => PdfName.Producer,
            "creationdate" => PdfName.CreationDate,
            "moddate" => PdfName.ModDate,
            "trapped" => PdfName.Trapped,
            _ => new PdfName(key)
        };
    }
}
