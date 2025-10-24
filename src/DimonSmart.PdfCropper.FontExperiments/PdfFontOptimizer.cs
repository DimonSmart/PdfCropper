using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace DimonSmart.PdfCropper.FontExperiments;

public class PdfFontOptimizer
{
    private readonly PdfDocument document;
    private readonly Dictionary<int, List<FontInfo>> pageFonts;
    private readonly Dictionary<string, Dictionary<(int, string, int), int>> fontCidRemappings = new Dictionary<string, Dictionary<(int, string, int), int>>();

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

    public PdfDocument GetDocument()
    {
        return document;
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

        // Удаляем subset-префикс
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

        var pattern = @"-\d+(-\d+)*$";
        fontName = System.Text.RegularExpressions.Regex.Replace(fontName, pattern, "");

        return fontName;
    }

    public Dictionary<string, List<FontInfo>> GetFontStatistics()
    {
        return pageFonts
            .SelectMany(page => page.Value)
            .GroupBy(font => font.BaseFontName)
            .ToDictionary(
                group => group.Key,
                group => group.ToList()
            );
    }

    public List<string> DetectResourceConflicts()
    {
        var conflicts = new List<string>();
        var resourceMap = new Dictionary<string, Dictionary<string, List<int>>>(); // resource -> (font -> pages)

        foreach (var pageEntry in pageFonts)
        {
            int pageNum = pageEntry.Key;
            foreach (var font in pageEntry.Value)
            {
                if (!resourceMap.ContainsKey(font.ResourceName))
                {
                    resourceMap[font.ResourceName] = new Dictionary<string, List<int>>();
                }

                if (!resourceMap[font.ResourceName].ContainsKey(font.BaseFontName))
                {
                    resourceMap[font.ResourceName][font.BaseFontName] = new List<int>();
                }

                resourceMap[font.ResourceName][font.BaseFontName].Add(pageNum);
            }
        }

        foreach (var entry in resourceMap)
        {
            if (entry.Value.Count > 1)
            {
                var details = entry.Value.Select(kvp =>
                    $"{kvp.Key} (pages: {string.Join(",", kvp.Value)})");
                conflicts.Add($"Resource {entry.Key} maps to different fonts: {string.Join(" vs ", details)}");
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

        // НОВОЕ: Используем CID remapping вместо простого объединения
        Console.WriteLine($"  🔄 Creating merged font dictionary for {baseFontName}:");
        
        var remapper = new CidRemapper();
        var (mergedToUnicode, cidRemapping) = remapper.RemapCidsWithConflictResolution(contexts);
        
        Console.WriteLine($"  ✅ CID remapping complete: {mergedToUnicode.Count} unique CIDs");
        
        var toUnicodeStream = CreateRemappedToUnicodeCMap(mergedToUnicode, baseFontName);
        mergedDict.Put(PdfName.ToUnicode, toUnicodeStream);
        
        StoreCidRemappingForFont(baseFontName, cidRemapping);
        Console.WriteLine($"  ✅ Applied merged ToUnicode CMap for {baseFontName} with {mergedToUnicode.Count} mappings");

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

    private PdfStream CreateRemappedToUnicodeCMap(Dictionary<int, string> mergedToUnicode, string fontName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo");
        sb.AppendLine("<< /Registry (Adobe)");
        sb.AppendLine("/Ordering (UCS)");
        sb.AppendLine("/Supplement 0");
        sb.AppendLine(">> def");
        sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine($"<0001> <{mergedToUnicode.Count:X4}>");
        sb.AppendLine("endcodespacerange");
        
        if (mergedToUnicode.Count > 0)
        {
            sb.AppendLine($"{mergedToUnicode.Count} beginbfchar");
            foreach (var kvp in mergedToUnicode.OrderBy(m => m.Key))
            {
                sb.AppendLine($"<{kvp.Key:X4}> <{kvp.Value}>");
            }
            sb.AppendLine("endbfchar");
        }
        
        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");
        
        var cmapBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var stream = new PdfStream(cmapBytes);
        
        Console.WriteLine($"  ✅ Created ToUnicode CMap with {mergedToUnicode.Count} mappings");
        
        return stream;
    }

    private void StoreCidRemappingForFont(string fontName, Dictionary<(int, string, int), int> cidRemapping)
    {
        fontCidRemappings[fontName] = cidRemapping;
        Console.WriteLine($"  💾 Stored CID remapping for {fontName}: {cidRemapping.Count} entries");
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

    private Dictionary<int, string> ExtractMappingsFromToUnicode(PdfStream toUnicodeStream)
    {
        var mappings = new Dictionary<int, string>();

        try
        {
            var cmapData = toUnicodeStream.GetBytes();
            var cmapString = System.Text.Encoding.UTF8.GetString(cmapData);

            var singlePattern = new System.Text.RegularExpressions.Regex(
                @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            var rangePattern = new System.Text.RegularExpressions.Regex(
                @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            foreach (System.Text.RegularExpressions.Match match in rangePattern.Matches(cmapString))
            {
                if (match.Groups.Count == 4)
                {
                    int startCid = Convert.ToInt32(match.Groups[1].Value, 16);
                    int endCid = Convert.ToInt32(match.Groups[2].Value, 16);
                    int startUnicode = Convert.ToInt32(match.Groups[3].Value, 16);

                    for (int i = 0; i <= endCid - startCid; i++)
                    {
                        int cid = startCid + i;
                        if (!mappings.ContainsKey(cid))
                        {
                            mappings[cid] = (startUnicode + i).ToString("X4");
                        }
                    }
                }
            }

            foreach (System.Text.RegularExpressions.Match match in singlePattern.Matches(cmapString))
            {
                if (match.Groups.Count == 3)
                {
                    int cid = Convert.ToInt32(match.Groups[1].Value, 16);
                    string unicode = match.Groups[2].Value;

                    if (!mappings.ContainsKey(cid))
                    {
                        mappings[cid] = unicode;
                    }
                }
            }

            Console.WriteLine($"    Extracted {mappings.Count} CID->Unicode mappings from ToUnicode");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️ Error extracting ToUnicode mappings: {ex.Message}");
        }

        return mappings;
    }

    private PdfStream CreateToUnicodeCMap(Dictionary<int, string> mappings)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo");
        sb.AppendLine("<< /Registry (Adobe)");
        sb.AppendLine("/Ordering (UCS)");
        sb.AppendLine("/Supplement 0");
        sb.AppendLine(">> def");
        sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");
        
        if (mappings.Count > 0)
        {
            sb.AppendLine($"{mappings.Count} beginbfchar");
            foreach (var kvp in mappings.OrderBy(m => m.Key))
            {
                sb.AppendLine($"<{kvp.Key:X4}> <{kvp.Value}>");
            }
            sb.AppendLine("endbfchar");
        }
        
        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");

        var cmapBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var stream = new PdfStream(cmapBytes);
        
        return stream;
    }

    private PdfStream? MergeToUnicodeMappings(List<FontContext> contexts, string baseFontName)
    {
        var mergedMappings = new Dictionary<int, string>();
        int totalSources = 0;

        Console.WriteLine($"  🔄 Merging ToUnicode mappings for {baseFontName} from {contexts.Count} contexts:");

        foreach (var context in contexts)
        {
            var toUnicode = context.FontDict?.Get(PdfName.ToUnicode);
            if (toUnicode != null && toUnicode is PdfStream stream)
            {
                totalSources++;
                Console.WriteLine($"    Context {totalSources}: Page {context.PageNumber}, {context.ResourceName}");
                
                var mappings = ExtractMappingsFromToUnicode(stream);
                
                foreach (var kvp in mappings)
                {
                    if (!mergedMappings.ContainsKey(kvp.Key))
                    {
                        mergedMappings[kvp.Key] = kvp.Value;
                    }
                    else if (mergedMappings[kvp.Key] != kvp.Value)
                    {
                        Console.WriteLine($"      ⚠️ CID conflict: <{kvp.Key:X4}> maps to <{mergedMappings[kvp.Key]}> and <{kvp.Value}>");
                    }
                }
            }
        }

        if (mergedMappings.Count == 0)
        {
            Console.WriteLine($"    ⚠️ No ToUnicode mappings found in any context for {baseFontName}");
            return null;
        }

        Console.WriteLine($"    ✅ Merged {mergedMappings.Count} total mappings from {totalSources} sources");

        return CreateToUnicodeCMap(mergedMappings);
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

    public void ApplyGlobalFontDictionary(
        PdfDictionary globalFontDict,
        Dictionary<string, string> mappingTable,
        Dictionary<string, Dictionary<(int, string, int), int>> fontCidRemappings)
    {
        Console.WriteLine("\n=== Applying Global Font Dictionary with CID Remapping ===");

        int totalReplacements = 0;
        var replacementStats = new Dictionary<string, int>();

        for (int pageNum = 1; pageNum <= document.GetNumberOfPages(); pageNum++)
        {
            var page = document.GetPage(pageNum);
            var resources = page.GetResources();

            if (resources == null) continue;

            var originalFontsDict = resources.GetPdfObject().GetAsDictionary(PdfName.Font);
            if (originalFontsDict != null)
            {
                Console.WriteLine($"Page {pageNum}: Replacing font dictionary (was {originalFontsDict.Size()} fonts, now {globalFontDict.Size()} merged fonts)");
                resources.GetPdfObject().Put(PdfName.Font, globalFontDict);
            }

            var pageCidRemapping = new Dictionary<(int, string, int), int>();
            foreach (var fontRemapping in fontCidRemappings)
            {
                foreach (var cidMap in fontRemapping.Value)
                {
                    if (cidMap.Key.Item1 == pageNum)
                    {
                        pageCidRemapping[cidMap.Key] = cidMap.Value;
                    }
                }
            }

            int pageReplacements = UpdatePageContentStreamWithRemapping(
                page, pageNum, mappingTable, pageCidRemapping, replacementStats);

            totalReplacements += pageReplacements;

            if (pageReplacements > 0)
            {
                Console.WriteLine($"Page {pageNum}: Updated {pageReplacements} references (font + CID)");
            }
        }

        Console.WriteLine($"\n📊 Font replacement statistics:");
        Console.WriteLine($"Total font+CID references updated: {totalReplacements}");
        
        foreach (var stat in replacementStats.OrderBy(s => s.Key))
        {
            Console.WriteLine($"  {stat.Key}: {stat.Value} replacements");
        }

        Console.WriteLine($"\n✅ Global font dictionary with CID remapping applied to all pages!");
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

    private int UpdatePageContentStreamWithRemapping(
        PdfPage page,
        int pageNum,
        Dictionary<string, string> resourceMapping,
        Dictionary<(int, string, int), int> cidRemapping,
        Dictionary<string, int> stats)
    {
        try
        {
            byte[] contentBytes = page.GetContentBytes();
            string content = Encoding.UTF8.GetString(contentBytes);

            int replacements = 0;

            var cidPattern = new Regex(@"<([0-9A-Fa-f]+)>");
            var tfPattern = new Regex(@"(\/F\d+)\s+([\d\.]+)\s+Tf");

            string? currentFont = null;
            var lines = content.Split('\n');
            var updatedLines = new List<string>();

            foreach (var line in lines)
            {
                string updatedLine = line;

                var tfMatch = tfPattern.Match(line);
                if (tfMatch.Success)
                {
                    string oldFontName = tfMatch.Groups[1].Value;
                    currentFont = oldFontName;

                    string mappingKey = $"{pageNum}_{oldFontName}";
                    if (resourceMapping.TryGetValue(mappingKey, out string? newFontName))
                    {
                        updatedLine = line.Replace(oldFontName, newFontName);
                        currentFont = newFontName;
                        replacements++;

                        string statKey = $"{oldFontName} -> {newFontName}";
                        if (!stats.ContainsKey(statKey))
                            stats[statKey] = 0;
                        stats[statKey]++;
                    }
                }

                if (currentFont != null && cidPattern.IsMatch(line))
                {
                    updatedLine = cidPattern.Replace(updatedLine, match =>
                    {
                        string cidHex = match.Groups[1].Value;
                        int oldCid = Convert.ToInt32(cidHex, 16);

                        string? originalResource = null;
                        foreach (var mapping in resourceMapping)
                        {
                            if (mapping.Key.StartsWith($"{pageNum}_") && mapping.Value == currentFont)
                            {
                                originalResource = mapping.Key.Substring(mapping.Key.IndexOf('_') + 1);
                                break;
                            }
                        }

                        if (originalResource != null)
                        {
                            var remappingKey = (pageNum, originalResource, oldCid);
                            if (cidRemapping.TryGetValue(remappingKey, out int newCid))
                            {
                                replacements++;
                                return $"<{newCid:X4}>";
                            }
                        }

                        return match.Value;
                    });
                }

                updatedLines.Add(updatedLine);
            }

            if (replacements > 0)
            {
                string updatedContent = string.Join("\n", updatedLines);
                byte[] newContentBytes = Encoding.UTF8.GetBytes(updatedContent);
                
                var contentStream = page.GetFirstContentStream();
                contentStream.SetData(newContentBytes);
            }

            return replacements;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating page {pageNum}: {ex.Message}");
            return 0;
        }
    }

    public Dictionary<string, Dictionary<(int, string, int), int>> GetFontCidRemappings()
    {
        return fontCidRemappings;
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

    public void SaveOptimizedPdf(string outputPath)
    {
        SaveOptimizedPdf();
    }

    public void Close()
    {
        document.Close();
    }

    // Публичный метод для тестирования объединения ToUnicode (вызывает приватный метод)
    public PdfStream? TestMergeToUnicodeMappings(List<FontContext> contexts, string baseFontName)
    {
        return MergeToUnicodeMappings(contexts, baseFontName);
    }

    public class CidRemapper
    {
        private int nextAvailableCid = 1;

        public (Dictionary<int, string> mergedToUnicode, Dictionary<(int, string, int), int> cidRemapping)
            RemapCidsWithConflictResolution(List<FontContext> contexts)
        {
            var mergedToUnicode = new Dictionary<int, string>();
            var cidRemapping = new Dictionary<(int pageNum, string resourceName, int oldCid), int>();
            var unicodeToCid = new Dictionary<string, int>();

            // ДИАГНОСТИКА: Отслеживаем критические символы
            var criticalUnicodes = new HashSet<string> { "0041", "0052", "0032", "0035", "002D" };
            var criticalFound = new Dictionary<string, List<(int page, string resource, int cid)>>();

            Console.WriteLine($"  🔧 Resolving CID conflicts for {contexts.Count} contexts:");

            foreach (var context in contexts)
            {
                var toUnicode = context.FontDict?.Get(PdfName.ToUnicode);
                if (toUnicode != null && toUnicode is PdfStream stream)
                {
                    var cmapData = stream.GetBytes();
                    var cmapString = Encoding.UTF8.GetString(cmapData);
                    var mappings = ExtractMappingsFromToUnicode(cmapString);

                    Console.WriteLine($"    Context: Page {context.PageNumber}, {context.ResourceName}");
                    Console.WriteLine($"      Extracted {mappings.Count} mappings");

                    // ДИАГНОСТИКА: Проверяем критические символы
                    foreach (var kvp in mappings)
                    {
                        if (criticalUnicodes.Contains(kvp.Value))
                        {
                            if (!criticalFound.ContainsKey(kvp.Value))
                                criticalFound[kvp.Value] = new List<(int, string, int)>();

                            criticalFound[kvp.Value].Add((context.PageNumber, context.ResourceName, kvp.Key));
                            Console.WriteLine($"      ✅ Found critical: U+{kvp.Value} at CID {kvp.Key:X4}");
                        }
                    }

                    foreach (var kvp in mappings)
                    {
                        int oldCid = kvp.Key;
                        string unicode = kvp.Value;

                        if (unicodeToCid.TryGetValue(unicode, out int existingCid))
                        {
                            cidRemapping[(context.PageNumber, context.ResourceName, oldCid)] = existingCid;
                        }
                        else
                        {
                            int newCid = nextAvailableCid++;
                            mergedToUnicode[newCid] = unicode;
                            unicodeToCid[unicode] = newCid;
                            cidRemapping[(context.PageNumber, context.ResourceName, oldCid)] = newCid;

                            // ДИАГНОСТИКА
                            if (criticalUnicodes.Contains(unicode))
                            {
                                Console.WriteLine($"      📌 Assigned new CID {newCid} to critical U+{unicode}");
                            }
                        }
                    }
                }
            }

            // ДИАГНОСТИКА: Финальная проверка
            Console.WriteLine($"\n  🔍 Critical characters in final mapping:");
            foreach (var critical in criticalUnicodes)
            {
                var found = mergedToUnicode.Any(m => m.Value == critical);
                if (found)
                {
                    var cid = mergedToUnicode.First(m => m.Value == critical).Key;
                    Console.WriteLine($"    ✅ U+{critical} → CID {cid}");
                }
                else
                {
                    Console.WriteLine($"    ❌ U+{critical} → NOT FOUND");
                    if (criticalFound.ContainsKey(critical))
                    {
                        Console.WriteLine($"       Was found in: {string.Join(", ", criticalFound[critical].Select(f => $"{f.resource}@p{f.page}"))}");
                    }
                }
            }

            Console.WriteLine($"  ✅ Remapped to {mergedToUnicode.Count} unique CIDs (was {cidRemapping.Count} total mappings)");

            return (mergedToUnicode, cidRemapping);
        }

        private Dictionary<int, string> ExtractMappingsFromToUnicode(string cmapString)
        {
            var mappings = new Dictionary<int, string>();

            // Формат 1: beginbfchar/endbfchar (одиночные маппинги)
            // <0020> <0041>
            var bfcharPattern = new Regex(@"beginbfchar\s*(.*?)\s*endbfchar", RegexOptions.Singleline);
            var bfcharMatches = bfcharPattern.Matches(cmapString);

            foreach (Match bfcharMatch in bfcharMatches)
            {
                var content = bfcharMatch.Groups[1].Value;
                var singlePattern = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>");

                foreach (Match match in singlePattern.Matches(content))
                {
                    try
                    {
                        int cid = Convert.ToInt32(match.Groups[1].Value, 16);
                        string unicode = match.Groups[2].Value.ToUpper();
                        mappings[cid] = unicode;
                    }
                    catch { }
                }
            }

            // Формат 2: beginbfrange/endbfrange (диапазоны)
            var bfrangePattern = new Regex(@"beginbfrange\s*(.*?)\s*endbfrange", RegexOptions.Singleline);
            var bfrangeMatches = bfrangePattern.Matches(cmapString);

            foreach (Match bfrangeMatch in bfrangeMatches)
            {
                var content = bfrangeMatch.Groups[1].Value;

                // Формат с массивом: <start> <end> [<val1> <val2> ...]
                var arrayPattern = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*\[(.*?)\]", RegexOptions.Singleline);
                foreach (Match match in arrayPattern.Matches(content))
                {
                    try
                    {
                        int startCid = Convert.ToInt32(match.Groups[1].Value, 16);
                        int endCid = Convert.ToInt32(match.Groups[2].Value, 16);
                        string unicodesStr = match.Groups[3].Value;

                        var unicodePattern = new Regex(@"<([0-9A-Fa-f]+)>");
                        var unicodeMatches = unicodePattern.Matches(unicodesStr);

                        for (int i = 0; i <= endCid - startCid && i < unicodeMatches.Count; i++)
                        {
                            mappings[startCid + i] = unicodeMatches[i].Groups[1].Value.ToUpper();
                        }
                    }
                    catch { }
                }

                // Формат с одним значением: <start> <end> <startUnicode>
                var simpleRangePattern = new Regex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>");
                foreach (Match match in simpleRangePattern.Matches(content))
                {
                    if (!match.Value.Contains("[")) // Убедимся, что это не массив
                    {
                        try
                        {
                            int startCid = Convert.ToInt32(match.Groups[1].Value, 16);
                            int endCid = Convert.ToInt32(match.Groups[2].Value, 16);
                            int startUnicode = Convert.ToInt32(match.Groups[3].Value, 16);

                            for (int i = 0; i <= endCid - startCid; i++)
                            {
                                mappings[startCid + i] = (startUnicode + i).ToString("X4");
                            }
                        }
                        catch { }
                    }
                }
            }

            return mappings;
        }
    }
}
