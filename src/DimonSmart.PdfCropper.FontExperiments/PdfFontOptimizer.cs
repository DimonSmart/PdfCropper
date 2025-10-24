using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser.Data;

namespace DimonSmart.PdfCropper.FontExperiments;

public class PdfFontOptimizer
{
    private readonly PdfDocument document;
    private readonly Dictionary<int, List<FontInfo>> pageFonts;

    public class FontInfo
    {
        public string ResourceName { get; set; } = string.Empty;
        public string BaseFontName { get; set; } = string.Empty;
        public PdfDictionary? FontDict { get; set; }
        public PdfDictionary? FontsDict { get; set; }
        public int PageNumber { get; set; }

        public override string ToString()
        {
            return $"Page {PageNumber}: {ResourceName} = {BaseFontName}";
        }
    }

    public PdfFontOptimizer(string inputPath)
    {
        var reader = new PdfReader(inputPath);
        document = new PdfDocument(reader);
        pageFonts = new Dictionary<int, List<FontInfo>>();
    }

    public PdfFontOptimizer(string inputPath, string outputPath)
    {
        var reader = new PdfReader(inputPath);
        var writer = new PdfWriter(outputPath);
        document = new PdfDocument(reader, writer);
        pageFonts = new Dictionary<int, List<FontInfo>>();
    }

    public int GetPageCount()
    {
        return document.GetNumberOfPages();
    }

    public List<FontInfo> GetPageFonts(int pageNum)
    {
        if (pageFonts.ContainsKey(pageNum))
        {
            return pageFonts[pageNum];
        }
        return [];
    }

    public void IndexFonts()
    {
        for (int pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
        {
            var page = document.GetPage(pageNum);
            var resources = page.GetResources();

            if (resources == null) continue;

            var fontsDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontsDict == null) continue;

            var fontsOnPage = new List<FontInfo>();

            foreach (var fontEntry in fontsDict.EntrySet())
            {
                var resourceName = fontEntry.Key.ToString();
                var fontDict = fontsDict.GetAsDictionary(fontEntry.Key);

                if (fontDict != null)
                {
                    var baseFontName = ExtractBaseFontName(fontDict);

                    fontsOnPage.Add(new FontInfo
                    {
                        ResourceName = resourceName,
                        BaseFontName = baseFontName,
                        FontDict = fontDict,
                        FontsDict = fontsDict,
                        PageNumber = pageNum
                    });

                    Console.WriteLine($"[INFO] Page {pageNum}: Found font {resourceName} = {baseFontName}");
                }
            }

            pageFonts[pageNum] = fontsOnPage;
        }
    }

    private string ExtractBaseFontName(PdfDictionary fontDict)
    {
        var baseFont = fontDict.GetAsName(PdfName.BaseFont);
        if (baseFont == null) return "Unknown";

        string fontName = baseFont.GetValue();

        if (fontName.Length > 7 && fontName[6] == '+')
        {
            bool isSubset = true;
            for (int i = 0; i < 6; i++)
            {
                char c = fontName[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    isSubset = false;
                    break;
                }
            }

            if (isSubset)
            {
                fontName = fontName.Substring(7);
            }
        }

        int dashIndex = fontName.IndexOf('-');
        if (dashIndex > 0)
        {
            string suffix = fontName.Substring(dashIndex + 1);
            if (suffix.All(c => char.IsDigit(c) || c == '-'))
            {
                fontName = fontName.Substring(0, dashIndex);
            }
        }

        return fontName;
    }

    public Dictionary<string, List<FontInfo>> GetFontStatistics()
    {
        var stats = new Dictionary<string, List<FontInfo>>();

        foreach (var pageEntry in pageFonts)
        {
            foreach (var font in pageEntry.Value)
            {
                if (!stats.ContainsKey(font.BaseFontName))
                {
                    stats[font.BaseFontName] = [];
                }
                stats[font.BaseFontName].Add(font);
            }
        }

        return stats;
    }

    public List<string> DetectResourceConflicts()
    {
        var conflicts = new List<string>();
        var resourceMap = new Dictionary<string, HashSet<string>>();

        foreach (var pageEntry in pageFonts)
        {
            foreach (var font in pageEntry.Value)
            {
                string key = font.ResourceName;

                if (!resourceMap.ContainsKey(key))
                {
                    resourceMap[key] = [];
                }

                resourceMap[key].Add(font.BaseFontName);
            }
        }

        foreach (var entry in resourceMap)
        {
            if (entry.Value.Count > 1)
            {
                conflicts.Add($"Resource {entry.Key} maps to different fonts: {string.Join(", ", entry.Value)}");
            }
        }

        return conflicts;
    }

