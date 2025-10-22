using DimonSmart.PdfCropper.PdfFontSubsetMerger;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Utils;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using FontSubsetMerger = DimonSmart.PdfCropper.PdfFontSubsetMerger.PdfFontSubsetMerger;
using Path = System.IO.Path;

namespace DimonSmart.PdfCropper.Tests;

public class PdfFontSubsetMergerTests
{
    private readonly ITestOutputHelper output;
    private const string Type0PageText = "Type0 sample";
    private const string Type0FormText = "Type0 sample";
    private const string TrueTypeLineOne = "TrueType sample";
    private const string TrueTypeLineTwo = "TrueType sample";

    public PdfFontSubsetMergerTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public static IEnumerable<object[]> MergeDuplicateSubsetsAfterMergingDocumentsData()
    {
        yield return new object[]
        {
            "Digits and letters",
            "abc",
            "123",
            "123",
            "abc",
            new[] { "1", "2", "3", "a", "b", "c" }
        };

        yield return new object[]
        {
            "Repeated upper and lower case letters",
            "ABCDa",
            "AaA",
            "BCDAa",
            "AAa",
            new[] { "A", "B", "C", "D", "a" }
        };

        yield return new object[]
        {
            "Digits with repetitions",
            "112233",
            string.Empty,
            "1",
            "23",
            new[] { "1", "2", "3" }
        };
    }

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

        var xObjectResources = GetFormXObjectFontResources(page);
        Assert.Single(xObjectResources);
        Assert.Equal(canonicalResourceName, xObjectResources[0].FontResourceName);
        Assert.All(xObjectResources, entry =>
        {
            var names = ExtractFontResourceNames(entry.Content);
            Assert.True(names.All(name => name == canonicalResourceName));
        });
        Assert.Equal(Type0PageText + Type0FormText, ExtractText(page));
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
        Assert.Equal(TrueTypeLineOne + TrueTypeLineTwo, ExtractText(page));
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
        var withCidFont = InjectUnsupportedCIDFontType0(source);
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

    [Theory]
    [MemberData(nameof(MergeDuplicateSubsetsAfterMergingDocumentsData))]
    public void MergeDuplicateSubsets_AfterMergingDocuments_DeduplicatesFontObjects(
        string scenario,
        string firstVisibleText,
        string firstAdditionalGlyphsText,
        string secondVisibleText,
        string secondAdditionalGlyphsText,
        string[] expectedSubsetCharacters)
    {
        var fontPath = GetFontPath();
        var firstPath = CreateSubsetPdfFile(firstVisibleText, firstAdditionalGlyphsText, fontPath);
        var secondPath = CreateSubsetPdfFile(secondVisibleText, secondAdditionalGlyphsText, fontPath);
        var firstDocumentSize = GetFileSize(firstPath);
        var secondDocumentSize = GetFileSize(secondPath);
        var expectedOrderedCharacters = expectedSubsetCharacters
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        try
        {
            WriteScenarioHeader(
                scenario,
                firstVisibleText,
                firstAdditionalGlyphsText,
                secondVisibleText,
                secondAdditionalGlyphsText,
                firstDocumentSize,
                secondDocumentSize,
                expectedOrderedCharacters);

            var combined = CombineDocuments(firstPath, secondPath);
            var combinedDocumentSize = combined.LongLength;
            LogDocumentSize("Combined document before merge", combinedDocumentSize);

            using (var beforeMerge = new PdfDocument(new PdfReader(new MemoryStream(combined))))
            {
                var fontObjectKeys = CollectFontObjectKeys(beforeMerge);
                Assert.Equal(2, fontObjectKeys.Count);

                var beforeCharacters = CollectFontCharacterMaps(beforeMerge);
                Assert.Equal(2, beforeCharacters.Count);
                LogFontCharacters("Before merge", beforeCharacters);

                foreach (var characters in beforeCharacters.Values)
                {
                    Assert.Equal(expectedSubsetCharacters.Length, characters.Count);
                    Assert.Equal(expectedOrderedCharacters, characters.OrderBy(value => value, StringComparer.Ordinal).ToList());
                }
            }

            var logger = new TestPdfLogger();
            var merged = Merge(combined, FontSubsetMergeOptions.CreateDefault(), logger);
            var mergedDocumentSize = merged.LongLength;
            LogDocumentSize("Combined document after merge", mergedDocumentSize);
            LogSizeReduction(combinedDocumentSize, mergedDocumentSize);
            Assert.True(
                mergedDocumentSize < combinedDocumentSize,
                $"Expected merged document to shrink. Before: {combinedDocumentSize} bytes, after: {mergedDocumentSize} bytes.");

            using var afterMerge = new PdfDocument(new PdfReader(new MemoryStream(merged)));
            var mergedFontObjectKeys = CollectFontObjectKeys(afterMerge);
            Assert.Single(mergedFontObjectKeys);

            var afterCharacters = CollectFontCharacterMaps(afterMerge);
            Assert.Single(afterCharacters);
            LogFontCharacters("After merge", afterCharacters);
            var mergedCharacters = afterCharacters.Values.Single();
            Assert.Equal(expectedSubsetCharacters.Length, mergedCharacters.Count);
            Assert.Equal(
                expectedOrderedCharacters,
                mergedCharacters.OrderBy(value => value, StringComparer.Ordinal).ToList());

            LogMergeLoggerEvents(logger);
        }
        finally
        {
            TryDelete(firstPath);
            TryDelete(secondPath);
        }
    }

