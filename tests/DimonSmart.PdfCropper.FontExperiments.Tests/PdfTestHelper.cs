using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace DimonSmart.PdfCropper.FontExperiments.Tests;

public static class PdfTestHelper
{
    public static void CreateTestPdf(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        
        using var writer = new PdfWriter(filePath);
        using var pdf = new PdfDocument(writer);
        using var document = new Document(pdf);
        
        document.Add(new Paragraph("Test PDF Document"));
        document.Add(new Paragraph("This is a test PDF file with some text content."));
        document.Add(new Paragraph("It contains multiple paragraphs for testing purposes."));
    }
}
