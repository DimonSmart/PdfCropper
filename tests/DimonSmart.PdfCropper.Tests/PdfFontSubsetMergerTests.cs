using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DimonSmart.PdfCropper;
using DimonSmart.PdfCropper.PdfFontSubsetMerger;
using iText.IO.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Utils;
using iText.Kernel.Font;
using Xunit;

using FontSubsetMerger = DimonSmart.PdfCropper.PdfFontSubsetMerger.PdfFontSubsetMerger;

namespace DimonSmart.PdfCropper.Tests;

public class PdfFontSubsetMergerTests
{
    private const string Type0PageText = "Type0 sample";
    private const string Type0FormText = "Type0 sample";
    private const string TrueTypeLineOne = "TrueType sample";
    private const string TrueTypeLineTwo = "TrueType sample";
    private static readonly string[] ExpectedSubsetCharacters = { "1", "2", "3", "a", "b", "c" };

    [Fact]
    public void MergeDuplicateSubsets_Type0Fonts_MergesResourcesAndFormXObjects()
    {
        var source = CreateType0Document();
        var logger = new TestPdfLogger();

        var merged = Merge(source, FontSubsetMergeOptions.CreateDefault(), logger);

        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.SubsetFontsMerged && e.Message.Contains("subset fonts", StringComparison.Ordinal));
        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.SubsetMergePrepared && e.Message.Contains("Type0", StringComparison.Ordinal));

        using var pdf = new PdfDocument(new PdfReader(new MemoryStream(merged)));
        var page = pdf.GetPage(1);
        var fonts = GetFontsDictionary(page);
        Assert.Single(fonts.KeySet());
        var canonicalResourceName = fonts.KeySet().Single().GetValue();

        var pageFontNames = ExtractFontResourceNames(page.GetContentBytes());
        Assert.Single(pageFontNames);
        Assert.Equal(canonicalResourceName, pageFontNames[0]);

        var xObjectResources = GetFormXObjectFonts(page);
        Assert.Single(xObjectResources);
        Assert.Equal(canonicalResourceName, xObjectResources[0].FontResourceName);
        Assert.All(xObjectResources, entry =>
        {
            var names = ExtractFontResourceNames(entry.Content);
            Assert.True(names.All(name => name == canonicalResourceName));
        });
        Assert.Equal(Type0PageText + Type0FormText, ExtractedText(page));
    }

    [Fact]
    public void MergeDuplicateSubsets_TrueTypeFonts_MergesToSingleResource()
    {
        var source = CreateTrueTypeDocument();
        var logger = new TestPdfLogger();

        var merged = Merge(source, FontSubsetMergeOptions.CreateDefault(), logger);

        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.SubsetFontsMerged && e.Message.Contains("subset fonts", StringComparison.Ordinal));
        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.SubsetMergePrepared && e.Message.Contains("TrueType", StringComparison.Ordinal));

        using var pdf = new PdfDocument(new PdfReader(new MemoryStream(merged)));
        var page = pdf.GetPage(1);
        var fonts = GetFontsDictionary(page);
        Assert.Single(fonts.KeySet());
        var canonicalResourceName = fonts.KeySet().Single().GetValue();

        var pageFontNames = ExtractFontResourceNames(page.GetContentBytes());
        Assert.True(pageFontNames.All(name => name == canonicalResourceName));
        Assert.Equal(TrueTypeLineOne + TrueTypeLineTwo, ExtractedText(page));
    }

    [Fact]
    public void MergeDuplicateSubsets_WithWidthConflict_LogsClusterSplitAndKeepsDuplicates()
    {
        var source = CreateTrueTypeDocument();
        var conflicting = IntroduceWidthConflict(source);
        var logger = new TestPdfLogger();

        var merged = Merge(conflicting, FontSubsetMergeOptions.CreateDefault(), logger);

        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.FontClustersSplit && e.Level == TestPdfLogger.InfoLevel);

        using var pdf = new PdfDocument(new PdfReader(new MemoryStream(merged)));
        var page = pdf.GetPage(1);
        var fonts = GetFontsDictionary(page);
        Assert.True(fonts.KeySet().Count >= 2);
    }

    [Fact]
    public void MergeDuplicateSubsets_WithUnsupportedCidFontType0_LogsWarningAndSkips()
    {
        var source = CreateType0Document();
        var withCidFont = InjectUnsupportedCidFontType0(source);
        var logger = new TestPdfLogger();

        var options = new FontSubsetMergeOptions
        {
            SupportedFontSubtypes = new HashSet<string>(StringComparer.Ordinal)
            {
                PdfName.Type0.GetValue(),
                PdfName.TrueType.GetValue(),
                PdfName.Type1.GetValue(),
                PdfName.CIDFontType2.GetValue()
            }
        };

        _ = Merge(withCidFont, options, logger);

        Assert.Contains(logger.Events, e => e.Id == FontMergeLogEventId.SubsetFontSkippedDueToUnsupportedSubtype && e.Level == TestPdfLogger.WarningLevel);
    }

    [Fact]
    public void MergeDuplicateSubsets_AfterMergingDocuments_DeduplicatesFontObjects()
    {
        var fontPath = GetFontPath();
        var firstPath = CreateSubsetPdfFile("abc", "123", fontPath);
        var secondPath = CreateSubsetPdfFile("123", "abc", fontPath);

        try
        {
            var combined = CombineDocuments(firstPath, secondPath);

            using (var beforeMerge = new PdfDocument(new PdfReader(new MemoryStream(combined))))
            {
                var fontObjectKeys = CollectFontObjectKeys(beforeMerge);
                Assert.Equal(2, fontObjectKeys.Count);

                var beforeCharacters = CollectFontCharacterMaps(beforeMerge);
                Assert.Equal(2, beforeCharacters.Count);

                var expectedCharacters = ExpectedSubsetCharacters
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToList();

                foreach (var characters in beforeCharacters.Values)
                {
                    Assert.Equal(ExpectedSubsetCharacters.Length, characters.Count);
                    Assert.Equal(expectedCharacters, characters.OrderBy(value => value, StringComparer.Ordinal).ToList());
                }
            }

            var logger = new TestPdfLogger();
            var merged = Merge(combined, FontSubsetMergeOptions.CreateDefault(), logger);

            using var afterMerge = new PdfDocument(new PdfReader(new MemoryStream(merged)));
            var mergedFontObjectKeys = CollectFontObjectKeys(afterMerge);
            Assert.Single(mergedFontObjectKeys);

            var afterCharacters = CollectFontCharacterMaps(afterMerge);
            Assert.Single(afterCharacters);
            var mergedCharacters = afterCharacters.Values.Single();
            Assert.Equal(ExpectedSubsetCharacters.Length, mergedCharacters.Count);
            Assert.Equal(
                ExpectedSubsetCharacters.OrderBy(value => value, StringComparer.Ordinal).ToList(),
                mergedCharacters.OrderBy(value => value, StringComparer.Ordinal).ToList());
        }
        finally
        {
            TryDelete(firstPath);
            TryDelete(secondPath);
        }
    }

    private static byte[] CreateType0Document()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            var fontPath = GetFontPath();
            var outerFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
            outerFont.SetSubset(true);

            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.BeginText();
            canvas.SetFontAndSize(outerFont, 14);
            canvas.MoveText(36, 760);
            canvas.ShowText(Type0PageText);
            canvas.EndText();

            using var formSourceStream = new MemoryStream();
            using (var formWriter = new PdfWriter(formSourceStream))
            using (var formPdf = new PdfDocument(formWriter))
            {
                var formPage = formPdf.AddNewPage(PageSize.A4);
                var formFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
                formFont.SetSubset(true);

                var formCanvas = new PdfCanvas(formPage);
                formCanvas.BeginText();
                formCanvas.SetFontAndSize(formFont, 12);
                formCanvas.MoveText(36, 740);
                formCanvas.ShowText(Type0FormText);
                formCanvas.EndText();
            }

            using var formReader = new PdfReader(new MemoryStream(formSourceStream.ToArray()));
            using var formDocument = new PdfDocument(formReader);
            var form = formDocument.GetPage(1).CopyAsFormXObject(pdf);
            canvas.SaveState();
            canvas.AddXObjectAt(form, 36, 700);
            canvas.RestoreState();
        }

        return stream.ToArray();
    }

    private static byte[] CreateTrueTypeDocument()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var fontPath = GetFontPath();
            var firstFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.WINANSI);
            var secondFont = PdfFontFactory.CreateFont(fontPath, PdfEncodings.WINANSI);
            firstFont.SetSubset(true);
            secondFont.SetSubset(true);

            var canvas = new PdfCanvas(page);
            canvas.BeginText();
            canvas.SetFontAndSize(firstFont, 12);
            canvas.MoveText(36, 760);
            canvas.ShowText(TrueTypeLineOne);
            canvas.EndText();

            canvas.BeginText();
            canvas.SetFontAndSize(secondFont, 12);
            canvas.MoveText(36, 730);
            canvas.ShowText(TrueTypeLineTwo);
            canvas.EndText();
        }

        return stream.ToArray();
    }

    private static byte[] IntroduceWidthConflict(byte[] pdfBytes)
    {
        using var result = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(new MemoryStream(pdfBytes)), new PdfWriter(result)))
        {
            var page = pdf.GetPage(1);
            var fonts = GetFontsDictionary(page);
            var names = fonts.KeySet().ToList();
            if (names.Count < 2)
            {
                throw new InvalidOperationException("Expected at least two font resources.");
            }

            var conflictFont = fonts.GetAsDictionary(names[1]);
            var widths = conflictFont?.GetAsArray(PdfName.Widths);
            if (conflictFont == null || widths == null)
            {
                throw new InvalidOperationException("Missing widths array for TrueType font.");
            }

            var number = widths.GetAsNumber(0);
            if (number == null)
            {
                throw new InvalidOperationException("Widths array is empty.");
            }

            widths.Set(0, new PdfNumber(number.FloatValue() + 120f));
        }

        return result.ToArray();
    }

    private static byte[] InjectUnsupportedCidFontType0(byte[] pdfBytes)
    {
        using var result = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(new MemoryStream(pdfBytes)), new PdfWriter(result)))
        {
            var page = pdf.GetPage(1);
            var resources = page.GetResources()?.GetPdfObject() ?? new PdfDictionary();
            var fonts = resources.GetAsDictionary(PdfName.Font) ?? new PdfDictionary();
            resources.Put(PdfName.Font, fonts);

            var cidFont = new PdfDictionary();
            cidFont.Put(PdfName.Subtype, PdfName.CIDFontType0);
            cidFont.Put(PdfName.BaseFont, new PdfName("ABCDEF+DummyCID"));
            fonts.Put(new PdfName("Z9"), cidFont);
        }

        return result.ToArray();
    }

    private static byte[] Merge(byte[] source, FontSubsetMergeOptions options, TestPdfLogger logger)
    {
        using var result = new MemoryStream();
        using (var pdf = new PdfDocument(new PdfReader(new MemoryStream(source)), new PdfWriter(result)))
        {
            FontSubsetMerger.MergeDuplicateSubsets(pdf, options, logger);
        }

        return result.ToArray();
    }

    private static byte[] CombineDocuments(string firstPath, string secondPath)
    {
        using var result = new MemoryStream();
        using (var writer = new PdfWriter(result))
        using (var output = new PdfDocument(writer))
        {
            var merger = new PdfMerger(output);

            foreach (var path in new[] { firstPath, secondPath })
            {
                using var reader = new PdfReader(path);
                using var document = new PdfDocument(reader);
                merger.Merge(document, 1, document.GetNumberOfPages());
            }
        }

        return result.ToArray();
    }

    private static PdfDictionary GetFontsDictionary(PdfPage page)
    {
        var resources = page.GetResources()?.GetPdfObject();
        return resources?.GetAsDictionary(PdfName.Font) ?? new PdfDictionary();
    }

    private static HashSet<long> CollectFontObjectKeys(PdfDocument pdfDocument)
    {
        var result = new HashSet<long>();
        var pageCount = pdfDocument.GetNumberOfPages();

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            var page = pdfDocument.GetPage(pageIndex);
            var fonts = GetFontsDictionary(page);

            foreach (var name in fonts.KeySet())
            {
                var fontDictionary = fonts.GetAsDictionary(name);
                var reference = fontDictionary?.GetIndirectReference();
                if (reference == null)
                {
                    continue;
                }

                result.Add(GetReferenceKey(reference));
            }
        }

        return result;
    }

    private static Dictionary<long, HashSet<string>> CollectFontCharacterMaps(PdfDocument pdfDocument)
    {
        var result = new Dictionary<long, HashSet<string>>();
        var pageCount = pdfDocument.GetNumberOfPages();

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            var page = pdfDocument.GetPage(pageIndex);
            var fonts = GetFontsDictionary(page);

            foreach (var name in fonts.KeySet())
            {
                var fontDictionary = fonts.GetAsDictionary(name);
                var reference = fontDictionary?.GetIndirectReference();
                if (fontDictionary == null || reference == null)
                {
                    continue;
                }

                var key = GetReferenceKey(reference);
                if (result.ContainsKey(key))
                {
                    continue;
                }

                result[key] = ExtractSubsetCharacters(fontDictionary);
            }
        }

        return result;
    }

    private static List<(string FontResourceName, byte[] Content)> GetFormXObjectFonts(PdfPage page)
    {
        var resources = page.GetResources()?.GetPdfObject();
        var xObjects = resources?.GetAsDictionary(PdfName.XObject);
        if (xObjects == null)
        {
            return new List<(string, byte[])>();
        }

        var result = new List<(string, byte[])>();
        foreach (var name in xObjects.KeySet())
        {
            var stream = xObjects.GetAsStream(name);
            if (stream == null)
            {
                continue;
            }

            var streamResources = stream.GetAsDictionary(PdfName.Resources);
            var fonts = streamResources?.GetAsDictionary(PdfName.Font);
            if (fonts == null || fonts.KeySet().Count == 0)
            {
                continue;
            }

            var resourceName = fonts.KeySet().Single().GetValue();
            result.Add((resourceName, stream.GetBytes(true)));
        }

        return result;
    }

    private static HashSet<string> ExtractSubsetCharacters(PdfDictionary fontDictionary)
    {
        var toUnicode = fontDictionary.GetAsStream(PdfName.ToUnicode);
        if (toUnicode == null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var bytes = toUnicode.GetBytes(true);
        if (bytes.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var content = Encoding.ASCII.GetString(bytes);
        return ParseToUnicodeCharacters(content);
    }

    private static HashSet<string> ParseToUnicodeCharacters(string cmapContent)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match block in Regex.Matches(
                     cmapContent,
                     @"\d+\s+beginbfchar\s+(?<entries>.*?)\s+endbfchar",
                     RegexOptions.Singleline))
        {
            var entries = block.Groups["entries"].Value;
            foreach (Match entry in Regex.Matches(entries, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", RegexOptions.Singleline))
            {
                result.Add(DecodeHexToString(entry.Groups[2].Value));
            }
        }

        foreach (Match block in Regex.Matches(
                     cmapContent,
                     @"\d+\s+beginbfrange\s+(?<entries>.*?)\s+endbfrange",
                     RegexOptions.Singleline))
        {
            var entries = block.Groups["entries"].Value;
            foreach (Match entry in Regex.Matches(
                         entries,
                         @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?<target>(?:<[^>]+>)|(?:\[(?:\s*<[^>]+>\s*)+\]))",
                         RegexOptions.Singleline))
            {
                var start = int.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var end = int.Parse(entry.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var target = entry.Groups["target"].Value;

                if (target.StartsWith("[", StringComparison.Ordinal))
                {
                    foreach (Match targetMatch in Regex.Matches(target, @"<([0-9A-Fa-f]+)>", RegexOptions.Singleline))
                    {
                        result.Add(DecodeHexToString(targetMatch.Groups[1].Value));
                    }

                    continue;
                }

                var baseHex = target.Trim('<', '>');
                var baseValue = int.Parse(baseHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var glyphCount = end - start + 1;

                for (var offset = 0; offset < glyphCount; offset++)
                {
                    var value = baseValue + offset;
                    var hex = value.ToString("X" + baseHex.Length, CultureInfo.InvariantCulture);
                    result.Add(DecodeHexToString(hex));
                }
            }
        }

        return result;
    }

    private static string DecodeHexToString(string hex)
    {
        if (hex.Length == 0)
        {
            return string.Empty;
        }

        if (hex.Length % 2 != 0)
        {
            hex = "0" + hex;
        }

        var bytes = new byte[hex.Length / 2];
        for (var index = 0; index < hex.Length; index += 2)
        {
            bytes[index / 2] = byte.Parse(hex.Substring(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return Encoding.BigEndianUnicode.GetString(bytes);
    }

    private static long GetReferenceKey(PdfIndirectReference reference)
    {
        return ((long)reference.GetObjNumber() << 32) | (uint)reference.GetGenNumber();
    }

    private static List<string> ExtractFontResourceNames(byte[] content)
    {
        if (content.Length == 0)
        {
            return new List<string>();
        }

        var text = Encoding.ASCII.GetString(content);
        var matches = Regex.Matches(text, @"/(\w+)\s+[\d\.\-]+\s+Tf");
        return matches.Select(match => match.Groups[1].Value).ToList();
    }

    private static string ExtractedText(PdfPage page)
    {
        var text = PdfTextExtractor.GetTextFromPage(page);
        return text.Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    private static string GetFontPath()
    {
        var environmentOverride = Environment.GetEnvironmentVariable("PDF_TEST_FONT_PATH");
        if (!string.IsNullOrWhiteSpace(environmentOverride) && File.Exists(environmentOverride))
        {
            return environmentOverride;
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "calibri.ttf",
            "arial.ttf",
            "LiberationSans-Regular.ttf",
            "LiberationSans.ttf",
            "DejaVuSans.ttf",
            "DejaVuSansCondensed.ttf",
            "FreeSans.ttf"
        };

        var searchRoots = GetFontDirectories().ToList();
        foreach (var directory in searchRoots)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in EnumerateFontFiles(directory))
            {
                var fileName = System.IO.Path.GetFileName(file);
                if (fileName != null && candidates.Contains(fileName))
                {
                    return file;
                }
            }
        }

        throw new FileNotFoundException(
            $"Test font not found. Looked for {string.Join(", ", candidates)} in {string.Join(", ", searchRoots)}.");
    }

    private static string CreateSubsetPdfFile(string visibleText, string additionalGlyphsText, string fontPath)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

        using (var writer = new PdfWriter(path))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var font = PdfFontFactory.CreateFont(fontPath, PdfEncodings.IDENTITY_H);
            font.SetSubset(true);

            var canvas = new PdfCanvas(page);
            canvas.BeginText();
            canvas.SetFontAndSize(font, 12);
            canvas.MoveText(36, 760);
            canvas.ShowText(visibleText);
            canvas.EndText();

            if (!string.IsNullOrEmpty(additionalGlyphsText))
            {
                canvas.BeginText();
                canvas.SetFontAndSize(font, 12);
                canvas.MoveText(36, -100);
                canvas.ShowText(additionalGlyphsText);
                canvas.EndText();
            }
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static IEnumerable<string> GetFontDirectories()
    {
        var result = new List<string>();

        var environmentDirectory = Environment.GetEnvironmentVariable("PDF_TEST_FONT_DIR");
        if (!string.IsNullOrWhiteSpace(environmentDirectory))
        {
            result.Add(environmentDirectory);
        }

        var specialFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (!string.IsNullOrWhiteSpace(specialFolder))
        {
            result.Add(specialFolder);
        }

        if (OperatingSystem.IsWindows())
        {
            var windowsDirectory = Environment.GetEnvironmentVariable("WINDIR");
            if (!string.IsNullOrEmpty(windowsDirectory))
            {
                result.Add(System.IO.Path.Combine(windowsDirectory, "Fonts"));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            result.Add("/System/Library/Fonts");
            result.Add("/Library/Fonts");
            var userLibrary = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Fonts");
            result.Add(userLibrary);
        }
        else if (OperatingSystem.IsLinux())
        {
            result.Add("/usr/share/fonts");
            result.Add("/usr/local/share/fonts");
            result.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".fonts"));
            result.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share", "fonts"));
        }

        return result
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(ExpandHomeDirectory)
            .Select(path => System.IO.Path.GetFullPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFontFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(current, "*.ttf", SearchOption.TopDirectoryOnly);
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(current);
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var directory in subdirectories)
            {
                pending.Push(directory);
            }
        }
    }

    private static string ExpandHomeDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '~')
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrEmpty(home))
        {
            return path;
        }

        return System.IO.Path.Combine(
            home,
            path.Substring(1).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
    }
}

public sealed class TestPdfLogger : IPdfCropLogger
{
    public const string InfoLevel = "Info";
    public const string WarningLevel = "Warning";
    public const string ErrorLevel = "Error";

    private readonly List<LogEvent> events = new();

    public IReadOnlyList<LogEvent> Events => events;

    public void LogInfo(string message) => Add(InfoLevel, message);

    public void LogWarning(string message) => Add(WarningLevel, message);

    public void LogError(string message) => Add(ErrorLevel, message);

    private void Add(string level, string message)
    {
        events.Add(new LogEvent(ParseId(message), level, message));
    }

    private static FontMergeLogEventId? ParseId(string message)
    {
        var prefix = "[FontSubsetMerge][";
        var start = message.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += prefix.Length;
        var end = message.IndexOf(']', start);
        if (end < 0)
        {
            return null;
        }

        var span = message[start..end];
        return Enum.TryParse<FontMergeLogEventId>(span, out var id) ? id : null;
    }

    public readonly record struct LogEvent(FontMergeLogEventId? Id, string Level, string Message);
}