    private void WriteScenarioHeader(
        string scenario,
        string firstVisibleText,
        string firstAdditionalGlyphsText,
        string secondVisibleText,
        string secondAdditionalGlyphsText,
        long firstDocumentSize,
        long secondDocumentSize,
        IReadOnlyCollection<string> expectedCharacters)
    {
        if (output == null) return;

        output.WriteLine($"Scenario: {scenario}");
        output.WriteLine($"  First document visible text: \"{firstVisibleText}\"");
        output.WriteLine($"  First document additional glyph text: \"{firstAdditionalGlyphsText}\"");
        output.WriteLine($"  First document size: {firstDocumentSize} bytes");
        output.WriteLine($"  Second document visible text: \"{secondVisibleText}\"");
        output.WriteLine($"  Second document additional glyph text: \"{secondAdditionalGlyphsText}\"");
        output.WriteLine($"  Second document size: {secondDocumentSize} bytes");
        output.WriteLine($"  Expected characters: {string.Join(", ", expectedCharacters)}");
    }

    private void LogFontCharacters(string stage, Dictionary<long, HashSet<string>> characters)
    {
        if (output == null)
        {
            return;
        }

        output.WriteLine($"{stage} font characters:");
        if (characters.Count == 0)
        {
            output.WriteLine("  <no fonts>");
            return;
        }

        foreach (var pair in characters.OrderBy(pair => pair.Key))
        {
            var orderedCharacters = pair.Value
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            output.WriteLine($"  Font {pair.Key}: {string.Join(", ", orderedCharacters)}");
        }
    }

    private void LogDocumentSize(string description, long size)
    {
        if (output == null)
        {
            return;
        }

        output.WriteLine($"{description}: {size} bytes");
    }

    private void LogSizeReduction(long before, long after)
    {
        if (output == null)
        {
            return;
        }

        var difference = before - after;
        if (before <= 0)
        {
            output.WriteLine("Size reduction: unavailable (non-positive baseline).");
            return;
        }

        var percent = difference * 100d / before;
        output.WriteLine(
            $"  Size reduction: {difference} bytes ({percent.ToString("F2", CultureInfo.InvariantCulture)}%)");
    }

