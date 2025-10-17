using System;
using System.IO;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

internal static class PdfXmpCleaner
{
    public static byte[] RemoveXmpMetadata(byte[] pdfBytes, PdfOptimizationSettings optimizationSettings)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(optimizationSettings);

        using var input = new MemoryStream(pdfBytes, writable: false);
        using var output = new MemoryStream();
        using var reader = new PdfReader(input);
        using var writer = new PdfWriter(output, PdfSmartCropper.CreateWriterProperties(optimizationSettings));
        using var document = new PdfDocument(reader, writer);

        document.GetCatalog().Remove(PdfName.Metadata);
        PdfDocumentInfoCleaner.Apply(document, optimizationSettings);
        document.Close();
        return output.ToArray();
    }
}
