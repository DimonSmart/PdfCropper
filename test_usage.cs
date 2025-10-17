using DimonSmart.PdfCropper;

// Example of using the new PdfCropper class name
class TestUsage
{
    public static async Task TestPdfCropper()
    {
        // Now we can use PdfCropper directly - much cleaner!
        var pdfCropper = new PdfCropper();
        
        byte[] inputPdf = await File.ReadAllBytesAsync("input.pdf");
        
        var cropSettings = new CropSettings(
            method: CropMethod.ContentBased,
            excludeEdgeTouchingObjects: true,
            margin: 0.5f
        );
        
        var optimizationSettings = new PdfOptimizationSettings(
            compressionLevel: 9,
            targetPdfVersion: PdfCompatibilityLevel.Pdf20,
            enableFullCompression: true,
            enableSmartMode: true,
            removeUnusedObjects: true,
            removeXmpMetadata: true,
            clearDocumentInfo: true,
            removeEmbeddedStandardFonts: true
        );
        
        byte[] croppedPdf = await pdfCropper.CropAsync(
            inputPdf,
            cropSettings,
            optimizationSettings
        );
        
        await File.WriteAllBytesAsync("output.pdf", croppedPdf);
    }
}