    private void LogMergeLoggerEvents(TestPdfLogger logger)
    {
        if (output == null)
        {
            return;
        }

        output.WriteLine("Merge logger events:");
        if (logger.Events.Count == 0)
        {
            output.WriteLine("  <none>");
            return;
        }

        foreach (var logEvent in logger.Events)
        {
            output.WriteLine($"  [{logEvent.Level}] {logEvent.Message}");
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

    /// <summary>
    /// Deliberately introduces a width conflict into the second TrueType font by
    /// bumping the first width entry. This forces the merger to split clusters,
    /// simulating PDFs produced from different hmtx sources.
    /// </summary>
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

    /// <summary>
    /// Inserts a dummy CIDFontType0 entry into the page resources to verify that
    /// unsupported subtypes are logged and skipped by the merger.
    /// </summary>
    private static byte[] InjectUnsupportedCIDFontType0(byte[] pdfBytes)
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

    private static List<(string FontResourceName, byte[] Content)> GetFormXObjectFontResources(PdfPage page)
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
        return ParseToUnicodeCMapCharacters(content);
    }



    /// <summary>
    /// Matches an entire bfchar block like:
    /// 2 beginbfchar
    /// <01> <0041>
    /// <02> <0042>
    /// endbfchar
    /// </summary>
    private static readonly Regex s_bfcharBlock = new(@"\d+\s+beginbfchar\s+(?<entries>.*?)\s+endbfchar", RegexOptions.Singleline | RegexOptions.Compiled);

    /*
Matches an entire bfrange block. Examples:


(1) Simple ranges with a single base target:


3 beginbfrange
<10> <12> <0041>
<20> <22> <0061>
<30> <30> <007A>
endbfrange


(2) Ranges with an explicit target array (each target is mapped positionally):


2 beginbfrange
<20> <22> [ <0061> <0062> <0063> ]
<30> <31> [<0041><0042>]
endbfrange


Named group `entries` captures everything between beginbfrange/endbfrange.
*/
    private static readonly Regex s_bfrangeBlock = new(@"\d+\s+beginbfrange\s+(?<entries>.*?)\s+endbfrange", RegexOptions.Singleline | RegexOptions.Compiled);

    /*
Matches a single hex pair "<src> <dst>" (used inside bfchar or expanded bfrange arrays).
Examples (each line is a separate match):


<01> <0041> // Group 1: 01, Group 2: 0041
<000D> <000A> // Group 1: 000D, Group 2: 000A
<01><0041> // Also matches; whitespace between tokens is optional
*/
    private static readonly Regex s_hexPair = new(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>", RegexOptions.Singleline | RegexOptions.Compiled);

    /*
Matches a single hex token "<XXXX>". Useful for pulling all targets from an array.
Examples (each token is a separate match):


<0041>
[ <0061> <0062> <0063> ] // Matches <0061>, <0062>, <0063>
<00a0> <00A0> // Mixed case is accepted
*/
    private static readonly Regex s_hexArray = new(@"<([0-9A-Fa-f]+)>", RegexOptions.Singleline | RegexOptions.Compiled);

    private static HashSet<string> ParseToUnicodeCMapCharacters(string cmapContent)
    {
        // Parses glyph→Unicode mappings from ToUnicode CMap.
        // Only the forms used by iText test PDFs are handled (bfchar/bfrange).
        // This is intentionally minimal and test‑oriented; not a full CMap parser.
        var result = new HashSet<string>(StringComparer.Ordinal);


        foreach (Match block in s_bfcharBlock.Matches(cmapContent))
        {
            var entries = block.Groups["entries"].Value;
            foreach (Match entry in s_hexPair.Matches(entries))
                result.Add(DecodeHexToString(entry.Groups[2].Value));
        }

        foreach (Match block in s_bfrangeBlock.Matches(cmapContent))
        {
            var entries = block.Groups["entries"].Value;
            foreach (Match entry in Regex.Matches(entries,
            @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(?<target>(?:<[^>]+>)|(?:\[(?:\s*<[^>]+>\s*)+]))",
            RegexOptions.Singleline | RegexOptions.Compiled))
            {
                var start = int.Parse(entry.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var end = int.Parse(entry.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var target = entry.Groups["target"].Value;

                if (target.StartsWith("[", StringComparison.Ordinal))
                {
                    foreach (Match t in s_hexArray.Matches(target))
                        result.Add(DecodeHexToString(t.Groups[1].Value));
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

    /// <summary>
    /// Packs the PDF object number and generation into a stable 64‑bit key:
    /// (obj << 32) | gen. Useful for de‑duplicating font objects across pages.
    /// </summary>
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

    private static string ExtractText(PdfPage page)
    {
        var text = PdfTextExtractor.GetTextFromPage(page);
        return text.Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    private static string GetFontPath()
    {
        var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Lato-Regular.ttf");
        if (File.Exists(fontPath))
        {
            return fontPath;
        }

        throw new FileNotFoundException("Test font not found. Expected Lato-Regular.ttf in the test output directory.", fontPath);
    }

    /// <summary>
    /// In subset‑PDF generation, we draw "additionalGlyphsText" **off‑page** (y = -100)
    /// to ensure those glyphs are included in the subset without affecting visible text.
    /// </summary>
    /// <summary>
    /// Creates a PDF with font subsetting. The additionalGlyphsText is rendered off-page (y = -100)
    /// to include those glyphs in the subset without affecting visible content.
    /// </summary>
    private static string CreateSubsetPdfFile(string visibleText, string additionalGlyphsText, string fontPath)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");

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

    private static long GetFileSize(string path)
    {
        return new FileInfo(path).Length;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        }
        catch { /* ignore test cleanup failures */ }
    }

}

public sealed class TestPdfLogger : IPdfCropLogger
{
    public const string InfoLevel = "Info";
    public const string WarningLevel = "Warning";
    public const string ErrorLevel = "Error";

    private readonly List<LogEvent> events = new();

    public IReadOnlyList<LogEvent> Events => events;

    public Task LogInfoAsync(string message)
    {
        Add(InfoLevel, message);
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message)
    {
        Add(WarningLevel, message);
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message)
    {
        Add(ErrorLevel, message);
        return Task.CompletedTask;
    }

    private void Add(string level, string message)
    {
        events.Add(new LogEvent(ParseId(message), level, message));
    }

    /// <summary>
    /// Extracts a <see cref="FontMergeLogEventId"/> from a logger line in the format
    /// "[FontSubsetMerge][<EventId>] ...". Returns null when the prefix/closing bracket
    /// is missing or when the value cannot be parsed. This keeps tests tolerant to
    /// surrounding wording changes while still asserting the intended event ID.
    /// </summary>
    private static FontMergeLogEventId? ParseId(string message)
    {
        const string Prefix = "[FontSubsetMerge][";
        var start = message.IndexOf(Prefix, StringComparison.Ordinal);
        if (start < 0) return null;

        start += Prefix.Length;
        var end = message.IndexOf(']', start);
        if (end < 0) return null;

        var span = message.AsSpan(start, end - start);
        return Enum.TryParse<FontMergeLogEventId>(span, out var id) ? id : null;
    }

    public readonly record struct LogEvent(FontMergeLogEventId? Id, string Level, string Message);
}
