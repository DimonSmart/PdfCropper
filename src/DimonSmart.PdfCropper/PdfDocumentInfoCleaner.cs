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
        trailer.Remove(PdfName.Info);
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

                if (RemoveDocumentInfoEntry(info, normalized))
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

        if (infoDictionary != null && infoDictionary.Size() == 0)
        {
            trailer?.Remove(PdfName.Info);
        }
    }

    private static bool RemoveDocumentInfoEntry(PdfDocumentInfo? info, string key)
    {
        if (info == null)
        {
            return false;
        }

        try
        {
            switch (key.ToLowerInvariant())
            {
                case "title":
                    info.SetTitle(null);
                    return true;
                case "author":
                    info.SetAuthor(null);
                    return true;
                case "subject":
                    info.SetSubject(null);
                    return true;
                case "keywords":
                    info.SetKeywords(null);
                    return true;
                case "creator":
                    info.SetCreator(null);
                    return true;
                case "producer":
                    info.SetProducer(null);
                    return true;
                default:
                    info.SetMoreInfo(key, null);
                    if (!string.IsNullOrEmpty(info.GetMoreInfo(key)))
                    {
                        info.SetMoreInfo(key, string.Empty);
                    }

                    return true;
            }
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
            _ => new PdfName(key)
        };
    }
}
