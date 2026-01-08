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

        var info = pdfDocument.GetDocumentInfo();
        var trailer = pdfDocument.GetTrailer();
        var infoDictionary = trailer?.GetAsDictionary(PdfName.Info);

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
                var removedFromDict = false;

                if (infoDictionary != null && infoDictionary.ContainsKey(pdfName))
                {
                    infoDictionary.Remove(pdfName);
                    removedFromDict = true;
                    removedAny = true;
                }

                // If not removed directly (e.g. dictionary not accessible), use API
                // Also use API for custom keys if dictionary access failed
                if (!removedFromDict)
                {
                    if (RemoveDocumentInfoEntry(info, normalized))
                    {
                        removedAny = true;
                    }
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

    private static bool RemoveDocumentInfoEntry(PdfDocumentInfo? info, string key)
    {
        if (info == null)
        {
            return false;
        }

        try
        {
            // Try removing via SetMoreInfo with null, which works for custom keys and many standard ones
            // in raw dictionary mode.
            info.SetMoreInfo(key, null);

            // Verify removal
            if (string.IsNullOrEmpty(info.GetMoreInfo(key)))
            {
                return true;
            }

            // Fallback: set to empty string if null didn't remove it (mostly for paranoid compatibility)
            info.SetMoreInfo(key, string.Empty);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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
