using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DimonSmart.PdfCropper;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper.PdfFontSubsetMerger;

public static class PdfFontSubsetMerger
{
    public static void MergeDuplicateSubsets(
        PdfDocument pdfDocument,
        FontSubsetMergeOptions? options = null,
        IPdfCropLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pdfDocument);

        var effectiveLogger = logger ?? NullLogger.Instance;
        var effectiveOptions = options ?? FontSubsetMergeOptions.CreateDefault();

        var service = new FontSubsetMergeService(effectiveOptions, effectiveLogger);
        service.Merge(pdfDocument);
    }

    private sealed class FontSubsetMergeService
    {
        private readonly FontSubsetMergeOptions options;
        private readonly IPdfCropLogger logger;

        public FontSubsetMergeService(FontSubsetMergeOptions options, IPdfCropLogger logger)
        {
            this.options = options;
            this.logger = logger;
        }

        public void Merge(PdfDocument pdfDocument)
        {
            var fonts = new FontResourceIndexer(options, logger).Collect(pdfDocument);
            if (fonts.Count == 0)
            {
                return;
            }

            var groups = fonts
                .GroupBy(entry => entry.MergeKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var canonical = group.First();
                var replacementObject = canonical.ReplacementObject;

                foreach (var duplicate in group.Skip(1))
                {
                    duplicate.ParentFontsDictionary.Put(duplicate.ResourceName, replacementObject);
                }

                var mergeMessage = $"Merged {group.Count()} subset fonts for \"{canonical.CanonicalName}\".";
                new FontMergeLogEvent(2005, FontMergeLogLevel.Info, mergeMessage).Log(logger);
            }
        }
    }

    private sealed class FontResourceIndexer
    {
        private readonly FontSubsetMergeOptions options;
        private readonly IPdfCropLogger logger;
        private readonly HashSet<long> visitedStreams = new();

        public FontResourceIndexer(FontSubsetMergeOptions options, IPdfCropLogger logger)
        {
            this.options = options;
            this.logger = logger;
        }

        public List<FontResourceEntry> Collect(PdfDocument pdfDocument)
        {
            var result = new List<FontResourceEntry>();
            var pageCount = pdfDocument.GetNumberOfPages();

            for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
            {
                var page = pdfDocument.GetPage(pageIndex);
                var resources = page.GetResources();
                var resourceDictionary = resources?.GetPdfObject() as PdfDictionary;
                resourceDictionary ??= page.GetPdfObject()?.GetAsDictionary(PdfName.Resources);
                CollectFromResources(resourceDictionary, pageIndex, result);

                if (options.IncludeAnnotations)
                {
                    CollectFromAnnotations(page, pageIndex, result);
                }
            }

            return result;
        }

        private void CollectFromResources(PdfDictionary? resources, int pageNumber, List<FontResourceEntry> target)
        {
            if (resources == null)
            {
                return;
            }

            var fontsDictionary = resources.GetAsDictionary(PdfName.Font);
            if (fontsDictionary != null)
            {
                foreach (var fontName in fontsDictionary.KeySet())
                {
                    var fontDictionary = fontsDictionary.GetAsDictionary(fontName);
                    if (fontDictionary == null)
                    {
                        continue;
                    }

                    var baseFontName = fontDictionary.GetAsName(PdfName.BaseFont)?.GetValue();
                    if (string.IsNullOrWhiteSpace(baseFontName))
                    {
                        continue;
                    }

                    if (!FontNameUtilities.IsSubsetFont(baseFontName))
                    {
                        continue;
                    }

                    var canonicalName = FontNameUtilities.GetCanonicalName(baseFontName);
                    if (canonicalName == null)
                    {
                        continue;
                    }

                    var subtype = fontDictionary.GetAsName(PdfName.Subtype)?.GetValue();
                    if (!options.IsSupportedFontSubtype(subtype))
                    {
                        var skipMessage = $"Skipped subset font \"{baseFontName}\" (resource {fontName.GetValue()}) on page {pageNumber} due to unsupported subtype \"{subtype}\".";
                        new FontMergeLogEvent(2001, FontMergeLogLevel.Warning, skipMessage).Log(logger);
                        continue;
                    }

                    var entry = FontResourceEntry.Create(fontsDictionary, fontName, fontDictionary, canonicalName, subtype);
                    target.Add(entry);
                    var indexMessage = $"Indexed subset font \"{baseFontName}\" (resource {fontName.GetValue()}) on page {pageNumber} with canonical name \"{canonicalName}\".";
                    new FontMergeLogEvent(2000, FontMergeLogLevel.Info, indexMessage).Log(logger);
                }
            }

            if (options.IncludeFormXObjects)
            {
                CollectFromFormXObjects(resources, pageNumber, target);
            }
        }

        private void CollectFromFormXObjects(PdfDictionary resources, int pageNumber, List<FontResourceEntry> target)
        {
            var xObjects = resources.GetAsDictionary(PdfName.XObject);
            if (xObjects == null)
            {
                return;
            }

            foreach (var name in xObjects.KeySet())
            {
                var stream = xObjects.GetAsStream(name);
                if (stream == null)
                {
                    continue;
                }

                var reference = stream.GetIndirectReference();
                long? key = reference != null ? ReferenceKey(reference) : null;
                if (key.HasValue && !visitedStreams.Add(key.Value))
                {
                    continue;
                }

                var subtype = stream.GetAsName(PdfName.Subtype);
                if (!PdfName.Form.Equals(subtype))
                {
                    continue;
                }

                var nestedResources = stream.GetAsDictionary(PdfName.Resources);
                CollectFromResources(nestedResources, pageNumber, target);
            }
        }

        private void CollectFromAnnotations(PdfPage page, int pageNumber, List<FontResourceEntry> target)
        {
            var pageDictionary = page.GetPdfObject();
            var annotations = pageDictionary?.GetAsArray(PdfName.Annots);
            if (annotations == null)
            {
                return;
            }

            for (var i = 0; i < annotations.Size(); i++)
            {
                var annotationDictionary = annotations.GetAsDictionary(i);
                if (annotationDictionary == null)
                {
                    continue;
                }

                var appearanceDictionary = annotationDictionary.GetAsDictionary(PdfName.AP);
                if (appearanceDictionary == null)
                {
                    continue;
                }

                foreach (var name in appearanceDictionary.KeySet())
                {
                    var appearanceObject = appearanceDictionary.Get(name);
                    CollectFromAppearanceObject(appearanceObject, pageNumber, target);
                }
            }
        }

        private void CollectFromAppearanceObject(PdfObject? appearanceObject, int pageNumber, List<FontResourceEntry> target)
        {
            if (appearanceObject == null)
            {
                return;
            }

            switch (appearanceObject)
            {
                case PdfStream stream:
                    var reference = stream.GetIndirectReference();
                    long? key = reference != null ? ReferenceKey(reference) : null;
                    if (key.HasValue && !visitedStreams.Add(key.Value))
                    {
                        return;
                    }

                    var resources = stream.GetAsDictionary(PdfName.Resources);
                    CollectFromResources(resources, pageNumber, target);
                    break;
                case PdfDictionary dictionary:
                    foreach (var name in dictionary.KeySet())
                    {
                        CollectFromAppearanceObject(dictionary.Get(name), pageNumber, target);
                    }

                    break;
                case PdfArray array:
                    for (var i = 0; i < array.Size(); i++)
                    {
                        CollectFromAppearanceObject(array.Get(i), pageNumber, target);
                    }

                    break;
            }
        }

        private static long ReferenceKey(PdfIndirectReference reference)
        {
            unchecked
            {
                return ((long)reference.GetObjNumber() << 32) | (uint)reference.GetGenNumber();
            }
        }
    }

    private readonly record struct FontResourceEntry(
        PdfDictionary ParentFontsDictionary,
        PdfName ResourceName,
        PdfDictionary FontDictionary,
        PdfObject ReplacementObject,
        string CanonicalName,
        string? Subtype,
        string MergeKey)
    {
        public static FontResourceEntry Create(
            PdfDictionary parentFontsDictionary,
            PdfName resourceName,
            PdfDictionary fontDictionary,
            string canonicalName,
            string? subtype)
        {
            var replacementObject = (PdfObject?)fontDictionary.GetIndirectReference() ?? fontDictionary;
            var mergeKey = FontMergeKeyFactory.Create(canonicalName, subtype, fontDictionary);
            return new FontResourceEntry(parentFontsDictionary, resourceName, fontDictionary, replacementObject, canonicalName, subtype, mergeKey);
        }
    }

    private static class FontMergeKeyFactory
    {
        public static string Create(string canonicalName, string? subtype, PdfDictionary fontDictionary)
        {
            var builder = new StringBuilder();
            builder.Append(canonicalName);
            builder.Append('|');
            builder.Append(subtype ?? string.Empty);
            builder.Append('|');
            builder.Append(FontDictionaryFingerprint.Create(fontDictionary));
            return builder.ToString();
        }
    }

    private static class FontDictionaryFingerprint
    {
        public static string Create(PdfDictionary fontDictionary)
        {
            var entries = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            FontSubsetFieldCleaner.Collect(fontDictionary, string.Empty, entries, new HashSet<PdfObject>());

            var builder = new StringBuilder();
            foreach (var pair in entries)
            {
                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(pair.Value);
                builder.Append(';');
            }

            return builder.ToString();
        }
    }

    private static class FontSubsetFieldCleaner
    {
        private static readonly HashSet<PdfName> FieldsToSkip = new()
        {
            PdfName.BaseFont,
            PdfName.FontName,
            PdfName.Name,
            PdfName.CIDSet,
            PdfName.ToUnicode,
            PdfName.FirstChar,
            PdfName.LastChar,
            PdfName.FontFile,
            PdfName.FontFile2,
            PdfName.FontFile3,
            PdfName.Length1
        };

        public static void Collect(
            PdfDictionary dictionary,
            string prefix,
            SortedDictionary<string, string?> target,
            HashSet<PdfObject> visited)
        {
            if (!visited.Add(dictionary))
            {
                return;
            }

            foreach (var key in dictionary.KeySet())
            {
                if (FieldsToSkip.Contains(key))
                {
                    continue;
                }

                var value = dictionary.Get(key);
                var entryKey = Combine(prefix, key.GetValue());

                switch (value)
                {
                    case PdfDictionary nested:
                        Collect(nested, entryKey, target, visited);
                        break;
                    case PdfArray array:
                        target[$"{entryKey}[]"] = array.Size().ToString(CultureInfo.InvariantCulture);
                        break;
                    case PdfName name:
                        target[entryKey] = name.GetValue();
                        break;
                    case PdfString text:
                        target[entryKey] = text.ToUnicodeString();
                        break;
                    case PdfNumber number:
                        target[entryKey] = number.GetValue().ToString(CultureInfo.InvariantCulture);
                        break;
                    case PdfBoolean booleanValue:
                        target[entryKey] = booleanValue.GetValue().ToString();
                        break;
                    default:
                        target[entryKey] = value?.ToString();
                        break;
                }
            }
        }

        private static string Combine(string prefix, string value)
        {
            return string.IsNullOrEmpty(prefix) ? value : string.Create(prefix.Length + value.Length + 1, (prefix, value), static (span, state) =>
            {
                var (first, second) = state;
                first.AsSpan().CopyTo(span);
                span[first.Length] = '.';
                second.AsSpan().CopyTo(span[(first.Length + 1)..]);
            });
        }
    }

    private static class FontNameUtilities
    {
        public static bool IsSubsetFont(string baseFontName)
        {
            if (string.IsNullOrEmpty(baseFontName))
            {
                return false;
            }

            var plusIndex = baseFontName.IndexOf('+');
            if (plusIndex <= 0 || plusIndex >= baseFontName.Length - 1)
            {
                return false;
            }

            for (var i = 0; i < plusIndex; i++)
            {
                if (!char.IsUpper(baseFontName[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static string? GetCanonicalName(string baseFontName)
        {
            if (string.IsNullOrEmpty(baseFontName))
            {
                return null;
            }

            var plusIndex = baseFontName.IndexOf('+');
            if (plusIndex > 0 && plusIndex < baseFontName.Length - 1)
            {
                return baseFontName[(plusIndex + 1)..];
            }

            return baseFontName;
        }
    }
}
