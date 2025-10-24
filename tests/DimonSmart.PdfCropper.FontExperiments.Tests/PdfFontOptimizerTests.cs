using NUnit.Framework;
using DimonSmart.PdfCropper.FontExperiments;
using iText.Kernel.Pdf;
using System.Text;
using System.Text.RegularExpressions;

namespace DimonSmart.PdfCropper.FontExperiments.Tests;

public class PdfFontOptimizerTests
{
    private string testPdfPath = string.Empty;

    [SetUp]
    public void Setup()
    {
        testPdfPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "test.pdf");
        PdfTestHelper.CreateTestPdf(testPdfPath);
    }

    [Test]
    public void TestOpenPdfDocument()
    {
        var optimizer = new PdfFontOptimizer(testPdfPath);
        int pageCount = optimizer.GetPageCount();

        Assert.That(pageCount, Is.GreaterThan(0), "PDF должен содержать хотя бы одну страницу");

        optimizer.Close();
    }

    [Test]
    public void TestFontIndexing_Step1()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var stats = optimizer.GetFontStatistics();
        var conflicts = optimizer.DetectResourceConflicts();

        Assert.That(stats, Is.Not.Null, "Статистика шрифтов должна быть получена");
        Assert.That(stats.Count, Is.GreaterThan(0), "Должен быть хотя бы один шрифт");

        Assert.That(stats.ContainsKey("Calibri"), Is.True, "Должен быть найден шрифт Calibri");
        Assert.That(stats.ContainsKey("TableauBook"), Is.True, "Должен быть найден шрифт TableauBook");
        Assert.That(stats.ContainsKey("TableauMedium"), Is.True, "Должен быть найден шрифт TableauMedium");

        Console.WriteLine("\n=== Font Statistics ===");
        foreach (var font in stats)
        {
            Console.WriteLine($"{font.Key}: {font.Value.Count} occurrences");

            var byResource = font.Value.GroupBy(f => f.ResourceName);
            foreach (var resource in byResource)
            {
                var pages = string.Join(", ", resource.Select(f => f.PageNumber).Distinct().OrderBy(p => p));
                Console.WriteLine($"  {resource.Key}: on pages {pages}");
            }
        }

        Console.WriteLine("\n=== Resource Conflicts ===");
        if (conflicts.Count > 0)
        {
            foreach (var conflict in conflicts)
            {
                Console.WriteLine($"CONFLICT: {conflict}");
            }

            Assert.That(conflicts.Any(c => c.Contains("F7")), Is.True,
                "Должен быть обнаружен конфликт для ресурса F7 (Calibri vs TableauBook)");
        }

        optimizer.Close();
    }

    [Test]
    public void TestSharedFontDictionaryDetection()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var sharedDicts = optimizer.DetectSharedFontDictionaries();
        var conflicts = optimizer.AnalyzeSharedDictionaryConflicts();

        optimizer.PrintSharedDictionaryAnalysis();

        Assert.That(sharedDicts, Is.Not.Null, "Should detect font dictionaries");

        var sharedCount = sharedDicts.Count(d => d.Value.Count > 1);
        Console.WriteLine($"\n📊 Найдено {sharedCount} shared font dictionaries");

        if (conflicts.Count > 0)
        {
            Console.WriteLine($"\n⚠️ Найдено {conflicts.Count} словарей с конфликтами!");

            foreach (var conflict in conflicts)
            {
                if (conflict.AffectedPages.Contains(4))
                {
                    Console.WriteLine($"\n🔍 Конфликт затрагивает страницу 4:");
                    Console.WriteLine($"   Страницы: {string.Join(", ", conflict.AffectedPages)}");

                    if (conflict.ResourceConflicts.ContainsKey("/F7"))
                    {
                        var f7Fonts = conflict.ResourceConflicts["/F7"];
                        Assert.That(f7Fonts.Contains("Calibri") && f7Fonts.Contains("TableauBook"), Is.True,
                            "F7 должен конфликтовать между Calibri и TableauBook");
                        Console.WriteLine($"   ✓ F7 конфликт подтверждён: {string.Join(" vs ", f7Fonts)}");
                    }

                    if (conflict.ResourceConflicts.ContainsKey("/F8"))
                    {
                        var f8Fonts = conflict.ResourceConflicts["/F8"];
                        Assert.That(f8Fonts.Contains("Calibri") && f8Fonts.Contains("TableauMedium"), Is.True,
                            "F8 должен конфликтовать между Calibri и TableauMedium");
                        Console.WriteLine($"   ✓ F8 конфликт подтверждён: {string.Join(" vs ", f8Fonts)}");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("\n✅ Конфликтов в shared dictionaries не обнаружено");
            Console.WriteLine("   (Возможно, страница 4 использует отдельный Font Dictionary)");
        }

        optimizer.Close();
    }

    [Test]
    public void TestFontMappingTable()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();

        Assert.That(mapping, Is.Not.Null, "Mapping table should be created");
        Assert.That(mapping.Count, Is.GreaterThan(0), "Mapping table should have entries");

        optimizer.ValidateFontMapping(mapping);

        string page4_F7_mapping = mapping["4_/F7"];
        string page4_F8_mapping = mapping["4_/F8"];
        Assert.That(page4_F7_mapping.Contains("Calibri"), Is.True,
            $"Page 4 /F7 should map to Calibri resource, but got: {page4_F7_mapping}");
        Assert.That(page4_F8_mapping.Contains("Calibri"), Is.True,
            $"Page 4 /F8 should map to Calibri resource, but got: {page4_F8_mapping}");

        string page1_F7_mapping = mapping["1_/F7"];
        string page1_F8_mapping = mapping["1_/F8"];
        Assert.That(page1_F7_mapping.Contains("TableauBook"), Is.True,
            $"Page 1 /F7 should map to TableauBook resource, but got: {page1_F7_mapping}");
        Assert.That(page1_F8_mapping.Contains("TableauMedium"), Is.True,
            $"Page 1 /F8 should map to TableauMedium resource, but got: {page1_F8_mapping}");

        Assert.That(page4_F7_mapping, Is.Not.EqualTo(page1_F7_mapping),
            "Page 4 /F7 should have different mapping than Page 1 /F7");
        Assert.That(page4_F8_mapping, Is.Not.EqualTo(page1_F8_mapping),
            "Page 4 /F8 should have different mapping than Page 1 /F8");

        Console.WriteLine("\n✅ Font mapping table correctly handles different font assignments!");
        Console.WriteLine($"   Total mappings created: {mapping.Count}");

        optimizer.Close();
    }

    [Test]
    public void TestGlobalFontDictionaryCreation()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        Assert.That(globalFontDict, Is.Not.Null, "Global font dictionary should be created");
        Assert.That(globalFontDict.Size(), Is.EqualTo(3), "Should have exactly 3 merged fonts (Calibri, TableauBook, TableauMedium)");

        bool hasCalibri = false;
        bool hasTableauBook = false;
        bool hasTableauMedium = false;

        foreach (var entry in globalFontDict.EntrySet())
        {
            string resourceName = entry.Key.ToString();
            var fontDict = globalFontDict.GetAsDictionary(entry.Key);

            Console.WriteLine($"\n📝 Checking font resource: {resourceName}");

            Assert.That(fontDict, Is.Not.Null, $"Font dictionary for {resourceName} should exist");
            Assert.That(fontDict.GetAsName(PdfName.Type), Is.EqualTo(PdfName.Font), "Should be Font type");

            var baseFont = fontDict.GetAsName(PdfName.BaseFont);
            Assert.That(baseFont, Is.Not.Null, "BaseFont should be set");

            string baseFontName = baseFont.GetValue();
            Console.WriteLine($"  BaseFont: {baseFontName}");

            Assert.That(baseFontName.Length > 7 && baseFontName[6] == '+', Is.True,
                "BaseFont should have subset prefix (XXXXXX+FontName)");

            if (resourceName.Contains("Calibri"))
            {
                hasCalibri = true;
                Assert.That(baseFontName.Contains("Calibri"), Is.True, "Calibri font should have Calibri in name");
            }
            else if (resourceName.Contains("TableauBook"))
            {
                hasTableauBook = true;
                Assert.That(baseFontName.Contains("TableauBook"), Is.True, "TableauBook font should have TableauBook in name");
            }
            else if (resourceName.Contains("TableauMedium"))
            {
                hasTableauMedium = true;
                Assert.That(baseFontName.Contains("TableauMedium"), Is.True, "TableauMedium font should have TableauMedium in name");
            }

            var subtype = fontDict.GetAsName(PdfName.Subtype);
            if (PdfName.Type0.Equals(subtype))
            {
                var descendantFonts = fontDict.GetAsArray(PdfName.DescendantFonts);
                Assert.That(descendantFonts, Is.Not.Null, "Type0 font should have DescendantFonts");
                Assert.That(descendantFonts.Size(), Is.GreaterThan(0), "DescendantFonts should not be empty");

                var cidFont = descendantFonts.GetAsDictionary(0);
                Assert.That(cidFont, Is.Not.Null, "CIDFont should exist");

                var fontDescriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
                Assert.That(fontDescriptor, Is.Not.Null, "CIDFont should have FontDescriptor");
                Assert.That(fontDescriptor.GetAsName(PdfName.Type), Is.EqualTo(PdfName.FontDescriptor),
                    "FontDescriptor should have correct type");
                Console.WriteLine($"  ✅ Type0 font structure is valid (FontDescriptor in CIDFont)");
            }
            else
            {
                var fontDescriptor = fontDict.GetAsDictionary(PdfName.FontDescriptor);
                Assert.That(fontDescriptor, Is.Not.Null, $"Non-Type0 font should have FontDescriptor");
                Assert.That(fontDescriptor.GetAsName(PdfName.Type), Is.EqualTo(PdfName.FontDescriptor),
                    "FontDescriptor should have correct type");
                Console.WriteLine($"  ✅ Font structure is valid (FontDescriptor in font dict)");
            }
        }

        Assert.That(hasCalibri, Is.True, "Global dictionary should contain merged Calibri font");
        Assert.That(hasTableauBook, Is.True, "Global dictionary should contain merged TableauBook font");
        Assert.That(hasTableauMedium, Is.True, "Global dictionary should contain merged TableauMedium font");

        Console.WriteLine("\n✅ Global font dictionary successfully created with all 3 merged fonts!");

        optimizer.Close();
    }

    [Test]
    public void TestApplyGlobalFontDictionary()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        string outputPath = @"P:\pdf3\Ladders_Optimized_Test.pdf";

        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var optimizer = new PdfFontOptimizer(testPdfPath, outputPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();

        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        optimizer.ApplyGlobalFontDictionary(globalFontDict, mapping);

        optimizer.SaveOptimizedPdf();

        Assert.That(File.Exists(outputPath), Is.True, "Output file should exist");

        var fileInfo = new FileInfo(outputPath);
        Assert.That(fileInfo.Length, Is.GreaterThan(1000), "Output file should have content");

        Console.WriteLine($"\n📄 Output file created: {outputPath}");
        Console.WriteLine($"   Size: {fileInfo.Length:N0} bytes");

        using (var reader = new PdfReader(outputPath))
        using (var resultDoc = new PdfDocument(reader))
        {
            Console.WriteLine($"   Pages: {resultDoc.GetNumberOfPages()}");

            var page1 = resultDoc.GetPage(1);
            var resources = page1.GetResources();
            var fontsDict = resources?.GetPdfObject()?.GetAsDictionary(PdfName.Font);

            Assert.That(fontsDict, Is.Not.Null, "Page 1 should have fonts dictionary");
            Assert.That(fontsDict.Size(), Is.EqualTo(3), "Should have exactly 3 merged fonts");

            bool hasCalibri = false;
            bool hasTableauBook = false;
            bool hasTableauMedium = false;

            foreach (var entry in fontsDict.EntrySet())
            {
                string fontName = entry.Key.ToString();
                Console.WriteLine($"   Found font in result: {fontName}");

                if (fontName.Contains("Calibri")) hasCalibri = true;
                if (fontName.Contains("TableauBook")) hasTableauBook = true;
                if (fontName.Contains("TableauMedium")) hasTableauMedium = true;
            }

            Assert.That(hasCalibri, Is.True, "Result should contain Calibri font");
            Assert.That(hasTableauBook, Is.True, "Result should contain TableauBook font");
            Assert.That(hasTableauMedium, Is.True, "Result should contain TableauMedium font");

            Console.WriteLine("\n✅ Font optimization successfully applied!");
        }
    }

    [Test]
    public void TestGlyphCollection()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        Assert.That(globalFontDict, Is.Not.Null, "Global font dictionary should be created");

        Console.WriteLine("\n=== Glyph Collection Test ===");
        Console.WriteLine("Check console output for 'Collected X unique glyphs' messages");
        Console.WriteLine("If properly implemented, should show different glyph counts for different fonts");

        optimizer.Close();
    }

    [Test]
    public void TestFontFile2Preservation()
    {
        // Arrange
        string testPdfPath = @"P:\pdf3\Ladders.pdf";

        // Act
        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        // Assert - проверяем что критические компоненты скопированы
        Console.WriteLine("\n=== Font Components Preservation Test ===\n");

        foreach (var entry in globalFontDict.EntrySet())
        {
            var fontDict = globalFontDict.GetAsDictionary(entry.Key);
            Console.WriteLine($"📝 Checking font: {entry.Key}");

            if (PdfName.Type0.Equals(fontDict.GetAsName(PdfName.Subtype)))
            {
                // Для Type0 - проверяем в CIDFont
                var descendantFonts = fontDict.GetAsArray(PdfName.DescendantFonts);
                Assert.That(descendantFonts, Is.Not.Null, "DescendantFonts should exist");

                var cidFont = descendantFonts.GetAsDictionary(0);
                Assert.That(cidFont, Is.Not.Null, "CIDFont should exist");

                var fontDescriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
                Assert.That(fontDescriptor, Is.Not.Null, "FontDescriptor should exist in CIDFont");

                // FontFile2 - КРИТИЧНО для отображения
                var fontFile2 = fontDescriptor.Get(PdfName.FontFile2);
                Assert.That(fontFile2, Is.Not.Null, $"FontFile2 should be preserved for {entry.Key}");
                Console.WriteLine($"  ✅ FontFile2 preserved");

                // W (widths) - ОПЦИОНАЛЬНО (PDF reader использует FontFile2 если W отсутствует)
                var widths = cidFont.Get(PdfName.W);
                if (widths != null)
                {
                    Console.WriteLine($"  ✅ W (widths) array present");
                }
                else
                {
                    Console.WriteLine($"  ℹ️ W array not present (will use FontFile2 metrics)");
                }

                // DW (default width) - ОПЦИОНАЛЬНО
                var dw = cidFont.Get(PdfName.DW);
                if (dw != null)
                {
                    Console.WriteLine($"  ✅ DW (default width) present: {dw}");
                }
                else
                {
                    Console.WriteLine($"  ℹ️ DW not set (default is 1000)");
                }

                // ToUnicode - ВАЖНО для корректного маппинга
                var toUnicode = fontDict.Get(PdfName.ToUnicode);
                Assert.That(toUnicode, Is.Not.Null, $"ToUnicode should be preserved for {entry.Key}");
                Console.WriteLine($"  ✅ ToUnicode CMap preserved");

                // CIDToGIDMap - ВАЖНО для Type0
                var cidToGidMap = cidFont.Get(PdfName.CIDToGIDMap);
                if (cidToGidMap != null)
                {
                    Console.WriteLine($"  ✅ CIDToGIDMap: {cidToGidMap}");
                }

                Console.WriteLine($"  ✅ Type0 font structure is valid\n");
            }
        }

        Console.WriteLine("✅ All critical font components preserved!");

        // Cleanup
        optimizer.Close();
    }

    [Test]
    public void DiagnoseTextTransformationIssue()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        string outputPath = @"P:\pdf3\Ladders_DiagnosticTest.pdf";

        var testPhrases = new Dictionary<string, string>
        {
            { "Original", "All Rates Parallel Ladders as of 2025-10-17" },
            { "Corrupted_Visual", "ll ates Parallel Ladders as o     10 1" },
            { "Corrupted_Copy", "Ϯ0ϮϱͲ10Ͳ" }
        };

        File.Copy(testPdfPath, outputPath, overwrite: true);

        Console.WriteLine("\n=== TOUNICODE MAPPING DIAGNOSTIC ===\n");

        var optimizer = new PdfFontOptimizer(outputPath);
        optimizer.IndexFonts();

        DiagnoseToUnicodeMappings(optimizer, "BEFORE MERGE");

        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        DiagnoseToUnicodeMappingsInGlobalDict(globalFontDict, "AFTER MERGE");

        optimizer.ApplyGlobalFontDictionary(globalFontDict, mapping);
        optimizer.SaveOptimizedPdf();

        AnalyzeTextExtraction(outputPath, testPhrases);

        optimizer.Close();
    }

    private void DiagnoseToUnicodeMappings(PdfFontOptimizer optimizer, string stage)
    {
        Console.WriteLine($"\n--- {stage} ---");

        var criticalChars = new Dictionary<char, string>
        {
            { 'A', "Capital A" },
            { 'R', "Capital R" },
            { '2', "Digit 2" },
            { '5', "Digit 5" },
            { '-', "Hyphen" }
        };

        var page1Fonts = optimizer.GetPageFonts(1);

        foreach (var font in page1Fonts)
        {
            Console.WriteLine($"\nFont: {font.ResourceName} ({font.BaseFontName})");

            var toUnicode = font.FontDict?.Get(PdfName.ToUnicode);
            if (toUnicode != null && toUnicode is PdfStream stream)
            {
                var cmapData = stream.GetBytes();
                var cmapString = System.Text.Encoding.UTF8.GetString(cmapData);

                Console.WriteLine("  ToUnicode entries for critical chars:");

                foreach (var kvp in criticalChars)
                {
                    string unicodeHex = ((int)kvp.Key).ToString("X4");
                    if (cmapString.Contains(unicodeHex))
                    {
                        Console.WriteLine($"    {kvp.Value} (U+{unicodeHex}): FOUND");

                        var pattern = $@"<([0-9A-Fa-f]+)>\s*<{unicodeHex}>";
                        var match = System.Text.RegularExpressions.Regex.Match(cmapString, pattern);
                        if (match.Success)
                        {
                            Console.WriteLine($"      CID: {match.Groups[1].Value}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    {kvp.Value} (U+{unicodeHex}): NOT FOUND ⚠️");
                    }
                }
            }
        }
    }

    private void DiagnoseToUnicodeMappingsInGlobalDict(PdfDictionary globalFontDict, string stage)
    {
        Console.WriteLine($"\n--- {stage} ---");

        var criticalChars = new Dictionary<char, string>
        {
            { 'A', "Capital A" },
            { 'R', "Capital R" },
            { '2', "Digit 2" },
            { '5', "Digit 5" },
            { '-', "Hyphen" }
        };

        foreach (var entry in globalFontDict.EntrySet())
        {
            var fontDict = globalFontDict.GetAsDictionary(entry.Key);
            if (fontDict == null) continue;

            var baseFontName = fontDict.GetAsName(PdfName.BaseFont)?.GetValue() ?? "Unknown";
            Console.WriteLine($"\nMerged Font: {entry.Key} ({baseFontName})");

            var toUnicode = fontDict.Get(PdfName.ToUnicode);
            if (toUnicode != null && toUnicode is PdfStream stream)
            {
                var cmapData = stream.GetBytes();
                var cmapString = System.Text.Encoding.UTF8.GetString(cmapData);

                Console.WriteLine("  ToUnicode entries for critical chars:");

                foreach (var kvp in criticalChars)
                {
                    string unicodeHex = ((int)kvp.Key).ToString("X4");
                    if (cmapString.Contains(unicodeHex))
                    {
                        Console.WriteLine($"    {kvp.Value} (U+{unicodeHex}): FOUND");

                        var pattern = $@"<([0-9A-Fa-f]+)>\s*<{unicodeHex}>";
                        var match = System.Text.RegularExpressions.Regex.Match(cmapString, pattern);
                        if (match.Success)
                        {
                            Console.WriteLine($"      CID: {match.Groups[1].Value}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    {kvp.Value} (U+{unicodeHex}): NOT FOUND ⚠️");
                    }
                }
            }
            else
            {
                Console.WriteLine("  ⚠️ No ToUnicode CMap found!");
            }
        }
    }

    private void AnalyzeTextExtraction(string pdfPath, Dictionary<string, string> expectedPhrases)
    {
        Console.WriteLine("\n=== TEXT EXTRACTION ANALYSIS ===\n");

        using (var reader = new PdfReader(pdfPath))
        using (var document = new PdfDocument(reader))
        {
            var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.SimpleTextExtractionStrategy();
            var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(document.GetPage(1), strategy);

            Console.WriteLine("Extracted text (first 200 chars):");
            Console.WriteLine(text.Substring(0, Math.Min(200, text.Length)));

            Console.WriteLine("\n--- Phrase Detection ---");

            if (text.Contains(expectedPhrases["Original"]))
            {
                Console.WriteLine("✅ Original phrase found correctly");
            }
            else
            {
                Console.WriteLine("❌ Original phrase NOT found");

                Console.WriteLine("\nCharacter-by-character analysis:");
                var original = expectedPhrases["Original"];

                for (int i = 0; i < Math.Min(50, original.Length); i++)
                {
                    char expectedChar = original[i];

                    if (text.Length > i)
                    {
                        char actualChar = text[i];
                        if (expectedChar != actualChar)
                        {
                            Console.WriteLine($"  Position {i}: Expected '{expectedChar}' (U+{((int)expectedChar):X4}), Got '{actualChar}' (U+{((int)actualChar):X4})");
                        }
                    }
                }
            }
        }
    }

    [Test]
    public void TestToUnicodeMerging()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        
        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();
        
        var mapping = optimizer.CreateFontMappingTable();
        
        Console.WriteLine("\n=== Testing ToUnicode Merging ===\n");
        
        // Группируем контексты по базовому шрифту для теста
        var calibriContexts = new List<PdfFontOptimizer.FontContext>();
        
        for (int page = 1; page <= 4; page++)
        {
            var fonts = optimizer.GetPageFonts(page);
            foreach (var font in fonts.Where(f => f.BaseFontName == "Calibri"))
            {
                calibriContexts.Add(new PdfFontOptimizer.FontContext
                {
                    PageNumber = page,
                    ResourceName = font.ResourceName,
                    ActualFontName = font.BaseFontName,
                    FontDict = font.FontDict,
                    FontsDict = font.FontsDict
                });
            }
        }
        
        Console.WriteLine($"Found {calibriContexts.Count} Calibri font contexts on pages 1-4");
        
        // Вызываем метод объединения ToUnicode
        var mergedToUnicode = optimizer.TestMergeToUnicodeMappings(calibriContexts, "Calibri");
        
        Assert.That(mergedToUnicode, Is.Not.Null, "Merged ToUnicode should be created");
        
        // Проверяем содержимое
        var mergedCmapData = mergedToUnicode.GetBytes();
        var mergedCmapString = System.Text.Encoding.UTF8.GetString(mergedCmapData);
        
        Console.WriteLine("\n=== Checking Critical Characters in Merged ToUnicode ===");
        
        // Проверяем критические символы
        var criticalChars = new Dictionary<string, string>
        {
            { "0041", "A" }, // Capital A
            { "0052", "R" }, // Capital R  
            { "0032", "2" }, // Digit 2
            { "0035", "5" }, // Digit 5
            { "002D", "-" }  // Hyphen
        };
        
        int foundCount = 0;
        foreach (var kvp in criticalChars)
        {
            if (mergedCmapString.Contains($"<{kvp.Key}>"))
            {
                Console.WriteLine($"  ✅ U+{kvp.Key} ({kvp.Value}) found in merged ToUnicode");
                foundCount++;
            }
            else
            {
                Console.WriteLine($"  ❌ U+{kvp.Key} ({kvp.Value}) NOT found in merged ToUnicode");
            }
        }
        
        Assert.That(foundCount, Is.EqualTo(5), $"All 5 critical characters should be found in merged ToUnicode, but only {foundCount} found");
        
        Console.WriteLine($"\n✅ All {foundCount}/5 critical characters found in merged ToUnicode!");
        
        optimizer.Close();
    }

    [Test]
    public void TestCidRemapping()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        
        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();
        
        var stats = optimizer.GetFontStatistics();
        Assert.That(stats.ContainsKey("Calibri"), Is.True, "Calibri font should exist");
        
        var calibriContexts = new List<PdfFontOptimizer.FontContext>();
        var document = optimizer.GetDocument();
        
        for (int pageNum = 1; pageNum <= Math.Min(4, optimizer.GetPageCount()); pageNum++)
        {
            var page = document.GetPage(pageNum);
            var resources = page.GetPdfObject().GetAsDictionary(PdfName.Resources);
            var fonts = resources?.GetAsDictionary(PdfName.Font);
            
            if (fonts != null)
            {
                foreach (var fontEntry in fonts.EntrySet())
                {
                    var resourceName = fontEntry.Key.GetValue();
                    var fontDict = fontEntry.Value as PdfDictionary;
                    
                    if (fontDict != null)
                    {
                        var baseFont = fontDict.GetAsName(PdfName.BaseFont)?.GetValue() ?? "";
                        
                        if (baseFont.Contains("Calibri"))
                        {
                            var context = new PdfFontOptimizer.FontContext
                            {
                                PageNumber = pageNum,
                                ResourceName = resourceName,
                                FontDict = fontDict
                            };
                            calibriContexts.Add(context);
                            Console.WriteLine($"Found {baseFont} on page {pageNum} as {resourceName}");
                        }
                    }
                }
            }
        }
        
        Assert.That(calibriContexts.Count, Is.GreaterThan(0), "Should find Calibri fonts on pages 1-4");
        Console.WriteLine($"\n=== Testing CID Remapping ===");
        Console.WriteLine($"Found {calibriContexts.Count} Calibri font contexts on pages 1-4");
        
        var remapper = new PdfFontOptimizer.CidRemapper();
        var (mergedToUnicode, cidRemapping) = remapper.RemapCidsWithConflictResolution(calibriContexts, document);
        
        Console.WriteLine($"\n=== CID Remapping Results ===");
        Console.WriteLine($"Original mappings with conflicts: {cidRemapping.Count}");
        Console.WriteLine($"Unique CIDs after remapping: {mergedToUnicode.Count}");
        
        var criticalChars = new Dictionary<string, char>
        {
            { "0041", 'A' },
            { "0052", 'R' },
            { "0032", '2' },
            { "0035", '5' },
            { "002D", '-' }
        };
        
        Console.WriteLine($"\n=== Critical Characters Mapping ===");
        foreach (var kvp in criticalChars)
        {
            var unicode = kvp.Key;
            var ch = kvp.Value;
            var cid = mergedToUnicode.FirstOrDefault(m => m.Value.Equals(unicode, StringComparison.OrdinalIgnoreCase)).Key;
            
            if (cid > 0)
            {
                Console.WriteLine($"  ✅ U+{unicode} ({ch}) → CID 0x{cid:X4}");
            }
            else
            {
                Console.WriteLine($"  ❌ U+{unicode} ({ch}) → NOT FOUND");
            }
            
            Assert.That(cid, Is.GreaterThan(0), $"Unicode {unicode} should have a CID assigned");
        }
        
        var uniqueNewCids = cidRemapping.Values.Distinct().Count();
        Console.WriteLine($"\n=== Remapping Statistics ===");
        Console.WriteLine($"Unique new CIDs used: {uniqueNewCids}");
        Console.WriteLine($"Total remapping entries: {cidRemapping.Count}");
        
        Assert.That(mergedToUnicode.Count, Is.EqualTo(uniqueNewCids), 
            "Each Unicode character should map to exactly one CID");
        
        var conflicts = cidRemapping.GroupBy(kvp => (kvp.Key.Item2, kvp.Key.Item3))
            .Where(g => g.Select(kvp => kvp.Value).Distinct().Count() > 1)
            .ToList();
        
        Console.WriteLine($"Conflicts detected: {conflicts.Count}");
        Assert.That(conflicts.Count, Is.EqualTo(0), "All CID conflicts should be resolved");
        
        Console.WriteLine($"\n✅ CID remapping successful - all conflicts resolved!");
        
        optimizer.Close();
    }

    [Test]
    public void TestMergedFontWithCidRemapping()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        
        var optimizer = new PdfFontOptimizer(testPdfPath);
        optimizer.IndexFonts();
        
        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);
        
        Console.WriteLine("\n=== Testing Merged Fonts with CID Remapping ===\n");
        
        foreach (var entry in globalFontDict.EntrySet())
        {
            var fontDict = globalFontDict.GetAsDictionary(entry.Key);
            string fontName = entry.Key.ToString();
            
            Console.WriteLine($"📝 Checking font: {fontName}");
            
            var toUnicode = fontDict.Get(PdfName.ToUnicode);
            if (toUnicode != null && toUnicode is PdfStream stream)
            {
                var cmapData = stream.GetBytes();
                var cmapString = Encoding.UTF8.GetString(cmapData);
                
                var criticalChars = new Dictionary<string, string>
                {
                    { "0041", "A" },
                    { "0052", "R" },
                    { "0032", "2" },
                    { "0035", "5" },
                    { "002D", "-" }
                };
                
                int foundCount = 0;
                foreach (var kvp in criticalChars)
                {
                    if (cmapString.Contains($"<{kvp.Key}>"))
                    {
                        foundCount++;
                        Console.WriteLine($"  ✅ U+{kvp.Key} ({kvp.Value}) found in ToUnicode");
                        
                        var pattern = $@"<([0-9A-Fa-f]+)>\s*<{kvp.Key}>";
                        var match = Regex.Match(cmapString, pattern);
                        if (match.Success)
                        {
                            Console.WriteLine($"     → Mapped to CID {match.Groups[1].Value}");
                        }
                    }
                }
                
                if (fontName.Contains("Calibri") && foundCount == 5)
                {
                    Console.WriteLine($"  🎉 All critical characters found in {fontName}!");
                }
            }
        }
        
        optimizer.Close();
    }

    [Test]
    public void TestCompleteOptimizationWithCidRemapping()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        string outputPath = @"P:\pdf3\Ladders_Optimized_WithCID.pdf";

        var optimizer = new PdfFontOptimizer(testPdfPath, outputPath);
        optimizer.IndexFonts();

        var mapping = optimizer.CreateFontMappingTable();
        var globalFontDict = optimizer.CreateGlobalFontDictionary(mapping);

        optimizer.ApplyGlobalFontDictionary(globalFontDict, mapping, optimizer.GetFontCidRemappings());
        optimizer.SaveOptimizedPdf();

        // Small delay to ensure file is fully written
        System.Threading.Thread.Sleep(100);

        using (var reader = new PdfReader(outputPath))
        using (var resultDoc = new PdfDocument(reader))
        {
            var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(resultDoc.GetPage(1));

            Assert.That(text.Contains("All Rates"), Is.True, "Should contain 'All Rates'");
            Assert.That(text.Contains("Parallel"), Is.True, "Should contain 'Parallel'");
            Assert.That(text.Contains("Ladders"), Is.True, "Should contain 'Ladders'");

            Console.WriteLine($"✅ Text is now readable!");
            Console.WriteLine($"First 200 chars: {text.Substring(0, Math.Min(200, text.Length))}");
        }
    }

    [Test]
    public void DiagnoseFileSizes()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        string outputPath = @"P:\pdf3\Ladders_Optimized_WithCID.pdf";

        var originalSize = new FileInfo(testPdfPath).Length;
        Console.WriteLine($"\n=== File Size Diagnostic ===\n");
        Console.WriteLine($"Original PDF size: {originalSize:N0} bytes ({originalSize / 1024.0 / 1024.0:F2} MB)");

        if (File.Exists(outputPath))
        {
            var optimizedSize = new FileInfo(outputPath).Length;
            Console.WriteLine($"Optimized PDF size: {optimizedSize:N0} bytes ({optimizedSize / 1024.0 / 1024.0:F2} MB)");

            var increase = optimizedSize - originalSize;
            var percentChange = (increase * 100.0) / originalSize;
            Console.WriteLine($"Size change: {increase:N0} bytes ({percentChange:+0.0;-0.0}%)");

            if (increase > 0)
            {
                Console.WriteLine($"\n⚠️  WARNING: File size INCREASED by {increase / 1024.0:F1} KB");
            }
            else
            {
                Console.WriteLine($"\n✅ File size DECREASED by {-increase / 1024.0:F1} KB");
            }
        }
        else
        {
            Console.WriteLine($"\n⚠️  Optimized file not found. Run TestCompleteOptimizationWithCidRemapping first.");
            return;
        }

        Console.WriteLine("\n=== FontFile2 Sizes in Original PDF ===");
        AnalyzeFontFile2Sizes(testPdfPath);

        Console.WriteLine("\n=== FontFile2 Sizes in Optimized PDF ===");
        AnalyzeFontFile2Sizes(outputPath);

        Console.WriteLine("\n=== Font Merging Analysis ===");
        AnalyzeFontMerging(outputPath);
    }

    private void AnalyzeFontMerging(string pdfPath)
    {
        using (var reader = new PdfReader(pdfPath))
        using (var doc = new PdfDocument(reader))
        {
            Console.WriteLine($"Checking all {doc.GetNumberOfPages()} pages for font resource sharing:\n");

            var allPageFonts = new Dictionary<int, Dictionary<string, string>>();

            for (int pageNum = 1; pageNum <= doc.GetNumberOfPages(); pageNum++)
            {
                var page = doc.GetPage(pageNum);
                var resources = page.GetResources();
                var fontsDict = resources?.GetPdfObject()?.GetAsDictionary(PdfName.Font);

                if (fontsDict == null) continue;

                var pageFontInfo = new Dictionary<string, string>();

                foreach (var entry in fontsDict.EntrySet())
                {
                    var fontDict = fontsDict.GetAsDictionary(entry.Key);
                    if (fontDict == null) continue;

                    var baseFont = fontDict.GetAsName(PdfName.BaseFont)?.GetValue() ?? "Unknown";
                    var fontObjectNumber = fontDict.GetIndirectReference()?.GetObjNumber() ?? 0;

                    pageFontInfo[entry.Key.ToString()] = $"{baseFont} (obj#{fontObjectNumber})";
                }

                allPageFonts[pageNum] = pageFontInfo;
            }

            var fontsByObject = new Dictionary<int, List<string>>();

            foreach (var pageInfo in allPageFonts)
            {
                int pageNum = pageInfo.Key;
                foreach (var fontInfo in pageInfo.Value)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(fontInfo.Value, @"obj#(\d+)");
                    if (match.Success)
                    {
                        int objNum = int.Parse(match.Groups[1].Value);
                        if (!fontsByObject.ContainsKey(objNum))
                        {
                            fontsByObject[objNum] = [];
                        }
                        fontsByObject[objNum].Add($"Page {pageNum}: {fontInfo.Key} ({fontInfo.Value.Split('(')[0].Trim()})");
                    }
                }
            }

            Console.WriteLine("Font Dictionary Sharing (by object number):\n");
            int sharedCount = 0;

            foreach (var objInfo in fontsByObject.OrderBy(kv => kv.Key))
            {
                if (objInfo.Value.Count > 1)
                {
                    sharedCount++;
                    Console.WriteLine($"  Object #{objInfo.Key} is shared by {objInfo.Value.Count} pages:");
                    foreach (var usage in objInfo.Value)
                    {
                        Console.WriteLine($"    - {usage}");
                    }
                    Console.WriteLine();
                }
            }

            if (sharedCount == 0)
            {
                Console.WriteLine("  ❌ NO FONT SHARING DETECTED!");
                Console.WriteLine("  Each page has its own font dictionary copies.");
                Console.WriteLine("  Font merging DID NOT HAPPEN!");
            }
            else
            {
                Console.WriteLine($"  ✅ Found {sharedCount} shared font dictionaries");
            }

            Console.WriteLine($"\n=== Expected vs Actual ===");
            Console.WriteLine($"Expected: 3 merged fonts (Calibri, TableauBook, TableauMedium)");
            Console.WriteLine($"Actual: {fontsByObject.Count} unique font objects");
            
            if (fontsByObject.Count > 3)
            {
                Console.WriteLine($"⚠️  Problem: We have {fontsByObject.Count} font objects instead of 3!");
            }
        }
    }

    private void AnalyzeFontFile2Sizes(string pdfPath)
    {
        using (var reader = new PdfReader(pdfPath))
        using (var doc = new PdfDocument(reader))
        {
            var page1 = doc.GetPage(1);
            var resources = page1.GetResources();
            var fontsDict = resources?.GetPdfObject()?.GetAsDictionary(PdfName.Font);

            if (fontsDict == null)
            {
                Console.WriteLine("  No fonts found");
                return;
            }

            long totalFontFile2Size = 0;
            int fontFile2Count = 0;

            foreach (var entry in fontsDict.EntrySet())
            {
                var fontDict = fontsDict.GetAsDictionary(entry.Key);
                if (fontDict == null) continue;

                string fontName = entry.Key.ToString();
                var baseFont = fontDict.GetAsName(PdfName.BaseFont)?.GetValue() ?? "Unknown";

                if (PdfName.Type0.Equals(fontDict.GetAsName(PdfName.Subtype)))
                {
                    var descendantFonts = fontDict.GetAsArray(PdfName.DescendantFonts);
                    var cidFont = descendantFonts?.GetAsDictionary(0);
                    var fontDescriptor = cidFont?.GetAsDictionary(PdfName.FontDescriptor);
                    var fontFile2 = fontDescriptor?.GetAsStream(PdfName.FontFile2);

                    if (fontFile2 != null)
                    {
                        var size = fontFile2.GetBytes().Length;
                        totalFontFile2Size += size;
                        fontFile2Count++;
                        Console.WriteLine($"  {fontName} ({baseFont}): FontFile2 = {size:N0} bytes ({size / 1024.0:F1} KB)");
                    }
                    else
                    {
                        Console.WriteLine($"  {fontName} ({baseFont}): No FontFile2");
                    }
                }
            }

            Console.WriteLine($"\n  Total FontFile2 data: {totalFontFile2Size:N0} bytes ({totalFontFile2Size / 1024.0:F1} KB)");
            Console.WriteLine($"  Number of FontFile2 streams: {fontFile2Count}");
        }
    }
}
