using System;
using System.Collections.Generic;
using System.Linq;
using iText.Kernel.Pdf;
using NUnit.Framework;

namespace DimonSmart.PdfCropper.FontExperiments.Tests
{
    [TestFixture]
    public class PdfFontDiagnosticsTests
    {
        [Test]
        public void DiagnoseFontStructure()
        {
            // Arrange
            string testPdfPath = @"P:\pdf3\Ladders.pdf";
            
            Console.WriteLine("=== PDF FONT STRUCTURE DIAGNOSTICS ===\n");
            
            using (var reader = new PdfReader(testPdfPath))
            using (var document = new PdfDocument(reader))
            {
                // Анализируем первые несколько страниц для разных типов шрифтов
                var fontSamples = new Dictionary<string, (int page, string resource)>
                {
                    { "Calibri_F10", (1, "/F10") },  // Calibri на странице 1
                    { "TableauBook_F7", (1, "/F7") }, // TableauBook на странице 1
                    { "Calibri_F7", (4, "/F7") },     // Calibri на странице 4 (конфликт!)
                };
                
                foreach (var sample in fontSamples)
                {
                    Console.WriteLine($"\n{new string('=', 80)}");
                    Console.WriteLine($"Analyzing: {sample.Key} (Page {sample.Value.page}, Resource {sample.Value.resource})");
                    Console.WriteLine($"{new string('=', 80)}");
                    
                    var page = document.GetPage(sample.Value.page);
                    var resources = page.GetResources();
                    var fontsDict = resources?.GetPdfObject()?.GetAsDictionary(PdfName.Font);
                    
                    if (fontsDict == null)
                    {
                        Console.WriteLine("❌ No fonts dictionary found!");
                        continue;
                    }
                    
                    var fontDict = fontsDict.GetAsDictionary(new PdfName(sample.Value.resource.TrimStart('/')));
                    if (fontDict == null)
                    {
                        Console.WriteLine($"❌ Font {sample.Value.resource} not found!");
                        continue;
                    }
                    
                    // Полная диагностика структуры шрифта
                    DiagnoseFontDictionary(fontDict, 0);
                }
                
                // Теперь проверим shared dictionaries
                Console.WriteLine($"\n\n{new string('=', 80)}");
                Console.WriteLine("SHARED FONT DICTIONARIES ANALYSIS");
                Console.WriteLine($"{new string('=', 80)}");
                
                var fontsDictReferences = new Dictionary<int, List<int>>(); // HashCode -> Pages
                
                for (int pageNum = 1; pageNum <= Math.Min(10, document.GetNumberOfPages()); pageNum++)
                {
                    var page = document.GetPage(pageNum);
                    var resources = page.GetResources();
                    var fontsDict = resources?.GetPdfObject()?.GetAsDictionary(PdfName.Font);
                    
                    if (fontsDict != null)
                    {
                        int hashCode = fontsDict.GetHashCode();
                        if (!fontsDictReferences.ContainsKey(hashCode))
                        {
                            fontsDictReferences[hashCode] = new List<int>();
                        }
                        fontsDictReferences[hashCode].Add(pageNum);
                    }
                }
                
                foreach (var entry in fontsDictReferences)
                {
                    if (entry.Value.Count > 1)
                    {
                        Console.WriteLine($"⚠️ Shared dictionary (hash: {entry.Key:X}) used by pages: {string.Join(", ", entry.Value)}");
                    }
                }
            }
            
            Console.WriteLine("\n=== END OF DIAGNOSTICS ===");
        }
        
