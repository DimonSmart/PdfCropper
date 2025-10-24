using NUnit.Framework;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace DimonSmart.PdfCropper.FontExperiments.Tests;

[TestFixture]
public class QuickTextExtractTest
{
    [Test]
    public void ExtractTextFromOptimized()
    {
        string path = @"P:\pdf3\Ladders_Optimized_WithCID.pdf";
        
        using (var reader = new PdfReader(path))
        using (var doc = new PdfDocument(reader))
        {
            var text = PdfTextExtractor.GetTextFromPage(doc.GetPage(1));
            
            Console.WriteLine($"Text length: {text.Length}");
            Console.WriteLine($"First 500 chars:");
            Console.WriteLine(text.Substring(0, Math.Min(500, text.Length)));
        }
    }
}