    public Dictionary<PdfDictionary, List<int>> DetectSharedFontDictionaries()
    {
        var dictionaryUsage = new Dictionary<PdfDictionary, List<int>>();

        for (int pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
        {
            var page = document.GetPage(pageNum);
            var resources = page.GetResources();

            if (resources == null) continue;

            var fontsDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (fontsDict == null) continue;

            if (!dictionaryUsage.ContainsKey(fontsDict))
            {
                dictionaryUsage[fontsDict] = [];
            }
            dictionaryUsage[fontsDict].Add(pageNum);
        }

        return dictionaryUsage;
    }

    public class SharedDictionaryConflict
    {
        public PdfDictionary? FontsDictionary { get; set; }
        public List<int> AffectedPages { get; set; }
        public Dictionary<string, HashSet<string>> ResourceConflicts { get; set; }

        public SharedDictionaryConflict()
        {
            AffectedPages = [];
            ResourceConflicts = [];
        }
    }

    public List<SharedDictionaryConflict> AnalyzeSharedDictionaryConflicts()
    {
        var conflicts = new List<SharedDictionaryConflict>();
        var sharedDicts = DetectSharedFontDictionaries();

        foreach (var entry in sharedDicts)
        {
            var fontsDict = entry.Key;
            var pages = entry.Value;

            if (pages.Count <= 1) continue;

            var conflict = new SharedDictionaryConflict
            {
                FontsDictionary = fontsDict,
                AffectedPages = pages
            };

            foreach (var pageNum in pages)
            {
                if (!pageFonts.ContainsKey(pageNum)) continue;

                foreach (var font in pageFonts[pageNum])
                {
                    if (font.FontsDict == fontsDict)
                    {
                        if (!conflict.ResourceConflicts.ContainsKey(font.ResourceName))
                        {
                            conflict.ResourceConflicts[font.ResourceName] = [];
                        }
                        conflict.ResourceConflicts[font.ResourceName].Add(font.BaseFontName);
                    }
                }
            }

            bool hasConflict = false;
            foreach (var resource in conflict.ResourceConflicts)
            {
                if (resource.Value.Count > 1)
                {
                    hasConflict = true;
                    break;
                }
            }

            if (hasConflict || pages.Count > 1)
            {
                conflicts.Add(conflict);
            }
        }

        return conflicts;
    }

    public void PrintSharedDictionaryAnalysis()
    {
        var sharedDicts = DetectSharedFontDictionaries();
        var conflicts = AnalyzeSharedDictionaryConflicts();

        Console.WriteLine("\n=== Shared Font Dictionary Analysis ===");

        foreach (var entry in sharedDicts.Where(e => e.Value.Count > 1))
        {
            var dictHash = entry.Key.GetHashCode().ToString("X");
            var pages = string.Join(", ", entry.Value.OrderBy(p => p));
            Console.WriteLine($"Dictionary {dictHash} shared by pages: {pages}");
        }

        if (conflicts.Count > 0)
        {
            Console.WriteLine("\n=== CONFLICTS DETECTED ===");
            foreach (var conflict in conflicts)
            {
                var dictHash = conflict.FontsDictionary?.GetHashCode().ToString("X");
                Console.WriteLine($"\n⚠️ Dictionary {dictHash} (pages: {string.Join(", ", conflict.AffectedPages)}):");

                foreach (var resource in conflict.ResourceConflicts)
                {
                    if (resource.Value.Count > 1)
                    {
                        Console.WriteLine($"  CONFLICT: {resource.Key} → {string.Join(" vs ", resource.Value)}");
                    }
                }
            }
        }
    }

    private class GlyphCollector : IEventListener
    {
        private readonly Dictionary<string, HashSet<int>> glyphsByFont;
        private readonly Dictionary<PdfDictionary, string> fontToResourceName;
        private string? currentFontResource;

        public GlyphCollector()
        {
            glyphsByFont = new Dictionary<string, HashSet<int>>();
            fontToResourceName = new Dictionary<PdfDictionary, string>();
        }

        public void SetFontResourceMapping(Dictionary<string, PdfDictionary> resourceToFont)
        {
            fontToResourceName.Clear();
            foreach (var entry in resourceToFont)
            {
                if (entry.Value != null)
                {
                    fontToResourceName[entry.Value] = entry.Key;
                }
            }
        }

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_TEXT)
            {
                var renderInfo = (TextRenderInfo)data;
                
                try
                {
                    var font = renderInfo.GetFont();
                    var fontDict = font?.GetPdfObject();
                    
                    if (fontDict != null && fontToResourceName.TryGetValue(fontDict, out string? resourceName))
                    {
                        if (!glyphsByFont.ContainsKey(resourceName))
                        {
                            glyphsByFont[resourceName] = [];
                        }

                        var text = renderInfo.GetText();
                        if (!string.IsNullOrEmpty(text))
                        {
                            foreach (char c in text)
                            {
                                glyphsByFont[resourceName].Add(c);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new[] { EventType.RENDER_TEXT };
        }

        public Dictionary<string, HashSet<int>> GetGlyphsByFont()
        {
            return glyphsByFont;
        }
    }

    public class FontContext
    {
        public int PageNumber { get; set; }
        public string ResourceName { get; set; } = string.Empty;
        public string ActualFontName { get; set; } = string.Empty;
        public PdfDictionary? FontDict { get; set; }
        public PdfDictionary? FontsDict { get; set; }

        public string GetContextKey() => $"Page{PageNumber}_{ResourceName}";
        public string GetFontGroupKey() => ActualFontName;
    }

    public Dictionary<string, string> CreateFontMappingTable()
    {
        var mapping = new Dictionary<string, string>();
        var fontGroups = new Dictionary<string, List<FontContext>>();

        foreach (var pageEntry in pageFonts)
        {
            int pageNum = pageEntry.Key;
            foreach (var font in pageEntry.Value)
            {
                var context = new FontContext
                {
                    PageNumber = pageNum,
                    ResourceName = font.ResourceName,
                    ActualFontName = font.BaseFontName,
                    FontDict = font.FontDict,
                    FontsDict = font.FontsDict
                };

                if (!fontGroups.ContainsKey(context.ActualFontName))
                {
                    fontGroups[context.ActualFontName] = [];
                }
                fontGroups[context.ActualFontName].Add(context);
            }
        }

        int fontCounter = 1;
        foreach (var group in fontGroups)
        {
            string actualFontName = group.Key;
            string newResourceName = $"/F{fontCounter++}_Merged_{actualFontName.Replace(" ", "")}";

            Console.WriteLine($"Font group '{actualFontName}' will be assigned resource name: {newResourceName}");

            foreach (var context in group.Value)
            {
                string mappingKey = $"{context.PageNumber}_{context.ResourceName}";
                mapping[mappingKey] = newResourceName;

                Console.WriteLine($"  Page {context.PageNumber}: {context.ResourceName} -> {newResourceName}");
            }
        }

        return mapping;
    }

    public void ValidateFontMapping(Dictionary<string, string> mapping)
    {
        Console.WriteLine("\n=== Font Mapping Validation ===");

        var page4Mappings = mapping.Where(m => m.Key.StartsWith("4_")).ToList();
        Console.WriteLine($"\nPage 4 mappings ({page4Mappings.Count} entries):");
        foreach (var m in page4Mappings)
        {
            Console.WriteLine($"  {m.Key} -> {m.Value}");
        }

        var f7Mappings = mapping.Where(m => m.Key.EndsWith("_/F7")).ToList();
        var f8Mappings = mapping.Where(m => m.Key.EndsWith("_/F8")).ToList();

        Console.WriteLine($"\n/F7 mappings across all pages:");
        foreach (var m in f7Mappings.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {m.Key} -> {m.Value}");
        }

        Console.WriteLine($"\n/F8 mappings across all pages:");
        foreach (var m in f8Mappings.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {m.Key} -> {m.Value}");
        }

        string page4_F7 = mapping.GetValueOrDefault("4_/F7", "NOT_FOUND");
        string page4_F8 = mapping.GetValueOrDefault("4_/F8", "NOT_FOUND");
        string otherPages_F7 = mapping.GetValueOrDefault("1_/F7", "NOT_FOUND");
        string otherPages_F8 = mapping.GetValueOrDefault("1_/F8", "NOT_FOUND");

        if (page4_F7.Contains("Calibri") && otherPages_F7.Contains("TableauBook"))
        {
            Console.WriteLine("\n✅ CORRECT: Page 4 /F7 maps to Calibri, other pages /F7 map to TableauBook");
        }
        else
        {
            Console.WriteLine("\n❌ ERROR: Incorrect mapping for /F7!");
        }

        if (page4_F8.Contains("Calibri") && otherPages_F8.Contains("TableauMedium"))
        {
            Console.WriteLine("✅ CORRECT: Page 4 /F8 maps to Calibri, other pages /F8 map to TableauMedium");
        }
        else
        {
            Console.WriteLine("❌ ERROR: Incorrect mapping for /F8!");
        }
    }

    public class MergedFontInfo
    {
        public string NewResourceName { get; set; } = string.Empty;
        public string BaseFontName { get; set; } = string.Empty;
        public PdfDictionary? MergedFontDict { get; set; }
        public HashSet<int> AllGlyphs { get; set; }
        public List<FontContext> SourceContexts { get; set; }

        public MergedFontInfo()
        {
            AllGlyphs = [];
            SourceContexts = [];
        }
    }

    public PdfDictionary CreateGlobalFontDictionary(Dictionary<string, string> mappingTable)
    {
        var globalFontDict = new PdfDictionary();
        var mergedFonts = CreateMergedFonts(mappingTable);

        foreach (var mergedFont in mergedFonts.Values)
        {
            // globalFontDict.Put(new PdfName(mergedFont.NewResourceName), mergedFont.MergedFontDict);
            globalFontDict.Put(new PdfName(mergedFont.NewResourceName.TrimStart('/')), mergedFont.MergedFontDict);

            Console.WriteLine($"Added merged font to global dictionary: {mergedFont.NewResourceName} ({mergedFont.BaseFontName})");
        }

        Console.WriteLine($"\n📚 Global font dictionary created with {globalFontDict.Size()} merged fonts");
        return globalFontDict;
    }

    private Dictionary<string, MergedFontInfo> CreateMergedFonts(Dictionary<string, string> mappingTable)
    {
        var mergedFonts = new Dictionary<string, MergedFontInfo>();
        var groupedByNewName = new Dictionary<string, List<FontContext>>();

        foreach (var mapping in mappingTable)
        {
            var parts = mapping.Key.Split('_');
            int pageNum = int.Parse(parts[0]);
            string oldResourceName = parts[1];
            string newResourceName = mapping.Value;

            if (!pageFonts.ContainsKey(pageNum)) continue;

            var fontInfo = pageFonts[pageNum].FirstOrDefault(f => f.ResourceName == oldResourceName);
            if (fontInfo == null) continue;

            var context = new FontContext
            {
                PageNumber = pageNum,
                ResourceName = oldResourceName,
                ActualFontName = fontInfo.BaseFontName,
                FontDict = fontInfo.FontDict,
                FontsDict = fontInfo.FontsDict
            };

            if (!groupedByNewName.ContainsKey(newResourceName))
            {
                groupedByNewName[newResourceName] = [];
            }
            groupedByNewName[newResourceName].Add(context);
        }

        foreach (var group in groupedByNewName)
        {
            string newResourceName = group.Key;
            var contexts = group.Value;

            if (contexts.Count == 0) continue;

            var firstContext = contexts[0];
            string baseFontName = firstContext.ActualFontName;

            var mergedFont = new MergedFontInfo
            {
                NewResourceName = newResourceName,
                BaseFontName = baseFontName,
                SourceContexts = contexts
            };

            mergedFont.MergedFontDict = CreateMergedFontDictionary(contexts, baseFontName);
            mergedFont.AllGlyphs = CollectAllGlyphs(contexts);

            mergedFonts[newResourceName] = mergedFont;

            Console.WriteLine($"Created merged font: {newResourceName} from {contexts.Count} sources, {mergedFont.AllGlyphs.Count} unique glyphs");
        }

        return mergedFonts;
    }

    private PdfDictionary CreateMergedFontDictionary(List<FontContext> contexts, string baseFontName)
    {
        var firstFontDict = contexts[0].FontDict;
        var mergedDict = new PdfDictionary();

        mergedDict.Put(PdfName.Type, PdfName.Font);

        var subtype = firstFontDict?.GetAsName(PdfName.Subtype);
        if (subtype != null)
        {
            mergedDict.Put(PdfName.Subtype, subtype);
        }

        string subsetPrefix = GenerateSubsetPrefix();
        string newBaseFontName = $"{subsetPrefix}+{baseFontName}";
        mergedDict.Put(PdfName.BaseFont, new PdfName(newBaseFontName));

        var encoding = firstFontDict?.Get(PdfName.Encoding);
        if (encoding != null)
        {
            mergedDict.Put(PdfName.Encoding, encoding);
        }

        var toUnicode = firstFontDict?.Get(PdfName.ToUnicode);
        if (toUnicode != null)
        {
            mergedDict.Put(PdfName.ToUnicode, toUnicode);
        }

        if (PdfName.Type0.Equals(subtype))
        {
            PdfDictionary? sourceCidFont = null;
            
            Console.WriteLine($"  🔍 Searching for W array among {contexts.Count} font contexts for {baseFontName}:");
            
            int contextIndex = 0;
            foreach (var context in contexts)
            {
                contextIndex++;
                var descendantFonts = context.FontDict?.GetAsArray(PdfName.DescendantFonts);
                if (descendantFonts != null && descendantFonts.Size() > 0)
                {
                    var cidFont = descendantFonts.GetAsDictionary(0);
                    if (cidFont != null)
                    {
                        var hasW = cidFont.Get(PdfName.W) != null;
                        var hasDW = cidFont.Get(PdfName.DW) != null;
                        Console.WriteLine($"    Context {contextIndex} (Page {context.PageNumber}, {context.ResourceName}): W={hasW}, DW={hasDW}");
                        
                        if (hasW && sourceCidFont == null)
                        {
                            sourceCidFont = cidFont;
                            Console.WriteLine($"    ✅ Found W array in context {contextIndex}");
                            break;
                        }
                    }
                }
            }

            if (sourceCidFont == null)
            {
                Console.WriteLine($"  ⚠️ No W array found in any context, using first font's CIDFont");
                var descendantFonts = firstFontDict?.GetAsArray(PdfName.DescendantFonts);
                if (descendantFonts != null && descendantFonts.Size() > 0)
                {
                    sourceCidFont = descendantFonts.GetAsDictionary(0);
                }
            }

            if (sourceCidFont != null)
            {
                var cidFont = new PdfDictionary();
                cidFont.Put(PdfName.Type, PdfName.Font);

                var cidSubtype = sourceCidFont.GetAsName(PdfName.Subtype);
                if (cidSubtype != null)
                {
                    cidFont.Put(PdfName.Subtype, cidSubtype);
                }

                cidFont.Put(PdfName.BaseFont, new PdfName(newBaseFontName));

                var cidSystemInfo = sourceCidFont.Get(PdfName.CIDSystemInfo);
                if (cidSystemInfo != null)
                {
                    cidFont.Put(PdfName.CIDSystemInfo, cidSystemInfo);
                }

                var widths = sourceCidFont.Get(PdfName.W);
                if (widths != null)
                {
                    cidFont.Put(PdfName.W, widths);
                    Console.WriteLine($"  ✅ Copied W (widths) array from original CIDFont for {baseFontName}");
                }
                else
                {
                    Console.WriteLine($"  ⚠️ No W (widths) array found in original CIDFont for {baseFontName}");
                }

                var defaultWidth = sourceCidFont.Get(PdfName.DW);
                if (defaultWidth != null)
                {
                    cidFont.Put(PdfName.DW, defaultWidth);
                    Console.WriteLine($"  ✅ Copied DW (default width) from original CIDFont for {baseFontName}");
                }

                var originalFontDescriptor = sourceCidFont.GetAsDictionary(PdfName.FontDescriptor);
                if (originalFontDescriptor != null)
                {
                    var newFontDescriptor = new PdfDictionary();
                    newFontDescriptor.Put(PdfName.Type, PdfName.FontDescriptor);
                    newFontDescriptor.Put(PdfName.FontName, new PdfName(newBaseFontName));

                    CopyFontMetrics(originalFontDescriptor, newFontDescriptor);

                    var fontFile2 = originalFontDescriptor.Get(PdfName.FontFile2);
                    if (fontFile2 != null)
                    {
                        newFontDescriptor.Put(PdfName.FontFile2, fontFile2);
                        Console.WriteLine($"  ✅ Copied FontFile2 from original FontDescriptor for {baseFontName}");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ No FontFile2 found in original FontDescriptor for {baseFontName}");
                    }

                    var cidSet = originalFontDescriptor.Get(PdfName.CIDSet);
                    if (cidSet != null)
                    {
                        newFontDescriptor.Put(PdfName.CIDSet, cidSet);
                    }

                    cidFont.Put(PdfName.FontDescriptor, newFontDescriptor);
                }
                else
                {
                    var newFontDescriptor = new PdfDictionary();
                    newFontDescriptor.Put(PdfName.Type, PdfName.FontDescriptor);
                    newFontDescriptor.Put(PdfName.FontName, new PdfName(newBaseFontName));

                    newFontDescriptor.Put(PdfName.Flags, new PdfNumber(32));
                    newFontDescriptor.Put(PdfName.FontBBox, new PdfArray(new float[] { -1000, -1000, 1000, 1000 }));
                    newFontDescriptor.Put(PdfName.ItalicAngle, new PdfNumber(0));
                    newFontDescriptor.Put(PdfName.Ascent, new PdfNumber(1000));
                    newFontDescriptor.Put(PdfName.Descent, new PdfNumber(-200));
                    newFontDescriptor.Put(PdfName.CapHeight, new PdfNumber(700));
                    newFontDescriptor.Put(PdfName.StemV, new PdfNumber(80));

                    cidFont.Put(PdfName.FontDescriptor, newFontDescriptor);
                }

                var newDescendantFonts = new PdfArray();
                newDescendantFonts.Add(cidFont);
                mergedDict.Put(PdfName.DescendantFonts, newDescendantFonts);
            }
        }
        else
        {
            var fontDescriptor = firstFontDict?.GetAsDictionary(PdfName.FontDescriptor);
            if (fontDescriptor != null)
            {
                var newFontDescriptor = new PdfDictionary();
                newFontDescriptor.Put(PdfName.Type, PdfName.FontDescriptor);
                newFontDescriptor.Put(PdfName.FontName, new PdfName(newBaseFontName));

                CopyFontMetrics(fontDescriptor, newFontDescriptor);

                var fontFile2 = fontDescriptor.Get(PdfName.FontFile2);
                if (fontFile2 != null)
                {
                    newFontDescriptor.Put(PdfName.FontFile2, fontFile2);
                }

                mergedDict.Put(PdfName.FontDescriptor, newFontDescriptor);
            }
        }

        return mergedDict;
    }

    private void CopyFontMetrics(PdfDictionary source, PdfDictionary target)
    {
        var metricsKeys = new[]
        {
            PdfName.Ascent, PdfName.Descent, PdfName.CapHeight,
            PdfName.Flags, PdfName.FontBBox, PdfName.ItalicAngle,
            PdfName.StemV, PdfName.XHeight, PdfName.Leading,
            PdfName.MissingWidth, PdfName.StemH
        };

        foreach (var key in metricsKeys)
        {
            var value = source.Get(key);
            if (value != null)
            {
                target.Put(key, value);
            }
        }
    }

    private HashSet<int> CollectAllGlyphs(List<FontContext> contexts)
    {
        var allGlyphs = new HashSet<int>();

        var contextsByPage = contexts.GroupBy(c => c.PageNumber);

        foreach (var pageGroup in contextsByPage)
        {
            int pageNum = pageGroup.Key;
            
            try
            {
                var page = document.GetPage(pageNum);
                var resources = page.GetResources();
                
                if (resources == null) continue;

                var fontsDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
                if (fontsDict == null) continue;

                var resourceToFont = new Dictionary<string, PdfDictionary>();
                foreach (var entry in fontsDict.EntrySet())
                {
                    var resourceName = entry.Key.ToString();
                    var fontDict = fontsDict.GetAsDictionary(entry.Key);
                    if (fontDict != null)
                    {
                        resourceToFont[resourceName] = fontDict;
                    }
                }

                var collector = new GlyphCollector();
                collector.SetFontResourceMapping(resourceToFont);
                
                var processor = new PdfCanvasProcessor(collector);
                processor.ProcessPageContent(page);

                var glyphsByFont = collector.GetGlyphsByFont();

                foreach (var context in pageGroup)
                {
                    if (glyphsByFont.TryGetValue(context.ResourceName, out var glyphs))
                    {
                        allGlyphs.UnionWith(glyphs);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Warning: Error collecting glyphs from page {pageNum}: {ex.Message}");
                AddBasicGlyphs(allGlyphs);
            }
        }

        if (allGlyphs.Count == 0)
        {
            AddBasicGlyphs(allGlyphs);
        }

        Console.WriteLine($"  Collected {allGlyphs.Count} unique glyphs from {contexts.Count} font contexts");

        return allGlyphs;
    }

    private void AddBasicGlyphs(HashSet<int> glyphs)
    {
        glyphs.Add(0);
        for (int i = 32; i < 127; i++)
        {
            glyphs.Add(i);
        }
        glyphs.Add(160);
    }

    private string GenerateSubsetPrefix()
    {
        var random = new Random();
        var prefix = new char[6];
        for (int i = 0; i < 6; i++)
        {
            prefix[i] = (char)('A' + random.Next(26));
        }
        return new string(prefix);
    }

    public void ApplyGlobalFontDictionary(PdfDictionary globalFontDict, Dictionary<string, string> mappingTable)
    {
        Console.WriteLine("\n=== Applying Global Font Dictionary ===");

        int totalReplacements = 0;
        var replacementStats = new Dictionary<string, int>();

        for (int pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
        {
            var page = document.GetPage(pageNum);
            var resources = page.GetResources();

            if (resources == null)
            {
                Console.WriteLine($"Page {pageNum}: No resources found, skipping");
                continue;
            }

            var originalFontsDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (originalFontsDict != null)
            {
                Console.WriteLine($"Page {pageNum}: Replacing font dictionary (was {originalFontsDict.Size()} fonts)");
                resources.GetPdfObject().Put(PdfName.Font, globalFontDict);
            }

            int pageReplacements = UpdatePageContentStream(page, pageNum, mappingTable, replacementStats);
            totalReplacements += pageReplacements;

            if (pageReplacements > 0)
            {
                Console.WriteLine($"Page {pageNum}: Updated {pageReplacements} font references in content stream");
            }
        }

        Console.WriteLine($"\n📊 Font replacement statistics:");
        Console.WriteLine($"Total font references updated: {totalReplacements}");

        foreach (var stat in replacementStats.OrderBy(s => s.Key))
        {
            Console.WriteLine($"  {stat.Key}: {stat.Value} replacements");
        }

        Console.WriteLine("\n✅ Global font dictionary applied to all pages!");
    }

    private int UpdatePageContentStream(PdfPage page, int pageNum, Dictionary<string, string> mappingTable, Dictionary<string, int> stats)
    {
        try
        {
            byte[] contentBytes = page.GetContentBytes();
            string content = System.Text.Encoding.UTF8.GetString(contentBytes);

            int replacements = 0;

            var tfPattern = new System.Text.RegularExpressions.Regex(@"(\/F\d+)\s+([\d\.]+)\s+Tf");

            content = tfPattern.Replace(content, match =>
            {
                string oldFontName = match.Groups[1].Value;
                string fontSize = match.Groups[2].Value;

                string mappingKey = $"{pageNum}_{oldFontName}";

                if (mappingTable.TryGetValue(mappingKey, out string? newFontName))
                {
                    replacements++;

                    string statKey = $"{oldFontName} -> {newFontName}";
                    if (!stats.ContainsKey(statKey))
                    {
                        stats[statKey] = 0;
                    }
                    stats[statKey]++;

                    return $"{newFontName} {fontSize} Tf";
                }

                return match.Value;
            });

            if (replacements > 0)
            {
                byte[] newContentBytes = System.Text.Encoding.UTF8.GetBytes(content);
                
                var contentStream = page.GetFirstContentStream();
                contentStream.SetData(newContentBytes);
            }

            return replacements;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating page {pageNum} content stream: {ex.Message}");
            return 0;
        }
    }

    public void SaveOptimizedPdf()
    {
        try
        {
            Console.WriteLine($"\n💾 Saving optimized PDF...");

            document.Close();

            Console.WriteLine("✅ PDF saved successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving PDF: {ex.Message}");
            throw;
        }
    }

    public void Close()
    {
        document.Close();
    }
}