        private void DiagnoseFontDictionary(PdfDictionary dict, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            
            // Основные поля шрифта
            var type = dict.GetAsName(PdfName.Type);
            var subtype = dict.GetAsName(PdfName.Subtype);
            var baseFont = dict.GetAsName(PdfName.BaseFont);
            
            Console.WriteLine($"{indentStr}📁 Font Dictionary:");
            Console.WriteLine($"{indentStr}  Type: {type?.GetValue() ?? "null"}");
            Console.WriteLine($"{indentStr}  Subtype: {subtype?.GetValue() ?? "null"}");
            Console.WriteLine($"{indentStr}  BaseFont: {baseFont?.GetValue() ?? "null"}");
            
            // Проверяем все ключи в словаре
            Console.WriteLine($"{indentStr}  All keys in dictionary:");
            foreach (var key in dict.KeySet())
            {
                var value = dict.Get(key);
                string valueInfo = GetValueInfo(value);
                Console.WriteLine($"{indentStr}    {key}: {valueInfo}");
            }
            
            // Специальная обработка для Type0 шрифтов
            if (PdfName.Type0.Equals(subtype))
            {
                Console.WriteLine($"\n{indentStr}🔍 Type0 Font Analysis:");
                
                // DescendantFonts
                var descendantFonts = dict.GetAsArray(PdfName.DescendantFonts);
                if (descendantFonts != null && descendantFonts.Size() > 0)
                {
                    Console.WriteLine($"{indentStr}  DescendantFonts array found ({descendantFonts.Size()} element(s))");
                    
                    for (int i = 0; i < descendantFonts.Size(); i++)
                    {
                        var cidFont = descendantFonts.GetAsDictionary(i);
                        if (cidFont != null)
                        {
                            Console.WriteLine($"\n{indentStr}  📂 CIDFont[{i}]:");
                            DiagnoseCIDFont(cidFont, indent + 2);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{indentStr}  ❌ No DescendantFonts found!");
                }
                
                // ToUnicode
                var toUnicode = dict.Get(PdfName.ToUnicode);
                if (toUnicode != null)
                {
                    Console.WriteLine($"{indentStr}  ✓ ToUnicode: {GetValueInfo(toUnicode)}");
                }
                else
                {
                    Console.WriteLine($"{indentStr}  ❌ No ToUnicode CMap");
                }
            }
            
            // FontDescriptor
            var fontDescriptor = dict.GetAsDictionary(PdfName.FontDescriptor);
            if (fontDescriptor != null)
            {
                Console.WriteLine($"\n{indentStr}  📋 FontDescriptor found:");
                DiagnoseFontDescriptor(fontDescriptor, indent + 2);
            }
        }
        
        private void DiagnoseCIDFont(PdfDictionary cidFont, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            
            Console.WriteLine($"{indentStr}Type: {cidFont.GetAsName(PdfName.Type)?.GetValue() ?? "null"}");
            Console.WriteLine($"{indentStr}Subtype: {cidFont.GetAsName(PdfName.Subtype)?.GetValue() ?? "null"}");
            Console.WriteLine($"{indentStr}BaseFont: {cidFont.GetAsName(PdfName.BaseFont)?.GetValue() ?? "null"}");
            
            // КРИТИЧНО: Проверяем W массив
            var wArray = cidFont.Get(PdfName.W);
            if (wArray != null)
            {
                Console.WriteLine($"{indentStr}✅ W (widths) array: {GetValueInfo(wArray)}");
                if (wArray is PdfArray arr)
                {
                    Console.WriteLine($"{indentStr}   W array size: {arr.Size()} elements");
                    // Показываем первые несколько элементов
                    if (arr.Size() > 0)
                    {
                        Console.WriteLine($"{indentStr}   First few elements:");
                        for (int i = 0; i < Math.Min(10, arr.Size()); i++)
                        {
                            Console.WriteLine($"{indentStr}     [{i}]: {GetValueInfo(arr.Get(i))}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"{indentStr}❌ W (widths) array NOT FOUND!");
                
                // Проверяем все ключи чтобы найти где могут быть ширины
                Console.WriteLine($"{indentStr}All keys in CIDFont:");
                foreach (var key in cidFont.KeySet())
                {
                    var value = cidFont.Get(key);
                    Console.WriteLine($"{indentStr}  {key}: {GetValueInfo(value)}");
                }
            }
            
            // DW (default width)
            var dw = cidFont.GetAsNumber(PdfName.DW);
            if (dw != null)
            {
                Console.WriteLine($"{indentStr}DW (default width): {dw.IntValue()}");
            }
            else
            {
                Console.WriteLine($"{indentStr}DW (default width): not set (default is 1000)");
            }
            
            // CIDSystemInfo
            var cidSystemInfo = cidFont.Get(PdfName.CIDSystemInfo);
            if (cidSystemInfo != null)
            {
                Console.WriteLine($"{indentStr}CIDSystemInfo: {GetValueInfo(cidSystemInfo)}");
            }
            
            // FontDescriptor
            var fontDescriptor = cidFont.GetAsDictionary(PdfName.FontDescriptor);
            if (fontDescriptor != null)
            {
                Console.WriteLine($"{indentStr}FontDescriptor in CIDFont:");
                DiagnoseFontDescriptor(fontDescriptor, indent + 1);
            }
        }
        
        private void DiagnoseFontDescriptor(PdfDictionary fontDescriptor, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            
            Console.WriteLine($"{indentStr}FontName: {fontDescriptor.GetAsName(PdfName.FontName)?.GetValue() ?? "null"}");
            
            // FontFile2 (TrueType)
            var fontFile2 = fontDescriptor.Get(PdfName.FontFile2);
            if (fontFile2 != null)
            {
                Console.WriteLine($"{indentStr}✓ FontFile2: {GetValueInfo(fontFile2)}");
            }
            
            // FontFile3 (CFF/Type1C)
            var fontFile3 = fontDescriptor.Get(PdfName.FontFile3);
            if (fontFile3 != null)
            {
                Console.WriteLine($"{indentStr}✓ FontFile3: {GetValueInfo(fontFile3)}");
            }
            
            // CIDSet
            var cidSet = fontDescriptor.Get(PdfName.CIDSet);
            if (cidSet != null)
            {
                Console.WriteLine($"{indentStr}CIDSet: {GetValueInfo(cidSet)}");
            }
            
            // Метрики
            var flags = fontDescriptor.GetAsNumber(PdfName.Flags);
            if (flags != null)
            {
                Console.WriteLine($"{indentStr}Flags: {flags.IntValue()} (binary: {Convert.ToString(flags.IntValue(), 2)})");
            }
        }
        
        private string GetValueInfo(PdfObject obj)
        {
            if (obj == null) return "null";
            
            if (obj is PdfIndirectReference r)
                return $"IndirectRef({r.GetObjNumber()}.{r.GetGenNumber()})";
            if (obj is PdfStream stream)
                return $"Stream[{stream.GetBytes()?.Length ?? 0} bytes]";
            if (obj is PdfArray arr)
                return $"Array[{arr.Size()}]";
            if (obj is PdfDictionary dict)
                return $"Dictionary[{dict.Size()} keys]";
            if (obj is PdfName name)
                return $"Name({name.GetValue()})";
            if (obj is PdfNumber num)
                return $"Number({num.GetValue()})";
            if (obj is PdfString str)
                return $"String({str.GetValue()})";
            if (obj is PdfBoolean b)
                return $"Boolean({b.GetValue()})";
            
            return $"{obj.GetType().Name}";
        }
    }
}
