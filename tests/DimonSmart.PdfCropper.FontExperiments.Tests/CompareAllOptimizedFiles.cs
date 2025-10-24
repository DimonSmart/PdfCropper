using NUnit.Framework;

namespace DimonSmart.PdfCropper.FontExperiments.Tests;

public class CompareAllOptimizedFiles
{
    [Test]
    public void CompareAllVersions()
    {
        string testPdfPath = @"P:\pdf3\Ladders.pdf";
        
        var files = new Dictionary<string, string>
        {
            { "Original", testPdfPath },
            { "Ladders_Optimized_Test", @"P:\pdf3\Ladders_Optimized_Test.pdf" },
            { "Ladders_Optimized_WithCID", @"P:\pdf3\Ladders_Optimized_WithCID.pdf" },
            { "Ladders_DiagnosticTest", @"P:\pdf3\Ladders_DiagnosticTest.pdf" }
        };

        Console.WriteLine("\n=== File Size Comparison ===\n");
        
        var originalSize = new FileInfo(testPdfPath).Length;
        Console.WriteLine($"{"File",-30} {"Size (MB)",-15} {"vs Original",-15} {"Status"}");
        Console.WriteLine(new string('-', 80));

        foreach (var file in files)
        {
            if (File.Exists(file.Value))
            {
                var size = new FileInfo(file.Value).Length;
                var sizeMB = size / 1024.0 / 1024.0;
                var diff = size - originalSize;
                var diffMB = diff / 1024.0 / 1024.0;
                var percent = (diff * 100.0) / originalSize;
                
                string status = diff == 0 ? "Same" : diff > 0 ? "Larger" : "Smaller";
                
                Console.WriteLine($"{file.Key,-30} {sizeMB,10:F2} MB   {diffMB,+8:F2} MB ({percent,+6:F1}%)   {status}");
            }
            else
            {
                Console.WriteLine($"{file.Key,-30} {"NOT FOUND",-15}");
            }
        }
        
        Console.WriteLine("\n" + new string('-', 80));
        Console.WriteLine("\nLegend:");
        Console.WriteLine("  - Original: Base file for comparison");
        Console.WriteLine("  - Ladders_Optimized_Test: From TestApplyGlobalFontDictionary (text broken?)");
        Console.WriteLine("  - Ladders_Optimized_WithCID: From TestCompleteOptimizationWithCidRemapping (text OK)");
        Console.WriteLine("  - Ladders_DiagnosticTest: From DiagnoseTextTransformationIssue");
    }
}
