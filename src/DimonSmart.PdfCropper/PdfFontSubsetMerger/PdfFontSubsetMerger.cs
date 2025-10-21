using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DimonSmart.PdfCropper;
using iText.Kernel.Font;
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

            var replacements = new List<FontResourceReplacement>();
            var groups = fonts
                .GroupBy(entry => entry.MergeKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var clusters = FontCompatibilityAnalyzer.Split(group.ToList(), logger);
                foreach (var cluster in clusters)
                {
                    if (cluster.Count <= 1)
                    {
                        continue;
                    }

                    var canonical = cluster[0];
                    FontSubsetMergerFactory
                        .TryCreate(canonical.Kind, logger)
                        ?.Merge(cluster);

                    var replacementObject = canonical.ReplacementObject;

                    foreach (var duplicate in cluster.Skip(1))
                    {
                        duplicate.ParentFontsDictionary.Put(duplicate.ResourceName, replacementObject);
                        replacements.Add(new FontResourceReplacement(
                            duplicate.ParentFontsDictionary,
                            duplicate.ResourceName,
                            canonical.ResourceName,
                            replacementObject,
                            canonical.CanonicalName));
                    }

                    var mergeMessage = $"Merged {cluster.Count} subset fonts for \"{canonical.CanonicalName}\".";
                    new FontMergeLogEvent(2005, FontMergeLogLevel.Info, mergeMessage).Log(logger);
                }
            }

            ApplyFontResourceReplacements(pdfDocument, replacements);
        }

        private void ApplyFontResourceReplacements(
            PdfDocument pdfDocument,
            List<FontResourceReplacement> replacements)
        {
            if (replacements.Count == 0)
            {
                return;
            }

            var dictionaryComparer = new PdfDictionaryReferenceComparer();
            var updates = new Dictionary<PdfDictionary, FontDictionaryUpdateInfo>(dictionaryComparer);

            foreach (var replacement in replacements)
            {
                if (!updates.TryGetValue(replacement.FontsDictionary, out var info))
                {
                    info = new FontDictionaryUpdateInfo(replacement.FontsDictionary);
                    updates[replacement.FontsDictionary] = info;
                }

                info.Add(replacement);
            }

            var renameLookup = new Dictionary<PdfDictionary, Dictionary<string, string>>(dictionaryComparer);

            foreach (var info in updates.Values)
            {
                info.Apply(logger);
                if (info.RenameMap.Count > 0)
                {
                    renameLookup[info.FontsDictionary] = info.RenameMap;
                }
            }

            if (renameLookup.Count == 0)
            {
                return;
            }

            var usedFontNames = new Dictionary<PdfDictionary, HashSet<string>>(dictionaryComparer);
            var visitedStreams = new HashSet<long>();

            var pageCount = pdfDocument.GetNumberOfPages();
            for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
            {
                var page = pdfDocument.GetPage(pageIndex);
                var resources = page.GetResources()?.GetPdfObject() as PdfDictionary
                    ?? page.GetPdfObject()?.GetAsDictionary(PdfName.Resources);

                ProcessPageContent(page, resources, pageIndex, renameLookup, usedFontNames, visitedStreams);

                if (options.IncludeAnnotations)
                {
                    ProcessAnnotations(page, pageIndex, renameLookup, usedFontNames, visitedStreams);
                }
            }

            RemoveUnusedFonts(renameLookup, usedFontNames);
        }

        private void ProcessPageContent(
            PdfPage page,
            PdfDictionary? resources,
            int pageNumber,
            Dictionary<PdfDictionary, Dictionary<string, string>> renameLookup,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames,
            HashSet<long> visitedStreams)
        {
            if (resources != null)
            {
                var fontsDictionary = resources.GetAsDictionary(PdfName.Font);
                if (fontsDictionary != null && renameLookup.TryGetValue(fontsDictionary, out var renameMap))
                {
                    var usageSet = GetUsageSet(fontsDictionary, usedFontNames);
                    var streamCount = page.GetContentStreamCount();
                    for (var index = 0; index < streamCount; index++)
                    {
                        var stream = page.GetContentStream(index);
                        if (stream == null)
                        {
                            continue;
                        }

                        var reference = stream.GetIndirectReference();
                        if (reference != null)
                        {
                            var key = ReferenceKey(reference);
                            if (!visitedStreams.Add(key))
                            {
                                continue;
                            }
                        }

                        ProcessStream(stream, renameMap, usageSet, $"page {pageNumber}");
                    }
                }

                if (options.IncludeFormXObjects)
                {
                    ProcessFormXObjects(resources, pageNumber, renameLookup, usedFontNames, visitedStreams);
                }
            }
        }

        private void ProcessFormXObjects(
            PdfDictionary resources,
            int pageNumber,
            Dictionary<PdfDictionary, Dictionary<string, string>> renameLookup,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames,
            HashSet<long> visitedStreams)
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
                if (reference != null)
                {
                    var key = ReferenceKey(reference);
                    if (!visitedStreams.Add(key))
                    {
                        continue;
                    }
                }

                var nestedResources = stream.GetAsDictionary(PdfName.Resources);
                if (nestedResources == null)
                {
                    continue;
                }

                var fontsDictionary = nestedResources.GetAsDictionary(PdfName.Font);
                if (fontsDictionary != null && renameLookup.TryGetValue(fontsDictionary, out var renameMap))
                {
                    var context = $"form XObject {name.GetValue()} on page {pageNumber}";
                    var usageSet = GetUsageSet(fontsDictionary, usedFontNames);
                    ProcessStream(stream, renameMap, usageSet, context);
                }

                if (options.IncludeFormXObjects)
                {
                    ProcessFormXObjects(nestedResources, pageNumber, renameLookup, usedFontNames, visitedStreams);
                }
            }
        }

        private void ProcessAnnotations(
            PdfPage page,
            int pageNumber,
            Dictionary<PdfDictionary, Dictionary<string, string>> renameLookup,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames,
            HashSet<long> visitedStreams)
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
                    ProcessAppearanceObject(
                        appearanceObject,
                        pageNumber,
                        $"annotation appearance {name.GetValue()} on page {pageNumber}",
                        renameLookup,
                        usedFontNames,
                        visitedStreams);
                }
            }
        }

        private void ProcessAppearanceObject(
            PdfObject? appearanceObject,
            int pageNumber,
            string context,
            Dictionary<PdfDictionary, Dictionary<string, string>> renameLookup,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames,
            HashSet<long> visitedStreams)
        {
            if (appearanceObject == null)
            {
                return;
            }

            switch (appearanceObject)
            {
                case PdfStream stream:
                    var reference = stream.GetIndirectReference();
                    if (reference != null)
                    {
                        var key = ReferenceKey(reference);
                        if (!visitedStreams.Add(key))
                        {
                            return;
                        }
                    }

                    var resources = stream.GetAsDictionary(PdfName.Resources);
                    if (resources != null)
                    {
                        var fontsDictionary = resources.GetAsDictionary(PdfName.Font);
                        if (fontsDictionary != null && renameLookup.TryGetValue(fontsDictionary, out var renameMap))
                        {
                            var usageSet = GetUsageSet(fontsDictionary, usedFontNames);
                            ProcessStream(stream, renameMap, usageSet, context);
                        }

                        if (options.IncludeFormXObjects)
                        {
                            ProcessFormXObjects(resources, pageNumber, renameLookup, usedFontNames, visitedStreams);
                        }
                    }

                    break;
                case PdfDictionary dictionary:
                    foreach (var name in dictionary.KeySet())
                    {
                        ProcessAppearanceObject(
                            dictionary.Get(name),
                            pageNumber,
                            context,
                            renameLookup,
                            usedFontNames,
                            visitedStreams);
                    }

                    break;
                case PdfArray array:
                    for (var i = 0; i < array.Size(); i++)
                    {
                        ProcessAppearanceObject(
                            array.Get(i),
                            pageNumber,
                            context,
                            renameLookup,
                            usedFontNames,
                            visitedStreams);
                    }

                    break;
            }
        }

        private void ProcessStream(
            PdfStream stream,
            Dictionary<string, string> renameMap,
            HashSet<string> usageSet,
            string context)
        {
            if (stream == null)
            {
                return;
            }

            var bytes = stream.GetBytes(true);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var replacements = new List<FontOperatorReplacement>();
            var editor = new ContentStreamFontReferenceEditor(bytes, renameMap);
            var updatedBytes = editor.Execute(usageSet, replacements);

            if (updatedBytes != null)
            {
                stream.SetData(updatedBytes);
            }

            LogFontOperatorReplacements(context, replacements);
        }

        private static HashSet<string> GetUsageSet(
            PdfDictionary fontsDictionary,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames)
        {
            if (!usedFontNames.TryGetValue(fontsDictionary, out var usageSet))
            {
                usageSet = new HashSet<string>(StringComparer.Ordinal);
                usedFontNames[fontsDictionary] = usageSet;
            }

            return usageSet;
        }

        private void LogFontOperatorReplacements(string context, List<FontOperatorReplacement> replacements)
        {
            if (replacements.Count == 0)
            {
                return;
            }

            var groups = replacements
                .GroupBy(replacement => replacement)
                .Select(group => new { group.Key.OldName, group.Key.NewName, Count = group.Count() });

            foreach (var group in groups)
            {
                var message = $"Updated Tf operator from \"/{group.OldName}\" to \"/{group.NewName}\" in {context} ({group.Count} occurrence(s)).";
                new FontMergeLogEvent(2050, FontMergeLogLevel.Info, message).Log(logger);
            }
        }

        private void RemoveUnusedFonts(
            Dictionary<PdfDictionary, Dictionary<string, string>> renameLookup,
            Dictionary<PdfDictionary, HashSet<string>> usedFontNames)
        {
            foreach (var pair in renameLookup)
            {
                var fontsDictionary = pair.Key;
                var usageSet = usedFontNames.TryGetValue(fontsDictionary, out var used)
                    ? used
                    : new HashSet<string>(StringComparer.Ordinal);

                var names = fontsDictionary.KeySet().ToArray();
                foreach (var name in names)
                {
                    var value = name.GetValue();
                    if (usageSet.Contains(value))
                    {
                        continue;
                    }

                    fontsDictionary.Remove(name);
                    var message = $"Removed unused font resource \"/{value}\" from fonts dictionary.";
                    new FontMergeLogEvent(2060, FontMergeLogLevel.Info, message).Log(logger);
                }
            }
        }

        private readonly record struct FontResourceReplacement(
            PdfDictionary FontsDictionary,
            PdfName OldName,
            PdfName NewName,
            PdfObject ReplacementObject,
            string CanonicalName);

        private sealed class FontDictionaryUpdateInfo
        {
            private readonly List<FontResourceReplacement> replacements = new();

            public FontDictionaryUpdateInfo(PdfDictionary fontsDictionary)
            {
                FontsDictionary = fontsDictionary;
            }

            public PdfDictionary FontsDictionary { get; }

            public Dictionary<string, string> RenameMap { get; } = new(StringComparer.Ordinal);

            public void Add(FontResourceReplacement replacement)
            {
                replacements.Add(replacement);
            }

            public void Apply(IPdfCropLogger logger)
            {
                foreach (var replacement in replacements)
                {
                    var oldName = replacement.OldName;
                    var newName = replacement.NewName;

                    if (!oldName.Equals(newName))
                    {
                        FontsDictionary.Remove(oldName);
                        RenameMap[oldName.GetValue()] = newName.GetValue();

                        var message = $"Replaced font resource key \"/{oldName.GetValue()}\" with \"/{newName.GetValue()}\" for \"{replacement.CanonicalName}\".";
                        new FontMergeLogEvent(2040, FontMergeLogLevel.Info, message).Log(logger);
                    }

                    FontsDictionary.Put(newName, replacement.ReplacementObject);
                }
            }
        }

        private sealed class PdfDictionaryReferenceComparer : IEqualityComparer<PdfDictionary>
        {
            public bool Equals(PdfDictionary? x, PdfDictionary? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(PdfDictionary obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private sealed record FontOperatorReplacement(string OldName, string NewName);

        private sealed class ContentStreamFontReferenceEditor
        {
            private readonly byte[] data;
            private readonly Dictionary<string, string> replacements;
            private readonly List<ReplacementSpan> spans = new();
            private readonly List<Token> operands = new();
            private int position;

            public ContentStreamFontReferenceEditor(byte[] data, Dictionary<string, string> replacements)
            {
                this.data = data ?? Array.Empty<byte>();
                this.replacements = replacements;
            }

            public byte[]? Execute(HashSet<string> usageSet, List<FontOperatorReplacement> replacementLog)
            {
                if (data.Length == 0)
                {
                    return null;
                }

                position = 0;
                spans.Clear();
                operands.Clear();

                while (TryReadToken(out var token))
                {
                    if (token.Type == TokenType.Operator)
                    {
                        ProcessOperator(token.Value!, usageSet, replacementLog);
                        operands.Clear();
                    }
                    else if (token.Type != TokenType.None)
                    {
                        operands.Add(token);
                    }
                }

                if (spans.Count == 0)
                {
                    return null;
                }

                using var stream = new MemoryStream(data.Length + spans.Count * 8);
                var currentIndex = 0;
                foreach (var span in spans)
                {
                    if (span.Start > currentIndex)
                    {
                        stream.Write(data, currentIndex, span.Start - currentIndex);
                    }

                    var replacementBytes = Encoding.ASCII.GetBytes(span.Replacement);
                    stream.Write(replacementBytes, 0, replacementBytes.Length);
                    currentIndex = span.Start + span.Length;
                }

                if (currentIndex < data.Length)
                {
                    stream.Write(data, currentIndex, data.Length - currentIndex);
                }

                return stream.ToArray();
            }

            private void ProcessOperator(string op, HashSet<string> usageSet, List<FontOperatorReplacement> replacementLog)
            {
                if (!string.Equals(op, "Tf", StringComparison.Ordinal))
                {
                    return;
                }

                if (operands.Count == 0)
                {
                    return;
                }

                var operand = operands[0];
                if (operand.Type != TokenType.Name || operand.Value == null)
                {
                    return;
                }

                usageSet.Add(operand.Value);

                if (!replacements.TryGetValue(operand.Value, out var newName))
                {
                    return;
                }

                if (string.Equals(newName, operand.Value, StringComparison.Ordinal))
                {
                    return;
                }

                usageSet.Add(newName);
                spans.Add(new ReplacementSpan(operand.Start, operand.Length, "/" + newName));
                replacementLog.Add(new FontOperatorReplacement(operand.Value, newName));
            }

            private bool TryReadToken(out Token token)
            {
                SkipWhitespaceAndComments();
                if (position >= data.Length)
                {
                    token = Token.None;
                    return false;
                }

                var current = (char)data[position];
                if (current == '<' && position + 1 < data.Length && data[position + 1] == (byte)'<')
                {
                    position += 2;
                    SkipDictionary();
                    return TryReadToken(out token);
                }

                token = ReadTokenInternal();
                return token.Type != TokenType.None;
            }

            private Token ReadTokenInternal()
            {
                if (position >= data.Length)
                {
                    return Token.None;
                }

                var current = (char)data[position];
                if (current == '/')
                {
                    var tokenStart = position;
                    position++;
                    var start = position;
                    while (position < data.Length)
                    {
                        var ch = (char)data[position];
                        if (IsDelimiter(ch) || char.IsWhiteSpace(ch))
                        {
                            break;
                        }

                        position++;
                    }

                    var raw = Encoding.ASCII.GetString(data, start, position - start);
                    var decoded = DecodeName(raw);
                    return new Token(TokenType.Name, decoded, tokenStart, position - tokenStart);
                }

                if (current == '(')
                {
                    var tokenStart = position;
                    position++;
                    SkipLiteralString();
                    return new Token(TokenType.String, null, tokenStart, position - tokenStart);
                }

                if (current == '<')
                {
                    var tokenStart = position;
                    position++;
                    SkipHexString();
                    return new Token(TokenType.String, null, tokenStart, position - tokenStart);
                }

                if (current == '[')
                {
                    var tokenStart = position;
                    position++;
                    while (true)
                    {
                        SkipWhitespaceAndComments();
                        if (position >= data.Length)
                        {
                            break;
                        }

                        if ((char)data[position] == ']')
                        {
                            position++;
                            break;
                        }

                        var nested = ReadTokenInternal();
                        if (nested.Type == TokenType.None)
                        {
                            break;
                        }
                    }

                    return new Token(TokenType.Array, null, tokenStart, position - tokenStart);
                }

                if (IsNumberStart(current))
                {
                    var tokenStart = position;
                    ReadNumber();
                    return new Token(TokenType.Number, null, tokenStart, position - tokenStart);
                }

                if (current == ']')
                {
                    position++;
                    return Token.None;
                }

                var operatorStart = position;
                var op = ReadOperator();
                return new Token(TokenType.Operator, op, operatorStart, position - operatorStart);
            }

            private void SkipWhitespaceAndComments()
            {
                while (position < data.Length)
                {
                    var current = (char)data[position];
                    if (current == '%')
                    {
                        position++;
                        while (position < data.Length)
                        {
                            var c = (char)data[position];
                            position++;
                            if (c == '\n' || c == '\r')
                            {
                                break;
                            }
                        }

                        continue;
                    }

                    if (!IsWhitespace(current))
                    {
                        break;
                    }

                    position++;
                }
            }

            private void SkipDictionary()
            {
                var depth = 1;
                while (position < data.Length && depth > 0)
                {
                    SkipWhitespaceAndComments();
                    if (position >= data.Length)
                    {
                        break;
                    }

                    var current = (char)data[position];
                    if (current == '<' && position + 1 < data.Length && data[position + 1] == (byte)'<')
                    {
                        position += 2;
                        depth++;
                        continue;
                    }

                    if (current == '>' && position + 1 < data.Length && data[position + 1] == (byte)'>')
                    {
                        position += 2;
                        depth--;
                        continue;
                    }

                    if (current == '(')
                    {
                        position++;
                        SkipLiteralString();
                        continue;
                    }

                    if (current == '<')
                    {
                        position++;
                        SkipHexString();
                        continue;
                    }

                    if (current == '[')
                    {
                        position++;
                        while (position < data.Length)
                        {
                            SkipWhitespaceAndComments();
                            if (position >= data.Length)
                            {
                                break;
                            }

                            if ((char)data[position] == ']')
                            {
                                position++;
                                break;
                            }

                            ReadTokenInternal();
                        }

                        continue;
                    }

                    if (current == '/')
                    {
                        position++;
                        while (position < data.Length)
                        {
                            var ch = (char)data[position];
                            if (IsDelimiter(ch) || char.IsWhiteSpace(ch))
                            {
                                break;
                            }

                            position++;
                        }

                        continue;
                    }

                    position++;
                }
            }

            private void SkipLiteralString()
            {
                var depth = 1;
                while (position < data.Length && depth > 0)
                {
                    var current = (char)data[position];
                    position++;

                    if (current == '\\')
                    {
                        if (position < data.Length)
                        {
                            var next = (char)data[position];
                            position++;

                            if (next is >= '0' and <= '7')
                            {
                                for (var i = 0; i < 2 && position < data.Length; i++)
                                {
                                    var peek = (char)data[position];
                                    if (peek is < '0' or > '7')
                                    {
                                        break;
                                    }

                                    position++;
                                }
                            }
                        }

                        continue;
                    }

                    if (current == '(')
                    {
                        depth++;
                        continue;
                    }

                    if (current == ')')
                    {
                        depth--;
                    }
                }
            }

            private void SkipHexString()
            {
                while (position < data.Length)
                {
                    var current = (char)data[position];
                    position++;
                    if (current == '>')
                    {
                        break;
                    }
                }
            }

            private void ReadNumber()
            {
                position++;
                while (position < data.Length)
                {
                    var ch = (char)data[position];
                    if (!(char.IsDigit(ch) || ch is '+' or '-' or '.' or 'E' or 'e'))
                    {
                        break;
                    }

                    position++;
                }
            }

            private string ReadOperator()
            {
                var start = position;
                position++;
                while (position < data.Length)
                {
                    var current = (char)data[position];
                    if (IsDelimiter(current) || IsWhitespace(current))
                    {
                        break;
                    }

                    position++;
                }

                return Encoding.ASCII.GetString(data, start, position - start);
            }

            private static string DecodeName(string raw)
            {
                if (string.IsNullOrEmpty(raw))
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(raw.Length);
                for (var i = 0; i < raw.Length; i++)
                {
                    var ch = raw[i];
                    if (ch == '#' && i + 2 < raw.Length)
                    {
                        var hex = raw.Substring(i + 1, 2);
                        if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                        {
                            builder.Append((char)value);
                            i += 2;
                            continue;
                        }
                    }

                    builder.Append(ch);
                }

                return builder.ToString();
            }

            private static bool IsDelimiter(char ch)
            {
                return ch is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
            }

            private static bool IsWhitespace(char ch)
            {
                return ch is '\0' or '\t' or '\n' or '\f' or '\r' or ' ';
            }

            private static bool IsNumberStart(char ch)
            {
                return char.IsDigit(ch) || ch is '+' or '-' or '.';
            }

            private readonly record struct ReplacementSpan(int Start, int Length, string Replacement);

            private readonly record struct Token(TokenType Type, string? Value, int Start, int Length)
            {
                public static readonly Token None = new(TokenType.None, null, 0, 0);
            }

            private enum TokenType
            {
                None,
                Name,
                Operator,
                Number,
                String,
                Array
            }
        }
    }

    private sealed class FontResourceIndexer
    {
        private readonly FontSubsetMergeOptions options;
        private readonly IPdfCropLogger logger;
        private readonly HashSet<long> visitedStreams = new();
        private readonly Dictionary<FontResourceKey, FontResourceEntry> entries = new(new FontResourceKeyComparer());
        private readonly ContentStreamFontUsageCollector usageCollector;

        public FontResourceIndexer(FontSubsetMergeOptions options, IPdfCropLogger logger)
        {
            this.options = options;
            this.logger = logger;
            usageCollector = new ContentStreamFontUsageCollector(entries);
        }

        public List<FontResourceEntry> Collect(PdfDocument pdfDocument)
        {
            var pageCount = pdfDocument.GetNumberOfPages();

            for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
            {
                var page = pdfDocument.GetPage(pageIndex);
                var resources = page.GetResources()?.GetPdfObject() as PdfDictionary
                    ?? page.GetPdfObject()?.GetAsDictionary(PdfName.Resources);

                CollectFromResources(resources, pageIndex);
                usageCollector.Collect(resources, page.GetContentBytes());

                if (options.IncludeAnnotations)
                {
                    CollectFromAnnotations(page, pageIndex);
                }
            }

            var result = entries.Values.ToList();

            foreach (var entry in result)
            {
                var codesCount = entry.EncounteredCodes.Count;
                var message = $"Collected {codesCount} glyph codes for font \"{entry.CanonicalName}\" (resource {entry.ResourceName.GetValue()}).";
                new FontMergeLogEvent(2010, FontMergeLogLevel.Info, message).Log(logger);
            }

            return result;
        }

        private void CollectFromResources(PdfDictionary? resources, int pageNumber)
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
                    TryCreateEntry(fontsDictionary, fontName, pageNumber);
                }
            }

            if (options.IncludeFormXObjects)
            {
                CollectFromFormXObjects(resources, pageNumber);
            }
        }

        private void TryCreateEntry(PdfDictionary fontsDictionary, PdfName fontName, int pageNumber)
        {
            var fontDictionary = fontsDictionary.GetAsDictionary(fontName);
            if (fontDictionary == null)
            {
                return;
            }

            var baseFontName = fontDictionary.GetAsName(PdfName.BaseFont)?.GetValue();
            if (string.IsNullOrWhiteSpace(baseFontName))
            {
                return;
            }

            if (!FontNameUtilities.IsSubsetFont(baseFontName))
            {
                return;
            }

            var canonicalName = FontNameUtilities.GetCanonicalName(baseFontName);
            if (canonicalName == null)
            {
                return;
            }

            var subtype = fontDictionary.GetAsName(PdfName.Subtype)?.GetValue();
            if (!options.IsSupportedFontSubtype(subtype))
            {
                var skipMessage = $"Skipped subset font \"{baseFontName}\" (resource {fontName.GetValue()}) on page {pageNumber} due to unsupported subtype \"{subtype}\".";
                new FontMergeLogEvent(2001, FontMergeLogLevel.Warning, skipMessage).Log(logger);
                return;
            }

            var key = new FontResourceKey(fontsDictionary, fontName);
            if (entries.ContainsKey(key))
            {
                return;
            }

            var entry = FontResourceEntry.Create(fontsDictionary, fontName, fontDictionary, canonicalName, subtype);
            entries[key] = entry;

            var indexMessage = $"Indexed subset font \"{baseFontName}\" (resource {fontName.GetValue()}) on page {pageNumber} with canonical name \"{canonicalName}\".";
            new FontMergeLogEvent(2000, FontMergeLogLevel.Info, indexMessage).Log(logger);
        }

        private void CollectFromFormXObjects(PdfDictionary resources, int pageNumber)
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
                CollectFromResources(nestedResources, pageNumber);
                usageCollector.Collect(nestedResources, stream.GetBytes(true));
            }
        }

        private void CollectFromAnnotations(PdfPage page, int pageNumber)
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
                    CollectFromAppearanceObject(appearanceObject, pageNumber);
                }
            }
        }

        private void CollectFromAppearanceObject(PdfObject? appearanceObject, int pageNumber)
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
                    CollectFromResources(resources, pageNumber);
                    usageCollector.Collect(resources, stream.GetBytes(true));
                    break;
                case PdfDictionary dictionary:
                    foreach (var name in dictionary.KeySet())
                    {
                        CollectFromAppearanceObject(dictionary.Get(name), pageNumber);
                    }

                    break;
                case PdfArray array:
                    for (var i = 0; i < array.Size(); i++)
                    {
                        CollectFromAppearanceObject(array.Get(i), pageNumber);
                    }

                    break;
            }
        }
    }

    private readonly record struct FontResourceKey(PdfDictionary FontsDictionary, PdfName ResourceName);

    private sealed class FontResourceKeyComparer : IEqualityComparer<FontResourceKey>
    {
        public bool Equals(FontResourceKey x, FontResourceKey y)
        {
            return ReferenceEquals(x.FontsDictionary, y.FontsDictionary)
                && ReferenceEquals(x.ResourceName, y.ResourceName);
        }

        public int GetHashCode(FontResourceKey obj)
        {
            return HashCode.Combine(
                RuntimeHelpers.GetHashCode(obj.FontsDictionary),
                RuntimeHelpers.GetHashCode(obj.ResourceName));
        }
    }

    private static class FontCompatibilityAnalyzer
    {
        public static List<List<FontResourceEntry>> Split(List<FontResourceEntry> fonts, IPdfCropLogger logger)
        {
            var clusters = new List<List<FontResourceEntry>>();
            foreach (var font in fonts)
            {
                var placed = false;
                foreach (var cluster in clusters)
                {
                    if (IsCompatible(font, cluster[0]))
                    {
                        cluster.Add(font);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    clusters.Add(new List<FontResourceEntry> { font });
                }
            }

            if (clusters.Count > 1 && fonts.Count > 1)
            {
                var canonicalName = fonts[0].CanonicalName;
                var message = $"Split {fonts.Count} subset fonts for \"{canonicalName}\" into {clusters.Count} clusters due to incompatible ToUnicode maps or metrics.";
                new FontMergeLogEvent(2020, FontMergeLogLevel.Info, message).Log(logger);
            }

            return clusters;
        }

        private static bool IsCompatible(FontResourceEntry left, FontResourceEntry right)
        {
            if (left.Kind != right.Kind)
            {
                return false;
            }

            var codes = new HashSet<int>(left.EncounteredCodes);
            codes.UnionWith(right.EncounteredCodes);

            foreach (var code in codes)
            {
                var leftHasUnicode = left.TryGetUnicode(code, out var leftUnicode);
                var rightHasUnicode = right.TryGetUnicode(code, out var rightUnicode);

                if (leftHasUnicode != rightHasUnicode)
                {
                    return false;
                }

                if (leftHasUnicode && !string.Equals(leftUnicode, rightUnicode, StringComparison.Ordinal))
                {
                    return false;
                }

                var leftHasWidth = left.TryGetWidth(code, out var leftWidth);
                var rightHasWidth = right.TryGetWidth(code, out var rightWidth);

                if (leftHasWidth != rightHasWidth)
                {
                    return false;
                }

                if (leftHasWidth && Math.Abs(leftWidth - rightWidth) > 0.01f)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class ContentStreamFontUsageCollector
    {
        private readonly Dictionary<FontResourceKey, FontResourceEntry> entries;

        public ContentStreamFontUsageCollector(Dictionary<FontResourceKey, FontResourceEntry> entries)
        {
            this.entries = entries;
        }

        public void Collect(PdfDictionary? resources, byte[]? contentBytes)
        {
            if (resources == null || contentBytes == null || contentBytes.Length == 0)
            {
                return;
            }

            var fontsDictionary = resources.GetAsDictionary(PdfName.Font);
            if (fontsDictionary == null)
            {
                return;
            }

            if (fontsDictionary.KeySet().Count == 0)
            {
                return;
            }

            var parser = new TextOperatorParser(entries, fontsDictionary);
            parser.Parse(contentBytes);
        }
    }

    private sealed class TextOperatorParser
    {
        private readonly Dictionary<FontResourceKey, FontResourceEntry> entries;
        private readonly PdfDictionary fontsDictionary;
        private readonly Dictionary<string, PdfName> nameLookup;
        private FontResourceEntry? currentFont;
        private byte[] data = Array.Empty<byte>();
        private int position;

        public TextOperatorParser(Dictionary<FontResourceKey, FontResourceEntry> entries, PdfDictionary fontsDictionary)
        {
            this.entries = entries;
            this.fontsDictionary = fontsDictionary;
            nameLookup = fontsDictionary
                .KeySet()
                .ToDictionary(name => name.GetValue(), name => name, StringComparer.Ordinal);
        }

        public void Parse(byte[] contentBytes)
        {
            if (contentBytes == null || contentBytes.Length == 0)
            {
                return;
            }

            data = contentBytes;
            position = 0;
            currentFont = null;
            var operands = new List<PdfContentItem>();

            while (TryReadToken(out var token))
            {
                if (token is PdfContentOperator op)
                {
                    ProcessOperator(op.Value, operands);
                    operands.Clear();
                }
                else if (token != null)
                {
                    operands.Add(token);
                }
            }
        }

        private void ProcessOperator(string op, List<PdfContentItem> operands)
        {
            switch (op)
            {
                case "Tf":
                    currentFont = ResolveFont(operands);
                    break;
                case "Tj":
                    if (operands.Count > 0 && operands[0] is PdfContentString singleString)
                    {
                        currentFont?.AddCodes(singleString.Bytes);
                    }

                    break;
                case "TJ":
                    if (operands.Count == 1 && operands[0] is PdfContentArray array)
                    {
                        foreach (var item in array.Items)
                        {
                            if (item is PdfContentString nestedString)
                            {
                                currentFont?.AddCodes(nestedString.Bytes);
                            }
                        }
                    }

                    break;
                case "'":
                    if (operands.Count > 0 && operands[^1] is PdfContentString apostropheString)
                    {
                        currentFont?.AddCodes(apostropheString.Bytes);
                    }

                    break;
                case "\"":
                    if (operands.Count > 0 && operands[^1] is PdfContentString quoteString)
                    {
                        currentFont?.AddCodes(quoteString.Bytes);
                    }

                    break;
            }
        }

        private FontResourceEntry? ResolveFont(List<PdfContentItem> operands)
        {
            if (operands.Count == 0)
            {
                return null;
            }

            if (operands[0] is not PdfContentName nameToken)
            {
                return null;
            }

            if (!nameLookup.TryGetValue(nameToken.Value, out var pdfName))
            {
                return null;
            }

            var key = new FontResourceKey(fontsDictionary, pdfName);
            return entries.TryGetValue(key, out var entry) ? entry : null;
        }

        private bool TryReadToken(out PdfContentItem? token)
        {
            SkipWhitespaceAndComments();
            if (position >= data.Length)
            {
                token = null;
                return false;
            }

            var current = (char)data[position];
            if (current == '<' && position + 1 < data.Length && data[position + 1] == (byte)'<')
            {
                position += 2;
                SkipDictionary();
                return TryReadToken(out token);
            }

            token = ReadTokenInternal();
            return token != null;
        }

        private PdfContentItem? ReadTokenInternal()
        {
            if (position >= data.Length)
            {
                return null;
            }

            var current = (char)data[position];
            if (current == '/')
            {
                position++;
                var start = position;
                while (position < data.Length)
                {
                    var ch = (char)data[position];
                    if (IsDelimiter(ch) || char.IsWhiteSpace(ch))
                    {
                        break;
                    }

                    position++;
                }

                var raw = Encoding.ASCII.GetString(data, start, position - start);
                var decoded = DecodeName(raw);
                return new PdfContentName(decoded);
            }

            if (current == '(')
            {
                position++;
                var bytes = ReadLiteralString();
                return new PdfContentString(bytes);
            }

            if (current == '<')
            {
                position++;
                var bytes = ReadHexString();
                return new PdfContentString(bytes);
            }

            if (current == '[')
            {
                position++;
                var items = new List<PdfContentItem>();
                while (true)
                {
                    SkipWhitespaceAndComments();
                    if (position >= data.Length)
                    {
                        break;
                    }

                    if ((char)data[position] == ']')
                    {
                        position++;
                        break;
                    }

                    var item = ReadTokenInternal();
                    if (item == null)
                    {
                        break;
                    }

                    items.Add(item);
                }

                return new PdfContentArray(items);
            }

            if (IsNumberStart(current))
            {
                var number = ReadNumber();
                return new PdfContentNumber(number);
            }

            if (current == ']')
            {
                position++;
                return null;
            }

            var op = ReadOperator();
            return new PdfContentOperator(op);
        }

        private void SkipWhitespaceAndComments()
        {
            while (position < data.Length)
            {
                var current = (char)data[position];
                if (current == '%')
                {
                    position++;
                    while (position < data.Length)
                    {
                        var c = (char)data[position];
                        position++;
                        if (c == '\n' || c == '\r')
                        {
                            break;
                        }
                    }

                    continue;
                }

                if (!IsWhitespace(current))
                {
                    break;
                }

                position++;
            }
        }

        private void SkipDictionary()
        {
            var depth = 1;
            while (position < data.Length && depth > 0)
            {
                SkipWhitespaceAndComments();
                if (position >= data.Length)
                {
                    break;
                }

                var current = (char)data[position];
                if (current == '<' && position + 1 < data.Length && data[position + 1] == (byte)'<')
                {
                    position += 2;
                    depth++;
                    continue;
                }

                if (current == '>' && position + 1 < data.Length && data[position + 1] == (byte)'>')
                {
                    position += 2;
                    depth--;
                    continue;
                }

                if (current == '(')
                {
                    position++;
                    ReadLiteralString();
                    continue;
                }

                if (current == '<')
                {
                    position++;
                    ReadHexString();
                    continue;
                }

                if (current == '[')
                {
                    position++;
                    while (position < data.Length)
                    {
                        SkipWhitespaceAndComments();
                        if (position >= data.Length)
                        {
                            break;
                        }

                        if ((char)data[position] == ']')
                        {
                            position++;
                            break;
                        }

                        ReadTokenInternal();
                    }

                    continue;
                }

                if (current == '/')
                {
                    position++;
                    while (position < data.Length)
                    {
                        var ch = (char)data[position];
                        if (IsDelimiter(ch) || char.IsWhiteSpace(ch))
                        {
                            break;
                        }

                        position++;
                    }

                    continue;
                }

                if (IsNumberStart(current))
                {
                    ReadNumber();
                    continue;
                }

                position++;
            }
        }

        private static bool IsWhitespace(char ch)
        {
            return ch == '\0' || ch == '\t' || ch == '\n' || ch == '\f' || ch == '\r' || ch == ' ';
        }

        private static bool IsDelimiter(char ch)
        {
            return ch is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
        }

        private static bool IsNumberStart(char ch)
        {
            return char.IsDigit(ch) || ch is '+' or '-' or '.';
        }

        private string ReadOperator()
        {
            var start = position;
            position++;
            while (position < data.Length)
            {
                var ch = (char)data[position];
                if (IsWhitespace(ch) || IsDelimiter(ch))
                {
                    break;
                }

                position++;
            }

            return Encoding.ASCII.GetString(data, start, position - start);
        }

        private byte[] ReadLiteralString()
        {
            var buffer = new List<byte>();
            var depth = 1;
            while (position < data.Length && depth > 0)
            {
                var current = (char)data[position++];
                if (current == '\\')
                {
                    if (position >= data.Length)
                    {
                        break;
                    }

                    var next = (char)data[position++];
                    switch (next)
                    {
                        case 'n':
                            buffer.Add((byte)'\n');
                            break;
                        case 'r':
                            buffer.Add((byte)'\r');
                            break;
                        case 't':
                            buffer.Add((byte)'\t');
                            break;
                        case 'b':
                            buffer.Add((byte)'\b');
                            break;
                        case 'f':
                            buffer.Add((byte)'\f');
                            break;
                        case '(':
                            buffer.Add((byte)'(');
                            break;
                        case ')':
                            buffer.Add((byte)')');
                            break;
                        case '\\':
                            buffer.Add((byte)'\\');
                            break;
                        case '\r':
                            if (position < data.Length && (char)data[position] == '\n')
                            {
                                position++;
                            }

                            break;
                        case '\n':
                            break;
                        default:
                            if (next is >= '0' and <= '7')
                            {
                                var octal = next - '0';
                                for (var i = 0; i < 2 && position < data.Length; i++)
                                {
                                    var peek = (char)data[position];
                                    if (peek is < '0' or > '7')
                                    {
                                        break;
                                    }

                                    position++;
                                    octal = (octal << 3) + (peek - '0');
                                }

                                buffer.Add((byte)octal);
                            }
                            else
                            {
                                buffer.Add((byte)next);
                            }

                            break;
                    }

                    continue;
                }

                if (current == '(')
                {
                    depth++;
                    buffer.Add((byte)'(');
                    continue;
                }

                if (current == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }

                    buffer.Add((byte)')');
                    continue;
                }

                buffer.Add((byte)current);
            }

            return buffer.ToArray();
        }

        private byte[] ReadHexString()
        {
            var start = position;
            while (position < data.Length)
            {
                var current = (char)data[position];
                position++;
                if (current == '>')
                {
                    break;
                }
            }

            var length = Math.Max(position - start - 1, 0);
            var token = length > 0 ? Encoding.ASCII.GetString(data, start, length) : string.Empty;
            var bytes = HexUtilities.ParseHex(token.AsSpan());
            return bytes;
        }

        private double ReadNumber()
        {
            var start = position;
            position++;
            while (position < data.Length)
            {
                var ch = (char)data[position];
                if (!(char.IsDigit(ch) || ch is '+' or '-' or '.' or 'E' or 'e'))
                {
                    break;
                }

                position++;
            }

            var token = Encoding.ASCII.GetString(data, start, position - start);
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                result = 0d;
            }

            return result;
        }

        private static string DecodeName(string raw)
        {
            if (raw.IndexOf('#', StringComparison.Ordinal) < 0)
            {
                return raw;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '#' && i + 2 < raw.Length && HexUtilities.IsHexDigit(raw[i + 1]) && HexUtilities.IsHexDigit(raw[i + 2]))
                {
                    var value = (HexUtilities.GetValue(raw[i + 1]) << 4) | HexUtilities.GetValue(raw[i + 2]);
                    builder.Append((char)value);
                    i += 2;
                }
                else
                {
                    builder.Append(raw[i]);
                }
            }

            return builder.ToString();
        }

        private abstract record PdfContentItem;

        private sealed record PdfContentOperator(string Value) : PdfContentItem;

        private sealed record PdfContentString(byte[] Bytes) : PdfContentItem;

        private sealed record PdfContentArray(IReadOnlyList<PdfContentItem> Items) : PdfContentItem;

        private sealed record PdfContentName(string Value) : PdfContentItem;

        private sealed record PdfContentNumber(double Value) : PdfContentItem;
    }

    private static class ToUnicodeCMapParser
    {
        public static Dictionary<int, string> Parse(PdfStream? stream)
        {
            var result = new Dictionary<int, string>();
            if (stream == null)
            {
                return result;
            }

            var bytes = stream.GetBytes(true);
            if (bytes == null || bytes.Length == 0)
            {
                return result;
            }

            var content = Encoding.ASCII.GetString(bytes);
            var tokens = Tokenize(content);
            var index = 0;

            while (index < tokens.Count)
            {
                var token = tokens[index];
                if (token.Equals("beginbfchar", StringComparison.Ordinal))
                {
                    index++;
                    while (index < tokens.Count && !tokens[index].Equals("endbfchar", StringComparison.Ordinal))
                    {
                        if (!IsHexToken(tokens[index]))
                        {
                            index++;
                            continue;
                        }

                        var sourceToken = tokens[index++];
                        if (index >= tokens.Count)
                        {
                            break;
                        }

                        var destinationToken = tokens[index++];
                        if (!IsHexToken(destinationToken))
                        {
                            continue;
                        }

                        var cid = HexUtilities.ParseHexInt(sourceToken);
                        var unicode = DecodeUnicode(destinationToken);
                        if (cid.HasValue && unicode != null)
                        {
                            result[cid.Value] = unicode;
                        }
                    }
                }
                else if (token.Equals("beginbfrange", StringComparison.Ordinal))
                {
                    index++;
                    while (index < tokens.Count && !tokens[index].Equals("endbfrange", StringComparison.Ordinal))
                    {
                        if (!IsHexToken(tokens[index]))
                        {
                            index++;
                            continue;
                        }

                        var startToken = tokens[index++];
                        if (index >= tokens.Count)
                        {
                            break;
                        }

                        var endToken = tokens[index++];
                        if (!IsHexToken(endToken))
                        {
                            continue;
                        }

                        if (index >= tokens.Count)
                        {
                            break;
                        }

                        if (tokens[index] == "[")
                        {
                            index++;
                            var list = new List<string>();
                            while (index < tokens.Count && tokens[index] != "]")
                            {
                                if (IsHexToken(tokens[index]))
                                {
                                    list.Add(tokens[index]);
                                }

                                index++;
                            }

                            if (index < tokens.Count && tokens[index] == "]")
                            {
                                index++;
                            }

                            var startCid = HexUtilities.ParseHexInt(startToken);
                            if (!startCid.HasValue)
                            {
                                continue;
                            }

                            for (var offset = 0; offset < list.Count; offset++)
                            {
                                var unicode = DecodeUnicode(list[offset]);
                                if (unicode != null)
                                {
                                    result[startCid.Value + offset] = unicode;
                                }
                            }
                        }
                        else
                        {
                            var destinationToken = tokens[index++];
                            if (!IsHexToken(destinationToken))
                            {
                                continue;
                            }

                            var startCid = HexUtilities.ParseHexInt(startToken);
                            var endCid = HexUtilities.ParseHexInt(endToken);
                            if (!startCid.HasValue || !endCid.HasValue)
                            {
                                continue;
                            }

                            var destBytes = HexUtilities.ParseHexBytes(destinationToken);
                            if (destBytes.Length == 2)
                            {
                                var startValue = (destBytes[0] << 8) | destBytes[1];
                                for (var cid = startCid.Value; cid <= endCid.Value; cid++)
                                {
                                    var unicodeValue = startValue + (cid - startCid.Value);
                                    var unicode = Encoding.BigEndianUnicode.GetString(new byte[]
                                    {
                                        (byte)((unicodeValue >> 8) & 0xFF),
                                        (byte)(unicodeValue & 0xFF)
                                    });
                                    result[cid] = unicode;
                                }
                            }
                            else
                            {
                                var unicode = DecodeUnicode(destinationToken);
                                if (unicode != null)
                                {
                                    for (var cid = startCid.Value; cid <= endCid.Value; cid++)
                                    {
                                        result[cid] = unicode;
                                    }
                                }
                            }
                        }
                    }
                }

                index++;
            }

            return result;
        }

        private static bool IsHexToken(string token)
        {
            return token.Length >= 2 && token[0] == '<' && token[^1] == '>';
        }

        private static string? DecodeUnicode(string token)
        {
            var bytes = HexUtilities.ParseHexBytes(token);
            if (bytes.Length == 0 || bytes.Length % 2 != 0)
            {
                return null;
            }

            return Encoding.BigEndianUnicode.GetString(bytes);
        }

        private static List<string> Tokenize(string content)
        {
            var tokens = new List<string>();
            var length = content.Length;
            var index = 0;
            while (index < length)
            {
                var ch = content[index];
                if (char.IsWhiteSpace(ch))
                {
                    index++;
                    continue;
                }

                if (ch == '%')
                {
                    while (index < length && content[index] != '\n' && content[index] != '\r')
                    {
                        index++;
                    }

                    continue;
                }

                if (ch == '<')
                {
                    if (index + 1 < length && content[index + 1] == '<')
                    {
                        tokens.Add("<<");
                        index += 2;
                        continue;
                    }

                    var end = content.IndexOf('>', index + 1);
                    if (end < 0)
                    {
                        break;
                    }

                    tokens.Add(content.Substring(index, end - index + 1));
                    index = end + 1;
                    continue;
                }

                if (ch == '[' || ch == ']')
                {
                    tokens.Add(ch.ToString());
                    index++;
                    continue;
                }

                if (ch == '(')
                {
                    var start = index;
                    index++;
                    var depth = 1;
                    while (index < length && depth > 0)
                    {
                        var current = content[index++];
                        if (current == '\\')
                        {
                            if (index < length)
                            {
                                index++;
                            }
                        }
                        else if (current == '(')
                        {
                            depth++;
                        }
                        else if (current == ')')
                        {
                            depth--;
                        }
                    }

                    tokens.Add(content.Substring(start, index - start));
                    continue;
                }

                var startIndex = index;
                index++;
                while (index < length && !char.IsWhiteSpace(content[index]) && content[index] != '[' && content[index] != ']')
                {
                    index++;
                }

                tokens.Add(content.Substring(startIndex, index - startIndex));
            }

            return tokens;
        }
    }

    private sealed class FontMetrics
    {
        public FontMetrics(FontSubsetKind kind, Dictionary<int, float> widths, float? defaultWidth, float? missingWidth, Dictionary<int, string> unicode)
        {
            Kind = kind;
            Widths = widths;
            DefaultWidth = defaultWidth;
            MissingWidth = missingWidth;
            Unicode = unicode;
        }

        public FontSubsetKind Kind { get; }

        public Dictionary<int, float> Widths { get; }

        public float? DefaultWidth { get; }

        public float? MissingWidth { get; }

        public Dictionary<int, string> Unicode { get; }
    }

    private static class FontMetricsExtractor
    {
        public static FontMetrics Extract(PdfDictionary fontDictionary)
        {
            var subtype = fontDictionary.GetAsName(PdfName.Subtype);
            if (PdfName.Type0.Equals(subtype))
            {
                var encoding = fontDictionary.GetAsName(PdfName.Encoding)?.GetValue();
                if (string.Equals(encoding, PdfName.IdentityH.GetValue(), StringComparison.Ordinal) || string.Equals(encoding, "Identity-V", StringComparison.Ordinal))
                {
                    return ExtractIdentityType0(fontDictionary);
                }
            }
            else if (PdfName.TrueType.Equals(subtype))
            {
                return ExtractTrueType(fontDictionary);
            }

            return new FontMetrics(FontSubsetKind.Unknown, new Dictionary<int, float>(), null, null, new Dictionary<int, string>());
        }

        private static FontMetrics ExtractIdentityType0(PdfDictionary fontDictionary)
        {
            var descendantFonts = fontDictionary.GetAsArray(PdfName.DescendantFonts);
            var cidFont = descendantFonts?.GetAsDictionary(0);

            float? defaultWidth = null;
            Dictionary<int, float> widths = new();
            if (cidFont != null)
            {
                widths = ParseCidWidths(cidFont, out defaultWidth);
            }

            var unicode = ToUnicodeCMapParser.Parse(fontDictionary.GetAsStream(PdfName.ToUnicode));
            return new FontMetrics(FontSubsetKind.Type0Identity, widths, defaultWidth, null, unicode);
        }

        private static FontMetrics ExtractTrueType(PdfDictionary fontDictionary)
        {
            var widths = ParseSimpleWidths(fontDictionary, out var missingWidth);
            var unicode = ToUnicodeCMapParser.Parse(fontDictionary.GetAsStream(PdfName.ToUnicode));
            return new FontMetrics(FontSubsetKind.TrueType, widths, null, missingWidth, unicode);
        }

        private static Dictionary<int, float> ParseSimpleWidths(PdfDictionary fontDictionary, out float? missingWidth)
        {
            var result = new Dictionary<int, float>();
            missingWidth = null;

            var firstChar = fontDictionary.GetAsNumber(PdfName.FirstChar)?.IntValue();
            var widthsArray = fontDictionary.GetAsArray(PdfName.Widths);
            if (firstChar.HasValue && widthsArray != null)
            {
                for (var index = 0; index < widthsArray.Size(); index++)
                {
                    var widthNumber = widthsArray.GetAsNumber(index);
                    if (widthNumber == null)
                    {
                        continue;
                    }

                    result[firstChar.Value + index] = (float)widthNumber.GetValue();
                }
            }

            missingWidth = fontDictionary
                .GetAsDictionary(PdfName.FontDescriptor)?
                .GetAsNumber(PdfName.MissingWidth)?.FloatValue();

            return result;
        }

        private static Dictionary<int, float> ParseCidWidths(PdfDictionary cidFont, out float? defaultWidth)
        {
            var result = new Dictionary<int, float>();
            defaultWidth = cidFont.GetAsNumber(PdfName.DW)?.FloatValue();

            var widths = cidFont.GetAsArray(PdfName.W);
            if (widths == null)
            {
                return result;
            }

            var index = 0;
            while (index < widths.Size())
            {
                var startNumber = widths.GetAsNumber(index++);
                if (startNumber == null)
                {
                    break;
                }

                var startCid = startNumber.IntValue();
                var next = widths.Get(index);
                if (next is PdfArray widthArray)
                {
                    index++;
                    for (var i = 0; i < widthArray.Size(); i++)
                    {
                        var widthNumber = widthArray.GetAsNumber(i);
                        if (widthNumber == null)
                        {
                            continue;
                        }

                        result[startCid + i] = (float)widthNumber.GetValue();
                    }
                }
                else if (next is PdfNumber endNumber)
                {
                    index++;
                    var widthNumber = widths.GetAsNumber(index++);
                    if (widthNumber == null)
                    {
                        continue;
                    }

                    var endCid = endNumber.IntValue();
                    var width = (float)widthNumber.GetValue();
                    for (var cid = startCid; cid <= endCid; cid++)
                    {
                        result[cid] = width;
                    }
                }
                else
                {
                    break;
                }
            }

            return result;
        }
    }

    private sealed class FontResourceEntry
    {
        private readonly Dictionary<int, float> widths;
        private readonly Dictionary<int, string> unicode;
        private readonly float? defaultWidth;
        private readonly float? missingWidth;

        private FontResourceEntry(
            PdfDictionary parentFontsDictionary,
            PdfName resourceName,
            PdfDictionary fontDictionary,
            PdfObject replacementObject,
            string canonicalName,
            string? subtype,
            string mergeKey,
            FontMetrics metrics)
        {
            ParentFontsDictionary = parentFontsDictionary;
            ResourceName = resourceName;
            FontDictionary = fontDictionary;
            ReplacementObject = replacementObject;
            CanonicalName = canonicalName;
            Subtype = subtype;
            MergeKey = mergeKey;
            Kind = metrics.Kind;
            widths = metrics.Widths;
            unicode = metrics.Unicode;
            defaultWidth = metrics.DefaultWidth;
            missingWidth = metrics.MissingWidth;
        }

        public PdfDictionary ParentFontsDictionary { get; }

        public PdfName ResourceName { get; }

        public PdfDictionary FontDictionary { get; }

        public PdfObject ReplacementObject { get; }

        public string CanonicalName { get; }

        public string? Subtype { get; }

        public string MergeKey { get; }

        public FontSubsetKind Kind { get; }

        public HashSet<int> EncounteredCodes { get; } = new();

        public IReadOnlyDictionary<int, float> GlyphWidths => widths;

        public float? DefaultWidth => defaultWidth;

        public float? MissingWidth => missingWidth;

        public IReadOnlyDictionary<int, string> ToUnicodeMap => unicode;

        public static FontResourceEntry Create(
            PdfDictionary parentFontsDictionary,
            PdfName resourceName,
            PdfDictionary fontDictionary,
            string canonicalName,
            string? subtype)
        {
            var replacementObject = (PdfObject?)fontDictionary.GetIndirectReference() ?? fontDictionary;
            var mergeKey = FontMergeKeyFactory.Create(canonicalName, subtype, fontDictionary);
            var metrics = FontMetricsExtractor.Extract(fontDictionary);
            return new FontResourceEntry(parentFontsDictionary, resourceName, fontDictionary, replacementObject, canonicalName, subtype, mergeKey, metrics);
        }

        public void AddCodes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length == 0)
            {
                return;
            }

            switch (Kind)
            {
                case FontSubsetKind.Type0Identity:
                    for (var index = 0; index + 1 < bytes.Length; index += 2)
                    {
                        var code = (bytes[index] << 8) | bytes[index + 1];
                        EncounteredCodes.Add(code);
                    }

                    break;
                case FontSubsetKind.TrueType:
                    foreach (var b in bytes)
                    {
                        EncounteredCodes.Add(b & 0xFF);
                    }

                    break;
            }
        }

        public bool TryGetUnicode(int code, out string? value)
        {
            if (unicode.TryGetValue(code, out var mapped))
            {
                value = mapped;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetWidth(int code, out float width)
        {
            switch (Kind)
            {
                case FontSubsetKind.Type0Identity:
                    if (widths.TryGetValue(code, out width))
                    {
                        return true;
                    }

                    if (defaultWidth.HasValue)
                    {
                        width = defaultWidth.Value;
                        return true;
                    }

                    break;
                case FontSubsetKind.TrueType:
                    if (widths.TryGetValue(code, out width))
                    {
                        return true;
                    }

                    if (missingWidth.HasValue)
                    {
                        width = missingWidth.Value;
                        return true;
                    }

                    break;
            }

            width = 0f;
            return false;
        }
    }

    private interface IFontSubsetMerger
    {
        void Merge(IReadOnlyList<FontResourceEntry> fonts);
    }

    private static class FontSubsetMergerFactory
    {
        public static IFontSubsetMerger? TryCreate(FontSubsetKind kind, IPdfCropLogger logger)
        {
            return kind switch
            {
                FontSubsetKind.TrueType => new TrueTypeSubsetMerger(logger),
                FontSubsetKind.Type0Identity => new Type0SubsetMerger(logger),
                _ => null
            };
        }
    }

    private sealed class FontSubsetMergeContext
    {
        public FontSubsetMergeContext(IReadOnlyList<FontResourceEntry> fonts)
        {
            if (fonts == null || fonts.Count == 0)
            {
                throw new ArgumentException("Cluster must contain at least one font resource.", nameof(fonts));
            }

            Fonts = fonts;
            Canonical = fonts[0];
            Glyphs = GlyphMergeMap.Build(fonts);
        }

        public IReadOnlyList<FontResourceEntry> Fonts { get; }

        public FontResourceEntry Canonical { get; }

        public GlyphMergeMap Glyphs { get; }

        public string CanonicalResourceName => Canonical.ResourceName.GetValue();

        public PdfDocument? Document => Canonical.FontDictionary.GetIndirectReference()?.GetDocument()
            ?? Canonical.ParentFontsDictionary.GetIndirectReference()?.GetDocument();
    }

    private sealed class TrueTypeSubsetMerger : IFontSubsetMerger
    {
        private readonly IPdfCropLogger logger;

        public TrueTypeSubsetMerger(IPdfCropLogger logger)
        {
            this.logger = logger;
        }

        public void Merge(IReadOnlyList<FontResourceEntry> fonts)
        {
            if (fonts.Count == 0)
            {
                return;
            }

            var context = new FontSubsetMergeContext(fonts);
            SubsetFontDictionaryCleaner.CleanTrueType(context.Canonical.FontDictionary);

            var prepareMessage = $"Prepared TrueType subset merge for \"{context.Canonical.CanonicalName}\" with {fonts.Count} entries.";
            new FontMergeLogEvent(2030, FontMergeLogLevel.Info, prepareMessage).Log(logger);

            MergeWidths(context);
            MergeToUnicode(context);
            MergeFontFile(context);
        }

        private void MergeWidths(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var glyphs = context.Glyphs;
            var resourceName = context.CanonicalResourceName;

            var ordered = glyphs.Entries
                .Where(pair => pair.Value.TryGetPreferredWidth(resourceName, out _))
                .OrderBy(pair => pair.Key)
                .ToList();

            if (ordered.Count == 0)
            {
                canonical.FontDictionary.Remove(PdfName.FirstChar);
                canonical.FontDictionary.Remove(PdfName.LastChar);
                canonical.FontDictionary.Remove(PdfName.Widths);

                var emptyMessage = $"No glyph widths available for \"{canonical.CanonicalName}\".";
                new FontMergeLogEvent(2031, FontMergeLogLevel.Info, emptyMessage).Log(logger);
                return;
            }

            var firstChar = ordered.First().Key;
            var lastChar = ordered.Last().Key;
            var widthsArray = new PdfArray();

            for (var code = firstChar; code <= lastChar; code++)
            {
                if (!glyphs.Entries.TryGetValue(code, out var entry)
                    || !entry.TryGetPreferredWidth(resourceName, out var width))
                {
                    widthsArray.Add(new PdfNumber(0));
                    continue;
                }

                widthsArray.Add(new PdfNumber(width));
                if (entry.HasWidthConflict)
                {
                    var conflict = entry.BuildWidthConflictDescription();
                    var warning = $"Width conflict for glyph code {code} while merging \"{canonical.CanonicalName}\": {conflict}. Using {width.ToString(CultureInfo.InvariantCulture)}.";
                    new FontMergeLogEvent(2031, FontMergeLogLevel.Warning, warning).Log(logger);
                }
            }

            var fontDictionary = canonical.FontDictionary;
            fontDictionary.Put(PdfName.FirstChar, new PdfNumber(firstChar));
            fontDictionary.Put(PdfName.LastChar, new PdfNumber(lastChar));
            fontDictionary.Put(PdfName.Widths, widthsArray);

            var mergeMessage = $"Merged {ordered.Count} glyph widths for \"{canonical.CanonicalName}\".";
            new FontMergeLogEvent(2031, FontMergeLogLevel.Info, mergeMessage).Log(logger);
        }

        private void MergeToUnicode(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var glyphs = context.Glyphs;
            var resourceName = context.CanonicalResourceName;
            var unicodeMap = new Dictionary<int, string>();

            foreach (var pair in glyphs.Entries)
            {
                if (!pair.Value.TryGetPreferredUnicode(resourceName, out var unicode))
                {
                    continue;
                }

                unicodeMap[pair.Key] = unicode!;
                if (pair.Value.HasUnicodeConflict)
                {
                    var conflict = pair.Value.BuildUnicodeConflictDescription();
                    var warning = $"Unicode conflict for glyph code {pair.Key} while merging \"{canonical.CanonicalName}\": {conflict}. Using \"{unicode}\".";
                    new FontMergeLogEvent(2032, FontMergeLogLevel.Warning, warning).Log(logger);
                }
            }

            if (unicodeMap.Count == 0)
            {
                canonical.FontDictionary.Remove(PdfName.ToUnicode);
                var emptyMessage = $"No ToUnicode entries available for \"{canonical.CanonicalName}\".";
                new FontMergeLogEvent(2032, FontMergeLogLevel.Info, emptyMessage).Log(logger);
                return;
            }

            var stream = ToUnicodeCMapWriter.Create(unicodeMap, false, context.Document);
            canonical.FontDictionary.Put(PdfName.ToUnicode, stream);

            var mergeMessage = $"Merged ToUnicode map with {unicodeMap.Count} entries for \"{canonical.CanonicalName}\".";
            new FontMergeLogEvent(2032, FontMergeLogLevel.Info, mergeMessage).Log(logger);
        }

        private void MergeFontFile(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var (sourceStream, sourceName) = FontDescriptorUtilities.GetPreferredFontFile2(context.Fonts);
            var descriptor = FontDescriptorUtilities.GetDescriptor(canonical);
            if (descriptor == null)
            {
                var message = $"Skipped FontFile2 merge for \"{canonical.CanonicalName}\" because font descriptor is missing.";
                new FontMergeLogEvent(2033, FontMergeLogLevel.Info, message).Log(logger);
                return;
            }

            descriptor.Remove(PdfName.FontFile);
            descriptor.Remove(PdfName.FontFile3);

            if (sourceStream != null)
            {
                var sharedStream = FontDescriptorUtilities.CloneFontFile(sourceStream, context.Document);
                descriptor.Put(PdfName.FontFile2, sharedStream);
            }
            else
            {
                descriptor.Remove(PdfName.FontFile2);
            }

            var newName = FontUtil.AddRandomSubsetPrefixForFontName(canonical.CanonicalName);
            canonical.FontDictionary.Put(PdfName.BaseFont, new PdfName(newName));
            descriptor.Put(PdfName.FontName, new PdfName(newName));

            var resultMessage = sourceStream != null
                ? $"Assigned shared FontFile2 from resource {sourceName} for \"{canonical.CanonicalName}\" and updated font name to \"{newName}\"."
                : $"Removed FontFile2 for \"{canonical.CanonicalName}\" and updated font name to \"{newName}\".";
            new FontMergeLogEvent(2033, FontMergeLogLevel.Info, resultMessage).Log(logger);
        }
    }

    private sealed class Type0SubsetMerger : IFontSubsetMerger
    {
        private readonly IPdfCropLogger logger;

        public Type0SubsetMerger(IPdfCropLogger logger)
        {
            this.logger = logger;
        }

        public void Merge(IReadOnlyList<FontResourceEntry> fonts)
        {
            if (fonts.Count == 0)
            {
                return;
            }

            var context = new FontSubsetMergeContext(fonts);
            SubsetFontDictionaryCleaner.CleanType0(context.Canonical.FontDictionary);

            var prepareMessage = $"Prepared Type0 subset merge for \"{context.Canonical.CanonicalName}\" with {fonts.Count} entries.";
            new FontMergeLogEvent(2030, FontMergeLogLevel.Info, prepareMessage).Log(logger);

            MergeWidths(context);
            MergeToUnicode(context);
            MergeFontFile(context);
        }

        private void MergeWidths(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var resourceName = context.CanonicalResourceName;
            var glyphs = context.Glyphs;
            var cidFont = FontDescriptorUtilities.GetCidFontDictionary(canonical.FontDictionary);

            if (cidFont == null)
            {
                var message = $"Skipped width merge for \"{canonical.CanonicalName}\" because CID font dictionary is missing.";
                new FontMergeLogEvent(2031, FontMergeLogLevel.Info, message).Log(logger);
                return;
            }

            var ordered = glyphs.Entries
                .Where(pair => pair.Value.TryGetPreferredWidth(resourceName, out _))
                .OrderBy(pair => pair.Key)
                .ToList();

            if (ordered.Count == 0)
            {
                cidFont.Remove(PdfName.W);
                var emptyMessage = $"No CID widths available for \"{canonical.CanonicalName}\".";
                new FontMergeLogEvent(2031, FontMergeLogLevel.Info, emptyMessage).Log(logger);
                return;
            }

            var widthsArray = new PdfArray();
            var currentStart = -1;
            List<float> currentWidths = new();
            var previousCode = -2;

            foreach (var pair in ordered)
            {
                if (!pair.Value.TryGetPreferredWidth(resourceName, out var width))
                {
                    continue;
                }

                if (currentStart < 0)
                {
                    currentStart = pair.Key;
                    previousCode = pair.Key;
                    currentWidths.Add(width);
                }
                else if (pair.Key == previousCode + 1)
                {
                    previousCode = pair.Key;
                    currentWidths.Add(width);
                }
                else
                {
                    AppendWidthRange(widthsArray, currentStart, currentWidths);
                    currentStart = pair.Key;
                    previousCode = pair.Key;
                    currentWidths = new List<float> { width };
                }

                if (pair.Value.HasWidthConflict)
                {
                    var conflict = pair.Value.BuildWidthConflictDescription();
                    var warning = $"Width conflict for CID {pair.Key} while merging \"{canonical.CanonicalName}\": {conflict}. Using {width.ToString(CultureInfo.InvariantCulture)}.";
                    new FontMergeLogEvent(2031, FontMergeLogLevel.Warning, warning).Log(logger);
                }
            }

            AppendWidthRange(widthsArray, currentStart, currentWidths);
            cidFont.Put(PdfName.W, widthsArray);

            var mergeMessage = $"Merged {ordered.Count} CID widths for \"{canonical.CanonicalName}\".";
            new FontMergeLogEvent(2031, FontMergeLogLevel.Info, mergeMessage).Log(logger);
        }

        private static void AppendWidthRange(PdfArray target, int startCode, List<float> widths)
        {
            if (startCode < 0 || widths.Count == 0)
            {
                return;
            }

            target.Add(new PdfNumber(startCode));
            var array = new PdfArray();
            foreach (var width in widths)
            {
                array.Add(new PdfNumber(width));
            }

            target.Add(array);
        }

        private void MergeToUnicode(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var glyphs = context.Glyphs;
            var resourceName = context.CanonicalResourceName;
            var unicodeMap = new Dictionary<int, string>();

            foreach (var pair in glyphs.Entries)
            {
                if (!pair.Value.TryGetPreferredUnicode(resourceName, out var unicode))
                {
                    continue;
                }

                unicodeMap[pair.Key] = unicode!;
                if (pair.Value.HasUnicodeConflict)
                {
                    var conflict = pair.Value.BuildUnicodeConflictDescription();
                    var warning = $"Unicode conflict for CID {pair.Key} while merging \"{canonical.CanonicalName}\": {conflict}. Using \"{unicode}\".";
                    new FontMergeLogEvent(2032, FontMergeLogLevel.Warning, warning).Log(logger);
                }
            }

            if (unicodeMap.Count == 0)
            {
                canonical.FontDictionary.Remove(PdfName.ToUnicode);
                var emptyMessage = $"No ToUnicode entries available for \"{canonical.CanonicalName}\".";
                new FontMergeLogEvent(2032, FontMergeLogLevel.Info, emptyMessage).Log(logger);
                return;
            }

            var stream = ToUnicodeCMapWriter.Create(unicodeMap, true, context.Document);
            canonical.FontDictionary.Put(PdfName.ToUnicode, stream);

            var mergeMessage = $"Merged ToUnicode map with {unicodeMap.Count} entries for \"{canonical.CanonicalName}\".";
            new FontMergeLogEvent(2032, FontMergeLogLevel.Info, mergeMessage).Log(logger);
        }

        private void MergeFontFile(FontSubsetMergeContext context)
        {
            var canonical = context.Canonical;
            var cidFont = FontDescriptorUtilities.GetCidFontDictionary(canonical.FontDictionary);
            if (cidFont == null)
            {
                var skipMessage = $"Skipped FontFile2 merge for \"{canonical.CanonicalName}\" because CID font dictionary is missing.";
                new FontMergeLogEvent(2033, FontMergeLogLevel.Info, skipMessage).Log(logger);
                return;
            }

            var descriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
            if (descriptor == null)
            {
                var skipMessage = $"Skipped FontFile2 merge for \"{canonical.CanonicalName}\" because font descriptor is missing.";
                new FontMergeLogEvent(2033, FontMergeLogLevel.Info, skipMessage).Log(logger);
                return;
            }

            descriptor.Remove(PdfName.FontFile);
            descriptor.Remove(PdfName.FontFile3);

            var (sourceStream, sourceName) = FontDescriptorUtilities.GetPreferredFontFile2(context.Fonts);
            if (sourceStream != null)
            {
                var sharedStream = FontDescriptorUtilities.CloneFontFile(sourceStream, context.Document);
                descriptor.Put(PdfName.FontFile2, sharedStream);
            }
            else
            {
                descriptor.Remove(PdfName.FontFile2);
            }

            var newName = FontUtil.AddRandomSubsetPrefixForFontName(canonical.CanonicalName);
            canonical.FontDictionary.Put(PdfName.BaseFont, new PdfName(newName));
            cidFont.Put(PdfName.BaseFont, new PdfName(newName));
            descriptor.Put(PdfName.FontName, new PdfName(newName));

            var resultMessage = sourceStream != null
                ? $"Assigned shared FontFile2 from resource {sourceName} for \"{canonical.CanonicalName}\" and updated font name to \"{newName}\"."
                : $"Removed FontFile2 for \"{canonical.CanonicalName}\" and updated font name to \"{newName}\".";
            new FontMergeLogEvent(2033, FontMergeLogLevel.Info, resultMessage).Log(logger);
        }
    }

    private static class SubsetFontDictionaryCleaner
    {
        public static void CleanTrueType(PdfDictionary fontDictionary)
        {
            fontDictionary.Remove(PdfName.FirstChar);
            fontDictionary.Remove(PdfName.LastChar);
            fontDictionary.Remove(PdfName.Widths);
            fontDictionary.Remove(PdfName.ToUnicode);

            var descriptor = fontDictionary.GetAsDictionary(PdfName.FontDescriptor);
            if (descriptor == null)
            {
                return;
            }

            descriptor.Remove(PdfName.FontFile);
            descriptor.Remove(PdfName.FontFile2);
            descriptor.Remove(PdfName.FontFile3);
        }

        public static void CleanType0(PdfDictionary fontDictionary)
        {
            fontDictionary.Remove(PdfName.ToUnicode);

            var cidFont = FontDescriptorUtilities.GetCidFontDictionary(fontDictionary);
            if (cidFont == null)
            {
                return;
            }

            cidFont.Remove(PdfName.W);

            var descriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
            if (descriptor == null)
            {
                return;
            }

            descriptor.Remove(PdfName.FontFile);
            descriptor.Remove(PdfName.FontFile2);
            descriptor.Remove(PdfName.FontFile3);
        }
    }

    private static class FontDescriptorUtilities
    {
        public static PdfDictionary? GetDescriptor(FontResourceEntry font)
        {
            return font.Kind switch
            {
                FontSubsetKind.TrueType => font.FontDictionary.GetAsDictionary(PdfName.FontDescriptor),
                FontSubsetKind.Type0Identity => GetCidFontDictionary(font.FontDictionary)?.GetAsDictionary(PdfName.FontDescriptor),
                _ => null
            };
        }

        public static PdfDictionary? GetCidFontDictionary(PdfDictionary fontDictionary)
        {
            var descendants = fontDictionary.GetAsArray(PdfName.DescendantFonts);
            return descendants?.GetAsDictionary(0);
        }

        public static (PdfStream? Stream, string SourceName) GetPreferredFontFile2(IReadOnlyList<FontResourceEntry> fonts)
        {
            foreach (var font in fonts)
            {
                var descriptor = GetDescriptor(font);
                var stream = descriptor?.GetAsStream(PdfName.FontFile2);
                if (stream != null)
                {
                    return (stream, font.ResourceName.GetValue());
                }
            }

            return (null, string.Empty);
        }

        public static PdfStream CloneFontFile(PdfStream source, PdfDocument? document)
        {
            var bytes = source.GetBytes(true);
            var stream = new PdfStream(bytes);
            if (document != null)
            {
                stream.MakeIndirect(document);
            }

            return stream;
        }
    }

    private sealed class GlyphMergeMap
    {
        private GlyphMergeMap(Dictionary<int, GlyphMergeEntry> entries)
        {
            Entries = entries;
        }

        public IReadOnlyDictionary<int, GlyphMergeEntry> Entries { get; }

        public static GlyphMergeMap Build(IReadOnlyList<FontResourceEntry> fonts)
        {
            var map = new Dictionary<int, GlyphMergeEntry>();
            foreach (var font in fonts)
            {
                var resourceName = font.ResourceName.GetValue();
                foreach (var code in font.EncounteredCodes)
                {
                    if (!map.TryGetValue(code, out var entry))
                    {
                        entry = new GlyphMergeEntry();
                        map[code] = entry;
                    }

                    if (font.TryGetWidth(code, out var width))
                    {
                        entry.AddWidth(resourceName, width);
                    }

                    if (font.TryGetUnicode(code, out var unicode) && unicode != null)
                    {
                        entry.AddUnicode(resourceName, unicode);
                    }
                }
            }

            return new GlyphMergeMap(map);
        }
    }

    private sealed class GlyphMergeEntry
    {
        private readonly List<(string FontResource, float Width)> widthEntries = new();
        private readonly Dictionary<float, HashSet<string>> widthsByValue = new();
        private readonly List<(string FontResource, string Unicode)> unicodeEntries = new();
        private readonly Dictionary<string, HashSet<string>> unicodeByValue = new();

        public bool TryGetPreferredWidth(string canonicalResource, out float width)
        {
            foreach (var entry in widthEntries)
            {
                if (entry.FontResource.Equals(canonicalResource, StringComparison.Ordinal))
                {
                    width = entry.Width;
                    return true;
                }
            }

            if (widthEntries.Count > 0)
            {
                width = widthEntries[0].Width;
                return true;
            }

            width = 0f;
            return false;
        }

        public bool TryGetPreferredUnicode(string canonicalResource, out string? unicode)
        {
            foreach (var entry in unicodeEntries)
            {
                if (entry.FontResource.Equals(canonicalResource, StringComparison.Ordinal))
                {
                    unicode = entry.Unicode;
                    return true;
                }
            }

            if (unicodeEntries.Count > 0)
            {
                unicode = unicodeEntries[0].Unicode;
                return true;
            }

            unicode = null;
            return false;
        }

        public bool HasWidthConflict => widthsByValue.Count > 1;

        public bool HasUnicodeConflict => unicodeByValue.Count > 1;

        public string BuildWidthConflictDescription()
        {
            return string.Join("; ", widthsByValue.Select(pair => $"{pair.Key.ToString(CultureInfo.InvariantCulture)}: {string.Join(",", pair.Value)}"));
        }

        public string BuildUnicodeConflictDescription()
        {
            return string.Join("; ", unicodeByValue.Select(pair => $"{pair.Key}: {string.Join(",", pair.Value)}"));
        }

        public void AddWidth(string fontResource, float width)
        {
            widthEntries.Add((fontResource, width));
            if (!widthsByValue.TryGetValue(width, out var fonts))
            {
                fonts = new HashSet<string>(StringComparer.Ordinal);
                widthsByValue[width] = fonts;
            }

            fonts.Add(fontResource);
        }

        public void AddUnicode(string fontResource, string unicode)
        {
            unicodeEntries.Add((fontResource, unicode));
            if (!unicodeByValue.TryGetValue(unicode, out var fonts))
            {
                fonts = new HashSet<string>(StringComparer.Ordinal);
                unicodeByValue[unicode] = fonts;
            }

            fonts.Add(fontResource);
        }
    }

    private static class ToUnicodeCMapWriter
    {
        public static PdfStream Create(Dictionary<int, string> map, bool isCidFont, PdfDocument? document)
        {
            var builder = new StringBuilder();
            builder.AppendLine("/CIDInit /ProcSet findresource begin");
            builder.AppendLine("12 dict begin");
            builder.AppendLine("begincmap");
            builder.AppendLine("/CMapName /Adobe-Identity-UCS def");
            builder.AppendLine("/CMapType 2 def");
            builder.AppendLine("1 begincodespacerange");
            builder.AppendLine(isCidFont ? "<0000> <FFFF>" : "<00> <FF>");
            builder.AppendLine("endcodespacerange");

            var ordered = map.OrderBy(pair => pair.Key).ToList();
            var index = 0;
            while (index < ordered.Count)
            {
                var chunkSize = Math.Min(100, ordered.Count - index);
                builder.Append(chunkSize.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine(" beginbfchar");

                for (var i = 0; i < chunkSize; i++)
                {
                    var entry = ordered[index + i];
                    var source = EncodeSource(entry.Key, isCidFont);
                    var destination = EncodeUnicode(entry.Value);
                    builder.Append('<');
                    builder.Append(source);
                    builder.Append("> <");
                    builder.Append(destination);
                    builder.AppendLine(">");
                }

                builder.AppendLine("endbfchar");
                index += chunkSize;
            }

            builder.AppendLine("endcmap");
            builder.AppendLine("CMapName currentdict /CMap defineresource pop");
            builder.AppendLine("end");
            builder.AppendLine("end");

            var bytes = Encoding.ASCII.GetBytes(builder.ToString());
            var stream = new PdfStream(bytes);
            if (document != null)
            {
                stream.MakeIndirect(document);
            }

            return stream;
        }

        private static string EncodeSource(int code, bool isCidFont)
        {
            var hex = code.ToString("X");
            if (hex.Length % 2 == 1)
            {
                hex = "0" + hex;
            }

            var minimumLength = isCidFont ? 4 : 2;
            while (hex.Length < minimumLength)
            {
                hex = "0" + hex;
            }

            return hex;
        }

        private static string EncodeUnicode(string value)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(value);
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("X2"));
            }

            return builder.ToString();
        }
    }

    private enum FontSubsetKind
    {
        Unknown,
        Type0Identity,
        TrueType
    }

    private static class HexUtilities
    {
        public static bool IsHexDigit(char ch)
        {
            return (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
        }

        public static int GetValue(char ch)
        {
            return ch switch
            {
                >= '0' and <= '9' => ch - '0',
                >= 'a' and <= 'f' => ch - 'a' + 10,
                >= 'A' and <= 'F' => ch - 'A' + 10,
                _ => 0
            };
        }

        public static byte[] ParseHex(ReadOnlySpan<char> span)
        {
            var result = new List<byte>();
            var index = 0;
            while (index < span.Length)
            {
                while (index < span.Length && char.IsWhiteSpace(span[index]))
                {
                    index++;
                }

                if (index >= span.Length)
                {
                    break;
                }

                if (!IsHexDigit(span[index]))
                {
                    break;
                }

                var high = GetValue(span[index++]);

                while (index < span.Length && char.IsWhiteSpace(span[index]))
                {
                    index++;
                }

                var low = 0;
                if (index < span.Length && IsHexDigit(span[index]))
                {
                    low = GetValue(span[index++]);
                }

                result.Add((byte)((high << 4) | low));
            }

            return result.ToArray();
        }

        public static int? ParseHexInt(string token)
        {
            if (token.Length < 2)
            {
                return null;
            }

            var bytes = ParseHex(token.AsSpan(1, token.Length - 2));
            if (bytes.Length == 0)
            {
                return null;
            }

            var value = 0;
            foreach (var b in bytes)
            {
                value = (value << 8) | b;
            }

            return value;
        }

        public static byte[] ParseHexBytes(string token)
        {
            if (token.Length < 2)
            {
                return Array.Empty<byte>();
            }

            return ParseHex(token.AsSpan(1, token.Length - 2));
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

    private static long ReferenceKey(PdfIndirectReference reference)
    {
        unchecked
        {
            return ((long)reference.GetObjNumber() << 32) | (uint)reference.GetGenNumber();
        }
    }
}
