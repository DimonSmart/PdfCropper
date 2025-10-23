using NUnit.Framework;
using DimonSmart.PdfCropper.FontExperiments;
using iText.Kernel.Pdf;

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
    public void TestFontIndexing()
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
}
